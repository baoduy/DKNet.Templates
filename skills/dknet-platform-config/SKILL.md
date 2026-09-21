---
name: dknet-platform-config
description: Reference for everything the template wires outside a feature's own vertical slice — Program.cs start-up order, the FeatureManagement flag table, every configuration section and its defaults, launch-time jobs, the Aspire host, and the test-host overrides. Use when adding or changing a platform-level behavior (auth, rate limiting, health checks, telemetry, caching, CORS, a new flag, a new launch job) rather than a business feature.
---

# DKNet platform configuration

Everything a generated solution wires before a business feature exists. For a feature's own
vertical slice (entity → endpoint), use `dknet-project-structure` and `dknet-feature-lifecycle`
instead — this skill only covers cross-cutting platform wiring.

## Start-up order

`Minimal.Api/Program.cs`, in the order it actually runs:

```csharp
var builder = WebApplication.CreateBuilder(args);
var feature = builder.Configuration.GetSection(FeatureOptions.Name).Get<FeatureOptions>() ?? new FeatureOptions();

builder.AddLogConfig(feature).AddAzureAppConfig(feature);

var jobSelection = JobSelector.Select(args, JobRegistry.Jobs.Keys.ToArray());
if (jobSelection.HasJobName)
{
    if (!jobSelection.IsRecognized) { /* print known jobs, exit 1 */ }
    return await JobRegistry.Jobs[jobSelection.RequestedJobName!](builder);
}

builder.AddFluentValidationConfig();
await builder.RunMigrationAsync(feature);   // only if RunDbMigrationWhenAppStart

builder.Services
    .AddOptions(builder.Configuration)
    .AddAppConfig(feature, builder.Configuration)
    .AddContextualRequestPopulation();      // populates [FromClaim] members

await builder.Build().UseAppConfig(a => a.UseEndpointConfigs(o =>
{
    o.RequireAuthorization = feature.RequireAuthorization;
    o.EnableVersioning = feature.EnableVersioning;
    o.ConfigureGroup = (group, _) => group.AddFluentValidationAutoValidation();
}, typeof(Program).Assembly));
```

Why this order matters:

- `AddLogConfig` and `AddAzureAppConfig` run **before** the job-name check, so a job (the
  `migration` job included) resolves configuration from exactly the same sources — Azure App
  Configuration included — as the serving path.
- Job dispatch happens **before any service is registered** — a job binds no HTTP listener and
  opens no message-bus connection. See [Launch-time jobs](#launch-time-jobs).
- `FeatureOptions` is bound once, at the very top, from `builder.Configuration` as it stood at that
  instant. A `WebApplicationFactory`'s `ConfigureAppConfiguration` override merges its settings
  later — too late to change a flag. Only an `appsettings.{Environment}.json` file or a
  `FeatureManagement__<Flag>` environment variable lands early enough. This is why
  `TestApiFactoryBase` (below) pushes overrides through `ConfigureAppConfiguration` with an
  in-memory collection, not through `ConfigureServices`.
- `RunMigrationAsync` runs after validation config but before any request-serving service is
  registered, so a failed migration (`RunDbMigrationWhenAppStart`) throws and the process never
  starts serving with an un-migrated schema.

`AppConfig.AddAppConfig` (service registration) and `AppConfig.UseAppConfig` (middleware) are the
two composition points everything else in this skill hangs off:

```csharp
// AddAppConfig — service registration order
if (EnableAntiforgery) AddAntiforgeryConfig();
if (RequireAuthorization && EnableDemoAuthentication) throw new InvalidOperationException(...); // mutually exclusive
if (RequireAuthorization) AddAuthConfig(); else if (EnableDemoAuthentication) AddDemoAuthConfig();
if (EnableSwagger) AddOpenApiDoc();
if (EnableHttps) AddHttpsConfig(configuration);
if (EnableRateLimit) AddRateLimitConfig(configuration);
if (EnableVersioning) AddAppVersioning();
AddForwardedHeadersConfig(features, configuration).AddSecurityHeadersConfig(features).AddRequestBoundsConfig(features, configuration);
AddHttpContextAccessor().AddFeatureManagement();
CacheConfig(configuration);
// Redis connection string set -> AddIdempotencyWithRedisStore, else AddIdempotentKey (in-memory)
AddCrosConfig(configuration).AddAllAppServices(configuration, features).AddHealthzConfig(features);
```

```csharp
// UseAppConfig — middleware order, with the reason for each position
UseAzureAppConfig()           // logs that it's enabled
    .UseForwardedHeadersConfig()  // must rewrite RemoteIpAddress before CORS/rate-limit read it
    .UseSecurityHeadersConfig()   // wraps everything downstream (200/404/500 alike)
    .UseAntiforgeryConfig()
    .UseCrosConfig()
    .UseHttpsConfig()
    .UseHealthzConfig();
UseRouting();
UseRequestBoundsConfig();
UseRateLimitConfig();
UseAuthConfig();               // must be after UseRouting
// -- UseEndpointConfigs runs here (extra?.Invoke(app)) --
UseOpenApiDoc();                // must be after UseEndpoints
```

Every `Add*Config`/`Use*Config` pair follows the same shape: `Add*` calls
`services.MarkConfigAdded(nameof(XConfig))` (a keyed singleton scoped to that host's own
`IServiceProvider` — see `HostConfigMarker`), and `Use*` checks
`app.Services.IsConfigAdded(nameof(XConfig))` before doing anything. This is why two
`WebApplicationFactory` instances built in the same test process never leak each other's feature
state, and why a flag that's off means the corresponding middleware is never added, not merely
skipped at request time.

`ServiceConfigs.AddAllAppServices` (called from inside `AddAppConfig`) wires the acting-user
providers (`IPrincipalProvider` as both `IDataOwnerProvider` and `ICurrentUserProvider`, see
`dknet-auth-and-ownership`), then `AddAppServices().AddInfraServices().AddServiceBus(...)`.
`ServiceConfigs.AddOptions` (called from `Program.cs`, before `AddAppConfig`) binds
`FeatureOptions` for `IOptions<FeatureOptions>` injection, configures JSON serializer options
(naming policy, ignore conditions, converters — see `SharedConsts.JsonSerializerOptions`), and
wires role-aware sensitive-data filtering onto the same `JsonOptions` instance via a factory
registration (needed because `ConfigureHttpJsonOptions` has no service-provider access).

## `FeatureManagement` flags

Section name is `FeatureManagement` (`FeatureOptions.Name`), bound in `Minimal.Api/Program.cs` via
`GetSection(FeatureOptions.Name).Get<FeatureOptions>()`. **Every JSON key must spell a
`FeatureOptions` property name exactly** — `Get<FeatureOptions>()` silently ignores an unknown key
instead of failing, so a typo no-ops rather than erroring.

| Flag | Class default | base `appsettings.json` | `Development` overlay | `Testing` overlay | Wires |
|---|---|---|---|---|---|
| `EnableAntiforgery` | `false` | `false` | `false` | — | `Configs/Antiforgery/AntiforgeryConfig.cs` |
| `EnableAzureAppConfig` | `false` | `false` | `false` | — | `Configs/AzureAppConfig/AzureAppConfigSetup.cs` |
| `EnableDemoAuthentication` | `false` | — (false) | **`true`** | **`true`** | `Configs/Auth/DemoAuthConfig.cs` |
| `EnableForwardedHeaders` | `true` | `true` | **`false`** | — | `Configs/ForwardedHeadersConfig.cs` |
| `EnableHealthCheck` | `true` | — | — | — | `Configs/Healthz/HealthzConfig.cs` |
| `EnableHttps` | `false` | **`true`** | `false` | `false` | `Configs/HttpsConfig.cs` |
| `EnableOpenTelemetry` | `false` | `false` | — | — | `Configs/LogConfigs.cs` |
| `EnableRateLimit` | `true` | `true` | `false` | `false` | `Configs/RateLimits/RateLimitConfig.cs` |
| `EnableRequestBounds` | `true` | `true` | **`false`** | — | `Configs/RequestBoundsConfig.cs` |
| `EnableSecurityHeaders` | `true` | `true` | **`false`** | — | `Configs/SecurityHeadersConfig.cs` |
| `EnableServiceBus` | `false` | `true` | `false` | — | `Minimal.Infra/Extensions/ServiceBusSetup.cs` (Azure child bus only) |
| `EnableSwagger` | `false` | `false` | `true` | — | `Configs/Swagger/SwaggerConfig.cs` |
| `EnableVersioning` | `true` | `true` | — | — | `Configs/VersioningConfig.cs` |
| `RequireAuthorization` | `false` | **`true`** | `false` | `false` | `Configs/Auth/AuthConfig.cs` |
| `RunDbMigrationWhenAppStart` | `false` | `false` | `true` | — | `Configs/DbMigration.cs` |

`—` means the file doesn't name the key; the value falls through to the column on its left. The
base file is what a deployed service runs with (no `appsettings.Production.json` ships), so it
always carries the secure value; relaxation lives in the `Development`/`Testing` overlays. Never
turn a security flag off in the base file — `SecureDefaultAppSettingsTests` (in the shipped test
suite) fails the build if you do.

Two mutual-exclusion rules, both enforced at start-up (`InvalidOperationException`, not a silent
pick): `RequireAuthorization` and `EnableDemoAuthentication` cannot both be `true`. `EnableServiceBus`
alone does nothing — the Azure child bus is added only when the flag is `true` **and**
`ConnectionStrings:AzureBus` is non-empty; the in-memory child bus that carries internal
command/event dispatch is unconditional.

**Adding a flag**: add the `bool` property to `Minimal.Share/Options/FeatureOptions.cs`, add the
same-spelled key to every `appsettings*.json` that needs a non-default value, and consume it either
as `features.YourFlag` inside `AppConfig.cs`/`ServiceConfigs.cs` (both already receive a
`FeatureOptions features` parameter) or via `IOptions<FeatureOptions>` injected anywhere else in DI.

## Configuration sections

| Section | Keys | Shipped default | Reads |
|---|---|---|---|
| `ConnectionStrings` | `AppDb`, `Redis`, `AzureBus`, `AzureAppConfig` | all `""` except overlays | `AppDb` → `Minimal.Infra/Extensions/InfraSetup.cs`, `DbMigration.cs`; `Redis` → `CacheConfig.cs`, `AppConfig.cs` (idempotency store); `AzureBus` → `ServiceBusSetup.cs`; `AzureAppConfig` → `AzureAppConfigSetup.cs` |
| `Authentication:Schemes:Bearer` | `MetadataAddress`, `ValidAudiences`, `ValidIssuer` | placeholder tenant/audience | bound by ASP.NET Core's own `AddJwtBearer()`; registered only when `RequireAuthorization` is on |
| `Cors` | `AllowedOrigins` (`[]`), `AllowedMethods` (`GET,POST,PUT,PATCH`), `AllowedHeaders` (`Authorization,Content-Type,Accept,X-Idempotency-Key`) | empty origins ⇒ CORS not wired at all | `Configs/CrosConfig.cs` |
| `Security` | `TrustedProxies` (IP list), `TrustedNetworks` (CIDR list) | both `[]` | `Configs/ForwardedHeadersConfig.cs` — both empty ⇒ `ForwardedHeaders.None` |
| `Https` | `HstsMaxAgeDays` | `365` | `Configs/HttpsConfig.cs` — preload only when ≥ 365 |
| `RequestBounds` | `RequestTimeoutSeconds` (30), `MaxRequestBodySizeBytes` (1048576), `RequestHeadersTimeoutSeconds` (10) | as class defaults | `Configs/RequestBoundsConfig.cs` |
| `RateLimit` | `DefaultRequestLimit`, `DefaultConcurrentLimit`, `TimeWindowInSeconds` | class default `2/2/1s`; base file `100/20/1s`; Development `1/1/10s` | `Configs/RateLimits/RateLimitConfig.cs` |
| `AzureAppConfiguration` | `KeyPrefix`, `Label`, `CacheExpirationInSeconds`, `LoadFeatureFlags`, `FeatureFlagPrefix` | ships in base file | **dead** — see below |
| `AzureAppConfig` | `ConnectionStringName` (`AzureAppConfig`), `Label` (`null`→`SharedConsts.ApiName`), `LoadFeatureFlags`, `FeatureFlagPrefix`, `RefreshIntervalInMinutes` | not shipped in base file | `Configs/AzureAppConfig/AzureAppConfigSetup.cs` — the last three properties are declared but never read; refresh is hard-coded to 30 minutes |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME` | flat keys, not a section | `http://localhost:4317`; `Minimal.Api` | `Configs/LogConfigs.cs` reads only the endpoint key as a presence check; `OTEL_SERVICE_NAME` is never read by template code |
| `AzureMonitor:ConnectionString` | — | `""` | `Configs/LogConfigs.cs` — non-blank adds `UseAzureMonitor()` |
| `DKNet:ListQuery` | `DefaultPageSize`, `MaxPageSize`, `DefaultActivityWindowMonths` | package defaults (1000/1000/3) | consumed by the generic `MapGetList<TEntity,TKey,TDto>` route |
| `SampleData:RecordsPerEntity` | — | `10000` | `Minimal.AppHost/appsettings.json`, read by `AppHost.cs` only — never present in the API |

`AzureAppConfiguration` (note the longer name) is a real trap: it ships in the base
`appsettings.json` but binds to nothing — `AzureAppConfigOptions.Name` is `"AzureAppConfig"`, a
different section name, and `KeyPrefix`/`CacheExpirationInSeconds` aren't even properties on that
options class. Setting any key under `AzureAppConfiguration` changes no behavior.

Override any key as an environment variable by replacing `:` with `__`, e.g.
`FeatureManagement__RequireAuthorization=false`. Environment variables outrank every JSON file but
lose to Azure App Configuration (added last, after `WebApplication.CreateBuilder(args)` has already
merged everything else).

## Platform features, one at a time

- **Forwarded headers** (`Configs/ForwardedHeadersConfig.cs`) — honors `X-Forwarded-For`/`-Proto`
  only from `Security:TrustedProxies` (exact `IPAddress.Parse`) or `Security:TrustedNetworks`
  (`IPNetwork.Parse` CIDR ranges); both empty clears the seeded loopback trust and sets
  `ForwardedHeaders.None`, not merely an unmodified default. Must run before rate limiting, which
  partitions on the rewritten `RemoteIpAddress`.
- **Security headers** (`Configs/SecurityHeadersConfig.cs`, package `OwaspHeaders.Core`) — writes
  the OWASP set (`X-Frame-Options`, CSP, `X-Content-Type-Options`, `Referrer-Policy`,
  `Cache-Control`, `X-Permitted-Cross-Domain-Policies`, `X-XSS-Protection`,
  `Cross-Origin-Resource-Policy`) via `Response.OnStarting`, so it survives `Response.Clear()` in
  the exception handler — 200, 404 and unhandled 500 all carry it. `/docs` gets a relaxed CSP
  (`script-src 'self' 'unsafe-inline'`) for Scalar's inline bootstrap; every other path keeps the
  strict policy. `Strict-Transport-Security` is owned by `HttpsConfig`, never duplicated here.
- **Request bounds** (`Configs/RequestBoundsConfig.cs`) — sets `KestrelServerOptions.Limits.MaxRequestBodySize`,
  `RequestHeadersTimeout`, and a default `RequestTimeoutPolicy` (→ `504` on expiry). Also sets
  `AddServerHeader = false`, unconditionally, not configurable.
- **CORS** (`Configs/CrosConfig.cs`) — plain config array, not a `FeatureManagement` flag; empty
  `AllowedOrigins` means neither `AddCors` nor `UseCors` is registered. A key present but empty
  (`[]`) for methods/headers is honored as "nothing allowed," distinct from the key being absent.
- **HTTPS/HSTS** (`Configs/HttpsConfig.cs`) — always adds both `UseHsts()` and
  `UseHttpsRedirection()`; the redirect only fires when ASP.NET Core can determine an HTTPS port,
  which the published container image does not expose by default — set `ASPNETCORE_HTTPS_PORT` if
  you need the redirect behind a TLS-terminating ingress.
- **Antiforgery** (`Configs/Antiforgery/AntiforgeryConfig.cs`) — off by default in every shipped
  file; cookie-based double-submit with `SameSite=Strict`, `Secure=Always`.
- **Rate limiting** (`Configs/RateLimits/*`) — chained `PartitionedRateLimiter` (fixed-window +
  concurrency), both `QueueLimit = 0` so an over-limit request is `429` immediately, never queued.
  Partition key: `User.Identity.Name`, else `Connection.RemoteIpAddress` (post-forwarded-headers),
  else request host (`RateLimitKeyProvider`). Both `IRateLimitKeyProvider` and
  `IRateLimitOptionsProvider` are replaceable DI seams. 429 is decided before authentication runs
  (`UseRateLimitConfig` sits before `UseAuthConfig` in the pipeline).
- **API versioning** (`Configs/VersioningConfig.cs`) — URL-segment versioning, default `1.0`,
  `AssumeDefaultVersionWhenUnspecified = true`.
- **Health checks** (`Configs/Healthz/HealthzConfig.cs`) — `/healthz` and `/` are anonymous,
  status-only (`{"status":"..."}`, no check name/duration/exception text). `/healthz/detail` is the
  full per-check report and requires authorization, but only when `AuthConfig` actually wired
  `UseAuthorization()` — with `RequireAuthorization` off, `/healthz/detail` is anonymous too.
  `HealthCheckHandler` is a template stub that always reports healthy; replace it for a real
  readiness check.
- **OpenAPI/Scalar** (`Configs/Swagger/SwaggerConfig.cs`) — document + UI at `/docs`
  (`SwaggerConfig.DocsPath`), gated on `EnableSwagger`. Outside `Development`, both require
  authorization independent of `EnableSwagger` — but again only enforceable while `AuthConfig` is
  wired. Health routes are hand-described into the OpenAPI document (`MapHealthChecks` leaves no
  `MethodInfo` for ApiExplorer to see).
- **Logging + OpenTelemetry** (`Configs/LogConfigs.cs`) — with `EnableOpenTelemetry` off (shipped
  default), only `#if DEBUG` console logging is added and standard `Logging:LogLevel` filtering
  applies. On, it clears providers, adds ASP.NET Core + HttpClient tracing/metrics, and the console
  exporter follows the **runtime environment** (`IsDevelopment()`), not the build configuration —
  a `Release` build run as `Development` still gets console traces. OTLP and Azure Monitor exporters
  are additive, each gated on its own connection value being non-blank.
- **Azure App Configuration** (`Configs/AzureAppConfig/AzureAppConfigSetup.cs`) — appended to
  `builder.Configuration` after every other source, so it outranks environment variables and
  command-line args too. Uses `DefaultAzureCredential`; a missing/blank connection string is a
  silent no-op even with the flag on.
- **Cache** (`Configs/CacheConfig.cs`) — `AddDistributedMemoryCache()` when `ConnectionStrings:Redis`
  is unset, else `AddStackExchangeRedisCache`; `AddHybridCache()` is always registered on top but no
  shipped feature consumes it yet.
- **Idempotency store** (in `AppConfig.AddAppConfig`) — Redis-backed
  (`AddIdempotencyWithRedisStore`) when `ConnectionStrings:Redis` is set, else in-memory
  (`AddIdempotentKey`), both with `ConflictHandling = IdempotentConflictHandling.ConflictResponse`
  (not `CachedResult` — verify against source if a doc says otherwise). Per-route opt-in only; see
  `dknet-appservices-actions` and `dknet-endpoint-config` for `.RequiredIdempotentKey()`.
- **JSON options** (`ServiceConfigs.AddOptions`) — naming policy, ignore conditions and converters
  come from `SharedConsts.JsonSerializerOptions`; role-aware sensitive-data filtering
  (`[SensitiveData]`) is wired onto the same `JsonOptions` instance via a factory registration. Full
  detail in `dknet-auth-and-ownership`.
- **Error responses** (`Configs/FluentValidationConfig.cs`, `AddErrorResponses`) — one registration
  answers every refusal: an error code prefixed `precondition.` (`PreconditionCodes.Prefix`) → `409`;
  an unhandled `OwnershipRequiredException` → `403`; everything else keeps its existing status. Full
  detail in `dknet-appservices-actions`.
- **Auth and ownership** — see `dknet-auth-and-ownership` for `AuthConfig`/`DemoAuthConfig`, scope
  policies, `[FromClaim]`, and `IDataOwnerProvider`/`ICurrentUserProvider`.

## Launch-time jobs

One process, one entry point. `JobSelector.Select` reads the first CLI argument that is neither an
option nor an option's value; `JobRegistry.Jobs` maps recognized names (case-insensitive) to
`Func<WebApplicationBuilder, Task<int>>`. Only `"migration"` ships, running
`InfraMigration.MigrateDb` and returning `0`/`1` — no host is built, no listener bound, no
message-bus connection opened.

```bash
dotnet run --project ApiEndpoints/Minimal.Api -- migration
```

`RunDbMigrationWhenAppStart` is the in-process alternative: same `MigrationJob.RunAsync` call, made
from `Program.cs` before serving, in every environment and build configuration. Use it for a single
replica; use the job for anything with more than one replica racing the same migration.

Adding a job is one entry in `JobRegistry.Jobs` — no new branch in `Program.cs`, no new project.
Keep a job as small as `MigrationJob`: it receives the builder as it stood right after
`AddLogConfig`, with no DI container built yet, so it constructs what it needs directly from
`builder.Configuration`/`builder.Services.BuildServiceProvider()` rather than resolving from a host.

## Aspire (`Minimal.AppHost`)

`AppHost.cs` provisions `Redis` and `Postgres` (with an `AppDb` database), starts the `Api` project
by path (`../Minimal.Api/Minimal.Api.csproj` — a literal path string, not `AddProject<T>`, so it
survives `sourceName` rewriting even for a dotted name), and calls `.WaitFor(cache).WaitFor(apDb)`.
Azure Service Bus is **not** wired — `.WaitFor(bus)` is commented out in source; only Redis and
PostgreSQL resources exist. `Minimal.AppHost/Configs/busConfig.json` (an Azure Service Bus emulator
topology file for the `product-tp`/`product-sub` topic) sits in the project but nothing in
`AppHost.cs` references it — it is not currently wired to any resource.

After resources are up, `SampleDataGenerator.RunAsync` (subscribed on
`AfterResourcesCreatedEvent`) populates `Product` and `PurchaseOrder` with
`SampleData:RecordsPerEntity` records each (default `10000`; `0` or negative skips generation
entirely). It polls up to 60s for the migration-created schema, never tops up a database that
already holds rows, and generates one fixed demonstration product
(`Demo-Product-With-Supplier-Data`) carrying both `[SensitiveData]` supplier properties so
role-gated filtering is visible on a freshly started host.

```bash
# Full stack — Redis + PostgreSQL via Docker, sample data generated
dotnet run --project ApiEndpoints/Minimal.AppHost

# API only — no containers, needs ConnectionStrings:AppDb supplied yourself
dotnet run --project ApiEndpoints/Minimal.Api
```

## Test hosts

`Minimal.App.TestSupport/TestApiFactoryBase.cs` is the shared `WebApplicationFactory<Program>` both
xUnit and BDD suites subclass. It always: `UseEnvironment("Testing")`; pushes
`FeatureManagement:RunDbMigrationWhenAppStart=false`, `EnableSwagger=false`,
`EnableAzureAppConfig=false`, `ConnectionStrings:AppDb=UseInMemory` through
`ConfigureAppConfiguration` (early enough to beat the `Program.cs` bind — see
[Start-up order](#start-up-order)); swaps `CoreDbContext` onto `AddDbContextWithHook` +
`UseInMemoryDatabase` (not plain `AddDbContext`, which would silently drop the DKNet events hook);
and substitutes `IMembershipService` with `TestMembershipService`.

Two `protected virtual` seams: `AddFeatureOverrides(IDictionary<string,string?>)` to extend the
config-override set for one suite, and `ConfigureTestServices(IServiceCollection)` (call
`base.ConfigureTestServices` first) to swap further services. Combined with the `Testing` overlay
(`RequireAuthorization=false`, `EnableDemoAuthentication=true`, `EnableHttps=false`,
`EnableRateLimit=false`), the effective test host runs with authorization off, the demo identity
authenticated on every call, no HTTPS redirect, and no rate limiting — a business test that needs a
different combination adds its own fixture subclass rather than changing these defaults.

## Common mistakes

- **What you might expect**: setting a `FeatureManagement` flag through a `WebApplicationFactory`'s
  `ConfigureAppConfiguration` in-memory collection changes behavior in a test.
  **What actually happens**: it's ignored. **Why**: `Program.cs` binds `FeatureOptions` before a
  factory's overrides are merged; only an `appsettings.{Environment}.json` file or a
  `FeatureManagement__<Flag>` environment variable lands early enough.
- **What you might expect**: `AzureAppConfiguration:*` keys in `appsettings.json` configure the
  Azure App Configuration integration. **What actually happens**: nothing — that section name binds
  to nothing; the real section is `AzureAppConfig` (no trailing "-uration").
- **What you might expect**: turning `EnableServiceBus` off disables the message bus.
  **What actually happens**: only the Azure child bus goes away; the in-memory child bus that
  carries every internal command/event/query still runs.
- **What you might expect**: a validator or `[Range]` attribute on a request is what determines
  `400` vs `201`. **What actually happens for platform wiring specifically**: FluentValidation runs
  on every endpoint group via `AddFluentValidationAutoValidation()` in `ConfigureGroup` — that part
  always works. What silently doesn't work is a DataAnnotations attribute on a **generated** CRUD
  request; see `dknet-appservices-actions` and `dknet-endpoint-config` for the enforcement gap.
