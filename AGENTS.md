# AGENTS.md

## Scope
- This repository template is centered on `src/ApiEndpoints` and the solution `src/DKNet.Templates.sln`.
- Prefer code-verified patterns in this guide over older README statements when they differ.

## Architecture at a glance
- API startup is in `src/ApiEndpoints/Minimal.Api/Program.cs`: bind `FeatureOptions`, then `AddLogConfig` -> `AddAzureAppConfig` -> `AddFluentValidationConfig` -> `RunMigrationAsync` -> `AddAppConfig` -> `AddContextualRequestPopulation` -> `UseAppConfig(a => a.UseEndpointConfigs(...))`.
- Middleware/service composition is orchestrated by `Minimal.Api/Configs/AppConfig.cs` and `Minimal.Api/Configs/ServiceConfigs.cs`.
- Layer boundaries are strict: `Api` -> `AppServices` -> `Domains`, with infra wiring from `Minimal.Infra/Extensions/InfraSetup.cs`.
- `Minimal.AppHost/AppHost.cs` is Aspire host orchestration (Redis + PostgreSQL + API project), not business logic.
- Persistence uses EF Core with auto model config and seeding (`UseAutoConfigModel`, `UseAutoDataSeeding`) in `InfraSetup.AddInfraServices` **and** in `InfraMigration.MigrateDb` — both context-construction paths must wire seeding, or seed data never appears over HTTP depending on which startup path runs.

## Two worked feature patterns — pick one before copying
The template carries two side-by-side vertical slices demonstrating opposite ends of "hand-write it" vs "declare it and let the generator produce it". Read [`docs/samples/manual-vs-automated.md`](docs/samples/manual-vs-automated.md) before copying either — it states, layer by layer, what each writes and what the generated path gives up (most sharply: forwarded DataAnnotations validation on the automated sample is never evaluated under this template's own endpoint-registration convention).

- **Manual — `PurchaseOrder`** (`*/ManualSample/`): every layer hand-written — entity, event (`AddEvent` in the constructor), FluentValidation-backed create/update requests, business-rule rejection (`Cancel` on an already-cancelled order), filtered/paged list, request idempotency (`.RequiredIdempotentKey()`), static seed data. Endpoint: `Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs`.
- **Automated — `Product`** (`*/AutomatedSample/`): entity declares `[RaisesEvent(...)]` for its events, `[CrudCreate]`/`[CrudUpdate]` for its write operations, and a one-line `[GenerateDto(typeof(Product))]` DTO; `DKNet.SlimBus.Generators` produces the request/handler/route types (`CreateProductRequest`, `ChangePriceProductRequest`, their handlers, and `ProductCrudEndpointExtensions.MapProductCrud()`). Carries external Azure Service Bus publish/subscribe instead of idempotency/seeding. Endpoint: `Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs`.

## Feature vertical slice pattern (copy this)
- Endpoint contract: implement `IEndpointConfig` (from `DKNet.AspCore.Extensions`) in `Minimal.Api/ApiEndpoints/**/*V1Endpoint.cs`.
- Hand-mapped routes use the raw minimal-API surface (`group.MapPost(...)`, etc. — see `PurchaseOrderV1Endpoint`) or the package's generic entity helpers `MapGetList<TEntity,TKey,TDto>`/`MapGetById`/`MapPost<TRequest,TDto>`/`MapPutById`/`MapDeleteById` (see the generated `ProductCrudEndpointExtensions`).
- Manual write workflow example (`CreatePurchaseOrderRequest`): FluentValidation validator -> handler constructs the aggregate (which raises its own event via `AddEvent`) -> `repository.AddAsync` -> `mapper.ResultOf<TDto>(entity)`.
- Automated write workflow example (`Product`): `[CrudCreate]` constructor parameters become the generated request's properties 1:1; the generated handler calls `new Product(request.Name, request.Price)` and `repository.AddAsync` — no hand-written request, validator, or handler exists for it.
- Domain entity lives in `Minimal.Domains/Features/<Feature>/Entities` and keeps mutation in methods (`ChangeAmount(...)`, `Cancel(...)`, `ChangePrice(...)`).
- EF mapping lives in `Minimal.Infra/Features/<Feature>/Mappers` (`PurchaseOrderConfigs`, `ProductConfigs`) and enforces indexes/length/schema — hand-written for both samples; no generator produces `IEntityTypeConfiguration<T>`.

## Message bus and events
- `AddServiceBus` in `Minimal.Infra/Extensions/ServiceBusSetup.cs` always wires an in-memory child bus (`ImMemory`) for internal handlers.
- Azure Service Bus child bus (`AzureBus`) is added only when `ConnectionStrings:AzureBus` is non-empty; `Product`'s `Produce<ProductCreatedEvent>`/`Consume<ProductCreatedEvent>` wiring lives there.
- Domain events are published through `Minimal.Infra/Services/EventPublisher.cs` using `IMessageBus.Publish(...)`.
- Hand-raised example: `PurchaseOrderCreatedEvent`, raised by `AddEvent(...)` in `PurchaseOrder`'s constructor, consumed by `PurchaseOrderCreatedEventHandler`.
- Declared example: `[RaisesEvent(EventOperations.Created, Include = [...])]` / `[RaisesEvent(EventOperations.Updated, nameof(Price))]` on `Product` compose `ProductCreatedEvent`/`ProductPriceUpdatedEvent` at compile time — no `AddEvent` call anywhere in `AutomatedSample/`. Raised by DKNet's EF Core save hook, not by application code.

## Commands, mapping, and user context
- The acting user is populated onto `[FromClaim(ClaimTypes.Name)]`-decorated request properties by `AddContextualRequestPopulation` (wired in `Program.cs`), which falls back to `SharedConsts.SystemAccount` only when `RequireAuthorization` is off. `PurchaseOrderV1Endpoint` additionally re-assigns it by hand from `ClaimsPrincipal` before sending.
- A generated CRUD request (`CreateProductRequest`, `ChangePriceProductRequest`) can **never** carry an acting-user field — the generator forwards only `System.ComponentModel.DataAnnotations` attributes, not `[FromClaim]` (namespace `DKNet.AspCore.Extensions.ModelBinding`). `CreatedBy`/`CreatedOn` are instead stamped by `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook`, wired once in `Minimal.Api/Configs/ServiceConfigs.cs` (`AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()`) — it only fills in a blank `CreatedBy`, so the manual sample's constructor-set value (via `AggregateRoot(byUser)`) is left untouched.
- Mapster is global in `Minimal.AppServices/AppSetup.cs`; DTO generation uses `[GenerateDto(...)]` (example: `ProductDto`, one line, generates every audited property). The manual sample's `PurchaseOrderDto` is hand-written instead, exposing only the 5 fields it chooses.
- Lazy mapping result helpers are `DKNet.SlimBus.Extensions.LazyMapper`'s `ResultOf<T>`/`LazyMap<T>` (the template's former local copy under `AppServices/Extensions/LazyMapper` was removed — both samples use the package's version now).

## Build, run, and migration workflow
- SDK/framework are pinned centrally (`src/global.json`, `src/Directory.Packages.props`) and target `net10.0`.
- Core commands:
  - `dotnet restore src/DKNet.Templates.sln`
  - `dotnet build src/DKNet.Templates.sln -c Release`
  - `dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings --collect:"XPlat Code Coverage"`
- Local host options:
  - API only: `dotnet run --project src/ApiEndpoints/Minimal.Api`
  - Aspire host: `dotnet run --project src/ApiEndpoints/Minimal.AppHost`
- EF migrations scripts from `src/ApiEndpoints`: `./add-migration.sh <Name>` and `./remove-migration.sh <Name>`.

## Testing and quality constraints
- Tests currently live mainly under `src/ApiEndpoints/Minimal.App.Tests/` (Shouldly + xUnit patterns) and `src/ApiEndpoints/Minimal.App.BDDTests/` (Reqnroll + NUnit).
- `Minimal.App.Tests.csproj` disables analyzers for tests; production projects enforce strict warnings-as-errors from `Directory.Packages.props`.
- Coverage filters are defined in `src/coverage.runsettings`; avoid placing real logic in excluded paths (`bin/`, `obj/`, `*Test*.cs`).

## BDD Testing (Reqnroll + NUnit)
- BDD tests live in `src/ApiEndpoints/Minimal.App.BDDTests/`.
- `Support/BddApiFactory.cs` boots `WebApplicationFactory<Program>` once per test run using Reqnroll `[BeforeTestRun]` hook in `ApiHooks.cs`.
- In-memory EF Core + disabled migrations/AzureAppConfig — no external services required.
- Each scenario resets the DB in `[BeforeScenario(Order=0)]`; `HttpClient` and `ScenarioState` are injected into step defs via Reqnroll's BoDi `IObjectContainer`.
- Add new scenarios: create `.feature` under `Features/<Domain>/` and matching `[Binding]` step class under `Features/<Domain>/Steps/`.
- POST endpoints that opt into idempotency (the manual sample) require `X-Idempotency-Key: {Guid}` header — generate `Guid.NewGuid()` per request in `[When]` steps. The automated sample's generated create route has no such requirement.

## Project-specific gotchas
- `FeatureOptions` section name is `FeatureManagement`; keep JSON keys aligned with `Minimal.Share/Options/FeatureOptions.cs`.
- `appsettings*.json` currently contains mixed key names (`EnableServiceBusProcess`, `EnableAzureAppConfiguration`, `EnableAzureAppConfig`). Prefer the keys in `Minimal.Share/Options/FeatureOptions.cs` as source of truth when changing configuration.
- Prefer adding new feature slices by mirroring `ManualSample/PurchaseOrder` (hand-written) or `AutomatedSample/Product` (generator-driven) across Domains -> Infra -> AppServices -> Api, per `docs/samples/manual-vs-automated.md`.
- When adding repos/services in Infra, keep classes `sealed` and under `.Repos` or `.Services` namespaces so Scrutor scanning picks them up.
- A generated CRUD request's DataAnnotations rules (e.g. `[Range]` on `Price`) are **not enforced** when the entity is mapped through the generated `MapProductCrud()`-style route, because .NET 10's automatic validation source generator can't see through `DKNet.AspCore.Extensions`'s generic `Map*<TRequest,TDto>` wrapper. Don't assume a `[Range]`/`[Required]` on a `[CrudCreate]` parameter is enforced without checking whether the entity's endpoint is hand-mapped (enforced) or generator-mapped (not enforced) — see the "Request validation" row in `docs/samples/manual-vs-automated.md`.

## Reference docs (link-first)
- Comparison + worked samples: `docs/samples/manual-vs-automated.md`, `docs/samples/manual-purchase-orders/README.md`, `docs/samples/automated-products/README.md`
- Skill catalog for guided implementation: `.github/skills/README.md`
