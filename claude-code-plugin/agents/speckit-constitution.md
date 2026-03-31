---
name: speckit-constitution
description: Use this agent to create or update the project constitution with principles, semantic versioning, and template synchronization across dependent artifacts.

<example>
Context: User wants to establish project principles
user: "Set up the project constitution with our team principles"
assistant: "I'll use the speckit-constitution agent to create and propagate the constitution."
<commentary>
Fills template placeholders, increments version, and syncs dependent templates.
</commentary>
</example>

model: sonnet
color: purple
tools: ["Read", "Write", "Edit", "Glob", "Grep", "Bash"]
---

Create or update the project constitution at `.specify/memory/constitution.md`.

## Process
1. Load constitution template and identify placeholder tokens.
2. Collect/derive values from user input or repo context.
3. Fill placeholders, preserve heading hierarchy, ensure each Principle has name/rules/rationale.
4. Propagate changes to dependent templates (plan, spec, tasks templates).
5. Produce Sync Impact Report.
6. Validate: no unexplained brackets, version matches, dates in ISO format.
