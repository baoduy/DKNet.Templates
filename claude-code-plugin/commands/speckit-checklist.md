---
description: Generate a custom requirement quality checklist for the current feature
argument-hint: "Checklist focus area. Example: security requirements for payment processing"
---

Generate a custom checklist for the current feature based on user requirements.

## Checklist Purpose: "Unit Tests for English"

**CRITICAL CONCEPT**: Checklists are **UNIT TESTS FOR REQUIREMENTS WRITING** - they validate the quality, clarity, and completeness of requirements in a given domain.

**FOR requirements quality validation**:
- "Are visual hierarchy requirements defined for all card types?" (completeness)
- "Is 'prominent display' quantified with specific sizing/positioning?" (clarity)
- "Are hover state requirements consistent across all interactive elements?" (consistency)

## User Input

$ARGUMENTS

You **MUST** consider the user input before proceeding (if not empty).

## Execution Steps

1. **Setup**: Run `.specify/scripts/bash/check-prerequisites.sh --json` from repo root and parse JSON for FEATURE_DIR and AVAILABLE_DOCS list.

2. **Clarify intent (dynamic)**: Derive up to THREE initial contextual clarifying questions. They MUST:
   - Be generated from the user's phrasing + extracted signals from spec/plan/tasks
   - Only ask about information that materially changes checklist content
   - Be skipped individually if already unambiguous

3. **Understand user request**: Combine user input + clarifying answers:
   - Derive checklist theme (e.g., security, review, deploy, ux)
   - Consolidate explicit must-have items mentioned by user
   - Map focus selections to category scaffolding

4. **Load feature context**: Read from FEATURE_DIR:
   - spec.md: Feature requirements and scope
   - plan.md (if exists): Technical details, dependencies
   - tasks.md (if exists): Implementation tasks

5. **Generate checklist** - Create "Unit Tests for Requirements":
   - Create `FEATURE_DIR/checklists/` directory if needed
   - Generate unique checklist filename based on domain (e.g., `ux.md`, `api.md`, `security.md`)
   - File handling: Create new or append to existing (continue from last CHK ID)

   **REQUIRED PATTERNS**:
   - "Are [requirement type] defined/specified/documented for [scenario]?"
   - "Is [vague term] quantified/clarified with specific criteria?"
   - "Are requirements consistent between [section A] and [section B]?"
   - "Can [requirement] be objectively measured/verified?"

   **PROHIBITED** (implementation tests):
   - Any item starting with "Verify", "Test", "Confirm" + implementation behavior
   - References to code execution, user actions, system behavior

6. **Structure Reference**: Use `.specify/templates/checklist-template.md` as structure.

7. **Report**: Output full path to checklist file, item count, and summary.
