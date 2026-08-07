---
name: qdrant-docsearch
description: "Use when answering documentation, specification, or architecture questions. Query qdrant-docsearch MCP first for fast semantic retrieval over project markdown files, then fallback to direct file reads when vector results are insufficient."
argument-hint: "Question about docs, specs, skills, or architecture. Example: how does the payout approval flow work"
---

# Skill: Qdrant Doc Search — Semantic Q&A with Source Fallback

## Purpose

Speed up documentation Q&A by using vector search over all project markdown files, especially for:

- feature specifications and requirements
- architecture decisions and trade-offs
- skill and agent definitions
- onboarding and process documentation
- end-to-end feature analysis

When vector results are incomplete or low-relevance, fallback to direct file reads and reconcile the answer.

## When to Use

Use this skill when the user asks for:

- feature spec details (requirements, contracts, data models)
- architecture decisions or design rationale
- skill definitions and procedures
- process documentation or onboarding guides
- cross-feature impact or dependency questions

Do not use this skill for:

- code-level questions (use `falkordb-codegraph` instead)
- runtime debugging with logs
- environment setup and deployment tasks
- implementing code changes

## Required Inputs

- User question targeting documentation or specifications
- Optional: scope boundaries (feature name, category, time period)
- Optional: preferred output format (summary, list, detailed)

## Decision Flow

1. **Vector-first query** via `qdrant-docsearch` MCP `search` tool.
2. If vector evidence is sufficient (high relevance, multiple corroborating chunks), build the answer.
3. If vector evidence is partial/low-relevance, read the referenced source files directly.
4. Merge vector + source findings, prioritize source-of-truth from files.
5. Return concise answer with explicit file references and confidence.

## Procedure

### Step 1: Normalize the Question

Extract:

- target topic (feature name, concept, process)
- information type needed (requirements, architecture, contracts, procedures)
- scope (specific spec number, category, or broad)

### Step 2: Query Qdrant First

Use the configured MCP server: `qdrant-docsearch`.

Search with a natural language query derived from the user's question.

Capture from results:

- chunk content and relevance scores
- file paths (for source verification)
- headings and categories (for context)
- parent headings (for document structure)

### Step 3: Relevance Check

Treat vector results as insufficient if any are true:

- top results have low similarity scores
- returned chunks don't address the actual question
- results come from unrelated categories
- critical expected documents are missing from results

### Step 4: Fallback File Read

When relevance check fails, read source files directly:

- use file paths from vector results as starting points
- navigate to related files via directory structure
- scan spec directories for the target feature
- check skill definitions and architecture docs

### Step 5: Reconcile and Answer

- Prefer file-verified content when vector chunks are ambiguous.
- Keep vector findings where they point to the right sources.
- Mark unknowns clearly.

Output options:

- **Quick answer**: short summary + key file references
- **Detailed**: structured answer with quotes and file locations
- **Navigation**: list of relevant files the user should read

## Output Template

Use this structure:

1. `Scope`: what was searched
2. `Vector Results`: key chunks found and their sources
3. `Source Verification`: file-confirmed content
4. `Answer`: merged response
5. `References`: file paths for further reading
6. `Confidence`: high/medium/low and why

## Quality Criteria

- Answers cite specific file paths where information was found.
- Fallback was used when vector results were weak.
- No speculative claims presented as documented facts.
- Cross-references between specs, skills, and docs are followed.

See checklist: [checklist.md](./checklist.md)
