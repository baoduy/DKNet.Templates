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
4. Keep `[MapsFrom(typeof(Entity))]` on request/response records to preserve Mapster alignment.

This keeps request/response property names consistent with entities and reduces mapping drift.

---

## Project Conventions (from actual codebase)

### Core Pattern: SlimMessageBus Fluent Handlers

This project does NOT use custom repository interfaces or service classes. Instead:

- **Commands** implement `Fluents.Requests.IWitResponse<TDto>` (with response) or `Fluents.Requests.INoResponse` (without)
- **Handlers** implement `Fluents.Requests.IHandler<TRequest, TResponse>` or `Fluents.Requests.IHandler<TRequest>`
- **Data access** uses `IRepositorySpec` (injected) — a generic spec-based repository
- **Queries** use `Specification<TEntity>` pattern
- **Mapping** uses `Mapster` via `IMapper` + `[MapsFrom]` attribute
- **Results** use `FluentResults` — `Result.Ok(dto)`, `Result.Fail<T>("message")`
- **Lazy mapping**: `mapper.ResultOf<TDto>(entity)` — maps AFTER SaveChanges

### Request Base Class

All requests extend `RequestBase` which provides `[JsonIgnore] string? ByUser` — auto-filled by `SetUserIdPropertyFilter` from JWT claims.

### File Locations

```
src/ApiEndpoints/Minimal.AppServices/
├── {Feature}/
│   └── V{N}/
│       ├── {Entity}Dto.cs                  ← Response DTO (GenerateDto)
│       ├── Actions/
│       │   ├── Create.cs                   ← Request + Validator + Handler
│       │   ├── Update.cs                   ← Request + Validator + Handler
│       │   └── Delete.cs                   ← Request + Handler
│       ├── Specs/
│       │   └── SpecGet{Entity}.cs          ← Query specification
│       └── Events/
│           └── {Event}Handlers.cs          ← Event record + handlers
├── Share/
│   ├── RequestBase.cs                      ← DO NOT MODIFY
│   ├── IPrincipalProvider.cs               ← DO NOT MODIFY
│   └── Generics/                           ← Generic list/paged specs
├── Extensions/
│   ├── MapsFromAttribute.cs                ← DO NOT MODIFY
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
using Minimal.AppServices.Extensions;

namespace Minimal.AppServices.{Feature}.V1;

[GenerateDto(typeof({Entity}), Exclude = [])]
[MapsFrom(typeof({Entity}))]
public sealed partial record {Entity}Dto;
```

`[GenerateDto]` auto-generates all properties from the entity. Use `Exclude = ["InternalProp"]` to hide fields.

If you need operation-specific variants, prefer additional generated DTOs (with `Exclude`) over hand-written duplicated records.

### Step 2: Create Action — Create.cs

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Create.cs`:

```csharp
using System.Data;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.{Feature}.V1.Events;
using Minimal.AppServices.{Feature}.V1.Specs;
using Minimal.AppServices.Extensions;
using Minimal.AppServices.Share;

namespace Minimal.AppServices.{Feature}.V1.Actions;

/// <summary>
/// Command to create a new {entity}.
/// </summary>
[MapsFrom(typeof({Entity}))]
public sealed record Create{Entity}Request : RequestBase, Fluents.Requests.IWitResponse<{Entity}Dto>
{
    #region Properties

    [Required] public string {RequiredField1} { get; set; } = null!;
    [Required] public string {RequiredField2} { get; set; } = null!;
    public string? {OptionalField} { get; set; }

    // Auto-generated fields (hidden from API)
    [JsonIgnore] public string {AutoField} { get; set; } = null!;

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
/// Handler: validates uniqueness, maps to entity, persists, publishes event.
/// </summary>
internal sealed class Create{Entity}Handler(
    IRepositorySpec repository,
    // Inject domain services if needed:
    // I{Service} serviceProvider,
    IMapper mapper)
    : Fluents.Requests.IHandler<Create{Entity}Request, {Entity}Dto>
{
    public async Task<IResult<{Entity}Dto>> OnHandle(
        Create{Entity}Request request,
        CancellationToken cancellationToken)
    {
        // 1. Auto-generate fields if needed
        // if (string.IsNullOrWhiteSpace(request.{AutoField}))
        //     request.{AutoField} = await serviceProvider.NextValueAsync();

        // 2. Check duplicates
        if (await repository.AnyAsync(
            new SpecGet{Entity}(by{UniqueField}: request.{UniqueField}),
            cancellationToken: cancellationToken))
        {
            return Result.Fail<{Entity}Dto>($"{UniqueField} {request.{UniqueField}} already exists.");
        }

        // 3. Map request to entity
        var entity = mapper.Map<{Entity}>(request);

        // 3b. Defensive check — ensure auto-generated fields were mapped
        // if (string.IsNullOrEmpty(entity.{AutoField}))
        //     throw new NoNullAllowedException(nameof(entity.{AutoField}));

        // 4. Persist
        await repository.AddAsync(entity, cancellationToken);

        // 5. Publish domain event
        entity.AddEvent(new {Entity}CreatedEvent(entity.Id, entity.{NameField}));

        // 6. Return lazy-mapped DTO (resolves after SaveChanges)
        return mapper.ResultOf<{Entity}Dto>(entity);
    }
}
```

For `Create*Request`, use a manual record only for the writable subset. Do not duplicate server-generated, audit, or immutable entity fields unless the API contract explicitly requires them.

### Step 3: Create Action — Update.cs

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Update.cs`:

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.{Feature}.V1.Specs;
using Minimal.AppServices.Extensions;
using Minimal.AppServices.Share;

namespace Minimal.AppServices.{Feature}.V1.Actions;

/// <summary>
/// Command to update an existing {entity}.
/// </summary>
[MapsFrom(typeof({Entity}))]
public record Update{Entity}Request : RequestBase, Fluents.Requests.IWitResponse<{Entity}Dto>
{
    public required Guid Id { get; init; }
    public string? {MutableField1} { get; init; }
    public string? {MutableField2} { get; init; }
}

internal sealed class Update{Entity}Handler(
    IMapper mapper,
    IRepositorySpec repo) : Fluents.Requests.IHandler<Update{Entity}Request, {Entity}Dto>
{
    public async Task<IResult<{Entity}Dto>> OnHandle(
        Update{Entity}Request request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
            return Result.Fail<{Entity}Dto>("The Id is invalid.");

        var entity = await repo.FirstOrDefaultAsync(
            new SpecGet{Entity}(request.Id), cancellationToken);

        if (entity == null)
            return Result.Fail<{Entity}Dto>($"The {Entity} {request.Id} is not found.");

        // Call entity mutation method
        entity.Update({mutable params}, request.ByUser!);

        return Result.Ok(mapper.Map<{Entity}Dto>(entity));
    }
}
```

When update payload is a near 1:1 projection of entity fields, prefer a generated DTO base shape (with excludes) to minimize property drift. Keep manual `Update*Request` only for command-specific constraints (for example required `Id`, partial semantics, workflow flags).

### Step 4: Create Action — Delete.cs

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Actions/Delete.cs`:

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.{Feature}.V1.Specs;
using Minimal.AppServices.Share;

namespace Minimal.AppServices.{Feature}.V1.Actions;

/// <summary>
/// Command to delete a {entity} by ID.
/// </summary>
public record Delete{Entity}Request : RequestBase, Fluents.Requests.INoResponse
{
    public required Guid Id { get; init; }
}

internal sealed class Delete{Entity}Handler(IRepositorySpec repository)
    : Fluents.Requests.IHandler<Delete{Entity}Request>
{
    public async Task<IResultBase> OnHandle(
        Delete{Entity}Request request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return Result.Fail("The Id is invalid.")
                .WithError(new Error("The Id is invalid.") { Metadata = { ["field"] = nameof(request.Id) } });
        }

        var entity = await repository.FirstOrDefaultAsync(
            new SpecGet{Entity}(request.Id), cancellationToken);

        if (entity == null)
            return Result.Fail($"The {Entity} {request.Id} is not found.");

        repository.Delete(entity);
        return Result.Ok();
    }
}
```

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

### Step 6: Create Domain Events

Create `src/ApiEndpoints/Minimal.AppServices/{Feature}/V1/Events/{Entity}CreatedEventHandlers.cs`:

```csharp
namespace Minimal.AppServices.{Feature}.V1.Events;

/// <summary>
/// Domain event published when a {entity} is created.
/// </summary>
public sealed record {Entity}CreatedEvent(Guid Id, string {NameField});

/// <summary>
/// In-memory handler for {Entity}CreatedEvent.
/// </summary>
internal sealed class {Entity}CreatedEventFromMemoryHandler
    : Fluents.EventsConsumers.IHandler<{Entity}CreatedEvent>
{
    public Task OnHandle({Entity}CreatedEvent notification, CancellationToken cancellationToken)
    {
        // Handle event: logging, notifications, side-effects
        return Task.CompletedTask;
    }
}
```

### Step 7: Add Entity to GlobalUsings (if frequently referenced)

Edit `src/ApiEndpoints/Minimal.AppServices/GlobalUsings.cs`:

```csharp
global using Minimal.Domains.Features.{Feature}.Entities;
```

---

## Reference: CustomerProfile Actions (actual production code)

### Create Pattern
- `CreateProfileRequest : RequestBase, Fluents.Requests.IWitResponse<CustomerProfileDto>`
- `CreateProfileCommandValidator : AbstractValidator<CreateProfileRequest>`
- `CreateProfileCommandHandler(IRepositorySpec, IMembershipService, IMapper) : Fluents.Requests.IHandler<CreateProfileRequest, CustomerProfileDto>`
- Flow: generate membership → check duplicate via spec → `mapper.Map<CustomerProfile>(request)` → `repository.AddAsync` → `AddEvent(new ProfileCreatedEvent(...))` → `mapper.ResultOf<CustomerProfileDto>(profile)`

### Update Pattern
- `UpdateProfileRequest : RequestBase, Fluents.Requests.IWitResponse<CustomerProfileDto>`
- Handler: validate Id → fetch via spec → call `entity.Update(...)` → return `mapper.Map<CustomerProfileDto>(entity)`

### Delete Pattern
- `DeleteProfileRequest : RequestBase, Fluents.Requests.INoResponse`
- Handler: validate Id → fetch via spec → `repository.Delete(entity)` → `Result.Ok()`

---

## Validation Checklist

- [ ] Response DTO uses `[GenerateDto]` + `[MapsFrom]` attributes
- [ ] Create request implements `Fluents.Requests.IWitResponse<{Dto}>`
- [ ] Create request has `[MapsFrom(typeof({Entity}))]` attribute
- [ ] Update request implements `Fluents.Requests.IWitResponse<{Dto}>`
- [ ] Delete request implements `Fluents.Requests.INoResponse`
- [ ] All requests extend `RequestBase` (provides `ByUser`)
- [ ] Validators are `internal sealed` and extend `AbstractValidator<T>`
- [ ] Handlers are `internal sealed` with primary constructor injection
- [ ] Handlers use `IRepositorySpec` (not custom repos)
- [ ] Create handler checks duplicates via Specification before adding
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
| Missing `[MapsFrom]` on create request | Mapster needs this to map request → entity |
| Using `Result.Ok(entity)` instead of `mapper.ResultOf<T>()` | Lazy mapping ensures DTO reflects post-SaveChanges state |
| Forgetting `request.ByUser!` in Update | Must pass user ID to entity mutation methods |
| Not adding `[JsonIgnore]` on auto-fields | Fields like `MembershipNo` shouldn't be client-settable |

---

## Next Steps

After creating AppServices actions, proceed to:
→ **dknet-endpoint-config** skill to expose these actions as REST API endpoints

For the judgment behind business-rule placement and domain event usage, see **dknet-ddd-principles**.
