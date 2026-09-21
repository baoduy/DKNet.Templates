---
name: dknet-project-structure
description: Orientation to the DKNet.Minimal.Template layer boundaries, the six projects, the vertical-slice folder layout for a feature, and auto-discovery wiring. Use first, before any other dknet-* skill, when working in a solution generated from this template.
---

# DKNet project structure

Read this before touching any layer. `AGENTS.md` at the solution root carries a condensed version
of this same map — read it too if you're unsure which project a file belongs in.

## Layer boundaries and dependency direction

```
Minimal.Api          → entry point, endpoints, auth, OpenAPI
  ↓
Minimal.AppServices  → CQRS handlers, validators, DTOs, domain event handlers
  ↓
Minimal.Domains      → entities, aggregate roots, domain service contracts
  ↑
Minimal.Infra        → EF Core (CoreDbContext), repos, event publisher, service bus
  (wires into Api via InfraSetup.AddInfraServices)

Minimal.Share        → shared constants/options/base types (read by all layers)
Minimal.AppHost      → Aspire orchestration only (Redis + PostgreSQL + Minimal.Api), no business logic
```

Every project reference points inward: `Minimal.Domains` references only `Minimal.Share`;
`Minimal.AppServices` depends only on `Domains` (+ `Share`); `Minimal.Infra` and `Minimal.Api`
depend on both, never the reverse. An outward reference (`Domains` → `AppServices`, say) is a
circular project reference MSBuild refuses outright — the compiler holds this boundary, not a test.

## The six feature-carrying projects

| Project | Owns |
|---|---|
| `Minimal.Domains` | Entities, aggregate roots, owned types, domain service **contracts** (`IDomainService`) |
| `Minimal.Infra` | EF Core mappers, seed data, repositories, `EventPublisher`, service-bus topology, domain service **implementations** |
| `Minimal.AppServices` | Command/query requests, handlers, FluentValidation validators, specs, DTOs, domain-event consumers |
| `Minimal.Api` | `IEndpointConfig` route groups, auth policies, platform `Configs/` |
| `Minimal.Share` | Cross-cutting constants/options (`FeatureOptions`, `SharedConsts`) read by every layer |
| `Minimal.AppHost` | Aspire orchestration (Redis, PostgreSQL, sample-data generation) — no business logic |

`Minimal.App.Tests` (xUnit + Shouldly), `Minimal.App.BDDTests` (Reqnroll + NUnit) and
`Minimal.App.TestSupport` (shared `WebApplicationFactory` base) round out the solution but carry no
feature code of their own.

## Vertical-slice folder footprint for one feature

Every business feature is a folder name (`<Feature>`) repeated across projects. Verified against
the two shipped samples, `ManualSample` and `AutomatedSample`, under `ApiEndpoints/`:

| Layer | `mode=manual` | `mode=auto` |
|---|---|---|
| Domains | `Minimal.Domains/Features/<Feature>/Entities/` — hand-written mutation + `AddEvent(...)` | same path — class-level `[RaisesEvent]`, `[CrudCreate]` ctor, `[CrudUpdate]`/`[CrudAction]` methods |
| Infra | `Minimal.Infra/Features/<Feature>/Mappers/` (`IEntityTypeConfiguration<T>`), optional `StaticData/` (seed data) | same `Mappers/` (still hand-written — no generator produces it), optional `ExternalEvents/` (broker consumers) |
| AppServices | `Minimal.AppServices/<Feature>/V1/Actions/`, `Queries/`, `Specs/`, `Events/`, `<Feature>Dto.cs` | `Minimal.AppServices/<Feature>/V1/<Feature>Dto.cs` (one `[GenerateDto]` line), `Events/` (consumers only — generator raises, doesn't consume), plus optional `Validators/` (precondition rules against a generated request), `Actions/` (any operation dropped out of the generated map), `Queries/`/`Specs/` (custom read shapes), and a Mapster `IRegister` for any hand-added DTO property |
| Api | `Minimal.Api/ApiEndpoints/<Feature>/<Entity>V1Endpoint.cs` — every route a literal `Map*` call | same file — one `group.Map<Entity>Crud(o => …)` call, plus any routes excluded from it mapped literally below |
| Tests | `Minimal.App.Tests/Unit/<Feature>/`, `Minimal.App.Tests/Integration/<Feature>/V1/` | same |
| BDD | `Minimal.App.BDDTests/Features/<Plural>/*.feature` + `Steps/*.cs` | same |

The domain/AppServices feature folder name doesn't have to match the BDD folder's plural — the two
samples happen to (`ManualSample`↔`PurchaseOrders`, `AutomatedSample`↔`Products`).

## Auto-discovery — what gets found without registering it

| What | Found by | Scans |
|---|---|---|
| HTTP route group | `IEndpointConfig` | `UseEndpointConfigs`, assembly scan of `Minimal.Api` |
| Command/query request+handler | `Fluents.Requests.*`/`Fluents.Queries.*` | `AutoDeclareFrom`/`AddServicesFromAssembly` on the in-memory bus, scanning `Minimal.AppServices` |
| Request validator | `AbstractValidator<TRequest>` | `AddValidatorsFromAssembly(typeof(AppSetup).Assembly, includeInternalTypes: true)` |
| EF Core table mapping | `IEntityTypeConfiguration<T>` | `UseAutoConfigModel([...])` — must be wired in **both** `InfraSetup.AddInfraServices` and `InfraMigration.MigrateDb` |
| Seed data | `DataSeedingConfiguration<T>` (base class, not an interface) | `UseAutoDataSeeding([...])` — same both-places rule; wiring only one is a real bug this template hit once |
| Repos/domain services | — (not auto-discovered) | Explicit `AddScoped<IService, Service>()` line in `InfraSetup.AddInfraServices`; keep implementations `internal sealed` under `Minimal.Infra/Services/` |
| DTO mapping | `[MapsFrom(typeof(Entity))]` or `[GenerateDto(typeof(Entity))]` | Mapster `ScanMaps()` in `Minimal.AppServices/AppSetup.cs` |
| Custom Mapster config | `IRegister` | `config.Scan(assembly)`, same `AppSetup.cs` |
| Internal event consumer | `Fluents.EventsConsumers.IHandler<TEvent>` in `AppServices` | `AddServicesFromAssembly` on the in-memory child bus |
| External (broker) event consumer | same interface in `Minimal.Infra/Features/<Feature>/ExternalEvents/` | `AddServicesFromAssembly` on the Azure child bus — reached only via an explicit `azb.Produce`/`azb.Consume` pair in `ServiceBusSetup.cs` |

## The two shipped samples

| Feature folder | Entity | Flow | Read for |
|---|---|---|---|
| `ManualSample` | `PurchaseOrder` | Hand-written every layer | Enforced DataAnnotations, idempotent create, `[FromClaim]` acting user, custom list filter |
| `AutomatedSample` | `Product` | `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`[RaisesEvent]`/`[GenerateDto]` | Generated CRUD + events, composite endpoint (generated routes + hand-written ones below them) |

Full trade-off table: `dknet-feature-lifecycle` §1.

## Commands

Run from the solution root — `dotnet build`/`dotnet test` need no explicit `.sln` path:

```bash
dotnet build -c Release
dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet run --project ApiEndpoints/Minimal.Api            # API only
dotnet run --project ApiEndpoints/Minimal.AppHost        # Redis + PostgreSQL via Aspire

cd ApiEndpoints
dotnet ef migrations add <Name>    -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj
dotnet ef migrations remove        -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj
```

See `dknet-scaffold` for the install/generate steps and how these project names map onto your own
solution's names.

## Which skill for what

Reference (read, don't invoke with args):

| Skill | For |
|---|---|
| `dknet-project-structure` | This page — orientation, always read first |
| `dknet-ddd-principles` | Aggregate boundaries, entity vs. value object, when to raise a domain event |
| `dknet-feature-lifecycle` | Choosing manual vs. auto, a feature's full file footprint, add/remove order |
| `dknet-scaffold` | Installing the template, generating a solution, first run, deleting the samples |
| `dknet-entity` | Entity class mechanics (`AggregateRoot`, ctor rules, mutation methods) |
| `dknet-efcore-config` | `IEntityTypeConfiguration<T>` mappers, seed data, domain-service wiring |
| `dknet-crud` | Commands: requests, validators, handlers, both flows |
| `dknet-queries-specs` | Queries: specs, hand-written read handlers, the generic list route |
| `dknet-dto-mapping` | DTO shape, Mapster config, `[GenerateDto]` vs. hand-written record |
| `dknet-endpoint` | `IEndpointConfig`, literal routes vs. `Map<Entity>Crud()`, idempotency |
| `dknet-messaging-events` | Domain events, `[RaisesEvent]`, in-memory vs. Azure Service Bus |
| `dknet-auth-and-ownership` | Auth policies, `[FromClaim]`, `IDataOwnerProvider`/`ICurrentUserProvider` |
| `dknet-platform-config` | Everything else the template wires: start-up order, flags, config sections, jobs, Aspire, test hosts |
| `dknet-unit-tests` | xUnit + `ApiFixture` integration tests |
| `dknet-bdd-tests` | Reqnroll + NUnit scenarios |
| `dknet-docs` | Feature README + architecture diagrams |
| `dknet-package-adoption` | Adopting DKNet packages into a non-template project |

Workflow (invoke with `/`, takes arguments):

| Command | Does |
|---|---|
| `/dknet-feature <Feature> <Entity> [mode=manual\|auto] [props…]` | End-to-end slice: plan → domain → CRUD → endpoint → tests → BDD → docs |
| `/dknet-feature-remove <Feature>` | Retire a slice end-to-end, including touchpoints and a drop migration |
| `/dknet-entity`, `/dknet-crud`, `/dknet-endpoint`, `/dknet-unit-tests`, `/dknet-bdd-tests`, `/dknet-docs` | Individual phases of the same lifecycle |

Subagents (Claude Code only, used by the workflow commands): `dknet-architect` (plans),
`dknet-implementer` (writes code across layers), `dknet-bdd-engineer` (BDD scenarios).

## Read-first ordering

1. This skill.
2. `dknet-ddd-principles`, if the aggregate shape or event placement isn't obvious.
3. `dknet-feature-lifecycle` §1, to pick manual vs. auto.
4. The layer skill for whatever you're about to write (`dknet-entity` → `dknet-efcore-config`
   → `dknet-crud`/`dknet-queries-specs`/`dknet-dto-mapping` → `dknet-endpoint`).
5. `dknet-platform-config` only when the change is cross-cutting, not feature-scoped.
