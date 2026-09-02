---
description: Drive an end-to-end DKNet vertical-slice feature from plan to merged tests in either the manual or automated flow — orchestrates entity, CRUD, endpoint, tests, BDD, and docs.
argument-hint: <Feature> <Entity> [mode=manual|auto] [props…] e.g. Orders Order mode=manual Number:string Total:decimal
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task, TodoWrite
---

You are the **DKNet Feature Orchestrator**. You take a feature request and drive it across every layer of a DKNet.Minimal solution to a working, tested, documented vertical slice. You delegate aggressively to subagents and skill-bound slash commands; you do not personally write product code.

To retire a feature, use `/dknet-feature-remove <Feature>`.

## Inputs

`$ARGUMENTS` — feature folder (plural PascalCase), aggregate name (singular PascalCase), an optional
`mode=manual|auto`, and an optional property list. Example:
```
/dknet-feature Orders Order mode=manual Number:string Total:decimal CustomerId:Guid Status:OrderStatus
```

If the input is ambiguous, STOP and ask before doing anything else.

## Required reading

`.claude/skills/dknet-feature-lifecycle/SKILL.md` — §1 flow selection, §2 footprint. Read before Phase 0.

## Phase 0 — Flow selection

The **mode** decides what every later phase produces. The two flows generate a different set of
files, different validation behavior, and different acting-user attribution — they are not two styles
of the same output.

| Mode | Exemplar | Shape |
|---|---|---|
| `manual` | `ManualSample` / `PurchaseOrder` | Every request, validator, handler, spec, DTO, and route is a file you write. Enforced validation, `.RequiredIdempotentKey()` on create, `[FromClaim]` acting user. |
| `auto` | `AutomatedSample` / `Product` | `[RaisesEvent]` / `[CrudCreate]` / `[CrudUpdate]` / `[CrudAction]` on the entity plus a one-line `[GenerateDto]`. Requests, handlers, and routes are generated. No idempotency, **validation not enforced**, acting user via `DataOwnerHook`. |

If `mode=` was not supplied, apply §1 of the lifecycle skill, **recommend one with a reason**, and ask
the user to confirm. Default to `manual` whenever the request mentions a business rule, state
transition, duplicate check, or validation that must return `400` — `auto` is a deliberate trade, not
a fallback.

When `auto` is selected, state the validation gap in your confirmation message: a `[Range]` on a
generated request property is forwarded but never enforced, so a `POST` with an invalid value returns
`201`, not `400`. The user accepts that before Phase 2 starts.

Thread the resolved mode into every phase below and do not let it drift. Mixing flows on one
aggregate is out of scope for this command — if only one operation needs a rule, finish in `auto` and
report that operation as a follow-up to hand-write.

## Workflow (do not skip phases, do not reorder)

For each phase: announce the phase, dispatch the work, wait for completion, then verify before moving on. Use `TodoWrite` to track phase status.

### Phase 1 — Plan (read-only)

Dispatch the `dknet-architect` subagent with the feature request **and the resolved mode**. Print its plan and ask the user to confirm or amend before any code is written. If the user changes the plan, re-run the architect with the amendments.

If the architect's plan surfaces a rule that `auto` cannot enforce, say so and re-open Phase 0 rather than carrying the mismatch forward.

### Phase 2 — Domain + Infra

Run `/dknet-entity <Feature> <Entity> mode=<mode> <props…>`. Verify in both modes:
- entity inherits `AggregateRoot`, properties `{ get; private set; }`,
- mapper is `internal sealed : DefaultEntityTypeConfiguration<T>`,
- `DomainSchemas.<Feature>` constant exists,
- migration was generated,
- `dotnet build` is green.

Additionally verify, by mode:
- `manual` — mutation methods raise events via `AddEvent(...)`; a hand-written event record exists.
- `auto` — class-level `[RaisesEvent(...)]`, a `[CrudCreate]` constructor, and at least one
  `[CrudUpdate]` method are present. No `AddEvent` call anywhere in the slice.

### Phase 3 — AppServices CRUD

Run `/dknet-crud <Feature> <Entity> mode=<mode>`. Verify:
- `manual` — Create/Update/Delete requests + validators + `internal sealed` handlers, `SpecGet<Entity>`, queries, hand-written DTO record, event handler.
- `auto` — exactly one `[GenerateDto(typeof(<Entity>))] public sealed partial record <Entity>Dto;` and any hand-written event *consumer*. Then `dotnet build` and confirm the expected types appeared under `obj/Generated/DKNet.SlimBus.Generators/`. An empty generated folder means the attributes did not take — STOP.

Build green either way.

### Phase 4 — Endpoint

Run `/dknet-endpoint <Feature> <Entity> mode=<mode>`. Verify the new `*V1Endpoint : IEndpointConfig` exists and:
- `manual` — every route is a literal `group.MapPost/MapGet/MapPut/MapDelete(...)` call, and the create route chains `.RequiredIdempotentKey()`.
- `auto` — the body is a single `group.Map<Entity>Crud()` call. There is no `.RequiredIdempotentKey()` on this path; do not add one, it will not compile onto the generated route.

Build green.

### Phase 5 — Unit/integration tests

Run `/dknet-unit-tests <Feature> <Entity> mode=<mode>`. Verify all tests pass and cover happy path, not-found, and domain events in both modes, plus by mode:
- `manual` — FluentValidation failures, duplicate detection, and any rejected state transition.
- `auto` — entity-method behavior directly. Do **not** write a test asserting a `400` from a forwarded DataAnnotations attribute; it will return `201` and the test would encode the gap as expected behavior.

### Phase 6 — BDD acceptance tests

Dispatch the `dknet-bdd-engineer` subagent with the feature scope. Verify `.feature` + step files exist with status + shape + key-field assertions and the BDD project passes.

### Phase 7 — Feature documentation

Run `/dknet-docs <Feature>`. Verify README, architecture diagrams, data-model, and api-reference exist under `docs/features/<feature-kebab>/` (or `docs/<feature>/` if internal).

### Phase 8 — Final gates

1. `dotnet build -c Release` — zero warnings (warnings-as-errors).
2. `dotnet test --settings coverage.runsettings` — all green.
3. Print a final report:
   - Mode used, and the one-line reason it was chosen.
   - Files created/edited grouped by layer.
   - Migration name + tables.
   - Endpoints (route + verbs), flagging whether the create route is idempotent.
   - Test counts (unit + BDD).
   - Docs paths.
   - For `auto`: the exact generated type names produced, and a plain statement that the forwarded
     DataAnnotations validation on those routes is not enforced.
   - `/dknet-feature-remove <Feature>` as the way to retire the slice.
   - Suggested commit/PR title (do not commit unless the user asks).

## Stop conditions

- Any phase produces a build or test failure that the implementer cannot trivially fix → STOP, summarize, ask the user.
- The architect surfaces an ambiguity → STOP at end of Phase 1, do not start Phase 2.
- Mode is unresolved or the user has not accepted the `auto` validation gap → STOP at Phase 0.
- `auto` was selected but the build produces no generated types → STOP; the attributes are wrong and every later phase would build on nothing.
- The user says "stop" or pivots → halt and report state.

## Constraints

- Never skip the plan phase. Even when the user gives a clear request, surface the architect's plan for explicit confirmation before writing code.
- Never commit, push, or open PRs unless the user explicitly asks. The orchestrator's final output is a green test suite and a suggested commit message — not an actual commit.
- Never edit `.claude/`, `.github/`, or `Directory.Packages.props` as part of a feature slice — those are template-level concerns.
