# Copilot Project Instructions

These guidelines help generate consistent, safe, high-quality code for the **DKNet.Minimal.Template** (.NET 10, vertical-slice DDD/CQRS).

## What this repo is

A NuGet solution template that scaffolds production-ready .NET 10 microservices. Everything under `src/ApiEndpoints/` is the template source; consumers run `dotnet new dknet-minimal -n <Name>` and the generated output mirrors that structure under their chosen namespace (`Minimal.*` → `<Name>.*`).

## Solution Architecture (High Level)

Projects (prefix `Minimal.*` in this template, `<Name>.*` in generated solutions):

- **Api**: entry point, endpoints, auth, OpenAPI. No business logic.
- **AppServices**: application/use-case layer — CQRS handlers (`Features/<Feature>/V1/Actions`), validators, DTOs, domain event handlers, specs.
- **Domains**: entities, aggregate roots, repo interfaces, domain events. No EF Core types leak in.
- **Infra**: EF Core (`CoreDbContext`), entity mappers, static data seeding, event publisher, service bus wiring.
- **Share**: shared constants/options/base types, read by all layers.
- **AppHost**: .NET Aspire orchestration only (Redis + PostgreSQL + Api project), no business logic.
- **App.Tests** / **App.BDDTests**: xUnit + Shouldly unit/integration tests; Reqnroll + NUnit BDD scenarios.

Layer boundaries are strict, no skipping: `Api → AppServices → Domains ← Infra` (Infra wires into Api via `InfraSetup.AddInfraServices`).

## Core Patterns

1. **Feature vertical slice** — mirror the existing `CustomerProfiles/V1` slice: domain entity in `Domains/Features/<Feature>/Entities/`, EF mapper in `Infra/Features/<Feature>/Mappers/` (`IEntityTypeConfiguration<T>` via `DefaultEntityTypeConfiguration<T>`), CQRS actions in `AppServices/<Feature>/V1/Actions/`, endpoint in `Api/ApiEndpoints/<Feature>V1Endpoint.cs` (`IEndpointConfig`).
2. **Validation** — FluentValidation validators alongside their request record; fail fast, use `.When(...)` for conditional rules.
3. **Handlers** — sealed `*CommandHandler`/`*QueryHandler` classes; async I/O only, never block on `.Result`/`.Wait()`.
4. **Mapping** — Mapster, global config in `AppServices/AppSetup.cs`. DTOs use `[GenerateDto(...)]`; `[MapsFrom(typeof(Entity))]` keeps request/response records aligned. Lazy mapping via `mapper.ResultOf<T>(entity)` / `mapper.LazyMap<T>()`.
5. **EF Core auto-discovery** — `UseAutoConfigModel` + `UseAutoDataSeeding` in `InfraSetup.AddInfraServices`; no manual `DbSet` declarations. Mappers and seed classes must be `internal sealed` to be picked up by assembly scan.
6. **Repositories** — Scrutor auto-registers classes that are `sealed` and live under a `.Repos` or `.Services` namespace.
7. **Domain events** — raised via aggregate methods, published by `Infra/Services/EventPublisher.cs` through SlimMessageBus. An in-memory bus is always wired; Azure Service Bus is added only when `ConnectionStrings:AzureBus` is configured.
8. **`ByUser` auto-fill** — commands inheriting `BaseCommand` get the user ID injected via `SetUserIdPropertyFilter`, added by `EndpointConfig.CreateGroup`.

## Naming & File Organization

- One public request/record per file unless small + variant.
- Endpoint fluent helpers: `MapGetList`, `MapGetById`, `MapPost`, `MapPut`, `MapDelete` from `FluentEndpointMapperExtensions.cs`. POST does NOT auto-add idempotency — call `.AddIdempotencyFilter()` explicitly; clients then send `X-Idempotency-Key: {Guid}`.
- DTOs generated with `[GenerateDto(typeof(Entity), Exclude = [...])]`; hand-write request/response records only when the contract diverges from the entity shape.

## EF Core / Migrations

- Migrations live in `Infra/Migrations`, targeting `CoreDbContext`. Use the provided scripts from `src/ApiEndpoints/`:
  - `./add-migration.sh <Name>`
  - `./remove-migration.sh <Name>`
- Never hand-edit designer migration code except for safe seed/data adjustments.
- If adding a new entity: domain model in `Domains` (no EF attributes) → `IEntityTypeConfiguration<T>` in `Infra/Features/<Feature>/Mappers/` → optional static seed data → migration via the shell helper.

## Validation & DTO Checklist

- For optional strings: `.When(x => !string.IsNullOrEmpty(x.Prop))` + `.Must(...).MaximumLength(n)`.
- For enums: `.IsInEnum()` and exclude sentinel/`Unknown` values when one exists.
- String properties always get `HasMaxLength()` in EF mappers.

## Testing Guidance

- Unit/integration tests (`Minimal.App.Tests`): xUnit + Shouldly, Arrange/Act/Assert, `MethodName_StateUnderTest_ExpectedOutcome` naming. Test projects disable analyzers.
- BDD (`Minimal.App.BDDTests`): Reqnroll + NUnit, `.feature` files under `Features/<Domain>/` with matching `[Binding]` step classes. Each scenario resets the DB in `[BeforeScenario(Order=0)]`. POST steps need a fresh `Guid.NewGuid()` for `X-Idempotency-Key`.
- Production projects enforce warnings-as-errors (`Directory.Packages.props`); test projects opt out.

## Performance & Safety

- Avoid N+1 by projecting queries (`ProjectToType<T>`) before enumeration.
- Paginate any collection that could exceed ~100 items.
- Keep handlers focused; extract pure logic into private methods or services.

## Security & Compliance

- No secrets in source. Use configuration + environment variables + Azure App Configuration.
- Validate all externally provided identifiers at the boundary (handler/validator), not deep in domain code.

## Disallowed / Avoid

- Business logic in API endpoints or `Program.cs`.
- Exposing EF entities or `CoreDbContext` outside `Infra`.
- Synchronous I/O or blocking calls.
- Adding `Version=` attributes to individual `.csproj` files — NuGet versions are centrally managed in `src/Directory.Packages.props`.

## When Unsure

Search for the closest existing feature (start with `CustomerProfiles/V1`) and replicate its style with minimal divergence. See `AGENTS.md` and `CLAUDE.md` for the full architecture reference, and `.github/skills/` (`dknet-project-structure`, `dknet-ddd-principles`, and the other `dknet-*` skills) for step-by-step guidance.

---
End of Copilot Instructions.
