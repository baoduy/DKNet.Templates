# DKNet Claude Code Plugin — DDD-Focused Redesign

> **2026-08-25 note:** This document is a historical record from when the template's demo
> features were `CustomerProfile`/`LoyaltyMembership`. Those were removed; current worked
> examples are the `PurchaseOrder` (hand-written) and `Product` (generator-driven) samples —
> see `docs/samples/manual-vs-automated.md`.

## Context

`.claude-plugin/dknet-minimal` already exists (added in commit `c73515a`, ported from `.github/skills/`). It ships 3 agents, 7 skills, and ~18 commands (including a full Spec-Kit command set and a `dknet-developer` orchestrator that drives Spec-Kit).

The existing skills are solid on this codebase's *mechanics* (e.g. `dknet-domain-entity` correctly documents the `AggregateRoot`/`DomainEntity` inheritance, private setters, mutation-only-via-methods, rehydration constructors) but don't teach DDD *judgment*: how to decide aggregate boundaries, when something is an entity vs. a value object, why an invariant belongs where it does, when a domain event is warranted vs. a direct call, how to avoid an anemic domain model.

Decision: replace the plugin content entirely, keeping the plugin id `dknet-minimal` (README and `marketplace.json` already document this install path — renaming breaks existing docs for no benefit). Speckit-* commands and the `dknet-developer` orchestrator are dropped; `/dknet-feature` remains the single top-level workflow entry point and does not depend on Spec-Kit.

## Goals

- Claude can understand this template's layer structure and vertical-slice conventions without re-deriving them each session.
- Claude applies DDD tactical judgment (aggregate boundaries, entity vs. value object, invariants, domain events, avoiding anemic models) when implementing features in this codebase — not just pattern-matching class shapes.
- The plugin stays a single coherent tool: skills teach conventions, agents orchestrate multi-step work, commands are quick entry points.

## Non-goals

- Strategic DDD (bounded contexts, context mapping, ubiquitous language across services) — this template is a single microservice/single bounded context.
- Spec-Kit integration — dropped by decision, not deferred.
- Changes to the GitHub Copilot plugin under `.github/skills/` (out of scope; this spec is Claude Code only).

## Design

### Skills (9 total)

**New:**

1. **`dknet-project-structure`** — orientation skill. Layer boundaries (`Api → AppServices → Domains ← Infra`, `Share` underneath), the vertical-slice folder table (Domains/Infra/AppServices/Api per feature), and the key auto-discovery wiring points (EF Core mapper/seeding scan via `UseAutoConfigModel`/`UseAutoDataSeeding`, Scrutor service scan, endpoint fluent helpers `MapGetList`/`MapGetById`/`MapPost`/`MapPut`/`MapDelete`). This is the "read first" skill so structure doesn't need to be re-derived from `CLAUDE.md`/`AGENTS.md` each time.
2. **`dknet-ddd-principles`** — foundational judgment skill, referenced by the two skills below rather than duplicated into them. Covers:
   - Aggregate boundaries: what must be transactionally consistent together stays in one aggregate; cross-aggregate consistency goes through domain events, not direct references.
   - Entity vs. value object: identity-and-lifecycle (entity, e.g. `CustomerProfile`) vs. equality-by-value with no independent identity (owned type, e.g. an address).
   - Invariant enforcement: why mutation happens only through named methods (`Update(...)`), never public setters — the entity protects its own consistency.
   - When a domain event is warranted (side effects other aggregates/handlers care about) vs. when a direct method call suffices (no external interest).
   - Avoiding anemic domain models: behavior belongs on the entity, not smeared across command handlers.

**Refreshed (mechanics kept, add a pointer to `dknet-ddd-principles` for the "why"):**

3. `dknet-domain-entity` — `AggregateRoot`/`DomainEntity` mechanics
4. `dknet-appservices-actions` — CQRS actions/validators/specs/domain event handlers

**Unchanged:**

5. `dknet-efcore-config`
6. `dknet-endpoint-config`
7. `dknet-unit-test`
8. `dknet-bdd-tests`
9. `dknet-feature-documentation`

### Agents (3, kept)

- `dknet-architect` — updated to explicitly consult `dknet-ddd-principles` when identifying aggregates/events/specs, and `dknet-project-structure` for layer placement.
- `dknet-implementer` — same update.
- `dknet-bdd-engineer` — unchanged; no DDD-judgment role.

### Commands (7, dropped speckit-* and dknet-developer)

`/dknet-feature`, `/dknet-entity`, `/dknet-crud`, `/dknet-endpoint`, `/dknet-unit-tests`, `/dknet-bdd-test`, `/dknet-docs`.

`/dknet-feature` already orchestrates entity → CRUD → endpoint → tests → BDD → docs without touching Spec-Kit, so it is unaffected by the removal and becomes the sole top-level workflow command.

## File changes

**Delete:**
- `.claude/commands/speckit-*.md` (9 files: analyze, architecture, checklist, clarify, constitution, implement, plan, specify, taskstoissues)
- `.claude/commands/dknet-developer.md`

**Add:**
- `.claude/skills/dknet-project-structure/SKILL.md`
- `.claude/skills/dknet-ddd-principles/SKILL.md`

**Edit:**
- `.claude/skills/dknet-domain-entity/SKILL.md` — add reference to `dknet-ddd-principles`
- `.claude/skills/dknet-appservices-actions/SKILL.md` — add reference to `dknet-ddd-principles`
- `.claude/agents/dknet-architect.md` — reference new skills
- `.claude/agents/dknet-implementer.md` — reference new skills
- `README.md` §"AI assistant plugins" — update skill count (seven → nine), add missing `/dknet-bdd-test` row to the command table (pre-existing gap, fixed while already editing this section)

**Untouched:**
- `.claude/skills/dknet-efcore-config`, `dknet-endpoint-config`, `dknet-unit-test`, `dknet-bdd-tests`, `dknet-feature-documentation`
- `.claude/agents/dknet-bdd-engineer.md`
- `.claude-plugin/plugin.json`, `.claude-plugin/marketplace.json` — no structural change needed; both reference directories (`./.claude/commands`, `./.claude/agents`, `./.claude/skills`) rather than individual files, so additions/removals inside those directories don't require manifest edits
- `src/DKNet.Minimal.Template.nuspec`, `src/DKNet.Minimal.Template.csproj` — packaging uses `**/*` globs over the same directories, no per-file references to update

## Validation

No automated test harness applies to plugin content (it's prompts/docs, not executable code). "Verified" means:

1. Each new/edited `SKILL.md`'s factual claims about the codebase (class shapes, method names, file paths) are checked against the actual source, the same way `dknet-domain-entity` was cross-checked against `AggregateRoot`/`DomainEntity`/`CustomerProfile` during design.
2. A manual dry run: invoke `/dknet-entity` (or the `dknet-architect` agent) on a toy feature and confirm the output reflects DDD-sound decisions (correct aggregate boundary, entity vs. value object choice) and that the agent's reasoning cites `dknet-ddd-principles` where relevant.
3. `README.md`'s command table and skill count match the shipped files exactly.

## Open risks

- Consumers who already generated a project from an older template version have the old plugin baked in; this redesign only affects the template repo and future `dotnet new` runs, not already-generated solutions. Not addressed by this spec — out of scope.
