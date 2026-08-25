---
name: dknet-appservices-actions
description: Create CRUD actions (Create/Update/Delete), DTOs, validators, specs, and domain events at the AppServices layer using this project's SlimMessageBus + FluentResults + Mapster pattern. Use after domain entity and EF Core config are ready.
---

# Skill: AppServices Actions (CRUD + Business Logic)

Create the application service layer — request/response DTOs, command handlers, validators, query specifications, and domain events — using SlimMessageBus Fluent patterns.

If you're unsure whether a business rule belongs on the entity or in the handler, or whether a mutation warrants a domain event, read **dknet-ddd-principles** first — this skill covers the handler mechanics, not that judgment.

---

## When to Use

- After completing dknet-domain-entity + dknet-efcore-config
- Adding Create, Update, Delete actions for an entity
- Adding query specifications for filtering/searching
- Publishing domain events after mutations

## Inputs Required

1. **Entity class** (from domain): with all properties
2. **DTO fields**: which properties to expose (or use `[GenerateDto]` for all)
3. **Create fields**: what's required on creation?
4. **Update fields**: what's mutable?
5. **Business rules**: duplicate checks, validation constraints
6. **Events to publish**: what happened notifications?

## DTO Strategy: hand-written vs. generated

This template ships both, side by side, and either is a legitimate choice:

1. **Hand-written response DTO** (`PurchaseOrderDto` — a plain `record` with exactly the fields the
   API should expose, no attribute). Full control over the response shape; you own every property.
2. **Generated response DTO** (`ProductDto` — one line: `[GenerateDto(typeof(Product))] public sealed
   partial record ProductDto;`). Generates every audited property on the entity by default
   (`Id`, `Name`, `Price`, `CreatedBy`, `CreatedOn`, `LastModifiedBy`, ... ) — use `Exclude`/`Include`
   to narrow, or accept that every caller sees the full audit trail.
3. **Hand-written request records are the norm for hand-mapped actions** in this template — see
   `CreatePurchaseOrderRequest`/`UpdatePurchaseOrderRequest`/`CancelPurchaseOrderRequest`/
   `DeletePurchaseOrderRequest` in `Actions/`. None of them is a mechanical 1:1 projection of the
   entity: `CreatePurchaseOrderRequest` carries `ByUser` (via `[FromClaim(ClaimTypes.Name)]`, never
   trusted from the client payload), `CustomerName`, and `Amount` — no `Status`, no `Id`.
4. **Generated request records** exist only for an entity using `[CrudCreate]`/`[CrudUpdate]` (see
   "Generated alternative" below) — there is no hand-written request/validator/handler for those at all.

Neither sample uses a `[MapsFrom(...)]` attribute on its request records — `PurchaseOrder`'s handlers
construct the entity directly (`new PurchaseOrder(request.CustomerName, request.Amount, request.ByUser)`)
rather than `mapper.Map<PurchaseOrder>(request)`. Reach for `[MapsFrom]` + `mapper.Map<TEntity>(request)`
only when the request is a genuine 1:1 field-for-field projection of the entity's constructor — for a
request with a subset of fields plus an acting-user claim, constructing the entity directly is simpler
and is what the codebase's own example does.

---

## Project Conventions (from actual codebase)

### Core Pattern: SlimMessageBus Fluent Handlers

This project does NOT use custom repository interfaces or service classes. Instead:

- **Commands** implement `Fluents.Requests.IWitResponse<TDto>` (with response) or `Fluents.Requests.INoResponse` (without)
- **Handlers** implement `Fluents.Requests.IHandler<TRequest, TResponse>` or `Fluents.Requests.IHandler<TRequest>`
- **Data access** uses `IRepositorySpec` (injected) — a generic spec-based repository
- **Queries** use `Specification<TEntity>` pattern
- **Mapping** uses `Mapster` via `IMapper`; `[MapsFrom(typeof(Entity))]` is available for a request that's a genuine 1:1 entity projection, but neither sample in this template uses it — both construct the entity directly or call an entity mutation method
- **Results** use `FluentResults` — `Result.Ok(dto)`, `Result.Fail<T>("message")`
- **Lazy mapping**: `mapper.ResultOf<TDto>(entity)` — maps AFTER SaveChanges

### Acting-User Property

There is no shared request base class in this template. Each request that needs the acting user
declares its own property directly, decorated with `[FromClaim(ClaimTypes.Name)]`:

```csharp
[FromClaim(ClaimTypes.Name)]
public string? ByUser { get; set; }
```

`AddContextualRequestPopulation` (wired in `Program.cs`) fills this in from the authenticated
`ClaimsPrincipal` before validation and before the handler runs — it is the only mechanism that
sets `ByUser`; no endpoint stamps it by hand. It only falls back to `SharedConsts.SystemAccount`
when `RequireAuthorization` is off. An authenticated caller whose token carries no
`ClaimTypes.Name` claim is left with `ByUser` unset, which is why every `*CommandHandler` in
`ManualSample/V1/Actions/` checks `string.IsNullOrEmpty(request.ByUser)` and fails the request if
it's missing — that guard is the live no-claim path, not defensive dead code. This mechanism only
works for a hand-written request; a **generated** `[CrudCreate]`/`[CrudUpdate]` request can never
carry a `[FromClaim]` property, because the generator forwards only
`System.ComponentModel.DataAnnotations` attributes (see "Generated alternative" below).

### File Locations

```
src/ApiEndpoints/Minimal.AppServices/
├── {Feature}/
│   └── V{N}/
│       ├── {Entity}Dto.cs                  ← Response DTO (hand-written record, or [GenerateDto])
│       ├── Actions/
│       │   ├── Create.cs                   ← Request + Validator + Handler
│       │   ├── Update.cs                   ← Request + Validator + Handler
│       │   ├── Cancel.cs / other transitions← Request (+ Validator) + Handler, one per business action
│       │   └── Delete.cs                   ← Request + Handler
│       ├── Specs/
│       │   └── SpecGet{Entity}.cs          ← Query specification
│       ├── Queries/
│       │   └── Get{Entity}ById.cs, List{Entity}s.cs ← Query + Handler pairs
│       └── Events/
│           └── {Event}Handlers.cs          ← In-memory subscriber for a domain event
├── Share/
│   └── IPrincipalProvider.cs               ← DO NOT MODIFY
├── Extensions/
│   └── MapsFromAttribute.cs                ← DO NOT MODIFY (available, unused by either sample)
└── GlobalUsings.cs                         ← Global imports (Fluents, FluentResults, etc.)
```

Lazy mapping (`mapper.ResultOf<T>(entity)` / `mapper.LazyMap<T>()`) comes from the `DKNet.SlimBus.Extensions`
package now — there is no local `LazyMapper/` folder to edit.

### Global Usings (already available)

```csharp
global using DKNet.SlimBus.Extensions;      // Fluents.Requests, Fluents.Queries, etc.
global using System.ComponentModel.DataAnnotations;
global using System.Text.Json.Serialization;
global using FluentResults;
global using FluentValidation;
global using Mapster;
global using MapsterMapper;
```

---

## Step-by-Step

### Step 1: Create Response DTO

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/{Entity}Dto.cs`. Either shape is valid —
pick hand-written when you want to control exactly which fields the API exposes (mirrors
`PurchaseOrderDto`), or generated when "everything audited" is acceptable (mirrors `ProductDto`):

```csharp
// Hand-written — see Minimal.AppServices/ManualSample/V1/PurchaseOrderDto.cs
namespace Minimal.AppServices.{Feature}.V1;

public sealed record {Entity}Dto
{
    public Guid Id { get; init; }
    public string {Field1} { get; init; } = null!;
    public decimal {Field2} { get; init; }
    public string CreatedBy { get; init; } = null!;
}
```

```csharp
// Generated — see Minimal.AppServices/AutomatedSample/V1/ProductDto.cs
using DKNet.EfCore.DtoGenerator;

namespace Minimal.AppServices.{Feature}.V1;

[GenerateDto(typeof({Entity}))]
public sealed partial record {Entity}Dto;
```

`[GenerateDto]` auto-generates every audited property from the entity (including `CreatedOn`,
`LastModifiedBy`, etc.) — use `Exclude = [...]`/`Include = [...]` to narrow, or hand-write the record
if you want an explicit allow-list instead of an implicit "everything" default.

### Step 2: Create Action — Create.cs

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Create.cs`, mirroring
`Minimal.AppServices/ManualSample/V1/Actions/Create.cs`:

```csharp
using DKNet.EfCore.Specifications.Repositories;
using Minimal.Domains.Features.{Feature}.Entities;

namespace Minimal.AppServices.{Feature}.V1.Actions;

/// <summary>
/// Command to create a new {entity}.
/// </summary>
public sealed record Create{Entity}Request : Fluents.Requests.IWitResponse<{Entity}Dto>
{
    #region Properties

    /// <summary>
    /// Gets or sets the identity of the acting user. Always overwritten by the endpoint from the
    /// authenticated caller — a payload value is never trusted.
    /// </summary>
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    [Required]
    [StringLength({max}, MinimumLength = 1)]
    public string {RequiredField1} { get; set; } = null!;

    public decimal {RequiredField2} { get; set; }

    #endregion
}

internal sealed class Create{Entity}CommandValidator : AbstractValidator<Create{Entity}Request>
{
    public Create{Entity}CommandValidator()
    {
        RuleFor(a => a.{RequiredField1}).NotEmpty().Length(1, {max});
        RuleFor(a => a.{RequiredField2}).GreaterThan(0);
    }
}

/// <summary>
/// Handles <see cref="Create{Entity}Request" /> by constructing the aggregate — which raises its
/// own domain event in the constructor — and persisting it.
/// </summary>
internal sealed class Create{Entity}CommandHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Requests.IHandler<Create{Entity}Request, {Entity}Dto>
{
    public async Task<IResult<{Entity}Dto>> OnHandle(
        Create{Entity}Request request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ByUser))
        {
            return Result.Fail<{Entity}Dto>("The caller is not authenticated.");
        }

        var entity = new {Entity}(request.{RequiredField1}, request.{RequiredField2}, request.ByUser);

        await repository.AddAsync(entity, cancellationToken);

        // Lazy mapping — resolves AFTER SaveChanges, so generated/audit fields are populated.
        return mapper.ResultOf<{Entity}Dto>(entity);
    }
}
```

The request carries only the writable subset (plus `ByUser`) — not `Id`, not `Status`. The handler
constructs the entity directly; there's no `mapper.Map<{Entity}>(request)` step because the
constructor already raises the created event itself (see `dknet-domain-entity`).

### Step 3: Create Action — Update.cs

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Update.cs`, mirroring
`Minimal.AppServices/ManualSample/V1/Actions/Update.cs`:

```csharp
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Minimal.AppServices.{Feature}.V1.Specs;

namespace Minimal.AppServices.{Feature}.V1.Actions;

/// <summary>
/// Command that changes the {mutable field} of an existing {entity}.
/// </summary>
public sealed record Update{Entity}Request : Fluents.Requests.IWitResponse<{Entity}Dto>
{
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    public Guid Id { get; init; }

    public decimal {MutableField} { get; init; }
}

internal sealed class Update{Entity}CommandValidator : AbstractValidator<Update{Entity}Request>
{
    public Update{Entity}CommandValidator()
    {
        // Id comes from the route, not the body — an unknown/empty Id is a 404 from the spec
        // lookup below, not a validation error. Don't add a NotEmpty rule on Id here.
        RuleFor(a => a.{MutableField}).GreaterThan(0);
    }
}

internal sealed class Update{Entity}CommandHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Requests.IHandler<Update{Entity}Request, {Entity}Dto>
{
    public async Task<IResult<{Entity}Dto>> OnHandle(
        Update{Entity}Request request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ByUser))
        {
            return Result.Fail<{Entity}Dto>("The caller is not authenticated.");
        }

        var entity = await repository.FirstOrDefaultAsync(new SpecGet{Entity}(request.Id), cancellationToken);

        if (entity is null)
        {
            return Result.Fail<{Entity}Dto>(new NotFoundError($"The {entity} {request.Id} was not found."));
        }

        entity.Change{MutableField}(request.{MutableField}, request.ByUser);

        return Result.Ok(mapper.Map<{Entity}Dto>(entity));
    }
}
```

A real bug this pattern already hit once: don't add a `NotEmpty()` validator rule on `Id` when `Id`
is bound from the route — route-value binding happens after FluentValidation's auto-validation runs,
so the rule would 400 on a value that hasn't been patched in yet. Let an unknown/empty id 404 from
the spec lookup instead (fixed in `UpdatePurchaseOrderRequest`, see `docs/samples/manual-vs-automated.md`).

### Step 4: Create Action — a business-rule transition (Cancel.cs)

Not every action is Create/Update/Delete. `Minimal.AppServices/ManualSample/V1/Actions/Cancel.cs`
shows the shape for a state-transition action with a guard, the pattern to reach for whenever a
mutation should be rejected under some existing condition:

```csharp
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Minimal.AppServices.{Feature}.V1.Specs;
using Minimal.Domains.Features.{Feature}.Entities;

namespace Minimal.AppServices.{Feature}.V1.Actions;

public sealed record Cancel{Entity}Request : Fluents.Requests.IWitResponse<{Entity}Dto>
{
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    public required Guid Id { get; init; }
}

internal sealed class Cancel{Entity}CommandHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Requests.IHandler<Cancel{Entity}Request, {Entity}Dto>
{
    public async Task<IResult<{Entity}Dto>> OnHandle(
        Cancel{Entity}Request request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ByUser))
        {
            return Result.Fail<{Entity}Dto>("The caller is not authenticated.");
        }

        var entity = await repository.FirstOrDefaultAsync(new SpecGet{Entity}(request.Id), cancellationToken);

        if (entity is null)
        {
            return Result.Fail<{Entity}Dto>(new NotFoundError($"The {entity} {request.Id} was not found."));
        }

        if (entity.Status == {Entity}Status.Cancelled)
        {
            return Result.Fail<{Entity}Dto>($"The {entity} {request.Id} is already cancelled.");
        }

        entity.Cancel(request.ByUser);

        return Result.Ok(mapper.Map<{Entity}Dto>(entity));
    }
}
```

The "already cancelled" guard lives in the **handler**, not on `Cancel()` itself — see
`dknet-ddd-principles` for why that's the right layer for a cross-cutting business rule check.

### Step 5: Create Action — Delete.cs

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Delete.cs`, mirroring
`Minimal.AppServices/ManualSample/V1/Actions/Delete.cs`:

```csharp
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Minimal.AppServices.{Feature}.V1.Specs;

namespace Minimal.AppServices.{Feature}.V1.Actions;

public sealed record Delete{Entity}Request : Fluents.Requests.INoResponse
{
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    public required Guid Id { get; init; }
}

internal sealed class Delete{Entity}CommandHandler(IRepositorySpec repository)
    : Fluents.Requests.IHandler<Delete{Entity}Request>
{
    public async Task<IResultBase> OnHandle(Delete{Entity}Request request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ByUser))
        {
            return Result.Fail("The caller is not authenticated.");
        }

        var entity = await repository.FirstOrDefaultAsync(new SpecGet{Entity}(request.Id), cancellationToken);

        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"The {entity} {request.Id} was not found."));
        }

        repository.Delete(entity);

        return Result.Ok();
    }
}
```

### Step 7: Create Query Specification

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Specs/SpecGet{Entity}.cs`, mirroring
`Minimal.AppServices/ManualSample/V1/Specs/SpecGetPurchaseOrder.cs`:

```csharp
using DKNet.EfCore.Specifications.Definitions;
using Minimal.Domains.Features.{Feature}.Entities;

namespace Minimal.AppServices.{Feature}.V1.Specs;

internal sealed class SpecGet{Entity} : Specification<{Entity}>
{
    public SpecGet{Entity}(Guid? byId = null, string? by{Field} = null)
    {
        var predicator = CreatePredicate();

        if (byId is not null)
        {
            predicator = predicator.And(a => a.Id == byId);
        }

        if (!string.IsNullOrEmpty(by{Field}))
        {
            predicator = predicator.And(a => a.{Field} == by{Field});
        }

        if (byId is null && string.IsNullOrEmpty(by{Field}))
        {
            // An unstarted predicate builder compiles to WHERE FALSE — without this, "no filter"
            // would silently match nothing instead of listing every row. A real bug this pattern
            // hit once; see docs/samples/manual-vs-automated.md.
            predicator = predicator.And(_ => true);
        }

        WithFilter(predicator);
    }
}
```

Add a paired `Get{Entity}ById.cs` / `List{Entity}s.cs` query+handler pair under `Queries/` when you
need a read side beyond what the spec's filter alone provides — see
`Minimal.AppServices/ManualSample/V1/Queries/GetPurchaseOrderById.cs` and `ListPurchaseOrders.cs` for
the `Fluents.Queries.IHandler<TQuery, TDto>` / `IPageHandler<TQuery, TDto>` shapes.

### Step 8: Add Entity to GlobalUsings (if frequently referenced)

Edit `src/ApiEndpoints/Minimal.AppServices/GlobalUsings.cs`:

```csharp
global using Minimal.Domains.Features.{Feature}.Entities;
```

---

## Reference: PurchaseOrder Actions (actual production code)

### Create Pattern
- `CreatePurchaseOrderRequest : Fluents.Requests.IWitResponse<PurchaseOrderDto>` — `ByUser` via `[FromClaim(ClaimTypes.Name)]`, `CustomerName` `[Required][StringLength(200)]`, `Amount`
- `CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderRequest>` — `NotEmpty().Length(1,200)` on `CustomerName`, `GreaterThan(0)` on `Amount`
- `CreatePurchaseOrderCommandHandler(IRepositorySpec, IMapper) : Fluents.Requests.IHandler<CreatePurchaseOrderRequest, PurchaseOrderDto>`
- Flow: check `ByUser` is present → `new PurchaseOrder(request.CustomerName, request.Amount, request.ByUser)` (constructor raises `PurchaseOrderCreatedEvent` itself) → `repository.AddAsync` → `mapper.ResultOf<PurchaseOrderDto>(order)`

### Update Pattern
- `UpdatePurchaseOrderRequest : Fluents.Requests.IWitResponse<PurchaseOrderDto>` — `Id`, `Amount`
- Handler: check `ByUser` → fetch via `SpecGetPurchaseOrder` → 404 (`NotFoundError`) on miss → `order.ChangeAmount(request.Amount, request.ByUser)` → `Result.Ok(mapper.Map<PurchaseOrderDto>(order))`

### Cancel Pattern (business-rule transition)
- `CancelPurchaseOrderRequest : Fluents.Requests.IWitResponse<PurchaseOrderDto>` — `Id`
- Handler: fetch via spec → 404 on miss → fail with a plain message if `order.Status == PurchaseOrderStatus.Cancelled` → `order.Cancel(request.ByUser)` → return mapped DTO

### Delete Pattern
- `DeletePurchaseOrderRequest : Fluents.Requests.INoResponse` — `Id`
- Handler: check `ByUser` → fetch via spec → 404 on miss → `repository.Delete(order)` → `Result.Ok()`

### Alternative: generated CRUD (`[CrudCreate]`/`[CrudUpdate]`)

For an entity like `Product` that declares `[CrudCreate]` on its constructor and `[CrudUpdate]` on a
mutation method, **this entire layer is generated, not hand-written** — there is no `Actions/`
folder, no validator, no handler class in source for create/update at all. The
`DKNet.SlimBus.Generators` analyzer produces `CreateProductRequest`/`ChangePriceProductRequest`
(requests) and `CreateProductHandler`/`ChangePriceProductHandler` (handlers) in the
`Minimal.AppServices.Crud` namespace, inspectable only after a build (not committed to source).
`GetById`/`GetList`/`Delete` skip even that — they map straight to `DKNet.AspCore.Extensions`'s
generic `MapGetById<TEntity,TKey,TDto>`/`MapGetList`/`MapDeleteById`, so there's no per-entity query,
spec, or handler class for those regardless of generator use.

**The gap that matters most if you pick this path**: a DataAnnotations attribute on a `[CrudCreate]`/
`[CrudUpdate]` parameter (e.g. `[Range(0.01, double.MaxValue)]` on `Product.Price`) is forwarded onto
the generated request property but is **not enforced** — confirmed live, `POST /v1/products` with a
negative price returns `201`. See `docs/samples/manual-vs-automated.md` for the full explanation
(the .NET 10 validation source generator can't see through the generic `Map*<TRequest,TDto>` wrapper
these routes are registered through). Pick the hand-written shape above whenever a request needs
validation that must actually run.

---

## Validation Checklist

- [ ] Response DTO is a hand-written `record` (full control) or uses `[GenerateDto(typeof(Entity))]` (generates every audited property)
- [ ] Create request implements `Fluents.Requests.IWitResponse<{Dto}>`
- [ ] Create/Update/Cancel/Delete requests that need the acting user declare `[FromClaim(ClaimTypes.Name)] public string? ByUser { get; set; }` directly — no shared base class
- [ ] Update request implements `Fluents.Requests.IWitResponse<{Dto}>`
- [ ] Delete request implements `Fluents.Requests.INoResponse`
- [ ] Every handler defensively checks `string.IsNullOrEmpty(request.ByUser)` and fails if missing
- [ ] Validators are `internal sealed` and extend `AbstractValidator<T>`
- [ ] Handlers are `internal sealed` with primary constructor injection
- [ ] Handlers use `IRepositorySpec` (not custom repos)
- [ ] Create handler checks duplicates via Specification before adding, only when the feature has a uniqueness rule (`PurchaseOrder` has none — not every Create needs this check)
- [ ] Create handler uses `mapper.ResultOf<T>()` for lazy mapping
- [ ] Update handler fetches entity, calls mutation method, returns mapped DTO
- [ ] Delete handler returns `IResultBase` (not `IResult<T>`)
- [ ] Domain events are `sealed record` types
- [ ] Event handlers implement `Fluents.EventsConsumers.IHandler<T>`
- [ ] Spec class is `internal sealed` extending `Specification<T>`
- [ ] Namespace follows `Minimal.AppServices.{Feature}.V1.Actions`
- [ ] `dotnet build src/DKNet.Templates.sln -c Release` passes

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Creating custom `IRepository` interface | Use `IRepositorySpec` — it's already registered |
| Using `record struct` for requests | Use `record` (reference type) — needed for bus serialization |
| Making handlers `public` | Must be `internal sealed` |
| Reaching for `[MapsFrom]` + `mapper.Map<TEntity>(request)` when the request isn't a 1:1 projection | Construct the entity directly from request fields, as `CreatePurchaseOrderCommandHandler` does — simpler when the request only carries a subset of constructor params plus `ByUser` |
| Using `Result.Ok(entity)` instead of `mapper.ResultOf<T>()` on Create | Lazy mapping ensures the DTO reflects post-SaveChanges state (audit fields, generated Id) |
| Skipping the `string.IsNullOrEmpty(request.ByUser)` guard | Every hand-written handler in this template checks it and fails the request rather than trusting an empty claim |
| Adding a `NotEmpty()` validator rule on a route-bound `Id` | Route values patch in after auto-validation runs — let an unknown/empty id 404 from the spec lookup instead (see `UpdatePurchaseOrderRequest`) |
| Hand-writing a validator/handler for an entity that already has `[CrudCreate]`/`[CrudUpdate]` | That layer is generated — hand-writing one defeats the point and won't be wired to the generated route |

---

## Next Steps

After creating AppServices actions, proceed to:
→ **dknet-endpoint-config** skill to expose these actions as REST API endpoints

For the judgment behind business-rule placement and domain event usage, see **dknet-ddd-principles**.
