---
name: neo4j-codegraph-qa
description: "Use when answering architecture or class relationship questions in this repository. Query neo4j-codegraph MCP first for fast dependency and relationship retrieval, then fallback to source code scanning when graph coverage is missing or stale."
argument-hint: "Question scope and target symbols. Example: explain CustomerProfile class relationships and call flow"
---

# Skill: Neo4j Codegraph Q&A with Source Fallback

## Purpose

Speed up codebase Q&A by using the Neo4j graph first, especially for:

- class relationships
- dependency chains
- upstream/downstream usages
- endpoint-to-handler-to-domain flow tracing

When graph results are incomplete or stale, fallback to source scanning and reconcile the answer.

## When to Use

Use this skill when the user asks for:

- class relationships or object interaction maps
- endpoint flows and sequence walkthroughs
- impact analysis for a class or module
- quick architecture questions requiring cross-file links

Do not use this skill for:

- implementing code changes
- runtime debugging with logs
- environment setup and deployment tasks

## Required Inputs

- User question and target symbol(s)
- Scope boundaries (feature, folder, or layer)
- Optional: preferred output format (summary, table, Mermaid)

## Decision Flow

1. **Graph-first query** via `neo4j-codegraph` MCP.
2. If graph evidence is sufficient, build the answer from graph results.
3. If graph evidence is partial/conflicting/stale, run source scanning fallback.
4. Merge graph + source findings, prioritize source-of-truth from code.
5. Return concise answer with explicit confidence and any gaps.

## Procedure

### Step 1: Normalize the Question

Extract:

- target symbols (class, interface, endpoint)
- relation type needed (inherits, uses, calls, maps, publishes)
- direction (incoming, outgoing, both)

### Step 2: Query Neo4j First

Use the configured MCP server: `neo4j-codegraph`.

Graph lookup goals:

- find target node(s)
- retrieve direct relationships (1-hop)
- retrieve critical transitive path(s) (2-3 hops max)
- identify endpoint/service/domain crossings

Capture:

- node names and labels
- relationship types
- shortest relevant paths

### Step 3: Graph Quality Check

Treat graph as insufficient if any are true:

- missing expected symbol(s)
- relationship types do not match naming conventions in code
- no path found for known wired flow
- suspiciously empty or outdated results

### Step 4: Fallback Source Scan

When quality check fails, scan source files to confirm:

- endpoint mappings
- handler signatures and dependencies
- repository/spec usage
- domain events and publishers/subscribers

Use fast code search and targeted file reads to rebuild the relationship chain.

### Step 5: Reconcile and Answer

- Prefer source-verified facts when graph and code differ.
- Keep graph findings where verified by code.
- Mark unknowns clearly.

Output options:

- **Quick answer**: short summary + key relationships
- **Trace**: ordered request flow
- **Diagram**: Mermaid flow/class graph for docs

## Output Template

Use this structure:

1. `Scope`: what was analyzed
2. `Graph Findings`: key nodes/edges
3. `Fallback Verification`: code-confirmed links
4. `Final Relationship Map`: merged truth
5. `Confidence`: high/medium/low and why

## Quality Criteria

- Relationship claims are backed by graph or source evidence.
- Fallback was used when graph coverage was weak.
- Final flow references concrete symbols and file locations.
- No speculative links presented as facts.

See checklist: [checklist.md](./checklist.md)
