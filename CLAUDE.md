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
| Api         | `ApiEndpoints/<Feature>V1Endpoint.cs`           | Implements `IEndpointConfig`; calls the generated `Map<Entity>Crud()` extension first, with any hand-written business routes mapped below it |
| *(generated)* | `obj/Generated/.../<Entity>CrudRequests.g.cs`, `...Handlers.g.cs`, `...Endpoints.g.cs` | Requests, handlers, and route registration — not committed, inspect after a build |

Note: the domain entity folder and the `AppServices` slice use the same feature folder name in both
samples (`ManualSample`, `AutomatedSample`) — the two namespaces don't have to match in general.

### Key wiring points

- **EF Core auto-discovery**: `UseAutoConfigModel` + `UseAutoDataSeeding` in **both** `InfraSetup.AddInfraServices` (DI host path) and `InfraMigration.MigrateDb` (startup-migration path) — no manual `DbSet` declarations needed. Mappers (`IEntityTypeConfiguration<T>`) and seeders are picked up by assembly scan — a seeder inherits the **base class** `DataSeedingConfiguration<T>` (see `PurchaseOrderStaticData`), not an `IDataSeedingConfiguration<T>` interface. Wiring seeding into only one of the two paths is a real bug this template hit once already (`PurchaseOrderStaticData` didn't appear over HTTP until `MigrateDb` got the same `.UseAutoDataSeeding(...)` call).
- **Service registration**: there is no Scrutor/convention scan in Infra — `InfraSetup.AddInfraServices` registers each service explicitly (`AddScoped<IMembershipService, MembershipService>()`); add a line there for every new Infra service. Keep implementations `internal sealed` under `Minimal.Infra/Services/`. The only assembly scans are EF Core's `UseAutoConfigModel`/`UseAutoDataSeeding`, SlimMessageBus's `AutoDeclareFrom`/`AddServicesFromAssembly`, FluentValidation's `AddValidatorsFromAssembly`, and Mapster's `ScanMaps`/`Scan`.
- **Endpoint mapping helpers** (`DKNet.AspCore.Extensions`, not local to this template): hand-mapped routes use the raw minimal-API surface directly (see `PurchaseOrderV1Endpoint`); generator-driven routes call the package's generic `MapGetList<TEntity,TKey,TDto>`/`MapGetById`/`MapPost<TRequest,TDto>`/`MapPutById`/`MapDeleteById` (see the generated `ProductCrudEndpointExtensions.MapProductCrud()`). POST does NOT auto-add idempotency either way — call `.RequiredIdempotentKey()` explicitly (see `PurchaseOrderV1Endpoint`'s create route); clients then send `X-Idempotency-Key: {Guid}`. The automated sample's generated create route has no such call — it accepts a replayed request as a fresh create.
- **`ByUser` / acting-user auto-fill**: `AddContextualRequestPopulation` (wired in `Program.cs`) populates any `[FromClaim(...)]`-decorated request property before validation and before the handler runs, from the authenticated caller's claims; with `RequireAuthorization` off, that caller is the built-in demonstration authentication provider (`FeatureManagement:EnableDemoAuthentication`, `Minimal.Api/Configs/Auth/DemoAuthConfig.cs`), never an unauthenticated fallback. A **generated** CRUD request can never carry a `[FromClaim]` property — the generator forwards only `System.ComponentModel.DataAnnotations` attributes onto generated properties — so the automated sample's acting-user stamping goes through `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook` instead, wired once in `ServiceConfigs.AddAllAppServices` (`.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()`) and applying to every entity on `CoreDbContext`, not just `Product`.
- **Mapster global config**: `Minimal.AppServices/AppSetup.cs`. DTOs use `[GenerateDto(...)]` (generates every audited property by default — `Exclude`/`Include` to narrow) or a hand-written record (full control, see `PurchaseOrderDto`). Lazy mapping after `SaveChanges` via `mapper.ResultOf<T>(entity)` / `mapper.LazyMap<T>()` from `DKNet.SlimBus.Extensions.LazyMapper` (the template's former local copy under `AppServices/Extensions/LazyMapper` was removed — use the package's).
- **Message bus**: `AddServiceBus` in `ServiceBusSetup.cs` always wires an in-memory child bus (`ImMemory`) for internal handlers. Azure Service Bus child bus (`AzureBus`) is added only when `ConnectionStrings:AzureBus` is non-empty — that's where `Product`'s `Produce<ProductCreatedEvent>`/`Consume<ProductCreatedEvent>` topology lives. Domain events are published by `Minimal.Infra/Services/EventPublisher.cs`, whether raised by hand (`AddEvent`, see `PurchaseOrder`) or declared (`[RaisesEvent]`, see `Product` — raised by DKNet's EF Core save hook, not application code).
- **Declared-event naming convention**: `[RaisesEvent(EventOperations.X, nameof(Prop1), ...)]` on an entity composes the generated payload record's name as `<Entity><NarrowingProps><Operation>Event` — e.g. `[RaisesEvent(EventOperations.Updated, nameof(Price))]` on `Product` generates `ProductPriceUpdatedEvent`, **not** `ProductUpdatedEvent`. Verify the composed name against the compiled assembly (`strings bin/**/Minimal.Domains.dll | grep <Entity>`) before wiring a consumer to it — the record has no hand-written source file to read.
- **Generated-route validation gap**: a `[Range]`/`[Required]`/etc. on a `[CrudCreate]`/`[CrudUpdate]` parameter *is* forwarded onto the generated request property, but is only *enforced* when the entity's create/update route is a **literal** `Map*(string, Delegate)` call the .NET 10 validation source generator can see in this repo's own source (true for hand-mapped routes like `PurchaseOrderV1Endpoint`; false for anything mapped through `DKNet.AspCore.Extensions`'s generic `Map*<TRequest,TDto>` wrapper, including every generated CRUD route). Don't assume a DataAnnotations attribute on a generated request is enforced without checking which mapping style its endpoint uses.

## Testing

### Both shipped suites are TEACHING MATERIAL — business tests only

`Minimal.App.Tests` and `Minimal.App.BDDTests` ship inside every consumer's generated solution. A
team reads them to learn *how we write tests here*, so every test in them must be about the business
domain — the `PurchaseOrder` (manual) and `Product` (automated) samples: entity invariants,
validators, specs, handler results, CRUD over HTTP, domain events.

**Do not add platform/infrastructure tests to either shipped suite.** Concretely, do not add tests for:

| Category | Examples that were deliberately deleted |
|---|---|
| Logging & telemetry | log sanitizing, console/OTel exporter wiring |
| Host & startup plumbing | job selectors, migration-job launch, host config markers, AppHost purity |
| Security middleware | CORS, HSTS, security headers, rate limits, JWT signature config, default-deny auth |
| Ops endpoints | health probes, Swagger/OpenAPI document regression, global exception handler |
| Config binding | `FeatureOptions` ↔ `appsettings` key contracts, secure-default appsettings scans |
| Template/repo shape | nuspec content, `dotnet new` scaffolding, package pinning, CI solution membership |

These test DKNet framework or ASP.NET behaviour, not the consumer's business. They bloat the suite a
team is meant to read as an example, and most are meaningless once the samples are deleted.

**Where the repo-only guards live instead:** `tests/DKNet.Templates.ScaffoldTests/` (outside `src/`,
never packed, member of the root `DKNet.Templates.slnx`). Packaging, scaffolding, repo-hygiene and
appsettings-placeholder guards go there. That project resolves paths via
`AppContext.BaseDirectory, "../../../../../src"` — keep new guards on the same convention.

**The one exception inside `Minimal.App.Tests`:** `Architecture/` keeps the NetArchTest layer rules
(`ApiTests`, `AppServiceTests`, `InfraTests`, `RecordArchitectureTests`) plus `MigrationSchemaTests`
and `SampleInvariantTests`. Those enforce the DDD conventions a team must follow, so they earn their
place as an example.

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

These live in this repository only — of the list below, `AGENTS.md` alone is packed into a consumer's
generated solution.

- `AGENTS.md` — full architecture reference (layer rules, message bus, command/mapping details).
- `docs/samples/manual-vs-automated.md` — layer-by-layer comparison of the two worked samples, including what the generator-driven sample gives up.
- `docs/samples/manual-purchase-orders/`, `docs/samples/automated-products/` — thin per-sample READMEs (what each demonstrates, routes, how to delete it).
- `skills/` — the agent skills (this repo root is the `dknet-minimal` plugin). Index: `skills/README.md`.
- `specs/` — Spec-Kit feature specs; workflow docs in `SPEC_KIT.md`.

## Feature lifecycle (plugin)

A business feature is one vertical slice, addressable by its `<Feature>` folder name, which appears
literally in ten fixed roots across six projects. `skills/dknet-feature-lifecycle/SKILL.md` is
the authority on that footprint, the out-of-folder touchpoints a delete must also clean
(`DomainSchemas`, `ServiceBusSetup` Produce/Consume, `FeatureOptions` + `FeatureManagement` JSON,
scope policies, migrations, docs links), and the migration rule on removal.

Every scaffolding workflow takes `mode=manual|auto`, threaded end-to-end by the orchestrator. The mode
is not a style preference — it changes which files exist, whether an attribute-declared rule is
enforced, whether create is idempotent, and how the acting user is attributed. A rule that has to
refuse an operation is not part of that trade: a FluentValidation validator on a generated request
runs on the generated route (`docs/api-pipeline.md`). Workflows that omit `mode=` detect it by
grepping the entity for `[CrudCreate]`.

The workflows are skills (there is no `.claude/commands/` — Claude Code, Copilot and the skills CLI
all read `SKILL.md`); each carries `metadata.arguments` (the Agent Skills spec has no `argument-hint`
field, and `agentskills validate` rejects unknown keys) plus a `Usage:` line, and is invoked as a slash command:

| Workflow | Purpose |
|---|---|
| `/dknet-feature <Feature> <Entity> [mode=…] [props…]` | Add a slice end-to-end (plan → domain → CRUD → endpoint → tests → BDD → docs) |
| `/dknet-feature-remove <Feature>` | Retire a slice end-to-end, including touchpoints and a drop migration |
| `/dknet-entity`, `/dknet-crud`, `/dknet-endpoint`, `/dknet-unit-tests`, `/dknet-bdd-test`, `/dknet-docs` | Individual phases, same `mode=` contract |

Reference skills (no arguments) back them: `dknet-project-structure` (read first),
`dknet-ddd-principles`, `dknet-feature-lifecycle`, `dknet-scaffold`, `dknet-domain-entity`,
`dknet-efcore-config`, `dknet-appservices-actions`, `dknet-queries-specs`, `dknet-dto-mapping`,
`dknet-endpoint-config`, `dknet-messaging-events`, `dknet-auth-and-ownership`,
`dknet-platform-config`, `dknet-unit-test`, `dknet-bdd-tests`, `dknet-feature-documentation`,
`dknet-package-adoption`. Subagents live in `agents/` and are listed one file each in
`.claude-plugin/plugin.json` (`agents` must be an array of file paths — a directory string fails
`claude plugin validate`).

`ls src/ApiEndpoints/Minimal.Domains/Features/` enumerates the features that exist — there is
deliberately no registry file to drift out of sync.

### Skill authoring rules (enforced by `./validate-plugin.sh` check 6)

Skills are copied verbatim into other repositories (plugin cache, `npx skills add` targets,
`node_modules/@drunkcoding/dknet-minimal-skills`), so every `skills/<x>/SKILL.md`:

- has frontmatter `name` equal to its folder, a single-line `description` (≤ 1024 chars), and only
  Agent Skills spec keys (`allowed-tools`, `license`, `compatibility`, `metadata`) — workflow skills put
  their argument shape in `metadata.arguments`, never in `argument-hint`;
- writes paths relative to the consumer's solution root (`ApiEndpoints/Minimal.Infra/…`), never
  `src/…`, and always `Minimal.*` (the template `sourceName`), never an example project name;
- refers to other skills by name (``the `dknet-queries-specs` skill``) and to workflows as
  `/dknet-entity`, never by a `.claude/…` or `.github/…` path;
- never links into `docs/` of this repository (only `AGENTS.md` ships) and never cites a `*.sh`
  script — inline `dotnet ef migrations add <Name> -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj`;
- inlines the exemplar code it teaches from, so it still works after a team deletes the two samples.

The repository root is the plugin: `.claude-plugin/plugin.json` + `skills/` + `agents/` for Claude Code,
`plugin.json` for GitHub Copilot, `package.json` for npm. There is no `.claude/` or `.github/skills/`
mirror any more — Copilot and every other agent get the skills through `npx skills add` or the npm
package. Versions stay `0.0.0` in git; `publish-nuget-github.yml` stamps the release version into all
three manifests (`npm version` → `scripts/sync-version.mjs`) and publishes to npm with OIDC trusted
publishing after the NuGet template package. Working on this repo in Claude Code: `claude --plugin-dir .`
(`skills/` is not auto-discovered the way `.claude/skills/` was).

## `AGENTS.md` ships to consumers — write guidance for THEIR tree, not this one

`DKNet.Minimal.Template.nuspec` packs exactly this into the template's `content/`: `AGENTS.md`,
`.template.config/`, the four solution-level files (`global.json`, `Directory.Packages.props`,
`coverage.runsettings`, `DKNet.Templates.sln`) and `ApiEndpoints/**`. Nothing else reaches a consumer —
`skills/`, `agents/`, `.claude-plugin/`, `.github/`, `.vscode/` and `docs/` stay in this repository, so a link
into any of them is a dead link in the generated tree. (`README.md` is packed to the *package* root as
the NuGet package-page readme, not into the generated solution.)

A consumer running `dotnet new dknet-minimal -n Contoso` therefore receives `AGENTS.md` inside their
own solution. It is product, and its paths must be correct **in the generated tree**:

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

Verify after touching shipped guidance — regenerating is the only real test:

```bash
cd src && dotnet pack DKNet.Minimal.Template.csproj -c Release -o /tmp/pkg
dotnet new install /tmp/pkg/DKNet.Minimal.Template.1.0.0.nupkg --force
cd /tmp && dotnet new dknet-minimal -n Contoso && cd Contoso && dotnet build -c Release
grep -n 'src/ApiEndpoints\|add-migration.sh' AGENTS.md    # expect no hits
dotnet new uninstall DKNet.Minimal.Template               # clean up after
```

The `dknet-scaffold` skill documents that flow end to end — install, generate, the six template
parameters, first run, and deleting the two shipped samples. It lives in this repository only;
consumers do not receive it.
