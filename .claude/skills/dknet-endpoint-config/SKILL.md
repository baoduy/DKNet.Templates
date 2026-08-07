---
name: dknet-endpoint-config
description: Create Minimal API endpoint configurations using this project's IEndpointConfig pattern with fluent helpers (MapGetList, MapGetById, MapPost, MapPut, MapDelete). Use after AppServices actions are ready.
---

# Skill: Endpoint Configuration

Create versioned REST API endpoints that wire AppServices actions to HTTP routes using the project's fluent endpoint mapping pattern.

---

## When to Use

- After completing dknet-appservices-actions skill
- Exposing CRUD operations as REST API endpoints
- Adding a new versioned endpoint group

## Inputs Required

1. **Entity class** (from domain)
2. **DTO class** (from AppServices): the response DTO
3. **Action requests** (from AppServices): Create, Update, Delete request types
4. **API version**: integer (e.g., `1`)
5. **Route group**: kebab-case path (e.g., `/customer-profiles`)
6. **Idempotency**: does POST need idempotency key?

## DTO Contract Rule (GenerateDto-First)

- Endpoint response DTOs should be generated from entities by default (`[GenerateDto]`) in AppServices.
- Prefer generated shapes for request/response consistency whenever request payload is entity-aligned.
- Only use hand-written request records when command semantics differ from entity structure (partial updates, hidden/server fields, workflow-only fields).
- Avoid manually duplicating entity property names/types in multiple request/response records unless contract differences are intentional.

---

## Project Conventions (from actual codebase)

### IEndpointConfig Pattern

- Implement `IEndpointConfig` interface — auto-discovered via assembly scanning
- Class must be `internal sealed`
- `Version` → API version integer
- `GroupEndpoint` → route path (e.g., `/customer-profiles`)
- `Map(RouteGroupBuilder group)` → wire endpoints using fluent helpers
- `Tag` is auto-derived from `GroupEndpoint` (strips `/` → kebab-case)

### Auto-Wiring (EndpointConfig.cs)

`UseEndpointConfigs()` scans the assembly for all `IEndpointConfig` implementations, creates versioned groups with:
- `SetUserIdPropertyFilter` → fills `RequestBase.ByUser` from JWT
- `FluentValidation` auto-validation (if configured)
- `RequireAuthorization()` (if auth is configured)
- API versioning via `{version:apiVersion}` path segment

### Fluent Endpoint Helpers

| Helper | HTTP | Request Interface | Response |
|--------|------|------------------|----------|
| `MapGetList<TEntity, TDto>()` | GET `/` | Auto-wires `GenericListParameters` (filter/sort/page) — NO request type needed | `PagedResponse<TDto>` |
| `MapGetById<TEntity, TDto>()` | GET `/{id:guid}` | Auto-wires `Guid id` from route — NO request type needed | `TDto` |
| `MapPost<TReq, TDto>()` | POST `/` | `Fluents.Requests.IWitResponse<TDto>` | 201 + `TDto` |
| `MapPut<TReq, TDto>()` | PUT `/` | `Fluents.Requests.IWitResponse<TDto>` | 200 + `TDto` |
| `MapDelete<TReq>()` | DELETE `/` | `Fluents.Requests.INoResponse` | 200 |
| `MapDelete<TReq, TDto>()` | DELETE `/` | `Fluents.Requests.IWitResponse<TDto>` | 200 + `TDto` |
| `MapGet<TReq, TDto>()` | GET `/` | `Fluents.Queries.IWitResponse<TDto>` | 200 + `TDto` |
| `MapGetPage<TReq, TDto>()` | GET `/` | `Fluents.Queries.IWitPageResponse<TDto>` | 200 + `PagedResponse<TDto>` |
| `MapPatch<TReq, TDto>()` | PATCH `/` | `Fluents.Requests.IWitResponse<TDto>` | 200 + `TDto` |
| `MapGetStatusCounts<TEntity>()` | GET `/status` | `GenericStatusCountsParameters` | `List<StatusCountsResult>` |

All fluent helpers automatically:
- Dispatch through `IMessageBus.Send(request)` (SlimMessageBus)
- Add common error Produces (400, 401, 403, 404, 409, 429, 500)
- POST with "Create" in name → 201 status code (detected via `typeof(TCommand).Name.Contains("Create")`)
- Note: `MapGetList` and `MapGetById` are different — they wire directly to `IRepositorySpec` with generic specs, NOT through the message bus. They don't need a request type parameter.

### File Location

```
src/ApiEndpoints/Minimal.Api/
└── ApiEndpoints/
    └── {Entity}V{N}Endpoint.cs         ← One file per entity per version
```

---

## Step-by-Step

### Step 1: Create the Endpoint Config

Create `src/ApiEndpoints/Minimal.Api/ApiEndpoints/{Entity}V1Endpoint.cs`:

```csharp
using Minimal.AppServices.{Feature}.V1.Actions;
using Minimal.Domains.Features.{Feature}.Entities;
using {Entity}Dto = Minimal.AppServices.{Feature}.V1.{Entity}Dto;

namespace Minimal.Api.ApiEndpoints;

internal sealed class {Entity}V1Endpoint : IEndpointConfig
{
    #region Properties

    public int Version => 1;

    public string GroupEndpoint => "/{kebab-case-plural}";

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        // GET /v1/{route} — paginated list with filtering/sorting
        group.MapGetList<{Entity}, {Entity}Dto>()
            .WithDescription("Get all {entities}");

        // GET /v1/{route}/{id} — single entity by ID
        group.MapGetById<{Entity}, {Entity}Dto>()
            .WithDescription("Get {entity} by id");

        // POST /v1/{route} — create new entity
        group.MapPost<Create{Entity}Request, {Entity}Dto>()
            .WithDescription("Create {entity}");

        // PUT /v1/{route} — update existing entity
        group.MapPut<Update{Entity}Request, {Entity}Dto>()
            .WithDescription("Update {entity} by id");

        // DELETE /v1/{route} — soft-delete entity
        group.MapDelete<Delete{Entity}Request>()
            .WithDescription("Delete {entity} by id");
    }

    #endregion
}
```

Before wiring endpoints, verify the DTO used by `{Entity}Dto` is generated from the entity and that request records intentionally deviate only where needed.

### Step 2: Add Idempotency (for POST, if needed)

```csharp
using Minimal.Api.Configs.Idempotency;

// In the Map method:
group.MapPost<Create{Entity}Request, {Entity}Dto>()
    .AddIdempotencyFilter()
    .WithDescription(
        "Create {entity}. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
        "X-Idempotency-Key: {IdempotencyKey} <br/>");
```

### Step 3: Add Custom Endpoints (if needed)

For endpoints beyond basic CRUD:

```csharp
// Custom query endpoint
group.MapGet<CustomQueryRequest, CustomResponseDto>("/custom-route")
    .WithDescription("Custom query description");

// Status counts endpoint
group.MapGetStatusCounts<{Entity}>("status",
    new StatusPropertyInfo("Status", typeof({Entity})))
    .WithDescription("Get {entity} status counts");
```

### Step 4: Override Auth Policy (if needed)

```csharp
internal sealed class {Entity}V1Endpoint : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/{route}";
    public string? AuthPolicy => "AdminOnly";  // Override default auth
    public string Tag => "Custom Tag";          // Override auto-derived tag

    public void Map(RouteGroupBuilder group) { /* ... */ }
}
```

---

## Reference: CustomerProfile (actual production code)

```csharp
using Minimal.Api.Configs.Idempotency;
using Minimal.AppServices.CustomerProfiles.V1.Actions;
using Minimal.Domains.Features.Profiles.Entities;
using CustomerProfileDto = Minimal.AppServices.CustomerProfiles.V1.CustomerProfileDto;

namespace Minimal.Api.ApiEndpoints;

internal sealed class CustomerProfileV1Endpoint : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/customer-profiles";

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetList<CustomerProfile, CustomerProfileDto>()
            .WithDescription("Get all profiles");
        group.MapGetById<CustomerProfile, CustomerProfileDto>()
            .WithDescription("Get profile by id");

        group.MapPost<CreateProfileRequest, CustomerProfileDto>()
            .AddIdempotencyFilter()
            .WithDescription(
                "Create profile. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
                "X-Idempotency-Key: {IdempotencyKey} <br/>");

        group.MapPut<UpdateProfileRequest, CustomerProfileDto>()
            .WithDescription("Update profile by id");

        group.MapDelete<DeleteProfileRequest>()
            .WithDescription("Delete profile by id");
    }
}
```

---

## Validation Checklist

- [ ] Class implements `IEndpointConfig` interface
- [ ] Class is `internal sealed`
- [ ] `Version` returns correct API version integer
- [ ] `GroupEndpoint` uses kebab-case with leading `/`
- [ ] `Map()` uses fluent helpers (`MapGetList`, `MapGetById`, `MapPost`, `MapPut`, `MapDelete`)
- [ ] DTO type alias added if namespace conflicts: `using {Entity}Dto = ...`
- [ ] All endpoints have `.WithDescription()` for OpenAPI docs
- [ ] POST for creation uses `MapPost` (auto 201 for "Create" in name)
- [ ] DELETE without response body uses `MapDelete<TReq>()` (single generic param)
- [ ] Idempotency filter added to POST if needed (`.AddIdempotencyFilter()`)
- [ ] File placed in `Minimal.Api/ApiEndpoints/`
- [ ] `dotnet build src/DKNet.Templates.sln -c Release` passes
- [ ] Swagger/Scalar UI shows endpoints correctly under versioned group

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Creating manual `app.MapGet(...)` routes | Use fluent helpers — they wire bus dispatch + error responses |
| Forgetting DTO type alias | Add `using {Entity}Dto = Minimal.AppServices.{Feature}.V1.{Entity}Dto;` |
| Using `MapDelete<TReq, TDto>` when no response needed | Use single-param `MapDelete<TReq>()` for void deletes |
| Making endpoint class `public` | Must be `internal sealed` — discovered by assembly scanning |
| Wrong `GroupEndpoint` format | Must start with `/`, use kebab-case plural (e.g., `/order-items`) |
| Registering endpoint in `Program.cs` | NOT needed — `UseEndpointConfigs()` auto-discovers all `IEndpointConfig` |

---

## Complete Feature Verification

After creating the endpoint, verify the full vertical slice works:

```bash
# Build
dotnet build src/DKNet.Templates.sln -c Release

# Run API
dotnet run --project src/ApiEndpoints/Minimal.Api

# Test via Scalar UI (default: https://localhost:5001/scalar)
# Or test via curl:
curl -X GET https://localhost:5001/api/v1/{route}
curl -X POST https://localhost:5001/api/v1/{route} -H "Content-Type: application/json" -d '{...}'
```
