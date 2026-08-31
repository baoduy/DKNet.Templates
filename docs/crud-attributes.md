# CRUD Attributes — the Generator-Driven Vertical Slice

How `AutomatedSample`/`Product` builds a full CRUD slice from four attributes instead of the
hand-written files `ManualSample`/`PurchaseOrder` uses. Read
[`docs/samples/manual-vs-automated.md`](samples/manual-vs-automated.md) first — it's the
layer-by-layer comparison of what each shape costs; this page only walks the mechanics of the
generated shape.

## The four attributes

`Minimal.Domains/Features/AutomatedSample/Entities/Product.cs`:

```csharp
[RaisesEvent(EventOperations.Created, Include = [nameof(Id), nameof(Name), nameof(Price)])]
[RaisesEvent(EventOperations.Updated, nameof(Price))]
[RaisesEvent(EventOperations.Updated, nameof(IsDiscontinued))]
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

    [CrudAction("approval")]
    public void Approve(string byUser) => SetUpdatedBy(byUser);

    [CrudAction(Verb = CrudActionVerb.Put)]
    public void Discontinue() => IsDiscontinued = true;
}
```

(`[RaisesEvent]` is covered in [`docs/efcore-events.md`](efcore-events.md); this page focuses on
`[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`[GenerateDto]`.)

- **`[CrudCreate]` on the constructor** — the constructor's parameter list *is* the generated create
  request's payload. `DataAnnotations` attributes on each parameter (`[Required]`, `[StringLength]`,
  `[Range]`) are forwarded 1:1 onto the generated request's matching property.
- **`[CrudUpdate]` on a method** — same rule for the method's parameter list. `ChangePrice(decimal
  price)` can only ever produce a request that changes price. A method that needs to change two
  unrelated fields together needs two `[CrudUpdate]` methods (two routes), not one request with two
  fields.
- **`[CrudAction]` on a method** — the same parameter-list rule once more, but it publishes a
  *domain action* instead of an update: a `POST` (by default) at the entity's by-id route plus one
  extra segment. `Approve` and `Discontinue` above are the sample's two.
  [Full walkthrough below](#domain-actions-with-crudaction).
- **`[GenerateDto(typeof(Product))]`** — `Minimal.AppServices/AutomatedSample/V1/ProductDto.cs`:

  ```csharp
  [GenerateDto(typeof(Product))]
  public sealed partial record ProductDto;
  ```

  One line, and it generates every audited property (`Id`, `Name`, `Price`, `IsDiscontinued`,
  `CreatedBy`/`CreatedOn`/`LastModifiedBy`/`LastModifiedOn`/`UpdatedBy`/`UpdatedOn`). There's no
  "only expose what I chose" default — narrow with `Exclude`/`Include` on the attribute if a field
  shouldn't ship.

Generated DTOs and `[MapsFrom]`-tagged hand-written DTOs both register with Mapster the same way.
`Minimal.AppServices/Extensions/MapsToExtensions.cs`'s `ScanMaps` reflects over the assembly for
either attribute and calls `config.NewConfig(entityType, dtoType)` for each hit. That scan runs from
`Minimal.AppServices/AppSetup.cs` at startup; no per-feature mapping registration is needed for
either style.

`DKNet.SlimBus.Generators` (the package behind `[CrudCreate]`/`[CrudUpdate]`) emits three things per
entity, under `obj/Generated/` (not committed to source control — inspect the output after a
build):

- The request records: `CreateProductRequest`, `ChangePriceProductRequest`,
  `ApproveProductRequest`, `DiscontinueProductRequest`.
- Their handlers, in the `Minimal.AppServices.Crud` namespace: `CreateProductHandler`,
  `ChangePriceProductHandler`, `ApproveProductHandler`, `DiscontinueProductHandler`.
- The route registrations, via `ProductCrudEndpointExtensions.MapProductCrud()`.

It never generates the `IEntityTypeConfiguration<T>` mapping, or any event *consumer* (see
[`docs/efcore-events.md`](efcore-events.md)). Those stay hand-written for both samples.

## Domain actions with `[CrudAction]`

`[CrudCreate]` and `[CrudUpdate]` cover "make one" and "change the value the caller sends". A
**domain action** is the third shape: a named business operation on a row that already exists —
approve it, discontinue it, publish it. `[CrudAction]` on a public entity method generates the
request, the handler and the route for that operation, so you write the method and nothing else.

Mechanically it behaves like `[CrudUpdate]`: the method's parameter list *is* the generated
request's payload, and `DataAnnotations` attributes on those parameters are forwarded 1:1 onto the
generated request's properties. What differs is what gets published.

| | `[CrudUpdate]` | `[CrudAction]` |
|---|---|---|
| Verb | `PUT` | `POST` by default, overridable to `PUT` or `PATCH` |
| Route | the entity's by-id route | the entity's by-id route **plus one segment** |
| That segment | — | the method name kebab-cased, or the string you pass |
| Response | `200` + the entity DTO | `200` + the entity DTO — **not** `204` |
| Unknown id | `404` via `NotFoundError` | `404` via `NotFoundError` |

The attribute lives in `DKNet.EfCore.Abstractions.Attributes` — the same namespace as
`[CrudCreate]` and `[CrudUpdate]`, so an entity already using those needs no new `using`.

### Declaring an action

Annotate a public method. That is the whole declaration:

```csharp
[CrudAction]
public void Discontinue() => IsDiscontinued = true;
```

The method name kebab-cases into the route segment, so this publishes:

```
POST /v1/products/{id}/discontinue   →  200 + ProductDto
```

A parameterless action generates a request carrying only the route's `id`. Give the method
parameters and they become the body, exactly as they do for an update:

```csharp
[CrudAction]
public void Approve(string byUser) => SetUpdatedBy(byUser);
```

```
POST /v1/products/{id}/approve
{ "byUser": "alice" }                →  200 + ProductDto
```

The generated request and handler follow the same `<Method><Entity>Request` /
`<Method><Entity>Handler` convention as the update pair — `ApproveProductRequest`,
`ApproveProductHandler` — in the `Minimal.AppServices.Crud` namespace. They are compiler output
under `obj/Generated/`, not files in the repo; build once and read them there. Set
`[CrudAction(Name = "...")]` if you need a different request type name.

### Overriding the route segment

Pass the segment as the attribute's constructor argument when the kebab-cased method name is not
the noun you want in the URL:

```csharp
[CrudAction("approval")]
public void Approve(string byUser) => SetUpdatedBy(byUser);
```

```
POST /v1/products/{id}/approval
```

This is what the sample does — the operation stays `Approve` in the domain, while the URL reads as
the resource `approval`. Pass a bare segment (`"approval"`), not a path: it is appended to the
entity's by-id route, never substituted for it.

### Overriding the verb

Set `Verb`:

```csharp
[CrudAction(Verb = CrudActionVerb.Put)]
public void Discontinue() => IsDiscontinued = true;
```

```
PUT /v1/products/{id}/discontinue
```

`CrudActionVerb` offers exactly three members — **`Post` (the default), `Put` and `Patch`**. There
is no `Delete`; if you are looking for one, the generated `DELETE /{id}` route from
`MapDeleteById` is the only delete the generator publishes, and a "soft delete" is a domain action
like any other (`Discontinue` above is precisely that). Segment and verb are independent — set
either, both, or neither:

```csharp
[CrudAction("archived", Verb = CrudActionVerb.Patch)]   // PATCH .../{id}/archived
```

### Action or update? Pick by whether repeating the call is safe

Both annotations run a method on an existing row, so the choice is not about mechanics. It is about
what you are promising the caller:

- **`[CrudUpdate]`** — the caller supplies a value and the row ends up holding that value.
  Sending it twice leaves the same result as sending it once. That is what `PUT` means, and
  `ChangePrice(decimal)` is the honest case: price becomes `9.99` however many times you call it.
- **`[CrudAction]`** — a business operation whose repetition is not automatically safe. Approving
  an already-approved order, re-issuing a refund, re-publishing a document: whether the second call
  is harmless is a question about *your domain*, not one the shape of the request answers. `POST`
  is the default verb precisely because it makes no promise the generator cannot keep.

This sample is its own cautionary tale. `Approve` was originally declared `[CrudUpdate]`, so the
template published a business action as `PUT /v1/products/{id}` — advertising to every client
generated against it that approval was safe to retry. Nothing about the code was wrong; the
*contract* was. Moving it to `[CrudAction("approval")]` did not change what `Approve` does, only
what the API promises about calling it twice.

Rule of thumb: if you would be uncomfortable with a client's retry-on-timeout policy replaying the
call unattended, it is an action, not an update.

> **What an action does not give you.** Everything the generated create/update path gives up, an
> action gives up too — the [validation gap](samples/manual-vs-automated.md#1-request-validation-that-looks-wired-but-never-runs-the-sharpest-gap)
> (`DataAnnotations` on an action's parameters are forwarded onto the request and never evaluated),
> no idempotency filter, and the DTO's every-audited-field default. One is worth calling out
> specifically: **a generated action has nowhere to hang a pre-condition.** `Discontinue` on an
> already-discontinued product returns `200`, not a domain failure — a generated handler runs the
> method and saves. The manual sample's `Cancel.cs`, which rejects an already-cancelled order with
> a domain-specific 400, is still the shape you need when the pre-condition must be enforced, and
> still means hand-writing that one route.

### Worked example: add your own action

Say the sample needs to archive a product — take it off the catalogue without deleting the row —
and you want it published as `PATCH /v1/products/{id}/archived`. Everything needed is in
`Product.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;   // [CrudCreate], [CrudUpdate], [CrudAction], CrudActionVerb

public class Product : AggregateRoot
{
    // ... existing members ...

    public bool IsArchived { get; private set; }
    public string? ArchiveReason { get; private set; }

    /// <summary>Takes the product off the catalogue.</summary>
    [CrudAction("archived", Verb = CrudActionVerb.Patch)]
    public void Archive([StringLength(500)] string reason)
    {
        IsArchived = true;
        ArchiveReason = reason;
    }
}
```

Build, and the generator has published:

| | |
|---|---|
| Verb & route | `PATCH /v1/products/{id}/archived` |
| Request body | `{ "reason": "..." }` — from the method's parameter list |
| Response | `200` + `ProductDto` |
| Unknown id | `404` |
| Generated types | `ArchiveProductRequest`, `ArchiveProductHandler` (`obj/Generated/`) |

Drop the `"archived"` argument and the route segment becomes `archive`. Drop `Verb` and it becomes
a `POST`. No endpoint registration, no request record and no handler is written by hand —
`ProductV1Endpoint`'s single `MapProductCrud()` call already picks the new route up.

> `Archive` is an exercise for this page. It is **not** part of the shipped sample, which carries
> exactly two actions — `Approve` and `Discontinue`. Add it to your own copy, not to the template's.

## End-to-end trace, both samples

| Step | `ManualSample`/`PurchaseOrder` | `AutomatedSample`/`Product` |
|---|---|---|
| HTTP endpoint | `Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs` — literal `group.MapPost("/", ...)` etc., one call per route | `Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs` — one `group.MapProductCrud()` call, nothing hand-mapped |
| Validation | `CreatePurchaseOrderCommandValidator`/`UpdatePurchaseOrderCommandValidator` (FluentValidation) run and are enforced on every hand-mapped route | `[Range]`/`[Required]` on the `[CrudCreate]`/`[CrudUpdate]` parameters are forwarded onto the generated request but **not evaluated** — `MapProductCrud` routes through `DKNet.AspCore.Extensions`'s generic `Map*<TRequest,TDto>` wrapper, which the .NET 10 minimal-API validation source generator can't see through. A negative price returns `201`, not `400` |
| Bus dispatch | `IMessageBus bus` → `bus.Send(req, ...)` in the endpoint delegate | Same `IMessageBus`, dispatched inside the generated route delegate |
| Handler | `CreatePurchaseOrderCommandHandler`/`UpdatePurchaseOrderCommandHandler` (`Minimal.AppServices/ManualSample/V1/Actions/`) | Generated `CreateProductHandler`/`ChangePriceProductHandler` (`Minimal.AppServices.Crud` namespace) |
| Domain entity method | `new PurchaseOrder(...)` / `order.ChangeAmount(...)` | `new Product(request.Name, request.Price)` / `product.ChangePrice(request.Price)` |
| Repository / `SaveChanges` | `IRepositorySpec.AddAsync`/implicit update via change tracking, same `CoreDbContext` | Same repository abstraction, same `CoreDbContext` |
| Event | `AddEvent(new PurchaseOrderCreatedEvent(...))` inside the constructor | `[RaisesEvent]`-declared, raised by DKNet's save hook — see [`docs/efcore-events.md`](efcore-events.md) |
| Response DTO | Hand-written `PurchaseOrderDto` mapped via `mapper.ResultOf<PurchaseOrderDto>(order)` | Generated `ProductDto` mapped the same way through the `ScanMaps`-registered Mapster config |

The hand-written path's explicit actions — `Minimal.AppServices/ManualSample/V1/Actions/Create.cs`,
`Update.cs`, `Cancel.cs`, `Delete.cs` — are the contrast. `Cancel.cs` rejects an already-cancelled
order with a domain-specific failure, and `Delete.cs` 404s via `NotFoundError` before deleting.
Neither shape is available on the generated path: a generic delete-by-id either deletes the row or
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
`IEntityTypeConfiguration<T>`) by assembly scan. This is wired in **both** places that build a
`CoreDbContext`, and both must carry the call for seeding to actually run:

- `Minimal.Infra/Extensions/InfraSetup.cs` → `AddInfraServices` (the DI-registered context the
  running app uses)
- `Minimal.Infra/Extensions/InfraMigration.cs` → `MigrateDb` (the startup-migration path)

Missing the call in either one is a real bug this template already hit once: seeding worked from
one path and silently not the other.

One caveat still stands: **no test fixture in this repo wires `.UseAutoDataSeeding(...)`.** Neither
the xUnit `Support.ApiFixture` nor the BDD `BddApiFactory` calls it. That wiring exists only in
`InfraMigration.MigrateDb` and `InfraSetup.AddInfraServices`, the real app's composition root. The
one test that exercises seeded data for real,
`Minimal.App.Tests/Integration/ManualSample/V1/InfraMigrationSeedingTests.cs`, calls
`InfraMigration.MigrateDb` directly against an ephemeral Postgres container. Every other test
fixture starts from an empty database.

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
one sequence member each. `NextValueAsync()` calls `dbContext.NextSeqValueWithFormat(sequence)` on
Postgres, formatting the next value with the pattern above (e.g. `T26082500001`). It falls back to
a plain `Guid` when the context isn't Npgsql, so unit tests without a real Postgres don't need one.

Neither `PurchaseOrder` nor `Product` uses a sequence today. This capability exists for a future
generated entity that needs a human-readable, sequential identifier instead of a `Guid`. Declare a
new `Sequences` member and a `SequenceService` subclass the same way `IMembershipService`/
`MembershipService` do for `Sequences.Membership`.
</content>
</invoke>
