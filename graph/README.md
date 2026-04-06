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
# Run Roslyn analyzer + start dashboard
./graph/load.sh

# Reset graph and re-analyze from scratch
./graph/load.sh --reset

# Analyze only (no Docker dashboard)
./graph/load.sh --no-dashboard

# Start dashboard only (skip analyzer)
./graph/load.sh --no-analyze
```

## Run Analyzer Directly

```bash
# Full run — writes Cypher to FalkorDB (must be running)
dotnet run graph/analyze.cs

# Dry run — parse only, no writes
dotnet run graph/analyze.cs -- --dry-run

# Custom connection
dotnet run graph/analyze.cs -- --host=localhost --port=6379 --password=codegraph123 --graph=codegraph

# Explicit src path (useful when running from a different directory)
dotnet run graph/analyze.cs -- --src=./src
```

## Dashboard

FalkorDB must be running during analysis (it's a server, not an embedded database).
Start with `docker compose -f graph/docker-compose.yml up -d`, then open http://localhost:3000 for the browser UI.

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