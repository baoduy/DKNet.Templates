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
  --filter "FullyQualifiedName~PurchaseOrder"
dotnet test src/ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj \
  --filter "TestCategory=PurchaseOrder"

# Run (API only, no containers)
dotnet run --project src/ApiEndpoints/Minimal.Api

# Run with Aspire (Redis + PostgreSQL via Docker)
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
Minimal.AppHost      → Aspire orchestration only (Redis + PostgreSQL + Minimal.Api), no business logic
```

`Program.cs` startup order: bind `FeatureOptions` → `AddLogConfig` → `AddAzureAppConfig` → `AddFluentValidationConfig` → `RunMigrationAsync` → `AddOptions` → `AddAppConfig` → `AddContextualRequestPopulation` → `UseAppConfig(a => a.UseEndpointConfigs())`. Middleware/services are composed in `Minimal.Api/Configs/AppConfig.cs` and `ServiceConfigs.cs`.

### Feature vertical slice pattern — two shapes, pick one

The template ships two complete worked examples of the same shape of feature — entity, event,
event handler, CRUD, queries, endpoint — built two different ways. Read
[`docs/samples/manual-vs-automated.md`](docs/samples/manual-vs-automated.md) before copying either;
it states exactly what each layer costs or gives up.

**Hand-written — mirror `ManualSample/PurchaseOrder`:**

| Layer       | Location                                        | What goes here                                                          |
|-------------|-------------------------------------------------|-------------------------------------------------------------------------|
| Domains     | `Features/<Feature>/Entities/`                  | `AggregateRoot` subclass; mutation in methods; raises its own events via `AddEvent(...)` |
| Infra       | `Features/<Feature>/Mappers/`                   | `IEntityTypeConfiguration<T>` — indexes, lengths, schema                |
| Infra       | `Features/<Feature>/StaticData/`                | Seed data discovered by `UseAutoDataSeeding`                            |
| AppServices | `<Feature>/V1/Actions/`                         | `*Request` (`[FromClaim]` for the acting user), `*CommandValidator` (FluentValidation), `*CommandHandler` (sealed) |
| AppServices | `<Feature>/V1/Specs/`                           | Specification classes for duplicate/filter queries                      |
| AppServices | `<Feature>/V1/Events/`                          | Domain event handlers                                                   |
| AppServices | `<Feature>/V1/<Feature>Dto.cs`                  | Hand-written DTO record — exposes exactly the fields you write into it  |
| Api         | `ApiEndpoints/<Feature>V1Endpoint.cs`           | Implements `IEndpointConfig`; every route is a literal `group.MapPost/MapGet/MapPut/MapDelete(...)` call |

**Generator-driven — mirror `AutomatedSample/Product`:**

| Layer       | Location                                        | What goes here                                                          |
|-------------|-------------------------------------------------|-------------------------------------------------------------------------|
| Domains     | `Features/<Feature>/Entities/`                  | `AggregateRoot` subclass; class-level `[RaisesEvent(...)]`; `[CrudCreate]` ctor; `[CrudUpdate]` method(s) |
| Infra       | `Features/<Feature>/Mappers/`                   | `IEntityTypeConfiguration<T>` — still hand-written, no generator produces this |
| AppServices | `<Feature>/V1/<Feature>Dto.cs`                  | One `[GenerateDto(typeof(Entity))] public sealed partial record <Feature>Dto;` |
| AppServices | `<Feature>/V1/Events/`                          | Hand-written consumer for a declared event — the generator raises, it does not consume |
| Api         | `ApiEndpoints/<Feature>V1Endpoint.cs`           | Implements `IEndpointConfig`; calls the generated `Map<Entity>Crud()` extension, nothing hand-mapped |
| *(generated)* | `obj/Generated/.../<Entity>CrudRequests.g.cs`, `...Handlers.g.cs`, `...Endpoints.g.cs` | Requests, handlers, and route registration — not committed, inspect after a build |

Note: the domain entity folder and the `AppServices` slice use the same feature folder name in both
samples (`ManualSample`, `AutomatedSample`) — the two namespaces don't have to match in general.

### Key wiring points

- **EF Core auto-discovery**: `UseAutoConfigModel` + `UseAutoDataSeeding` in **both** `InfraSetup.AddInfraServices` (DI host path) and `InfraMigration.MigrateDb` (startup-migration path) — no manual `DbSet` declarations needed. Mappers (`IEntityTypeConfiguration<T>`) and seeders are picked up by assembly scan — a seeder inherits the **base class** `DataSeedingConfiguration<T>` (see `PurchaseOrderStaticData`), not an `IDataSeedingConfiguration<T>` interface. Wiring seeding into only one of the two paths is a real bug this template hit once already (`PurchaseOrderStaticData` didn't appear over HTTP until `MigrateDb` got the same `.UseAutoDataSeeding(...)` call).
- **Service registration**: Scrutor scans Infra; keep concrete repos/services `sealed` and place them under `.Repos` or `.Services` namespaces so the convention scan picks them up.
- **Endpoint mapping helpers** (`DKNet.AspCore.Extensions`, not local to this template): hand-mapped routes use the raw minimal-API surface directly (see `PurchaseOrderV1Endpoint`); generator-driven routes call the package's generic `MapGetList<TEntity,TKey,TDto>`/`MapGetById`/`MapPost<TRequest,TDto>`/`MapPutById`/`MapDeleteById` (see the generated `ProductCrudEndpointExtensions.MapProductCrud()`). POST does NOT auto-add idempotency either way — call `.RequiredIdempotentKey()` explicitly (see `PurchaseOrderV1Endpoint`'s create route); clients then send `X-Idempotency-Key: {Guid}`. The automated sample's generated create route has no such call — it accepts a replayed request as a fresh create.
- **`ByUser` / acting-user auto-fill**: `AddContextualRequestPopulation` (wired in `Program.cs`) populates any `[FromClaim(...)]`-decorated request property before validation and before the handler runs; it only falls back to `SharedConsts.SystemAccount` when `RequireAuthorization` is off. A **generated** CRUD request can never carry a `[FromClaim]` property — the generator forwards only `System.ComponentModel.DataAnnotations` attributes onto generated properties — so the automated sample's acting-user stamping goes through `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook` instead, wired once in `ServiceConfigs.AddAllAppServices` (`.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()`) and applying to every entity on `CoreDbContext`, not just `Product`.
- **Mapster global config**: `Minimal.AppServices/AppSetup.cs`. DTOs use `[GenerateDto(...)]` (generates every audited property by default — `Exclude`/`Include` to narrow) or a hand-written record (full control, see `PurchaseOrderDto`). Lazy mapping after `SaveChanges` via `mapper.ResultOf<T>(entity)` / `mapper.LazyMap<T>()` from `DKNet.SlimBus.Extensions.LazyMapper` (the template's former local copy under `AppServices/Extensions/LazyMapper` was removed — use the package's).
- **Message bus**: `AddServiceBus` in `ServiceBusSetup.cs` always wires an in-memory child bus (`ImMemory`) for internal handlers. Azure Service Bus child bus (`AzureBus`) is added only when `ConnectionStrings:AzureBus` is non-empty — that's where `Product`'s `Produce<ProductCreatedEvent>`/`Consume<ProductCreatedEvent>` topology lives. Domain events are published by `Minimal.Infra/Services/EventPublisher.cs`, whether raised by hand (`AddEvent`, see `PurchaseOrder`) or declared (`[RaisesEvent]`, see `Product` — raised by DKNet's EF Core save hook, not application code).
- **Declared-event naming convention**: `[RaisesEvent(EventOperations.X, nameof(Prop1), ...)]` on an entity composes the generated payload record's name as `<Entity><NarrowingProps><Operation>Event` — e.g. `[RaisesEvent(EventOperations.Updated, nameof(Price))]` on `Product` generates `ProductPriceUpdatedEvent`, **not** `ProductUpdatedEvent`. Verify the composed name against the compiled assembly (`strings bin/**/Minimal.Domains.dll | grep <Entity>`) before wiring a consumer to it — the record has no hand-written source file to read.
- **Generated-route validation gap**: a `[Range]`/`[Required]`/etc. on a `[CrudCreate]`/`[CrudUpdate]` parameter *is* forwarded onto the generated request property, but is only *enforced* when the entity's create/update route is a **literal** `Map*(string, Delegate)` call the .NET 10 validation source generator can see in this repo's own source (true for hand-mapped routes like `PurchaseOrderV1Endpoint`; false for anything mapped through `DKNet.AspCore.Extensions`'s generic `Map*<TRequest,TDto>` wrapper, including every generated CRUD route). Don't assume a DataAnnotations attribute on a generated request is enforced without checking which mapping style its endpoint uses.

## Testing

- **Unit/integration** (`Minimal.App.Tests`, xUnit + Shouldly): folders are `Architecture/` (NetArchTest rules — enforce layer boundaries), `Data/`, `Extensions/`, `Integration/`, `Unit/`. Test project disables analyzers, so production warnings-as-errors do not apply here.
- **BDD** (`Minimal.App.BDDTests`, Reqnroll + NUnit): `Support/BddApiFactory.cs` boots `WebApplicationFactory<Program>` once per test run via `[BeforeTestRun]` in `ApiHooks.cs`. Uses in-memory EF Core with migrations and Azure App Config disabled. Each scenario resets the DB in `[BeforeScenario(Order=0)]`; `HttpClient` and `ScenarioState` are injected via Reqnroll's BoDi. New scenarios go under `Features/<Domain>/*.feature` with matching `[Binding]` step class in `Features/<Domain>/Steps/`.
- **POST in BDD**: generate a fresh `Guid.NewGuid()` for the `X-Idempotency-Key` header in each `[When]` step.
- **Coverage filter**: `src/coverage.runsettings` includes `[DKNet*]` + `[Minimal*]` and excludes `*Tests`, `bin/`, `obj/`, `GlobalUsings.cs`. Don't put real logic in excluded paths.

### Test layering — where a test belongs

Keep the two suites at different levels; do not duplicate the same behavior in both.

- **xUnit owns three things** and BDD must not re-cover them:
  1. **Architecture/convention** — NetArchTest + reflection + csproj/source text scans (`Architecture/*`). Cannot be expressed as HTTP scenarios; never port to BDD.
  2. **Pure functional** — entity methods, validators, mappers, extensions, spec filters (`Unit/*`, plus `Test_*_Mapping`). No host, no DB, no HTTP. This *is* the functional layer; keep it here.
  3. **Result-level integration** — handler failures asserted on the `Result` object (not-found, empty-id, "already existed") and EF model/schema/migration shape (`Architecture/MigrationSchemaTests`, `Integration/**` failure cases). BDD's HTTP-status/response-text assertions are coarser and would lose this intent (Rule 9).
- **BDD owns user-facing HTTP behavior**: request→status→response-body scenarios, and domain-event side effects observed via log capture (e.g. the `ProductCreatedEventHandler`/`ProductCreatedNotificationHandler` log lines the automated sample emits). When a behavior is exercised end-to-end over HTTP, BDD is the stronger home — delete the xUnit integration duplicate.
- **Schema/model assertions belong in xUnit, never BDD.** (`MigrationVerification.feature` was removed for this reason; `MigrationSchemaTests` already covers it.)
- **Still owed** (BDD gaps): static-seeding and external-broker scenarios — see `docs/samples/manual-vs-automated.md` for the current gap list; dev-qc extends coverage at Verify, not at Build.

## Gotchas

- Path of truth is `src/ApiEndpoints/` (not `src/Minimal.ApiEndpoints/` — the inner project folders are prefixed `Minimal.*`).
- `FeatureOptions` config section is named `FeatureManagement` (not `Features`). Every JSON key under it matches a `FeatureOptions` property name one-for-one — add a property, add the key with the same spelling. `Get<FeatureOptions>()` ignores unknown keys, so a misspelled key no-ops silently instead of failing; keep `Minimal.Share/Options/FeatureOptions.cs` and `appsettings*.json` in step. Flag table with shipped values: `docs/template-features.md`.
- Production projects enforce **warnings-as-errors** via `Directory.Packages.props` (`EnforceCodeStyleInBuild=true`, `AnalysisMode=All`, plus an explicit `WarningsAsErrors` list including CA/CS/IDE/MA/S rules). Test projects opt out.
- All NuGet versions are centrally managed in `src/Directory.Packages.props` — do not add `Version=` attributes to individual `.csproj` files.
- SDK and target framework are pinned to `net10.0` in `src/global.json` (`rollForward: latestMajor`, `allowPrerelease: false`).
- `add-migration.sh` / `remove-migration.sh` always target `CoreDbContext` in `Minimal.Infra`. Run them from `src/ApiEndpoints/`, not the repo root.

## Reference docs

- `AGENTS.md` — full architecture reference (layer rules, message bus, command/mapping details).
- `docs/samples/manual-vs-automated.md` — layer-by-layer comparison of the two worked samples, including what the generator-driven sample gives up.
- `docs/samples/manual-purchase-orders/`, `docs/samples/automated-products/` — thin per-sample READMEs (what each demonstrates, routes, how to delete it).
- `.github/skills/` — guided skill catalog (domain-modeling, crud-operations, api-endpoints, BDD, etc.). See `.github/skills/CATALOG.md`.
- `specs/` — Spec-Kit feature specs; workflow docs in `SPEC_KIT.md`.

## Feature lifecycle (plugin)

A business feature is one vertical slice, addressable by its `<Feature>` folder name, which appears
literally in ten fixed roots across six projects. `.claude/skills/dknet-feature-lifecycle/SKILL.md` is
the authority on that footprint, the out-of-folder touchpoints a delete must also clean
(`DomainSchemas`, `ServiceBusSetup` Produce/Consume, `FeatureOptions` + `FeatureManagement` JSON,
migrations, docs links), and the migration rule on removal.

Every scaffolding command takes `mode=manual|auto`, threaded end-to-end by the orchestrator. The mode
is not a style preference — it changes which files exist, whether validation is enforced, whether
create is idempotent, and how the acting user is attributed. Commands that omit `mode=` detect it by
grepping the entity for `[CrudCreate]`.

| Command | Purpose |
|---|---|
| `/dknet-feature <Feature> <Entity> [mode=…] [props…]` | Add a slice end-to-end (plan → domain → CRUD → endpoint → tests → BDD → docs) |
| `/dknet-feature-remove <Feature>` | Retire a slice end-to-end, including touchpoints and a drop migration |
| `/dknet-entity`, `/dknet-crud`, `/dknet-endpoint`, `/dknet-unit-tests`, `/dknet-bdd-test`, `/dknet-docs` | Individual phases, same `mode=` contract |

`ls src/ApiEndpoints/Minimal.Domains/Features/` enumerates the features that exist — there is
deliberately no registry file to drift out of sync.

New `.claude/skills/<x>` must be mirrored byte-identically to `.github/skills/<x>` and added to
`CORE_SKILLS` in `validate-plugin.sh`; check 3 enforces the pair.

## The plugin ships to consumers — write guidance for THEIR tree, not this one

`DKNet.Minimal.Template.nuspec` packs `.claude/{skills,commands,agents}`, `.claude-plugin/`,
`.github/`, `docs/` and `AGENTS.md` into the template. A consumer running
`dotnet new dknet-minimal -n Contoso` receives this plugin inside their own solution. Guidance files
are therefore product, and their paths must be correct **in the generated tree**:

- **No `src/` prefix.** The pack's content root is `src/`, so the generated layout is
  `ApiEndpoints/<App>.Domains/…` with the solution at the root. Write every path relative to the
  **solution root** — correct in both trees, since this repo's solution root is `src/`.
- **Never name the solution file.** `dotnet build -c Release` and
  `dotnet test --settings coverage.runsettings` work unqualified from the solution root; the `.sln` is
  renamed per consumer (`Contoso.sln`).
- **No `*.sh` reaches consumers** — the nuspec excludes them, so `add-migration.sh` does not exist
  there. Inline the real command:
  `dotnet ef migrations add <Name> -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj` from
  `ApiEndpoints/`.
- **Write `Minimal.*`, never a sample name.** `sourceName` is `Minimal`, so `Minimal.Infra` in a
  markdown file becomes `Contoso.Infra` for the consumer. A hardcoded example name like `Acme.Infra`
  does **not** get rewritten and ships wrong to everyone.
- **Only linked docs that are packed resolve.** `docs/**` is packed (excluding `docs/superpowers/`).

Verify after touching shipped guidance — regenerating is the only real test:

```bash
cd src && dotnet pack DKNet.Minimal.Template.csproj -c Release -o /tmp/pkg
dotnet new install /tmp/pkg/DKNet.Minimal.Template.1.0.0.nupkg --force
cd /tmp && dotnet new dknet-minimal -n Contoso && cd Contoso && dotnet build -c Release
grep -rn 'src/ApiEndpoints\|add-migration.sh' .claude/    # expect no hits
dotnet new uninstall DKNet.Minimal.Template               # clean up after
```

`/dknet-scaffold`'s skill (`dknet-scaffold`) is the consumer's entry point: install, generate, the six
template parameters, first run, and deleting the two shipped samples.
