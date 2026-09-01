# Template Feature List

Everything `dotnet new dknet-minimal` wires up before you write a line of feature code. For the
full list of DKNet NuGet packages behind these features, see
[`docs/dknet-packages.md`](dknet-packages.md).

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
| **.NET Aspire orchestration** | `dotnet run --project Minimal.AppHost` provisions Redis + PostgreSQL containers and starts the API wired to both, no manual `docker run`. | `Minimal.AppHost/AppHost.cs` |
| **Redis** | Distributed cache backing store and (when configured) the idempotency-key store. | `ConnectionStrings:Redis`; wiring in `Minimal.Api/Configs/CacheConfig.cs` and `AppConfig.cs` |
| **PostgreSQL (Npgsql)** | The only supported EF Core provider — connection, migrations table, retry-on-failure, split queries. | `ConnectionStrings:AppDb`; `Minimal.Infra/Extensions/InfraSetup.cs` |
| **FluentValidation** | Automatic `400` responses for invalid requests — handlers never call `Validate()` themselves. | Add an `AbstractValidator<TRequest>` next to the action; wiring in `Minimal.Api/Configs/FluentValidationConfig.cs` |
| **OpenTelemetry** | ASP.NET Core + HttpClient tracing/metrics; console exporter in DEBUG, OTLP or Azure Monitor otherwise. | `FeatureManagement:EnableOpenTelemetry`, `OTEL_EXPORTER_OTLP_ENDPOINT` / `AzureMonitor:ConnectionString`; `Minimal.Api/Configs/LogConfigs.cs` |
| **Azure App Configuration** | Centralized config + feature flags, 30-min refresh. Disabled automatically in tests. | `FeatureManagement:EnableAzureAppConfig`, `ConnectionStrings:AzureAppConfiguration`; `Minimal.Api/Configs/AzureAppConfig/AzureAppConfigSetup.cs` |
| **JWT bearer auth** | Standard bearer-token auth, plus a sample scope policy and an `IClaimsTransformation` to replace with your own. The shipped `Authentication:Schemes:Bearer` values are placeholders wired to the `--TenantId` and `--ApiAudience` template parameters — replace them before enabling authorization. `ValidAudiences` deliberately lists only the API's own audience, so tokens issued for any other resource are rejected. | `FeatureManagement:RequireAuthorization`, `Authentication:Schemes:Bearer:*`; `Minimal.Api/Configs/Auth/AuthConfig.cs`. Full pipeline order: [`docs/api-pipeline.md`](api-pipeline.md) |
| **API versioning** | URL-segment versioning (`/v1/...`), default version `1.0`. | `FeatureManagement:EnableVersioning`; `Minimal.Api/Configs/VersioningConfig.cs`. Full pipeline order: [`docs/api-pipeline.md`](api-pipeline.md) |
| **Health checks** | EF Core connectivity check mapped at `/healthz` and `/`. | `FeatureManagement:EnableHealthCheck`; `Minimal.Api/Configs/Healthz/HealthzConfig.cs` |
| **Hybrid caching** | `AddHybridCache()` registered (Redis-backed when `ConnectionStrings:Redis` is set, otherwise in-memory). Not yet consumed by any shipped feature — wire it up where you need query caching. | `Minimal.Api/Configs/CacheConfig.cs` |
| **SlimMessageBus** | In-memory child bus always wired for internal command/event dispatch — no flag gates it. The Azure Service Bus child bus is added only when `EnableServiceBus` is on **and** `ConnectionStrings:AzureBus` is non-empty. | `FeatureManagement:EnableServiceBus`, `ConnectionStrings:AzureBus`; `Minimal.Infra/Extensions/ServiceBusSetup.cs`. Deep dive: [`docs/slimbus-messaging.md`](slimbus-messaging.md) |
| **Mapster** | Entity↔request/DTO mapping registered automatically from `[MapsFrom]`/`[GenerateDto]` attributes — no per-feature mapping config. | `Minimal.AppServices/AppSetup.cs`. Attribute mechanics: [`docs/crud-attributes.md`](crud-attributes.md) |
| **Scalar / OpenAPI** | OpenAPI 3.0 document + Scalar UI at `/docs` with a Bearer-auth preset. | `FeatureManagement:EnableSwagger`; `Minimal.Api/Configs/Swagger/SwaggerConfig.cs` |
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

## FeatureManagement flags

Class defaults come from `Minimal.Share/Options/FeatureOptions.cs`. The remaining columns are what
the shipped config files actually set. `—` means the file does not name the key, so the value falls
through to the column on its left.

| Flag | Class default | base `appsettings.json` (Production) | `appsettings.Development.json` | `appsettings.Testing.json` |
|---|---|---|---|---|
| `EnableAntiforgery` | `false` | `false` | `false` | — |
| `EnableAzureAppConfig` | `false` | `false` | `false` | — |
| `EnableHealthCheck` | `true` | — | — | — |
| `EnableHttps` | `false` | **`true`** | `false` | `false` |
| `EnableOpenTelemetry` | `false` | `false` | — | — |
| `EnableRateLimit` | `true` | `true` | `false` | `false` |
| `EnableServiceBus` | `false` | `true` | `false` | — |
| `EnableSwagger` | `false` | `false` | `true` | — |
| `EnableVersioning` | `true` | `true` | — | — |
| `RequireAuthorization` | `false` | **`true`** | `false` | `false` |
| `RunDbMigrationWhenAppStart` | `false` | `false` | `true` | — |

`RequireAuthorization`, `EnableHttps` and `EnableRateLimit` are the secure-by-default set: a
scaffolded service deployed without a config change authenticates every request, applies HSTS,
rate-limits, and redirects to HTTPS when an HTTPS port is configured.

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
protections at once. To relax a flag for an environment, add it to that environment's overlay — or
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
