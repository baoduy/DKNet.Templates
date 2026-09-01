# Configuration Reference

Every configuration key a scaffolded solution reads, what it means, what it defaults to, what it
changes, and the code path that reads it. Feature flags are the one section this page does not
restate — they have their own table in
[`template-features.md`](template-features.md#featuremanagement-flags), which is the source of
truth for class default versus each environment overlay.

Paths below use the template's own project names (`Minimal.Api`, `Minimal.Infra`, …). A generated
solution renames them to `<YourApp>.*`.

## Where configuration comes from

The API is a stock `WebApplication.CreateBuilder(args)` host, so the standard ASP.NET Core order
applies — later sources win:

1. `Minimal.Api/appsettings.json` — the base file. **This is what a deployed service runs with**,
   because the template ships no `appsettings.Production.json`.
2. `Minimal.Api/appsettings.{Environment}.json` — `Development` and `Testing` overlays ship; both
   are copied into the scaffolded solution.
3. User secrets, in `Development` only.
4. Environment variables — `Section__Key` with a double underscore, e.g.
   `FeatureManagement__RequireAuthorization=false`. These outrank every JSON file.
5. Command-line arguments.
6. Azure App Configuration, when `FeatureManagement:EnableAzureAppConfig` is on and its connection
   string resolves — added last by `Minimal.Api/Configs/AzureAppConfig/AzureAppConfigSetup.cs`, so
   it overrides the JSON files.

One exception matters for tests. `Minimal.Api/Program.cs` binds `FeatureOptions` on its first
lines, before a `WebApplicationFactory`'s `ConfigureAppConfiguration` overrides are merged. A test
fixture therefore cannot flip a feature flag with an in-memory entry — only an
`appsettings.{Environment}.json` file or a `FeatureManagement__<Flag>` environment variable lands
early enough.

## `ConnectionStrings`

| Key | Type | Shipped default | Effect | Read by |
|---|---|---|---|---|
| `AppDb` | string | `""` in the `Development` overlay; absent from the base file | The PostgreSQL connection for `CoreDbContext`. Without it the API cannot open a database connection. Supplied automatically when you launch through the Aspire host, which injects it from the `AppDb` resource. | `SharedConsts.DbConnectionString`; `Minimal.Infra/Extensions/InfraSetup.cs` (`AddInfraServices`) and `Minimal.Api/Configs/DbMigration.cs` (`RunMigrationAsync`) |
| `Redis` | string | not shipped in any file | Selects the distributed-cache backing store **and** the idempotency-key store. Set → `AddStackExchangeRedisCache` plus `AddIdempotencyWithRedisStore`. Unset → `AddDistributedMemoryCache` plus the in-process `AddIdempotentKey()` fallback, which is correct only for a single instance. Injected by the Aspire host from the `Redis` resource. | `SharedConsts.RedisConnectionString`; `Minimal.Api/Configs/CacheConfig.cs` and `Minimal.Api/Configs/AppConfig.cs` |
| `AzureBus` | string | `""` in the `Development` overlay | The Azure Service Bus namespace connection string. Non-empty **and** `FeatureManagement:EnableServiceBus` true is what adds the `AzureBus` child bus; either one missing leaves external messaging off while in-memory dispatch keeps working. | `SharedConsts.AzureBusConnectionString`; `Minimal.Infra/Extensions/ServiceBusSetup.cs` |
| `AzureAppConfig` | string | **not shipped** | The Azure App Configuration endpoint URI. `AzureAppConfigSetup` looks it up under the name in `AzureAppConfig:ConnectionStringName`, which defaults to `AzureAppConfig`. Without it the integration silently no-ops even with the flag on. | `Minimal.Api/Configs/AzureAppConfig/AzureAppConfigSetup.cs` |

> The base `appsettings.json` also ships `TEMPDb`, `AppConfig` and `AzureAppConfiguration` under
> `ConnectionStrings`. **No code reads any of the three** — see
> [Keys that ship but are never read](#keys-that-ship-but-are-never-read).

## `Authentication:Schemes:Bearer`

Bound by ASP.NET Core's own `AddJwtBearer()` configuration binding, which reads
`Authentication:Schemes:<SchemeName>`. `Minimal.Api/Configs/Auth/AuthConfig.cs` calls
`AddAuthentication().AddJwtBearer()` with no inline options, so this section is the whole
configuration surface for token validation. The block is registered only when
`FeatureManagement:RequireAuthorization` is `true`.

| Key | Type | Shipped default | Effect |
|---|---|---|---|
| `Authentication:DefaultScheme` | string | `Bearer` | The scheme used when an endpoint names none. |
| `Authentication:Schemes:Bearer:MetadataAddress` | string (URL) | `https://login.microsoftonline.com/00000000-.../v2.0/.well-known/openid-configuration` — **placeholder** | The OIDC discovery document the scheme fetches its signing keys from. Rewritten by `--TenantId`. |
| `Authentication:Schemes:Bearer:ValidAudiences` | string array | `[ "api://your-api" ]` — **placeholder** | The audiences a token may carry. Exactly one entry by design: a token minted for any other resource is rejected. Rewritten by `--ApiAudience`. |
| `Authentication:Schemes:Bearer:ValidIssuer` | string (URL) | `https://sts.windows.net/00000000-.../` — **placeholder** | The issuer a token must declare. Rewritten by `--TenantId`. |

Signature validation is never disabled — `Minimal.App.Tests/Architecture/JwtSignatureValidationTests.cs`
fails the build if the API source ever turns it off.

Two extension seams sit next to this section and are registered with it, both marked `TODO` in
source and both meant to be replaced:
`Minimal.Api/Configs/Auth/SampleClaimsTransformation.cs` (an `IClaimsTransformation`) and the
`SampleScopePolicy` policy backed by `HasScopeRequirement`/`HasScopeHandler`. Neither is applied to
any shipped route. See [`extension-points.md`](extension-points.md#authorization-and-claims).

## `Cors`

| Key | Type | Base `appsettings.json` | `appsettings.Development.json` | Effect |
|---|---|---|---|---|
| `Cors:AllowedOrigins` | string array | `[]` | `[ "http://localhost:3000", "http://localhost:5173" ]` | Deny-by-default allow-list. Empty or all-blank → neither `AddCors` nor `UseCors` is registered at all, so no `Access-Control-Allow-*` header is emitted. Non-empty → a default policy allowing exactly those origins, any header and any method. Credentials are never allowed on any path. |

Entries are absolute origins — scheme included, no trailing slash, no path. This is a plain
configuration array, not a `FeatureManagement` flag; the empty array is its off switch. Read by
`Minimal.Api/Configs/CrosConfig.cs`; behaviour pinned by
`Minimal.App.Tests/Integration/Cors/CorsPolicyTests.cs`.

## `RateLimit`

Bound to `RateLimitOptions` and applied only when `FeatureManagement:EnableRateLimit` is `true`.
The limiter is a chained `PartitionedRateLimiter`: a fixed-window limiter and a concurrency
limiter, both partitioned by the same key.

| Key | Type | Class default | Base `appsettings.json` | `appsettings.Development.json` | Effect |
|---|---|---|---|---|---|
| `RateLimit:DefaultRequestLimit` | int | `2` | `100` | `1` | `PermitLimit` on the fixed-window limiter — requests allowed per window, per partition. |
| `RateLimit:DefaultConcurrentLimit` | int | `2` | `20` | `1` | `PermitLimit` on the concurrency limiter — in-flight requests allowed at once, per partition. |
| `RateLimit:TimeWindowInSeconds` | int | `1` | `1` | `10` | The fixed window's length. |

Both limiters use `QueueLimit = 0`, so an over-limit request is rejected immediately with
`429 Too Many Requests` rather than queued. The partition key is
`User.Identity.Name`, falling back to the remote IP address, falling back to the request host
(`Minimal.Api/Configs/RateLimits/RateLimitKeyProvider.cs`) — so unauthenticated callers are
limited per IP and authenticated callers per user.

The base file must carry this section explicitly, because the class defaults are 2 requests per
second — an outage, not a rate limit.
`Minimal.App.Tests/Architecture/SecureDefaultAppSettingsTests.cs` asserts it stays there. The
shipped production numbers are a placeholder ceiling to tune, not a researched limit.

Both `IRateLimitKeyProvider` and `IRateLimitOptionsProvider` are public interfaces you can replace
— see [`extension-points.md`](extension-points.md#rate-limiting).

## `AzureAppConfig`

Bound to `AzureAppConfigOptions` and read only when `FeatureManagement:EnableAzureAppConfig` is
`true`. **The base `appsettings.json` ships no `AzureAppConfig` section at all**, so every value
below falls through to its class default.

| Key | Type | Class default | Effect | Read by |
|---|---|---|---|---|
| `AzureAppConfig:ConnectionStringName` | string | `AzureAppConfig` | Which `ConnectionStrings` entry holds the App Configuration endpoint URI. | `AzureAppConfigSetup.AddAzureAppConfig` |
| `AzureAppConfig:Label` | string | `null` → falls back to `SharedConsts.ApiName` (`Minimal.Api`) | The label filter applied to `op.Select(KeyFilter.Any, label)`, so only values labelled for this API are loaded. | `AzureAppConfigSetup.AddAzureAppConfig` |
| `AzureAppConfig:LoadFeatureFlags` | bool | `true` | **Declared but never read.** `UseFeatureFlags()` is called unconditionally. | — |
| `AzureAppConfig:FeatureFlagPrefix` | string | `""` | **Declared but never read.** No prefix filter is applied. | — |
| `AzureAppConfig:RefreshIntervalInMinutes` | int | `300` | **Declared but never read.** The refresh interval is hard-coded to 30 minutes in `ConfigureRefresh(c => c.RegisterAll().SetRefreshInterval(TimeSpan.FromMinutes(30)))`. | — |

The connection is opened with `DefaultAzureCredential`, so the host needs a managed identity or a
local Azure login with read access to the store. If the connection string is missing or blank the
method returns without adding the source — the flag alone does nothing.

The three "declared but never read" rows are an options-class-versus-setup mismatch in source, not
a documentation gap. Wiring them up is a code change; it is recorded on this documentation ticket
rather than made here.

## Telemetry

`Minimal.Api/Configs/LogConfigs.cs` runs before anything else in `Program.cs`. When
`FeatureManagement:EnableOpenTelemetry` is `false` (the shipped default) it adds a console logger
in `DEBUG` builds and returns — no tracing, no metrics, no exporter. When `true` it clears the
logging providers, adds the OpenTelemetry logger, and registers ASP.NET Core plus `HttpClient`
tracing and metrics instrumentation, with a console exporter in `DEBUG` builds only.

| Key | Type | Shipped default | Effect | Read by |
|---|---|---|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | string (URL) | `http://localhost:4317` in the base file | Non-blank → `UseOtlpExporter()` is added. The template reads the key only as a presence check; the exporter resolves its own endpoint through the OpenTelemetry SDK's standard configuration for this key. | `LogConfigs.AddLogConfig` |
| `AzureMonitor:ConnectionString` | string | `""` in the base file | Non-blank → `UseAzureMonitor()` is added, shipping traces, metrics and logs to Application Insights. Blank, as shipped, means no Azure Monitor exporter. | `LogConfigs.AddLogConfig` |
| `Logging:LogLevel:*` | string | `Default: Information`, `Microsoft: Warning`, `Microsoft.Hosting.Lifetime: Warning`; the `Development` overlay drops to `Debug`/`None`/`None` | Standard ASP.NET Core log filtering. Note that `EnableOpenTelemetry` clears the providers, so these filters then apply to the OpenTelemetry logger. | ASP.NET Core logging |

Both exporter keys are additive: set both and both exporters run.

## Framework-owned keys

| Key | Shipped default | Effect |
|---|---|---|
| `AllowedHosts` | `"*"` in the `Development` overlay only | ASP.NET Core's host-filtering middleware. Absent from the base file, where the framework default (`*`) applies. Front a deployed service with an ingress that enforces the host, or set this explicitly. |

## Keys that ship but are never read

Each of these appears in a shipped `appsettings*.json` and is bound by nothing. They are inert —
setting them changes no behaviour.

| Key | File | Why it is dead |
|---|---|---|
| `ConnectionStrings:TEMPDb` | base `appsettings.json` | A leftover name. The database connection is read from `AppDb` (`SharedConsts.DbConnectionString`). |
| `ConnectionStrings:AppConfig` | base `appsettings.json` | `AzureAppConfigOptions.ConnectionStringName` defaults to `AzureAppConfig`, not `AppConfig`. |
| `ConnectionStrings:AzureAppConfiguration` | base `appsettings.json` | Same reason — the looked-up name is `AzureAppConfig`. |
| The whole `AzureAppConfiguration` section (`KeyPrefix`, `Label`, `CacheExpirationInSeconds`, `LoadFeatureFlags`, `FeatureFlagPrefix`) | base `appsettings.json` | `AzureAppConfigOptions.Name` is `AzureAppConfig`. Nothing binds a section called `AzureAppConfiguration`, and `KeyPrefix`/`CacheExpirationInSeconds` are not properties on the options class at all. |
| `OTEL_SERVICE_NAME` | base `appsettings.json` | No template code reads it. The OpenTelemetry SDK resolves the service name from the environment variable of the same name, not from this configuration entry. |
| `ApplicationInsights:InstrumentationKey` | `appsettings.Development.json` | Azure Monitor is wired from `AzureMonitor:ConnectionString`; instrumentation keys are not read anywhere. |

Removing them is a source change to the shipped `appsettings*.json` files, out of scope for this
documentation page and recorded on the ticket instead.

## Overriding a key without editing a file

Any key above can be set as an environment variable by replacing `:` with `__`:

```bash
export FeatureManagement__RequireAuthorization=false
export ConnectionStrings__AppDb="Host=localhost;Username=postgres;Password=postgres;Database=AppDb"
export RateLimit__DefaultRequestLimit=500
export Cors__AllowedOrigins__0="https://app.example.com"
```

Environment variables outrank every JSON file, and — unlike an in-memory test override — they are
in place before `Program.cs` binds `FeatureOptions`.
