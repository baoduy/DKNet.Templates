# DKNet Package Inventory

The DKNet family ships one NuGet package per capability, each with its own reference doc in the
[DKNet repo](https://github.com/baoduy/DKNet). This page lists only what this template actually
wires up. Follow a package's link for its full API surface.

## Wired by this template

This table is verified against the `.csproj` files under `src/`. Every DKNet package is pinned at
**10.1.14** in `src/Directory.Packages.props`, and
`Minimal.App.Tests/Architecture/PackageArchitectureTests.cs` fails the build if the pins ever
straddle two releases, if a pin exists that no project references, or if a `PackageReference`
carries an inline `Version` attribute.

| Package | What it gives you | Where the template wires it | DKNet doc |
|---|---|---|---|
| **DKNet.AspCore.Extensions** | `IEndpointConfig` discovery/mapping, `[FromClaim]` request-member population via `AddContextualRequestPopulation` | `Minimal.Api/Program.cs` (`UseEndpointConfigs`, `AddContextualRequestPopulation`); `[FromClaim(ClaimTypes.Name)]` on `Minimal.AppServices/ManualSample/V1/Actions/Create.cs`'s `ByUser` | [docs/AspNetCore/DKNet.AspCore.Extensions.md](https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Extensions.md) |
| **DKNet.AspCore.Idempotency** | Endpoint filter enforcing an idempotency key header on a route, with configurable conflict handling | `.RequiredIdempotentKey()` on the create route in `Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs`; `AddIdempotentKey()` fallback registration in `Minimal.Api/Configs/AppConfig.cs` — the non-generic overload is obsolete and deliberately suppressed there, used only when no Redis connection string is configured | [docs/AspNetCore/DKNet.AspCore.Idempotency.md](https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Idempotency.md) |
| **DKNet.AspCore.Idempotency.RedisStore** | Redis-backed idempotency key store, used when a Redis connection string is configured | `AddIdempotencyWithRedisStore(...)` in `Minimal.Api/Configs/AppConfig.cs`, gated on `ConnectionStrings:Redis` | [docs/AspNetCore/DKNet.AspCore.Idempotency.RedisStore.md](https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Idempotency.RedisStore.md) |
| **DKNet.EfCore.Abstractions** | `AuditedEntity<TKey>`/`IAuditedProperties` base audit tracking, `[RaisesEvent]`/`[CrudCreate]`/`[CrudUpdate]` declarative attributes | `Minimal.Domains/Share/DomainEntity.cs` extends `AuditedEntity<Guid>`; `[RaisesEvent]`, `[CrudCreate]`, `[CrudUpdate]` on `Minimal.Domains/Features/AutomatedSample/Entities/Product.cs` | [docs/EfCore/DKNet.EfCore.Abstractions.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Abstractions.md) |
| **DKNet.EfCore.DataAuthorization** | `IDataOwnerProvider`/`IDataOwnerDbContext`, the `DataOwnerHook` that stamps ownership and audit fields on save | `Minimal.AppServices/Share/IPrincipalProvider.cs` implements `IDataOwnerProvider`; `.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()` in `Minimal.Api/Configs/ServiceConfigs.cs`; `Minimal.Infra/Contexts/CoreDbContext.cs` implements `IDataOwnerDbContext`. See [auditing-and-data-ownership.md](./auditing-and-data-ownership.md) for the full save-pipeline story. | [docs/EfCore/DKNet.EfCore.DataAuthorization.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DataAuthorization.md) |
| **DKNet.EfCore.DtoGenerator** | Source-generates a DTO record from `[GenerateDto(typeof(Entity))]`, mapped automatically | `[GenerateDto(typeof(Product))]` on `Minimal.AppServices/AutomatedSample/V1/ProductDto.cs` | [docs/EfCore/DKNet.EfCore.DtoGenerator.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DtoGenerator.md) |
| **DKNet.EfCore.Events** | `AddEventPublisher<TDbContext, TPublisher>()` — publishes domain events raised on aggregates during `SaveChanges` | `.AddEventPublisher<CoreDbContext, EventPublisher>()` in `Minimal.Infra/Extensions/InfraSetup.cs` | [docs/EfCore/DKNet.EfCore.Events.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Events.md) |
| **DKNet.EfCore.Extensions** | `UseAutoConfigModel` / `UseAutoDataSeeding` — assembly-scan discovery of `IEntityTypeConfiguration<T>` mappers and `IDataSeedingConfiguration<T>` seeders, so no manual `DbSet` or seeding registration is needed | `.UseAutoConfigModel(...)` + `.UseAutoDataSeeding(...)` in both `Minimal.Infra/Extensions/InfraSetup.cs` and `Minimal.Infra/Extensions/InfraMigration.cs`; seeding configuration types imported via `Minimal.Infra/GlobalUsings.cs` | [docs/EfCore/DKNet.EfCore.Extensions.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Extensions.md) |
| **DKNet.EfCore.Relational.Helpers** | `DbContextHelpers` — table-existence checks, raw connection access, schema/table-name lookup | Referenced by `Minimal.Infra.csproj`; no call site in the checked-in template source today — it's a ready import for developer code that needs those helpers | [docs/EfCore/DKNet.EfCore.Relational.Helpers.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Relational.Helpers.md) |
| **DKNet.EfCore.Specifications** | `Specification<T>`, `IRepositorySpec`, keyset paging — composable query filters over a repository | `.AddSpecRepo<CoreDbContext>()` in `Minimal.Infra/Extensions/InfraSetup.cs`; `Minimal.AppServices/ManualSample/V1/Specs/SpecGetPurchaseOrder.cs` extends `Specification<PurchaseOrder>`; `IRepositorySpec` used throughout `Minimal.AppServices/ManualSample/V1/Actions/` | [docs/EfCore/DKNet.EfCore.Specifications.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Specifications.md) |
| **DKNet.Fw.Extensions** | Core framework helpers, including the `TypeExtractors` fluent assembly-scanning API (`.Extract().Classes().NotAbstract()...`) | `Minimal.AppServices/Extensions/MapsToExtensions.cs` uses `DKNet.Fw.Extensions.TypeExtractors` to discover `[MapsFrom]`/`[GenerateDto]` DTO types and register their Mapster configs | [docs/Core/DKNet.Fw.Extensions.md](https://github.com/baoduy/DKNet/blob/dev/docs/Core/DKNet.Fw.Extensions.md) |
| **DKNet.SlimBus.Extensions** | `Fluents.Requests`/`Fluents.Queries` interfaces for SlimMessageBus handlers, `AddSlimBusEfCoreInterceptor<TDbContext>()` | `.AddSlimBusEfCoreInterceptor<CoreDbContext>()` in `Minimal.Infra/Extensions/ServiceBusSetup.cs`; `Fluents.Requests.IWitResponse<T>`/`IHandler` implemented across `Minimal.AppServices/ManualSample/V1/Actions/` and `Minimal.AppServices/AutomatedSample/V1/` | [docs/Messaging/DKNet.SlimBus.Extensions.md](https://github.com/baoduy/DKNet/blob/dev/docs/Messaging/DKNet.SlimBus.Extensions.md) |
| **DKNet.SlimBus.Generators** | Roslyn source generator: from `[CrudCreate]`/`[CrudUpdate]` on an entity plus its `[GenerateDto]` DTO, emits the request records, handlers, and a `Map{Entity}Crud()` endpoint-mapping extension for a full CRUD slice | Analyzer-only reference on `Minimal.AppServices.csproj`; triggered by `[CrudCreate]`/`[CrudUpdate]` on `Minimal.Domains/Features/AutomatedSample/Entities/Product.cs` plus `[GenerateDto(typeof(Product))]` on `Minimal.AppServices/AutomatedSample/V1/ProductDto.cs`; the generated `MapProductCrud()` is called from `Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs` — this is the entire `AutomatedSample` slice, no hand-written request/handler exists for it | [docs/Messaging/DKNet.SlimBus.Generators.md](https://github.com/baoduy/DKNet/blob/dev/docs/Messaging/DKNet.SlimBus.Generators.md) |

## Available but not wired by this template

The rest of the DKNet family a developer may want to add later. None of these are referenced by any
`.csproj` in this template — add the package yourself before using them.

| Package | What it gives you | DKNet doc |
|---|---|---|
| DKNet.EfCore.AuditLogs | Structured audit-log entries for entity changes, beyond the four `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` fields | [docs/EfCore/DKNet.EfCore.AuditLogs.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.AuditLogs.md) |
| DKNet.Svc.BlobStorage.Abstractions (+ .AzureStorage / .AwsS3 / .Local) | Provider-agnostic blob storage abstraction with swappable backends | [docs/Services/DKNet.Svc.BlobStorage.Abstractions.md](https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.BlobStorage.Abstractions.md) |
| DKNet.EfCore.Encryption | Transparent column-level encryption through an EF Core value converter | [docs/EfCore/DKNet.EfCore.Encryption.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Encryption.md) |
| DKNet.EfCore.Hooks | The before/after-`SaveChanges` interceptor pipeline the Events and DataAuthorization packages register against. Pulled in transitively today — reference it directly only to register a hook of your own | [docs/EfCore/DKNet.EfCore.Hooks.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Hooks.md) |
| DKNet.Svc.Encryption | Symmetric/asymmetric encryption helpers for sensitive field values | [docs/Services/DKNet.Svc.Encryption.md](https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.Encryption.md) |
| DKNet.Svc.PdfGenerators | PDF document generation | [docs/Services/DKNet.Svc.PdfGenerators.md](https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.PdfGenerators.md) |
| DKNet.Svc.Transformation | Data transformation pipelines | [docs/Services/DKNet.Svc.Transformation.md](https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.Transformation.md) |
| DKNet.RandomCreator | Deterministic/seedable random value generation for tests and seeding | [docs/Core/DKNet.RandomCreator.md](https://github.com/baoduy/DKNet/blob/dev/docs/Core/DKNet.RandomCreator.md) |
| DKNet.AspCore.Tasks | Background task scheduling | [docs/AspNetCore/DKNet.AspCore.Tasks.md](https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Tasks.md) |
| DKNet.AspCore.Idempotency.MsSqlStore / .NpgsqlStore / .Relational | Idempotency key stores on a SQL database instead of Redis — an alternative to the Redis store this template wires by default. `.NpgsqlStore` is the one that matches this template's provider | [docs/AspNetCore/DKNet.AspCore.Idempotency.Relational.md](https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Idempotency.Relational.md) |

### `Aspire.Hosting.ServiceBus` — in DKNet, but not on NuGet

DKNet also carries an `Aspire.Hosting.ServiceBus` project: an Aspire resource that runs the Azure
Service Bus emulator locally, so the `AzureBus` child bus can be exercised without a real namespace.
It is **not published** — `src/Aspire/Aspire.Hosting.ServiceBus/Aspire.Hosting.ServiceBus.csproj`
sets `<IsPackable>false</IsPackable>`, so `dotnet add package Aspire.Hosting.ServiceBus` cannot
resolve. The only way to use it is a `ProjectReference` from `Minimal.AppHost` to that project in a
local DKNet clone. DKNet's
[docs/Aspire/Aspire.Hosting.ServiceBus.md](https://github.com/baoduy/DKNet/blob/dev/docs/Aspire/Aspire.Hosting.ServiceBus.md)
describes the resource itself.

## Retired packages — never recommended

`DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions` are **retired and never published to
NuGet** — both projects set `<IsPackable>false</IsPackable>` and every public type carries
`[Obsolete]`. This template has never referenced them, and new code should not: `IRepositorySpec`
from `DKNet.EfCore.Specifications` is the supported way to query and persist. DKNet's own
[Migrating-Repos-To-Specifications](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/Migrating-Repos-To-Specifications.md)
covers the call-site mapping if you are porting older code into a scaffolded solution.
