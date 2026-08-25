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

Every feature mirrors this table. Two worked examples live side by side under `src/ApiEndpoints/` —
the hand-written `PurchaseOrder` (feature folder `ManualSample`) and the generator-driven `Product`
(feature folder `AutomatedSample`) — see `docs/samples/manual-vs-automated.md` for the full
layer-by-layer comparison:

| Layer       | Location                                        | What goes here                                                          |
|-------------|--------------------------------------------------|--------------------------------------------------------------------------|
| Domains     | `Features/<Feature>/Entities/`                  | `AggregateRoot` subclass; mutation in methods, or class-level `[RaisesEvent]`/`[CrudCreate]`/`[CrudUpdate]` |
| Infra       | `Features/<Feature>/Mappers/`                   | `IEntityTypeConfiguration<T>` — indexes, lengths, schema (hand-written either way) |
| Infra       | `Features/<Feature>/StaticData/`                | Seed data discovered by `UseAutoDataSeeding` (optional — `Product` has none)  |
| AppServices | `<Feature>/V1/Actions/`                         | `*Request` (`[FromClaim]` for the acting user), `*CommandValidator`, `*CommandHandler` (sealed) — skipped entirely when the entity uses `[CrudCreate]`/`[CrudUpdate]`, the generator produces this layer instead |
| AppServices | `<Feature>/V1/Specs/`                           | Specification classes for duplicate/filter queries — skipped when routes map through the generic `MapGetById`/`MapGetList` |
| AppServices | `<Feature>/V1/Events/`                          | Domain event handlers                                                   |
| AppServices | `<Feature>/V1/<Feature>Dto.cs`                  | Hand-written DTO record, or one `[GenerateDto(typeof(Entity))]` partial record |
| Api         | `ApiEndpoints/<Feature>V1Endpoint.cs`           | Implements `IEndpointConfig`; every route hand-mapped, or one `group.Map<Entity>Crud()` call |

Note: the domain entity folder and the AppServices slice use the same feature folder name in both
samples (`ManualSample`, `AutomatedSample`) — the two namespaces don't have to match in general.

## Key Auto-Discovery Wiring

You almost never register things manually in this codebase — these scans do it for you:

- **EF Core model + seeding**: `UseAutoConfigModel` + `UseAutoDataSeeding` in **both** `InfraSetup.AddInfraServices` (DI host path) and `InfraMigration.MigrateDb` (startup-migration path) scan the assembly for `IEntityTypeConfiguration<T>` and `IDataSeedingConfiguration<T>` classes. No manual `DbSet<T>` declarations. Wiring seeding into only one of the two paths is a real bug this template hit once — `PurchaseOrderStaticData` didn't appear over HTTP until `MigrateDb` got the same `.UseAutoDataSeeding(...)` call.
- **Service registration**: Scrutor scans `Minimal.Infra`. Keep concrete repos/services `internal sealed` and place them under a `.Repos` or `.Services` namespace so the convention scan picks them up.
- **Endpoint helpers**: hand-mapped routes use the raw minimal-API surface directly (`group.MapPost/MapGet/MapPut/MapDelete`, see `PurchaseOrderV1Endpoint`); generator-driven routes call `DKNet.AspCore.Extensions`'s generic `MapGetById<TEntity,TKey,TDto>`/`MapGetList`/`MapPost<TRequest,TDto>`/`MapPutById`/`MapDeleteById` (see the generated `ProductCrudEndpointExtensions.MapProductCrud()`). POST does NOT auto-add idempotency either way — call `.RequiredIdempotentKey()` explicitly (see `PurchaseOrderV1Endpoint`'s create route); clients then send `X-Idempotency-Key: {Guid}`. The automated sample's generated create route has no such call.
- **`ByUser` auto-fill**: `AddContextualRequestPopulation` (wired in `Program.cs`) populates any `[FromClaim(...)]`-decorated request property before the handler runs (see `CreatePurchaseOrderRequest.ByUser`). A **generated** CRUD request can never carry a `[FromClaim]` property — the generator forwards only `System.ComponentModel.DataAnnotations` attributes — so `Product`'s acting-user stamping goes through `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook` instead, wired once in `ServiceConfigs.cs`.
- **Mapster**: global config lives in `Minimal.AppServices/AppSetup.cs`. DTOs use `[GenerateDto(...)]` or a hand-written record. Lazy mapping after `SaveChanges` via `mapper.ResultOf<T>(entity)`.
- **Domain events**: published by `Minimal.Infra/Services/EventPublisher.cs`, whether raised by hand (`AddEvent`, see `PurchaseOrder`) or declared (`[RaisesEvent]`, see `Product` — raised by DKNet's EF Core save hook, not application code). An in-memory child bus (`ImMemory`) always exists for internal handlers; an Azure Service Bus child bus (`AzureBus`) is added only when `ConnectionStrings:AzureBus` is configured — that's where `Product`'s external publish/subscribe lives.

## Exemplar to Read When Unsure

`PurchaseOrder` (`ManualSample`) is the reference implementation for every hand-written layer:
- `Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs`
- `Minimal.Infra/Features/ManualSample/Mappers/PurchaseOrderConfigs.cs`, `StaticData/PurchaseOrderStaticData.cs`
- `Minimal.AppServices/ManualSample/V1/Actions/`, `Specs/`, `Events/`
- `Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs`

`Product` (`AutomatedSample`) is the reference implementation for the generator-driven shape:
- `Minimal.Domains/Features/AutomatedSample/Entities/Product.cs` (`[RaisesEvent]`, `[CrudCreate]`, `[CrudUpdate]`)
- `Minimal.Infra/Features/AutomatedSample/Mappers/ProductConfigs.cs` (still hand-written)
- `Minimal.AppServices/AutomatedSample/V1/ProductDto.cs` (`[GenerateDto]`)
- `Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs` (`group.MapProductCrud()`)

---

## Next Steps

Before writing any domain or application code, read:
→ **dknet-ddd-principles** — for judgment calls (aggregate boundaries, entity vs. value object, when to use a domain event)
→ **dknet-domain-entity** — for the entity class mechanics
