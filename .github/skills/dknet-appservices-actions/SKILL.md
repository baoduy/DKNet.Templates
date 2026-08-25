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

## DTO Strategy (GenerateDto-First)

Default policy for this repository:

1. **Response DTOs**: use `[GenerateDto(typeof(Entity), Exclude = [...])]` by default.
2. **Request records**: use generated DTO shapes **when contract shape matches entity fields** (for example, full update payloads), and only hand-write request records when workflow-specific fields diverge.
3. **Manual request records** are required when any of these apply:
    - server-side/generated fields must be hidden from clients
    - request uses different names/types from entity
    - operation is partial/mutation-specific and not a 1:1 entity projection
4. Match property names 1:1 with the entity wherever possible — Mapster maps by convention with no attribute needed for either current sample; reach for `[MapsFrom(typeof(Entity))]` (`Minimal.AppServices/Extensions/MapsFromAttribute.cs`) only if Mapster can't infer the source type on its own.

This keeps request/response property names consistent with entities and reduces mapping drift.

---

## Project Conventions (from actual codebase)

### Core Pattern: SlimMessageBus Fluent Handlers

This project does NOT use custom repository interfaces or service classes. Instead:

- **Commands** implement `Fluents.Requests.IWitResponse<TDto>` (with response) or `Fluents.Requests.INoResponse` (without)
- **Handlers** implement `Fluents.Requests.IHandler<TRequest, TResponse>` or `Fluents.Requests.IHandler<TRequest>`
- **Data access** uses `IRepositorySpec` (injected) — a generic spec-based repository
- **Queries** use `Specification<TEntity>` pattern
- **Mapping** uses `Mapster` via `IMapper` — plain `mapper.Map<TDto>(entity)`/`mapper.ResultOf<TDto>(entity)`, matched by property name; no per-request attribute is needed for the two current samples
- **Results** use `FluentResults` — `Result.Ok(dto)`, `Result.Fail<T>("message")`
- **Lazy mapping**: `mapper.ResultOf<TDto>(entity)` — maps AFTER SaveChanges

### Acting-user attribution on the request

There is no `RequestBase` class in this codebase. A hand-written request instead declares its own claim-bound property:

```csharp
[FromClaim(ClaimTypes.Name)]
public string? ByUser { get; set; }
```

`AddContextualRequestPopulation` (wired once in `Program.cs`) populates any `[FromClaim(...)]` property before validation and before the handler runs, falling back to `SharedConsts.SystemAccount` only when `RequireAuthorization` is off. See `CreatePurchaseOrderRequest` for the real usage. Every handler in the manual sample still re-checks `string.IsNullOrEmpty(request.ByUser)` defensively before touching the entity — copy that guard.

### File Locations

```
src/ApiEndpoints/Minimal.AppServices/
├── {Feature}/
│   └── V{N}/
│       ├── {Entity}Dto.cs                  ← Response DTO (hand-written record, or [GenerateDto])
│       ├── Actions/
│       │   ├── Create.cs                   ← Request + Validator + Handler
│       │   ├── Update.cs                   ← Request + Validator + Handler
│       │   ├── Cancel.cs                   ← Request + Handler (business-rule guard, no validator needed)
│       │   └── Delete.cs                   ← Request + Handler
│       ├── Specs/
│       │   └── SpecGet{Entity}.cs          ← Query specification
│       └── Events/
│           └── {Event}Handlers.cs          ← Event record + handlers
├── Share/
│   ├── IPrincipalProvider.cs               ← DO NOT MODIFY
│   └── Generics/                           ← Generic list/paged specs
├── Extensions/
│   ├── MapsFromAttribute.cs                ← available if Mapster needs an explicit source-type hint; unused by either sample today
│   └── LazyMapper/                         ← DO NOT MODIFY
└── GlobalUsings.cs                         ← Global imports (Fluents, FluentResults, etc.)
```

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

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/{Entity}Dto.cs`:

```csharp
using DKNet.EfCore.DtoGenerator;

namespace Minimal.AppServices.{Feature}.V1;

[GenerateDto(typeof({Entity}), Exclude = [])]
public sealed partial record {Entity}Dto;
```

`[GenerateDto]` auto-generates all properties from the entity (this is exactly how `ProductDto` is written — see `Minimal.AppServices/AutomatedSample/V1/ProductDto.cs`, a single line). Use `Exclude = ["InternalProp"]` to hide fields; the default is "everything audited", not "only what you chose to expose".

If instead you want full control over which fields leave the process — the manual sample's choice — hand-write the DTO as a plain `record` with exactly the fields you want (see `PurchaseOrderDto`, 5 fields, no `[GenerateDto]`) and skip this step entirely.

### Step 2: Create Action — Create.cs

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Create.cs`:

```csharp
using System.Data;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.{Feature}.V1.Events;
using Minimal.AppServices.{Feature}.V1.Specs;

namespace Minimal.AppServices.{Feature}.V1.Actions;

/// <summary>
/// Command to create a new {entity}.
/// </summary>
public sealed record Create{Entity}Request : Fluents.Requests.IWitResponse<{Entity}Dto>
{
    #region Properties

    /// <summary>
    /// The identity of the acting user. Always overwritten by
    /// <c>AddContextualRequestPopulation</c> from the authenticated caller — a payload value is
    /// never trusted (see <c>CreatePurchaseOrderRequest</c>).
    /// </summary>
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    [Required] public string {RequiredField1} { get; set; } = null!;
    [Required] public string {RequiredField2} { get; set; } = null!;
    public string? {OptionalField} { get; set; }

    #endregion
}

/// <summary>
/// Validator for <see cref="Create{Entity}Request"/>.
/// </summary>
internal sealed class Create{Entity}RequestValidator : AbstractValidator<Create{Entity}Request>
{
    public Create{Entity}RequestValidator()
    {
        RuleFor(a => a.{RequiredField1}).NotEmpty().Length({min}, {max});
        RuleFor(a => a.{RequiredField2}).NotEmpty().EmailAddress().Length(1, {max});
    }
}

/// <summary>
/// Handler: guards on the acting user, optionally checks a business rule, constructs the
/// aggregate (which may raise its own event — see <c>PurchaseOrder</c>), persists, returns the DTO.
/// </summary>
internal sealed class Create{Entity}Handler(IRepositorySpec repository, IMapper mapper)
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

        // Optional: reject a duplicate before constructing the aggregate.
        // if (await repository.AnyAsync(new SpecGet{Entity}(by{UniqueField}: request.{UniqueField}), cancellationToken))
        //     return Result.Fail<{Entity}Dto>($"{UniqueField} {request.{UniqueField}} already exists.");

        var entity = new {Entity}(request.{RequiredField1}, request.{RequiredField2}, request.ByUser);

        await repository.AddAsync(entity, cancellationToken);

        // Lazy-mapped DTO — resolves after SaveChanges, so any DB-computed field is populated.
        return mapper.ResultOf<{Entity}Dto>(entity);
    }
}
```

For `Create*Request`, use a manual record only for the writable subset plus the `[FromClaim]` acting-user property. Do not duplicate server-generated, audit, or immutable entity fields unless the API contract explicitly requires them.

### Step 3: Create Action — Update.cs

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Update.cs`:

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.{Feature}.V1.Specs;

namespace Minimal.AppServices.{Feature}.V1.Actions;

/// <summary>
/// Command that changes a mutable field on an existing {entity}.
/// </summary>
public sealed record Update{Entity}Request : Fluents.Requests.IWitResponse<{Entity}Dto>
{
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    public Guid Id { get; init; }

    public string? {MutableField1} { get; init; }
}

internal sealed class Update{Entity}RequestValidator : AbstractValidator<Update{Entity}Request>
{
    public Update{Entity}RequestValidator()
    {
        // Id comes from the route, not the body — an unknown/empty Id is a 404 from the
        // spec lookup below, not a validation error. Don't add a NotEmpty rule on it.
        RuleFor(a => a.{MutableField1}).NotEmpty();
    }
}

internal sealed class Update{Entity}Handler(IRepositorySpec repository, IMapper mapper)
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
            return Result.Fail<{Entity}Dto>(new NotFoundError($"The {Entity} {request.Id} was not found."));
        }

        // Call the entity's named mutation method — see dknet-domain-entity
        entity.Change{MutableField1}(request.{MutableField1}, request.ByUser);

        return Result.Ok(mapper.Map<{Entity}Dto>(entity));
    }
}
```

Keep the `Id` off the validator's rules entirely — `UpdatePurchaseOrderRequest`'s validator only checks `Amount`, precisely because a stale/missing `Id` check on the request body would run before the route value is patched in (see the "two real bugs" section of `docs/samples/manual-vs-automated.md`).

### Step 4: Create Action — Delete.cs

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Delete.cs`:

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.{Feature}.V1.Specs;

namespace Minimal.AppServices.{Feature}.V1.Actions;

/// <summary>
/// Command to delete a {entity} by ID.
/// </summary>
public sealed record Delete{Entity}Request : Fluents.Requests.INoResponse
{
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    public required Guid Id { get; init; }
}

internal sealed class Delete{Entity}Handler(IRepositorySpec repository)
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
            return Result.Fail(new NotFoundError($"The {Entity} {request.Id} was not found."));
        }

        repository.Delete(entity);

        return Result.Ok();
    }
}
```

For a "reject this operation if the entity is already in state X" business rule (rather than an unconditional delete), see `CancelPurchaseOrderRequest`'s handler — it fetches the entity, then adds one extra check (`if (order.Status == PurchaseOrderStatus.Cancelled) return Result.Fail(...)`) before calling the mutation method.

### Step 5: Create Query Specification

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Specs/SpecGet{Entity}.cs`:

```csharp
using DKNet.EfCore.Specifications;

namespace Minimal.AppServices.{Feature}.V1.Specs;

internal sealed class SpecGet{Entity} : Specification<{Entity}>
{
    public SpecGet{Entity}(Guid? byId = null, string? by{UniqueField} = null)
    {
        var predicator = CreatePredicate();

        if (byId is not null)
            predicator = predicator.And(a => a.Id == byId);

        if (!string.IsNullOrEmpty(by{UniqueField}))
            predicator = predicator.And(a => a.{UniqueField} == by{UniqueField});

        WithFilter(predicator);
    }
}
```

### Step 6: Create the Event Handler

The event record itself lives with the entity in `Minimal.Domains` (see `PurchaseOrderCreatedEvent.cs` — a one-line `public sealed record PurchaseOrderCreatedEvent(Guid Id, string CustomerName, decimal Amount);`), raised by `AddEvent(...)` in the constructor. The AppServices layer only owns the **consumer**:

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Events/{Entity}CreatedEventHandler.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Minimal.Domains.Features.{Feature}.Entities;

namespace Minimal.AppServices.{Feature}.V1.Events;

/// <summary>
/// Consumes <see cref="{Entity}CreatedEvent"/>, raised by hand from <see cref="{Entity}"/>'s constructor.
/// </summary>
internal sealed class {Entity}CreatedEventHandler(ILogger<{Entity}CreatedEventHandler> logger)
    : Fluents.EventsConsumers.IHandler<{Entity}CreatedEvent>
{
    public Task OnHandle({Entity}CreatedEvent notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("{Entity}CreatedEvent received for {{Id}}.", notification.Id);
        }

        return Task.CompletedTask;
    }
}
```

If the entity instead declares `[RaisesEvent(...)]` (the `Product` shape), the event record is generated for you and never appears as a source file — only the consumer above is still hand-written; see the generated-actions section above.

---

## Reference: PurchaseOrder Actions (actual production code)

Every request/validator/handler below is hand-written — `Minimal.AppServices/ManualSample/V1/Actions/`. Nothing here uses `[MapsFrom]`; instead the acting user comes from `[FromClaim(ClaimTypes.Name)]` on the request, populated by `AddContextualRequestPopulation` before the handler runs.

### Create Pattern (`Create.cs`)
- `CreatePurchaseOrderRequest : Fluents.Requests.IWitResponse<PurchaseOrderDto>` — `ByUser` via `[FromClaim(ClaimTypes.Name)]`, `CustomerName` `[Required][StringLength(200, MinimumLength = 1)]`, `Amount`
- `CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderRequest>` — `CustomerName` `NotEmpty().Length(1, 200)`, `Amount` `GreaterThan(0)`
- `CreatePurchaseOrderCommandHandler(IRepositorySpec, IMapper) : Fluents.Requests.IHandler<CreatePurchaseOrderRequest, PurchaseOrderDto>`
- Flow: guard on empty `ByUser` → `new PurchaseOrder(request.CustomerName, request.Amount, request.ByUser)` (the constructor raises `PurchaseOrderCreatedEvent` itself) → `repository.AddAsync` → `mapper.ResultOf<PurchaseOrderDto>(order)`

### Update Pattern (`Update.cs`)
- `UpdatePurchaseOrderRequest : Fluents.Requests.IWitResponse<PurchaseOrderDto>` — `Id`, `Amount`
- Validator only checks `Amount` `GreaterThan(0)` — deliberately **no** `NotEmpty` rule on `Id`, because `Id` comes from the route, not the body; an unknown/empty id 404s from the repository lookup instead (a real bug this sample's build caught and fixed)
- Handler: fetch via `SpecGetPurchaseOrder(request.Id)` → 404 (`NotFoundError`) on miss → `order.ChangeAmount(request.Amount, request.ByUser)` → `Result.Ok(mapper.Map<PurchaseOrderDto>(order))`

### Cancel Pattern (`Cancel.cs`)
- `CancelPurchaseOrderRequest : Fluents.Requests.IWitResponse<PurchaseOrderDto>` — `Id` only, no validator needed (nothing beyond presence/shape to check)
- Handler: fetch via spec → 404 on miss → **business-rule guard**: `if (order.Status == PurchaseOrderStatus.Cancelled) return Result.Fail<PurchaseOrderDto>(...)` → `order.Cancel(request.ByUser)` → `Result.Ok(mapper.Map<PurchaseOrderDto>(order))`. This is the pattern to copy whenever an operation must reject an already-applied state transition — that rule lives in the handler because it needs to *read* current state before deciding, but the transition itself (`Cancel`) still lives on the entity.

### Delete Pattern (`Delete.cs`)
- `DeletePurchaseOrderRequest : Fluents.Requests.INoResponse` — `Id` only
- Handler: fetch via spec → 404 (`NotFoundError`) on miss → `repository.Delete(order)` → `Result.Ok()`

---

## Alternative: generated actions (no hand-written layer at all)

For an entity whose constructor is `[CrudCreate]` and whose mutation method is `[CrudUpdate]` (see `Product` in **dknet-domain-entity**), this entire Actions layer — request, validator, handler — does not exist as hand-written source. `Minimal.AppServices/AutomatedSample/V1/ProductDto.cs` is one line (`[GenerateDto(typeof(Product))] public sealed partial record ProductDto;`), and the `DKNet.SlimBus.Generators` analyzer produces `CreateProductRequest`/`CreateProductHandler` and `ChangePriceProductRequest`/`ChangePriceProductHandler` in the `Minimal.AppServices.Crud` namespace (inspect them under `obj/Generated/` after a build — they are not committed to source). There is no generated FluentValidation validator either.

The DataAnnotations on `Product`'s `[CrudCreate]` constructor parameters and `[CrudUpdate]` method parameter (`[Required]`, `[StringLength]`, `[Range]`) are forwarded onto the generated request's properties — but **not enforced**: this template's own routing convention (see **dknet-endpoint-config**) maps generated CRUD routes through a generic library wrapper the .NET 10 validation source generator can't see through. Confirmed live: `POST /v1/products` with a negative price returns `201`, not `400`. Pick the generated shape only when you either don't need that validation enforced, or the entity's rules are simple enough that this gap is acceptable — see `docs/samples/manual-vs-automated.md` for the full account.

There is also no generated query/spec layer: `GetById`/`GetList`/`Delete` for `Product` map straight to `DKNet.AspCore.Extensions`'s generic `MapGetById`/`MapGetList`/`MapDeleteById` — no per-entity query object exists to add a filter parameter to.

---

## Validation Checklist

- [ ] Response DTO is either `[GenerateDto(typeof(Entity))]` (exposes everything audited) or a hand-written `record` with exactly the fields you want exposed
- [ ] Create request implements `Fluents.Requests.IWitResponse<{Dto}>`
- [ ] Create/Update/Cancel requests carry `[FromClaim(ClaimTypes.Name)] public string? ByUser { get; set; }` for the acting user
- [ ] Update request implements `Fluents.Requests.IWitResponse<{Dto}>`
- [ ] Delete request implements `Fluents.Requests.INoResponse`
- [ ] Validators are `internal sealed` and extend `AbstractValidator<T>`; no rule on `Id` when it comes from the route
- [ ] Handlers are `internal sealed` with primary constructor injection
- [ ] Handlers use `IRepositorySpec` (not custom repos)
- [ ] Every handler guards on `string.IsNullOrEmpty(request.ByUser)` before touching the entity
- [ ] Create handler uses `mapper.ResultOf<T>()` for lazy mapping
- [ ] Update/Cancel handlers fetch entity via `Specification`, 404 via `NotFoundError` on miss, call a named mutation method, return the mapped DTO
- [ ] A business-rule guard (e.g. "already cancelled") lives in the handler, right after the fetch, before calling the mutation method
- [ ] Delete handler returns `IResultBase` (not `IResult<T>`)
- [ ] Domain events are `sealed record` types, raised via `AddEvent(...)` in the entity's own constructor (or declared via `[RaisesEvent]` — see the generated-actions section above)
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
| Extending a `RequestBase` class | It doesn't exist in this codebase — declare `[FromClaim(ClaimTypes.Name)] ByUser` directly on the request |
| Using `Result.Ok(entity)` instead of `mapper.ResultOf<T>()` | Lazy mapping ensures DTO reflects post-SaveChanges state |
| Adding a `NotEmpty` validator rule on a route-supplied `Id` | It runs before the route value is patched in — let the repository lookup 404 instead (a real bug this template's build hit and fixed) |
| Trusting `request.ByUser` without a null/empty guard | Every handler in the manual sample checks it first and fails with "The caller is not authenticated." |

---

## Next Steps

After creating AppServices actions, proceed to:
→ **dknet-endpoint-config** skill to expose these actions as REST API endpoints

For the judgment behind business-rule placement and domain event usage, see **dknet-ddd-principles**.
