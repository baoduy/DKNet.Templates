# Template Feature List

Everything `dotnet new dknet-minimal` wires up before you write a line of feature code. For the
full list of DKNet NuGet packages behind these features, see
[`docs/dknet-packages.md`](dknet-packages.md).

Toggles live in `Minimal.Share/Options/FeatureOptions.cs`, bound once at startup from the
`FeatureManagement` config section (`Program.cs`). Every JSON key under that section matches a
`FeatureOptions` property name one-for-one — the [table at the end of this page](#featuremanagement-flags-minimalshareoptionsfeatureoptionscs)
lists the flags and the value each shipped `appsettings*.json` sets.

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
| **SlimMessageBus** | In-memory bus always wired for internal command/event dispatch; Azure Service Bus child bus added only when the flag is on *and* a connection string is set. | `FeatureManagement:EnableServiceBus`, `ConnectionStrings:AzureBus`; `Minimal.Infra/Extensions/ServiceBusSetup.cs`. Deep dive: [`docs/slimbus-messaging.md`](slimbus-messaging.md) |
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

## FeatureManagement flags (`Minimal.Share/Options/FeatureOptions.cs`)

| Flag | Property default | `appsettings.json` | `appsettings.Development.json` | Gates |
|---|---|---|---|---|
| `EnableAntiforgery` | `false` | `false` | `false` | Antiforgery token validation (`Configs/AppConfig.cs`) |
| `EnableAzureAppConfig` | `false` | `false` | `false` | Loading config + feature flags from Azure App Configuration (`Configs/AzureAppConfig/AzureAppConfigSetup.cs`) |
| `EnableHealthCheck` | `true` | *(not set)* | *(not set)* | `/healthz` and `/` health endpoints (`Configs/Healthz/HealthzConfig.cs`) |
| `EnableHttps` | `false` | `false` | `false` | HTTPS redirection (`Configs/AppConfig.cs`) |
| `EnableOpenTelemetry` | `false` | `false` | *(not set)* | OpenTelemetry logging/tracing/metrics and the OTLP + Azure Monitor exporters (`Configs/LogConfigs.cs`) |
| `EnableRateLimit` | `true` | `false` | `false` | Rate-limiting middleware (`Configs/AppConfig.cs`) |
| `EnableServiceBus` | `false` | `true` | `false` | The **Azure** Service Bus child bus only — the in-memory bus is always registered (`Minimal.Infra/Extensions/ServiceBusSetup.cs`) |
| `EnableSwagger` | `false` | `false` | `true` | OpenAPI document + Scalar UI at `/docs` (`Configs/AppConfig.cs`) |
| `EnableVersioning` | `true` | `true` | *(not set)* | URL-segment API versioning (`Program.cs`, `Configs/AppConfig.cs`) |
| `RequireAuthorization` | `false` | `false` | `false` | Authorization requirement on every mapped endpoint (`Program.cs`, `Configs/AppConfig.cs`) |
| `RunDbMigrationWhenAppStart` | `false` | `false` | `true` | Running EF Core migrations at startup, then exiting (`Configs/DbMigration.cs`) |

*(not set)* means the file omits the key, so the property default applies. `EnableHealthCheck`
appears in no shipped file — health checks are on unless you add the key and set it `false`.

Two notes worth knowing before you flip anything:

- **A key that does not match a property is ignored, not rejected.** `Get<FeatureOptions>()` drops
  unknown keys, so a typo leaves the flag at its property default with no error at startup. Copy the
  spelling from `FeatureOptions.cs`.
- **`EnableOpenTelemetry` ships `false` on purpose**, even though `appsettings.json` also ships
  `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_SERVICE_NAME`. A freshly scaffolded service must not try to
  export to `http://localhost:4317` where nothing is listening. Set it `true` once you have a
  collector, or an `AzureMonitor:ConnectionString`.

Every flag can also be set as an environment variable using the double-underscore form —
`FeatureManagement__EnableSwagger=true` — which is how the integration test fixtures flip them
per host.
