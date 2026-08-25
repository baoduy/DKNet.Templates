# DKNet Plugin DDD Redesign Implementation Plan

> **2026-08-25 note:** This document is a historical record from when the template's demo
> features were `CustomerProfile`/`LoyaltyMembership`. Those were removed; current worked
> examples are the `PurchaseOrder` (hand-written) and `Product` (generator-driven) samples —
> see `docs/samples/manual-vs-automated.md`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the `.claude-plugin/dknet-minimal` plugin's skill/agent/command set so Claude has both DDD tactical judgment (aggregate boundaries, entity vs. value object, invariants, domain events) and the existing per-layer mechanics, while dropping the unrelated Spec-Kit command set.

**Architecture:** Add two new foundational skills (`dknet-project-structure`, `dknet-ddd-principles`) that other skills and agents reference rather than duplicate. Cross-link them into the two skills where DDD judgment matters most (`dknet-domain-entity`, `dknet-appservices-actions`) and into the two agents that make architectural decisions (`dknet-architect`, `dknet-implementer`). Remove the Spec-Kit command set and its `dknet-developer` orchestrator, which have no DDD/structure role and depend on tooling out of scope. Update `README.md`'s plugin section to stay accurate.

**Tech Stack:** Markdown-only changes (Claude Code skill/agent/command files under `.claude/skills/`, `.claude/agents/`, `.claude/commands/`, plus `README.md`). No C# code, no build changes.

## Global Constraints

- Plugin id stays `dknet-minimal` (per spec — README and `marketplace.json` already reference this install path).
- `.claude-plugin/plugin.json` and `.claude-plugin/marketplace.json` are NOT modified — they reference directories, not individual files.
- `src/DKNet.Minimal.Template.nuspec` and `.csproj` are NOT modified — packaging uses `**/*` globs.
- Every factual claim in a skill (class names, method names, file paths) must be verified against actual source before being written down — no invented APIs.
- No automated test suite applies to this content; each task's "test" step is a `grep`/`Read`-based verification that the file was written correctly and that referenced facts are true.

---

### Task 1: Remove Spec-Kit commands and the dknet-developer orchestrator

**Files:**
- Delete: `.claude/commands/speckit-analyze.md`
- Delete: `.claude/commands/speckit-architecture.md`
- Delete: `.claude/commands/speckit-checklist.md`
- Delete: `.claude/commands/speckit-clarify.md`
- Delete: `.claude/commands/speckit-constitution.md`
- Delete: `.claude/commands/speckit-implement.md`
- Delete: `.claude/commands/speckit-plan.md`
- Delete: `.claude/commands/speckit-specify.md`
- Delete: `.claude/commands/speckit-tasks.md`
- Delete: `.claude/commands/speckit-taskstoissues.md`
- Delete: `.claude/commands/dknet-developer.md`

**Interfaces:**
- Consumes: nothing (pure removal)
- Produces: a `.claude/commands/` directory containing only `dknet-bdd-test.md`, `dknet-crud.md`, `dknet-docs.md`, `dknet-endpoint.md`, `dknet-entity.md`, `dknet-feature.md`, `dknet-unit-tests.md` — later tasks (README update) rely on this being the final command list.

- [ ] **Step 1: Confirm no remaining command references the files being deleted**

Run: `grep -rln "speckit\|dknet-developer" .claude/commands/dknet-bdd-test.md .claude/commands/dknet-crud.md .claude/commands/dknet-docs.md .claude/commands/dknet-endpoint.md .claude/commands/dknet-entity.md .claude/commands/dknet-feature.md .claude/commands/dknet-unit-tests.md`
Expected: no output, exit code 1 — confirms `dknet-feature.md` and the other kept commands don't call into Spec-Kit or `dknet-developer`. (Do not include `dknet-developer.md` itself in this check — it's the file being deleted and obviously references both.)

- [ ] **Step 2: Delete the files**

```bash
git rm .claude/commands/speckit-analyze.md \
       .claude/commands/speckit-architecture.md \
       .claude/commands/speckit-checklist.md \
       .claude/commands/speckit-clarify.md \
       .claude/commands/speckit-constitution.md \
       .claude/commands/speckit-implement.md \
       .claude/commands/speckit-plan.md \
       .claude/commands/speckit-specify.md \
       .claude/commands/speckit-tasks.md \
       .claude/commands/speckit-taskstoissues.md \
       .claude/commands/dknet-developer.md
```

- [ ] **Step 3: Verify the command directory now matches the expected final list**

Run: `ls .claude/commands/`
Expected:
```
dknet-bdd-test.md
dknet-crud.md
dknet-docs.md
dknet-endpoint.md
dknet-entity.md
dknet-feature.md
dknet-unit-tests.md
```

- [ ] **Step 4: Commit**

```bash
git commit -m "$(cat <<'EOF'
chore(plugin): drop Spec-Kit commands and dknet-developer orchestrator

Spec-Kit is out of scope for the DDD-focused dknet plugin; dknet-feature
already orchestrates the full vertical slice without it.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Create the dknet-project-structure skill

**Files:**
- Create: `.claude/skills/dknet-project-structure/SKILL.md`

**Interfaces:**
- Consumes: nothing
- Produces: a skill named `dknet-project-structure`, referenced by Task 4 (agent updates) as the "read first" orientation skill. No other task consumes its content directly.

- [ ] **Step 1: Write the skill file**

Create `.claude/skills/dknet-project-structure/SKILL.md`:

```markdown
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
Minimal.AppHost      → Aspire orchestration only (Redis + SqlServer + Minimal.Api), no business logic
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
```

- [ ] **Step 2: Verify the frontmatter and structural claims are accurate**

Run: `grep -n "^name:\|^description:" .claude/skills/dknet-project-structure/SKILL.md`
Expected:
```
name: dknet-project-structure
description: Orientation to the DKNet.Minimal.Template layer boundaries, vertical-slice folder layout, and auto-discovery wiring. Use first, before any other dknet-* skill, when working in a solution generated from this template.
```

Run: `grep -n "class EventPublisher" src/ApiEndpoints/Minimal.Infra/Services/EventPublisher.cs`
Expected: a match (confirms the file/class referenced in the skill still exists).

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/dknet-project-structure/SKILL.md
git commit -m "$(cat <<'EOF'
feat(plugin): add dknet-project-structure orientation skill

Gives Claude a single "read first" skill for layer boundaries, the
vertical-slice folder layout, and auto-discovery wiring, instead of
re-deriving structure from CLAUDE.md/AGENTS.md each session.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Create the dknet-ddd-principles skill

**Files:**
- Create: `.claude/skills/dknet-ddd-principles/SKILL.md`

**Interfaces:**
- Consumes: nothing
- Produces: a skill named `dknet-ddd-principles`, referenced by Task 4 (`dknet-domain-entity`, `dknet-appservices-actions`) and Task 5 (agents).

- [ ] **Step 1: Write the skill file**

Create `.claude/skills/dknet-ddd-principles/SKILL.md`:

```markdown
---
name: dknet-ddd-principles
description: DDD tactical judgment for this codebase — aggregate boundaries, entity vs. value object, invariant enforcement, when to use a domain event, avoiding anemic domain models. Use before dknet-domain-entity and dknet-appservices-actions whenever the aggregate shape or business-rule placement isn't obvious.
---

# Skill: DDD Tactical Principles

This codebase already gives you the class shapes (`AggregateRoot`, `DomainEntity`, `AddEvent`) — see `dknet-domain-entity`. This skill covers the judgment calls those shapes don't make for you: what belongs in one aggregate, what's an entity vs. a value object, where an invariant lives, and when a domain event is the right tool.

This template is a single microservice / single bounded context — these are tactical patterns, not strategic (bounded-context) design.

---

## When to Use

- Deciding what a new aggregate root should own vs. what belongs to a separate aggregate.
- Deciding whether a new type needs its own identity (entity) or is just a bag of values (owned/value object).
- Deciding whether a business rule belongs on the entity, in a domain service, or in the command handler.
- Deciding whether a mutation needs to publish a domain event or just needs a plain method call.

## Aggregate Boundaries

An aggregate is a transactional consistency boundary: everything inside it is saved together and its invariants are enforced together. The rule of thumb —

- If two pieces of data must always be consistent with each other *at the moment they're saved* (e.g. an order and its line items, where a total must match the sum of lines), they belong in the same aggregate.
- If two pieces of data can be consistent *eventually*, a moment apart, they belong in separate aggregates, coordinated through a domain event — not a direct object reference.
- Aggregates reference each other by ID (`Guid`), never by object reference. `CustomerProfile` does not hold a collection of `Order` — an `Order` holds a `CustomerId`.

Keep aggregates small. A large aggregate means more contention (every mutation locks the whole thing) and usually signals a boundary was drawn around "things that seem related" rather than "things that must be consistent together."

## Entity vs. Value Object

- **Entity** (in this codebase: `AggregateRoot` for the root, `DomainEntity` for non-root entities): has identity and a lifecycle. Two entities with identical property values are still different entities if their `Id` differs. `CustomerProfile` is an entity — two profiles with the same name and email are still two different customers.
- **Value object** (owned type, plain class with no `Id`): defined entirely by its values. Two value objects with identical properties are interchangeable. If a type never needs to be looked up or referenced independently of its parent entity, it's a value object — model it as a plain owned type (see `dknet-domain-entity` "Step 3: Create Owned Value Objects"), not as another `DomainEntity`.

Ask: "Do I ever need to fetch or reference this thing on its own, independent of its parent?" Yes → entity. No → value object.

## Invariant Enforcement

An invariant is a rule that must always hold true for an entity (e.g. "email is never empty," "quantity is never negative"). In this codebase, invariants are enforced by construction and by the entity's own mutation methods — never by a public setter:

- Properties are `{ get; private set; }`. Nothing outside the entity can put it into an invalid state directly.
- The constructor establishes the invariant for a new entity. `Update(...)` methods re-establish it for every mutation, and are the *only* path to changing mutable state (see `CustomerProfile.Update(...)` in `dknet-domain-entity`).
- If a rule needs data external to the entity (e.g. "email must be unique across all profiles"), that's not an entity invariant — it's a cross-entity business rule, and it belongs in the command handler as a duplicate-check `Specification` query (see `dknet-appservices-actions` "Step 2: Create Action — Create.cs"), because the entity has no way to see other entities.

## When to Use a Domain Event

Publish a domain event (`entity.AddEvent(new SomethingHappenedEvent(...))`, delivered via `Minimal.Infra/Services/EventPublisher.cs`) when **something outside this aggregate might care that this happened** — another aggregate needs to react, or an external system needs to be notified. Example: `ProfileCreatedEvent` after a new `CustomerProfile` is created.

Do NOT reach for an event when the effect is entirely local to this one request:
- Setting a computed field during the same handler → just do it in the handler or the entity method, no event needed.
- A validation failure → return `Result.Fail(...)`, don't publish an event.

If you can't name a concrete future subscriber (even a logging handler counts, but "just in case" doesn't), it's not an event yet — add it when a real consumer appears.

## Avoiding Anemic Domain Models

An anemic model is an entity that's just a property bag, with all the actual business logic living in command handlers. Symptoms to watch for:

- A handler reads several properties off an entity, computes something, then writes several properties back — that computation belongs in a method on the entity (e.g. `entity.Update(...)`, or a more specific method like `entity.Cancel(userId)`), not in the handler.
- The handler is the only place an invariant is checked — meaning the entity could be constructed or mutated elsewhere into an invalid state.

The handler's job is orchestration: fetch the entity (via `IRepositorySpec` + a `Specification`), call one or more methods on it, persist, map to a DTO. The entity's job is protecting its own consistency and encoding what a valid state transition looks like.

## Decision Checklist

- [ ] Can I name a concrete reason this needs to be consistent with the parent in the same transaction? If no, it's a separate aggregate.
- [ ] Does this type ever get looked up independently of its parent? If no, it's a value object, not an entity.
- [ ] Does this rule only need data already on the entity? If yes, enforce it in the entity's constructor/`Update` method, not the handler.
- [ ] Can I name a real, current subscriber for this event? If no, skip the event for now.
- [ ] Is the handler computing business logic, or just orchestrating fetch → mutate → persist → map? If it's computing, move the logic onto the entity.

---

## Next Steps

→ **dknet-domain-entity** — apply these decisions to actual entity code
→ **dknet-appservices-actions** — apply the handler-orchestration boundary to CQRS actions
```

- [ ] **Step 2: Verify the frontmatter and grounded claims**

Run: `grep -n "^name:\|^description:" .claude/skills/dknet-ddd-principles/SKILL.md`
Expected:
```
name: dknet-ddd-principles
description: DDD tactical judgment for this codebase — aggregate boundaries, entity vs. value object, invariant enforcement, when to use a domain event, avoiding anemic domain models. Use before dknet-domain-entity and dknet-appservices-actions whenever the aggregate shape or business-rule placement isn't obvious.
```

Run: `grep -n "AddEvent(new ProfileCreatedEvent" src/ApiEndpoints/Minimal.AppServices/CustomerProfiles/V1/Actions/Create.cs`
Expected: a match (confirms the `ProfileCreatedEvent` example cited in the skill is real, not invented).

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/dknet-ddd-principles/SKILL.md
git commit -m "$(cat <<'EOF'
feat(plugin): add dknet-ddd-principles skill for DDD tactical judgment

Existing skills teach this codebase's class shapes but not the judgment
behind them (aggregate boundaries, entity vs value object, invariant
placement, when a domain event is warranted, avoiding anemic models).

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Cross-link dknet-domain-entity and dknet-appservices-actions to dknet-ddd-principles

**Files:**
- Modify: `.claude/skills/dknet-domain-entity/SKILL.md:10-16` and `:276-280`
- Modify: `.claude/skills/dknet-appservices-actions/SKILL.md:10-17` and `:408-412`

**Interfaces:**
- Consumes: skill name `dknet-ddd-principles` (Task 3)
- Produces: nothing new consumed by later tasks — this is a leaf edit.

- [ ] **Step 1: Add a principles pointer to dknet-domain-entity's intro**

In `.claude/skills/dknet-domain-entity/SKILL.md`, the file currently opens with:

```markdown
# Skill: Domain Entity Definition

Create domain entities that integrate with this project's DDD infrastructure — `AggregateRoot`, `DomainEntity`, and owned value objects.

---

## When to Use
```

Change it to:

```markdown
# Skill: Domain Entity Definition

Create domain entities that integrate with this project's DDD infrastructure — `AggregateRoot`, `DomainEntity`, and owned value objects.

If the aggregate boundary, entity-vs-value-object choice, or invariant placement isn't obvious, read **dknet-ddd-principles** first — this skill covers class mechanics, not those judgment calls.

---

## When to Use
```

- [ ] **Step 2: Add a principles pointer to dknet-domain-entity's "Next Steps"**

The file currently ends with:

```markdown
## Next Steps

After creating the domain entity, proceed to:
→ **dknet-efcore-config** skill to create the EF Core mapper configuration
```

Change it to:

```markdown
## Next Steps

After creating the domain entity, proceed to:
→ **dknet-efcore-config** skill to create the EF Core mapper configuration

For the judgment calls behind this entity's shape (aggregate boundary, entity vs. value object, invariant placement), see **dknet-ddd-principles**.
```

- [ ] **Step 3: Add a principles pointer to dknet-appservices-actions' intro**

In `.claude/skills/dknet-appservices-actions/SKILL.md`, the file currently opens with:

```markdown
# Skill: AppServices Actions (CRUD + Business Logic)

Create the application service layer — request/response DTOs, command handlers, validators, query specifications, and domain events — using SlimMessageBus Fluent patterns.

---

## When to Use
```

Change it to:

```markdown
# Skill: AppServices Actions (CRUD + Business Logic)

Create the application service layer — request/response DTOs, command handlers, validators, query specifications, and domain events — using SlimMessageBus Fluent patterns.

If you're unsure whether a business rule belongs on the entity or in the handler, or whether a mutation warrants a domain event, read **dknet-ddd-principles** first — this skill covers the handler mechanics, not that judgment.

---

## When to Use
```

- [ ] **Step 4: Add a principles pointer to dknet-appservices-actions' "Next Steps"**

The file currently ends with:

```markdown
## Next Steps

After creating AppServices actions, proceed to:
→ **dknet-endpoint-config** skill to expose these actions as REST API endpoints
```

Change it to:

```markdown
## Next Steps

After creating AppServices actions, proceed to:
→ **dknet-endpoint-config** skill to expose these actions as REST API endpoints

For the judgment behind business-rule placement and domain event usage, see **dknet-ddd-principles**.
```

- [ ] **Step 5: Verify both files reference the new skill**

Run: `grep -n "dknet-ddd-principles" .claude/skills/dknet-domain-entity/SKILL.md .claude/skills/dknet-appservices-actions/SKILL.md`
Expected: 2 matches in each file (4 total) — one near the top, one in "Next Steps."

- [ ] **Step 6: Commit**

```bash
git add .claude/skills/dknet-domain-entity/SKILL.md .claude/skills/dknet-appservices-actions/SKILL.md
git commit -m "$(cat <<'EOF'
docs(plugin): cross-link domain-entity and appservices-actions to ddd-principles

Points Claude to the judgment-call skill before applying the mechanical
patterns, without duplicating that judgment content into both skills.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Update dknet-architect and dknet-implementer agents to reference the new skills

**Files:**
- Modify: `.claude/agents/dknet-architect.md:15-21`
- Modify: `.claude/agents/dknet-implementer.md:15-24`

**Interfaces:**
- Consumes: skill names `dknet-project-structure` (Task 2), `dknet-ddd-principles` (Task 3)
- Produces: nothing new consumed by later tasks.

- [ ] **Step 1: Update dknet-architect's required reading**

In `.claude/agents/dknet-architect.md`, the "Required reading before you plan" section currently reads:

```markdown
## Required reading before you plan

Always start by reading:
- `CLAUDE.md` and `AGENTS.md` for current layer rules and conventions.
- The existing exemplar slice — `src/ApiEndpoints/Minimal.AppServices/CustomerProfiles/V1/`, `Minimal.Domains/Features/Profiles/Entities/`, `Minimal.Infra/Features/Profiles/`, and `Minimal.Api/ApiEndpoints/CustomerProfileV1Endpoint.cs`.
- The skill that matches the layer you're planning (`.claude/skills/dknet-domain-entity/SKILL.md`, `dknet-efcore-config`, `dknet-appservices-actions`, `dknet-endpoint-config`, `dknet-bdd-tests`, `dknet-unit-test`).
```

Change it to:

```markdown
## Required reading before you plan

Always start by reading:
- `.claude/skills/dknet-project-structure/SKILL.md` for layer boundaries and folder layout.
- `.claude/skills/dknet-ddd-principles/SKILL.md` for aggregate boundary, entity-vs-value-object, invariant, and domain-event judgment calls — apply these when deciding what the new aggregate owns and what triggers an event.
- `CLAUDE.md` and `AGENTS.md` for current layer rules and conventions.
- The existing exemplar slice — `src/ApiEndpoints/Minimal.AppServices/CustomerProfiles/V1/`, `Minimal.Domains/Features/Profiles/Entities/`, `Minimal.Infra/Features/Profiles/`, and `Minimal.Api/ApiEndpoints/CustomerProfileV1Endpoint.cs`.
- The skill that matches the layer you're planning (`.claude/skills/dknet-domain-entity/SKILL.md`, `dknet-efcore-config`, `dknet-appservices-actions`, `dknet-endpoint-config`, `dknet-bdd-tests`, `dknet-unit-test`).
```

- [ ] **Step 2: Add a DDD-judgment line to dknet-architect's output contract (Aggregates & owned types)**

The file currently has this line in the "Output contract" section:

```markdown
1. **Aggregates & owned types** — name, schema prefix for `DomainSchemas`, immutable vs. mutable fields, mutation methods, sequence usage.
```

Change it to:

```markdown
1. **Aggregates & owned types** — name, schema prefix for `DomainSchemas`, immutable vs. mutable fields, mutation methods, sequence usage. State explicitly which fields are entities vs. value objects and why (per `dknet-ddd-principles`), and what the aggregate's consistency boundary is.
```

- [ ] **Step 3: Update dknet-implementer's required reading**

In `.claude/agents/dknet-implementer.md`, the "Required reading before you write code" section currently reads:

```markdown
## Required reading before you write code

Read these in order, every time:
1. `CLAUDE.md` — layer rules and gotchas.
2. The skills for each layer you'll touch:
   - `.claude/skills/dknet-domain-entity/SKILL.md`
   - `.claude/skills/dknet-efcore-config/SKILL.md`
   - `.claude/skills/dknet-appservices-actions/SKILL.md`
   - `.claude/skills/dknet-endpoint-config/SKILL.md`
3. The exemplar slice for any layer where you're unsure:
   - `Minimal.Domains/Features/Profiles/Entities/CustomerProfile.cs`
   - `Minimal.Infra/Features/Profiles/Mappers/`
   - `Minimal.AppServices/CustomerProfiles/V1/Actions/`, `Specs/`, `Events/`
   - `Minimal.Api/ApiEndpoints/CustomerProfileV1Endpoint.cs`
```

Change it to:

```markdown
## Required reading before you write code

Read these in order, every time:
1. `.claude/skills/dknet-project-structure/SKILL.md` — layer boundaries and folder layout.
2. `.claude/skills/dknet-ddd-principles/SKILL.md` — apply this if the architect's plan leaves any aggregate boundary, entity-vs-value-object, or event-vs-direct-call choice implicit.
3. `CLAUDE.md` — layer rules and gotchas.
4. The skills for each layer you'll touch:
   - `.claude/skills/dknet-domain-entity/SKILL.md`
   - `.claude/skills/dknet-efcore-config/SKILL.md`
   - `.claude/skills/dknet-appservices-actions/SKILL.md`
   - `.claude/skills/dknet-endpoint-config/SKILL.md`
5. The exemplar slice for any layer where you're unsure:
   - `Minimal.Domains/Features/Profiles/Entities/CustomerProfile.cs`
   - `Minimal.Infra/Features/Profiles/Mappers/`
   - `Minimal.AppServices/CustomerProfiles/V1/Actions/`, `Specs/`, `Events/`
   - `Minimal.Api/ApiEndpoints/CustomerProfileV1Endpoint.cs`
```

- [ ] **Step 4: Verify both agent files reference the new skills**

Run: `grep -n "dknet-project-structure\|dknet-ddd-principles" .claude/agents/dknet-architect.md .claude/agents/dknet-implementer.md`
Expected: at least 2 matches per file (one for each new skill name).

- [ ] **Step 5: Commit**

```bash
git add .claude/agents/dknet-architect.md .claude/agents/dknet-implementer.md
git commit -m "$(cat <<'EOF'
docs(plugin): point architect and implementer agents at new dknet skills

Both agents now read dknet-project-structure and dknet-ddd-principles
before planning/implementing, so aggregate and event decisions are
judgment-driven rather than pattern-matched from the exemplar alone.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Update README.md's AI assistant plugins section

**Correction (found during Task 6 execution):** the original plan only removed Spec-Kit *commands* (`.claude/commands/speckit-*.md`, done in Task 1) and never checked `.claude/skills/` for Spec-Kit content. `.claude/skills/` also contained 9 stray `speckit-*` directories (`speckit-analyze`, `speckit-checklist`, `speckit-clarify`, `speckit-constitution`, `speckit-implement`, `speckit-plan`, `speckit-specify`, `speckit-tasks`, `speckit-taskstoissues`), making `.claude/skills/` show 18 entries instead of the 9 this plan produces and the README's skill count wrong. Verified via `git log --all -- .claude/skills/speckit-*`: these were empty, never git-tracked (no commit ever added a file under those paths) — local filesystem cruft, not shipped plugin content. Removal is therefore a plain `rm -r`, not a `git rm`, and produces no commit of its own (there is nothing for git to record). Folded into this task since that's where the gap was caught.

**Files:**
- Delete: `.claude/skills/speckit-analyze/`
- Delete: `.claude/skills/speckit-checklist/`
- Delete: `.claude/skills/speckit-clarify/`
- Delete: `.claude/skills/speckit-constitution/`
- Delete: `.claude/skills/speckit-implement/`
- Delete: `.claude/skills/speckit-plan/`
- Delete: `.claude/skills/speckit-specify/`
- Delete: `.claude/skills/speckit-tasks/`
- Delete: `.claude/skills/speckit-taskstoissues/`
- Modify: `README.md:149-171`

**Interfaces:**
- Consumes: final command list from Task 1, final skill count from Tasks 2–3 (7 existing + 2 new = 9)
- Produces: nothing consumed by later tasks.

- [ ] **Step 0: Remove stale speckit-* skill directories**

First check whether git tracks anything under these paths: `git log --all --oneline -- ".claude/skills/speckit-*"`. If that produces no output, they were never committed (empty local directories) — remove with plain `rm -r .claude/skills/speckit-analyze .claude/skills/speckit-checklist .claude/skills/speckit-clarify .claude/skills/speckit-constitution .claude/skills/speckit-implement .claude/skills/speckit-plan .claude/skills/speckit-specify .claude/skills/speckit-tasks .claude/skills/speckit-taskstoissues` (no `git rm`, no commit — there is nothing for git to record). If that command DOES produce output, stop and report NEEDS_CONTEXT — the premise that these are untracked cruft would be wrong and the plan needs to be revisited before deleting tracked content.

Verify: `ls .claude/skills/ | grep -v '.DS_Store'`
Expected: exactly `dknet-appservices-actions`, `dknet-bdd-tests`, `dknet-ddd-principles`, `dknet-domain-entity`, `dknet-efcore-config`, `dknet-endpoint-config`, `dknet-feature-documentation`, `dknet-project-structure`, `dknet-unit-test` (9 entries).

No commit for this step (nothing git-tracked changed).

- [ ] **Step 1: Update the command table and skill/agent count**

In `README.md`, the "AI assistant plugins" section currently reads (lines 149–171):

```markdown
## AI assistant plugins

The repo ships a Claude Code plugin and a GitHub Copilot plugin that drive vertical-slice features end-to-end. Generated solutions include both folders (`.claude/`, `.claude-plugin/`, `.github/`), so your team gets the same agents, skills, and slash commands the template authors use.

### Claude Code

```text
/plugin marketplace add baoduy/dknet.templates
/plugin install dknet-minimal
```

Once installed, the following slash commands are available:

| Command | Purpose |
|---|---|
| `/dknet-feature <Feature> <Entity> [props…]` | Orchestrates a full vertical slice: plan → entity → CRUD → endpoint → tests → BDD → docs |
| `/dknet-entity <Feature> <Entity> [props…]` | Domain entity + EF mapper + migration |
| `/dknet-crud <Feature> <Entity>` | AppServices CRUD (DTO + Create/Update/Delete + spec + event) |
| `/dknet-endpoint <Feature> <Entity>` | Minimal API `IEndpointConfig` with idempotency on POST |
| `/dknet-unit-tests <Feature> <Entity>` | `ApiFixture` + `IMessageBus` integration tests |
| `/dknet-docs <Feature>` | Feature documentation under `docs/features/<feature>/` |

Subagents (`dknet-architect`, `dknet-implementer`, `dknet-bdd-engineer`) and seven domain skills back the commands; see `.claude/agents/` and `.claude/skills/`.
```

Change it to:

```markdown
## AI assistant plugins

The repo ships a Claude Code plugin and a GitHub Copilot plugin that drive vertical-slice features end-to-end. Generated solutions include both folders (`.claude/`, `.claude-plugin/`, `.github/`), so your team gets the same agents, skills, and slash commands the template authors use.

### Claude Code

```text
/plugin marketplace add baoduy/dknet.templates
/plugin install dknet-minimal
```

Once installed, the following slash commands are available:

| Command | Purpose |
|---|---|
| `/dknet-feature <Feature> <Entity> [props…]` | Orchestrates a full vertical slice: plan → entity → CRUD → endpoint → tests → BDD → docs |
| `/dknet-entity <Feature> <Entity> [props…]` | Domain entity + EF mapper + migration |
| `/dknet-crud <Feature> <Entity>` | AppServices CRUD (DTO + Create/Update/Delete + spec + event) |
| `/dknet-endpoint <Feature> <Entity>` | Minimal API `IEndpointConfig` with idempotency on POST |
| `/dknet-unit-tests <Feature> <Entity>` | `ApiFixture` + `IMessageBus` integration tests |
| `/dknet-bdd-test <Feature> <Entity>` | Reqnroll + NUnit BDD scenarios |
| `/dknet-docs <Feature>` | Feature documentation under `docs/features/<feature>/` |

Subagents (`dknet-architect`, `dknet-implementer`, `dknet-bdd-engineer`) and nine domain skills back the commands — including `dknet-project-structure` (layer/folder orientation) and `dknet-ddd-principles` (aggregate boundaries, entity vs. value object, invariants, domain events); see `.claude/agents/` and `.claude/skills/`.
```

- [ ] **Step 2: Verify the section renders the expected counts**

Run: `grep -n "nine domain skills\|dknet-bdd-test <Feature>" README.md`
Expected: 2 matches (one for the updated skill count, one for the added command row).

Run: `ls .claude/skills/ | grep -v '.DS_Store' | wc -l`
Expected: `9` (confirms the skill directory count matches what README now claims).

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "$(cat <<'EOF'
docs: update README plugin section for new skill count and bdd-test command

Skill count moves from seven to nine (dknet-project-structure and
dknet-ddd-principles added); also fixes a pre-existing gap where
/dknet-bdd-test was missing from the command table.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Final consistency verification

**Files:**
- Modify (only if Step 1 finds a leftover reference): the offending file, with a minimal one-line fix.

**Interfaces:**
- Consumes: final state of all files from Tasks 1–6.
- Produces: nothing (terminal task).

- [ ] **Step 1: Confirm no dangling references to removed commands anywhere in the plugin**

Run: `grep -rln "speckit\|dknet-developer" .claude/ README.md SPEC_KIT.md 2>/dev/null`
Expected: `SPEC_KIT.md` only (that file documents the standalone Spec-Kit CLI tool, not the plugin, and is out of scope per the spec's non-goals — no other file should match). If any other file matches, fix it before continuing to Step 2 — do not treat this as an informational-only check.

- [ ] **Step 2: Confirm every skill referenced by the two agents actually exists on disk**

Run:
```bash
for f in dknet-project-structure dknet-ddd-principles dknet-domain-entity dknet-efcore-config dknet-appservices-actions dknet-endpoint-config dknet-bdd-tests dknet-unit-test; do
  test -f ".claude/skills/$f/SKILL.md" && echo "OK: $f" || echo "MISSING: $f"
done
```
Expected: `OK:` for all eight names, no `MISSING:` lines.

- [ ] **Step 3: Confirm the plugin manifest still resolves (directories unchanged)**

Run: `cat .claude-plugin/plugin.json`
Expected: `commands`, `agents`, `skills` keys still point at `./.claude/commands`, `./.claude/agents`, `./.claude/skills` respectively — unchanged from before this plan (manifest edits were explicitly out of scope).

- [ ] **Step 4: Confirm the git history for this plan is clean**

Run: `git log --oneline docs/superpowers/plans/2026-07-08-dknet-plugin-ddd-redesign.md..HEAD` (or, if that range is empty because the plan file itself was touched by a correction commit, `git log --oneline` and visually confirm every commit since the plan was first written is one of: a Task 1–6 commit, or a documented plan-correction commit).
Expected: every commit carries a `Co-Authored-By: Claude Sonnet 5` trailer; no commit is unexplained by a task or a disclosed correction (the plan's Task sections and this file's own edit history note the two known corrections: the missing `speckit-tasks.md` command in Task 1, and the missing `speckit-*` skill directories in Task 6).

**Correction (found during Step 1):** `.claude/agents/dknet-architect.md:34` still reads:

```markdown
7. **Risks & open questions** — anything ambiguous; flag for `/speckit-clarify` if Spec-Kit is in use.
```

Task 5 only touched the architect's "Required reading" section and output-contract item 1 — it missed this Spec-Kit reference in item 7. Since Spec-Kit is fully removed from this plugin (not "used conditionally" — the command no longer exists), fix it:

Change it to:

```markdown
7. **Risks & open questions** — anything ambiguous; surface it here for the user to resolve before implementation begins.
```

Verify: `grep -c "speckit\|dknet-developer" .claude/agents/dknet-architect.md`
Expected: `0`

Commit:

```bash
git add .claude/agents/dknet-architect.md
git commit -m "$(cat <<'EOF'
fix(plugin): remove leftover Spec-Kit reference from dknet-architect

Task 5 updated the architect's required reading and aggregates line
but missed this Spec-Kit reference in the output contract, caught by
Task 7's final grep sweep.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

If Step 1 finds no other stray references, no further commit is needed for this task.
