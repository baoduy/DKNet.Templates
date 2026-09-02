---
name: dknet-project-structure
description: Orientation to the DKNet.Minimal.Template layer boundaries, vertical-slice folder layout, and auto-discovery wiring. Use first, before any other dknet-* skill, when working in a solution generated from this template.
---

# Skill: Project Structure Orientation

Read this before touching any layer. It answers "where does this code go" and "how does it get discovered" so you don't have to re-derive the architecture from scratch.

---

## When to Use

- First skill to read when starting any feature work in a DKNet.Minimal.Template solution (or a solution generated from it).
- Before `dknet-ddd-principles`, `dknet-domain-entity`, `dknet-efcore-config`, `dknet-appservices-actions`, or `dknet-endpoint-config`.
- Whenever you're unsure which project a file belongs in.

## Layer Boundaries (strict, no skipping)

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

`Api` depends on `AppServices`, which depends on `Domains`. `Infra` also depends on `Domains` (for the entities it persists) and is wired into `Api` at startup — it is never referenced directly by `AppServices`. Never let `AppServices` reference EF Core types directly, and never let `Domains` reference `Infra` or `AppServices`.

## Vertical Slice Folder Layout

This template ships two complete worked examples of the same shape of feature, built two different ways — read
[`docs/samples/manual-vs-automated.md`](../../../docs/samples/manual-vs-automated.md) for the full comparison before copying either.

**Hand-written — mirror `ManualSample/PurchaseOrder`:**

| Layer       | Location                                        | What goes here                                                          |
|-------------|--------------------------------------------------|--------------------------------------------------------------------------|
| Domains     | `Features/<Feature>/Entities/`                  | `AggregateRoot` subclass; mutation in named methods; raises its own events via `AddEvent(...)` |
| Infra       | `Features/<Feature>/Mappers/`                   | `IEntityTypeConfiguration<T>` — indexes, lengths, schema                |
| Infra       | `Features/<Feature>/StaticData/`                | Seed data discovered by `UseAutoDataSeeding`                            |
| AppServices | `<Feature>/V1/Actions/`                         | `*Request` (`[FromClaim]` for the acting user), `*CommandValidator`, `*CommandHandler` (sealed) |
| AppServices | `<Feature>/V1/Specs/`                           | Specification classes for duplicate/filter queries                      |
| AppServices | `<Feature>/V1/Events/`                          | Domain event handlers                                                   |
| AppServices | `<Feature>/V1/<Feature>Dto.cs`                  | Hand-written DTO record — exposes exactly the fields you write into it  |
| Api         | `ApiEndpoints/<Feature>V1Endpoint.cs`           | Implements `IEndpointConfig`; every route is a literal `group.MapPost/MapGet/MapPut/MapDelete(...)` call |

**Generator-driven — mirror `AutomatedSample/Product`:**

| Layer       | Location                                        | What goes here                                                          |
|-------------|--------------------------------------------------|--------------------------------------------------------------------------|
| Domains     | `Features/<Feature>/Entities/`                  | `AggregateRoot` subclass; class-level `[RaisesEvent(...)]`; `[CrudCreate]` ctor; `[CrudUpdate]` method(s) |
| Infra       | `Features/<Feature>/Mappers/`                   | `IEntityTypeConfiguration<T>` — still hand-written, no generator produces this |
| AppServices | `<Feature>/V1/<Feature>Dto.cs`                  | One `[GenerateDto(typeof(Entity))] public sealed partial record <Feature>Dto;` |
| AppServices | `<Feature>/V1/Events/`                          | Hand-written consumer for a declared event — the generator raises, it does not consume |
| Api         | `ApiEndpoints/<Feature>V1Endpoint.cs`           | Implements `IEndpointConfig`; calls the generated `Map<Entity>Crud()` extension, nothing hand-mapped |

Note: the domain entity folder and the `AppServices` slice use the same feature folder name in both samples (`ManualSample`, `AutomatedSample`) — the two namespaces don't have to match in general.

## Key Auto-Discovery Wiring

You almost never register things manually in this codebase — these scans do it for you:

- **EF Core model + seeding**: `UseAutoConfigModel` + `UseAutoDataSeeding` scan the assembly for `IEntityTypeConfiguration<T>` and `DataSeedingConfiguration<T>` classes. No manual `DbSet<T>` declarations. Both calls must be wired into **both** `InfraSetup.AddInfraServices` (DI host path) and `InfraMigration.MigrateDb` (startup-migration path). Wiring seeding into only one is a real bug this template hit once — `PurchaseOrderStaticData` didn't appear over HTTP until `MigrateDb` got the same `.UseAutoDataSeeding(...)` call.
- **Service registration**: Scrutor scans `Minimal.Infra`. Keep concrete repos/services `internal sealed` and place them under a `.Repos` or `.Services` namespace so the convention scan picks them up.
- **Endpoint mapping helpers** (`DKNet.AspCore.Extensions`, not local to this template): hand-mapped routes use the raw minimal-API surface directly (see `PurchaseOrderV1Endpoint`); generator-driven routes call the package's generic `MapGetList`/`MapGetById`/`MapPost<TRequest,TDto>`/`MapPutById`/`MapDeleteById` (see the generated `ProductCrudEndpointExtensions.MapProductCrud()`). POST does NOT auto-add idempotency either way — call `.RequiredIdempotentKey()` explicitly (see `PurchaseOrderV1Endpoint`'s create route); clients then send `X-Idempotency-Key: {Guid}`. The automated sample's generated create route has no such call.
- **`ByUser` / acting-user auto-fill**: `AddContextualRequestPopulation` (wired in `Program.cs`) populates any `[FromClaim(...)]`-decorated request property before validation and before the handler runs (see `CreatePurchaseOrderRequest.ByUser`) — no `RequestBase` class is involved. A **generated** CRUD request can never carry a `[FromClaim]` property (the generator forwards only DataAnnotations attributes), so the automated sample's acting-user stamping goes through `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook` instead, wired once in `ServiceConfigs.cs` and applying to every entity on `CoreDbContext`.
- **Mapster**: global config lives in `Minimal.AppServices/AppSetup.cs`. DTOs use `[GenerateDto(...)]` or a hand-written record. Lazy mapping after `SaveChanges` via `mapper.ResultOf<T>(entity)`.
- **Domain events**: published by `Minimal.Infra/Services/EventPublisher.cs`, which forwards to `IMessageBus` (SlimMessageBus), whether raised by hand (`AddEvent`, see `PurchaseOrder`) or declared (`[RaisesEvent]`, see `Product` — raised by DKNet's EF Core save hook, not by application code). An in-memory child bus (`ImMemory`) always exists for internal handlers; an Azure Service Bus child bus (`AzureBus`) is added only when `ConnectionStrings:AzureBus` is configured — that's where `Product`'s `Produce<ProductCreatedEvent>`/`Consume<ProductCreatedEvent>` topology lives.

## Exemplar to Read When Unsure

Two worked examples cover all four layers — pick the one matching how your new feature should be built (see `docs/samples/manual-vs-automated.md` for the full trade-off table):

**`PurchaseOrder`** (hand-written, everything explicit):
- `Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs`
- `Minimal.Infra/Features/ManualSample/Mappers/`, `StaticData/`
- `Minimal.AppServices/ManualSample/V1/Actions/`, `Specs/`, `Events/`
- `Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs`

**`Product`** (generator-driven CRUD + events):
- `Minimal.Domains/Features/AutomatedSample/Entities/Product.cs` (`[RaisesEvent]`, `[CrudCreate]`, `[CrudUpdate]`, `[CrudAction]`)
- `Minimal.Infra/Features/AutomatedSample/Mappers/` (still hand-written), `ExternalEvents/`
- `Minimal.AppServices/AutomatedSample/V1/ProductDto.cs` (`[GenerateDto]`), `Events/`
- `Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs` (`group.MapProductCrud()`)

---

## Next Steps

Before writing any domain or application code, read:
→ **dknet-ddd-principles** — for judgment calls (aggregate boundaries, entity vs. value object, when to use a domain event)
→ **dknet-domain-entity** — for the entity class mechanics
