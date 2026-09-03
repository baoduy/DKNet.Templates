# Minimal.Api Configs

This directory contains modular configuration components for a MediatR-based API built with Minimal. Each module is
responsible for a specific aspect of API infrastructure, promoting maintainability and extensibility.

---

## Table of Contents

- [Minimal.Api Configs](#minimalapi-configs)
  - [Table of Contents](#table-of-contents)
  - [Overview](#overview)
  - [Configuration Modules](#configuration-modules)
    - [AppConfig.cs](#appconfigcs)
    - [ServiceConfigs.cs](#serviceconfigscs)
    - [Azure App Configuration](#azure-app-configuration)
      - [AzureAppConfiguration (`AzureAppConfiguration/`)](#azureappconfiguration-azureappconfiguration)
    - [Authentication \& Security](#authentication--security)
      - [Antiforgery (`Antiforgery/`)](#antiforgery-antiforgery)
      - [Auth (`Auth/`)](#auth-auth)
      - [Forwarded Headers (`ForwardedHeadersConfig.cs`)](#forwarded-headers-forwardedheadersconfigcs)
      - [Security Headers (`SecurityHeadersConfig.cs`)](#security-headers-securityheadersconfigcs)
      - [HTTPS \& HSTS (`HttpsConfig.cs`)](#https--hsts-httpsconfigcs)
    - [API Documentation](#api-documentation)
      - [Swagger (`Swagger/`)](#swagger-swagger)
    - [API Features](#api-features)
      - [Versioning (`VersioningConfig.cs`)](#versioning-versioningconfigcs)
      - [Endpoints (`Endpoints/`)](#endpoints-endpoints)
      - [Idempotency (`Idempotency/`)](#idempotency-idempotency)
    - [Error Handling (`GlobalExceptions/`)](#error-handling-globalexceptions)
    - [Monitoring \& Health](#monitoring--health)
      - [Healthz (`Healthz/`)](#healthz-healthz)
    - [Performance \& Reliability](#performance--reliability)
      - [CacheConfig.cs](#cacheconfigcs)
      - [Rate Limiting (`RateLimits/`)](#rate-limiting-ratelimits)
      - [Request Bounds (`RequestBoundsConfig.cs`)](#request-bounds-requestboundsconfigcs)
  - [Implementation Examples](#implementation-examples)
  - [Best Practices](#best-practices)
  - [Directory Structure](#directory-structure)
  - [Middleware Order](#middleware-order)

---

## Overview

The configuration system is organized into focused modules, each handling a distinct concern such as authentication,
versioning, documentation, error handling, and more. This modularity enables easy customization and feature toggling.

---

## Configuration Modules

### AppConfig.cs

- Central orchestrator for all configuration modules.
- Manages feature toggles via `FeatureOptions`.
- Provides extension methods for service registration (`AddAppConfig`) and middleware setup (`UseAppConfig`).
- Enables conditional features based on configuration.

### ServiceConfigs.cs

- Registers core services (HTTP context, principal provider).
- Configures options and database connections.
- Registers infrastructure, application, and service bus integrations.

### Azure App Configuration

#### AzureAppConfiguration (`AzureAppConfiguration/`)

- **Remote Configuration Management**: Centralizes application configuration in Azure App Configuration.
- **Feature Flag Support**: Loads and manages feature flags from Azure App Configuration.
- **Dynamic Configuration Refresh**: Automatically refreshes configuration values at runtime.
- **Environment-Specific Configuration**: Supports labels for different environments (dev, staging, prod).
- **Secure Connection Handling**: Uses connection strings stored in local configuration for secure access.
- **Graceful Fallback**: Falls back to local configuration if Azure App Configuration is unavailable.
- **Feature Toggle Control**: Can be enabled/disabled via `FeatureOptions.EnableAzureAppConfig`.

**Configuration Options:**

- `ConnectionString`: Azure App Configuration connection string (stored in `ConnectionStrings` section)
- `KeyPrefix`: Optional prefix to filter configuration keys
- `Label`: Optional label to filter configuration values by environment
- `CacheExpirationInSeconds`: Refresh interval for configuration values (default: 300 seconds)
- `LoadFeatureFlags`: Whether to load feature flags (default: true)
- `FeatureFlagPrefix`: Optional prefix to filter feature flags

**Usage Example:**

```json
{
  "ConnectionStrings": {
    "AzureAppConfiguration": "Endpoint=https://your-app-config.azconfig.io;Id=your-id;Secret=your-secret"
  },
  "FeatureManagement": {
    "EnableAzureAppConfig": true
  },
  "AzureAppConfiguration": {
    "KeyPrefix": "Minimal:",
    "Label": "Development",
    "CacheExpirationInSeconds": 300,
    "LoadFeatureFlags": true,
    "FeatureFlagPrefix": "Minimal"
  }
}
```

**Setup Process:**

1. Configuration is loaded early in `Program.cs` before service registration
2. Azure App Configuration values override local `appsettings.json` values
3. Feature flags are automatically integrated with .NET Feature Management
4. Configuration refresh is handled automatically in the background

### Authentication & Security

#### Antiforgery (`Antiforgery/`)

- Implements CSRF protection with configurable cookie/header names.
- Secure cookie policy (HTTP-only, SameSite).
- Middleware for token validation and rotation.

#### Auth (`Auth/`)

- JWT handling for Microsoft Graph and other providers.
- Authorization policy configuration.
- **`SampleClaimsTransformation`** – sample `IClaimsTransformation` implementation that shows how to enrich or
  normalise the user principal after authentication (e.g., add roles from a database).
- **`SampleAuthorizationRequirement` / `HasScopeHandler`** – sample `IAuthorizationRequirement` and its
  `IAuthorizationHandler` that demonstrate how to build custom authorization policies (e.g., require a specific JWT
  scope).

#### Forwarded Headers (`ForwardedHeadersConfig.cs`)

- Honours `X-Forwarded-For` / `X-Forwarded-Proto` so `Connection.RemoteIpAddress` and
  `Request.Scheme` describe the real caller instead of the ingress.
- Trusted proxies come from `Security:TrustedProxies` — **empty in the shipped base file**. Empty
  means forwarded values are ignored outright (`ForwardedHeaders.None`), because
  `ForwardedHeadersMiddleware` treats "no known proxy" as "trust any peer", which is the opposite of
  what a service wants.
- `KnownProxies` and `KnownIPNetworks` are cleared before the configured list is applied, so
  ASP.NET Core's seeded loopback entry is not silently trusted.
- Registered first in `UseAppConfig` — everything that decides on the caller's address (CORS, rate
  limiting) must run after the rewrite.
- Feature flag: `FeatureOptions.EnableForwardedHeaders` (default `true`).

#### Security Headers (`SecurityHeadersConfig.cs`)

- OWASP-recommended response headers via `OwaspHeaders.Core`: `X-Frame-Options`,
  `X-Content-Type-Options`, a default `Content-Security-Policy`,
  `X-Permitted-Cross-Domain-Policies`, `Referrer-Policy`, `Cache-Control`, `X-XSS-Protection`,
  `Cross-Origin-Resource-Policy`.
- Written from `HttpResponse.OnStarting`, registered before routing and before the global exception
  handler, so a `200`, a `404` for an unpublished path and an unhandled-`500` problem response all
  carry the headers — the exception-handler path clears the response, which would otherwise drop
  headers added the ordinary way.
- Does **not** emit `Strict-Transport-Security`: `HttpsConfig` owns that header, so it is never sent
  twice.
- Feature flag: `FeatureOptions.EnableSecurityHeaders` (default `true`).

#### HTTPS & HSTS (`HttpsConfig.cs`)

- `UseHsts()` + `UseHttpsRedirection()`, with `IncludeSubDomains` always on.
- Announced `max-age` comes from `Https:HstsMaxAgeDays` (default `365`). `Preload` is requested only
  when that value is at least 365 days — the preload list's minimum — and is otherwise off, so a
  shortened lifetime never announces preload it cannot qualify for.
- Feature flag: `FeatureOptions.EnableHttps`.

### API Documentation

#### Swagger (`Swagger/`)

- OpenAPI/Swagger setup with bearer token support.
- Custom security transformers.
- API versioning in docs.
- Scalar API reference and theming.
- Anonymous in `Development` only: outside it the OpenAPI document and `/docs` both carry
  `RequireAuthorization()` (applied when `AuthConfig` is wired, i.e. `RequireAuthorization` is on).

### API Features

#### Versioning (`VersioningConfig.cs`)

- URL segment-based versioning (e.g., `/v1/api/resource`).
- Default version fallback.
- Version reporting and OpenAPI integration.
- Deprecation support.

#### Endpoints (`Endpoints/`)

- Route group creation with version prefixing.
- Fluent validation, authorization, and tagging.
- Filter pipeline for user ID and other concerns.

#### Idempotency (`Idempotency/`)

- Header-based idempotency key validation.
- Configurable conflict handling (409 or cached response).
- Response caching and custom key storage.

### Error Handling (`GlobalExceptions/`)

- Centralized exception handling via `GlobalExceptionHandler`.
- Problem Details (RFC 7807) formatting.
- Trace ID correlation and logging.
- Standardized error responses.

### Monitoring & Health

#### Healthz (`Healthz/`)

- `CoreDbContext` connectivity check plus the custom `HealthCheckHandler`.
- `/healthz` and `/` — anonymous public probes. They evaluate every registered check but write the
  overall status only (`{"status":"Healthy"}`): no check name, duration, description or exception
  text, in any state.
- `/healthz/detail` — the full per-check report (`UIResponseWriter`), carrying
  `RequireAuthorization()` when `AuthConfig` is wired.
- Feature flag: `FeatureOptions.EnableHealthCheck`.

### Performance & Reliability

#### CacheConfig.cs

- Distributed and memory cache setup.
- Cache profile management.

#### Rate Limiting (`RateLimits/`)

- Client IP and JWT-based rate limiting.
- Configurable request limits and time windows.
- Partitions on the authenticated user, else on `Connection.RemoteIpAddress` as rewritten by the
  forwarded-headers middleware, else on the request host. It never reads `X-Forwarded-For` itself —
  an untrusted peer must not be able to claim another caller's budget.
- Automatic user identity extraction from JWT tokens.
- Feature flag controlled via `FeatureOptions.EnableRateLimit`.

#### Request Bounds (`RequestBoundsConfig.cs`)

- States the three request bounds instead of inheriting Kestrel's, from the `RequestBounds` section:
  `RequestTimeoutSeconds` (default `30`, exceeded ⇒ `504`), `MaxRequestBodySizeBytes` (default
  `1048576`, exceeded ⇒ `413`), `RequestHeadersTimeoutSeconds` (default `10`).
- Also sets `KestrelServerOptions.AddServerHeader = false` — no response names the web server.
- `UseRequestTimeouts()` is registered after `UseRouting()`, as ASP.NET Core requires.
- Feature flag: `FeatureOptions.EnableRequestBounds` (default `true`).

---

## Implementation Examples

**Custom Authorization Requirement (Scope-Based)**

```csharp
// Apply the sample scope policy to a specific endpoint or route group:
group.MapGet("/secure", handler).RequireAuthorization(HasScopeRequirement.PolicyName);

// To define your own requirement, follow the pattern in SampleAuthorizationRequirement.cs:
//   1. Create a class that implements IAuthorizationRequirement.
//   2. Create a corresponding class that extends AuthorizationHandler<TRequirement>.
//   3. Register both in AuthConfig.AddAuthConfig() (or AddAuthorization()).
```

**Claims Transformation**

```csharp
// IClaimsTransformation is automatically invoked by ASP.NET Core after each authentication event.
// To customise the principal, follow the pattern in SampleClaimsTransformation.cs:
//   1. Implement IClaimsTransformation.TransformAsync() to add/modify/normalise claims.
//   2. Inject any required services (e.g., IUserRepository) via the constructor.
//   3. Register the implementation:
services.AddScoped<IClaimsTransformation, YourClaimsTransformation>();
```

**Antiforgery Setup**

```csharp
services.AddAntiforgeryConfig(cookieName: "x-csrf-cookie", headerName: "x-csrf-header");
app.UseAntiforgeryConfig();
```

**Rate Limiting**

```csharp
// Enable in FeatureOptions
services.Configure<FeatureOptions>(options => options.EnableRateLimit = true);

// Custom configuration (optional)
services.AddRateLimitConfig(options => {
    options.DefaultRequestLimit = 5;
    options.TimeWindowInSeconds = 1;
});

// Apply to specific endpoints
app.MapPost("/api/resource", handler).RequireRateLimit();

// Apply to route groups
var apiGroup = app.MapGroup("/api").RequireRateLimit();
```

**Idempotency**

```csharp
services.AddIdempotency(options => {
    options.IdempotencyHeaderKey = "X-Idempotency-Key";
    options.ConflictHandling = IdempotentConflictHandling.ConflictResponse;
});
app.MapPost("/api/resource", handler).AddIdempotencyFilter();
```

**Global Exception Handling**

```csharp
services.AddGlobalException();
app.UseGlobalException();
services.AddProblemDetails(options => {
    options.CustomizeProblemDetails = ctx => {
        ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
        ctx.ProblemDetails.Extensions["trace-id"] = ctx.HttpContext.TraceIdentifier;
    };
});
```

**Endpoint Configuration**

```csharp
public class UserEndpoints : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/users";
    public void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetUsers);
        group.MapPost("/", CreateUser).AddIdempotencyFilter();
    }
}
```

**API Versioning**

```csharp
services.AddAppVersioning();
app.MapGroup($"/v{version}/users")
   .WithApiVersionSet(versionSet)
   .MapToApiVersion(new ApiVersion(1, 0));
```

---

## Best Practices

- **Modularity:** Isolate configuration by concern; use feature flags.
- **Security:** Enable antiforgery, CORS, secure cookies, and authorization.
- **Performance:** Use idempotency and caching; configure health checks.
- **API Design:** Version endpoints, validate input, document with OpenAPI.
- **Error Handling:** Centralize exception handling, use trace IDs, and standardize responses.

---

## Directory Structure

```
Configs/
├── AppConfig.cs
├── ServiceConfigs.cs
├── VersioningConfig.cs
├── HttpsConfig.cs
├── ForwardedHeadersConfig.cs
├── SecurityHeadersConfig.cs
├── RequestBoundsConfig.cs
├── AzureAppConfiguration/
│   ├── AzureAppConfigurationOptions.cs
│   └── AzureAppConfigurationSetup.cs
├── Antiforgery/
├── Auth/
├── Endpoints/
├── Idempotency/
├── Swagger/
└── GlobalExceptions/
```

---

## Middleware Order

`UseAppConfig()` registers the pipeline in this order — registration order is execution order, and
each stage is skipped entirely when its flag is off:

| # | Stage | Gate | Why here |
|---|---|---|---|
| 1 | Forwarded headers | `EnableForwardedHeaders` + `Security:TrustedProxies` | Must rewrite `RemoteIpAddress`/`Scheme` before anything decides on the caller |
| 2 | Security response headers | `EnableSecurityHeaders` | Ahead of routing and of the global exception handler, so `200`, `404` and unhandled `500` responses all carry the headers |
| 3 | Antiforgery | `EnableAntiforgery` | — |
| 4 | CORS | `Cors:AllowedOrigins` non-empty | Before routing, so the preflight `OPTIONS` is covered too |
| 5 | HSTS + HTTPS redirection | `EnableHttps` | — |
| 6 | Health-check endpoints | `EnableHealthCheck` | Mapped before routing so the probes answer even if endpoint registration is empty |
| 7 | `UseRouting()` | always | — |
| 8 | Request timeouts | `EnableRequestBounds` | ASP.NET Core requires `UseRequestTimeouts()` after `UseRouting()` |
| 9 | Rate limiter | `EnableRateLimit` | After routing (per-endpoint policies), before authentication — an over-limit request is `429`ed without validating a token |
| 10 | Authentication + authorization | `RequireAuthorization` | Must follow `UseRouting()`; the fallback policy makes every non-anonymous endpoint default-deny |
| 11 | Endpoints (`UseEndpointConfigs`) | always | — |
| 12 | OpenAPI + Scalar mapping | `EnableSwagger` | — |
| 13 | Global exception handler | always | Registered last but still upstream of the handler at request time — `WebApplication` appends endpoint execution at the very end of the pipeline |

The two body-size and header-timeout bounds are not middleware at all: they are Kestrel server
limits set in `AddRequestBoundsConfig`, enforced by the server before any of the above runs.

Full request-level narrative, including the endpoint filters that run inside stage 11:
`docs/api-pipeline.md` at the solution root. Every configuration key each stage reads:
`docs/configuration-reference.md`.
