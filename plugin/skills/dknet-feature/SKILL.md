---
name: dknet-feature
description: Drive an end-to-end DKNet vertical-slice feature from plan to merged tests in either the manual or automated flow — orchestrates entity, CRUD, endpoint, tests, BDD, and docs.
metadata:
  kind: workflow
  arguments: "<Feature> <Entity> [mode=manual|auto] [props…] e.g. Orders Order mode=manual Number:string Total:decimal"
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Agent, TodoWrite
---

Usage: `/dknet-feature <Feature> <Entity> [mode=manual|auto] [props…] e.g. Orders Order mode=manual Number:string Total:decimal`

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

The `dknet-feature-lifecycle` skill — §1 flow selection, §2 footprint. Read before Phase 0.

## Phase 0 — Flow selection

The **mode** decides what every later phase produces. The two flows generate a different set of
files, different validation behavior, and different acting-user attribution — they are not two styles
of the same output.

| Mode | Exemplar | Shape |
|---|---|---|
| `manual` | `ManualSample` / `PurchaseOrder` | Every request, validator, handler, spec, DTO, and route is a file you write. Enforced validation, `.RequiredIdempotentKey()` on create, `[FromClaim]` acting user. |
| `auto` | `AutomatedSample` / `Product` | `[RaisesEvent]` / `[CrudCreate]` / `[CrudUpdate]` / `[CrudAction]` on the entity plus a one-line `[GenerateDto]`. Requests, handlers, and routes are generated. No idempotency, **forwarded DataAnnotations not enforced** (a FluentValidation validator on a generated request *is*), acting user via `DataOwnerHook`. |

If `mode=` was not supplied, apply §1 of the lifecycle skill, **recommend one with a reason**, and ask
the user to confirm. **Default to `auto`.** Recommend `manual` only when the request actually needs
one of the five things the generator cannot express: an operation writing more than one aggregate in
one transaction, idempotent writes, a query beyond the generic list route's
`filter`/`search`/`orderBy` contract, `[FromClaim]` acting-user attribution, or attribute-declared
validation that must return `400` and cannot be restated as a FluentValidation rule. A business
rule, state transition, duplicate check or a DTO that hides fields does **not** force `manual`:
write the rule as a FluentValidation validator on the generated request (as the product sample
refuses a duplicate name and a delete of a product still for sale) and narrow the DTO with
`[GenerateDto(..., Exclude = [...])]`.

When `auto` is selected, state the validation gap in your confirmation message: a `[Range]` on a
generated request property is forwarded but never enforced, so a `POST` with an invalid value returns
`201`, not `400`. The gap is the attribute only — FluentValidation still runs on generated routes. The user accepts that before Phase 2 starts.

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
- `manual` — every event is raised at the highest rung that fits: `[RaisesEvent]` where the event is
  "created" or "these properties changed", else `AddEvent<TEvent>()` with the payload projected from
  the entity, and a hand-built `AddEvent(new …)` only where the payload is not a projection.
- `auto` — class-level `[RaisesEvent(...)]`, a `[CrudCreate]` constructor, and at least one
  `[CrudUpdate]` method are present. No `AddEvent` call anywhere in the slice.

### Phase 3 — AppServices CRUD

Run `/dknet-crud <Feature> <Entity> mode=<mode>`. Verify:
- `manual` — Create/Update/Delete requests + validators + `internal sealed` handlers, `SpecGet<Entity>`, queries, hand-written DTO record, event handler.
- `auto` — exactly one `[GenerateDto(typeof(<Entity>))] public sealed partial record <Entity>Dto;` and any hand-written event *consumer*. Then `dotnet build` and confirm the expected types appeared under `obj/Generated/DKNet.SlimBus.Generators/`. An empty generated folder means the attributes did not take — STOP.

Build green either way.

### Phase 4 — Endpoint

Run `/dknet-endpoint <Feature> <Entity> mode=<mode>`. Verify the new `*V1Endpoint : IEndpointConfig` exists and:
- `manual` — every route is a literal `group.MapPost/MapGet/MapPut/MapDelete(...)` call, and the create route chains `.RequiredIdempotentKey()`. Confirm each hand-mapped route is one no CRUD attribute and no generic `Map*` helper could have covered; report any that could have been generated.
- `auto` — the body is a single `group.Map<Entity>Crud()` call. There is no `.RequiredIdempotentKey()` on this path; do not add one, it will not compile onto the generated route.

Authorization scopes, either mode: declared with `[EndpointGroupScope]` on the endpoint class where
the package version supports it, dropping to `o.Configure(...)`/`.RequireAuthorization(...)` — each
behind the `FeatureOptions.RequireAuthorization` guard — only for a route whose HTTP method cannot
decide its scope.

Build green.

### Phase 5 — Unit/integration tests

Run `/dknet-unit-tests <Feature> <Entity> mode=<mode>`. xUnit here covers only what a BDD scenario
cannot: architecture/convention rules, pure functional tests (entity methods, validators, specs),
EF model/schema shape, and a `Result`-level assertion where the HTTP response cannot tell two
failures apart. Every business rule reachable over HTTP is Phase 6's job — do not write it twice.
- `manual` — validator rules and rejected state transitions asserted at the unit level.
- `auto` — entity-method behavior directly. Do **not** write a test asserting a `400` from a forwarded DataAnnotations attribute; it will return `201` and the test would encode the gap as expected behavior.

### Phase 6 — BDD acceptance tests

Dispatch the `dknet-bdd-engineer` subagent with the feature scope. **This is where the feature's
business rules are specified** — happy path, every refusal, every state transition, and the
domain-event side effects visible in captured logs. Verify `.feature` + step files exist with
status + shape + key-field assertions, that every route from Phase 4 appears in at least one
scenario, and that the BDD project passes.

### Phase 7 — Feature documentation

Run `/dknet-docs <Feature>`. Verify README, architecture diagrams, data-model, and api-reference
exist under `docs/features/<feature-kebab>/` (or `docs/<feature>/` if internal), that
`api-reference.md` has a section for **every** route Phase 4 published, and that the architecture,
sequence and event diagrams are archify renders committed with their JSON sources under
`diagrams/` (Mermaid only if archify could not be installed — the report must say so).

### Phase 8 — Final gates

1. `dotnet build -c Release` — zero warnings (warnings-as-errors).
2. `dotnet test --settings coverage.runsettings` — all green.
3. Print a final report:
   - Mode used, and the one-line reason it was chosen.
   - Files created/edited grouped by layer.
   - Migration name + tables.
   - Endpoints (route + verbs), flagging whether the create route is idempotent.
   - Test counts (unit + BDD).
   - Docs paths, and whether the diagrams are archify renders or Mermaid fallbacks.
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
- Never edit agent skill/plugin folders or `Directory.Packages.props` as part of a feature slice — those are template-level concerns.
