# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A NuGet solution template (`DKNet.Minimal.Template`) that scaffolds production-ready .NET 10 microservices using vertical slice DDD/CQRS. Everything under `src/ApiEndpoints/` is the template source; consumers run `dotnet new dknet-minimal -n <Name>` and the generated output mirrors that structure under their chosen namespace (`Minimal.*` → `<Name>.*`).

## Commands

```bash
# Build
dotnet restore src/DKNet.Templates.sln
dotnet build src/DKNet.Templates.sln -c Release

# Test (with coverage)
dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings --collect:"XPlat Code Coverage"

# Run a single test by fully-qualified name (xUnit) or display name (NUnit/Reqnroll)
dotnet test src/ApiEndpoints/Minimal.App.Tests/Minimal.App.Tests.csproj \
  --filter "FullyQualifiedName~CustomerProfile"
dotnet test src/ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj \
  --filter "TestCategory=CustomerProfile"

# Run (API only, no containers)
dotnet run --project src/ApiEndpoints/Minimal.Api

# Run with Aspire (Redis + SQL Server via Docker)
dotnet run --project src/ApiEndpoints/Minimal.AppHost

# EF Core migrations (run from src/ApiEndpoints/ — scripts target CoreDbContext in Minimal.Infra)
./add-migration.sh <MigrationName>
./remove-migration.sh <MigrationName>

# Pack template as NuGet
cd src && dotnet pack DKNet.Minimal.Template.csproj -c Release -o ./nupkgs
```

## Architecture

### Layer boundaries (strict, no skipping)

```
Minimal.Api          → entry point, endpoints, auth, OpenAPI
  ↓
Minimal.AppServices  → CQRS handlers, validators, DTOs, domain event handlers
  ↓
Minimal.Domains      → entities, aggregate roots, repo interfaces
  ↑
Minimal.Infra        → EF Core (CoreDbContext), repos, event publisher, service bus
  (wires into Api via InfraSetup.AddInfraServices)

Minimal.Share        → shared constants/options/base types (read by all layers)
Minimal.AppHost      → Aspire orchestration only (Redis + SqlServer + Minimal.Api), no business logic
```

`Program.cs` startup order: bind `FeatureOptions` → `AddLogConfig` → `AddAzureAppConfig` → `AddFluentValidationConfig` → `RunMigrationAsync` → `AddOptions` → `AddAppConfig` → `UseAppConfig(a => a.UseEndpointConfigs())`. Middleware/services are composed in `Minimal.Api/Configs/AppConfig.cs` and `ServiceConfigs.cs`.

### Feature vertical slice pattern

Mirror the existing `CustomerProfiles/V1` slice when adding a new feature:

| Layer       | Location                                        | What goes here                                                          |
|-------------|-------------------------------------------------|-------------------------------------------------------------------------|
| Domains     | `Features/<Feature>/Entities/`                  | `AggregateRoot` subclass + owned types; mutation in methods             |
| Infra       | `Features/<Feature>/Mappers/`                   | `IEntityTypeConfiguration<T>` — indexes, lengths, schema                |
| Infra       | `Features/<Feature>/StaticData/`                | Seed data discovered by `UseAutoDataSeeding`                            |
| AppServices | `<Feature>/V1/Actions/`                         | `*Request` (`[MapsFrom]`), `*CommandValidator`, `*CommandHandler` (sealed) |
| AppServices | `<Feature>/V1/Specs/`                           | Specification classes for duplicate/filter queries                      |
| AppServices | `<Feature>/V1/Events/`                          | Domain event handlers                                                   |
| AppServices | `<Feature>/V1/<Feature>Dto.cs`                  | `[GenerateDto]` partial DTO                                             |
| Api         | `ApiEndpoints/<Feature>V1Endpoint.cs`           | Implements `IEndpointConfig`                                            |

Note: domain entity folder is `Features/Profiles/` (singular feature), but the `AppServices` slice uses `CustomerProfiles/V1/` — the two namespaces don't have to match.

### Key wiring points

- **EF Core auto-discovery**: `UseAutoConfigModel` + `UseAutoDataSeeding` in `InfraSetup.AddInfraServices` — no manual `DbSet` declarations needed. Mappers (`IEntityTypeConfiguration<T>`) and `IDataSeedingConfiguration<T>` classes are picked up by assembly scan.
- **Service registration**: Scrutor scans Infra; keep concrete repos/services `sealed` and place them under `.Repos` or `.Services` namespaces so the convention scan picks them up.
- **Endpoint fluent helpers**: `MapGetList`, `MapGetById`, `MapPost`, `MapPut`, `MapDelete` from `FluentEndpointMapperExtensions.cs`. POST does NOT auto-add idempotency — call `.AddIdempotencyFilter()` explicitly (see `CustomerProfileV1Endpoint`); clients must then send `X-Idempotency-Key: {Guid}`.
- **`ByUser` auto-fill**: `SetUserIdPropertyFilter` is added by `EndpointConfig.CreateGroup` — commands inheriting `BaseCommand` (or `RequestBase`) get the user ID injected without extra code.
- **Mapster global config**: `Minimal.AppServices/AppSetup.cs`. DTOs use `[GenerateDto(...)]`. Lazy mapping after `SaveChanges` via `mapper.ResultOf<T>(entity)` / `mapper.LazyMap<T>()` from `AppServices/Extensions/LazyMapper`.
- **Message bus**: `AddServiceBus` in `ServiceBusSetup.cs` always wires an in-memory child bus (`ImMemory`) for internal handlers. Azure Service Bus child bus (`AzureBus`) is added only when `ConnectionStrings:AzureBus` is non-empty. Domain events are published by `Minimal.Infra/Services/EventPublisher.cs`.

## Testing

- **Unit/integration** (`Minimal.App.Tests`, xUnit + Shouldly): folders are `Architecture/` (NetArchTest rules — enforce layer boundaries), `Data/`, `Extensions/`, `Integration/`, `Unit/`. Test project disables analyzers, so production warnings-as-errors do not apply here.
- **BDD** (`Minimal.App.BDDTests`, Reqnroll + NUnit): `Support/BddApiFactory.cs` boots `WebApplicationFactory<Program>` once per test run via `[BeforeTestRun]` in `ApiHooks.cs`. Uses in-memory EF Core with migrations and Azure App Config disabled. Each scenario resets the DB in `[BeforeScenario(Order=0)]`; `HttpClient` and `ScenarioState` are injected via Reqnroll's BoDi. New scenarios go under `Features/<Domain>/*.feature` with matching `[Binding]` step class in `Features/<Domain>/Steps/`.
- **POST in BDD**: generate a fresh `Guid.NewGuid()` for the `X-Idempotency-Key` header in each `[When]` step.
- **Coverage filter**: `src/coverage.runsettings` includes `[DKNet*]` + `[Minimal*]` and excludes `*Tests`, `bin/`, `obj/`, `GlobalUsings.cs`. Don't put real logic in excluded paths.

## Gotchas

- Path of truth is `src/ApiEndpoints/` (not `src/Minimal.ApiEndpoints/` — the inner project folders are prefixed `Minimal.*`).
- `FeatureOptions` config section is named `FeatureManagement` (not `Features`). `appsettings*.json` historically contains mixed key names (`EnableServiceBusProcess`, `EnableAzureAppConfiguration`, `EnableAzureAppConfig`); treat `Minimal.Share/Options/FeatureOptions.cs` as the source of truth.
- Production projects enforce **warnings-as-errors** via `Directory.Packages.props` (`EnforceCodeStyleInBuild=true`, `AnalysisMode=All`, plus an explicit `WarningsAsErrors` list including CA/CS/IDE/MA/S rules). Test projects opt out.
- All NuGet versions are centrally managed in `src/Directory.Packages.props` — do not add `Version=` attributes to individual `.csproj` files.
- SDK and target framework are pinned to `net10.0` in `src/global.json` (`rollForward: latestMajor`, `allowPrerelease: false`).
- `add-migration.sh` / `remove-migration.sh` always target `CoreDbContext` in `Minimal.Infra`. Run them from `src/ApiEndpoints/`, not the repo root.

## Reference docs

- `AGENTS.md` — full architecture reference (layer rules, message bus, command/mapping details).
- `docs/features/customer-profiles/` — feature exemplar with architecture + API reference.
- `.github/skills/` — guided skill catalog (domain-modeling, crud-operations, api-endpoints, BDD, etc.). See `.github/skills/CATALOG.md`.
- `specs/` — Spec-Kit feature specs; workflow docs in `SPEC_KIT.md`.
