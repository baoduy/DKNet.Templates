# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A NuGet solution template (`DKNet.Minimal.Template`) that scaffolds production-ready .NET 10 microservices using vertical slice DDD/CQRS. The `src/` directory contains the template source; the generated output mirrors that structure under the consumer's chosen namespace.

## Commands

```bash
# Build
dotnet restore src/DKNet.Templates.sln
dotnet build src/DKNet.Templates.sln -c Release

# Test (with coverage)
dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings --collect:"XPlat Code Coverage"

# Run (API only, no containers)
dotnet run --project src/Minimal.ApiEndpoints/Minimal.Api

# Run with Aspire (Redis + SQL Server via Docker)
dotnet run --project src/Minimal.ApiEndpoints/Minimal.AppHost

# EF Core migrations (run from src/Minimal.ApiEndpoints/)
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
Minimal.AppServices  → CQRS handlers, validators, DTOs
  ↓
Minimal.Domains      → entities, aggregate roots, repo interfaces
  ↑
Minimal.Infra        → EF Core, repos, event publisher, service bus
  (wires into Api via InfraSetup.cs)

Minimal.Share        → shared constants/options/base types (read by all layers)
Minimal.AppHost      → Aspire orchestration only, no business logic
```

`Program.cs` startup order: `FeatureOptions` bind → `AddLogConfig` → `AddAzureAppConfig` → `AddFluentValidationConfig` → `RunMigrationAsync` → `AddAppConfig`. Middleware/services are composed in `Minimal.Api/Configs/AppConfig.cs` and `ServiceConfigs.cs`.

### Feature vertical slice pattern

Mirror the existing `CustomerProfiles/V1` slice when adding a new feature:

| Layer | Location | What goes here |
|-------|----------|---------------|
| Domains | `Features/<Feature>/Entities/` | `AggregateRoot` subclass + owned types; mutation in methods |
| Infra | `Features/<Feature>/Mappers/` | `IEntityTypeConfiguration<T>` — indexes, lengths, schema |
| AppServices | `<Feature>/V1/Actions/` | `*Request`, `*CommandValidator`, `*CommandHandler` (sealed) |
| AppServices | `<Feature>/V1/Specs/` | Specification classes for duplicate/filter queries |
| AppServices | `<Feature>/V1/Events/` | Domain event handlers |
| Api | `ApiEndpoints/<Feature>V1Endpoint.cs` | Implements `IEndpointConfig` |

### Key wiring points

- **EF Core auto-discovery**: `UseAutoConfigModel` + `UseAutoDataSeeding` in `InfraSetup.AddInfraServices` — no manual `DbSet` declarations needed.
- **Service registration**: Scrutor scans Infra; keep concrete repos/services `sealed` under `.Repos` or `.Services` namespaces.
- **Endpoint fluent helpers**: `MapGetList`, `MapGetById`, `MapPost`, `MapPut`, `MapDelete` from `FluentEndpointMapperExtensions.cs`. POST gets an idempotency filter automatically.
- **`ByUser` auto-fill**: `SetUserIdPropertyFilter` is added by `EndpointConfig.CreateGroup` — commands inheriting `BaseCommand` get the user ID injected without extra code.
- **Mapster global config**: `Minimal.AppServices/AppSetup.cs`. DTOs use `[GenerateDto(...)]`. Lazy mapping after `SaveChanges` via `ResultOf<T>()` / `LazyMap<T>()`.
- **Message bus**: `AddServiceBus` in `ServiceBusSetup.cs` always wires an in-memory bus; Azure Service Bus is added only when `ConnectionStrings:AzureBus` is non-empty.

## Gotchas

- Config section for feature flags is `FeatureManagement` (not `Features`) — keep JSON keys aligned with `Minimal.Share/Options/FeatureOptions.cs`.
- Production projects enforce **warnings-as-errors** (all analyzers, `EnforceCodeStyleInBuild: true` in `Directory.Packages.props`). Test project disables this.
- All NuGet versions are centrally managed in `src/Directory.Packages.props` — do not add `Version=` attributes to individual `.csproj` files.
- SDK and framework are pinned to `net10.0` in `src/global.json`.
- See `AGENTS.md` for the full architecture reference including message bus and command/mapping details.
