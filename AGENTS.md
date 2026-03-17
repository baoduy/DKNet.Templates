# AGENTS.md

## Scope
- This repository template is centered on `src/SlimBus.ApiEndpoints` and the solution `src/DKNet.Templates.sln`.
- Prefer code-verified patterns in this guide over older README statements when they differ.

## Architecture at a glance
- API startup is in `src/SlimBus.ApiEndpoints/SlimBus.Api/Program.cs`: bind `FeatureOptions`, then `AddLogConfig` -> `AddAzureAppConfig` -> `AddFluentValidationConfig` -> `RunMigrationAsync` -> `AddAppConfig`.
- Middleware/service composition is orchestrated by `SlimBus.Api/Configs/AppConfig.cs` and `SlimBus.Api/Configs/ServiceConfigs.cs`.
- Layer boundaries are strict: `Api` -> `AppServices` -> `Domains`, with infra wiring from `SlimBus.Infra/Extensions/InfraSetup.cs`.
- `SlimBus.AppHost/AppHost.cs` is Aspire host orchestration (Redis + SQL Server + API project), not business logic.
- Persistence uses EF Core with auto model config and seeding (`UseAutoConfigModel`, `UseAutoDataSeeding`) in `InfraSetup.AddInfraServices`.

## Feature vertical slice pattern (copy this)
- Endpoint contract: implement `IEndpointConfig` in `SlimBus.Api/ApiEndpoints/*Endpoints.cs` (example: `ProfileV1Endpoint`).
- Endpoint mapping uses fluent helpers (`MapGetList`, `MapGetById`, `MapPost`, `MapPut`, `MapDelete`) from `SlimBus.Api/Configs/Endpoints/FluentEndpointMapperExtensions.cs`.
- Write workflow example (`CreateProfileCommand`): validate -> spec duplicate check (`SpecGetCustomerProfile`) -> map command to entity -> repo add -> domain event -> lazy result.
- Domain entity lives in `SlimBus.Domains/Features/<Feature>/Entities` (example: `CustomerProfile`) and keeps mutation in methods (`Update(...)`).
- EF mapping lives in `SlimBus.Infra/Features/<Feature>/Mappers` (example: `ProfileMapper`) and enforces indexes/length/schema.

## Message bus and events
- `AddServiceBus` in `SlimBus.Infra/Extensions/ServiceBusSetup.cs` always wires an in-memory child bus (`ImMemory`) for internal handlers.
- Azure Service Bus child bus (`AzureBus`) is added only when `ConnectionStrings:AzureBus` is non-empty.
- Domain events are published through `SlimBus.Infra/Services/EventPublisher.cs` using `IMessageBus.Publish(...)`.
- Example event flow: `ProfileCreatedEvent` in `AppServices/Profiles/V1/Events` + consumers in app/infra.

## Commands, mapping, and user context
- Commands derive from `BaseCommand` (`ByUser` is filled by `SetUserIdPropertyFilter`).
- Endpoint groups automatically add `SetUserIdPropertyFilter` in `EndpointConfig.CreateGroup`.
- Mapster is global in `SlimBus.AppServices/AppSetup.cs`; DTO generation uses `[GenerateDto(...)]` (example: `CustomerProfileDto`).
- Lazy mapping result helpers are in `AppServices/Extensions/LazyMapper` (`ResultOf<T>`, `LazyMap<T>`).

## Build, run, and migration workflow
- SDK/framework are pinned centrally (`src/global.json`, `src/Directory.Packages.props`) and target `net10.0`.
- Core commands:
  - `dotnet restore src/DKNet.Templates.sln`
  - `dotnet build src/DKNet.Templates.sln -c Release`
  - `dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings --collect:"XPlat Code Coverage"`
- Local host options:
  - API only: `dotnet run --project src/SlimBus.ApiEndpoints/SlimBus.Api`
  - Aspire host: `dotnet run --project src/SlimBus.ApiEndpoints/SlimBus.AppHost`
- EF migrations scripts from `src/SlimBus.ApiEndpoints`: `./add-migration.sh <Name>` and `./remove-migration.sh <Name>`.

## Testing and quality constraints
- Tests currently live mainly under `src/SlimBus.ApiEndpoints/SlimBus.App.Tests/Unit` (Shouldly + xUnit patterns).
- `SlimBus.App.Tests.csproj` disables analyzers for tests; production projects enforce strict warnings-as-errors from `Directory.Packages.props`.
- Coverage filters are defined in `src/coverage.runsettings`; avoid placing real logic in excluded paths (`bin/`, `obj/`, `*Test*.cs`).

## Project-specific gotchas
- `FeatureOptions` section name is `FeatureManagement`; keep JSON keys aligned with `SlimBus.Share/Options/FeatureOptions.cs`.
- Prefer adding new feature slices by mirroring existing `Profiles/V1` structure across Domains -> Infra -> AppServices -> Api.
- When adding repos/services in Infra, keep classes `sealed` and under `.Repos` or `.Services` namespaces so Scrutor scanning picks them up.
