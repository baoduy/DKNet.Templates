---
name: dknet-api-endpoints
description: Expose CRUD operations via REST endpoints with OpenAPI documentation using the fluent endpoint mapper pattern. Use this when creating HTTP API endpoints.
---

# Skill: REST API Endpoints with Fluent Mapper Pattern

**Duration**: 30–40 minutes | **Difficulty**: Intermediate | **Category**: REST API & Orchestration

---

## Overview

**When to use this skill**: Your entity has domain modeling (Skill 1) and CRUD operations (Skill 2). Now expose it via REST API endpoints using the fluent endpoint mapper pattern.

**What you'll create**: 
- Request/Response DTOs
- `IEndpointConfig` implementation
- Fluent mapper endpoint registration (Read operations are generic; Writes use custom commands)
- Custom action endpoints (e.g., Approve, Reject)

**Context**: This codebase uses:
- **Generic Fluent Mappers** for reads: `MapGetList<Entity, Dto>`, `MapGetById<Entity, Dto>`
- **Message Bus Commands** for writes: `MapPost<Request, Dto>`, `MapPut<Request, Dto>`, `MapDelete<Request>`, `MapPatch<Request>` for custom actions
- **IEndpointConfig** interface for endpoint registration in `Program.cs`

---

## Prerequisites: Do You Know This?

- [ ] Completed [Domain Modeling Skill](../domain-modeling/skill.md) and [CRUD Operations Skill](../crud-operations/skill.md)
- [ ] Understanding of REST API concepts (GET, POST, PUT, PATCH, DELETE)
- [ ] Familiarity with DTOs and request/response mapping
- [ ] Know the difference between query params (GET list) and body params (POST, PUT)
- [ ] Understand message bus pattern for command dispatch

---

## Inputs Checklist

- [ ] **Entity name**: Which entity are you exposing? (e.g., `CustomerProfile`)
- [ ] **CRUD operations to expose**: Which subset of Create, Update, Delete?
- [ ] **API version**: Version number (typically `v1`)
- [ ] **Custom actions**: Any domain-specific actions? (e.g., Approve, Reject, Publish)
- [ ] **Response DTO name**: Will it use `[GenerateDto]` attribute?

---

## Step-by-Step Workflow

### Step 1: Create Response DTO with [GenerateDto]

**What you're doing**: Auto-generate the response DTO from your entity using `[GenerateDto]` attribute.

In `SlimBus.AppServices/Features/<YourFeature>/V1/`:

```csharp
using DKNet.EfCore.DtoGenerator;
using Mapster;
using SlimBus.Domains.Features.CustomerProfiles.Entities;

namespace SlimBus.AppServices.CustomerProfiles.V1;

/// <summary>
/// Response DTO auto-generated from CustomerProfile entity.
/// Used for all GET, POST, PUT responses.
/// </summary>
[GenerateDto(typeof(CustomerProfile), Exclude = [])]
[MapsFrom(typeof(CustomerProfile))]
public sealed partial record CustomerProfileDto;
```

**Expected outcome**: Code generator creates the `CustomerProfileDto` record with all properties from `CustomerProfile`.

---

### Step 2: Create Request Types (Create, Update, Delete)

**What you're doing**: Create request records that inherit from `RequestBase` and implement `IWitResponse<TResponse>` interface. Each CRUD operation needs its own request type.

```csharp
using SlimBus.AppServices.Share;
using Mapster;
using Fluents.Requests;

namespace SlimBus.AppServices.CustomerProfiles.V1.Actions;

/// <summary>
/// Command to create a new customer profile.
/// Implements IWitResponse<T> because it returns CustomerProfileDto.
/// </summary>
[MapsFrom(typeof(CustomerProfile))]
public sealed record CreateProfileRequest : RequestBase, IWitResponse<CustomerProfileDto>
{
    /// <summary>Email is required and must be unique.</summary>
    [Required]
    public string Email { get; set; } = null!;

    /// <summary>Full name of the customer.</summary>
    [StringLength(150)]
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>Phone number - optional but should be valid format if provided.</summary>
    [Phone]
    public string? Phone { get; set; }
}

/// <summary>
/// Command to update an existing customer profile.
/// Only non-null fields will be updated (partial update).
/// </summary>
[MapsFrom(typeof(CustomerProfile))]
public sealed record UpdateProfileRequest : RequestBase, IWitResponse<CustomerProfileDto>
{
    /// <summary>ID of the profile to update.</summary>
    public required Guid Id { get; init; }

    /// <summary>New email (optional; if null, current value preserved).</summary>
    public string? Email { get; init; }

    /// <summary>New name (optional; if null, current value preserved).</summary>
    public string? Name { get; init; }

    /// <summary>New phone (optional; if null, current value preserved).</summary>
    public string? Phone { get; init; }
}

/// <summary>
/// Command to delete a customer profile by ID.
/// Implements INoResponse because delete endpoints return 204 NoContent.
/// </summary>
public sealed record DeleteProfileRequest : RequestBase, INoResponse
{
    public required Guid Id { get; init; }
}

/// <summary>
/// Custom action: Approve a pending profile.
/// Use MapPatch<ApproveProfileRequest, CustomerProfileDto>() in IEndpointConfig.
/// </summary>
public sealed record ApproveProfileRequest : RequestBase, IWitResponse<CustomerProfileDto>
{
    public required Guid Id { get; init; }
    
    /// <summary>Reason for approval (optional audit trail).</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Custom action: Reject a pending profile.
/// Use MapPatch<RejectProfileRequest, CustomerProfileDto>() in IEndpointConfig.
/// </summary>
public sealed record RejectProfileRequest : RequestBase, IWitResponse<CustomerProfileDto>
{
    public required Guid Id { get; init; }
    
    /// <summary>Reason for rejection (required for audit trail).</summary>
    [Required]
    public string Reason { get; init; } = null!;
}
```

**Key Notes**:
- All requests inherit from `RequestBase` (provided by framework; includes user context like `ByUser`, `IsIdentity`, etc.)
- `IWitResponse<T>` = command returns a response DTO
- `INoResponse` = command returns no data (e.g., Delete)
- `[MapsFrom(typeof(Entity))]` enables automatic Mapster mapping from entity
- Data annotations (`[Required]`, `[StringLength]`, `[Phone]`) provide validation

---

### Step 3: Create Fluent Validators for Each Request

**What you're doing**: Add FluentValidation validators for each request type to enforce business rules.

```csharp
using FluentValidation;

namespace SlimBus.AppServices.CustomerProfiles.V1.Actions;

/// <summary>
/// Validates CreateProfileRequest before command execution.
/// </summary>
public sealed class CreateProfileRequestValidator : AbstractValidator<CreateProfileRequest>
{
    public CreateProfileRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(256);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .Length(2, 150).WithMessage("Name must be between 2 and 150 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Phone cannot exceed 50 characters");
    }
}

/// <summary>
/// Validates UpdateProfileRequest - at least one field must be updated.
/// </summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID is required");

        RuleFor(x => x)
            .Custom((request, context) =>
            {
                if (string.IsNullOrEmpty(request.Email) && 
                    string.IsNullOrEmpty(request.Name) && 
                    string.IsNullOrEmpty(request.Phone))
                {
                    context.AddFailure("At least one field (Email, Name, Phone) must be provided for update");
                }
            });
    }
}

/// <summary>
/// Validates RejectProfileRequest - reason is required.
/// </summary>
public sealed class RejectProfileRequestValidator : AbstractValidator<RejectProfileRequest>
{
    public RejectProfileRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID is required");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters");
    }
}
```

**Expected outcome**: Validators auto-discovered by FluentValidation during Startup.

---

### Step 4: Create Command Handlers

**What you're doing**: Create handler classes that execute the commands. These inherit from `IHandler<TRequest, TResponse>` and are auto-discovered by message bus.

```csharp
namespace SlimBus.AppServices.CustomerProfiles.V1.Actions;

/// <summary>
/// Handles CreateProfileRequest: creates and persists new customer profile.
/// </summary>
internal sealed class CreateProfileCommandHandler : 
    Fluents.Requests.IHandler<CreateProfileRequest, CustomerProfileDto>
{
    private readonly IMapper _mapper;
    private readonly IRepositorySpec _repo;
    private readonly IEventPublisher _eventPublisher;

    public CreateProfileCommandHandler(
        IMapper mapper,
        IRepositorySpec repo,
        IEventPublisher eventPublisher)
    {
        _mapper = mapper;
        _repo = repo;
        _eventPublisher = eventPublisher;
    }

    public async Task<Result<CustomerProfileDto>> Handle(
        CreateProfileRequest request,
        CancellationToken cancellationToken)
    {
        // Check for duplicate email using Spec
        var spec = new SpecGetProfileByEmail(request.Email);
        var existing = await _repo.FirstOrDefaultAsync(spec, cancellationToken);
        if (existing != null)
            return Result<CustomerProfileDto>.Invalid("Email already exists");

        // Create entity
        var profile = CustomerProfile.Create(
            request.UserId,  // ByUser from RequestBase
            request.Name,
            request.Email,
            request.Phone);

        // Persist
        await _repo.AddAsync(profile, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        // Publish domain event
        await _eventPublisher.PublishAsync(
            new CustomerProfileCreatedEvent(
                profile.Id,
                profile.UserId,
                profile.Email,
                profile.CreatedAt),
            cancellationToken);

        // Return mapped response
        return Result<CustomerProfileDto>.Success(_mapper.Map<CustomerProfileDto>(profile));
    }
}

/// <summary>
/// Handles UpdateProfileRequest: updates existing profile with partial data.
/// </summary>
internal sealed class UpdateProfileCommandHandler : 
    Fluents.Requests.IHandler<UpdateProfileRequest, CustomerProfileDto>
{
    private readonly IMapper _mapper;
    private readonly IRepositorySpec _repo;

    public UpdateProfileCommandHandler(IMapper mapper, IRepositorySpec repo)
    {
        _mapper = mapper;
        _repo = repo;
    }

    public async Task<Result<CustomerProfileDto>> Handle(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        // Get existing profile
        var profile = await _repo.FirstOrDefaultAsync(
            new SpecGetProfileById(request.Id),
            cancellationToken);

        if (profile == null)
            return Result<CustomerProfileDto>.NotFound("Profile not found");

        // Update only non-null fields
        if (!string.IsNullOrEmpty(request.Email))
            profile.Email = request.Email;

        if (!string.IsNullOrEmpty(request.Name))
            profile.SetName(request.Name);

        if (!string.IsNullOrEmpty(request.Phone))
            profile.Phone = request.Phone;

        // Persist
        await _repo.UpdateAsync(profile, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<CustomerProfileDto>.Success(_mapper.Map<CustomerProfileDto>(profile));
    }
}

/// <summary>
/// Handles DeleteProfileRequest: soft-delete or hard-delete profile.
/// </summary>
internal sealed class DeleteProfileCommandHandler : 
    Fluents.Requests.IHandler<DeleteProfileRequest>
{
    private readonly IRepositorySpec _repo;

    public DeleteProfileCommandHandler(IRepositorySpec repo)
    {
        _repo = repo;
    }

    public async Task<Result> Handle(
        DeleteProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await _repo.FirstOrDefaultAsync(
            new SpecGetProfileById(request.Id),
            cancellationToken);

        if (profile == null)
            return Result.NotFound("Profile not found");

        // Soft delete (if entity supports IsDeleted)
        profile.MarkAsDeleted();

        await _repo.UpdateAsync(profile, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Custom action handler: approve pending profile.
/// </summary>
internal sealed class ApproveProfileCommandHandler : 
    Fluents.Requests.IHandler<ApproveProfileRequest, CustomerProfileDto>
{
    private readonly IMapper _mapper;
    private readonly IRepositorySpec _repo;

    public ApproveProfileCommandHandler(IMapper mapper, IRepositorySpec repo)
    {
        _mapper = mapper;
        _repo = repo;
    }

    public async Task<Result<CustomerProfileDto>> Handle(
        ApproveProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await _repo.FirstOrDefaultAsync(
            new SpecGetProfileById(request.Id),
            cancellationToken);

        if (profile == null)
            return Result<CustomerProfileDto>.NotFound("Profile not found");

        profile.Approve(request.Reason);

        await _repo.UpdateAsync(profile, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<CustomerProfileDto>.Success(_mapper.Map<CustomerProfileDto>(profile));
    }
}
```

**Key Points**:
- Handlers inherit from `IHandler<TRequest, TResponse>` interface
- They're auto-discovered and registered by the message bus framework
- Receive dependencies via constructor injection
- Use Specs for queries: `await _repo.FirstOrDefaultAsync(spec)`
- Use domain methods for state changes: `profile.Approve()`, `profile.MarkAsDeleted()`
- Return `Result<T>` or `Result` for success/failure handling

---

### Step 5: Implement IEndpointConfig Interface

**What you're doing**: Register all endpoints using fluent mappers. Generic `MapGetList` and `MapGetById` for reads; custom `MapPost`, `MapPut`, `MapDelete`, `MapPatch` for writes and custom actions.

```csharp
using Fluents.Builder;

namespace SlimBus.Api.ApiEndpoints;

/// <summary>
/// Endpoint configuration for Customer Profile API.
/// Uses fluent mapper pattern:
/// - MapGetList/MapGetById for generic read endpoints
/// - MapPost/MapPut/MapDelete for CRUD commands
/// - MapPatch for custom actions (Approve, Reject, etc.)
/// </summary>
internal sealed class CustomerProfileV1Endpoint : IEndpointConfig
{
    #region IEndpointConfig Implementation

    public int Version => 1;
    public string GroupEndpoint => "/customer-profiles";

    public void Map(RouteGroupBuilder group)
    {
        // ========== READ OPERATIONS (Generic Fluent Mappers) ==========
        
        /// <summary>
        /// GET /api/v1/customer-profiles
        /// Returns paginated list with filtering and sorting.
        /// Parameters: pageNumber, pageSize, sortBy, search, filters
        /// </summary>
        group.MapGetList<CustomerProfile, CustomerProfileDto>("")
            .WithDescription("Get paginated list of profiles with optional filtering and sorting");

        /// <summary>
        /// GET /api/v1/customer-profiles/{id}
        /// Returns single profile by ID.
        /// </summary>
        group.MapGetById<CustomerProfile, CustomerProfileDto>("{id:guid}")
            .WithDescription("Get profile by ID");

        // ========== WRITE OPERATIONS (Custom Request Commands) ==========
        
        /// <summary>
        /// POST /api/v1/customer-profiles
        /// Creates new profile. Returns 201 Created with Location header.
        /// Request: CreateProfileRequest (Body)
        /// Response: CustomerProfileDto (201 Created)
        /// </summary>
        group.MapPost<CreateProfileRequest, CustomerProfileDto>("")
            .WithDescription("Create new profile")
            .Accepts<CreateProfileRequest>("application/json")
            .Produces<CustomerProfileDto>(StatusCodes.Status201Created);

        /// <summary>
        /// PUT /api/v1/customer-profiles/{id}
        /// Updates profile fields. Null fields are ignored (partial update).
        /// Request: UpdateProfileRequest (Body + Route param)
        /// Response: CustomerProfileDto (200 OK)
        /// </summary>
        group.MapPut<UpdateProfileRequest, CustomerProfileDto>("{id:guid}")
            .WithDescription("Update profile by ID (partial update supported)")
            .Accepts<UpdateProfileRequest>("application/json");

        /// <summary>
        /// DELETE /api/v1/customer-profiles/{id}
        /// Soft-deletes profile. Returns 204 NoContent.
        /// Response: 204 NoContent (no body)
        /// </summary>
        group.MapDelete<DeleteProfileRequest>("{id:guid}")
            .WithDescription("Delete profile by ID");

        // ========== CUSTOM ACTION ENDPOINTS (MapPatch) ==========
        
        /// <summary>
        /// PATCH /api/v1/customer-profiles/{id}/approve
        /// Approves a pending profile (custom domain action).
        /// Request: ApproveProfileRequest
        /// Response: CustomerProfileDto (200 OK)
        /// </summary>
        group.MapPatch<ApproveProfileRequest, CustomerProfileDto>("{id:guid}/approve")
            .WithDescription("Approve pending profile")
            .Accepts<ApproveProfileRequest>("application/json");

        /// <summary>
        /// PATCH /api/v1/customer-profiles/{id}/reject
        /// Rejects a pending profile with required reason (custom domain action).
        /// Request: RejectProfileRequest
        /// Response: CustomerProfileDto (200 OK)
        /// </summary>
        group.MapPatch<RejectProfileRequest, CustomerProfileDto>("{id:guid}/reject")
            .WithDescription("Reject pending profile with reason")
            .Accepts<RejectProfileRequest>("application/json");
    }

    #endregion
}
```

**Key Details**:
- Implements `IEndpointConfig` interface
- `Version` property for API versioning
- `GroupEndpoint` for route prefix (e.g., `/customer-profiles` → `/api/v1/customer-profiles`)
- `MapGetList<Entity, Dto>("")` maps generic paged list endpoint
- `MapGetById<Entity, Dto>("{id:guid}")` maps generic get-by-id endpoint
- `MapPost<Request, Response>("")` maps create endpoint (auto-detects 201 Created)
- `MapPut<Request, Response>("{id:guid}")` maps update endpoint
- `MapDelete<Request>("{id:guid}")` maps delete endpoint
- `MapPatch<Request, Response>("{id:guid}/action")` maps custom action endpoints

---

### Step 6: Register Endpoint Configuration in Program.cs

**What you're doing**: Wire up the `IEndpointConfig` implementation so endpoints are available at runtime.

```csharp
// In SlimBus.Api/Program.cs

var builder = WebApplication.CreateBuilder(args);

// ... other configurations ...

// Add endpoint configurations
builder.Services.AddEndpointConfigurations();  // Automatically registers all IEndpointConfig types

var app = builder.Build();

// ... middleware ...

// Map all registered endpoints
app.MapEndpoints();

app.Run();
```

The framework auto-discovers all `IEndpointConfig` implementations via reflection and registers them.

---

### Step 7: Verify Endpoint Swagger Documentation

**What you're doing**: Ensure Swagger/OpenAPI docs are generated correctly.

Run your app and navigate to:
```
https://localhost:7001/swagger
```

Expected documentation should show:
- ✅ `GET /api/v1/customer-profiles` - List (with pagination query params)
- ✅ `GET /api/v1/customer-profiles/{id}` - Get by ID
- ✅ `POST /api/v1/customer-profiles` - Create (201 Created)
- ✅ `PUT /api/v1/customer-profiles/{id}` - Update
- ✅ `DELETE /api/v1/customer-profiles/{id}`
- ✅ `PATCH /api/v1/customer-profiles/{id}/approve` - Custom action
- ✅ `PATCH /api/v1/customer-profiles/{id}/reject` - Custom action

---

## Architecture Summary

```
┌──────────────────────────────────────────────────────────────┐
│ API Layer: IEndpointConfig Implementation                     │
│ (CustomerProfileV1Endpoint - routes only, no logic)          │
└──────────────────────────────────┬──────────────────────────┘
                                   │
                    ┌──────────────┴──────────────┐
                    │                             │
         ┌──────────▼─────────┐       ┌──────────▼──────────┐
         │ Generic Mappers    │       │ Message Bus Commands│
         │ (Reads)            │       │ (Writes)            │
         │ ┌─────────────┐    │       │ ┌──────────────┐    │
         │ │MapGetList   │    │       │ │CreateRequest ├──┐ │
         │ │MapGetById   │    │       │ │UpdateRequest │  │ │
         │ └─────────────┘    │       │ │DeleteRequest │  │ │
         └────────┬────────────┘       │ │ApproveRequest├──┤ │
                  │                    │ │RejectRequest │  │ │
                  │                    │ └──────────────┘  │ │
                  │                    └────────┬──────────┘ │
                  │                             │            │
         ┌────────▼─────────┐         ┌────────▼───────┐   │
         │ IRepositorySpec  │         │ IHandler       │   │
         │ (Read queries)   │         │ (Command exec) │◄──┘
         │ - FirstOrDefault │         │                │
         │ - ToPagedList    │         │ • Validation   │
         └────────┬─────────┘         │ • Domain logic │
                  │                   │ • Persistence  │
                  │                   │ • Events       │
         ┌────────▼─────────┐         └────────┬───────┘
         │ Specification<T> │                  │
         │ - SpecByEmail    │         ┌────────▼────────────┐
         │ - SpecById       │         │ IRepositorySpec     │
         │ - SpecWithDetails│         │ - Add/Update/Delete │
         └─────────────────┘         └────────┬────────────┘
                                               │
                                      ┌────────▼──────────┐
                                      │ EF Core DbContext │
                                      │ - SaveChanges     │
                                      └───────────────────┘

Legend:
┌─────┐ Request: HTTP POST/PUT/PATCH body + route params
│ Dto │ Response: JSON DTO (200/201/204)
└─────┘ Spec: Query pattern (Where, Include, OrderBy)
```

**Flow Example (Create Profile)**:

1. Client: `POST /api/v1/customer-profiles` with `CreateProfileRequest` JSON body
2. Fluent Mapper: Captures request → message bus
3. Message Bus: Dispatches to `CreateProfileCommandHandler`
4. Handler: Validates → queries Spec → creates entity → persists → publishes event
5. Mapper: Returns 201 Created with `CustomerProfileDto`

---

## Key Patterns to Remember

### 1. **Generic Reads** (No custom handler needed)
```csharp
group.MapGetList<CustomerProfile, CustomerProfileDto>("")
group.MapGetById<CustomerProfile, CustomerProfileDto>("{id:guid}")
```

### 2. **Custom Writes** (Request + Handler pair)
```csharp
// Request
public sealed record CreateProfileRequest : RequestBase, IWitResponse<CustomerProfileDto> { ... }

// Endpoint mapping
group.MapPost<CreateProfileRequest, CustomerProfileDto>("")

// Handler auto-discovered
internal sealed class CreateProfileCommandHandler : IHandler<CreateProfileRequest, CustomerProfileDto> { ... }
```

### 3. **Custom Actions** (Domain-specific business logic)
```csharp
// Endpoint
group.MapPatch<ApproveProfileRequest, CustomerProfileDto>("{id:guid}/approve")

// Handler
internal sealed class ApproveProfileCommandHandler : IHandler<ApproveProfileRequest, CustomerProfileDto> { ... }
```

### 4. **Request Interfaces**
- `IWitResponse<T>` → returns response DTO (201/200/etc.)
- `INoResponse` → no response body (204 NoContent)

---

## Common Issues & Solutions

| Issue                         | Solution                                                                                    |
| ----------------------------- | ------------------------------------------------------------------------------------------- |
| "Handler not found" error     | Check that handler class implements `IHandler<TRequest, TResponse>` and is in same assembly |
| Validation errors not showing | Make sure validator class exists and is named `{RequestName}Validator`                      |
| 404 on custom action          | Ensure route pattern is correct, e.g., `"{id:guid}/approve"`                                |
| Swagger not showing endpoint  | Verify `MapPost`, `MapPut`, etc. has `.Produces<T>()` metadata                              |

