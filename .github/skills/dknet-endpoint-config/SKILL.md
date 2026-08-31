---
name: dknet-endpoint-config
description: Create Minimal API endpoint configurations using this project's IEndpointConfig pattern — either hand-mapped literal routes or the generator's single MapXxxCrud() call. Use after AppServices actions are ready.
---

# Skill: Endpoint Configuration

Create versioned REST API endpoints that wire AppServices actions to HTTP routes, using this template's `IEndpointConfig` pattern.

This project currently ships two real, different ways to do this — pick the one matching how the underlying feature was built (see **dknet-appservices-actions**):

1. **Hand-mapped literal routes** (`PurchaseOrderV1Endpoint`) — every route is a literal `group.MapPost/MapGet/MapPut/MapDelete(...)` call against the raw minimal-API surface. This is the primary pattern taught below.
2. **Generator-driven CRUD** (`ProductV1Endpoint`) — one `group.MapXxxCrud()` call, generated from the entity's `[CrudCreate]`/`[CrudUpdate]`/`[RaisesEvent]` attributes. See the dedicated section near the end.

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
5. **Route group**: kebab-case path (e.g., `/purchase-orders`)
6. **Idempotency**: does POST need an idempotency key? (only meaningful for hand-mapped routes — see below)

---

## Project Conventions (from actual codebase)

### IEndpointConfig Pattern

- Implement `IEndpointConfig` interface — auto-discovered via assembly scanning
- Class must be `internal sealed`
- `Version` → API version integer
- `GroupEndpoint` → route path (e.g., `/purchase-orders`, `/products`)
- `Map(RouteGroupBuilder group)` → wire endpoints
- `Tag` is auto-derived from `GroupEndpoint` (strips `/` → kebab-case) unless overridden

### Auto-Wiring (`UseEndpointConfigs()`)

`UseEndpointConfigs()` scans the assembly for all `IEndpointConfig` implementations and creates route groups with:
- `RequireAuthorization()` (if auth is configured)
- API versioning via `{version:apiVersion}` path segment — switchable with the `EnableVersioning` feature flag (default on); when off, groups are registered with no version segment. A group whose `IEndpointConfig.Version` is not overridden defaults to version 1.
- Request-user population (`[FromClaim]` properties, via `AddContextualRequestPopulation`) and FluentValidation are wired once at the composition root (`Program.cs`), not per endpoint.

### Hand-mapped routes: no fluent entity/DTO helper

`PurchaseOrderV1Endpoint` maps every route with the raw minimal-API surface directly — `group.MapPost("/", async (CreatePurchaseOrderRequest req, IMessageBus bus, CancellationToken ct) => {...})`. The acting user is never stamped by the endpoint itself; `req.ByUser` (a `[FromClaim(ClaimTypes.Name)]` property) is populated by `AddContextualRequestPopulation` before the lambda runs. The lambda just calls `bus.Send(req, cancellationToken: ct)` and returns `result.Response(isCreated: true)` (or `result.Response()` for non-create). Population only runs over the endpoint delegate's *bound* parameters, so a request carrying `[FromClaim]` that the lambda would otherwise construct itself (`Cancel`/`Delete`) must instead be bound with `[AsParameters]`. This is the current convention for any hand-written feature — there is no generic `MapPost<TReq,TDto>()`-style call to reach for here; that call shape now exists only inside the generator's own output (see below).

POST does **not** auto-add idempotency — call `.RequiredIdempotentKey()` explicitly on the route you want protected; clients then send `X-Idempotency-Key: {Guid}`. A replayed key returns the original response instead of creating a duplicate.

### File Location

```
src/ApiEndpoints/Minimal.Api/
└── ApiEndpoints/
    └── {Feature}/
        └── {Entity}V{N}Endpoint.cs         ← One file per entity per version
```

---

## Step-by-Step (hand-mapped routes)

### Step 1: Create the Endpoint Config

Create `src/ApiEndpoints/Minimal.Api/ApiEndpoints/{Feature}/{Entity}V1Endpoint.cs`:

```csharp
using DKNet.AspCore.Extensions.Responses;
using DKNet.AspCore.Idempotency;
using Minimal.AppServices.{Feature}.V1.Actions;
using Minimal.AppServices.{Feature}.V1.Queries;
using {Entity}Dto = Minimal.AppServices.{Feature}.V1.{Entity}Dto;

namespace Minimal.Api.ApiEndpoints.{Feature};

/// <summary>
/// Every route here is written with the raw minimal-API surface — no generic entity/DTO
/// route-registration helper is used.
/// </summary>
internal sealed class {Entity}V1Endpoint : IEndpointConfig
{
    #region Properties

    public int Version => 1;

    public string GroupEndpoint => "/{kebab-case-plural}";

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
                Create{Entity}Request req,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var result = await bus.Send(req, cancellationToken: ct);
                return result.Response(isCreated: true);
            })
            .RequiredIdempotentKey()
            .Produces<{Entity}Dto>(StatusCodes.Status201Created)
            .WithDescription(
                "Create {entity}. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
                "X-Idempotency-Key: {IdempotencyKey} <br/>");

        group.MapGet("{id:guid}", async (
                Guid id,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var dto = await bus.Send(new Get{Entity}ByIdQuery { Id = id }, cancellationToken: ct);
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            })
            .Produces<{Entity}Dto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithDescription("Get {entity} by id");

        group.MapPut("{id:guid}", async (
                Guid id,
                Update{Entity}Request req,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var result = await bus.Send(req with { Id = id }, cancellationToken: ct);
                return result.Response();
            })
            .WithDescription("Update {entity}");

        group.MapDelete("{id:guid}", async (
                [AsParameters] Delete{Entity}Request req,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var result = await bus.Send(req, cancellationToken: ct);
                return result.Response();
            })
            .WithDescription("Delete {entity}");
    }

    #endregion
}
```

This mirrors `PurchaseOrderV1Endpoint` exactly (base route `/v1/purchase-orders`) — it also maps a `POST {id}/cancel` route the same way, for the `CancelPurchaseOrderRequest` action.

### Step 2: Idempotency (POST only, if needed)

```csharp
group.MapPost("/", async (Create{Entity}Request req, IMessageBus bus, CancellationToken ct) =>
    {
        var result = await bus.Send(req, cancellationToken: ct);
        return result.Response(isCreated: true);
    })
    .RequiredIdempotentKey()
    .WithDescription(
        "Create {entity}. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
        "X-Idempotency-Key: {IdempotencyKey} <br/>");
```

There is no equivalent for the generator-driven path — see the gap called out below.

### Step 3: Override Auth Policy or Tag (if needed)

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

## Reference: PurchaseOrderV1Endpoint (actual production code)

`Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs`, base route `/v1/purchase-orders`:

```csharp
internal sealed class PurchaseOrderV1Endpoint : IEndpointConfig
{
    public int Version => 1;

    public string GroupEndpoint => "/purchase-orders";

    public void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
                CreatePurchaseOrderRequest req,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var result = await bus.Send(req, cancellationToken: ct);
                return result.Response(isCreated: true);
            })
            .RequiredIdempotentKey()
            .Produces<PurchaseOrderDto>(StatusCodes.Status201Created)
            .WithDescription(
                "Create purchase order. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
                "X-Idempotency-Key: {IdempotencyKey} <br/>");

        group.MapGet("/", async ([AsParameters] ListPurchaseOrdersQuery query, IMessageBus bus, CancellationToken ct) =>
                Results.Ok(await bus.Send(query, cancellationToken: ct)))
            .WithDescription("Get purchase orders (paged, optionally filtered by customer name).");

        group.MapGet("{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
            {
                var dto = await bus.Send(new GetPurchaseOrderByIdQuery { Id = id }, cancellationToken: ct);
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            })
            .WithDescription("Get purchase order by id");

        group.MapPut("{id:guid}", async (Guid id, UpdatePurchaseOrderRequest req, IMessageBus bus, CancellationToken ct) =>
            {
                var result = await bus.Send(req with { Id = id }, cancellationToken: ct);
                return result.Response();
            })
            .WithDescription("Update purchase order amount");

        group.MapPost("{id:guid}/cancel", async ([AsParameters] CancelPurchaseOrderRequest req, IMessageBus bus, CancellationToken ct) =>
            {
                var result = await bus.Send(req, cancellationToken: ct);
                return result.Response();
            })
            .WithDescription("Cancel purchase order");

        group.MapDelete("{id:guid}", async ([AsParameters] DeletePurchaseOrderRequest req, IMessageBus bus, CancellationToken ct) =>
            {
                var result = await bus.Send(req, cancellationToken: ct);
                return result.Response();
            })
            .WithDescription("Delete purchase order");
    }
}
```

Confirmed live: blank customer name, a non-positive amount, and a missing `X-Idempotency-Key` header on create all return `400` — FluentValidation and the idempotency filter both run for every route here, because the .NET 10 validation source generator can see a literal `Map*(string, Delegate)` call written in this project's own source.

---

## Generator-driven alternative: `ProductV1Endpoint`

For an entity using `[CrudCreate]`/`[CrudUpdate]`/`[RaisesEvent]` (see `Product` in **dknet-domain-entity**), the entire endpoint file collapses to one call. `Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs` in full:

```csharp
internal sealed class ProductV1Endpoint : IEndpointConfig
{
    public int Version => 1;

    public string GroupEndpoint => "/products";

    public void Map(RouteGroupBuilder group)
    {
        group.WithDescription("Automated sample — Product CRUD generated from [CrudCreate]/[CrudUpdate]/[RaisesEvent].");
        group.MapProductCrud();
    }
}
```

`MapProductCrud()` is generated by `DKNet.SlimBus.Generators` (inspect it under `obj/Generated/.../ProductCrudEndpoints.g.cs` after a build) and internally calls the same generic library extensions from `DKNet.AspCore.Extensions` that a hand-mapped endpoint no longer calls directly:

```csharp
group.MapGetById<Product, Guid, ProductDto>();
group.MapGetList<Product, Guid, ProductDto>();
group.MapDeleteById<Product, Guid>();
group.MapPost<CreateProductRequest, ProductDto>("/");
group.MapPutById<ChangePriceProductRequest, Guid, ProductDto>("{id}");
```

`MapProductCrud(configure)` also accepts an optional `Action<CrudMapOptions>` to exclude one of the five operations (`CrudMapOptions.Exclude(CrudOp.Delete)`, for example) if you need to drop one route and hand-write a replacement.

**What you give up, concretely:**
- **No idempotency.** Nothing in this call chain adds `.RequiredIdempotentKey()` — a duplicate-submit or client retry on `POST /v1/products` silently creates a second product. Adding one means excluding `CrudOp.Create` and hand-mapping that one route.
- **No enforced request validation.** `CreateProductRequest.Price` genuinely carries `[Range(0.01, double.MaxValue)]` (attribute forwarding works), but nothing evaluates it — the .NET 10 validation source generator can't see through this generic library wrapper the way it can see `PurchaseOrderV1Endpoint`'s literal calls. Confirmed live: `POST /v1/products` with `price: -1` returns `201`, not `400`. See `docs/samples/manual-vs-automated.md` for the full account.
- **No custom filter/sort surface.** `MapGetList`/`MapGetById` here are the generic, any-`IEntity<TKey>` library helpers — there's no per-entity query object to add a parameter to (contrast `PurchaseOrderV1Endpoint`'s `ListPurchaseOrdersQuery`, which has a `CustomerName` filter).

Pick this shape only when the entity's validation is fully expressible as DataAnnotations *and* you don't need it enforced, and idempotency doesn't matter for that route.

---

## Validation Checklist

- [ ] Class implements `IEndpointConfig` interface
- [ ] Class is `internal sealed`
- [ ] `Version` returns correct API version integer
- [ ] `GroupEndpoint` uses kebab-case with leading `/`
- [ ] Hand-mapped routes use the raw minimal-API surface (`MapPost`/`MapGet`/`MapPut`/`MapDelete` with a literal lambda) and dispatch through `bus.Send(...)`
- [ ] The endpoint never assigns `ByUser` itself — `[FromClaim]` + `AddContextualRequestPopulation` is the only mechanism; a request carrying it that the lambda would otherwise construct (`Cancel`/`Delete`) is bound with `[AsParameters]` instead
- [ ] DTO type alias added if namespace conflicts: `using {Entity}Dto = ...`
- [ ] All endpoints have `.WithDescription()` for OpenAPI docs
- [ ] Create route calls `.RequiredIdempotentKey()` if duplicate submits must be rejected
- [ ] Generator-driven endpoints are a single `group.MapXxxCrud()` call plus `.WithDescription()` — nothing else hand-mapped
- [ ] File placed in `Minimal.Api/ApiEndpoints/{Feature}/`
- [ ] `dotnet build src/DKNet.Templates.sln -c Release` passes
- [ ] Swagger/Scalar UI shows endpoints correctly under versioned group

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Reaching for a generic `MapPost<TReq,TDto>()`/`MapGetList<TEntity,TDto>()` call in a hand-written endpoint | That call shape now belongs to the generator's own output; hand-mapped routes use the raw minimal-API surface directly (see `PurchaseOrderV1Endpoint`) |
| Assuming `[Range]`/`[Required]` on a generated CRUD request is enforced | It isn't, under this template's routing convention — see the generator-driven section above |
| Forgetting DTO type alias | Add `using {Entity}Dto = Minimal.AppServices.{Feature}.V1.{Entity}Dto;` when the DTO name collides |
| Making endpoint class `public` | Must be `internal sealed` — discovered by assembly scanning |
| Wrong `GroupEndpoint` format | Must start with `/`, use kebab-case plural (e.g., `/purchase-orders`) |
| Registering endpoint in `Program.cs` | NOT needed — `UseEndpointConfigs()` auto-discovers all `IEndpointConfig` |
| Expecting `.RequiredIdempotentKey()` on a generated create route | It doesn't exist there — idempotency is a hand-mapped-only capability today |

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
