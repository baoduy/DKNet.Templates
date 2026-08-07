# Phase 1 Data Model: C# Metadata Index In Neo4j

## Entities

## IndexRun
- Purpose: Represents one analyzer execution for observability and validation.
- Key fields:
  - `runId` (string, unique)
  - `startedAtUtc` (datetime)
  - `completedAtUtc` (datetime)
  - `scannedFileCount` (integer)
  - `indexedClassCount` (integer)
  - `indexedMethodCount` (integer)
  - `failedFileCount` (integer)
  - `status` (enum: `success`, `partial`, `failed`)
- Relationships:
  - `(:IndexRun)-[:INDEXED_FILE]->(:SourceFile)`
  - `(:IndexRun)-[:INDEXED_CLASS]->(:ClassSymbol)`
  - `(:IndexRun)-[:INDEXED_METHOD]->(:MethodSymbol)`

## SourceFile
- Purpose: Metadata for a scanned C# file from `src/`.
- Key fields:
  - `path` (string, unique repository-relative path)
  - `fileName` (string)
  - `project` (string, nullable)
- Relationships:
  - `(:ClassSymbol)-[:DECLARED_IN]->(:SourceFile)`
  - `(:MethodSymbol)-[:DECLARED_IN]->(:SourceFile)`
- Validation rules:
  - `path` must be normalized with `/`
  - no body-like fields (`content`, `body`, `sourceText`) are allowed

## ClassSymbol
- Purpose: C# class declaration metadata for graph lookup.
- Key fields:
  - `classKey` (string, stable identity)
  - `name` (string)
  - `namespace` (string, optional)
  - `project` (string)
  - `filePath` (string)
- Relationships:
  - `(:ClassSymbol)-[:DECLARED_IN]->(:SourceFile)`
  - `(:MethodSymbol)-[:BELONGS_TO]->(:ClassSymbol)`
- Validation rules:
  - unique by `(project, classKey)`
  - must have a linked `SourceFile`

## MethodSymbol
- Purpose: C# method declaration metadata for search and navigation.
- Key fields:
  - `methodKey` (string, stable identity)
  - `name` (string)
  - `classKey` (string)
  - `project` (string)
  - `filePath` (string)
  - `lineStart` (integer, optional)
- Relationships:
  - `(:MethodSymbol)-[:BELONGS_TO]->(:ClassSymbol)`
  - `(:MethodSymbol)-[:DECLARED_IN]->(:SourceFile)`
- Validation rules:
  - unique by `(project, methodKey)`
  - method names duplicated across classes are allowed when class identity differs

## QueryPack
- Purpose: Versioned reusable lookup queries stored under `graph/`.
- Key fields:
  - `name` (string)
  - `version` (string)
  - `queries` (array/object reference)

## ValidationResult
- Purpose: Captures validation outcomes for metadata completeness and no-body guarantees.
- Key fields:
  - `validationRunId` (string)
  - `timestampUtc` (datetime)
  - `requiredFieldChecksPassed` (boolean)
  - `relationshipChecksPassed` (boolean)
  - `noBodyChecksPassed` (boolean)
  - `details` (string/object)

## State Transitions

1. Start analyzer run and create `IndexRun` record with in-progress status.
2. Enumerate C# files under `src/` and merge `SourceFile` nodes by `path`.
3. Parse class declarations and merge `ClassSymbol` nodes with `DECLARED_IN` links.
4. Parse method declarations and merge `MethodSymbol` nodes with `BELONGS_TO` and `DECLARED_IN` links.
5. Finalize `IndexRun` with counts and status (`success`, `partial`, or `failed`).
6. Execute validation scripts and produce `ValidationResult` output artifacts.
7. Re-run on unchanged files and confirm idempotent merge behavior (stable counts within tolerance).
