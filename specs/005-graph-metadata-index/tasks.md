# Tasks: C# Metadata Index In Neo4j

**Input**: Design documents from `/specs/005-graph-metadata-index/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/graph-metadata-contract.md`

**Tests**: Explicit test tasks are included for script-level validation and Neo4j integration validation.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create task scaffolding, validation entrypoints, and documentation anchors used by all stories.

- [ ] T001 Create graph metadata query pack index in graph/queries/README.md
- [ ] T002 [P] Create script-level validation runner entrypoint in graph/scripts/run-script-validation.sh
- [ ] T003 [P] Create Neo4j integration validation runner entrypoint in graph/scripts/run-neo4j-validation.sh
- [ ] T004 [P] Add graph test fixture seed file for parser validation in graph/tests/fixtures/sample-metadata.cs
- [ ] T005 Update feature quickstart command skeleton for setup and validation entrypoints in specs/005-graph-metadata-index/quickstart.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement analyzer infrastructure that blocks all user stories until complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T006 Implement analyzer CLI argument handling and environment validation in graph/analyze.mjs
- [ ] T007 Implement C# file discovery under src/ with normalized repository-relative paths in graph/analyze.mjs
- [ ] T008 [P] Implement stable identity key builders for SourceFile, ClassSymbol, and MethodSymbol in graph/lib/identity-keys.mjs
- [ ] T009 [P] Implement metadata allowlist and forbidden body-property guard in graph/lib/property-guard.mjs
- [ ] T010 Implement Neo4j transactional client wrapper for parameterized Cypher execution in graph/lib/neo4j-client.mjs
- [ ] T011 Implement MERGE payload contract builders for nodes and containment relationships in graph/lib/upsert-contract.mjs
- [ ] T012 [P] Implement run summary metrics formatter (files/classes/methods/failures) in graph/lib/run-summary.mjs
- [ ] T013 Wire discovery, parsing, guarded upsert, and summary output pipeline in graph/analyze.mjs

**Checkpoint**: Foundation complete; user stories can start.

---

## Phase 3: User Story 1 - Build Metadata Index Safely (Priority: P1) 🎯 MVP

**Goal**: Index class/method/file metadata into Neo4j while guaranteeing no source body persistence.

**Independent Test**: Run script-level validation then one analyzer run and verify required metadata exists while forbidden body-like fields are absent.

### Tests for User Story 1

- [ ] T014 [P] [US1] Add parser metadata extraction test for class/method/file fields in graph/tests/script/parse-metadata.test.mjs
- [ ] T015 [P] [US1] Add no-body guard test for forbidden properties and payload text in graph/tests/script/no-body-guard.test.mjs
- [ ] T016 [US1] Wire script-level test execution flow and non-zero failure handling in graph/scripts/run-script-validation.sh

### Implementation for User Story 1

- [ ] T017 [US1] Implement C# metadata parser for classes and methods without body capture in graph/lib/csharp-parser.mjs
- [ ] T018 [US1] Implement SourceFile/ClassSymbol/MethodSymbol property mapping contract in graph/lib/upsert-contract.mjs
- [ ] T019 [US1] Integrate parser outputs with guarded upsert pipeline in graph/analyze.mjs
- [ ] T020 [US1] Add no-body Cypher validation query pack in graph/queries/validation/no-body.cypher
- [ ] T021 [US1] Update metadata contract required/forbidden fields in specs/005-graph-metadata-index/contracts/graph-metadata-contract.md
- [ ] T022 [US1] Document script-level validation workflow and expected results in specs/005-graph-metadata-index/quickstart.md

**Checkpoint**: User Story 1 is independently testable and meets confidentiality guarantees.

---

## Phase 4: User Story 2 - Query Code Locations From Graph (Priority: P2)

**Goal**: Provide reusable query packs and integration validation for class/method-to-file navigation.

**Independent Test**: Run Neo4j integration validation and verify class and method lookups return owning class and file location data.

### Tests for User Story 2

- [ ] T023 [P] [US2] Add Neo4j graph-shape integration validation script for required nodes and relationships in graph/tests/integration/validate-graph-shape.mjs
- [ ] T024 [P] [US2] Add lookup query smoke-test script for class and method search scenarios in graph/tests/integration/query-smoke.test.mjs
- [ ] T025 [US2] Wire Neo4j integration test execution and exit status handling in graph/scripts/run-neo4j-validation.sh

### Implementation for User Story 2

- [ ] T026 [US2] Add class-to-file reusable lookup query in graph/queries/lookup/class-to-file.cypher
- [ ] T027 [US2] Add method-to-class reusable lookup query in graph/queries/lookup/method-to-class.cypher
- [ ] T028 [P] [US2] Add method-to-file reusable lookup query in graph/queries/lookup/method-to-file.cypher
- [ ] T029 [US2] Implement query pack loader utility for ordered execution in graph/lib/query-pack-loader.mjs
- [ ] T030 [US2] Integrate query pack loading with integration smoke tests in graph/tests/integration/query-smoke.test.mjs
- [ ] T031 [US2] Document graph lookup and validation commands for developers in graph/README.md
- [ ] T032 [US2] Update Neo4j integration validation instructions in specs/005-graph-metadata-index/quickstart.md

**Checkpoint**: User Story 2 is independently testable with validated graph lookup flows.

---

## Phase 5: User Story 3 - Operate Entirely From graph/ Artifacts (Priority: P3)

**Goal**: Keep implementation isolated to graph/ assets while validating idempotency and operational repeatability.

**Independent Test**: Run artifact-scope audit and rerun indexing on unchanged input to verify idempotent counts and graph-only implementation assets.

### Tests for User Story 3

- [ ] T033 [P] [US3] Add artifact scope audit script enforcing graph/ implementation paths and allowed specs docs paths in graph/tests/script/artifact-scope-audit.mjs
- [ ] T034 [P] [US3] Add Neo4j idempotency validation script comparing repeated-run node/relationship counts in graph/tests/integration/idempotency-validation.mjs

### Implementation for User Story 3

- [ ] T035 [US3] Add unified validation orchestrator for script-level and Neo4j checks in graph/scripts/validate-all.sh
- [ ] T036 [US3] Update graph bootstrap workflow to support validate flag and analyzer rerun checks in graph/load.sh
- [ ] T037 [US3] Document full graph-only workflow, including idempotency validation, in graph/README.md
- [ ] T038 [US3] Update feature research notes with graph-only implementation boundary and audit guidance in specs/005-graph-metadata-index/research.md

**Checkpoint**: User Story 3 is independently testable and confirms graph-only implementation boundaries.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation hardening, performance sanity checks, and documentation consistency.

- [ ] T039 [P] Add integration performance smoke check for validation completion under SC-005 target in graph/tests/integration/perf-smoke.test.mjs
- [ ] T040 [P] Add run-metrics validation query pack for failed file diagnostics and count auditing in graph/queries/validation/run-metrics.cypher
- [ ] T041 Update quickstart final verification gate covering script-level plus Neo4j integration checks in specs/005-graph-metadata-index/quickstart.md
- [ ] T042 Update operational command reference and troubleshooting for validators in graph/README.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies.
- **Phase 2 (Foundational)**: Depends on Phase 1; blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2.
- **Phase 4 (US2)**: Depends on Phase 2 and uses US1 metadata contracts.
- **Phase 5 (US3)**: Depends on Phase 2 and builds on US1/US2 validation assets.
- **Phase 6 (Polish)**: Depends on completion of Phases 3, 4, and 5.

### User Story Dependencies

- **US1 (P1)**: Starts immediately after Foundational.
- **US2 (P2)**: Starts after Foundational; consumes US1 metadata guarantees.
- **US3 (P3)**: Starts after Foundational; consumes US1/US2 validators for full workflow checks.

### Within Each User Story

- Test tasks execute before implementation tasks.
- Parser/contract updates precede analyzer wiring.
- Query definitions precede query-runner integration.
- Validation orchestration follows creation of individual validators.

### Parallel Opportunities

- **Setup**: T002, T003, and T004 can run in parallel after T001.
- **Foundational**: T008, T009, and T012 can run in parallel after T007.
- **US1**: T014 and T015 can run in parallel before T016.
- **US2**: T023 and T024 can run in parallel before T025.
- **US3**: T033 and T034 can run in parallel before T035.
- **Polish**: T039 and T040 can run in parallel before T041 and T042.

---

## Parallel Example: User Story 1

```bash
# Run US1 script-level tests in parallel:
Task T014: graph/tests/script/parse-metadata.test.mjs
Task T015: graph/tests/script/no-body-guard.test.mjs
```

## Parallel Example: User Story 2

```bash
# Build US2 Neo4j integration validators in parallel:
Task T023: graph/tests/integration/validate-graph-shape.mjs
Task T024: graph/tests/integration/query-smoke.test.mjs
```

## Parallel Example: User Story 3

```bash
# Run US3 scope and idempotency validation in parallel:
Task T033: graph/tests/script/artifact-scope-audit.mjs
Task T034: graph/tests/integration/idempotency-validation.mjs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3 (US1), including script-level validation tasks.
3. Validate metadata-only persistence and no-body guarantees.
4. Demo MVP indexing safety.

### Incremental Delivery

1. Deliver US1 metadata-safe indexing.
2. Deliver US2 query packs and Neo4j integration validation.
3. Deliver US3 graph-only boundary audit and idempotency orchestration.
4. Finish with Polish phase checks and docs alignment.

### Parallel Team Strategy

1. Team completes Setup and Foundational phases together.
2. One developer can own US1 parser/contract work while another prepares US2 query packs once Foundational is complete.
3. US3 validation orchestration can start after foundational validators from US1/US2 are in place.
