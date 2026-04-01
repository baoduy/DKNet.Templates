# Quickstart: C# Metadata Index Validation

## Goal
Run the JavaScript analyzer under `graph/` to index C# metadata from `src/` into Docker-hosted Neo4j, then validate graph integrity and no-body persistence.

## Prerequisites
- Docker available and running
- Node.js 18+
- Repository checked out on branch `005-graph-metadata-index`

## 1. Start Neo4j with graph workflow

```bash
./graph/load.sh
```

This uses `graph/docker-compose.yml` and prepares local Neo4j for indexing.

## 2. Script-level validation (before integration)

```bash
node --check graph/analyze.mjs
```

```bash
node graph/analyze.mjs --help || true
```

Script-level checks to run in validation scripts:
- parser extracts required class/method/file metadata from sample fixtures
- deterministic key generation for idempotency
- parse failures in one file do not abort indexing for other files

## 3. Run indexing

```bash
node graph/analyze.mjs
```

Expected run output includes summary metrics: scanned files, indexed classes, indexed methods, failed files.

## 4. Neo4j integration validation

Run representative Cypher assertions:

```cypher
MATCH (f:SourceFile)
RETURN count(f) AS fileCount;
```

```cypher
MATCH (c:ClassSymbol)-[:DECLARED_IN]->(f:SourceFile)
RETURN c.name, c.project, f.path
LIMIT 20;
```

```cypher
MATCH (m:MethodSymbol)-[:BELONGS_TO]->(c:ClassSymbol)
RETURN m.name, c.name, m.filePath
LIMIT 20;
```

No-body persistence assertion:

```cypher
MATCH (n)
UNWIND keys(n) AS k
WITH labels(n) AS labels, k, toString(n[k]) AS v
WHERE k IN ['content','source','sourceText','body','code']
	OR v CONTAINS "class "
	OR v CONTAINS "namespace "
RETURN labels, k, substring(v, 0, 120)
LIMIT 25;
```

Expected: zero rows indicating stored source body content.

## 5. Idempotency validation

Run index twice on unchanged files and compare counts:

```cypher
MATCH (n) RETURN labels(n)[0] AS label, count(*) AS count ORDER BY label;
```

Expected: counts remain stable within tolerance defined by SC-003.

## Success Checklist
- implementation artifacts remain under `graph/`
- class/method/file metadata is queryable in Neo4j
- source body content is absent from graph properties
- reruns are idempotent and metrics are reported per run
