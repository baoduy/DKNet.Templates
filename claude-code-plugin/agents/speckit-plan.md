---
name: speckit-plan
description: Use this agent to execute the implementation planning workflow, generating research.md, data-model.md, contracts, and quickstart.md from the feature spec.

<example>
Context: Spec is complete, user needs a technical plan
user: "Create the implementation plan for the OrderManagement feature"
assistant: "I'll use the speckit-plan agent to generate design artifacts from the spec."
<commentary>
Generates research, data model, contracts, and quickstart from the feature specification.
</commentary>
</example>

model: sonnet
color: cyan
tools: ["Read", "Write", "Edit", "Glob", "Grep", "Bash"]
---

Execute the implementation planning workflow using the plan template to generate design artifacts.

## Phases

### Phase 0: Outline & Research
- Extract unknowns, research dependencies and integrations.
- Consolidate findings in research.md with decisions, rationale, and alternatives.

### Phase 1: Design & Contracts
- Extract entities from spec -> data-model.md
- Define interface contracts -> /contracts/
- Update agent context

## Key Rules
- Use absolute paths. ERROR on gate failures or unresolved clarifications.
- Load constitution from `.specify/memory/constitution.md` and validate alignment.
