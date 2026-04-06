# Code Knowledge Graph

Roslyn-based C# analyzer that scans the codebase and builds a FalkorDB knowledge graph (Redis)
with classes, methods, properties, fields, endpoints, dependencies, and architecture metadata.

The graph contains **structural metadata only** — no source code or business logic is stored.

**Stored:**
- Type declarations (names, visibility, modifiers, generics)
- Method signatures (name, return type, parameter names/types, async/static/override)
- Property and field declarations (name, type, visibility)
- Line numbers and file paths (for navigation back to source)
- Relationships: inheritance, interface implementation, project dependencies, NuGet packages
- Call-site references (which method calls which type — by name only)
- Endpoint mappings (verb, request/response types, route)
- Bus dispatches and domain events (message/event type names only)
- Architecture labels (layer, pattern, bounded context, CQRS/DDD concepts)

**Not stored:**
- Method body source code
- Business rules or conditional logic
- String literals, constant values, or configuration
- Comments or documentation
- Variable assignments or control flow
- Any actual implementation details

The analyzer visits method bodies only to extract call signatures and type references —
it never captures body text. This makes the graph safe to share and optimized for AI
tools that need to understand codebase structure without reading every line of code.

---

## Prerequisites

- .NET SDK >= 10.0
- Docker (for FalkorDB + browser UI)

## Quick Start

```bash
# Run both graph + vector indexers (full scan)
./graph/load.sh

# Graph only / vector only
./graph/load.sh --graph
./graph/load.sh --vector

# Purge all data + full rebuild
./graph/load.sh --purge

# Preview without writing
./graph/load.sh --dry-run

# Stop containers after indexing
./graph/load.sh --no-dashboard
```

## Incremental Updates

Both indexers track SHA256 file hashes. On re-run, unchanged files are skipped automatically — even without `--changed-files`.

```bash
# Incremental: only process specific changed files
./graph/load.sh --graph --changed-cs-files=src/Mx.Pgw.Api/Program.cs,src/Mx.Pgw.Domains/Charge.cs
./graph/load.sh --vector --changed-md-files=CLAUDE.md,specs/payout.md

# Skip silently if Docker containers are not running
./graph/load.sh --skip-if-down --changed-cs-files=src/Foo.cs
```

### Git Post-Commit Hook

Auto-trigger incremental updates after every commit:

```bash
# One-time setup (installs .git/hooks/post-commit)
./graph/setup-hooks.sh
```

The hook runs in background so commits are not blocked. It:
- Classifies changed files by extension (`.cs` → graph, `.md` → vector)
- Only runs if Docker containers are already up (`--skip-if-down`)
- Logs to `/tmp/monxa-graph-update.log`

## Run Indexers Directly

```bash
# Graph: Roslyn → FalkorDB
dotnet run graph/graph.cs
dotnet run graph/graph.cs -- --dry-run
dotnet run graph/graph.cs -- --changed-files=src/Mx.Pgw.Api/Program.cs

# Vector: Markdown → ONNX → Qdrant
dotnet run graph/vector.cs
dotnet run graph/vector.cs -- --dry-run
dotnet run graph/vector.cs -- --changed-files=CLAUDE.md,README.md

# Custom connections
dotnet run graph/graph.cs -- --host=localhost --port=6379 --password=codegraph123 --graph=codegraph
dotnet run graph/vector.cs -- --host=localhost --port=6334 --collection=monxa-docs
```

## Dashboard

FalkorDB and Qdrant must be running during indexing (they're servers, not embedded databases).
Start with `docker compose -f graph/docker-compose.yml up -d`.

- FalkorDB Browser: http://localhost:3000
- Qdrant Dashboard: http://localhost:6333/dashboard

## What Gets Indexed

| Node Type | Description |
|-----------|-------------|
| `Project` | Each .csproj with layer, type, framework |
| `SourceFile` | Every .cs file with project association |
| `Classes` | Classes, records, interfaces, structs |
| `Methods` | Methods and constructors with parameters |
| `Property` / `Field` | Type members |
| `Namespace` | Namespace groupings |
| `NugetPackage` | Package dependencies |
| `Endpoint` | HTTP endpoints (MapGet, MapPost, etc.) |
| `Layer` | Architecture layers (Api, AppServices, Domains, Infra, Share) |
| `ArchitectureConcept` | CQRS, DDD, API pattern roles |

Each node includes metadata optimized for AI consumption: `filePath`, `project`, `layer`,
`pattern`, `boundedContext`, and `lineStart`.