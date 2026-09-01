# Extension Points

Where your own code attaches to a solution scaffolded by `dotnet new dknet-minimal`. Each seam
below is discovered by convention or by assembly scan — none of them has a registration list you
must remember to update.

Paths use the template's own project names (`Minimal.Api`, `Minimal.AppServices`, …); a generated
solution renames them to `<YourApp>.*`. For the configuration keys these seams read, see
[`configuration-reference.md`](configuration-reference.md).

## At a glance

| Seam | Contract | Where you put it | How it is discovered |
|---|---|---|---|
| HTTP route group | `IEndpointConfig` | `Minimal.Api/ApiEndpoints/<Feature>/` | Assembly scan by `UseEndpointConfigs` |
| Command / query | `Fluents.Requests.*` / `Fluents.Queries.*` | `Minimal.AppServices/<Feature>/V1/` | `AutoDeclareFrom` on the in-memory bus |
| Request validation | `AbstractValidator<TRequest>` | next to the request | `AddValidatorsFromAssembly` |
| Domain event consumer | `Fluents.EventsConsumers.IHandler<TEvent>` | `AppServices` or `Infra` | `AddServicesFromAssembly` |
| Query filter | `Specification<TEntity>` | `Minimal.AppServices/<Feature>/V1/Specs/` | Constructed by the handler |
| EF Core mapping | `IEntityTypeConfiguration<T>` | `Minimal.Infra/Features/<Feature>/Mappers/` | `UseAutoConfigModel` |
| Seed data | `IDataSeedingConfiguration<T>` | `Minimal.Infra/Features/<Feature>/StaticData/` | `UseAutoDataSeeding` |
| Claims enrichment | `IClaimsTransformation` | `Minimal.Api/Configs/Auth/` | Explicit DI registration |
| Authorization rule | `IAuthorizationRequirement` + `IAuthorizationHandler` | `Minimal.Api/Configs/Auth/` | Explicit DI registration |
| Rate-limit partitioning | `IRateLimitKeyProvider` | `Minimal.Api/Configs/RateLimits/` | Explicit DI registration |
| Rate-limit values | `IRateLimitOptionsProvider` | `Minimal.Api/Configs/RateLimits/` | Explicit DI registration |
| Domain service | `IDomainService` and friends | `Minimal.Domains/Services/` + `Minimal.Infra/Services/` | Explicit DI registration |
| Test host | `TestApiFactoryBase` | `Minimal.App.TestSupport/` | Subclass it |

## Endpoints

`IEndpointConfig` (`DKNet.AspCore.Extensions`) is the only route-registration seam. Implement it
and the route group exists — `Minimal.Api/Program.cs` calls `UseEndpointConfigs(...)` once, which
scans the API assembly, builds one versioned `RouteGroupBuilder` per implementation, and calls
`Map`. There is no registration list anywhere in the template.

```csharp
internal sealed class PurchaseOrderV1Endpoint : IEndpointConfig
{
    public int Version => 1;                     // → /v1/...
    public string GroupEndpoint => "/purchase-orders";

    public void Map(RouteGroupBuilder group) { /* literal MapPost/MapGet/... */ }
}
```

Two further members carry defaults the template never overrides, and both are yours to set:

| Member | Default | What it controls |
|---|---|---|
| `string? AuthPolicy` | `null` | The authorization policy applied to the whole group. `null` means `RequireAuthorization()` with no named policy. Ignored entirely when `FeatureManagement:RequireAuthorization` is `false`. This is where you would attach `HasScopeRequirement.PolicyName`. |
| `string Tag` | `GroupEndpoint` with `/` replaced by `-` — `"/products"` becomes `"products"` | The OpenAPI tag the group's routes are grouped under. |

Two rules the template's own endpoints follow, both enforced by
`Minimal.App.Tests/Architecture/ApiTests.cs`:

- The class must be `internal sealed` (abstract bases may stay non-sealed).
- Keep every `Map*` call **literal**. .NET 10's validation source generator only sees literal
  `Map*(string, Delegate)` calls in the compiling project's own source; a route registered through
  a generic library wrapper silently loses DataAnnotations validation. See
  [`api-pipeline.md`](api-pipeline.md) and
  [`samples/manual-vs-automated.md`](samples/manual-vs-automated.md#1-request-validation-that-looks-wired-but-never-runs-the-sharpest-gap).

The `Program.cs` call also sets `ConfigureGroup`, which runs for every discovered group after the
contextual-population filter and before authorization. The template uses it for exactly one thing
— `group.AddFluentValidationAutoValidation()` — and it is where any other cross-cutting endpoint
filter belongs:

```csharp
o.ConfigureGroup = (group, _) => group.AddFluentValidationAutoValidation();
```

## Requests, handlers and validators

A request is a record implementing one of the `DKNet.SlimBus.Extensions` fluent contracts; the
handler is an `internal sealed` class implementing the matching handler interface. Both are
discovered by `AutoDeclareFrom`/`AddServicesFromAssembly` on the in-memory child bus
(`Minimal.Infra/Extensions/ServiceBusSetup.cs`), scanning the `Minimal.AppServices` assembly. No
per-message registration.

| Shape | Request contract | Handler contract |
|---|---|---|
| Command returning a DTO | `Fluents.Requests.IWitResponse<TDto>` | `Fluents.Requests.IHandler<TRequest, TDto>` |
| Command with no response | `Fluents.Requests.INoResponse` | `Fluents.Requests.IHandler<TRequest>` |
| Query returning one item | `Fluents.Queries.IWitResponse<TDto>` | `Fluents.Queries.IHandler<TRequest, TDto>` |
| Query returning a page | `Fluents.Queries.IWitPageResponse<TDto>` | `Fluents.Queries.IPageHandler<TRequest, TDto>` |
| Domain event consumer | a plain `sealed record` | `Fluents.EventsConsumers.IHandler<TEvent>` |

The handler method is `OnHandle(TRequest, CancellationToken)`, not `Handle`. Handlers never call
`SaveChanges` — the SlimBus EF Core interceptor does, after the handler returns.

**The validator convention.** Declare an `AbstractValidator<TRequest>` and it runs; there is no
opt-in call on the route. `Minimal.Api/Configs/FluentValidationConfig.cs` registers every validator
in the `Minimal.AppServices` assembly, internal types included:

```csharp
builder.Services.AddValidatorsFromAssembly(typeof(AppSetup).Assembly, includeInternalTypes: true);
```

Two constraints come with the convention:

- The validator must live in the `Minimal.AppServices` assembly. One placed in `Minimal.Api` is
  never registered.
- It must be `internal sealed`, enforced by `Minimal.App.Tests/Architecture/AppServiceTests.cs`.

Co-locate it with the request it validates, as `ListPurchaseOrders.cs` does.

## Authorization and claims

Both seams are registered by `Minimal.Api/Configs/Auth/AuthConfig.cs`, and both ship as samples
marked `TODO` in source — replace them rather than adding a parallel set.

**Claims transformation.** `SampleClaimsTransformation` implements `IClaimsTransformation` and is
registered scoped. ASP.NET Core invokes it on every authentication event, so it is the seam for
enriching the principal — roles from a database, normalised claim types, a tenant identifier. It
may be called more than once per request, so guard against adding a claim twice; the sample shows
the guard.

**Authorization policy.** `HasScopeRequirement` (an `IAuthorizationRequirement`) plus
`HasScopeHandler` (an `AuthorizationHandler<HasScopeRequirement>`) implement the sample
`SampleScopePolicy`, which checks the `scp`/`scope` claim for a required scope. The policy is
registered but applied to no shipped route — attach it with
`.RequireAuthorization(HasScopeRequirement.PolicyName)` on a route, or by returning that constant
from an endpoint's `AuthPolicy`.

**The acting user.** `IPrincipalProvider` (`Minimal.AppServices/Share/IPrincipalProvider.cs`)
extends DKNet's `IDataOwnerProvider` with `ProfileId`, `Email` and `UserName`. The implementation,
`Minimal.Api/Configs/Handlers/PrincipalProvider.cs`, resolves the ownership key from the first
non-empty of `http://schemas.microsoft.com/identity/claims/objectidentifier`, `oid`,
`ClaimTypes.NameIdentifier`, `sub`. Replace the implementation to change how identity is read;
`.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()` in `ServiceConfigs.cs` is the single
registration point. Full behaviour:
[`auditing-and-data-ownership.md`](auditing-and-data-ownership.md).

**Reading a claim into a request.** Mark a request property `[FromClaim(...)]` and the endpoint
pipeline overwrites it from the caller's claims before validation and before the handler — a
security seam, not a binding convenience. It never reaches a generated CRUD request, because the
generator forwards only `System.ComponentModel.DataAnnotations` attributes.

## Rate limiting

Two public interfaces sit behind the limiter, both registered in
`Minimal.Api/Configs/RateLimits/RateLimitConfig.cs` and both meant to be replaced:

| Interface | Default implementation | Replace it to |
|---|---|---|
| `IRateLimitKeyProvider` | `RateLimitKeyProvider` — `User.Identity.Name`, then remote IP, then request host | Partition by tenant, subscription tier, API key, or route instead of caller identity. Registered as a singleton. |
| `IRateLimitOptionsProvider` | `RateLimitOptionsProvider` — returns the same `RateLimit` section values for everyone | Return per-caller limits: look the caller's plan up and vary `PermitLimit`/`Window`. Registered as **scoped**, so it can inject request-scoped services. |

Both are resolved per request from `HttpContext.RequestServices`, so a replacement takes effect
without touching the limiter wiring itself.

## Generated CRUD slices

An entity carrying `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` plus a `[GenerateDto]` DTO gets its
requests, handlers and routes emitted by `DKNet.SlimBus.Generators`. Full mechanics:
[`crud-attributes.md`](crud-attributes.md). Two seams are worth knowing here.

**Excluding an operation.** The generated `Map<Entity>Crud()` takes an optional
`Action<CrudMapOptions>`. It is the only knob on the generated method:

```csharp
public void Map(RouteGroupBuilder group)
{
    group.MapProductCrud(o => o.Exclude(CrudOp.Delete));   // hand-map delete yourself instead
    group.MapDelete("{id:guid}", /* your own pre-delete rule */);
}
```

`CrudOp` has six members — `GetById`, `GetList`, `Create`, `Update`, `Delete`, `Action`. `Update`
and `Action` are all-or-nothing: there is no per-method exclusion. Nothing is excluded by default.
The template's own `ProductV1Endpoint` passes no options, and
`Minimal.App.Tests/Architecture/SampleInvariantTests.cs` pins that — change the sample and that
test tells you.

**Narrowing the response.** Filter, search and order on the generated list route resolve against
the DTO, never the entity, so `[GenerateDto(..., Exclude = [...])]` is the query-surface control as
well as the response-shape control. See
[`generic-list-endpoint.md`](generic-list-endpoint.md#the-dto-is-the-boundary).

## Persistence

| Seam | Contract | Discovery |
|---|---|---|
| Table mapping | `IEntityTypeConfiguration<T>`, usually via `DefaultEntityTypeConfiguration<T>` | `UseAutoConfigModel([...])` scans `Minimal.Infra` and `Minimal.Domains` |
| Seed data | `IDataSeedingConfiguration<T>`, usually via `DataSeedingConfiguration<T>` | `UseAutoDataSeeding([...])` scans `Minimal.Infra` |
| Named sequences | a `[SqlSequence]` enum plus a `SequenceService` subclass | `UseAutoConfigModel` registers the enum's sequences on PostgreSQL |

Both scans are wired in **two** places that build a `CoreDbContext`, and both must carry the call:
`Minimal.Infra/Extensions/InfraSetup.cs` (`AddInfraServices`, the DI host path) and
`Minimal.Infra/Extensions/InfraMigration.cs` (`MigrateDb`, the startup-migration path). Wiring only
one is a bug this template already hit.

Mapping and seeding classes must be `internal sealed`
(`Minimal.App.Tests/Architecture/InfraTests.cs`), every mapped `string` needs an explicit
`HasMaxLength`, and every mapped enum needs `HasConversion<string>()`.

`CoreDbContext` itself carries one guard you can rely on but should not bypass:
`EnsureOwnershipResolvable` throws `OwnershipRequiredException` before EF Core attempts an insert
that would leave a required `CreatedBy` unset, and the global exception handler turns that into a
`403`, not a `500`.

## Domain services

`Minimal.Domains/Services/` holds the contracts, `Minimal.Infra/Services/` the implementations,
and `InfraSetup.AddInfraServices` the registration. The shipped chain is deliberately thin:

```csharp
public interface IDomainService;                        // marker
public interface ISequenceServices : IDomainService { ValueTask<string> NextValueAsync(); }
public interface IMembershipService : ISequenceServices; // one sequence, one interface
```

Add your own the same way: an interface in `Minimal.Domains/Services/` so the domain can depend on
it, an implementation in `Minimal.Infra/Services/`, and one `AddScoped` line in `AddInfraServices`.
Keeping the contract in `Domains` is what lets a domain method call the service without the domain
layer knowing about infrastructure.

## Messaging beyond the in-memory bus

`Minimal.Infra/Extensions/ServiceBusSetup.cs` is the single place topology is declared. To forward
an event to Azure Service Bus, add the produce/consume pair next to the existing
`ProductCreatedEvent` lines and write the consumer under
`Minimal.Infra/Features/<Feature>/ExternalEvents/`:

```csharp
azb.Produce<TEvent>(o => o.DefaultTopic("<topic>"));
azb.Consume<TEvent>(o => o.Path("<topic>")
    .SubscriptionName("<subscription>")
    .WithConsumer<THandler>());
```

No DI registration is needed — `azb.AddServicesFromAssembly(typeof(InfraSetup).Assembly)` picks the
consumer up. `TopologyProvisioning.Enabled` is `false`, so provision the topic and subscription
yourself. Detail: [`slimbus-messaging.md`](slimbus-messaging.md).

## Health checks

`Minimal.Api/Configs/Healthz/HealthzConfig.cs` registers an EF Core connectivity check plus
`HealthCheckHandler`, and maps them at `/healthz` and `/`. `HealthCheckHandler` is a template stub
that always reports healthy — implement `IHealthCheck` and add it in `AddHealthzConfig` for a real
readiness probe. The whole block is gated on `FeatureManagement:EnableHealthCheck`.

## Test hosts

`Minimal.App.TestSupport/TestApiFactoryBase.cs` is the shared `WebApplicationFactory<Program>` both
suites use. It boots the `Testing` environment, swaps `CoreDbContext` onto EF Core InMemory (via
`AddDbContextWithHook`, so domain events still publish) and substitutes `IMembershipService`. Two
`protected virtual` members are the seams:

| Member | Use it to |
|---|---|
| `AddFeatureOverrides(IDictionary<string, string?>)` | Add configuration entries for one suite. Extends the base set rather than replacing it. |
| `ConfigureTestServices(IServiceCollection)` | Swap further services. Call `base.ConfigureTestServices(services)` first. |

Feature flags are the exception: `Program.cs` binds `FeatureOptions` before these overrides merge,
so a flag must come from `appsettings.Testing.json` or a `FeatureManagement__<Flag>` environment
variable. The shipped fixtures — `AuthOnApiFixture`, `SwaggerOnApiFixture`,
`VersioningOffApiFixture`, `CorsAllowlistApiFixture` — show the pattern; add a variant only when a
test genuinely needs a different host.

## Boundaries your code must respect

These are checked by `Minimal.App.Tests/Architecture/`, so a violation fails the test run rather
than review.

| Rule | Test |
|---|---|
| Endpoint, handler, validator, EF-config and seeding classes are `internal` and `sealed` | `ApiTests`, `AppServiceTests`, `InfraTests` |
| `Configs` classes are static and `[ExcludeFromCodeCoverage]` | `ApiTests` |
| Every mapped `string` property has an explicit max length | `InfraTests.NoEntityString_ShouldBe_ConfiguredAs_Max` |
| Every mapped enum is stored with `HasConversion<string>()` | `InfraTests.AllEnumProperties_StoringToDb_ShouldHaveStringConversion` |
| No public property on a `record` has a private setter | `RecordArchitectureTests` |
| Npgsql only — no SQL Server EF Core, Testcontainers or Aspire hosting package | `PackageArchitectureTests`, `MigrationSchemaTests` |
| Every `PackageReference` is version-less; every DKNet version pin is referenced by a project; all DKNet packages resolve to one release | `PackageArchitectureTests` |
| JWT signature validation is never disabled in the API source | `JwtSignatureValidationTests` |
| The base `appsettings.json` never turns a security flag off, and always carries an explicit `RateLimit` section | `SecureDefaultAppSettingsTests` |
| Every `FeatureManagement` key in every `appsettings` file binds to a `FeatureOptions` property, and every property is read by production code | `FeatureOptionsBindingTests`, `FeatureFlagContractTests` |
| The two samples never reference each other | `SampleInvariantTests.SampleAreas_ShouldNotCrossReferenceEachOther` |

The **layer boundary itself** is not one of these. `Minimal.Domains` references only
`Minimal.Share`; `Minimal.AppServices` references only `Domains`; `Infra` and `Api` reference
inward. Because every reference already points inward, adding an outward one — `Domains` to
`AppServices`, say — is a circular project reference that MSBuild refuses, so the compiler is what
holds the boundary, not a test.
