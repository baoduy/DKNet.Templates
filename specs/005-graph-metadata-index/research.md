# Phase 0 Research: C# Metadata Index In Neo4j

## Decision 1: Restrict implementation scope to graph/

- Decision: Keep analyzer scripts, query packs, validation scripts, and docs under `graph/` only.
- Rationale: Directly satisfies FR-007 and keeps tooling isolated from runtime projects.
- Alternatives considered:
  - Place tests/docs under `src/` test projects: rejected because the feature is tooling workflow, not runtime app behavior.

## Decision 2: Scan only C# metadata from src/

- Decision: Analyzer input scope is C# files under `src/`; graph records include class and method metadata plus file location.
- Rationale: Aligns to rewritten feature objective and FR-001/FR-002 while minimizing unnecessary parse surface.
- Alternatives considered:
  - Continue indexing markdown metadata: rejected because rewritten scope is C# metadata indexing.
  - Whole-repo scan outside `src/`: rejected to avoid noise and accidental ingestion of non-target files.

## Decision 3: No source body persistence by contract

- Decision: Persist only identifiers and location fields (`name`, `path`, ownership relationships, and run metrics) and explicitly forbid source body text properties.
- Rationale: Required by FR-006 and SC-002; must be testable through Cypher validation scripts.
- Alternatives considered:
  - Storing snippets or hashes: rejected due to confidentiality risk or low value.

## Decision 4: Use Docker Neo4j workflow as the integration baseline

- Decision: Integration path is `graph/docker-compose.yml` + `graph/load.sh` for local Neo4j bootstrap and analyzer execution.
- Rationale: Satisfies hard constraints and creates a reproducible validation environment.
- Alternatives considered:
  - Embedded/in-memory graph for tests: rejected because operational target is Docker-hosted Neo4j.

## Decision 5: Two-layer validation strategy (script + integration)

- Decision: Define script-level validation for parser/identity logic and integration validation for persisted graph shape/content.
- Rationale: Covers FR-008/FR-010/FR-011 and gives confidence before and after Neo4j writes.
- Alternatives considered:
  - Integration-only checks: rejected due to slower feedback and weaker parser failure isolation.
  - Script-only checks: rejected because persistence/relationship correctness would remain unverified.

## Decision 6: Idempotent indexing via stable merge keys

- Decision: Merge keys are repository-relative file path for files, `(project,class)` identity for class symbols, and `(project,class,method,signature-or-arity)` identity for methods.
- Rationale: Supports FR-010 and SC-003 by preventing uncontrolled duplicates on repeated runs.
- Alternatives considered:
  - Full graph reset on each run: rejected because it prevents lightweight reruns and obscures duplicate detection.
