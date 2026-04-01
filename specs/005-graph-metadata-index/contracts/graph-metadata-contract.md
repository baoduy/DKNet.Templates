# Contract: C# Metadata Graph Output

## Purpose
Define the observable contract produced by `graph/analyze.mjs` when indexing C# metadata into Neo4j for navigation use cases.

## Scope
- Input: C# files under `src/`
- Runtime: Neo4j from `graph/docker-compose.yml`, launched through `graph/load.sh`
- Output: Metadata-only nodes and containment relationships
- Non-goal: Persisting source body text

## Node Contracts

## `SourceFile`
- Required properties:
  - `path`: string (repository-relative file path)
  - `fileName`: string
- Optional properties:
  - `project`: string
- Forbidden properties:
  - `content`, `source`, `sourceText`, `body`, `code`, or equivalent body payload fields

## `ClassSymbol`
- Required properties:
  - `classKey`: string (stable key)
  - `name`: string
  - `project`: string
  - `filePath`: string
- Optional properties:
  - `namespace`: string
- Forbidden properties:
  - class declaration body/member text

## `MethodSymbol`
- Required properties:
  - `methodKey`: string (stable key)
  - `name`: string
  - `classKey`: string
  - `project`: string
  - `filePath`: string
- Optional properties:
  - `lineStart`: integer
- Forbidden properties:
  - method body text, inline source snippets

## `IndexRun`
- Required properties:
  - `runId`: string
  - `startedAtUtc`: datetime
  - `scannedFileCount`: integer
  - `indexedClassCount`: integer
  - `indexedMethodCount`: integer
  - `failedFileCount`: integer
  - `status`: `success | partial | failed`

## Relationship Contracts
- `(:ClassSymbol)-[:DECLARED_IN]->(:SourceFile)`
- `(:MethodSymbol)-[:BELONGS_TO]->(:ClassSymbol)`
- `(:MethodSymbol)-[:DECLARED_IN]->(:SourceFile)`
- `(:IndexRun)-[:INDEXED_FILE]->(:SourceFile)`
- `(:IndexRun)-[:INDEXED_CLASS]->(:ClassSymbol)`
- `(:IndexRun)-[:INDEXED_METHOD]->(:MethodSymbol)`

## Query Pack Contract (graph/)
- Must provide reusable query definitions for:
  - class-to-file lookup
  - method-to-class lookup
  - method-to-file lookup

## Validation Contract (graph/)
- Script-level validations must assert:
  - parser extracts required metadata fields
  - stable keys are deterministic for idempotency
  - malformed files increment failure metrics without aborting full run
- Neo4j integration validations must assert:
  - required nodes/properties/relationships exist
  - no forbidden body-like properties are present
  - repeated runs on unchanged input remain within duplicate tolerance target

## Query Guarantees
1. Query by class name returns class metadata and source file path.
2. Query by method name returns method metadata, owning class, and file path.
3. Query results do not require source body data to support repository navigation.

## Idempotency Guarantee
Analyzer reruns on unchanged input must merge into stable identities and avoid uncontrolled duplicate nodes/relationships.
