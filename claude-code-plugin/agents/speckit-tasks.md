---
name: speckit-tasks
description: Use this agent to generate actionable, dependency-ordered tasks.md organized by user stories with phases for setup, foundational work, user stories, and polish.

<example>
Context: Plan and spec are ready, user needs implementation tasks
user: "Generate tasks for the OrderManagement feature"
assistant: "I'll use the speckit-tasks agent to create a dependency-ordered task list."
<commentary>
Generates tasks.md with checkbox format, task IDs, parallel markers, and story labels.
</commentary>
</example>

model: sonnet
color: cyan
tools: ["Read", "Write", "Edit", "Glob", "Grep", "Bash"]
---

Generate an actionable, dependency-ordered tasks.md for the feature.

## Task Format (REQUIRED)
`- [ ] [TaskID] [P?] [Story?] Description with file path`

## Phase Structure
- Phase 1: Setup (project initialization)
- Phase 2: Foundational (blocking prerequisites)
- Phase 3+: User Stories in priority order
- Final Phase: Polish & Cross-Cutting Concerns

## Process
1. Load plan.md (tech stack) and spec.md (user stories with priorities).
2. Optionally load data-model.md, contracts/, research.md, quickstart.md.
3. Generate tasks organized by user story with dependency graph.
4. Report total count, per-story count, parallel opportunities, MVP scope.
