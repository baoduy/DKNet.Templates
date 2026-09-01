# Template Feature List

Everything `dotnet new dknet-minimal` wires up before you write a line of feature code. For the
full list of DKNet NuGet packages behind these features, see
[`docs/dknet-packages.md`](dknet-packages.md).

Toggles live in `Minimal.Share/Options/FeatureOptions.cs`, bound from the `FeatureManagement`
config section. A class default only applies when no config file sets the key — the shipped
`appsettings*.json` files win wherever they name a flag, so read
[the flag table below](#featuremanagement-flags) for both values side by side rather than assuming
the class default is what you get. A couple of the checked-in JSON keys have drifted from the
property names, noted at the end of this page.

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
| **JWT bearer auth** | Standard bearer-token auth, optional MS Graph token handler swap-in. | `FeatureManagement:RequireAuthorization`, `Authentication:Schemes:Bearer:*`; `Minimal.Api/Configs/Auth/AuthConfig.cs`. Full pipeline order: [`docs/api-pipeline.md`](api-pipeline.md) |
| **API versioning** | URL-segment versioning (`/v1/...`), default version `1.0`. | `FeatureManagement:EnableVersioning`; `Minimal.Api/Configs/VersioningConfig.cs`. Full pipeline order: [`docs/api-pipeline.md`](api-pipeline.md) |
| **Health checks** | EF Core connectivity check mapped at `/healthz` and `/`. | `FeatureManagement:EnableHealthCheck`; `Minimal.Api/Configs/Healthz/HealthzConfig.cs` |
| **Hybrid caching** | `AddHybridCache()` registered (Redis-backed when `ConnectionStrings:Redis` is set, otherwise in-memory). Not yet consumed by any shipped feature — wire it up where you need query caching. | `Minimal.Api/Configs/CacheConfig.cs` |
| **SlimMessageBus** | In-memory bus always wired for internal command/event dispatch; Azure Service Bus child bus added only when configured. | `ConnectionStrings:AzureBus`; `Minimal.Infra/Extensions/ServiceBusSetup.cs`. Deep dive: [`docs/slimbus-messaging.md`](slimbus-messaging.md) |
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
| `EnableAzureAppConfig` | `false` | — (drifted key, see below) | `false` | — |
| `EnableHealthCheck` | `true` | — | — | — |
| `EnableHttps` | `false` | **`true`** | `false` | `false` |
| `EnableMsGraphJwtTokenValidation` | `false` | — | — | — |
| `EnableOpenTelemetry` | `false` | — | — | — |
| `EnableRateLimit` | `true` | `true` | `false` | `false` |
| `EnableServiceBus` | `false` | — (drifted key, see below) | — (drifted key) | — |
| `EnableSwagger` | `false` | `false` | `true` | — |
| `EnableVersioning` | `true` | `true` | — | — |
| `RequireAuthorization` | `false` | **`true`** | `false` | `false` |
| `RunDbMigrationWhenAppStart` | `false` | `false` | `true` | — |

`RequireAuthorization`, `EnableHttps` and `EnableRateLimit` are the secure-by-default set: a
scaffolded service deployed without a config change authenticates every request, redirects to HTTPS
and rate-limits. `dotnet run` locally picks up `appsettings.Development.json`, and both test suites
boot under the `Testing` environment
(`Minimal.App.TestSupport/TestApiFactoryBase.cs` calls `UseEnvironment("Testing")`), so neither is
affected. To relax a flag for an environment, add it to that environment's overlay — or set the
`FeatureManagement__<Flag>` environment variable, which outranks every JSON file.

Because `EnableRateLimit` is on in the base file, the base file also carries an explicit `RateLimit`
section so the limiter never falls back to `RateLimitOptions`'s 2-requests-per-second class defaults:

| `RateLimit` key | base (Production) | `appsettings.Development.json` |
|---|---|---|
| `DefaultRequestLimit` | `100` | `1` |
| `DefaultConcurrentLimit` | `20` | `1` |
| `TimeWindowInSeconds` | `1` | `10` |

Those production numbers are a placeholder ceiling to tune, not a researched limit for your service.

> The generated `Minimal.Api/appsettings.json` currently sets a couple of these under drifted key
> names: `EnableServiceBusProcess` and `EnableAzureAppConfiguration`, instead of `EnableServiceBus`
> and `EnableAzureAppConfig`. Those two JSON entries silently no-op against the class defaults
> above. When changing a flag, verify the key against `FeatureOptions.cs`, not the checked-in JSON.
