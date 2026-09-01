# API Request Pipeline

This page traces everything that happens to a request before it reaches a handler, in the order it
actually runs. The feature guides cover what each handler does; this page covers only the pipeline
stages upstream of it, which aren't visible from reading a single handler.

## At a glance

Every `Default` below is the value the shipped base `appsettings.json` produces — that is what an
unmodified deployed service runs with, since the template ships no `appsettings.Production.json`.
Where an environment overlay relaxes it, the row says so. Full flag matrix:
[`docs/template-features.md`](template-features.md#featuremanagement-flags).

| # | Stage | Default |
|---|---|---|
| 1 | Routing and endpoint registration | — |
| 2 | API versioning | `FeatureManagement:EnableVersioning` = `true` |
| 3 | Authentication / authorization | `FeatureManagement:RequireAuthorization` = `true` (`false` in Development/Testing) |
| 4 | `[FromClaim]` population | — |
| 5 | FluentValidation auto-validation | — |
| 6 | Idempotency on POST | opt-in per route |
| 7 | Rate limiting | `FeatureManagement:EnableRateLimit` = `true` (`false` in Development/Testing) |
| 8 | Global exception handling | — |
| 9 | Health checks and OpenAPI/Scalar | `EnableHealthCheck` = `true`, `EnableSwagger` = `false` |

## 1. Routing and endpoint registration

Every route group is an `IEndpointConfig` (`DKNet.AspCore.Extensions`) — for example
`Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs` and
`Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs`. `Minimal.Api/Program.cs` calls
`UseEndpointConfigs(...)`, which discovers every non-abstract `IEndpointConfig` in the app assembly.
For each one it builds a versioned route group and calls its `Map(RouteGroupBuilder)`.

## 2. API versioning

`FeatureManagement:EnableVersioning` (default `true`) is read in `Minimal.Api/Program.cs` and
passed as `o.EnableVersioning` to `UseEndpointConfigs`. When enabled, each group's route becomes
`/v{version:apiVersion}{GroupEndpoint}` — for example `/v1/purchase-orders`. The host must already
have called `AddAppVersioning()` (`Minimal.Api/Configs/VersioningConfig.cs`); otherwise registration
throws at startup, before any endpoint is even discovered.

## 3. Authentication / authorization

`FeatureManagement:RequireAuthorization` — `true` in the base `appsettings.json`, so a deployed
service authenticates by default; `appsettings.Development.json` and `appsettings.Testing.json` both
set it to `false`, so `dotnet run` locally and both test suites stay anonymous. It drives two things
at once:

- `Minimal.Api/Configs/AppConfig.cs` only calls `AddAuthConfig()` (JWT bearer auth,
  `Minimal.Api/Configs/Auth/AuthConfig.cs`) when it's `true`.
- The same flag is passed to `UseEndpointConfigs` as `o.RequireAuthorization`, which is applied to
  every route group after mapping and before `IEndpointConfig.Map` runs.

When it is `false` no authentication middleware is added at all — this is not a permissive policy
but the absence of any identity, which is why the base file must never ship it off.

`Minimal.App.Tests/Integration/EndpointConfig/PurchaseOrderStampingAndVersioningTests.cs` pins the
authorization-off behavior explicitly (see below).

`Program.cs` binds `FeatureOptions` from `builder.Configuration` in its first lines, before a
`WebApplicationFactory`'s `ConfigureAppConfiguration` overrides are merged in. A test fixture
therefore cannot flip this flag with an in-memory config entry — only an `appsettings.{Environment}.json`
file or a `FeatureManagement__RequireAuthorization` environment variable lands early enough. See the
remarks on `Minimal.App.Tests/Integration/Support/AuthOnApiFixture.cs`.

## 4. `[FromClaim]` population

Registered once via
`.AddContextualRequestPopulation(o => o.SystemAccountFallback = SharedConsts.SystemAccount)` in
`Minimal.Api/Program.cs`, and applied automatically by `UseEndpointConfigs` for every mapped
endpoint. Any request property marked `[FromClaim(...)]` — for example `ByUser` on
`Minimal.AppServices/ManualSample/V1/Actions/Create.cs` — is **overwritten** from the caller's claim
before validation and before the handler runs. This is a security property, not a model-binding
convenience: whatever the caller put in the body or query string for that member is always
discarded.

`SystemAccountFallback` only substitutes a value when `RequireAuthorization` is `false` *and* the
claim resolver couldn't resolve a value — with the shipped defaults that means local Development and
the test suites, never a deployed service running the base file. An authenticated caller with a
genuinely missing claim never gets the fallback — the member holds its type's default instead, and the handler must reject
it explicitly (see `CreatePurchaseOrderCommandHandler.OnHandle`'s `IsNullOrEmpty(request.ByUser)`
check). Pinned by `AuthorizationOff_CreateIsAttributedToSystemAccount` and
`AuthenticatedCallerWithNoNameClaim_CreateIsRefused_NeverAttributedToSystemAccount` in
`Minimal.App.Tests/Integration/EndpointConfig/PurchaseOrderStampingAndVersioningTests.cs`.

## 5. FluentValidation auto-validation

`Minimal.Api/Configs/FluentValidationConfig.cs` registers `AddFluentValidationAutoValidation()` and
scans the `AppServices` assembly for `AbstractValidator<T>` implementations — for example
`CreatePurchaseOrderCommandValidator` next to `CreatePurchaseOrderRequest`. A request failing
validation never reaches a handler. It short-circuits to a `400` with FluentValidation's
problem-details shape, with no handler code involved.

## 6. Idempotency on POST

Idempotency is opt-in per route, not automatic for every POST.
`Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs`'s create route chains
`.RequiredIdempotentKey()`, which enforces the idempotency key header (default
`X-Idempotency-Key`) on that route — a request missing it is rejected before the handler runs. The
automated sample's generated create route
(`Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs`'s `MapProductCrud()`) makes no such
call; a replayed request there is processed as a brand-new create, not deduplicated. Add
`.RequiredIdempotentKey()` yourself on any route where duplicate submissions matter.

Store selection happens once in `Minimal.Api/Configs/AppConfig.cs`, based on whether
`ConnectionStrings:Redis` is configured:

- **Redis configured** — `AddIdempotencyWithRedisStore(redisConnectionString, o =>
  o.ConflictHandling = IdempotentConflictHandling.CachedResult)`: keys are tracked in Redis, so
  idempotency works correctly across multiple app instances.
- **No Redis** — falls back to the non-generic `AddIdempotentKey(...)` (same `CachedResult`
  conflict handling), an in-process store. Fine for local development, not for a multi-instance
  deployment.

With `IdempotentConflictHandling.CachedResult` (this template's setting), a replayed request with
the same key returns the original cached response rather than re-running the handler or returning a
conflict error.

## 7. Rate limiting

`FeatureManagement:EnableRateLimit` — `true` in the base `appsettings.json`, `false` in the
`Development` and `Testing` overlays — wires
`Minimal.Api/Configs/RateLimits/RateLimitConfig.cs`: a chained `PartitionedRateLimiter` combining a
fixed-window limiter and a concurrency limiter, both keyed per-request by `IRateLimitKeyProvider`
and configured per-request by `IRateLimitOptionsProvider`. A request over either limit is rejected
with `429 Too Many Requests` before reaching routing's endpoint handler.

Limits come from the `RateLimit` section. The base `appsettings.json` sets it explicitly
(`DefaultRequestLimit: 100`, `DefaultConcurrentLimit: 20`, `TimeWindowInSeconds: 1`) — without that
section the limiter would fall back to `RateLimitOptions`'s class defaults of 2 requests per second,
which is an outage rather than a rate limit. Treat the shipped numbers as a placeholder to tune.

## 8. Global exception handling

`Minimal.Api/Configs/GlobalExceptions/GlobalExceptionHandler.cs` is registered as the app's
`IExceptionHandler`. Any unhandled exception from a handler becomes a `ProblemDetails` response:

- `Status` = `500`
- `Title` = `"Something went wrong!."`
- `Detail` and `Type` depend on the hosting environment:
  - in `Development` — `Detail` is the exception's message and `Type` is the exception's type name;
  - outside `Development` (Staging, Production) — `Detail` is the fixed generic string
    `"An unexpected error occurred. Quote the trace-id when reporting this."` and the response
    carries no `type` member at all.
- a `trace-id` extension (the request's `TraceIdentifier`)
- `Instance` = `"{Method} {Path}"`

The `trace-id` and `Instance` values are added by
`Minimal.Api/Configs/GlobalExceptions/GlobalExceptionConfigs.cs`'s `CustomizeProblemDetails`. Once
the detail is generic, that `trace-id` is the correlation handle a caller quotes when reporting the
error — it is the only way to tie the response back to the logged exception. A client never sees a
raw stack trace.

## 9. Health checks and OpenAPI/Scalar

- **Health checks** (`FeatureManagement:EnableHealthCheck`, default `true`) — an EF Core
  connectivity check plus a custom `HealthCheckHandler`, mapped at both `/healthz` and `/` by
  `Minimal.Api/Configs/Healthz/HealthzConfig.cs`.
- **OpenAPI/Scalar** (`FeatureManagement:EnableSwagger`, default `false`) —
  `Minimal.Api/Configs/Swagger/SwaggerConfig.cs` maps the OpenAPI 3.0 document and a Scalar UI at
  `/docs`, pre-configured with a Bearer-auth scheme.
