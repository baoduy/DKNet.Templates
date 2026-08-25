# API Request Pipeline

What happens to a request before it reaches a handler — in the order it actually runs. The feature guides
cover what each handler does; this page covers everything upstream of the handler that isn't visible from
reading one alone.

## 1. Routing and endpoint registration

Every route group is an `IEndpointConfig` (`DKNet.AspCore.Extensions`) — for example
`Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs` and
`Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs`. `Minimal.Api/Program.cs` calls
`UseEndpointConfigs(...)`, which discovers every non-abstract `IEndpointConfig` in the app assembly,
builds a versioned route group per config, and calls its `Map(RouteGroupBuilder)`.

## 2. API versioning

`FeatureManagement:EnableVersioning` (default `true`) is read in `Minimal.Api/Program.cs` and passed as
`o.EnableVersioning` to `UseEndpointConfigs`. When enabled, each group's route becomes
`/v{version:apiVersion}{GroupEndpoint}` — e.g. `/v1/purchase-orders` — and the host must have called
`AddAppVersioning()` (`Minimal.Api/Configs/VersioningConfig.cs`) or registration throws at startup, even
before any endpoint is discovered.

## 3. Authentication / authorization

`FeatureManagement:RequireAuthorization` (default `false`) drives two things at once:

- `Minimal.Api/Configs/AppConfig.cs` only calls `AddAuthConfig()` (JWT bearer auth,
  `Minimal.Api/Configs/Auth/AuthConfig.cs`) when it's `true`.
- The same flag is passed to `UseEndpointConfigs` as `o.RequireAuthorization`, which is applied to every
  route group after mapping and before `IEndpointConfig.Map` runs.

`Minimal.App.Tests/Integration/EndpointConfig/PurchaseOrderStampingAndVersioningTests.cs` pins the
authorization-off behavior explicitly (see below).

## 4. `[FromClaim]` population

Registered once via `.AddContextualRequestPopulation(o => o.SystemAccountFallback = SharedConsts.SystemAccount)`
in `Minimal.Api/Program.cs`, and applied automatically by `UseEndpointConfigs` for every mapped endpoint.
Any request property marked `[FromClaim(...)]` — e.g. `ByUser` on
`Minimal.AppServices/ManualSample/V1/Actions/Create.cs` — is **overwritten** from the caller's claim
before validation and before the handler runs. This is a security property, not a model-binding
convenience: whatever the caller put in the body or query string for that member is always discarded.

`SystemAccountFallback` only substitutes a value when `RequireAuthorization` is `false` *and* the claim
resolver couldn't resolve a value — an authenticated caller with a genuinely missing claim never gets the
fallback; the member holds its type's default instead, and the handler must reject it explicitly (see
`CreatePurchaseOrderCommandHandler.OnHandle`'s `IsNullOrEmpty(request.ByUser)` check). Pinned by
`AuthorizationOff_CreateIsAttributedToSystemAccount` and
`AuthenticatedCallerWithNoNameClaim_CreateIsRefused_NeverAttributedToSystemAccount` in
`Minimal.App.Tests/Integration/EndpointConfig/PurchaseOrderStampingAndVersioningTests.cs`.

## 5. FluentValidation auto-validation

`Minimal.Api/Configs/FluentValidationConfig.cs` registers `AddFluentValidationAutoValidation()` and scans
the `AppServices` assembly for `AbstractValidator<T>` implementations (e.g.
`CreatePurchaseOrderCommandValidator` next to `CreatePurchaseOrderRequest`). A request failing validation
never reaches a handler — it short-circuits to a `400` with FluentValidation's problem-details shape,
with no handler code involved.

## 6. Idempotency on POST

Idempotency is opt-in per route, not automatic for every POST. `Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs`'s
create route chains `.RequiredIdempotentKey()`, which enforces the idempotency key header (default
`X-Idempotency-Key`) on that route — a request missing it is rejected before the handler runs. The
automated sample's generated create route (`Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs`'s
`MapProductCrud()`) makes no such call — a replayed request there is processed as a brand-new create, not
deduplicated. Add `.RequiredIdempotentKey()` yourself on any route where duplicate submissions matter.

Store selection happens once in `Minimal.Api/Configs/AppConfig.cs`, based on whether
`ConnectionStrings:Redis` is configured:

- **Redis configured** — `AddIdempotencyWithRedisStore(redisConnectionString, o => o.ConflictHandling =
  IdempotentConflictHandling.CachedResult)`: keys are tracked in Redis, so idempotency works correctly
  across multiple app instances.
- **No Redis** — falls back to the non-generic `AddIdempotentKey(...)` (same `CachedResult` conflict
  handling), an in-process store — fine for local development, not for a multi-instance deployment.

With `IdempotentConflictHandling.CachedResult` (this template's setting), a replayed request with the
same key returns the original cached response rather than re-running the handler or returning a conflict
error.

## 7. Rate limiting

`FeatureManagement:EnableRateLimit` (default `true`) wires `Minimal.Api/Configs/RateLimits/RateLimitConfig.cs`:
a chained `PartitionedRateLimiter` combining a fixed-window limiter and a concurrency limiter, both keyed
per-request by `IRateLimitKeyProvider` and configured per-request by `IRateLimitOptionsProvider`. A
request over either limit is rejected with `429 Too Many Requests` before reaching routing's endpoint
handler.

## 8. Global exception handling

`Minimal.Api/Configs/GlobalExceptions/GlobalExceptionHandler.cs` is registered as the app's
`IExceptionHandler`. Any unhandled exception from a handler becomes a `ProblemDetails` response:
`Status = 500`, `Title = "Something went wrong!."`, `Detail` set to the exception's message (or its
`InnerException`'s, if present), `Type` set to the exception's type name, plus a `trace-id` extension
(the request's `TraceIdentifier`) and `Instance` set to `"{Method} {Path}"` — added by
`Minimal.Api/Configs/GlobalExceptions/GlobalExceptionConfigs.cs`'s `CustomizeProblemDetails`. A client
never sees a raw stack trace.

## 9. Health checks and OpenAPI/Scalar

- **Health checks** (`FeatureManagement:EnableHealthCheck`, default `true`) — an EF Core connectivity
  check plus a custom `HealthCheckHandler`, mapped at both `/healthz` and `/` by
  `Minimal.Api/Configs/Healthz/HealthzConfig.cs`.
- **OpenAPI/Scalar** (`FeatureManagement:EnableSwagger`, default `false`) — `Minimal.Api/Configs/Swagger/SwaggerConfig.cs`
  maps the OpenAPI 3.0 document and a Scalar UI at `/docs`, pre-configured with a Bearer-auth scheme.
