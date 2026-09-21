---
name: dknet-endpoint
description: Create Minimal API endpoint configurations using this project's IEndpointConfig pattern — raw minimal-API routes mapped by hand, a single generated Map<Entity>Crud() call, or the underlying DKNet.AspCore.Extensions generic route helpers called directly. Use after AppServices actions (or CrudCreate/CrudUpdate/CrudAction entity attributes) are ready, to expose a feature over HTTP. Invoke as `/dknet-endpoint <Feature> <Entity> [mode=manual|auto] [routePrefix] [version=V1]` to scaffold it for a feature.
metadata:
  kind: workflow
  arguments: "<Feature> <Entity> [mode=manual|auto] [routePrefix] [version=V1]"
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Agent
---

Usage: `/dknet-endpoint <Feature> <Entity> [mode=manual|auto] [routePrefix] [version=V1]`

# Skill: Endpoint Configuration

Wires AppServices actions/queries — or an entity's `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`
declarations — to HTTP routes via `IEndpointConfig`. Three ways to map a route: pick the one matching
how the feature's actions were built (`mode=manual` or `mode=auto`), and reach for the third only when
neither fits one particular route.

## 1. The `IEndpointConfig` contract

```csharp
internal sealed class {Entity}V1Endpoint : IEndpointConfig
{
    public int Version => 1;                    // -> /v{version:apiVersion}/... ; defaults to 1
    public string GroupEndpoint => "/{kebab-case-plural}";
    public void Map(RouteGroupBuilder group) { /* register routes */ }
}
```

- **Discovery**: every non-abstract `IEndpointConfig` in the API assembly is found by
  `UseEndpointConfigs(...)`, called once from `Minimal.Api/Program.cs`. Never register a route group
  by hand.
- **Versioning**: `EnableVersioning` (`FeatureManagement`, default `true`) turns `GroupEndpoint` into
  `/v{version}/{route}` — `"/products"` at `Version => 1` becomes `/v1/products`. Off, the group
  registers with no version segment.
- **Optional members**: `AuthPolicy` (policy required for the group; `null` means "authenticated, no
  specific policy") and `Tag` (OpenAPI grouping tag, defaulting to `GroupEndpoint` with slashes turned
  to dashes). Neither shipped sample overrides either.
- **`internal sealed`, always.** `Architecture/ApiTests.cs` enforces it two ways —
  `AllApiClassesShouldBeInternal` and `AllEndpointClassesShouldBeInternalAndSealed_ExceptAbstractClasses`
  (every concrete class under an `ApiEndpoints` namespace) — so a `public` or non-`sealed` endpoint
  class fails the build.
- **Cross-cutting wiring happens once, not per endpoint.** `Program.cs` passes `ConfigureGroup =
  (group, _) => group.AddFluentValidationAutoValidation()` to `UseEndpointConfigs`, and
  `.AddContextualRequestPopulation()` (the `[FromClaim]` populator) is registered the line before.
  Every group gets both automatically.
- **`.WithDescription(...)`/`.Produces<T>(...)`** are ordinary `RouteHandlerBuilder` calls for OpenAPI
  documentation; every hand-mapped route in both samples sets at least `.WithDescription`.

## 2. Option A — raw minimal API (hand-mapped)

Mirror `PurchaseOrderV1Endpoint` (`ManualSample/PurchaseOrder`) when the feature's actions are
hand-written `AppServices` requests/queries. Every route is a literal `group.MapPost/MapGet/MapPut/
MapDelete(...)` call, dispatching through `IMessageBus` by hand:

```csharp
using DKNet.AspCore.Extensions.Responses;
using DKNet.AspCore.Idempotency;
using Minimal.AppServices.ManualSample.V1.Actions;
using Minimal.AppServices.ManualSample.V1.Queries;
using PurchaseOrderDto = Minimal.AppServices.ManualSample.V1.PurchaseOrderDto;

namespace Minimal.Api.ApiEndpoints.ManualSample;

internal sealed class PurchaseOrderV1Endpoint : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/purchase-orders";

    public void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreatePurchaseOrderRequest req, IMessageBus bus, CancellationToken ct) =>
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
            .Produces<PurchaseOrderDto>()
            .Produces(StatusCodes.Status404NotFound)
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

Rules this exemplar carries:

- **`IMessageBus.Send(...)`** dispatches every request/query straight to its handler; the delegate
  never touches `CoreDbContext` or a repository.
- **`result.Response(isCreated: true)`** (`DKNet.AspCore.Extensions.Responses`) turns a
  `FluentResults` result into `201`/`200`/an error response; plain `.Response()` picks `200`/`204`.
  Don't hand-write the success/failure branching yourself.
- **`[AsParameters]`** binds a request from the route + query string as one object, and is the *only*
  way a `[FromClaim]`-carrying request (`Cancel`, `Delete` above) gets populated — the population
  filter only inspects an endpoint delegate's bound parameters. Constructing the same request type
  inside the lambda body instead skips population silently.
- **`req with { Id = id }`** is how a hand-mapped `PUT {id}` merges a route-bound id into a request
  record built from the body — the id is never part of the body's own DTO shape.
- **`Results.NotFound()` for a `null` query result** — `Get{Entity}ByIdQuery`'s handler returns
  `TDto?`; a `null` means "not found" and the endpoint delegate turns that into the `404`, not the
  handler.
- **`.RequiredIdempotentKey()`** (`DKNet.AspCore.Idempotency`) is opt-in per route — see
  [Cross-cutting behavior](#5-cross-cutting-behavior).

## 3. Option B — generated composite `Map<Entity>Crud()`

Mirror `ProductV1Endpoint` (`AutomatedSample/Product`) when the entity carries
`[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` (see `/dknet-entity`/`/dknet-crud`). One call
registers the entire generated CRUD surface; hand-mapped routes for what the generator cannot express
go below it.

```csharp
using Minimal.AppServices.AutomatedSample.V1;
using Minimal.AppServices.AutomatedSample.V1.Actions;
using Minimal.AppServices.AutomatedSample.V1.Queries;
using Minimal.AppServices.Crud;

namespace Minimal.Api.ApiEndpoints.AutomatedSample;

internal sealed class ProductV1Endpoint : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/products";

    public void Map(RouteGroupBuilder group)
    {
        var requireAuthorization = ((IEndpointRouteBuilder)group).ServiceProvider
            .GetRequiredService<IOptions<FeatureOptions>>().Value.RequireAuthorization;

        group.MapProductCrud(o =>
        {
            o.Exclude("Discontinue");   // dropped, hand-written below

            if (!requireAuthorization) return;

            o.Configure(CrudOp.GetById, rb => rb.RequireAuthorization(ProductScopes.Read));
            o.Configure(CrudOp.GetList, rb => rb.RequireAuthorization(ProductScopes.Read));
            o.Configure(CrudOp.Create, rb => rb.RequireAuthorization(ProductScopes.Write));
            o.Configure(CrudOp.Update, rb => rb.RequireAuthorization(ProductScopes.Write));
            o.Configure(CrudOp.Delete, rb => rb.RequireAuthorization(ProductScopes.Write));
            o.Configure("Approve", rb => rb.RequireAuthorization(ProductScopes.Write));
            o.Configure("AssignSupplierReference", rb => rb.RequireAuthorization(ProductScopes.Supplier));
        });

        // Writes two aggregates (this product + its replacement) in one transaction — the generator
        // cannot express that, so it is excluded by name above and hand-written here instead.
        var discontinue = group.MapPut("{id:guid}/discontinue", async (Guid id, DiscontinueProductCommand req, IMessageBus bus, CancellationToken ct) =>
            {
                var result = await bus.Send(req with { Id = id }, cancellationToken: ct);
                return result.Response();
            })
            .Produces<ProductDto>()
            .WithDescription("Discontinue a product and create its named replacement in the same transaction.");
        if (requireAuthorization) discontinue.RequireAuthorization(ProductScopes.Discontinue);

        // No generated shape produces an aggregate summary — hand-written.
        var summary = group.MapGet("summary", async (IMessageBus bus, CancellationToken ct) =>
                Results.Ok(await bus.Send(new ProductPriceSummaryQuery(), cancellationToken: ct)))
            .Produces<ProductPriceSummaryDto>()
            .WithDescription("Product count and average price across every product the caller can see.");
        if (requireAuthorization) summary.RequireAuthorization(ProductScopes.Read);
    }
}
```

`ProductScopes` (`Minimal.Api/ApiEndpoints/AutomatedSample/ProductScopes.cs`) is a plain
`internal static class` of policy-name constants — one per authorization scope the routes above
require, registered as authorization policies by `AddAuthConfig()` only when `RequireAuthorization`
is on:

```csharp
internal static class ProductScopes
{
    public const string Read = "products.read";
    public const string Write = "products.write";
    public const string Supplier = "products.supplier";
    public const string Discontinue = "products.discontinue";
}
```

### What `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` produce, per attribute

| Entity declaration | Generated route |
|---|---|
| `[CrudCreate]` on a constructor | `POST /` → `201` + DTO |
| (always) | `GET {id}` → `200` + DTO, `404` if missing |
| (always) | `GET /` → paged, filterable, searchable list — see the `dknet-queries-specs` skill |
| `[CrudUpdate]` on a method | `PUT {id}` → `200` + DTO, one route per `[CrudUpdate]` method |
| (always, unless a request implements `IWithKey<TKey>` only) | `DELETE {id}` → `204`/`404` |
| `[CrudAction("segment", Verb = ...)]` on a method | `POST`\|`PUT`\|`PATCH {id}/segment` → `200` + DTO (verb `POST` by default; segment defaults to the method name kebab-cased) |

The generated extension carries a doc comment naming this exactly: *"Maps GET {id}, GET /, POST /,
PUT {id} (per update request), DELETE {id} and each generated domain-action endpoint"*.

### Where the generated code lives

`DKNet.SlimBus.Generators` emits, per entity, under
`ApiEndpoints/Minimal.AppServices/obj/Generated/DKNet.SlimBus.Generators/.../` — not committed, so
build once (`dotnet build`) and read it there for the exact shape:

- `{Entity}CrudRequests.g.cs` — one `sealed partial record` per route (`Create{Entity}Request`,
  `Change{Member}{Entity}Request`, `{Method}{Entity}Request` per action, `Delete{Entity}Request`),
  with the declaring member's `DataAnnotations` attributes forwarded onto matching properties.
- `{Entity}CrudHandlers.g.cs` — one `internal sealed` handler per request, implementing
  `Fluents.Requests.IHandler<TRequest, TDto>`. Every non-create handler loads the entity through a
  private `{Entity}ByIdCrudSpec : Specification<TEntity>`, answers `NotFoundError` if missing, then
  calls the entity method and returns `LazyMapper.LazyMapExtensions.ResultOf<TDto>(...)`.
- `{Entity}CrudEndpointExtensions.g.cs` — the `Map{Entity}Crud()` extension itself.

**A generated handler can be replaced**: write your own class implementing the same
`IHandler<TRequest, TDto>` for that request type and it takes over.

### `CrudMapOptions` — excluding and configuring generated routes

```csharp
group.Map{Entity}Crud(o => o
    .Exclude(CrudOp.Delete)                                                     // a whole operation kind
    .Exclude("Discontinue")                                                     // one route, by member name
    .Configure(CrudOp.GetById, b => b.RequireAuthorization("products.read"))    // every route of a kind
    .Configure("ChangePrice", b => b.RequireAuthorization("products.write")));  // one named route
```

- **`CrudOp`** has six members: `GetById`, `GetList`, `Create`, `Update`, `Delete`, `Action`.
- **`Exclude(params CrudOp[])`** drops every route of that kind. **`Exclude(params string[])`** drops
  one route by name, leaving the entity's other routes of the same kind published.
- **`Configure(CrudOp, Action<RouteHandlerBuilder>)`** / **`Configure(string, Action<RouteHandlerBuilder>)`**
  are additive: several calls for the same operation or name all run, in call order — operation-kind
  settings before name settings, for any one route.
- **Route names** are `GetById`, `GetList`, `Create`, `Delete` for the four fixed operations, and the
  verbatim C# member name for each `[CrudUpdate]`/`[CrudAction]` (`ChangePrice`, `Approve`,
  `Discontinue`, `AssignSupplierReference` above) — never the kebab-cased URL segment, never the
  generated request type's name.
- **A typo fails the build, not the request.** The generated extension calls
  `options.ValidateRouteNames("{Entity}", "GetById", "GetList", ..., "ChangePrice", "Approve", ...)`
  at start-up, throwing `ArgumentException` if an `Exclude`/`Configure` call names a route the entity
  doesn't have — so an unprotected route from a misspelled scope name is caught at startup, not
  shipped silently.
- Nothing is excluded by default.

## 4. Option C — the package's generic helpers, called directly

`DKNet.AspCore.Extensions.Endpoints` is the library the generator's own `Map{Entity}Crud()` calls
into. You can call these same extensions yourself for an entity that has **no** `[CrudCreate]`/
`[CrudUpdate]` attributes — for example a read-mostly reference entity that only needs a list route
and a delete route, with everything else hand-written. This is exactly what the generated file calls;
copy the shape, not the generator:

```csharp
group.MapGetById<TEntity, TKey, TDto>("{id:guid}");        // GET  {id}  -> 200/404 + TDto
group.MapGetList<TEntity, TKey, TDto>("/");                 // GET  /     -> paged PagedResponse<TDto>
group.MapPost<TRequest, TDto>("/");                         // POST /     -> 201 if TRequest's name contains "Create", else 200
group.MapPutById<TRequest, TKey, TDto>("{id}");             // PUT  {id}  -> 200 + TDto
group.MapDeleteById<TEntity, TKey, TRequest>();             // DELETE {id} -> 204/404, TRequest carries validation-only key binding
group.MapActionById<TRequest, TKey, TDto>("{id}/x", "POST");            // any verb, body-bound beyond the key
group.MapParameterlessActionById<TRequest, TKey, TDto>("{id}/x", "PUT"); // any verb, no request body at all
```

- **`IWithKey<TKey>`** is the interface `MapPutById`, the by-request-type `MapDeleteById`,
  `MapActionById`, and `MapParameterlessActionById` all require on `TRequest` — it's how the route's
  `{id}` gets bound into the request before dispatch, for a request the caller also validates.
- **`MapGetById`/`MapGetList`/the plain `MapDeleteById<TEntity,TKey>()`** work straight off the
  entity/DTO pair with no request type at all — they build their own internal
  `EntityByIdSpecification`/`EntityListSpecification` and dispatch through `IRepositorySpec`, not
  `IMessageBus`.
- **`MapPost`/`MapPut`/`MapPatch`/`MapDelete`/`MapGetPage`** (the `Fluents*` family, not `*ById`)
  dispatch a `Fluents.Requests`/`Fluents.Queries` type through `IMessageBus`, exactly like the
  hand-mapped Option A calls — these are the underlying primitives `bus.Send(...)` wraps.
- **Error handling and response shaping are built in** — `ProducesCommons()` plus an endpoint filter
  that converts a dispatch exception into the same unhandled-error body `AddErrorResponses(...)`
  produces elsewhere. `FluentValidationConfig.cs`'s own comment notes the generated CRUD routes
  already resolve `ErrorResponseOptions` via `[FromServices]` — calling these helpers directly gets
  the same behavior.

Reach for Option C only when neither A nor B fits one particular entity — most features should be
entirely A or entirely B.

## 5. Cross-cutting behavior

**Idempotency (Option A only).** POST is never idempotent automatically. `.RequiredIdempotentKey()`
(`DKNet.AspCore.Idempotency`) enforces the `X-Idempotency-Key` request header on the one route it's
called on; a replayed key returns the original response instead of creating a duplicate. The
generated `Create` route in Option B has no equivalent — a duplicate submit or client retry against
`POST /v1/products` creates two rows. To add it, exclude `"Create"` from `Map{Entity}Crud` and
hand-map that one route with `.RequiredIdempotentKey()`.

**FluentValidation runs on every group, generated routes included.** `Program.cs` attaches
`AddFluentValidationAutoValidation()` to every group, so an `AbstractValidator<T>` for a *generated*
request (`CreateProductRequestValidator`, `DeleteProductRequestValidator`) runs before the generated
handler, can read stored data through `IRepositorySpec`, and can refuse with `409` by tagging its
failure's `Code` with the `precondition.` prefix. Unrelated to the next point.

**DataAnnotations on a generated request are forwarded but not enforced.** A `[Range]`/`[Required]`
on a `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` parameter is copied onto the generated request's
matching property, but the .NET 10 minimal-API validation source generator only recognizes a literal
`Map*(string, Delegate)` call it can see in the *compiling project's own source*. Option B's and
Option C's calls route through this package's generic `Map*<TRequest,TDto>` wrapper, which the
generator can't see through — so a negative price on `POST /v1/products` returns `201`, not `400`.
Only Option A's literal `group.MapPost("/", async (CreateXRequest req, ...) => ...)` gets the
attribute enforced. If a create/update rule must be enforced, either hand-map that one route (Option
A) or write it as a FluentValidation validator instead of a DataAnnotations attribute.

**Authorization scopes are conditional on the flag, always.** `RequireAuthorization(...)` needs
authorization middleware to evaluate — that middleware is only added when `FeatureManagement:
RequireAuthorization` is `true`. Calling `.RequireAuthorization(scope)` unconditionally would throw at
request time with the flag off (local development, both test suites). Every scope call in
`ProductV1Endpoint` is therefore gated behind reading the flag from `IOptions<FeatureOptions>`, as
shown in §3 — copy that guard, don't call `RequireAuthorization` bare.

**Status-counts helper.** `group.MapGetStatusCounts<TEntity>("status", new StatusPropertyInfo(nameof(X.Status), typeof(XStatus)))`
is a template-local extension (not part of the published package) that groups an entity's rows by an
enum-backed status property, backfilling every enum member with a zero count. No shipped endpoint
config calls it today — wire it into your own `Map(RouteGroupBuilder)` the same way any other route is
mapped, if a status breakdown is useful for your entity. Full contract: the `dknet-queries-specs`
skill.

**When a route must be hand-written**, regardless of mode:
- The operation writes more than one aggregate in one transaction (`Discontinue` above: it also
  creates a replacement `Product` row).
- The response is a shape the generator has none for (`summary` above: an aggregate, not a per-row
  DTO).

**When a route must NOT be hand-written**, even though it feels like it should be:
- A refusing precondition on create/update/delete — write it as a FluentValidation validator against
  the generated request (`CreateProductRequestValidator`, `DeleteProductRequestValidator`); it runs on
  the generated route with no hand-written endpoint, request, or handler needed.
- A derived response value on a generated DTO — write it as a Mapster `IRegister`
  (`ProductDto.GrossMargin` is `Price - SupplierCostPrice`, mapped this way); it reaches the generated
  route's response with no endpoint change at all.

## 6. Step-by-step

### `mode=manual`

1. Confirm the feature's `AppServices` layer exposes hand-written request/query records (see the
   `dknet-crud` skill).
2. Create `ApiEndpoints/Minimal.Api/ApiEndpoints/{Feature}/{Entity}V1Endpoint.cs`, `internal sealed`,
   implementing `IEndpointConfig`.
3. Map every route as a literal `group.MapPost/MapGet/MapPut/MapDelete(...)` call per §2, dispatching
   through `IMessageBus`.
4. Add `.RequiredIdempotentKey()` to the create route if a duplicate submit must not create two rows.
5. Add `.WithDescription(...)`/`.Produces<T>(...)` to every route.

### `mode=auto`

1. Confirm the entity declares `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` (see `/dknet-entity`)
   and its DTO is `[GenerateDto(typeof(Entity))]`.
2. Build once so the generated `Map{Entity}Crud()` extension exists.
3. Create `ApiEndpoints/Minimal.Api/ApiEndpoints/{Feature}/{Entity}V1Endpoint.cs`, calling
   `group.Map{Entity}Crud()` — bare, or with an `Action<CrudMapOptions>` per §3 for per-route scopes
   or exclusions.
4. For anything the generator can't express, hand-map it below the composite call, dropping the
   generated route it replaces via `o.Exclude(...)` when one exists.

## Verification

```bash
dotnet build -c Release
dotnet run --project ApiEndpoints/Minimal.Api
```

Then exercise the route via `/docs` (Scalar, when `EnableSwagger` is on) or curl:

```bash
curl -X POST https://localhost:5001/v1/{route} \
  -H "Content-Type: application/json" -H "X-Idempotency-Key: $(uuidgen)" -d '{...}'
curl https://localhost:5001/v1/{route}/{id}
```

## Common mistakes

| What you might expect | What actually happens | Why |
|---|---|---|
| A `[Range]`/`[Required]` on a `[CrudCreate]`/`[CrudUpdate]` parameter is enforced | A generated route accepts the out-of-range value and returns `201`/`200` | The .NET validation source generator can't see through the package's generic `Map*<TRequest,TDto>` wrapper — see §5 |
| `.RequiredIdempotentKey()` works the same on a generated create route | There's no route to call it on unless you exclude `"Create"` and hand-map it | The generated extension's calls are compiler output, not source you can chain onto |
| `RequireAuthorization(scope)` is safe to call unconditionally | It throws at request time when `RequireAuthorization` is off | No authorization middleware is added unless the flag is on — gate every call on `IOptions<FeatureOptions>` |
| A `[CrudAction]` method parameter named `byUser` is the acting-user stamp | It becomes a caller-settable, body-bound `required string ByUser` on the generated request | Generated requests carry no `[FromClaim]`; the automated sample's acting-user attribution goes through `DataOwnerHook`/`AddCurrentUserProvider` instead — never name a generated action parameter after the acting user |
| Excluding a route by a typo'd name is silently ignored | The host throws `ArgumentException` at start-up | `ValidateRouteNames` checks every `Exclude`/`Configure` name against the entity's real route names before the app can serve traffic |
| A hand-mapped endpoint can call the generic `MapPost<TRequest,TDto>` helper directly for convenience | It works, but bypasses the DataAnnotations-enforcement your Option A route would otherwise get | Only a literal `group.MapPost("/", async (...) => ...)` — not a call to the generic wrapper — is visible to the validation source generator |

---

# Workflow: `/dknet-endpoint`

The procedure an agent follows when invoked with arguments. The reference sections above are the rules it applies.

You are wiring the **Api** layer for a feature whose AppServices CRUD already exists. Run `/dknet-crud` first if not.

### Inputs

`$ARGUMENTS` — feature folder, entity, optional `mode=manual|auto`, optional kebab-case route prefix
(defaults to entity plural lowercased), optional version.

The mode must match the one `/dknet-crud` ran in. `mode=manual` uses the hand-mapped steps below;
`mode=auto` skips straight to **Alternative: generated CRUD route** at the bottom — its single
`Map<Entity>Crud()` call replaces every step in the Steps section, and `.RequiredIdempotentKey()` does
not apply to it. If `mode=` was not supplied, detect it: a `[CrudCreate]` on the entity means `auto`.

### Required reading

1. The reference sections above
2. `ApiEndpoints/Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs` (exemplar — every route is a literal `group.MapPost/MapGet/MapPut/MapDelete(...)` call against the raw minimal-API surface, base route `/v1/purchase-orders`)
3. The `dknet-feature-lifecycle` skill §1 — read the endpoint-registration and request-idempotency rows before choosing a mapping style

### Steps (`mode=manual`)

1. Use the `dknet-implementer` subagent to execute Step 6 of the implementer protocol:
   - Create `<Feature>V<N>Endpoint : IEndpointConfig` with `Version` and `GroupEndpoint`.
   - Map each route with a literal `group.MapPost(...)`/`MapGet(...)`/`MapPut(...)`/`MapDelete(...)` call, mirroring `PurchaseOrderV1Endpoint` (create, list, get-by-id, update, and any business action route such as `cancel`, plus delete).
   - Call `.RequiredIdempotentKey()` on the `MapPost` chain that creates the resource — clients then send `X-Idempotency-Key: {Guid}`; a replayed key returns the original response instead of creating a duplicate.
   - Add `.WithDescription(...)` on each route.
2. Build the solution and confirm Scalar/OpenAPI lists the new endpoints (run the API briefly if practical).
3. Report the mode used, files added, and the next command (`/dknet-unit-tests <Feature> <Entity> mode=<mode>` then `/dknet-bdd-tests <Feature>`).

### Constraints

- Endpoint class MUST be `internal sealed` and implement `IEndpointConfig` — in **both** modes.
- The idempotency constraint below applies to `mode=manual` only; see the Alternative section for `auto`.
- Do NOT register the endpoint manually — `EndpointConfig.CreateGroup` discovers it.
- Do NOT add controllers or attribute routing — this is Minimal API only.
- `.RequiredIdempotentKey()` is required on the create route; without it, duplicate `X-Idempotency-Key` retries will not be deduped.

### Alternative: generated CRUD route

If the entity is plain CRUD with `[CrudCreate]`/`[CrudUpdate]`/`[GenerateDto]` already in place (see `Product`), skip hand-mapping entirely — the generator emits a `Map<Entity>Crud()` extension (namespace `Minimal.AppServices.Crud`) that wires GetById/GetList/Create/Update/Delete in one call. `ProductV1Endpoint` is the exemplar: `group.MapProductCrud(o => …)` carrying the per-route scopes and one `Exclude("Discontinue")`, plus a `.WithDescription`, with the two routes the generator cannot express hand-mapped below it. This path does **not** get `.RequiredIdempotentKey()` and its DataAnnotations validation is not enforced (the .NET 10 validation source generator can't see through the generic `Map*<TRequest,TDto>` wrapper the generated route uses) — confirmed live: `POST /v1/products` with a negative price returns `201`. Only use this path when idempotency and enforced validation are not required.
