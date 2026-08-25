# CRUD Attributes — the Generator-Driven Vertical Slice

How `AutomatedSample`/`Product` builds a full CRUD slice from three attributes instead of the
hand-written files `ManualSample`/`PurchaseOrder` uses. Read
[`docs/samples/manual-vs-automated.md`](samples/manual-vs-automated.md) first — it's the
layer-by-layer comparison of what each shape costs; this page only walks the mechanics of the
generated shape.

## The three attributes

`Minimal.Domains/Features/AutomatedSample/Entities/Product.cs`:

```csharp
[RaisesEvent(EventOperations.Created, Include = [nameof(Id), nameof(Name), nameof(Price)])]
[RaisesEvent(EventOperations.Updated, nameof(Price))]
public class Product : AggregateRoot
{
    [CrudCreate]
    public Product([Required, StringLength(150)] string name, [Range(0.01, double.MaxValue)] decimal price)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
    public bool IsDiscontinued { get; private set; }

    [CrudUpdate]
    public void ChangePrice([Range(0.01, double.MaxValue)] decimal price) => Price = price;
}
```

(`[RaisesEvent]` is covered in [`docs/efcore-events.md`](efcore-events.md); this page focuses on
`[CrudCreate]`/`[CrudUpdate]`/`[GenerateDto]`.)

- **`[CrudCreate]` on the constructor** — the constructor's parameter list *is* the generated create
  request's payload. `DataAnnotations` attributes on each parameter (`[Required]`, `[StringLength]`,
  `[Range]`) are forwarded 1:1 onto the generated request's matching property.
- **`[CrudUpdate]` on a method** — same rule for the method's parameter list. `ChangePrice(decimal
  price)` can only ever produce a request that changes price; a method that needs to change two
  unrelated fields together needs two `[CrudUpdate]` methods (two routes), not one request with two
  fields.
- **`[GenerateDto(typeof(Product))]`** — `Minimal.AppServices/AutomatedSample/V1/ProductDto.cs`:

  ```csharp
  [GenerateDto(typeof(Product))]
  public sealed partial record ProductDto;
  ```

  One line, generates every audited property (`Id`, `Name`, `Price`, `IsDiscontinued`,
  `CreatedBy`/`CreatedOn`/`LastModifiedBy`/`LastModifiedOn`/`UpdatedBy`/`UpdatedOn`) — there's no
  "only expose what I chose" default; narrow with `Exclude`/`Include` on the attribute if a field
  shouldn't ship.

Generated DTOs and `[MapsFrom]`-tagged hand-written DTOs both register with Mapster the same way —
`Minimal.AppServices/Extensions/MapsToExtensions.cs`'s `ScanMaps` reflects over the assembly for
either attribute and calls `config.NewConfig(entityType, dtoType)` for each hit. That scan runs from
`Minimal.AppServices/AppSetup.cs` at startup; no per-feature mapping registration is needed for
either style.

`DKNet.SlimBus.Generators` (the package behind `[CrudCreate]`/`[CrudUpdate]`) emits, per entity,
under `obj/Generated/` (not committed — inspect after a build): the request records
(`CreateProductRequest`, `ChangePriceProductRequest`), their handlers (`CreateProductHandler`,
`ChangePriceProductHandler` in the `Minimal.AppServices.Crud` namespace), and — via
`ProductCrudEndpointExtensions.MapProductCrud()` — the route registrations. What it does **not**
generate, ever: the `IEntityTypeConfiguration<T>` mapping, and any event *consumer* (see
`docs/efcore-events.md`). Those stay hand-written for both samples.

## End-to-end trace, both samples

| Step | `ManualSample`/`PurchaseOrder` | `AutomatedSample`/`Product` |
|---|---|---|
| HTTP endpoint | `Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs` — literal `group.MapPost("/", ...)` etc., one call per route | `Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs` — one `group.MapProductCrud()` call, nothing hand-mapped |
| Validation | `CreatePurchaseOrderCommandValidator`/`UpdatePurchaseOrderCommandValidator` (FluentValidation) run and are enforced on every hand-mapped route | `[Range]`/`[Required]` on the `[CrudCreate]`/`[CrudUpdate]` parameters are forwarded onto the generated request but **not evaluated** — `MapProductCrud` routes through `DKNet.AspCore.Extensions`'s generic `Map*<TRequest,TDto>` wrapper, which the .NET 10 minimal-API validation source generator can't see through. A negative price returns `201`, not `400` |
| Bus dispatch | `IMessageBus bus` → `bus.Send(req, ...)` in the endpoint delegate | Same `IMessageBus`, dispatched inside the generated route delegate |
| Handler | `CreatePurchaseOrderCommandHandler`/`UpdatePurchaseOrderCommandHandler` (`Minimal.AppServices/ManualSample/V1/Actions/`) | Generated `CreateProductHandler`/`ChangePriceProductHandler` (`Minimal.AppServices.Crud` namespace) |
| Domain entity method | `new PurchaseOrder(...)` / `order.ChangeAmount(...)` | `new Product(request.Name, request.Price)` / `product.ChangePrice(request.Price)` |
| Repository / `SaveChanges` | `IRepositorySpec.AddAsync`/implicit update via change tracking, same `CoreDbContext` | Same repository abstraction, same `CoreDbContext` |
| Event | `AddEvent(new PurchaseOrderCreatedEvent(...))` inside the constructor | `[RaisesEvent]`-declared, raised by DKNet's save hook — see `docs/efcore-events.md` |
| Response DTO | Hand-written `PurchaseOrderDto` mapped via `mapper.ResultOf<PurchaseOrderDto>(order)` | Generated `ProductDto` mapped the same way through the `ScanMaps`-registered Mapster config |

The hand-written path's explicit actions — `Minimal.AppServices/ManualSample/V1/Actions/Create.cs`,
`Update.cs`, `Cancel.cs`, `Delete.cs` — are the contrast: `Cancel.cs` rejects an already-cancelled
order with a domain-specific failure, and `Delete.cs` 404s via `NotFoundError` before deleting.
Neither shape is available on the generated path — a generic delete-by-id either deletes the row or
404s, with nowhere to hang a pre-delete rule.

## Data seeding

`IDataSeedingConfiguration<T>` implementations seed reference data on migration. The manual sample
has one — `Minimal.Infra/Features/ManualSample/StaticData/PurchaseOrderStaticData.cs`:

```csharp
internal sealed class PurchaseOrderStaticData : DataSeedingConfiguration<PurchaseOrder>
{
    protected override ValueTask<ICollection<PurchaseOrder>> GetDataAsync(CancellationToken cancellation = new())
        => ValueTask.FromResult<ICollection<PurchaseOrder>>([ /* three fixed-Guid rows */ ]);
}
```

`UseAutoConfigModel`/`UseAutoDataSeeding` pick up every such class (and every
`IEntityTypeConfiguration<T>`) by assembly scan — wired in **both** places that build a
`CoreDbContext`, and both must carry the call for seeding to actually run:

- `Minimal.Infra/Extensions/InfraSetup.cs` → `AddInfraServices` (the DI-registered context the
  running app uses)
- `Minimal.Infra/Extensions/InfraMigration.cs` → `MigrateDb` (the startup-migration path)

Missing the call in either one is a real bug this template already hit once — seeding worked from
one path and silently not the other. The caveat that still stands: **no test fixture in this repo
wires `.UseAutoDataSeeding(...)`.** Neither the xUnit `Support.ApiFixture` nor the BDD
`BddApiFactory` calls it — that wiring exists only in `InfraMigration.MigrateDb` and
`InfraSetup.AddInfraServices`, the real app's composition root. The one test that exercises seeded
data for real (`Minimal.App.Tests/Integration/ManualSample/V1/InfraMigrationSeedingTests.cs`) does
so by calling `InfraMigration.MigrateDb` directly against an ephemeral Postgres container — every
other test fixture starts from an empty database.

## Sequences

`[SqlSequence]` on an enum declares a set of named PostgreSQL sequences —
`Minimal.Domains/Share/Sequences.cs`:

```csharp
[SqlSequence]
public enum Sequences
{
    None = 0,

    [Sequence(typeof(int), FormatString = "T{DateTime:yyMMdd}{1:00000}", Max = 99999)]
    Membership = 1
}
```

`ISequenceServices`/its base `SequenceService` (`Minimal.Infra/Services/SequenceService.cs`) wrap
one sequence member each: `NextValueAsync()` calls `dbContext.NextSeqValueWithFormat(sequence)` on
Postgres, formatting the next value with the pattern above (e.g. `T26082500001`), and falls back to
a plain `Guid` when the context isn't Npgsql (so unit tests without a real Postgres don't need one).
Neither `PurchaseOrder` nor `Product` uses a sequence today — this capability exists for a future
generated entity that needs a human-readable, sequential identifier instead of a `Guid`; declare a
new `Sequences` member and a `SequenceService` subclass the same way `IMembershipService`/
`MembershipService` do for `Sequences.Membership`.
