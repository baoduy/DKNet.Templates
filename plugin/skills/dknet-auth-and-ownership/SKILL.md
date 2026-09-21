---
name: dknet-auth-and-ownership
description: Explains authentication, per-route authorization scopes, acting-user attribution, row-level data ownership, and role-aware sensitive-data filtering in this template — including the demo authentication provider and how to write a test that runs with RequireAuthorization on. Use whenever adding a scope-guarded route, wiring acting-user attribution for a new feature, reasoning about data isolation between callers, or writing an auth-on integration test.
---

# DKNet authentication, authorization, and ownership

## `FeatureManagement:RequireAuthorization`

When `true`, `AddAppConfig` calls `AddAuthConfig` (`<YourApp>.Api/Configs/Auth/AuthConfig.cs`):

```csharp
services.AddAuthentication().AddJwtBearer();

services.AddAuthorization(options =>
{
    // Default deny: any endpoint not explicitly declared anonymous requires an authenticated caller.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(HasScopeRequirement.PolicyName,
        policy => policy.Requirements.Add(new HasScopeRequirement("sample-scope")));

    foreach (var scope in ProductScopes.All)
        options.AddPolicy(scope, policy => policy.Requirements.Add(new HasScopeRequirement(scope)));
});

services.AddScoped<IClaimsTransformation, SampleClaimsTransformation>();
services.AddScoped<IAuthorizationHandler, HasScopeHandler>();
```

JWT bearer validation reads its metadata address from `Authentication:Schemes:Bearer:MetadataAddress`
(plus `ValidAudiences`/`ValidIssuer`). `FallbackPolicy` is default-deny: any route without an
explicit `[AllowAnonymous]` requires an authenticated caller, even one with no scope requirement
attached. `HasScopeRequirement`/`HasScopeHandler` check the `scp` or `scope` claim (space-separated,
both spellings checked for provider portability). `SampleClaimsTransformation` is a `TODO`-marked
seam for enriching the principal after authentication — replace it, don't add a parallel one.

When `RequireAuthorization` is `false`, **no authentication or authorization middleware is
registered at all**, unless `EnableDemoAuthentication` is separately on (below). There is no
implicit fallback identity.

`appsettings.json` ships `RequireAuthorization: true`. Both `appsettings.Development.json` and
`appsettings.Testing.json` override it to `false` and turn `EnableDemoAuthentication` on instead —
local development and the test hosts do not exercise real JWT validation by default.

## Scopes → policies

`<YourApp>.Api/ApiEndpoints/AutomatedSample/ProductScopes.cs` defines the scope constants for the
`Product` feature:

```csharp
internal static class ProductScopes
{
    public const string Read = "products.read";
    public const string Write = "products.write";
    public const string Supplier = "products.supplier";
    public const string Discontinue = "products.discontinue";
    public static readonly string[] All = [Read, Write, Supplier, Discontinue];
}
```

`AuthConfig`'s `foreach (var scope in ProductScopes.All)` loop registers **one authorization policy
per scope, the scope value doubling as its own policy name** — so a route names
`ProductScopes.Read` directly, with no separate policy name to remember.

### Attach the scope with `[EndpointGroupScope]` first

Prefer declaring scopes **above the `IEndpointConfig` class**, not route by route inside `Map`:

```csharp
[EndpointGroupScope(ProductScopes.Read, EndpointHttpMethods.Get)]
[EndpointGroupScope(ProductScopes.Write, EndpointHttpMethods.Post, EndpointHttpMethods.Put,
    EndpointHttpMethods.Delete)]
internal sealed class ProductV1Endpoint : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/products";

    public void Map(RouteGroupBuilder group) => group.MapProductCrud();
}
```

(`EndpointGroupScopeAttribute`/`EndpointHttpMethods` live in
`DKNet.AspCore.Extensions.Endpoints`.)

Rules, in the order they bite:

- The attribute is `AllowMultiple` — stack one declaration per scope, each naming the HTTP methods
  that need it.
- A declaration naming **no** HTTP method is the group's default for every method it serves. A
  declaration that names a method wins over that default for its own method.
- It is applied **only when `EndpointRegistrationOptions.RequireAuthorization` is `true`** — the
  same value `Program.cs` assigns from `FeatureOptions.RequireAuthorization`. That is the reason to
  prefer it: the flag check is built in, so there is no `if (!requireAuthorization) return;` branch
  to write and no way to forget one.
- A route that already names its own policy, or allows anonymous access, is left alone — which is
  what makes the per-route fallbacks below safe to mix in.
- Once **any** declaration exists on a group, an endpoint serving a method with neither its own
  declaration nor a group default is **refused when the group's endpoints are built**. Cover every
  method the group serves, or declare a default.
- Requires `DKNet.AspCore.Extensions` **13.0.0 or newer**. The shipped `ProductV1Endpoint` is
  declared exactly this way.

### Fallback, in order

Drop one rung only when the rung above cannot express the rule:

1. **`[EndpointGroupScope]`** — the scope is a function of the HTTP method. Covers a whole
   generated CRUD slice in two lines.
2. **`o.Configure(CrudOp.X, …)` / `o.Configure("RouteName", …)`** — two routes sharing one HTTP
   method need *different* scopes, so no per-method declaration can separate them.
   `o.Configure(CrudOp, …)` targets a generated composite route by operation kind (see
   `dknet-endpoint`); `o.Configure("RouteName", …)` targets one `[CrudAction]` route by its C#
   member name. `Product` needs this: `Update` and `AssignSupplierReference` are both `PUT`, and
   the second must demand `products.supplier` rather than `products.write`.
3. **`.RequireAuthorization(scope)` on the `RouteHandlerBuilder`** — a hand-mapped route, or a
   generated route replaced by a hand-mapped one (`discontinue` below).

Rungs 2 and 3 do **not** self-gate. Calling `RequireAuthorization(scope)` with the flag off throws
at request time, because `AddAuthConfig` never ran and no policy of that name was registered — so
each must sit behind a flag check. `ProductV1Endpoint` reads it once via DI, and needs it for
exactly the two `PUT` routes the group declarations cannot separate:

```csharp
// Only for the per-route overrides below — the class-level declarations gate themselves.
var requireAuthorization = ((IEndpointRouteBuilder)group).ServiceProvider
    .GetRequiredService<IOptions<FeatureOptions>>().Value.RequireAuthorization;

group.MapProductCrud(o =>
{
    o.Exclude("Discontinue");

    // A PUT like Update, so no per-method declaration separates them, and products.write must not
    // be enough to assign a supplier reference.
    if (requireAuthorization)
        o.Configure("AssignSupplierReference", rb => rb.RequireAuthorization(ProductScopes.Supplier));
});

var discontinue = group.MapPut("{id:guid}/discontinue", /* ... */);
if (requireAuthorization) discontinue.RequireAuthorization(ProductScopes.Discontinue);

// GET summary needs no call at all — the group's products.read declaration covers it.
group.MapGet("summary", /* ... */);
```

`GetById`, `GetList`, `Create`, `Update`, `Delete` and `Approve` carry no scope call: their HTTP
method decides their scope, and the two class-level declarations already say what it is.

`IEndpointConfig` also exposes an optional `string? AuthPolicy` member for gating an entire group
under one policy (`null` means plain authentication) rather than per-method scopes — neither shipped
sample overrides it, because different methods in the same group need different scopes and
`[EndpointGroupScope]` expresses that without giving up the group-level declaration.

**Recipe: add a new scope for a new feature.**
1. Add a constant (and to an `All` array, if you loop like `ProductScopes` does) in a
   `<Feature>Scopes` static class next to the feature's endpoint config.
2. Register it as a policy — either loop over your `All` array in `AuthConfig` the way
   `ProductScopes.All` is registered, or call `options.AddPolicy(YourScopes.X, ...)` explicitly for a
   one-off scope.
3. Attach it with `[EndpointGroupScope(YourScopes.X, EndpointHttpMethods.…)]` on the endpoint class.
   Only where a single HTTP method needs two different scopes, fall back to `o.Configure(...)` or
   `.RequireAuthorization(...)` — and then gate that call behind
   `FeatureOptions.RequireAuthorization`, never call it unconditionally.

## Demo authentication

`FeatureManagement:EnableDemoAuthentication` registers `DemoAuthenticationHandler`
(`<YourApp>.Api/Configs/Auth/DemoAuthConfig.cs`) as the default authenticate/challenge scheme. Every
request is authenticated, unconditionally, as a fixed fake identity:

```csharp
var identity = new ClaimsIdentity(
    [
        new Claim(ClaimTypes.Name, SharedConsts.DemoAccount),
        new Claim(ClaimTypes.NameIdentifier, SharedConsts.SystemAccount)
    ],
    SchemeName);
```

`AddAppConfig` throws at start-up if both `RequireAuthorization` and `EnableDemoAuthentication` are
`true` — the demonstration identity is never a real caller. Demo authentication exists to give
local/demo runs a real authenticated principal, so acting-user attribution and ownership stamping
still work, without standing up a real identity provider. It never gates access — no authorization
services or policies are registered alongside it.

## Acting user — three surfaces

**Manual: `[FromClaim]`.** `CreatePurchaseOrderRequest.ByUser` is populated by
`AddContextualRequestPopulation` (wired in `Program.cs`) before FluentValidation runs and before the
handler is invoked:

```csharp
[FromClaim(ClaimTypes.Name)]
public string? ByUser { get; set; }
```

There is no fallback: if the caller has no `ClaimTypes.Name` claim, `ByUser` stays `null`/empty, and
the handler must reject it explicitly —

```csharp
if (string.IsNullOrEmpty(request.ByUser))
    return Result.Fail<PurchaseOrderDto>("The caller is not authenticated.");
```

— from `CreatePurchaseOrderCommandHandler`. A payload value for `ByUser` is always overwritten, never
trusted; this is a security seam, not a binding convenience.

`[FromRequestHeader("X-Header-Name")]` is the same populator reading a named request header instead
of a claim: also overwritten before validation and the handler (with the property's default when the
header is absent), also impossible to forge through the payload, and also inert unless
`AddContextualRequestPopulation` is registered — it is. A missing header is never a refusal, just a
default. **It is not an authorization signal**: a header is caller-supplied and carries no identity
guarantee, so never use it where `[FromClaim]` belongs. Neither shipped sample uses it; reach for it
for correlation ids, a tenant hint, or a client-version marker the handler wants without adding a
body field.

**Automated: `PrincipalProvider` + two save hooks.** A `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`
generated request forwards only `System.ComponentModel.DataAnnotations` attributes, so it can never
carry `[FromClaim]`. Acting-user attribution instead goes through `PrincipalProvider`
(`<YourApp>.Api/Configs/Handlers/PrincipalProvider.cs`), which implements `IPrincipalProvider`
(`IDataOwnerProvider` + `ICurrentUserProvider` plus `ProfileId`/`Email`/`UserName`):

```csharp
string[] subjectClaimTypes =
[
    "http://schemas.microsoft.com/identity/claims/objectidentifier", "oid", ClaimTypes.NameIdentifier, "sub"
];
// first non-empty of these wins
```

Unauthenticated callers resolve to `SharedConsts.SystemAccount`. `ServiceConfigs.AddAllAppServices`
wires it twice, deliberately as two separate hooks over the same value today:

```csharp
.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()   // stamps OwnedBy on insert, row-isolation key
.AddCurrentUserProvider<CoreDbContext, PrincipalProvider>() // stamps CreatedBy/UpdatedBy from the same subject
```

`GetOwnershipKey()` and `GetCurrentUser()` return the same value today — kept as two methods (not
collapsed into one) because the two hooks are wired independently and are free to diverge later if
ownership and audit attribution ever need different claims.

A domain method can still override the audit stamp explicitly — `Product.Approve(string byUser) =>
SetUpdatedBy(byUser)` calls the base `SetUpdatedBy` directly, so `UpdatedBy` reflects the payload's
`byUser` argument rather than the caller's own principal for that one action.

**Table: manual vs. automated acting-user attribution**

| | Manual (`PurchaseOrder`) | Automated (`Product`) |
|---|---|---|
| Carried on | `[FromClaim]` request property | Not on the request at all |
| Populated by | `AddContextualRequestPopulation`, pre-validation | `PrincipalProvider`, at `SaveChanges` |
| Empty/missing case | Handler must check and reject | `CreatedBy`/`UpdatedBy` stamped `SystemAccount`, or the write is refused (see ownership below) |
| Override per-action | Pass a different value in the payload (validator's job to police that) | A domain method can call `SetUpdatedBy(explicitUser)` directly, as `Approve` does |

**`[CrudAction]` pitfall.** A generated action method's parameter *name* becomes a bindable request
property, regardless of what it's meant to represent. `Product.Approve(string byUser) =>
SetUpdatedBy(byUser)` generates `ApproveProductRequest` with a caller-settable, `required string
ByUser` property — any caller can claim to be approving as anyone. This is by design for `Approve`
(the BDD suite calls it "approve as X"), but it means **never name a `[CrudAction]` parameter
`byUser`/similar expecting it to be silently populated from the caller's identity** — a generated
request has no `[FromClaim]` seam at all.

## Row-level isolation

`Product : AggregateRoot, IOwnedBy` carries an `OwnedBy` property. `CoreDbContext : IDataOwnerDbContext`
exposes:

```csharp
public IEnumerable<string> AccessibleKeys =>
    _dataKeyProvider is not null ? _dataKeyProvider.GetAccessibleKeys() : [];
```

DKNet's global query filter reads `AccessibleKeys` to scope every `IOwnedBy` entity's reads to the
current caller — `ProductOwnershipIsolationTests` proves two authenticated callers sharing one
host/database get distinct ownership keys and neither can read or list the other's row (a cross-read
returns 404, not 403 — the filter denies it as "not found", it does not reveal existence).

Before every `SaveChanges`, `CoreDbContext.EnsureOwnershipResolvable()` fails closed:

```csharp
private void EnsureOwnershipResolvable()
{
    if (_dataKeyProvider is null) return;
    if (!string.IsNullOrEmpty(_dataKeyProvider.GetOwnershipKey())) return;

    var hasUnattributableInsert = ChangeTracker.Entries()
        .Any(e => e.State == EntityState.Added
                  && e.Entity is IAuditedProperties { CreatedBy: null or "" }
                  && e.Metadata.FindProperty(nameof(IAuditedProperties.CreatedBy)) is { IsNullable: false });

    if (hasUnattributableInsert) throw new OwnershipRequiredException();
}
```

If an authenticated caller's ownership key cannot be resolved and a new row would be left with no
`CreatedBy`, this throws `OwnershipRequiredException` **before** EF Core attempts the insert —
otherwise EF Core's own required-column check would throw a raw `DbUpdateException` that leaks
column/entity names into the response. `FluentValidationConfig.AddErrorResponses` maps that
exception to **403 Forbidden**:

```csharp
o.StatusCode = ctx =>
{
    if (ctx.Source == ErrorSource.Unhandled && ctx.Exception is OwnershipRequiredException)
        return StatusCodes.Status403Forbidden;
    return ctx.Errors.Any(e => e.Code?.StartsWith(PreconditionCodes.Prefix, StringComparison.Ordinal) == true)
        ? StatusCodes.Status409Conflict
        : null;
};
```

`ProductOwnershipIsolationTests.AuthenticatedCallerWithNoSubjectClaim_IsRefusedWithForbidden_NoRowPersistedOrReadable`
pins the whole chain: an authenticated caller with no resolvable subject claim gets 403, and the row
is never persisted at all (checked with `IgnoreQueryFilters()` directly against the database, not
just "unreadable over HTTP").

**Tests that pin this area** (all in `<YourApp>.App.Tests/Integration/...`):

- `AutomatedSample/V1/ProductOwnershipIsolationTests` — two distinct authenticated callers get
  distinct ownership keys; neither can read or list the other's row; `oid` takes precedence over
  `NameIdentifier` when both are present; a caller with no resolvable subject claim is refused 403
  with no row left behind.
- `AutomatedSample/V1/ProductAuditStampTests` — the acting caller is recorded as `CreatedBy`/
  `UpdatedBy` even under a **fixed tenant** ownership key, proving `OwnedBy` (tenant) and
  `CreatedBy`/`UpdatedBy` (individual actor) are stamped independently and never conflated.
- `AutomatedSample/V1/ProductSecurityTests` — `CreatedBy`/`UpdatedBy` come from the authenticated
  caller's own ownership key, never from any acting-user-shaped field in the request payload; an
  explicit action (`Approve`) is the one place a payload-supplied acting user is legitimately
  honored.
- `ManualSample/V1/PurchaseOrderSecurityTests` — `[FromClaim] ByUser` always wins over any `byUser`
  value present in the request body or query string.
- `ManualSample/V1/PurchaseOrderNoNameClaimAttributionTests` — with no `ClaimTypes.Name` claim at
  all, update/cancel/delete are all refused 400 and the stored order is unchanged.

## Sensitive data

`[SensitiveData("role")]` (DKNet.EfCore.Abstractions.Attributes) on an entity property means the
JSON response omits that property for any caller who does not hold the named role; `[SensitiveData]`
with no role means it is omitted for any caller who is not authenticated at all — any authenticated
caller receives it regardless of role. On `Product`:

```csharp
[SensitiveData("pricing")]
public decimal? SupplierCostPrice { get; private set; }

[SensitiveData]
public string? SupplierReferenceCode { get; private set; }
```

The attribute travels onto the generated DTO automatically; a hand-written DTO member that mirrors a
`[SensitiveData]` entity property must carry the same attribute itself — it is not inherited through
a plain property copy.

Filtering is opt-in at the JSON-serialization layer, wired once in `ServiceConfigs.AddOptions`:

```csharp
services.AddSingleton<IConfigureOptions<JsonOptions>>(sp =>
    new ConfigureOptions<JsonOptions>(op =>
        op.SerializerOptions.UseRoleAwareSensitiveData(
            sp.GetRequiredService<ISensitiveDataPrincipalAccessor>())));
```

`ISensitiveDataPrincipalAccessor`'s implementation is a two-line adapter over the already-registered
`IHttpContextAccessor`:

```csharp
internal sealed class HttpContextSensitiveDataPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
    : ISensitiveDataPrincipalAccessor
{
    public ClaimsPrincipal? Current => httpContextAccessor.HttpContext?.User;
}
```

**Tests:** `ProductSensitiveDataTests` (role-bearing vs. non-role-bearing authenticated callers, and
that two callers are judged independently in either request order), `ProductSensitiveDataAnonymousTests`
(an anonymous caller gets neither sensitive property but still gets the ordinary ones),
`ProductSensitiveDataDemoAuthenticatedTests` (the demo identity gets the no-role property but not the
`"pricing"`-role one, since the demo identity holds no roles).

## Idempotency, in one paragraph

POST routes are not idempotent unless the route calls `.RequiredIdempotentKey()` explicitly, with
callers sending `X-Idempotency-Key`. The store is Redis when `ConnectionStrings:Redis` is set, else
an in-process in-memory store; both use `ConflictHandling = IdempotentConflictHandling.ConflictResponse`
(`<YourApp>.Api/Configs/AppConfig.cs`). See `dknet-crud` for how a handler's `Result`
maps to a response body, and `dknet-platform-config` for CORS, security headers, and rate limiting.
Status mapping: 400 validation, 401 no/invalid credential, 403 `OwnershipRequiredException` or a
failed authorization policy, 404 not found or filtered out by ownership, 409 a
`PreconditionCodes`-prefixed error, 429 rate limit, 500 unhandled.

## Testing with authorization on

`Program.cs` binds `FeatureOptions` from configuration in its very first lines — before
`WebApplicationFactory`'s own `ConfigureAppConfiguration` override is merged in. A plain
configuration override registered through the test factory's usual hook therefore cannot flip
`RequireAuthorization` or `EnableDemoAuthentication`, because that early bind has already run by the
time the override would apply. The shipped fixtures work around this by setting environment
variables in the fixture's constructor, which **are** visible to `WebApplication.CreateBuilder(args)`
at the point it builds its initial configuration:

```csharp
public sealed class AuthOnApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    private const string RequireAuthorizationEnvKey = "FeatureManagement__RequireAuthorization";
    private const string EnableDemoAuthenticationEnvKey = "FeatureManagement__EnableDemoAuthentication";

    public AuthOnApiFixture()
    {
        Environment.SetEnvironmentVariable(RequireAuthorizationEnvKey, "true");
        Environment.SetEnvironmentVariable(EnableDemoAuthenticationEnvKey, "false");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        TestAuthHandler.Register(services);
    }
    // ... clears both env vars again in Dispose
}
```

This is only safe because the test assembly disables collection parallelization — no other test's
host can boot while the variable is set, or it would leak into an unrelated test run.

`TestAuthHandler` (`<YourApp>.App.TestSupport/TestAuthHandler.cs`) replaces the real JWT bearer scheme
so a request can be authenticated without a live token. It issues a fixed name/subject and a scope
claim built from every `ProductScopes` entry by default, overridable per request via a header:

```csharp
public const string ScopesHeaderName = "X-Test-Scopes";
public static readonly string DefaultScopes = string.Join(' ', ProductScopes.All);

var identity = new ClaimsIdentity(
    [
        new Claim(ClaimTypes.Name, CallerName),
        new Claim(ClaimTypes.NameIdentifier, CallerProfileId.ToString()),
        new Claim("scp", scopes)
    ],
    SchemeName);
```

To write an auth-on test for a new feature: add a fixture that mirrors `AuthOnApiFixture` (env vars
in the constructor, cleared in `Dispose`, `TestAuthHandler.Register(services)` in
`ConfigureTestServices`), then assert against `TestAuthHandler.CallerName` /
`TestAuthHandler.CallerProfileId` the same way `PurchaseOrderSecurityTests`/`ProductSecurityTests`
do. For a test that needs two distinct callers in one host (row-isolation tests),
`<YourApp>.App.TestSupport/MultiSubjectAuthHandler.cs` reads the subject from a request header instead
of a fixed constant — see `AuthOnMultiSubjectApiFixture` and `ProductOwnershipIsolationTests`. For a
caller authenticated with no name claim at all, see `AuthOnNoNameClaimApiFixture`. For a fixed-tenant
`OwnedBy` with a still-distinct acting `CreatedBy`, see `AuthOnFixedTenantApiFixture`.
`RequireAuthorizationPlusDemoApiFixture` sets **both** flags on and, unlike every other fixture in
that folder, does not implement `IAsyncLifetime` or reset the database — building the host is itself
the thing under test, and it is expected to fail start-up.

## Common mistakes

- **What you might expect:** calling `.RequireAuthorization(scope)` unconditionally on a route.
  **What actually happens:** with `RequireAuthorization` off, no policies were ever registered, so
  the call throws at request time. Gate it on `FeatureOptions.RequireAuthorization`, as
  `ProductV1Endpoint` does — or declare the scope with `[EndpointGroupScope]`, which is applied only
  when that flag is on and therefore needs no branch at all.
- **What you might expect:** one `[EndpointGroupScope]` covering the methods you care about leaves
  the rest of the group unguarded. **What actually happens:** it fails closed — a served method with
  no declaration and no group default makes endpoint building throw, not silently pass through.
- **What you might expect:** giving a generated `[CrudAction]` a parameter meant to auto-populate
  from the caller. **What actually happens:** it becomes an ordinary, caller-settable bound property
  — there is no `[FromClaim]` seam on a generated request.
- **What you might expect:** `EnableDemoAuthentication` is safe to leave on alongside
  `RequireAuthorization` for a "belt and suspenders" local setup. **What actually happens:**
  `AddAppConfig` throws at start-up — the two are hard mutually exclusive.
- **What you might expect:** an unauthenticated caller's write just gets attributed to
  `SharedConsts.SystemAccount` and proceeds. **What actually happens:** only true for a context with
  no `IDataOwnerProvider` friction; once ownership stamping is wired (as it is for `Product`), a
  caller whose subject claim cannot be resolved gets a 403 `OwnershipRequiredException`, not a
  silent system-account write.
- **What you might expect:** a config-file override in a `WebApplicationFactory` subclass can flip
  `RequireAuthorization` for a single test class. **What actually happens:** `Program.cs` has already
  bound `FeatureOptions` before that override merges in — only an environment variable set before the
  host builds (in the fixture's constructor) takes effect.
