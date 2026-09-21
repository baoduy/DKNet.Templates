---
name: dknet-queries-specs
description: Write read/query logic for a DKNet feature — Specification<T> filters, hand-written query requests/handlers dispatched over IMessageBus, and the generic filter/search/order/page list route every generator-driven CRUD slice gets for free. Use after the domain entity and DTO exist, whenever a feature needs anything more than the default GET-by-id.
---

# Skill: Queries and Specifications

Covers the read side of a feature: specifications, hand-written queries (`mode=manual`), and the
generic list route every `[CrudCreate]`-declared entity gets automatically (`mode=auto`).

## 1. Specifications — why, and the WHERE-FALSE trap

Handlers never build a LINQ query against `CoreDbContext` directly. They ask `IRepositorySpec` for
`FirstOrDefaultAsync(spec, ct)`, `AnyAsync(spec, ct)`, `Query(spec)`, or
`ToPagedListAsync(spec, pageIndex, pageSize, ct)`, passing a `Specification<TEntity>`
(`DKNet.EfCore.Specifications.Definitions`). A spec is a reusable, named, unit-testable filter — the
same predicate isn't hand-rolled differently in every handler that needs it.

```csharp
internal sealed class SpecGetPurchaseOrder : Specification<PurchaseOrder>
{
    public SpecGetPurchaseOrder(Guid? byId = null, string? byCustomerName = null)
    {
        var predicator = CreatePredicate();

        if (byId is not null)
            predicator = predicator.And(a => a.Id == byId);

        if (!string.IsNullOrEmpty(byCustomerName))
            predicator = predicator.And(a => a.CustomerName == byCustomerName);

        if (byId is null && string.IsNullOrEmpty(byCustomerName))
            // An unstarted predicate builder compiles to WHERE FALSE — without this, "no filter"
            // would silently match nothing instead of listing every order.
            predicator = predicator.And(_ => true);

        WithFilter(predicator);
    }
}
```

(`Minimal.AppServices/ManualSample/V1/Specs/SpecGetPurchaseOrder.cs`; the automated sample's
`SpecGetProduct` and `SpecProductByName` follow the same shape.)

- **`CreatePredicate()`** with no `.And(...)`/`.Or(...)` ever called compiles to `WHERE FALSE`. Any
  spec whose constructor can be called with *no* filter argument at all needs an explicit
  `.And(_ => true)` fallback in that branch, or an unfiltered "list everything" call silently returns
  zero rows. `SpecGetProduct` hits the same trap with a plain `else` branch instead of a combined `if`
  — same fix, either shape works.
- **`WithFilter(predicate)`** is what actually attaches the compiled predicate to the specification;
  building `predicator` without ever calling it is a no-op spec.
- **Specs live in `<Feature>/V1/Specs/`, `internal sealed`** — `SpecGetPurchaseOrder`,
  `SpecGetProduct`, `SpecProductByName` all follow this.
- **Specs are reused outside query handlers, too.** `SpecProductByName` (`Minimal.AppServices/
  AutomatedSample/V1/Specs/SpecProductByName.cs`) exists purely so `CreateProductRequestValidator` can
  check for a duplicate name before create — a spec is not only a query-handler concern.
- **Unit-test a spec without EF Core or a database**: compile `.FilterQuery!.Compile()` into a plain
  `Func<TEntity, bool>` and assert against in-memory instances (`SpecGetPurchaseOrderTests.cs` — see
  [Testing pointers](#5-testing-pointers)).

## 2. Hand-written queries (`mode=manual`)

A query is a record implementing one of two `SlimBus.Extensions.Fluents.Queries` interfaces,
dispatched from the endpoint via `IMessageBus.Send(...)` exactly like a command.

### Single result — `IWitResponse<TDto>` / `IHandler<TQuery,TDto>`

```csharp
public sealed record GetPurchaseOrderByIdQuery : Fluents.Queries.IWitResponse<PurchaseOrderDto>
{
    public required Guid Id { get; init; }
}

internal sealed class GetPurchaseOrderByIdQueryHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Queries.IHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto>
{
    public async Task<PurchaseOrderDto?> OnHandle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await repository.FirstOrDefaultAsync(new SpecGetPurchaseOrder(request.Id), cancellationToken);
        return order is null ? null : mapper.Map<PurchaseOrderDto>(order);
    }
}
```

`OnHandle` returns `TDto?`, not a `Result`. A `null` return is the "not found" signal — the calling
endpoint turns it into `404` (`dto is null ? Results.NotFound() : Results.Ok(dto)`; see the
`dknet-endpoint-config` skill). There is no failure channel beyond that; a query handler doesn't
refuse with an error code the way a command handler does.

### Paged list — `IWitPageResponse<TDto>` / `IPageHandler<TQuery,TDto>`

```csharp
public sealed record ListPurchaseOrdersQuery : Fluents.Queries.IWitPageResponse<PurchaseOrderDto>
{
    public const int DefaultPageIndex = 1;
    public const int DefaultPageSize = 20;

    // Nullable so [AsParameters] leaves these `null` (not the CLR default 0) when the caller never
    // supplied the query parameter — distinguishing "not supplied" from an explicit pageSize=0.
    public int? PageIndex { get; init; }
    public int? PageSize { get; init; }
    public string? CustomerName { get; init; }
}

internal sealed class ListPurchaseOrdersQueryValidator : AbstractValidator<ListPurchaseOrdersQuery>
{
    public ListPurchaseOrdersQueryValidator()
    {
        RuleFor(a => a.PageSize).InclusiveBetween(1, 100).When(a => a.PageSize.HasValue);
        RuleFor(a => a.PageIndex).GreaterThan(0).When(a => a.PageIndex.HasValue);
    }
}

internal sealed class ListPurchaseOrdersQueryHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Queries.IPageHandler<ListPurchaseOrdersQuery, PurchaseOrderDto>
{
    public async Task<IPagedList<PurchaseOrderDto>> OnHandle(ListPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var spec = new SpecGetPurchaseOrder(byCustomerName: request.CustomerName);
        var pageIndex = request.PageIndex ?? ListPurchaseOrdersQuery.DefaultPageIndex;
        var pageSize = request.PageSize ?? ListPurchaseOrdersQuery.DefaultPageSize;
        var page = await repository.ToPagedListAsync(spec, pageIndex, pageSize, cancellationToken);
        return new StaticPagedList<PurchaseOrderDto>(page.Select(mapper.Map<PurchaseOrderDto>), page);
    }
}
```

(`Minimal.AppServices/ManualSample/V1/Queries/GetPurchaseOrderById.cs`,
`ListPurchaseOrders.cs`.)

- **Nullable paging parameters + `[AsParameters]` + a co-located validator with `.When(x =>
  x.PageSize.HasValue)`** is the pattern: it lets an omitted parameter fall back to the query's own
  declared default (`DefaultPageIndex`/`DefaultPageSize`), while an explicit out-of-range value
  (`pageSize=0`) still fails validation instead of silently falling back.
- **`DefaultPageSize`/the `1..100` bound are this query's own choices**, unrelated to the generic list
  route's `1000`/`DKNet:ListQuery` defaults (§3) — a hand-written query keeps whatever numbers you
  pick regardless of the package's own configuration.
- **`X.PagedList`'s `StaticPagedList<TDto>`** rewraps the projected DTO page so page metadata (total
  count, page index, page size) survives the entity-to-DTO conversion — the handler pages the entity
  query first, then projects, never the other way around.

### Aggregate/summary queries over `repository.Query(spec)`

Not every read is "one row" or "a page of rows". `ProductPriceSummaryQuery`
(`Minimal.AppServices/AutomatedSample/V1/Queries/ProductPriceSummary.cs`) computes a count and an
average directly against the queryable a spec produces:

```csharp
public sealed record ProductPriceSummaryDto(int ProductCount, decimal AveragePrice);
public sealed record ProductPriceSummaryQuery : Fluents.Queries.IWitResponse<ProductPriceSummaryDto>;

internal sealed class ProductPriceSummaryQueryHandler(IRepositorySpec repository)
    : Fluents.Queries.IHandler<ProductPriceSummaryQuery, ProductPriceSummaryDto>
{
    public async Task<ProductPriceSummaryDto?> OnHandle(ProductPriceSummaryQuery request, CancellationToken cancellationToken)
    {
        var products = repository.Query(new SpecGetProduct());
        var count = await products.CountAsync(cancellationToken);
        var averagePrice = count == 0 ? 0m : await products.AverageAsync(p => p.Price, cancellationToken);
        return new ProductPriceSummaryDto(count, averagePrice);
    }
}
```

`repository.Query(spec)` hands back an `IQueryable<TEntity>` with the spec's filter already applied —
use it for `CountAsync`/`AverageAsync`/`GroupBy`/anything that isn't "one entity" or "one page of
entities". This query has no dedicated DTO-projection step because the response isn't shaped like the
entity at all.

## 3. The generic list route (generator-driven, `mode=auto`)

Every entity with `[CrudCreate]`/`[CrudUpdate]` gets a `GET /` list route for free through
`Map{Entity}Crud()` — no handler, validator, or query object written by hand. It comes from
`MapGetList<TEntity,TKey,TDto>()` in `DKNet.AspCore.Extensions`; you never call it directly, the
generator emits the call.

**This is a separate contract from §2 — the two don't share defaults, limits, or behavior.**

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `pageNumber` | int | `1` | `< 1` is clamped to `1`, never rejected. |
| `pageSize` | int | `1000` | `< 1` falls back to `1000`; ceiling `1000` (host-configurable) — clamped, not rejected. |
| `filter` | repeatable | none | `field:operation:value`, ANDed across repeats. Max 20. |
| `search` | string | none | Free-text `Contains`, OR'd across every string DTO field. Min 2 chars. |
| `orderBy` | string | none | One DTO field name; `Id` appended as a descending tie-break. |
| `desc` | bool | `false` | Reverses `orderBy`. |
| `fromDate`/`toDate` | ISO-8601 | none | Inclusive bounds on last-activity (`CreatedOn` or `UpdatedOn`); see below. |

### Filter operations

`Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Contains`,
`NotContains`, `StartsWith`, `EndsWith`, `In`, `NotIn` (comma-separated value list), `IsNull`,
`IsNotNull` (two-segment form, no value: `field:IsNull`). `Field` is normalized to PascalCase, matched
case-insensitively, and only the **first two** colons split the triple — a value may itself contain
colons (`filter=CreatedOn:GreaterThan:2026-01-31T00:00:00Z`).

```
GET /v1/products?filter=Price:GreaterThan:100&filter=IsDiscontinued:Equal:false
```

### Recent-activity window

`fromDate`/`toDate` bound when a record was **last active** — either its creation or its last update
moment falling inside the window counts. Neither bound given, over an audited entity (every
`AggregateRoot` is): the listing covers the **last three months** by default, not all history. Naming
either bound replaces the default entirely (open-ended on the side you left out) —
`?fromDate=0001-01-01T00:00:00Z` is the documented way to ask for all history. `fromDate` later than
`toDate` is a `400`. Configurable via `DKNet:ListQuery:DefaultActivityWindowMonths` (`0` disables the
default window).

### Configuring the defaults

`DKNet:ListQuery` config section: `DefaultPageSize` (`1000`), `MaxPageSize` (`1000`, the ceiling an
explicit `pageSize` clamps to and the cap on the default), `DefaultActivityWindowMonths` (`3`).

### Response envelope

`200 OK` with `PagedResponse<TDto>{ Items, PageCount, PageNumber, PageSize, TotalItemCount,
HasNextPage, HasPreviousPage }`, built from `X.PagedList` via `ToPagedListAsync(...)` — `TotalItemCount`
is always the full unpaged count.

### Error behavior

Everything malformed is `400`, never silently dropped or ignored: an unknown filter/order field, an
unparseable filter triple, a value that won't coerce to the field's CLR type, more than 20 filters, a
`search` under 2 characters, `fromDate` after `toDate`. Only paging is clamped instead of rejected.

### The DTO is the boundary

Filter, search, and order fields resolve against the **DTO** (`TModel`), never the raw entity — a
deliberate security boundary. `ProductDto` excludes `OwnedBy`
(`[GenerateDto(..., Exclude = [nameof(Product.OwnedBy), ...])]`), so `?filter=ownedBy:Equal:x` is a
`400`, not a leak of a hidden column. Widen or narrow the query surface by changing the DTO's
`Exclude`/`Include`, never the endpoint — see the `dknet-dto-mapping` skill for how a DTO's shape is
declared.

**Trap: a queryable DTO field must map to a real column.** Filter/search/order build EF predicates by
property name against the *entity*. A DTO field the entity declares but does not map (`[NotMapped]`,
or a computed property like `AuditedEntity<TKey>.LastModifiedBy`/`LastModifiedOn`) passes the "is this
field on the DTO" check and then fails to translate — a **500**, not a `400`, and search hits it on
the very first `?search=` call since search touches every string field. A DTO field with *no* entity
counterpart at all (`ProductDto.GrossMargin`, hand-mapped via a Mapster `IRegister` — see the
`dknet-dto-mapping` skill) is caught earlier and refused with a clean `400` naming the field instead.
Either way: `Exclude` a computed/unmapped member from `[GenerateDto]` rather than let the list route
inherit a latent failure.

### Behavioral spec: `ProductList.feature`

`Minimal.App.BDDTests/Features/Products/ProductList.feature` is the regression fence for this whole
contract — it exists specifically because nothing in the slice is hand-written, so a future package
bump could silently change behavior. Representative scenarios:

- paging envelope carries the *unpaged* `totalItemCount`; `pageSize` above the max is clamped to
  `1000`, not rejected;
- `orderBy=price&desc=true` sorts descending; ascending is the default;
- `filter=price:GreaterThan:20` and two ANDed filters both narrow correctly; `filter=name:In:Apple,Cherry`
  matches any listed value;
- `search=rico` matches `Apricot` (substring, not prefix); a one-character search is `400`;
- `filter=colour:Equal:red` (unknown field) and `filter=ownedBy:Equal:someone` (excluded field) are
  both `400` — the second is the DTO-boundary regression fence specifically;
- a `fromDate` before the seeded rows still returns them; a `toDate` before them excludes all of them
  (empty page, not `404`); `fromDate` after `toDate` is `400`;
  `orderBy=grossMargin`/`filter=grossMargin:GreaterThan:0` are `400` naming the field, never a `500`.

## 4. Status counts

`group.MapGetStatusCounts<TEntity>("status", new StatusPropertyInfo(nameof(X.Status), typeof(XStatus)))`
(`Minimal.Api/Configs/Endpoints/StatusCountsEndpointMapperExtensions.cs`) is template-local — not part
of the published `DKNet.AspCore.Extensions` package. It groups rows by an enum-backed property using
`ModelSpecStatusCounts<TEntity>` (`Minimal.AppServices/Share/Generics/ModelSpecGenericStatusCounts.cs`):

```csharp
public class ModelSpecStatusCounts<TEntity> : Specification<TEntity> where TEntity : DomainEntity
{
    public ModelSpecStatusCounts(GenericStatusCountsParameters parameters)
    {
        var predicate = CreatePredicate(x => true);   // seeded non-empty; no WHERE-FALSE guard needed
        if (parameters.From is { } from) predicate = predicate.And(x => x.CreatedOn >= from);
        if (parameters.To is { } to) predicate = predicate.And(x => x.CreatedOn <= to);
        WithFilter(predicate);
    }
}
```

`StatusPropertyInfo(string Name, Type EnumType)` names the property to group by and its enum type;
`GetStatusCounts<TEntity>` backfills every enum member with a zero count when the database has none,
so a caller always sees the full status set. `GenericStatusCountsParameters.From`/`To` are both
optional and **unbounded by default** — omitting both reports counts over all history, not a rolling
window; pass explicit bounds for something like "the last 30 days". No shipped endpoint config calls
this today; wire it into a `Map(RouteGroupBuilder)` like any other route (see the
`dknet-endpoint-config` skill) if a status breakdown is useful for your entity.

## 5. Decision table — generic list vs. hand-written query

| Need | Use |
|---|---|
| Filter/sort/search only ever touches fields already on the generated DTO | Generic list route (§3) — free |
| A filter on a field the DTO must *not* expose (ownership key, an internal flag) | Neither — exclude it from the DTO; don't build a query to reach it |
| A join across two entities, or a projection the DTO can't express | Hand-written query (§2) |
| Custom paging defaults/limits different from `1000`/`DKNet:ListQuery` | Hand-written query (§2) — the generic route's defaults aren't overridable per-feature |
| An aggregate value (count, average, grouped totals) instead of a list of rows | Hand-written query over `repository.Query(spec)` (§2) |
| The query needs the acting user (e.g. "orders I created") | Hand-written query — the generic list route has no request shape to carry a `[FromClaim]` member |
| A field the DTO exposes only as a computed/unmapped value needs to be searchable | Neither works — searching/filtering an unmapped field 500s; add a real mapped column or drop the requirement |

## 6. Testing pointers

- **Spec unit tests** — compile the spec's `.FilterQuery!.Compile()` and assert against
  hand-constructed entities, no EF Core or database involved
  (`Minimal.App.Tests/Unit/ManualSample/SpecGetPurchaseOrderTests.cs`): a "no filter matches
  everything" case is the WHERE-FALSE regression guard, plus one case per optional filter argument and
  one for combining them.
- **List/paging integration tests** — exercise the real HTTP route against `ApiFixture`
  (`Minimal.App.Tests/Integration/ManualSample/V1/PurchaseOrderListPagingTests.cs`): the shape to copy
  is asserting the *declared default* is served when a nullable paging parameter is omitted (not just
  "200 OK"), and asserting an out-of-range value produces the same `ValidationProblemDetails` shape as
  every other validation failure, not a `500`.
- Full test-writing guidance (fixtures, `IMessageBus` test dispatch, assertion style): load the
  `dknet-unit-tests` skill.

## Common mistakes

| What you might expect | What actually happens | Why |
|---|---|---|
| A spec with no filter argument supplied lists every row | It returns zero rows | `CreatePredicate()` with nothing ever `.And`'d compiles to `WHERE FALSE` — add an explicit `.And(_ => true)` fallback |
| The generic list route and a hand-written list query share page-size defaults/limits | They don't — `1000`/`DKNet:ListQuery` vs. whatever the hand-written query declares (often `20`/`1..100`) | Two independent contracts; see §2 vs §3 |
| Excluding a DTO field only hides it from JSON output | It also makes the field unqueryable through the generic list route | Filter/search/order resolve against the DTO, not the entity — this is a security boundary, not a display choice |
| A computed DTO property (`LastModifiedBy`, a hand-mapped derived field) is safe to leave on a `[GenerateDto]` | The first `?search=` against it throws a `500` (unmapped) or a clean `400` (no entity counterpart at all) | Filter/search/order build EF predicates by property name against the entity; `Exclude` computed members |
| Omitting `pageSize` on a hand-written list query falls back to `0` | It falls back to the query's own declared default, and an explicit `pageSize=0` still `400`s | Nullable paging properties + `[AsParameters]` + a validator gated on `.HasValue` — see §2 |
| `fromDate`/`toDate` narrow an *unaudited* entity's listing | They're silently ignored | The recent-activity window only applies to entities carrying `CreatedOn`/`UpdatedOn` |
| No `fromDate`/`toDate` means "all history" on the generic list route | It means "the last three months" (or whatever `DefaultActivityWindowMonths` is configured to) | The default window applies unless you explicitly widen it with `fromDate=0001-01-01T00:00:00Z` |
