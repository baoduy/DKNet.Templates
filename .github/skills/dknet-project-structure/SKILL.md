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

Every feature mirrors this table (exemplar: `CustomerProfile`, feature folder `CustomerProfiles`):

| Layer       | Location                                        | What goes here                                                          |
|-------------|--------------------------------------------------|--------------------------------------------------------------------------|
| Domains     | `Features/<Feature>/Entities/`                  | `AggregateRoot` subclass + owned types; mutation in methods             |
| Infra       | `Features/<Feature>/Mappers/`                   | `IEntityTypeConfiguration<T>` — indexes, lengths, schema                |
| Infra       | `Features/<Feature>/StaticData/`                | Seed data discovered by `UseAutoDataSeeding`                            |
| AppServices | `<Feature>/V1/Actions/`                         | `*Request` (`[MapsFrom]`), `*CommandValidator`, `*CommandHandler` (sealed) |
| AppServices | `<Feature>/V1/Specs/`                           | Specification classes for duplicate/filter queries                      |
| AppServices | `<Feature>/V1/Events/`                          | Domain event handlers                                                   |
| AppServices | `<Feature>/V1/<Feature>Dto.cs`                  | `[GenerateDto]` partial DTO                                             |
| Api         | `ApiEndpoints/<Feature>V1Endpoint.cs`           | Implements `IEndpointConfig`                                            |

Note: the domain entity folder is singular (`Features/Profiles/`) while the AppServices slice is plural (`CustomerProfiles/V1/`) — the two namespaces don't have to match.

## Key Auto-Discovery Wiring

You almost never register things manually in this codebase — these scans do it for you:

- **EF Core model + seeding**: `UseAutoConfigModel` + `UseAutoDataSeeding` in `InfraSetup.AddInfraServices` scan the assembly for `IEntityTypeConfiguration<T>` and `IDataSeedingConfiguration<T>` classes. No manual `DbSet<T>` declarations.
- **Service registration**: Scrutor scans `Minimal.Infra`. Keep concrete repos/services `internal sealed` and place them under a `.Repos` or `.Services` namespace so the convention scan picks them up.
- **Endpoint helpers**: `MapGetList`, `MapGetById`, `MapPost`, `MapPut`, `MapDelete` from `FluentEndpointMapperExtensions.cs`. POST does NOT auto-add idempotency — call `.AddIdempotencyFilter()` explicitly; clients then send `X-Idempotency-Key: {Guid}`.
- **`ByUser` auto-fill**: `SetUserIdPropertyFilter` (added by `EndpointConfig.CreateGroup`) injects the user ID into any command inheriting `RequestBase` — no extra code needed in handlers.
- **Mapster**: global config lives in `Minimal.AppServices/AppSetup.cs`. DTOs use `[GenerateDto(...)]`. Lazy mapping after `SaveChanges` via `mapper.ResultOf<T>(entity)`.
- **Domain events**: published by `Minimal.Infra/Services/EventPublisher.cs`, which forwards to `IMessageBus` (SlimMessageBus). An in-memory child bus (`ImMemory`) always exists for internal handlers; an Azure Service Bus child bus (`AzureBus`) is added only when `ConnectionStrings:AzureBus` is configured.

## Exemplar to Read When Unsure

`CustomerProfile` is the reference implementation across all four layers:
- `Minimal.Domains/Features/Profiles/Entities/CustomerProfile.cs`
- `Minimal.Infra/Features/Profiles/Mappers/`
- `Minimal.AppServices/CustomerProfiles/V1/Actions/`, `Specs/`, `Events/`
- `Minimal.Api/ApiEndpoints/CustomerProfileV1Endpoint.cs`

---

## Next Steps

Before writing any domain or application code, read:
→ **dknet-ddd-principles** — for judgment calls (aggregate boundaries, entity vs. value object, when to use a domain event)
→ **dknet-domain-entity** — for the entity class mechanics
