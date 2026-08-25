---
name: dknet-bdd-tests
description: Create and maintain Reqnroll + NUnit BDD .feature scenarios for DKNet.Templates using specs/contracts as the assertion source of truth and docs/specs as scenario reference context. Use when adding or updating BDD scenarios and step bindings.
---

# Skill: DKNet BDD Scenario Development (Reqnroll + NUnit)

**Duration**: 25-45 minutes | **Difficulty**: Intermediate | **Category**: Testing & Quality

---

## Overview

Use this skill to build high-quality BDD scenarios for `src/ApiEndpoints/Minimal.App.BDDTests/` that are traceable to feature documentation and specifications.

This skill enforces a context-first workflow:
1. Read `docs/features/**` for business intent and architecture context.
2. Read `specs/**` for acceptance criteria, contracts, and edge cases.
3. Draft `.feature` scenarios that map directly to those requirements.
4. Implement matching `[Binding]` steps with deterministic setup and contract-first assertions.

Assertion policy:
- `specs/<feature>/contracts/*` is the source of truth for response assertions.
- `docs/features/**` and `specs/<feature>/spec.md` are reference context for scenario coverage and language only.

---

## When to Use

- Adding a new `.feature` file under `src/ApiEndpoints/Minimal.App.BDDTests/Features/`.
- Expanding existing feature files with new scenarios.
- Aligning existing BDD tests with updated docs/spec requirements.
- Standardizing step text and assertions across the BDD test suite.

---

## Inputs Checklist

Gather these first:

- [ ] Feature name and target domain (for example: `ManualSample` / `PurchaseOrder`, `AutomatedSample` / `Product`).
- [ ] Matching docs folder under `docs/features/<feature-name>/`.
- [ ] Matching spec folder under `specs/<feature-id-or-name>/`.
- [ ] API contract details: route, method, headers, expected response behavior — for a `POST`, check whether the target endpoint is hand-mapped with `.RequiredIdempotentKey()` (mirrors `PurchaseOrderV1Endpoint`, which requires an `X-Idempotency-Key` header on every scenario that creates a purchase order) or generator-driven via `Map<Entity>Crud()` (mirrors `ProductV1Endpoint`, which has no idempotency header at all — omit it from those scenarios).
- [ ] Existing hooks/fixtures in `Minimal.App.BDDTests/Support/`.

**Status on this branch**: no `.feature` files exist yet for either `PurchaseOrder` or `Product` — both samples' BDD coverage is owed at a later Verify cycle, not written at Build. Don't assume a `Features/ManualSample/` or `Features/AutomatedSample/` folder already has scenarios to extend.

---

## Step-by-Step Workflow

### Step 1: Build Context Map From Docs + Specs

Collect sources in this order:

1. `docs/features/<feature>/README.md` (business narrative)
2. `docs/features/<feature>/api-reference.md` (endpoint examples)
3. `docs/features/<feature>/architecture.md` (workflow and boundaries)
4. `specs/<feature>/spec.md` (user stories, acceptance scenarios, edge cases)
5. `specs/<feature>/contracts/*` (canonical request/response and scenario contract)
6. `specs/<feature>/tasks.md` (implementation checks)

Output of this step:
- A scenario matrix: happy path, business-rule failure, validation failure.
- A binding map from Gherkin step text to C# method names.

### Step 2: Resolve Decision Points Before Writing

Use this branching logic:

1. **Docs/spec mismatch**:
   - Prefer `specs/<feature>/contracts/*` for assertions.
   - Use docs/spec only to shape scenario narratives and edge-case coverage.
   - Update docs/spec if mismatch is confirmed.
2. **Contract missing a required assertion field**:
   - Add/align contract first, then implement step assertions.
3. **Step reuse vs new step**:
   - Reuse existing step phrases when semantics are identical.
   - Create new phrases only for genuinely new behavior.

### Step 3: Author the .feature File

Location pattern:
- `src/ApiEndpoints/Minimal.App.BDDTests/Features/<Domain>/<Action>.feature`

Authoring rules:

- Keep one business capability per `.feature` file.
- Include `Background` only for setup shared by all scenarios.
- Use exact, stable domain language from docs/specs.
- Cover at least:
  - Happy path
  - Business-rule failure (for example: duplicate email)
  - Validation failure (for example: missing required field)

### Step 4: Implement Step Definitions

Location pattern:
- `src/ApiEndpoints/Minimal.App.BDDTests/Features/<Domain>/Steps/<Action>Steps.cs`

Implementation rules:

- Add `[Binding]` class with constructor injection for shared context (`HttpClient`, `ScenarioState`, and other registered services).
- Keep step methods concise: arrange input, call API, assert outcome.
- Serialize requests using `SharedConsts.JsonSerializerOptions`.
- Include idempotency header where required (`X-Idempotency-Key`).
- Parse response JSON with deterministic property assertions against the contract.
- Validate response at three levels whenever applicable:
   - HTTP status code
   - response structure (`isSuccess`, `value`, `errors`, required object/array shape)
   - key data fields and values (for example `value.name`, `errors[0].message`, identifiers, totals)
- For generated response DTO contracts, assert representative entity-derived fields to catch DTO/property drift.
- Avoid assertion patterns that only check substring existence when contract defines structured fields.

### Step 5: Validate End-to-End

Run and verify:

1. `dotnet build src/DKNet.Templates.sln -c Release`
2. `dotnet test src/ApiEndpoints/Minimal.App.BDDTests`

Then confirm:

- Scenario names are discoverable in test output.
- No undefined/pending steps.
- Scenario outcomes match spec acceptance criteria.

---

## Completion Criteria

A BDD scenario set is complete when:

- Every scenario traces back to `docs/features` and `specs` artifacts.
- `.feature` step text and `[Binding]` attributes match exactly.
- Assertions are contract-first and verify HTTP status, response structure, and key data fields.
- Tests pass with no external infrastructure dependency.

See validation checklist: [checklist.md](./checklist.md)

---

## Common Errors and Fixes

### Error: Scenario passes but business assertion is weak

Cause: Asserting only `IsSuccessStatusCode` or plain string fragments without validating contract-defined JSON shape.

Fix: Parse JSON and assert contract fields (`isSuccess`, `value`, `errors`, nested fields and required values).

### Error: Undefined step bindings

Cause: Step text changed in `.feature` but attributes in step class were not updated.

Fix: Keep step phrases centralized and copy exact text into `[Given]/[When]/[Then]` attributes.

### Error: 400/401/409 unexpectedly

Cause: Missing required headers or test host configuration mismatch.

Fix: Verify idempotency header and `Support/ApiHooks.cs` + `Support/BddApiFactory.cs` setup.

---

## Related Skills

- [AppServices Actions](../dknet-appservices-actions/SKILL.md)
- [Endpoint Config](../dknet-endpoint-config/SKILL.md)
- [Feature Documentation](../dknet-feature-documentation/skill.md)
