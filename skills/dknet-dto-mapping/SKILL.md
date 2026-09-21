---
name: dknet-dto-mapping
description: Design response DTOs and Mapster mapping for a DKNet.Minimal feature — hand-written vs [GenerateDto] shapes, custom Mapster IRegister mappings for values the generator's convention can't produce, LazyMapper, and the JSON/sensitive-data contract. Use after (or alongside) dknet-crud when a handler needs a DTO to return.
---

# DTO and Mapster mapping

Response DTO shape and how it gets filled from the entity. For request contracts, validators and
handlers, load `dknet-crud`. For paged query projections, load `dknet-queries-specs`.

## Two DTO shapes

**Hand-written** — a plain record, exactly the fields you list:

```csharp
// ManualSample/V1/PurchaseOrderDto.cs
public sealed record PurchaseOrderDto
{
    public Guid Id { get; init; }
    public string CustomerName { get; init; } = null!;
    public decimal Amount { get; init; }
    public PurchaseOrderStatus Status { get; init; }
    public string CreatedBy { get; init; } = null!;
}
```

This carries **no attribute at all**, yet `mapper.Map<PurchaseOrderDto>(order)` and
`mapper.ResultOf<PurchaseOrderDto>(order)` both work correctly. Mapster's global convention
(`TypeAdapterConfig.GlobalSettings.Default`, configured once in `AppSetup.cs`) maps any two types by
flexible name matching with no registration required — a hand-written DTO whose property names
match the entity's needs nothing further.

**Generated** — one attribute on an empty `partial record`:

```csharp
[GenerateDto(typeof(Product),
    Exclude = [nameof(Product.OwnedBy), nameof(AuditedEntity<Guid>.LastModifiedBy), nameof(AuditedEntity<Guid>.LastModifiedOn)])]
public sealed partial record ProductDto
{
    [SensitiveData("pricing")]
    public decimal? GrossMargin { get; init; }
}
```

`DKNet.EfCore.DtoGenerator` emits every remaining audited property at compile time. What that
produces here, verbatim (`obj/Generated/DKNet.EfCore.DtoGenerator/.../ProductDto.g.cs`):

```csharp
public partial record ProductDto
{
    public required string Name { get; init; }
    public decimal Price { get; init; }
    public bool IsDiscontinued { get; init; }
    [SensitiveData("pricing")] public decimal? SupplierCostPrice { get; init; }
    [SensitiveData] public string? SupplierReferenceCode { get; init; }
    [MaxLength(500)] public required string CreatedBy { get; init; }
    public DateTimeOffset CreatedOn { get; init; }
    [MaxLength(500)] public string? UpdatedBy { get; init; }
    public DateTimeOffset? UpdatedOn { get; init; }
    public Guid Id { get; init; }
}
```

`[SensitiveData]` and `[MaxLength]` on the entity property travel onto the generated one unchanged —
that's how `SupplierCostPrice`/`SupplierReferenceCode` stay gated per caller (see
`docs`'s role-aware filtering, summarized below) without being declared twice. The DTO's own
`GrossMargin` is the file's one hand-written addition, on the *other*, non-generated partial
declaration — the generator's output file is never edited directly.

## `[GenerateDto]` options

```csharp
public GenerateDtoAttribute(Type entityType)
public string[] Exclude { get; set; } = [];          // mutually exclusive with Include
public string[] Include { get; set; } = [];           // only these properties, if set
public bool IgnoreComplexType { get; set; }            // default true: navigation properties dropped
```

`Include` and `Exclude` are mutually exclusive — set one or the other, never both. `Product`'s DTO
uses `Exclude` because it wants "everything except three names"; reach for `Include` when you want
"only these few" instead. `IgnoreComplexType` defaults to `true` (falls through to the MSBuild
property `DtoGeneratorIgnoreComplexType` if set, else `true`): navigation properties to other
entities are dropped automatically unless the target is `[Owned]`. Set it `false` per-DTO to include
navigation properties.

**Why `OwnedBy`, `LastModifiedBy`, `LastModifiedOn` are excluded.** `OwnedBy` duplicates
`CreatedBy` as the same ownership key — excluding it avoids exposing the tenant key twice, and
means it is unqueryable via the generic list route by construction (you cannot filter by ownership
key over HTTP even though the column exists). `LastModifiedBy`/`LastModifiedOn` on
`AuditedEntity<TKey>` are *computed* conveniences (the updated value, or the created one if never
modified) — not mapped columns. Left on a generated DTO, they'd resolve on `GetById` (read once, in
memory) but break the **list** route: its filter/search/order build EF predicates against the entity
by property name, and an unmapped member makes the whole query fail to translate at the database —
a `500`, not a `400`, the first time `?search=` touches it. `UpdatedBy`/`UpdatedOn` stay — they are
real mapped columns covering the same intent.

## Mapster global configuration

`Minimal.AppServices/AppSetup.cs`, run once at startup:

```csharp
TypeAdapterConfig.GlobalSettings.Default.NameMatchingStrategy(NameMatchingStrategy.Flexible);
TypeAdapterConfig.GlobalSettings.Default.MapToConstructor(true);
TypeAdapterConfig.GlobalSettings.Default.PreserveReference(true);
TypeAdapterConfig.GlobalSettings.ScanMaps();
TypeAdapterConfig.GlobalSettings.Compile();

services.AddSingleton(TypeAdapterConfig.GlobalSettings)
        .AddScoped<IMapper, ServiceMapper>();
```

`ScanMaps()` (`Minimal.AppServices/Extensions/MapsToExtensions.cs`) reflects over the assembly for
every type carrying `[MapsFrom(typeof(Entity))]` or `[GenerateDto(typeof(Entity))]` and calls
`config.NewConfig(entityType, dtoType)` for each — this is what makes a generated/`[MapsFrom]`-typed
pair eagerly compiled and validated at startup, rather than resolved lazily by the `Default`
fallback rule on first use. **Ordering matters**: only *after* that loop does it call
`config.Scan(assembly)`, which discovers `IRegister` classes (like `ProductMappingRegister`) and
merges their `ForType` customizations onto the config the loop just built. Reversing the order would
have the convention's `NewConfig` wipe out the `IRegister`'s merge.

`[MapsFrom]` on a hand-written DTO (`Minimal.AppServices.Extensions.MapsFromAttribute`) is **not
required for the mapping to work** — `PurchaseOrderDto` proves that with zero attributes and zero
registration. Reach for it only when you also want to attach a Mapster `IRegister` customization
(a computed property, a rename) to that specific entity/DTO pair: `[MapsFrom]` puts the hand-written
pair through the same `ScanMaps()` → `NewConfig` → `IRegister`-merge pipeline `[GenerateDto]` already
gets, so a custom `ForType` call has a config to merge onto instead of relying on the untyped
`Default` fallback.

## Custom response mapping

A value the naming convention can't derive — anything computed from more than one column — is a
hand-written property on the DTO's `partial record` plus an `IRegister`:

```csharp
internal sealed class ProductMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config) =>
        config.ForType<Product, ProductDto>()
            .Map(d => d.GrossMargin, s => s.Price - s.SupplierCostPrice);
}
```

`ForType<TSource, TDest>()` **merges** onto whatever config already exists for that pair — every
convention-mapped property (`Name`, `Price`, the audit columns) stays untouched; only `GrossMargin`
is affected. `NewConfig` would **replace** the pair's whole config instead, silently discarding the
convention mapping — never use `NewConfig` inside an `IRegister` meant to *add* one property.

Keep the mapping expression a plain, EF-translatable expression (`s.Price - s.SupplierCostPrice`,
not a method call or branch Entity Framework can't turn into SQL): on a generated CRUD entity, the
list route projects the DTO directly over `IQueryable<TEntity>`, so every property's mapping
expression must compile to SQL, not just to CLR code. A hand-mapped property is **response-only** —
it has no entity counterpart, so the generic list route refuses to filter or order on it
(`?orderBy=grossMargin` and `?filter=grossMargin:GreaterThan:0` both answer `400` naming the field,
not `500` — the field-existence check catches it before a query is built). Mirror any
`[SensitiveData]` restriction from the source columns onto the derived property if it discloses the
same information — `GrossMargin` carries `[SensitiveData("pricing")]` because it discloses the same
confidential number as `SupplierCostPrice`.

Two tests pin this shape: `Architecture/ProductGrossMarginStructureTests.cs` asserts no hand-written
route, request, or handler type exists anywhere with "GrossMargin" in its name — proving the
customization stayed a mapping concern, not a routing one — and
`Integration/AutomatedSample/V1/ProductGrossMarginTests.cs` proves the value appears correctly on
every generated route's response (create, get, list, each `[CrudUpdate]`/`[CrudAction]`), is `null`
(not omitted) for a caller in the required role when the source is undisclosed, is omitted entirely
for a caller outside that role, and that ordering/filtering by it is refused with `400` while
ordinary fields keep working.

## Other customizations

Still inside an `IRegister`'s `ForType<TSource, TDest>()` chain:

- **Ignore a member**: `.Ignore(d => d.SomeField)` — the DTO keeps the property but Mapster never
  writes it (default value stays). Prefer `Exclude`/`Include` on `[GenerateDto]` when the property
  shouldn't exist on the DTO at all.
- **Rename**: `.Map(d => d.NewName, s => s.OldName)` — same mechanism as `GrossMargin`, one column
  instead of a computed expression; stays EF-translatable.
- **Enums and strings**: convention mapping handles same-named enum-to-enum and enum-to-string by
  member name already; only add a `.Map(...)` when you need a different textual representation than
  the default.
- **Nested/owned types**: convention mapping recurses into an owned type's own properties by name;
  `[GenerateDto(..., IgnoreComplexType = false)]` is what lets a navigation property reach the DTO at
  all.
- **`AfterMapping`**: `.AfterMapping((src, dest) => ...)` runs CLR code after the projection — safe
  for a hand-written DTO read one row at a time (`mapper.Map<TDto>(entity)`), but **not**
  EF-translatable, so it cannot appear in a mapping used by the generic list route's `IQueryable`
  projection. Treat any `AfterMapping` customization as reads-only, never wired to a
  `[GenerateDto]` type that a list route also projects.

## LazyMapper

`DKNet.SlimBus.Extensions.LazyMapper` (namespace already in `GlobalUsings.cs`) gives two extension
methods on `IMapper`:

- `mapper.ResultOf<TDto>(entity)` — wraps `entity` in a **successful** `IResult<TDto>`, mapped only
  when its `.Value` is first read. Use this as a handler's return value right after a write whose
  DTO needs something only `SaveChanges` produces — a database-generated `Id`, `CreatedOn`,
  `CreatedBy`/`OwnedBy` stamped by a save hook. Because the SlimBus EF Core interceptor runs
  synchronously right after `OnHandle` returns and before the framework reads the `Result`'s value
  to build the HTTP response, those values already exist by the time the lazy mapping actually runs.
- `mapper.LazyMap<TValue>(entity)` — the same deferred mapping, without the `IResult` wrapper, for a
  context that isn't returning a `FluentResults` result directly.

For an ordinary read, or a write where nothing on the DTO depends on a post-save value (an amount
change, a status flip), map eagerly instead: `Result.Ok(mapper.Map<TDto>(entity))`.

## Paged projection

```csharp
new StaticPagedList<TDto>(page.Select(mapper.Map<TDto>), page)
```

`page` is the `IPagedList<TEntity>` from `repository.ToPagedListAsync(spec, pageIndex, pageSize, ct)`
(`X.PagedList`); wrapping the mapped items in `StaticPagedList<TDto>` alongside the original page
carries its paging metadata (page number, total count) forward onto the DTO-typed result.

## JSON contract

`Minimal.Share/SharedConsts.JsonSerializerOptions` is the one source of truth for serialization
shape — camelCase property names, nulls omitted (`DefaultIgnoreCondition.WhenWritingNull`), enums as
camelCase strings (`JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`). `ServiceConfigs.AddOptions`
copies these settings onto ASP.NET Core's own `JsonOptions` via `ConfigureHttpJsonOptions`, so a
minimal-API response and this constant never drift apart. The same call site also opts the app into
role-aware `[SensitiveData]` filtering (`UseRoleAwareSensitiveData`, needing
`ISensitiveDataPrincipalAccessor` from DI) — the full caller/role decision table is a
`dknet-auth-and-ownership` concern, not this skill's; what matters here is that it is response-side
JSON serialization only, layered on top of whatever DTO shape and Mapster mapping you built, never a
change to the DTO type itself.

## Decision table

| | Hand-written record | `[GenerateDto]` |
|---|---|---|
| Response shape | Exactly what you list | Every audited property, minus `Exclude` |
| New entity property | Invisible until you add it | Appears automatically on next build |
| Query surface (generated list route) | N/A — no generated list route without `[CrudCreate]`/`[GenerateDto]` together | DTO fields are the filter/search/order surface |
| Derived/computed value | Any C# expression, `AfterMapping` included | Only via a hand-written partial member + `IRegister`, and only if EF-translatable |
| Needs `[MapsFrom]`? | Only to attach an `IRegister` customization | N/A — `[GenerateDto]` already opts in |

Pick hand-written when the response should intentionally expose less than the entity, or when a
value needs CLR-only computation (`AfterMapping`, external lookups) and will never back a generated
list route. Pick `[GenerateDto]` when the entity already carries `[CrudCreate]`/`[CrudUpdate]` and
the DTO doubles as the generic CRUD/list contract.

## Common mistakes

- **What you might expect**: adding `NewConfig(typeof(Product), typeof(ProductDto))` inside a new
  `IRegister` is a normal way to add one more mapping rule.
  **What actually happens**: every property `[GenerateDto]`'s convention was already mapping for
  that pair stops working — only what the new `NewConfig` explicitly re-declares survives.
  **Why**: `NewConfig` replaces a pair's whole configuration; `ForType` merges onto it. Inside an
  `IRegister` meant to add to an existing generated or `[MapsFrom]` pair, always use `ForType`.

- **What you might expect**: a DTO member with no matching entity property is harmless as long as
  it's nullable and never populated by a create/update request.
  **What actually happens**: `GET`/`?search=` on the generic list route throws or 500s the first
  time that field participates in a query, or (if the field genuinely doesn't exist on the entity at
  all) the route answers `400` naming it as unsupported.
  **Why**: list-route filter/search/order resolve DTO fields against mapped entity columns; a
  property that's declared-but-unmapped fails to translate (500), and one with no entity
  counterpart at all fails the existence check first (400) — see `LastModifiedBy`/`LastModifiedOn`
  vs `GrossMargin` above for the two cases.

- **What you might expect**: forgetting `partial` on a `[GenerateDto]` record is a harmless typo the
  compiler will catch immediately with a clear message.
  **What actually happens**: it does fail to compile, but as a "partial declarations must have
  matching partial modifiers" error against the generator's own output file, not against your file
  — easy to mis-diagnose as a generator bug.
  **Why**: `[GenerateDto]` emits a second `partial record` declaration under the same name; without
  `partial` on your declaration the two can't merge.

- **What you might expect**: putting an acting-user field (`ByUser`, `CreatedBy`) on a DTO record
  is fine since the entity already carries it.
  **What actually happens**: it maps through unchanged on a **read**, which is fine, but on a DTO
  used as part of a create/update request shape (rare, but seen when a hand-written request and its
  response DTO are conflated) it becomes caller-settable.
  **Why**: DTOs and requests are different concerns — a DTO is response-only; acting-user
  attribution belongs on the request (`dknet-crud`) or a save hook, never inferred
  from a response type reused as input.
