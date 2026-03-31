# AGENTS.md

## Scope
- This repository template is centered on `src/ApiEndpoints` and the solution `src/DKNet.Templates.sln`.
- Prefer code-verified patterns in this guide over older README statements when they differ.

## Architecture at a glance
- API startup is in `src/ApiEndpoints/Minimal.Api/Program.cs`: bind `FeatureOptions`, then `AddLogConfig` -> `AddAzureAppConfig` -> `AddFluentValidationConfig` -> `RunMigrationAsync` -> `AddAppConfig`.
- Middleware/service composition is orchestrated by `Minimal.Api/Configs/AppConfig.cs` and `Minimal.Api/Configs/ServiceConfigs.cs`.
- Layer boundaries are strict: `Api` -> `AppServices` -> `Domains`, with infra wiring from `Minimal.Infra/Extensions/InfraSetup.cs`.
- `Minimal.AppHost/AppHost.cs` is Aspire host orchestration (Redis + SQL Server + API project), not business logic.
- Persistence uses EF Core with auto model config and seeding (`UseAutoConfigModel`, `UseAutoDataSeeding`) in `InfraSetup.AddInfraServices`.

## Feature vertical slice pattern (copy this)
- Endpoint contract: implement `IEndpointConfig` in `Minimal.Api/ApiEndpoints/*Endpoints.cs` (example: `ProfileV1Endpoint`).
- Endpoint mapping uses fluent helpers (`MapGetList`, `MapGetById`, `MapPost`, `MapPut`, `MapDelete`) from `Minimal.Api/Configs/Endpoints/FluentEndpointMapperExtensions.cs`.
- Write workflow example (`CreateProfileCommand`): validate -> spec duplicate check (`SpecGetCustomerProfile`) -> map command to entity -> repo add -> domain event -> lazy result.
- Domain entity lives in `Minimal.Domains/Features/<Feature>/Entities` (example: `CustomerProfile`) and keeps mutation in methods (`Update(...)`).
- EF mapping lives in `Minimal.Infra/Features/<Feature>/Mappers` (example: `ProfileMapper`) and enforces indexes/length/schema.

## Message bus and events
- `AddServiceBus` in `Minimal.Infra/Extensions/ServiceBusSetup.cs` always wires an in-memory child bus (`ImMemory`) for internal handlers.
- Azure Service Bus child bus (`AzureBus`) is added only when `ConnectionStrings:AzureBus` is non-empty.
- Domain events are published through `Minimal.Infra/Services/EventPublisher.cs` using `IMessageBus.Publish(...)`.
- Example event flow: `ProfileCreatedEvent` in `AppServices/Profiles/V1/Events` + consumers in app/infra.

## Commands, mapping, and user context
- Commands derive from `BaseCommand` (`ByUser` is filled by `SetUserIdPropertyFilter`).
- Endpoint groups automatically add `SetUserIdPropertyFilter` in `EndpointConfig.CreateGroup`.
- Mapster is global in `Minimal.AppServices/AppSetup.cs`; DTO generation uses `[GenerateDto(...)]` (example: `CustomerProfileDto`).
- Lazy mapping result helpers are in `AppServices/Extensions/LazyMapper` (`ResultOf<T>`, `LazyMap<T>`).

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
- POST endpoints require `X-Idempotency-Key: {Guid}` header — generate `Guid.NewGuid()` per request in `[When]` steps.

## Project-specific gotchas
- `FeatureOptions` section name is `FeatureManagement`; keep JSON keys aligned with `Minimal.Share/Options/FeatureOptions.cs`.
- `appsettings*.json` currently contains mixed key names (`EnableServiceBusProcess`, `EnableAzureAppConfiguration`, `EnableAzureAppConfig`). Prefer the keys in `Minimal.Share/Options/FeatureOptions.cs` as source of truth when changing configuration.
- Prefer adding new feature slices by mirroring existing `Profiles/V1` structure across Domains -> Infra -> AppServices -> Api.
- When adding repos/services in Infra, keep classes `sealed` and under `.Repos` or `.Services` namespaces so Scrutor scanning picks them up.

## Reference docs (link-first)
- Feature exemplar (full docs set): `docs/features/customer-profiles/README.md`
- API/docs architecture details: `docs/features/customer-profiles/architecture.md`, `docs/features/customer-profiles/api-reference.md`
- BDD workflow and expected assertions: `specs/004-bdd-reqnroll-nunit-setup/quickstart.md`, `specs/004-bdd-reqnroll-nunit-setup/contracts/customer-profile-bdd.md`
- Skill catalog for guided implementation: `.github/skills/README.md`
