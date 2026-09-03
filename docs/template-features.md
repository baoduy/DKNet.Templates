# Template Feature List

Everything `dotnet new dknet-minimal` wires up before you write a line of feature code. For the
full list of DKNet NuGet packages behind these features, see
[`docs/dknet-packages.md`](dknet-packages.md).

This page is the flag reference. Every **other** configuration key a generated solution reads —
connection strings, the bearer scheme, CORS, rate limits, Azure App Configuration, telemetry — is
documented key by key in [`configuration-reference.md`](configuration-reference.md), which also
lists the keys that ship in `appsettings*.json` but bind to nothing.

Toggles live in `Minimal.Share/Options/FeatureOptions.cs`, bound from the `FeatureManagement`
config section. A class default only applies when no config file sets the key — the shipped
`appsettings*.json` files win wherever they name a flag, so read
[the flag table below](#featuremanagement-flags) for both values side by side rather than assuming
the class default is what you get.

**Security flags are secure-by-default.** The base `appsettings.json` is what an unmodified service
runs with in Production (the template ships no `appsettings.Production.json`), so it carries the
safe value; the relaxation lives in the `Development` and `Testing` overlays. Keep it that way when
you change a flag — never turn a security switch off in the base file.

## What gets wired

| Feature | What it gives you | Configure it in |
|---|---|---|
| **.NET Aspire orchestration** | `dotnet run --project Minimal.AppHost` provisions Redis + PostgreSQL containers and starts the API wired to both, no manual `docker run`. [Topology below](#aspire-topology). | `Minimal.AppHost/AppHost.cs` |
| **Redis** | Distributed cache backing store and (when configured) the idempotency-key store. | `ConnectionStrings:Redis`; wiring in `Minimal.Api/Configs/CacheConfig.cs` and `AppConfig.cs` |
| **PostgreSQL (Npgsql)** | The only supported EF Core provider — connection, migrations table, retry-on-failure, split queries. | `ConnectionStrings:AppDb`; `Minimal.Infra/Extensions/InfraSetup.cs` |
| **FluentValidation** | Automatic `400` responses for invalid requests — handlers never call `Validate()` themselves. | Add an `AbstractValidator<TRequest>` next to the action; wiring in `Minimal.Api/Configs/FluentValidationConfig.cs` |
| **OpenTelemetry** | ASP.NET Core + HttpClient tracing/metrics; console exporter in DEBUG, OTLP or Azure Monitor otherwise. | `FeatureManagement:EnableOpenTelemetry`, `OTEL_EXPORTER_OTLP_ENDPOINT` / `AzureMonitor:ConnectionString`; `Minimal.Api/Configs/LogConfigs.cs` |
| **Azure App Configuration** | Centralized config + feature flags, 30-min refresh. Disabled automatically in tests. | `FeatureManagement:EnableAzureAppConfig`, plus a `ConnectionStrings:AzureAppConfig` entry — **not** the `AzureAppConfiguration` name the base file ships, which nothing reads; `Minimal.Api/Configs/AzureAppConfig/AzureAppConfigSetup.cs`. Every key: [`configuration-reference.md`](configuration-reference.md#azureappconfig) |
| **JWT bearer auth** | Standard bearer-token auth, plus a sample scope policy and an `IClaimsTransformation` to replace with your own. The shipped `Authentication:Schemes:Bearer` values are placeholders wired to the `--TenantId` and `--ApiAudience` template parameters — replace them before enabling authorization. `ValidAudiences` deliberately lists only the API's own audience, so tokens issued for any other resource are rejected. Authorization is **default-deny**: a fallback policy requires an authenticated user on every endpoint that does not declare itself anonymous, so a route published outside a configured group is not reachable by accident. | `FeatureManagement:RequireAuthorization`, `Authentication:Schemes:Bearer:*`; `Minimal.Api/Configs/Auth/AuthConfig.cs`. Full pipeline order: [`docs/api-pipeline.md`](api-pipeline.md) |
| **API versioning** | URL-segment versioning (`/v1/...`), default version `1.0`. | `FeatureManagement:EnableVersioning`; `Minimal.Api/Configs/VersioningConfig.cs`. Full pipeline order: [`docs/api-pipeline.md`](api-pipeline.md) |
| **CORS allow-list** | Browser front-ends can call the API only from origins you list; with the shipped empty list CORS is not wired at all and no `Access-Control-Allow-*` header is emitted. Origins are absolute — scheme included, no trailing slash. Methods and headers are enumerated, not `AllowAny*`: `GET, POST, PUT, PATCH` (no `DELETE`) and `Authorization, Content-Type, Accept, X-Idempotency-Key`. Never allows credentials. | `Cors:AllowedOrigins`, `Cors:AllowedMethods`, `Cors:AllowedHeaders` — plain configuration arrays in `appsettings*.json`, **not** `FeatureManagement` flags; `Minimal.Api/Configs/CrosConfig.cs`. Details: [`docs/api-pipeline.md`](api-pipeline.md) |
| **Health checks** | EF Core connectivity check plus a custom check, mapped at `/healthz` and `/` — both anonymous and both reporting the overall status only. The full per-check report lives at `/healthz/detail` and requires an authenticated caller. [Details below](#hardened-by-default). | `FeatureManagement:EnableHealthCheck`; `Minimal.Api/Configs/Healthz/HealthzConfig.cs` |
| **Security response headers** | OWASP-recommended headers (`X-Frame-Options`, `X-Content-Type-Options`, a default CSP, `Referrer-Policy`, `Cache-Control`, `X-Permitted-Cross-Domain-Policies`, `X-XSS-Protection`, `Cross-Origin-Resource-Policy`) on every response, including 404s and unhandled 500s. `Server` is not sent at all. | `FeatureManagement:EnableSecurityHeaders`; `Minimal.Api/Configs/SecurityHeadersConfig.cs` (package `OwaspHeaders.Core`) |
| **Forwarded headers** | `X-Forwarded-For` / `X-Forwarded-Proto` honoured from the proxies you list — and from nobody else, so rate limiting sees the real caller behind your ingress. | `FeatureManagement:EnableForwardedHeaders`, `Security:TrustedProxies` (empty by default); `Minimal.Api/Configs/ForwardedHeadersConfig.cs`. Keys: [`configuration-reference.md`](configuration-reference.md#security) |
| **Stated request bounds** | Request lifetime, request-body size and header-read timeout are set by the template instead of inherited from Kestrel — 30 s → `504`, 1 MB → `413`, 10 s of headers. | `FeatureManagement:EnableRequestBounds`, `RequestBounds:*`; `Minimal.Api/Configs/RequestBoundsConfig.cs`. Keys: [`configuration-reference.md`](configuration-reference.md#requestbounds) |
| **Hybrid caching** | `AddHybridCache()` registered (Redis-backed when `ConnectionStrings:Redis` is set, otherwise in-memory). Not yet consumed by any shipped feature — wire it up where you need query caching. | `Minimal.Api/Configs/CacheConfig.cs` |
| **SlimMessageBus** | In-memory child bus always wired for internal command/event dispatch — no flag gates it. The Azure Service Bus child bus is added only when `EnableServiceBus` is on **and** `ConnectionStrings:AzureBus` is non-empty. | `FeatureManagement:EnableServiceBus`, `ConnectionStrings:AzureBus`; `Minimal.Infra/Extensions/ServiceBusSetup.cs`. Deep dive: [`docs/slimbus-messaging.md`](slimbus-messaging.md) |
| **Mapster** | Entity↔request/DTO mapping registered automatically from `[MapsFrom]`/`[GenerateDto]` attributes — no per-feature mapping config. | `Minimal.AppServices/AppSetup.cs`. Attribute mechanics: [`docs/crud-attributes.md`](crud-attributes.md) |
| **Scalar / OpenAPI** | OpenAPI 3.0 document + Scalar UI at `/docs` with a Bearer-auth preset. Anonymous in `Development`; outside it both require an authenticated caller, so the API surface is not readable by anyone who finds the URL. | `FeatureManagement:EnableSwagger`; `Minimal.Api/Configs/Swagger/SwaggerConfig.cs` |
| **Reqnroll + NUnit BDD** | Gherkin feature files exercised against a real `WebApplicationFactory<Program>` host, in-memory DB. | `Minimal.App.BDDTests/` |
| **EF Core migration scripts** | `./add-migration.sh <Name>` / `./remove-migration.sh <Name>`, always targeting `CoreDbContext`. | Run from `src/ApiEndpoints/` |
| **AI-assistant agents, skills, prompts** | Same Claude Code / GitHub Copilot agents, skills, and slash commands the template authors use, copied into every generated solution. | `.claude/agents/`, `.claude/skills/`, `.claude/commands/`; `.github/agents/`, `.github/skills/` (see `.github/skills/CATALOG.md`); `extensions/dknet-implement/` (Spec-Kit validator/implementer for the four DDD layers) |

## Also always on

- **xUnit + Shouldly** unit/integration tests (`Minimal.App.Tests`) alongside the BDD suite above.
- **Contextual claim population** fills request properties tagged `[FromClaim]` from the
  authenticated caller's claims, via the endpoint pipeline, overwriting anything the caller sent.
  Audit-field stamping (`CreatedBy`/`UpdatedBy`) applies the same principle at save time. See
  [`docs/auditing-and-data-ownership.md`](auditing-and-data-ownership.md).
- **NetArchTest architecture tests** (`Minimal.App.Tests/Architecture/`) enforcing internal/sealed
  visibility, max-length on every mapped string, and Npgsql-only package references — these fail
  the build, not just review.
- **Domain events** — raised manually (`AddEvent`) or declaratively (`[RaisesEvent]`), dispatched
  after `SaveChanges` succeeds. See [`docs/efcore-events.md`](efcore-events.md).
- **Specifications** — filtered/paged/projected queries via `DKNet.EfCore.Specifications` instead of
  a raw `IQueryable`. See [`docs/querying-and-specifications.md`](querying-and-specifications.md).

## Hardened by default

A generated service is protected at its HTTP surface with **no security configuration supplied** —
the base `appsettings.json` carries the secure value for every switch below, and a developer relaxes
one for local work through configuration alone. Every key: the
[configuration reference](configuration-reference.md). Where each control sits in the pipeline:
[`docs/api-pipeline.md`](api-pipeline.md).

| Control | What is enforced | Switch |
|---|---|---|
| **Public health probe reports status only** | `/healthz` and `/` stay anonymous and keep evaluating the registered checks — a service with its database down does not report healthy — but the body is `{"status":"Healthy"}` and nothing else: no check name, duration, description or exception text in any state. | `FeatureManagement:EnableHealthCheck` (turns the probes off entirely) |
| **Detailed health report is authenticated** | The full per-check report moved to `/healthz/detail`, which carries `RequireAuthorization()`. | Enforced only while `FeatureManagement:RequireAuthorization` is on — see the caveat below |
| **Rate limiting identifies the real caller** | The limiter partitions on the authenticated user, else on the remote address as rewritten by the forwarded-headers middleware — never on a header the caller can set. Two clients behind one trusted ingress get separate budgets; a peer that is not a listed proxy spends its own. | `Security:TrustedProxies` (empty ⇒ forwarded values ignored), `FeatureManagement:EnableForwardedHeaders`, `FeatureManagement:EnableRateLimit` |
| **Stated request bounds** | 30 s request lifetime (`504`), 1 MB max body (`413`), 10 s to send the request headers — set by the template, not inherited from Kestrel's ~30 MB / 30 s. | `FeatureManagement:EnableRequestBounds`, `RequestBounds:*` |
| **Security headers on every response** | The OWASP header set is written as the response starts, so a `200`, a `404` for an unpublished path and the `500` problem+json from the global exception handler all carry it. `Server` is never sent. `Strict-Transport-Security` has a single owner (`HttpsConfig`), so it is not duplicated. | `FeatureManagement:EnableSecurityHeaders`; max-age via `Https:HstsMaxAgeDays` |
| **Default-deny authorization** | An authorization fallback policy requires an authenticated user on any endpoint that does not declare itself anonymous. The public health probes are the declared exception. | `FeatureManagement:RequireAuthorization` |
| **Documentation is not anonymously readable** | Outside `Development`, the OpenAPI document and the Scalar UI at `/docs` require an authenticated caller, independent of `EnableSwagger`. | `FeatureManagement:EnableSwagger` (turns them off entirely); anonymous in `Development` |
| **Non-root container image** | `dotnet publish /t:PublishContainer` sets the image's default user to the base image's `$APP_UID` instead of root. | `ContainerUser` in `Minimal.Api.csproj` — a source change, not configuration |
| **Vulnerable dependency fails the build** | NuGet auditing is on for direct **and** transitive packages at `moderate` and above, with `NU1901`–`NU1904` promoted to errors, so `dotnet restore` fails and names the offending package. `.github/workflows/build.yml` runs the same restore and build on every pull request and every push to `dev`. | `NuGetAudit*` in `src/Directory.Packages.props` — a source change, not configuration |

Two of these ride on `RequireAuthorization`, because `.RequireAuthorization()` needs authorization
middleware to evaluate it and `AddAuthConfig()` is only called when that flag is on. With
`RequireAuthorization` off — the `Development` and `Testing` overlays — `/healthz/detail` and, in a
non-Development environment, `/docs` are **anonymous**. The base file ships the flag on, so a
deployed service protects both.

Relaxing a control locally is a one-line overlay edit or one environment variable, and each control
is independent — turning security headers off does not affect the request bounds:

```jsonc
// Minimal.Api/appsettings.Development.json — what the template already ships
"FeatureManagement": {
  "EnableSecurityHeaders": false,
  "EnableForwardedHeaders": false,
  "EnableRequestBounds": false
}
```

```bash
# or, for a single run, without touching a file
FeatureManagement__EnableRequestBounds=false dotnet run --project Minimal.Api
```

Never make the same edit in the base `appsettings.json` — that file is what a deployed service runs
with.

## Aspire topology

![Architecture diagram: Minimal.AppHost provisions a Redis container and a PostgreSQL container, adds the AppDb database to the PostgreSQL resource, and starts Minimal.Api with WithReference injecting both connection strings and WaitFor delaying start until the containers are ready; Azure Service Bus sits outside the host as a namespace you bring and configure yourself.](diagrams/templates-aspire-topology.svg)

`Minimal.AppHost/AppHost.cs` is thirteen lines and carries no business logic. It provisions the two
containers, adds the `AppDb` database to the PostgreSQL resource, and starts the API with both
connection strings injected. Azure Service Bus is deliberately **not** orchestrated here — the
commented-out `.WaitFor(bus)` marks where it would go if you added a resource for it. Running
`dotnet run --project Minimal.Api` on its own skips all of it, so `ConnectionStrings:AppDb` and
`ConnectionStrings:Redis` become yours to supply.

## FeatureManagement flags

Class defaults come from `Minimal.Share/Options/FeatureOptions.cs`. The remaining columns are what
the shipped config files actually set. `—` means the file does not name the key, so the value falls
through to the column on its left.

| Flag | Class default | base `appsettings.json` (Production) | `appsettings.Development.json` | `appsettings.Testing.json` |
|---|---|---|---|---|
| `EnableAntiforgery` | `false` | `false` | `false` | — |
| `EnableAzureAppConfig` | `false` | `false` | `false` | — |
| `EnableForwardedHeaders` | `true` | `true` | **`false`** | — |
| `EnableHealthCheck` | `true` | — | — | — |
| `EnableHttps` | `false` | **`true`** | `false` | `false` |
| `EnableOpenTelemetry` | `false` | `false` | — | — |
| `EnableRateLimit` | `true` | `true` | `false` | `false` |
| `EnableRequestBounds` | `true` | `true` | **`false`** | — |
| `EnableSecurityHeaders` | `true` | `true` | **`false`** | — |
| `EnableServiceBus` | `false` | `true` | `false` | — |
| `EnableSwagger` | `false` | `false` | `true` | — |
| `EnableVersioning` | `true` | `true` | — | — |
| `RequireAuthorization` | `false` | **`true`** | `false` | `false` |
| `RunDbMigrationWhenAppStart` | `false` | `false` | `true` | — |

`RequireAuthorization`, `EnableHttps`, `EnableRateLimit`, `EnableSecurityHeaders`,
`EnableForwardedHeaders` and `EnableRequestBounds` are the secure-by-default set: a scaffolded
service deployed without a config change authenticates every request, applies HSTS, rate-limits,
redirects to HTTPS when an HTTPS port is configured, sends the security response headers, bounds
every request, and honours forwarded caller information only from a proxy you listed. What each of
them enforces: [Hardened by default](#hardened-by-default).

The redirect is the one conditional part. `EnableHttps` always adds both `UseHsts()` and
`UseHttpsRedirection()` (`Minimal.Api/Configs/HttpsConfig.cs`), but `UseHttpsRedirection` only
redirects when ASP.NET Core can determine an HTTPS port — from `ASPNETCORE_HTTPS_PORT`, from
`HttpsRedirectionOptions`, or from the server's own listening addresses. The container this template
publishes to (`mcr.microsoft.com/dotnet/aspnet:10.0-alpine`, set as `ContainerBaseImage` in
`Minimal.Api.csproj`) carries none of them, so a service running HTTP-only behind a TLS-terminating
ingress logs `Failed to determine the https port for redirect` and passes the request through
unredirected. Set `ASPNETCORE_HTTPS_PORT` if you need the redirect itself; HSTS is unaffected.

`dotnet run` locally picks up `appsettings.Development.json`, and both test suites
boot under the `Testing` environment
(`Minimal.App.TestSupport/TestApiFactoryBase.cs` calls `UseEnvironment("Testing")`), so neither is
affected. That `appsettings.Testing.json` overlay ships inside the scaffolded solution — it is not
template-only — and it sets `RequireAuthorization`, `EnableHttps` and `EnableRateLimit` all to
`false`. Never run a deployed instance with `ASPNETCORE_ENVIRONMENT=Testing`: it drops all three
protections at once. It does **not** name `EnableSecurityHeaders`, `EnableForwardedHeaders` or
`EnableRequestBounds`, so those three fall through to the base file and stay on under `Testing` —
the `Development` overlay is the only shipped file that relaxes them. To relax a flag for an environment, add it to that environment's overlay — or
set the `FeatureManagement__<Flag>` environment variable, which outranks every JSON file.

Because `EnableRateLimit` is on in the base file, the base file also carries an explicit `RateLimit`
section so the limiter never falls back to `RateLimitOptions`'s 2-requests-per-second class defaults:

| `RateLimit` key | base (Production) | `appsettings.Development.json` |
|---|---|---|
| `DefaultRequestLimit` | `100` | `1` |
| `DefaultConcurrentLimit` | `20` | `1` |
| `TimeWindowInSeconds` | `1` | `10` |

Those production numbers are a placeholder ceiling to tune, not a researched limit for your service.

`EnableServiceBus` is the one flag whose `true` in the base file is not sufficient on its own: it
gates the **Azure** Service Bus child bus, which `Minimal.Infra/Extensions/ServiceBusSetup.cs` adds
only when the flag is on **and** `ConnectionStrings:AzureBus` is non-empty. The in-memory child bus
that carries internal command/event dispatch is registered unconditionally, so turning the flag off
never disables in-process messaging.

> CORS is deliberately absent from this table. It is configured by the `Cors:AllowedOrigins`
> array, not by a flag — an empty array is its off switch. See
> [`docs/api-pipeline.md`](api-pipeline.md).
