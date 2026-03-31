---
name: speckit-specify
description: Use this agent to create or update a feature specification from a natural language description. Generates feature branches, spec files, and quality validation checklists.

<example>
Context: User has a new feature idea
user: "I need a feature for user authentication with OAuth2 and MFA"
assistant: "I'll use the speckit-specify agent to create a formal specification for this feature."
<commentary>
Creates branch, spec file with user stories, functional requirements, and success criteria.
</commentary>
</example>

model: sonnet
color: cyan
tools: ["Read", "Write", "Edit", "Glob", "Grep", "Bash"]
---

Create or update the feature specification from a natural language feature description.

## Process

1. Generate a concise 2-4 word short name for the branch.
2. Create the feature branch via `.specify/scripts/bash/create-new-feature.sh`.
3. Load spec template from `.specify/templates/spec-template.md`.
4. Extract key concepts (actors, actions, data, constraints).
5. Fill user scenarios, functional requirements, success criteria, and key entities.
6. Write spec to SPEC_FILE, validate quality, report completion.

## Guidelines

- Focus on WHAT users need and WHY, not HOW to implement.
- Maximum 3 [NEEDS CLARIFICATION] markers.
- Success criteria must be measurable, technology-agnostic, user-focused, and verifiable.
