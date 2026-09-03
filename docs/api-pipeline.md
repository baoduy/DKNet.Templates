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
| 0 | Kestrel request limits — max body size, header-read timeout, no `Server` header | `FeatureManagement:EnableRequestBounds` = `true` (`false` in Development); server-level, not middleware |
| 1 | Forwarded headers (`X-Forwarded-For`, `X-Forwarded-Proto`) | `FeatureManagement:EnableForwardedHeaders` = `true` (`false` in Development), `Security:TrustedProxies` empty — forwarded values ignored |
| 2 | Security response headers | `FeatureManagement:EnableSecurityHeaders` = `true` (`false` in Development) |
| 3 | Antiforgery cookie middleware | `FeatureManagement:EnableAntiforgery` = `false` — not wired |
| 4 | CORS | `Cors:AllowedOrigins` empty — CORS not wired |
| 5 | HSTS and HTTPS redirect | `FeatureManagement:EnableHttps` = `true` (`false` in Development/Testing) |
| 6 | Health-check endpoints | `FeatureManagement:EnableHealthCheck` = `true` |
| 7 | Routing and endpoint registration | — |
| 8 | Request timeouts | `FeatureManagement:EnableRequestBounds` = `true`; 30 s, then `504` |
| 9 | Rate limiting | `FeatureManagement:EnableRateLimit` = `true` (`false` in Development/Testing) |
| 10 | Authentication / authorization | `FeatureManagement:RequireAuthorization` = `true` (`false` in Development/Testing) |
| 11 | Global exception handling and OpenAPI/Scalar | `EnableSwagger` = `false` |
| 12 | `[FromClaim]` population (endpoint filter) | — |
| 13 | FluentValidation auto-validation (endpoint filter) | — |
| 14 | Idempotency on POST (endpoint filter) | opt-in per route |
| 15 | Handler | — |

Rows 1–15 are registration order in `Minimal.Api/Configs/AppConfig.cs`'s `UseAppConfig`, which is
execution order; row 0 is not middleware at all — those are Kestrel limits, enforced by the server
before the first middleware sees the request. The sections below explain each stage, grouped for reading rather than re-sorted
into that sequence — the table is the authority on what runs when. Two consequences are easy to get wrong:

- **Rate limiting runs before authentication, validation and idempotency.** An over-limit request
  is answered `429` without a token ever being validated, so a throttled caller never reaches a
  handler, a validator, or the idempotency store.
- **The global exception handler is registered after the endpoints and still wraps them.**
  `WebApplication` appends endpoint execution at the very end of the pipeline, so anything
  registered after `UseEndpointConfigs` still sits upstream of the handler at request time.
- **Forwarded headers and security headers run before everything else.** The first rewrites the
  caller's address before CORS or the rate limiter reads it; the second must sit upstream of the
  exception handler, or an unhandled `500` would answer without security headers.

**API versioning** is absent from the table because it is not a middleware. It shapes the route
template when the group is registered — see [API versioning](#api-versioning) below.

![Workflow diagram of the request pipeline: a request passes the edge middleware that applies forwarded headers, security response headers and CORS, then routing with the request bounds and the rate limiter, then authentication with its default-deny fallback, then the endpoint filters that populate FromClaim members and run FluentValidation, and finally the handler; opt-in routes take a detour through the idempotency filter, and each stage has its own short-circuit response — 413 for an oversized body, 429 or 504, 401 or 403, 400, and the 500 problem+json the global exception handler writes.](diagrams/templates-request-pipeline.svg)

## Forwarded headers

`Minimal.Api/Configs/ForwardedHeadersConfig.cs` is the first middleware in the pipeline, gated on
`FeatureManagement:EnableForwardedHeaders` (default `true`; `false` in the `Development` overlay). It
reads the trusted-proxy list from `Security:TrustedProxies` — **empty in the shipped base file** —
and:

- **empty list** → `ForwardedHeaders.None`. Nothing is honoured: `Connection.RemoteIpAddress` stays
  the immediate peer and `Request.Scheme` stays what the peer actually used. This is deliberate.
  `ForwardedHeadersMiddleware`'s own restriction check is a no-op when `KnownProxies` and
  `KnownIPNetworks` are both empty — it then trusts the header from *any* peer — so "no trusted
  proxy configured" has to switch the feature off outright to actually mean "trust nobody".
- **non-empty list** → `X-Forwarded-For` and `X-Forwarded-Proto` are applied, but only when the
  immediate peer is one of the listed addresses. `KnownProxies` and `KnownIPNetworks` are cleared
  first, so ASP.NET Core's seeded loopback entry is gone unless you list `127.0.0.1` yourself.

A caller that is not a listed proxy therefore cannot claim another client's address — its
`X-Forwarded-For` is ignored and it is rate-limited as itself. Behind an ingress you *do* list,
each client gets its own rate-limit partition instead of all of them sharing the ingress's.

Entries are single IP addresses parsed with `IPAddress.Parse`; a CIDR range is not accepted. Keys:
[`configuration-reference.md`](configuration-reference.md#security).

## Security response headers

`Minimal.Api/Configs/SecurityHeadersConfig.cs` (`FeatureManagement:EnableSecurityHeaders`, default
`true`, `false` in the `Development` overlay) adds the `OwaspHeaders.Core` header set:
`X-Frame-Options`, `X-Content-Type-Options`, a default `Content-Security-Policy`,
`X-Permitted-Cross-Domain-Policies`, `Referrer-Policy`, `Cache-Control`, `X-XSS-Protection` and
`Cross-Origin-Resource-Policy`.

It is registered second — before routing and before the global exception handler — and writes the
headers from `HttpResponse.OnStarting`, so a `200`, a `404` for an unpublished path and the `500`
problem+json the exception handler writes all carry them. The `OnStarting` detour is load-bearing:
the exception-handler path clears the response before writing its own, which would drop headers
added the ordinary way.

`Strict-Transport-Security` is **not** emitted here — `Minimal.Api/Configs/HttpsConfig.cs` owns that
header, with its `max-age` from `Https:HstsMaxAgeDays` (default `365` days, and `preload` requested
only at 365 days or more). One owner, so the header is never sent twice.

## Request bounds

`Minimal.Api/Configs/RequestBoundsConfig.cs` (`FeatureManagement:EnableRequestBounds`, default
`true`, `false` in the `Development` overlay) states three bounds the template would otherwise
inherit from Kestrel, all from the `RequestBounds` section:

| Bound | Default | Over the bound |
|---|---|---|
| `RequestTimeoutSeconds` | `30` | `504 Gateway Timeout`, from the default request-timeout policy |
| `MaxRequestBodySizeBytes` | `1048576` (1 MB) | `413 Payload Too Large`, from Kestrel |
| `RequestHeadersTimeoutSeconds` | `10` | Kestrel drops the connection while the headers are still arriving |

Only the timeout is middleware — `UseRequestTimeouts()` sits right after `UseRouting()`, as ASP.NET
Core requires, which means it bounds endpoint execution. The other two are server limits enforced
before any middleware runs, so a request that is both oversized and slow is rejected on size first.
The same module sets `AddServerHeader = false`: no response names the web-server product.

## CORS

`Minimal.Api/Configs/CrosConfig.cs` reads the `Cors:AllowedOrigins` string array and is
**deny-by-default**. When the key is absent, the array is empty, or every entry is blank, neither
`AddCors(...)` nor the `UseCors()` middleware is registered at all — a cross-origin request is still
served, but the response carries no `Access-Control-Allow-*` header, so the browser refuses to hand
it to the calling page. This is "not wired", not "wired but permissive". When the array is
non-empty, the default policy allows exactly those origins and exactly the methods and headers
enumerated in `Cors:AllowedMethods` (default `GET, POST, PUT, PATCH` — **no `DELETE`**) and
`Cors:AllowedHeaders` (default `Authorization`, `Content-Type`, `Accept`, `X-Idempotency-Key`, the
header the template's own create route requires); an origin, method or header that isn't listed is
never reflected back, so its preflight fails. Credentials are never allowed — `AllowCredentials()`
is not called on any path.

Both lists fall back to those defaults only when the key is absent. A key present but empty (`[]`)
means "nothing allowed" and is not widened back. Per-key detail:
[`configuration-reference.md`](configuration-reference.md#cors).

Entries are absolute origins: scheme included, no trailing slash and no path —
`https://app.example.com`, not `app.example.com` or `https://app.example.com/`.

The checked-in `Minimal.Api/appsettings.json` ships an empty list, so a service deployed with the
template defaults is closed to browsers. `Minimal.Api/appsettings.Development.json` lists the local
SPA dev-server origins `http://localhost:3000` and `http://localhost:5173`:

```json
"Cors": {
  "AllowedOrigins": [ "http://localhost:3000", "http://localhost:5173" ]
}
```

`UseCrosConfig()` runs from `Minimal.Api/Configs/AppConfig.cs` before `UseRouting()`, so the policy
covers every endpoint including the CORS preflight `OPTIONS` request.

> **Breaking behavioural change when you regenerate from the template.** The previous revision
> registered `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` unconditionally, so any web page
> could call a scaffolded service. A browser front-end that used to work must now have its origin
> listed in `Cors:AllowedOrigins` for the environment it talks to.

## Routing and endpoint registration

Every route group is an `IEndpointConfig` (`DKNet.AspCore.Extensions`) — for example
`Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs` and
`Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs`. `Minimal.Api/Program.cs` calls
`UseEndpointConfigs(...)`, which discovers every non-abstract `IEndpointConfig` in the app assembly.
For each one it builds a versioned route group and calls its `Map(RouteGroupBuilder)`.

## API versioning

`FeatureManagement:EnableVersioning` (default `true`) is read in `Minimal.Api/Program.cs` and
passed as `o.EnableVersioning` to `UseEndpointConfigs`. When enabled, each group's route becomes
`/v{version:apiVersion}{GroupEndpoint}` — for example `/v1/purchase-orders`. The host must already
have called `AddAppVersioning()` (`Minimal.Api/Configs/VersioningConfig.cs`); otherwise registration
throws at startup, before any endpoint is even discovered.

## Authentication / authorization

`FeatureManagement:RequireAuthorization` — `true` in the base `appsettings.json`, so a deployed
service authenticates by default; `appsettings.Development.json` and `appsettings.Testing.json` both
set it to `false`, so `dotnet run` locally and both test suites stay anonymous. It drives two things
at once:

- `Minimal.Api/Configs/AppConfig.cs` only calls `AddAuthConfig()` (JWT bearer auth,
  `Minimal.Api/Configs/Auth/AuthConfig.cs`) when it's `true`.
- The same flag is passed to `UseEndpointConfigs` as `o.RequireAuthorization`, which is applied to
  every route group after mapping and before `IEndpointConfig.Map` runs.

Authorization is **default-deny** when the flag is on: `AddAuthConfig()` sets
`options.FallbackPolicy` to `RequireAuthenticatedUser()`, so an endpoint that neither declares a
policy nor declares itself anonymous still requires an authenticated caller — a route published
outside a configured group is not anonymous by accident. The public health probes at `/healthz` and
`/` are the declared exception (`.AllowAnonymous()`).

When it is `false` no authentication middleware is added at all — this is not a permissive policy
but the absence of any identity, which is why the base file must never ship it off.

`Minimal.App.Tests/Integration/EndpointConfig/PurchaseOrderStampingAndVersioningTests.cs` pins the
authorization-off behavior explicitly (see below).

`Program.cs` binds `FeatureOptions` from `builder.Configuration` in its first lines, before a
`WebApplicationFactory`'s `ConfigureAppConfiguration` overrides are merged in. A test fixture
therefore cannot flip this flag with an in-memory config entry — only an `appsettings.{Environment}.json`
file or a `FeatureManagement__RequireAuthorization` environment variable lands early enough. See the
remarks on `Minimal.App.Tests/Integration/Support/AuthOnApiFixture.cs`.

## `[FromClaim]` population

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

## FluentValidation auto-validation

`Minimal.Api/Configs/FluentValidationConfig.cs` registers `AddFluentValidationAutoValidation()` and
scans the `AppServices` assembly for `AbstractValidator<T>` implementations — for example
`CreatePurchaseOrderCommandValidator` next to `CreatePurchaseOrderRequest`. A request failing
validation never reaches a handler. It short-circuits to a `400` with FluentValidation's
problem-details shape, with no handler code involved.

## Idempotency on POST

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

## Rate limiting

`FeatureManagement:EnableRateLimit` — `true` in the base `appsettings.json`, `false` in the
`Development` and `Testing` overlays — wires
`Minimal.Api/Configs/RateLimits/RateLimitConfig.cs`: a chained `PartitionedRateLimiter` combining a
fixed-window limiter and a concurrency limiter, both keyed per-request by `IRateLimitKeyProvider`
and configured per-request by `IRateLimitOptionsProvider`. A request over either limit is rejected
with `429 Too Many Requests` — and because `UseRateLimiter()` runs immediately after `UseRouting()`
and before `UseAuthConfig()`, that rejection happens before the caller is authenticated and long
before any endpoint filter runs.

The partition key comes from `Minimal.Api/Configs/RateLimits/RateLimitKeyProvider.cs`:
`User.Identity.Name`, falling back to the remote IP address, falling back to the request host.
Since the limiter runs before authentication, `User.Identity.Name` is only populated for a caller
already authenticated by an earlier middleware — in practice the shipped pipeline partitions by IP.

That IP is `Connection.RemoteIpAddress` as the [forwarded-headers
middleware](#forwarded-headers) left it: the real client when the immediate peer is a proxy listed
in `Security:TrustedProxies`, and the immediate peer itself otherwise. The provider never parses
`X-Forwarded-For` on its own, so an untrusted peer's forwarded claim spends that peer's own budget
rather than someone else's. Behind an ingress that is not listed — the shipped default, since the
list is empty — every client shares one partition; list the ingress and they are separated again.
Both providers are public interfaces you can replace; see
[`extension-points.md`](extension-points.md#rate-limiting).

Limits come from the `RateLimit` section. The base `appsettings.json` sets it explicitly
(`DefaultRequestLimit: 100`, `DefaultConcurrentLimit: 20`, `TimeWindowInSeconds: 1`) — without that
section the limiter would fall back to `RateLimitOptions`'s class defaults of 2 requests per second,
which is an outage rather than a rate limit. Treat the shipped numbers as a placeholder to tune.

## Global exception handling

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

## Health checks and OpenAPI/Scalar

- **Health checks** (`FeatureManagement:EnableHealthCheck`, default `true`) — a `CoreDbContext`
  connectivity check plus a custom `HealthCheckHandler`, mapped by
  `Minimal.Api/Configs/Healthz/HealthzConfig.cs` at three paths:
  - `/healthz` and `/` — anonymous (`.AllowAnonymous()`, so the default-deny fallback policy does
    not apply). Both evaluate every registered check, so a service whose database is unreachable
    does not report healthy, but the body is the overall status and nothing else —
    `{"status":"Healthy"}`. No check name, duration, description or exception text is ever written
    to this surface, in any state.
  - `/healthz/detail` — the full per-check report, carrying `.RequireAuthorization()`.
- **OpenAPI/Scalar** (`FeatureManagement:EnableSwagger`, default `false`) —
  `Minimal.Api/Configs/Swagger/SwaggerConfig.cs` maps the OpenAPI 3.0 document and a Scalar UI at
  `/docs`, pre-configured with a Bearer-auth scheme. Outside `Development` both carry
  `.RequireAuthorization()`, so a deployed service's API surface is not readable by whoever finds
  the URL; in `Development` they stay anonymous.

Both of those `RequireAuthorization()` calls need authorization middleware to evaluate them, and
that is only wired when `FeatureManagement:RequireAuthorization` is on. With authorization off — the
`Development` and `Testing` overlays — `/healthz/detail` is anonymous, and so is `/docs` in a
non-Development environment. The base `appsettings.json` ships the flag on, so a deployed service
protects both.
