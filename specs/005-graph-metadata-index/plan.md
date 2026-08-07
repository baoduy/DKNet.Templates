# Implementation Plan: C# Metadata Index In Neo4j

**Branch**: `005-graph-metadata-index` | **Date**: 2026-04-01 | **Spec**: [/specs/005-graph-metadata-index/spec.md](/specs/005-graph-metadata-index/spec.md)
**Input**: Feature specification from `/specs/005-graph-metadata-index/spec.md`

## Summary

Regenerate graph tooling planning for a JavaScript analyzer that scans C# declarations from `src/`, writes metadata-only graph records into Docker-hosted Neo4j, and keeps all implementation artifacts under `graph/`. The design enforces strict no-body persistence and adds explicit script-level plus Neo4j integration validation.

## Technical Context

**Language/Version**: JavaScript (Node.js >= 18) analyzer and validation scripts; C# is input corpus only  
**Primary Dependencies**: Node.js built-ins, Neo4j HTTP transactional endpoint, Docker Compose workflow (`graph/docker-compose.yml`, `graph/load.sh`)  
**Storage**: Neo4j 5 graph database running in Docker; metadata nodes and containment relationships only  
**Testing**: Script-level validation (`node --check`, parser fixture tests, idempotency checks) plus Neo4j integration validation (container startup, ingest run, Cypher assertions)  
**Target Platform**: macOS/Linux developer environments with Docker Desktop/Engine and Node.js >= 18  
**Project Type**: Repository tooling feature (`graph/` scripts and docs), not an application runtime slice  
**Performance Goals**: Full repository indexing and validation under 2 minutes for current repo size (aligns with SC-005)  
**Constraints**: Implementation changes live under `graph/` only; scan C# metadata from `src/`; use Neo4j from `graph/docker-compose.yml` and `graph/load.sh`; never persist source code body text  
**Scale/Scope**: All C# source files under `src/` plus query/validation assets under `graph/`; entities limited to index run, source file, class symbol, method symbol, and containment edges

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Pre-Design Gate Assessment (truthful tooling interpretation):

- [x] **Vertical Slice**: N/A for runtime layers because this feature is tooling-only; no Api/AppServices/Domains/Infra feature slice is being added.
- [x] **Layer Boundaries**: Preserved by design because runtime business layers are untouched.
- [x] **Class-First Domain**: N/A to this tooling feature; no domain entities are created or changed.
- [x] **EF Core Configuration**: N/A; no EF model/migration/seeding changes are involved.
- [x] **Event-Driven Integration**: N/A; no domain event or message bus behavior changes.
- [x] **Test Coverage**: Satisfied via explicit script-level validation and Neo4j integration validation strategy documented in Phase 1 artifacts.
- [x] **Code-Verified Patterns**: Satisfied by respecting AGENTS guidance that architecture/business slices stay isolated while tooling remains external.

Post-Design Re-Check:

- [x] Phase 1 artifacts remain tooling-scoped, retain no-body guarantee, and do not introduce constitution violations.

## Project Structure

### Documentation (this feature)

```text
specs/005-graph-metadata-index/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── graph-metadata-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
graph/
├── analyze.mjs
├── load.sh
├── docker-compose.yml
├── README.md
└── (query packs + validation scripts for this feature)

src/
└── ... C# source corpus scanned as read-only input
```

**Structure Decision**: All implementation work for this feature is constrained to `graph/`; `src/` is scanned input only and receives no feature implementation artifacts.

## Phase 0: Research Output

All prior clarifications for parser boundaries, identity strategy, no-body persistence, and Docker-hosted Neo4j workflow are resolved in `research.md`.

## Phase 1: Design Output

- Data model refined to required entities and relationships only.
- Contract updated with required/forbidden fields and validation obligations.
- Quickstart updated with script-level and Neo4j integration validation workflow.

## Complexity Tracking

No constitution violations requiring exemptions.
