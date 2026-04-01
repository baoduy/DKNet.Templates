# Feature Specification: C# Metadata Index In Neo4j

**Feature Branch**: `005-graph-metadata-index`  
**Created**: 2026-04-01  
**Status**: Draft  
**Input**: User description: "Implement a JavaScript analyzer under graph/ that scans C# source metadata and pushes a searchable index into Neo4j via Docker, with all implementation artifacts under graph/ and no source body storage in graph."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build Metadata Index Safely (Priority: P1)

As a maintainer, I want an analyzer in `graph/` to index C# metadata into Neo4j without code bodies so that the graph is searchable but source content never leaves repository files.

**Why this priority**: This is the core scope: searchable metadata plus confidentiality.

**Independent Test**: Run the analyzer and inspect stored node/relationship properties to verify that only metadata fields exist and no source body text is present.

**Acceptance Scenarios**:

1. **Given** C# files with class and method bodies exist in the repository, **When** the analyzer runs, **Then** Neo4j contains class/method/file metadata and relationship edges, and no source body text is stored.
2. **Given** metadata index records already exist in Neo4j, **When** the analyzer runs again on unchanged files, **Then** the graph remains deduplicated and queryable.

---

### User Story 2 - Query Code Locations From Graph (Priority: P2)

As a developer using graph queries, I want to find classes and methods with exact repository file locations so that I can open and inspect implementation in source files directly.

**Why this priority**: Search value depends on reliable source navigation.

**Independent Test**: Execute query-pack searches by class and method and verify returned file path/location points to real repository files.

**Acceptance Scenarios**:

1. **Given** a known class name, **When** I run a search query, **Then** I get class metadata and file path/location to open the source file.
2. **Given** a known method name, **When** I run a search query, **Then** I get method metadata, owning class, and source file path/location.

---

### User Story 3 - Operate Entirely From graph/ Artifacts (Priority: P3)

As a project contributor, I want analyzer scripts, query packs, validation scripts, and docs to live only under `graph/` so that the indexing workflow is isolated, discoverable, and easy to maintain.

**Why this priority**: Folder-level scope control prevents drift into unrelated project areas.

**Independent Test**: Confirm all new or updated implementation artifacts for this feature are located under `graph/` and the workflow can be executed from there.

**Acceptance Scenarios**:

1. **Given** this feature is implemented, **When** I inspect changed files, **Then** implementation artifacts for analyzer/index/query/validation/docs are all under `graph/`.
2. **Given** a fresh environment with Docker and repository checkout, **When** I follow docs under `graph/`, **Then** I can run Neo4j, execute indexing, and run validation queries.

### Edge Cases

- Files with identical names in different directories are indexed with distinct repository-relative paths.
- Partial classes spread across files preserve each file location without collapsing to a single file entry.
- Methods with identical names in different classes remain uniquely linked to their owning class and file path.
- C# files containing no class or method declarations are handled without failing the full indexing run.
- Parsing errors in one file do not block indexing of other valid files; failures are reported for validation.
- If Neo4j is unavailable, indexing exits with a non-success status and a clear actionable error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a JavaScript analyzer script under `graph/` that scans repository C# source files for metadata needed by the index.
- **FR-002**: The system MUST index, at minimum, class name, method name, file name, and repository-relative file location for discovered declarations.
- **FR-003**: The system MUST push indexed metadata into Neo4j running via Docker as part of the documented graph workflow.
- **FR-004**: The system MUST create and maintain searchable graph structures so users can query by class name and method name.
- **FR-005**: The system MUST preserve relationships between methods, classes, and files required for source navigation.
- **FR-006**: The system MUST treat repository files as the source of truth for implementation details and MUST NOT store source code bodies in graph data.
- **FR-007**: The system MUST keep feature implementation artifacts under `graph/`, including analyzer scripts, query packs, validation scripts, and feature documentation.
- **FR-008**: The system MUST include validation scripts under `graph/` that verify required metadata fields, required relationships, and absence of source body content.
- **FR-009**: The system MUST include reusable query packs under `graph/` for common lookups (class-to-file, method-to-class, method-to-file).
- **FR-010**: The system MUST run idempotently so repeated indexing on unchanged input does not create uncontrolled duplicate nodes or relationships.
- **FR-011**: The system MUST report indexing summary metrics per run, including scanned file count, indexed class count, indexed method count, and failed file count.
- **FR-012**: The system MUST provide graph-folder documentation describing prerequisites, run commands, validation steps, and troubleshooting for Docker Neo4j connectivity.

### Key Entities *(include if feature involves data)*

- **Index Run**: A single execution record with timestamp, file counts, entity counts, and failure counts.
- **Source File**: Metadata node for a C# file, including file name and repository-relative path.
- **Class Symbol**: Metadata node for a class declaration linked to one or more source files.
- **Method Symbol**: Metadata node for a method declaration linked to an owning class and source file.
- **Containment Relationship**: Relationship set describing method-in-class and class-in-file structure.
- **Query Pack**: Versioned set of reusable search queries stored under `graph/`.
- **Validation Result**: Output record proving required fields/relationships exist and source body text is absent.

### Assumptions

- Docker is available for running Neo4j locally.
- C# source files are readable from repository paths during indexing.
- Consumers use graph results for discovery and navigate to repository files for full implementation context.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of indexed class and method nodes include a valid repository-relative file location.
- **SC-002**: 0 indexed nodes contain source body text in validation checks across three consecutive full runs.
- **SC-003**: Re-running indexing on unchanged input changes total node and relationship counts by no more than 1%.
- **SC-004**: At least 95% of sampled class-name and method-name searches in the query pack return at least one matching result.
- **SC-005**: Validation scripts complete successfully and report required metadata/relationship checks in under 2 minutes for the current repository size.
- **SC-006**: 100% of feature implementation artifacts introduced for this objective are located under `graph/`.
