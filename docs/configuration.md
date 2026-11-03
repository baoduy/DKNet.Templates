# DKNet.Minimal.Template configuration

Every configuration key a scaffolded service reads, what it changes and when the change takes effect.
The `FeatureManagement` flags are the one group this page does not list: they have their own table in
[Template features](template-features.md#featuremanagement-flags), which is the source of truth for
the class default and each environment overlay. Neither sample feature reads a key of its own, so
this page is the whole configuration surface.

Project names below are the template's own (`Minimal.Api`, `Minimal.Infra`, …). A generated solution
renames them to `<YourApp>.*`.

## Where configuration comes from

The API is a stock `WebApplication.CreateBuilder(args)` host, so the standard ASP.NET Core order
applies — later sources win:

1. `Minimal.Api/appsettings.json` — the base file. **This is what a deployed service runs with**,
   because the template ships no `appsettings.Production.json`.
2. `Minimal.Api/appsettings.{Environment}.json` — `Development` and `Testing` overlays ship; both
   are copied into the scaffolded solution.
3. User secrets, in `Development` only.
4. Environment variables. A key maps to a variable by replacing every `:` with `__`
   (`FeatureManagement:RequireAuthorization` → `FeatureManagement__RequireAuthorization`). These
   outrank every JSON file.
5. Command-line arguments.
6. Azure App Configuration, when `FeatureManagement:EnableAzureAppConfig` is on and its connection
   string resolves. `AzureAppConfigSetup` appends it to `builder.Configuration` after
   `WebApplication.CreateBuilder(args)` has already added every source above, so it wins over the
   JSON files **and** over environment variables and command-line arguments.

One exception matters for tests. `Program.cs` binds `FeatureOptions` on its first lines, before a
`WebApplicationFactory`'s `ConfigureAppConfiguration` overrides are merged. A test fixture therefore
cannot flip a feature flag with an in-memory entry — only an `appsettings.{Environment}.json` file or
a `FeatureManagement__<Flag>` environment variable lands early enough.

Every key on this page is read while the host starts: change it, then restart the service. The
**Takes effect** column says `startup` for that reason.

## ⚙️ Configuration reference

### `ConnectionStrings`

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `ConnectionStrings:AppDb` | string | yes | `""` in the base file and the `Development` overlay | an Npgsql connection string | yes — your secret store (Aspire injects it on local runs) | startup | The PostgreSQL connection for `CoreDbContext`. Read by `InfraSetup.AddInfraServices` and by the start-up migration in `DbMigration`. Left empty with `FeatureManagement:RunDbMigrationWhenAppStart` on (the `Development` overlay), the start-up migration fails with `The ConnectionString property has not been initialized` and the host exits; with the flag off (the base file) the host starts and the first database call fails. Supplied automatically when you launch through the Aspire host, which injects it from the `AppDb` resource. |
| `ConnectionStrings:Redis` | string | no | not shipped in any file | a StackExchange.Redis connection string | yes — your secret store (Aspire injects it on local runs) | startup | Selects the distributed-cache store **and** the idempotency-key store. Set → `AddStackExchangeRedisCache` plus `AddIdempotencyWithRedisStore`. Unset → `AddDistributedMemoryCache` plus the in-process `AddIdempotentKey()` fallback, which is correct only for a single instance. Read by `CacheConfig` and `AppConfig`. |
| `ConnectionStrings:AzureBus` | string | no | `""` in the `Development` overlay; absent from the base file | an Azure Service Bus namespace connection string | yes — your secret store | startup | Non-empty **and** `FeatureManagement:EnableServiceBus` true is what adds the `AzureBus` child bus in `ServiceBusSetup.AddServiceBus`; either one missing leaves external messaging off while in-memory dispatch keeps working. |
| `ConnectionStrings:AzureAppConfig` | string (URI) | no | `""` in the base file | the App Configuration endpoint URI | no — an endpoint, not a credential: access goes through `DefaultAzureCredential` | startup | Looked up under the name in `AzureAppConfiguration:ConnectionStringName`. Without it the integration silently no-ops even with the flag on. |

### `Authentication`

Bound by ASP.NET Core's own `AddJwtBearer()` configuration binding, which reads
`Authentication:Schemes:<SchemeName>`. `AuthConfig` calls `AddAuthentication().AddJwtBearer()` with no
inline options, so this section is the whole configuration surface for token validation. The block is
registered only when `FeatureManagement:RequireAuthorization` is `true`.

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `Authentication:DefaultScheme` | string | yes, with `RequireAuthorization` on | `Bearer` | a registered scheme name | no | startup | The scheme used when an endpoint names none. |
| `Authentication:Schemes:Bearer:MetadataAddress` | string (URL) | yes, with `RequireAuthorization` on | `https://login.microsoftonline.com/00000000-…/v2.0/.well-known/openid-configuration` — **placeholder** | an OIDC discovery document URL | no | startup | Where the scheme fetches its signing keys. Rewritten by the `--TenantId` scaffold parameter. |
| `Authentication:Schemes:Bearer:ValidAudiences` | string array | yes, with `RequireAuthorization` on | `[ "api://your-api" ]` — **placeholder** | exactly one entry by design | no | startup | The audiences a token may carry; a token minted for any other resource is rejected. Rewritten by `--ApiAudience`. |
| `Authentication:Schemes:Bearer:ValidIssuer` | string (URL) | yes, with `RequireAuthorization` on | `https://sts.windows.net/00000000-…/` — **placeholder** | the issuer your tenant signs with | no | startup | The issuer a token must declare. Rewritten by `--TenantId`. |

Signature validation is never disabled: `JwtSignatureValidationTests` in
`tests/DKNet.Templates.ScaffoldTests/` fails the build if the API source ever turns it off.

Two extension seams sit next to this section and are registered with it, both marked `TODO` in source
and both meant to be replaced: `SampleClaimsTransformation` (an `IClaimsTransformation`) and the
`SampleScopePolicy` policy backed by `HasScopeRequirement`/`HasScopeHandler`. Neither is applied to any
shipped route. See [Extension points](extension-points.md#authorization-and-claims).

#### Built-in demonstration authentication provider

`FeatureManagement:EnableDemoAuthentication` (see its row in
[Template features](template-features.md#featuremanagement-flags)) registers a `"Demo"` authentication
scheme, `DemoAuthenticationHandler` in `DemoAuthConfig`, only while `RequireAuthorization` is `false`.
Every caller is authenticated as one fixed, self-evidently fake identity: `ClaimTypes.Name` =
`SharedConsts.DemoAccount` (`"demo-user@not-a-real-identity.invalid"`, on the `.invalid` TLD reserved by
RFC 2606 so it can never resolve) and `ClaimTypes.NameIdentifier` = `SharedConsts.SystemAccount`.
`RequireAuthorization` and `EnableDemoAuthentication` are mutually exclusive: both `true` throws
`InvalidOperationException` from `AppConfig.AddAppConfig` at start-up. **Development and demonstration
only — never enable this flag in a deployed service.**

### `Cors`

Read by `CorsConfig` in the `DKNet.AspCore.Extensions` package (`DKNet.AspCore.Extensions.Hosting`).

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `Cors:AllowedOrigins` | string array | no | `[]`; the `Development` overlay ships `[ "http://localhost:3000", "http://localhost:5173" ]` | absolute origins: scheme included, no trailing slash, no path | no | startup | Deny-by-default allow-list. Empty or all-blank → neither `AddCors` nor `UseCors` is registered, so no `Access-Control-Allow-*` header is emitted. Non-empty → a default policy allowing exactly those origins, the methods and headers below, and nothing else. Credentials are never allowed on any path. |
| `Cors:AllowedMethods` | string array | no | `[ "GET", "POST", "PUT", "PATCH" ]` (base file and package default) | HTTP method names | no | startup | The methods reflected in `Access-Control-Allow-Methods`. `DELETE` is deliberately absent: add it if a browser front-end needs it. An entry not listed is never reflected, so a preflight for it fails. |
| `Cors:AllowedHeaders` | string array | no | `[ "Authorization", "Content-Type", "Accept", "X-Idempotency-Key" ]` | header names | no | startup | The request headers reflected in `Access-Control-Allow-Headers`. `X-Idempotency-Key` is there because the purchase-order create route requires it. No tracing header (`traceparent`, `X-Request-Id`, …) is listed — add yours if your front-end sends one. |

This is a plain configuration array, not a `FeatureManagement` flag; the empty array is its off switch,
and it gates the other two keys as well — with no origin listed, CORS is not wired and the method and
header lists are never consulted. `AllowedMethods` and `AllowedHeaders` fall back to the lists above
only when the key is **absent**. A key present but empty (`[]`) is honoured as "nothing allowed"
rather than widened back to the default, so a preflight for any method (or header) then fails.

### `Security`

Read by `ForwardedHeadersConfig` in `DKNet.AspCore.Extensions`, and only when
`FeatureManagement:EnableForwardedHeaders` is `true`.

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `Security:TrustedProxies` | string array | behind a proxy, this or `TrustedNetworks` | `[]` | each entry a single literal IPv4 or IPv6 address (`IPAddress.Parse`); a CIDR range fails start-up with a `FormatException` | no | startup | The proxies whose `X-Forwarded-For` / `X-Forwarded-Proto` the service believes, by exact address. Non-empty → `XForwardedFor | XForwardedProto` are applied, but only when the immediate peer is one of the listed addresses. |
| `Security:TrustedNetworks` | string array | behind a proxy, this or `TrustedProxies` | `[]` | each entry a CIDR range (`IPNetwork.Parse`); a malformed entry fails start-up | no | startup | The same trust expressed as a range, for a proxy whose address is not fixed (a Kubernetes ingress, Container Apps). Either key alone enables forwarded headers; both together are both honoured. |

**Empty — as shipped — means no forwarded information is honoured at all**: with both lists empty the
module sets `ForwardedHeaders.None`, so `Connection.RemoteIpAddress` stays the immediate peer and rate
limiting partitions on it. This is the one pair of keys a production host behind an ingress, load
balancer or CDN **must** supply — until one of them does, every request appears to come from that
ingress and shares one rate-limit partition. A proxy with a fixed address goes in `TrustedProxies`, one
entry per proxy:

```json
"Security": {
  "TrustedProxies": [ "10.0.0.4", "10.0.0.5" ],
  "TrustedNetworks": []
}
```

A proxy whose address moves goes in `TrustedNetworks` as a CIDR range instead:

```json
"Security": {
  "TrustedProxies": [],
  "TrustedNetworks": [ "10.244.0.0/16" ]
}
```

`KnownProxies` and `KnownIPNetworks` are cleared before either list is applied, so ASP.NET Core's
seeded loopback entry is gone too: `127.0.0.1` is trusted only if you list it. Turning the flag off and
leaving both lists empty are equivalent in effect; the flag exists so the middleware can be taken out of
the pipeline entirely for local work.

### `Https`

Read by `HttpsConfig` in `DKNet.AspCore.Extensions`, and only when `FeatureManagement:EnableHttps` is
`true` (the base file's value; `false` in the `Development` and `Testing` overlays).

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `Https:HstsMaxAgeDays` | int | no | `365` (package default and base file) | days | no | startup | The `max-age` announced by `Strict-Transport-Security`. `Preload` is requested **only** when this value is at least 365 — the preload list's minimum — and is otherwise switched off, so a shortened max-age never announces preload it cannot qualify for. `IncludeSubDomains` is always on. |

Lowering it is the safe way to try HSTS on a domain you are not ready to commit for a year —
`"HstsMaxAgeDays": 1` announces one day and no preload. The header has exactly one owner: the
security-headers middleware deliberately does not emit `Strict-Transport-Security`, so it is never sent
twice.

### `RequestBounds`

Bound to `RequestBoundsOptions` by `RequestBoundsConfig` in `DKNet.AspCore.Extensions`, and applied
only when `FeatureManagement:EnableRequestBounds` is `true` (default `true`; `false` in the
`Development` overlay). The template states all three bounds rather than inheriting Kestrel's, so a
generated service is bounded with no configuration supplied.

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `RequestBounds:RequestTimeoutSeconds` | int | no | `30` (class default and base file) | seconds | no | startup | The default request-timeout policy's lifetime. A request still running when it elapses is answered `504 Gateway Timeout`. Raise it for a long-running endpoint, or opt that endpoint out with `.DisableRequestTimeout()`. Replaces: none — ASP.NET Core caps request lifetime only if you ask it to. |
| `RequestBounds:MaxRequestBodySizeBytes` | long | no | `1048576` (1 MB) | bytes; `null` is not expressible, so there is no "unlimited" value through configuration | no | startup | `KestrelServerOptions.Limits.MaxRequestBodySize`. A larger body is rejected with `413 Payload Too Large`. Raise it for file upload. Replaces Kestrel's ~30 MB. |
| `RequestBounds:RequestHeadersTimeoutSeconds` | int | no | `10` | seconds | no | startup | How long Kestrel waits for the complete request headers before dropping the connection — the slow-headers bound. Replaces Kestrel's 30 s. |

Set only the keys you want to change; a partially specified section keeps the class default for the
rest. The same module also sets `KestrelServerOptions.AddServerHeader = false`, which is **not**
configurable: no response names the web-server product. `UseRequestTimeouts()` is registered after
`UseRouting()`, as ASP.NET Core requires, so the timeout applies to endpoint execution — a request that
is both oversized and slow is rejected on size first, at the server, before the timeout policy is
reached.

### `RateLimit`

Bound to `RateLimitOptions` by `RateLimitConfig` in `DKNet.AspCore.Extensions`, and applied only when
`FeatureManagement:EnableRateLimit` is `true`. The limiter is a chained `PartitionedRateLimiter`: a
fixed-window limiter and a concurrency limiter, both partitioned by the same key.

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `RateLimit:DefaultRequestLimit` | int | no | base file `100`; `Development` overlay `1`; class default `2` | requests per window, per partition | no | startup | `PermitLimit` on the fixed-window limiter. |
| `RateLimit:DefaultConcurrentLimit` | int | no | base file `20`; `Development` overlay `1`; class default `2` | in-flight requests, per partition | no | startup | `PermitLimit` on the concurrency limiter. |
| `RateLimit:TimeWindowInSeconds` | int | no | base file `1`; `Development` overlay `10`; class default `1` | seconds | no | startup | The fixed window's length. |

Both limiters use `QueueLimit = 0`, so an over-limit request is rejected immediately with
`429 Too Many Requests` rather than queued. The partition key is the signed-in user's name, else the
remote IP address, else one shared bucket — so unauthenticated callers are limited per IP and
authenticated callers per user. That remote IP is `Connection.RemoteIpAddress` *after* the
forwarded-headers middleware has had its say, which is why
[`Security:TrustedProxies` / `Security:TrustedNetworks`](#security) matter to rate limiting: list your
ingress and each client behind it gets its own budget; leave both empty and they all share the
ingress's. The key never comes from `X-Forwarded-For` or `Host`, so a peer that is not a trusted proxy
cannot claim another client's identity and spend its budget.

The base file must carry this section explicitly, because the class defaults are 2 requests per second
— an outage, not a rate limit. `SecureDefaultAppSettingsTests` in `tests/DKNet.Templates.ScaffoldTests/`
asserts it stays there. The shipped production numbers are a placeholder ceiling to tune, not a
researched limit. The per-caller limits are replaceable: register your own `IRateLimitOptionsProvider`
— see [Extension points](extension-points.md#rate-limiting).

### `AzureAppConfiguration`

Bound to `AzureAppConfigOptions` and read only when `FeatureManagement:EnableAzureAppConfig` is `true`.
The connection is opened with `DefaultAzureCredential`, so the host needs a managed identity or a local
Azure login with read access to the store.

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `AzureAppConfiguration:ConnectionStringName` | string | no | `AzureAppConfig` (class default; not in the base file) | a `ConnectionStrings` entry name | no | startup | Which `ConnectionStrings` entry holds the App Configuration endpoint URI. |
| `AzureAppConfiguration:Label` | string | no | `""` in the base file; blank falls back to `SharedConsts.ApiName` (`Minimal.Api`) | — | no | startup | The label filter applied with `Select(KeyFilter.Any, label)`, so only values labelled for this API are loaded. |
| `AzureAppConfiguration:CacheExpirationInSeconds` | int | no | `300` | seconds; zero or negative falls back to `300` | no | startup | The refresh interval registered for every loaded key (`ConfigureRefresh(...).SetRefreshInterval(...)`). |
| `AzureAppConfiguration:LoadFeatureFlags` | bool | no | `true` | — | no | startup | `true` → feature flags are loaded from the store; `false` → every `.appconfig.featureflag/` setting is dropped, including flags the label selector fetched. |
| `AzureAppConfiguration:FeatureFlagPrefix` | string | no | `""` | matched with an ordinal prefix test; `\`, `*` and `,` are escaped for the store filter | no | startup | Non-blank → only feature flags whose name starts with it are loaded. Blank → all flags. |

### Telemetry

`LogConfigs.AddLogConfig` runs before anything else in `Program.cs`. When
`FeatureManagement:EnableOpenTelemetry` is `false` (the shipped default) it returns without registering
any telemetry — no tracing, no metrics, no exporter — and the service keeps logging through the ASP.NET
Core pipeline under `Logging:LogLevel:*`. When `true` it clears the logging providers, adds the
OpenTelemetry logger, and registers ASP.NET Core plus `HttpClient` tracing and metrics instrumentation.

The console exporter follows the **environment the service runs in**, not the configuration it was
built in. With telemetry enabled and `ASPNETCORE_ENVIRONMENT=Development`, traces and metrics are
written to the console from a `Release` build exactly as from a `Debug` one; under `Production` neither
build writes any to the console, so a deployed service exports only to the OTLP or Azure Monitor
destinations below. Application logging is not affected either way.

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | string (URL) | no | `http://localhost:4317` in the base file | — | no | startup | Non-blank → `UseOtlpExporter()` is added. The template reads the key only as a presence check; the exporter resolves its own endpoint through the OpenTelemetry SDK's standard configuration for this key. |
| `AzureMonitor:ConnectionString` | string | no | `""` in the base file | an Application Insights connection string | yes — your secret store | startup | Non-blank → `UseAzureMonitor()` is added, shipping traces, metrics and logs to Application Insights. Blank, as shipped, means no Azure Monitor exporter. |
| `Logging:LogLevel:*` | string | no | `Default: Information`, `Microsoft: Warning`, `Microsoft.Hosting.Lifetime: Warning`; the `Development` overlay drops to `Debug`/`None`/`None` | a `LogLevel` name | no | startup | Standard ASP.NET Core log filtering. `EnableOpenTelemetry` clears the providers, so these filters then apply to the OpenTelemetry logger. |

Both exporter keys are additive: set both and both exporters run.

### `SampleData`

The one key on this page that the API never reads. It lives in `Minimal.AppHost/appsettings.json` — the
Aspire application host's own configuration — and is read by `Minimal.AppHost/AppHost.cs`, which passes
it to `SampleDataGenerator`. The application host orchestrates a local development environment and is
not part of what gets published, so this key has no effect on, and no presence in, a deployed service.

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `SampleData:RecordsPerEntity` | int | no | `10000` | `0` or negative turns generation off | no | startup of the Aspire host | How many records to generate per sample entity (`Product` and `PurchaseOrder`) once the application host has started its resources. `0` or less skips generation without opening a database connection and leaves the database exactly as the migration left it — that is how you get an empty database to test empty-state behaviour; the volume *is* the switch. |

Generation writes into the schema the API's start-up migration creates, so with
[`FeatureManagement:RunDbMigrationWhenAppStart`](template-features.md#featuremanagement-flags) off there
is no schema to write into: generation polls for up to 60 seconds waiting for it — the host process
itself starts immediately, only sample-data generation waits — then gives up, and a warning in the
host's logs names that flag, alongside the last poll failure observed, as the likely reason. Generation
never accumulates into a database that already holds sample rows: products are skipped if the table
holds any row, purchase orders if it holds more than the three the migration seeds, so a database you
chose to keep is skipped rather than topped up and no row you created by hand is ever deleted or
modified. What generation produces: [Template features](template-features.md#aspire-topology).

### Framework-owned keys

| Key | Type | Required | Default | Rules | Secret | Takes effect | Effect |
|---|---|---|---|---|---|---|---|
| `AllowedHosts` | string | no | `"*"` in the `Development` overlay only; absent from the base file, where the framework default (`*`) applies | `;`-separated host names | no | startup | ASP.NET Core's host-filtering middleware. Front a deployed service with an ingress that enforces the host, or set this explicitly. |

### Keys that ship but are never read

Each of these appears in a shipped `appsettings*.json` and is bound by nothing. Setting them changes no
behaviour.

| Key | File | Why it is dead |
|---|---|---|
| `OTEL_SERVICE_NAME` | base `appsettings.json` | No template code reads it. The OpenTelemetry SDK resolves the service name from the environment variable of the same name, not from this configuration entry. |
| `ApplicationInsights:InstrumentationKey` | `appsettings.Development.json` | Azure Monitor is wired from `AzureMonitor:ConnectionString`; instrumentation keys are not read anywhere. |

## Overriding a key without editing a file

Any key above can be set as an environment variable by replacing `:` with `__`:

```bash
export FeatureManagement__RequireAuthorization=false
export ConnectionStrings__AppDb="Host=localhost;Username=postgres;Password=<password>;Database=AppDb"
export RateLimit__DefaultRequestLimit=500
export Cors__AllowedOrigins__0="https://app.example.com"
```

Environment variables outrank every JSON file and — unlike an in-memory test override — are in place
before `Program.cs` binds `FeatureOptions`. The one source that outranks them is Azure App
Configuration, which is appended after the host builder has already read the environment.
