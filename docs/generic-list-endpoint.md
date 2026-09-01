# Generic List Endpoint (filter · search · order · page)

Every generator-driven CRUD slice gets a `GET /` list route for free. It is a single, uniform query
surface — pagination, multi-field filtering, free-text search, and ordering — driven entirely by the
query string, with **no per-feature code to write**. This page is the full contract for that route.

> This is the *automated* path. Hand-written slices (`ManualSample/PurchaseOrder`) instead expose a
> bespoke `ListPurchaseOrdersQuery` with its own hand-picked parameters — see
> [Querying and Specifications](querying-and-specifications.md). The two do not share a contract;
> everything below applies only to routes mapped through the generator.

## Where it comes from

The route is `MapGetList<TEntity, TKey, TModel>()`, part of the **`DKNet.AspCore.Extensions`** NuGet
package (version pinned in `src/Directory.Packages.props`). You never call it directly in this
template. The `DKNet.SlimBus.Generators` source generator emits the call for you inside the generated
`Map<Entity>Crud()` extension whenever an entity carries `[CrudCreate]`/`[CrudUpdate]`.

Worked instance — the automated `Product` sample:

```csharp
// src/ApiEndpoints/Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs
public void Map(RouteGroupBuilder group)
{
    group.MapProductCrud();   // generated → group.MapGetList<Product, Guid, ProductDto>()
}
```

So `GET /v1/products` is a fully capable list endpoint even though no list handler, validator, or
query object was hand-written anywhere in the slice.

Signature (from the package):

```csharp
public RouteHandlerBuilder MapGetList<TEntity, TKey, TModel>(string endpoint = "/")
    where TEntity : class, IEntity<TKey>
    where TKey    : IEquatable<TKey>
    where TModel  : class;

// Guid-key shorthand
public RouteHandlerBuilder MapGetList<TEntity, TModel>(string endpoint = "/")
    where TEntity : class, IEntity<Guid>
    where TModel  : class;
```

`TModel` is the projected DTO (`ProductDto` for the sample). It is central to the whole contract:
**you can only filter, search, and order by fields that exist on the DTO**, never on the raw entity.
See [The DTO is the boundary](#the-dto-is-the-boundary) below.

## Query parameters

| Parameter    | Type          | Default | Notes                                                              |
|--------------|---------------|---------|--------------------------------------------------------------------|
| `pageNumber` | `int`         | `1`     | 1-based. A value `< 1` is silently clamped to `1`.                 |
| `pageSize`   | `int`         | `20`    | A value `< 1` falls back to `20`. Max **100** — larger is clamped down, not rejected. |
| `filter`     | repeatable    | none    | `field:operation:value`. Repeat the param to AND multiple conditions. Max **20**. |
| `search`     | `string`      | none    | Free-text OR across all string DTO fields. Min **2** characters.   |
| `orderBy`    | `string`      | none    | A single DTO field name to sort by.                                |
| `desc`       | `bool`        | `false` | Reverses the `orderBy` direction.                                  |

Paging is always applied. Filter, search, and order are each optional and independent; when present
they are ANDed together (search is one OR-group, then ANDed with the filter predicate).

## Filtering

Each `filter` value is a colon-delimited triple parsed into a `ListFilter(Field, Operation, Value)`.
Repeat the parameter to combine conditions with **AND**:

```
GET /v1/products?filter=Price:GreaterThan:100&filter=IsDiscontinued:Equal:false
```

### Operations

| Operation            | Value form          | Meaning                                          |
|----------------------|---------------------|--------------------------------------------------|
| `Equal`              | scalar              | `field == value`                                 |
| `NotEqual`           | scalar              | `field != value`                                 |
| `GreaterThan`        | scalar              | `field > value`                                  |
| `GreaterThanOrEqual` | scalar              | `field >= value`                                 |
| `LessThan`           | scalar              | `field < value`                                  |
| `LessThanOrEqual`    | scalar              | `field <= value`                                 |
| `Contains`           | string              | `field.Contains(value)`                          |
| `NotContains`        | string              | `!field.Contains(value)`                         |
| `StartsWith`         | string              | `field.StartsWith(value)`                        |
| `EndsWith`           | string              | `field.EndsWith(value)`                          |
| `In`                 | comma-separated     | `value.Contains(field)` — any of the listed values |
| `NotIn`              | comma-separated     | none of the listed values                        |
| `IsNull`             | *(omit value)*      | `field == null` — two-part form `field:IsNull`   |
| `IsNotNull`          | *(omit value)*      | `field != null` — two-part form `field:IsNotNull`|

- **`In` / `NotIn`** take a comma-separated list; each element is coerced to the property's CLR type.
  Example: `filter=Status:In:Active,Pending`.
- **`IsNull` / `IsNotNull`** use the two-segment form with no value: `filter=UpdatedOn:IsNull`.
- The scalar `Value` is coerced to the DTO property's CLR type (int, decimal, bool, Guid, DateTime, …).
  A value that cannot be coerced is a `400`, not a silent no-op.

### Field naming

`Field` is normalised to PascalCase, so `unit_price`, `unit-price`, and `UnitPrice` all resolve to
the same DTO property, matched case-insensitively. The resolved name **must** be a public property
on `TModel`. If it is not, the request fails with `400 Bad Request` — unknown fields are never
silently dropped, because dropping a condition would answer a filtered query with unfiltered data.

Only the **first two** colons split the triple, so a value may contain colons of its own — an
ISO-8601 timestamp being the case that matters:
`filter=CreatedOn:GreaterThan:2026-01-31T00:00:00Z`.

### Limits

- At most **20** filter conditions per request. A 21st is a `400`.

## Ordering

`orderBy` names one DTO field (PascalCased, same rules as filter fields; it must exist on both the DTO
and the entity). `desc=true` sorts descending; omitted or `false` sorts ascending.

```
GET /v1/products?orderBy=Price&desc=true
```

- The unique `Id` is appended as a **descending tie-breaker** so paging is deterministic — unless you
  already ordered by `Id`.
- **Default order (no `orderBy` given):**
  - Audited entities (`IAuditedEntity<TKey>`, which every `AggregateRoot` here is) → `CreatedOn`
    descending, then `Id` descending. Newest first.
  - Non-audited entities → `Id` descending only.
- An unknown `orderBy` field is a `400`, not a silent fallback to the default.

## Search

`search` is a single free-text term matched with `Contains` across **every string property of the
DTO**, OR'd together, then ANDed with any `filter`/default predicate:

```
GET /v1/products?search=widget
```

- **Which fields:** the text properties of `TModel`. As with `filter` and `orderBy`, the DTO is the
  boundary — a column it does not expose is never searched.
- **How deep:** up to 2 levels of properties (`MaxDepth = 2` in `ModelSearch`), so `Name` and
  `Merchant.Name` are searched but `Merchant.Address.City` is not. A collection member is wrapped in
  `Any(...)`; dictionaries and `byte[]` are never descended into or searched.
- **Operator:** substring (`LIKE '%…%'`), not prefix match. Each clause is emitted as
  `Field != null && Field.Contains(term)` — the null guard matters for a provider that evaluates in
  memory.
- **Minimum length:** 2 characters after trimming. A 1-character `search` is a `400`. Blank or
  omitted is treated as absent.
- **Case sensitivity** follows the database collation — no lowercasing is applied in the predicate.
- If the DTO has no text field, `search` matches nothing (an empty page, not an error).

For `ProductDto` the string fields are `Name` plus the mapped audit columns `CreatedBy` / `UpdatedBy`,
so a search hits any of those. (Every searched field must map to a real column — see
[the trap below](#trap-a-dto-field-must-map-to-a-real-column).)

## Response envelope

The route returns `200 OK` with a `PagedResponse<TModel>`:

```csharp
public sealed record PagedResponse<TResult>
{
    public IList<TResult> Items { get; init; } = [];
    public int  PageCount       { get; init; }
    public int  PageNumber      { get; init; }
    public int  PageSize        { get; init; }
    public int  TotalItemCount  { get; init; }
    public bool HasNextPage     { get; init; }
    public bool HasPreviousPage { get; init; }
}
```

`Items` holds the projected DTOs for the current page; the rest is paging metadata. It is built from
`X.PagedList` via the repository's `ToPagedListAsync(...)`, so `TotalItemCount` is the full unpaged
count.

## Error behavior

Every malformed input is a **`400 Bad Request`** with a reason (via `Results.Problem`), never a
silently-ignored parameter:

- filter/order field not on the DTO,
- unparseable filter triple or unknown operation,
- a value that cannot be coerced to the property type,
- more than 20 filter conditions,
- a `search` shorter than 2 characters.

Out-of-range **paging** is the one exception: `pageNumber < 1` and `pageSize` outside `1..100` are
clamped, not rejected.

## The DTO is the boundary

Filter, search, and order fields are resolved against `TModel` (the returned DTO), **never the raw
entity**. This is a deliberate security boundary: a column the DTO doesn't expose cannot be sorted on,
filtered on, or searched — no hidden column leaks through the query surface. Widen or narrow the query
surface by changing what the DTO exposes (`[GenerateDto(... Exclude/Include ...)]`), not the endpoint.

The `Product` sample DTO is generated as:

```csharp
[GenerateDto(typeof(Product),
    Exclude = [nameof(Product.OwnedBy), nameof(AuditedEntity<Guid>.LastModifiedBy), nameof(AuditedEntity<Guid>.LastModifiedOn)])]
public sealed partial record ProductDto;
```

`OwnedBy` is excluded, so it is unqueryable through this route by construction — you cannot filter or
sort products by their ownership key over HTTP, even though the column exists on the entity.

### Trap: a DTO field must map to a real column

Because filter/search/order build EF predicates against the entity **by property name**, every
queryable DTO field has to resolve to a *mapped* entity column. A DTO property whose entity counterpart
is computed or `[NotMapped]` makes the whole query fail to translate — a **500**, not a `400`, and it
fires the moment such a field is touched (search touches *every* string field, so it breaks on the
first search).

This is exactly why `LastModifiedBy` / `LastModifiedOn` are excluded above. On `AuditedEntity<TKey>`
they are computed conveniences ("the updated value, or the created one if never modified"), not columns
— `[GenerateDto]` would otherwise surface them and every `?search=` would `500`. The mapped
`UpdatedBy` / `UpdatedOn` stay and cover the same intent. **When you point `[GenerateDto]` at an entity
with computed or unmapped members, `Exclude` them** or the free list route inherits a latent 500.

## Worked example

```
GET /v1/products
  ?search=widget
  &filter=Price:GreaterThanOrEqual:100
  &filter=IsDiscontinued:Equal:false
  &orderBy=Price
  &desc=true
  &pageNumber=2
  &pageSize=50
```

Reads as: products whose `Name`/`CreatedBy`/`UpdatedBy` contains "widget", priced at 100 or more, not
discontinued, sorted by price descending (with `Id` as tie-break), returning the second page of 50.
