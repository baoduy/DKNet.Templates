---
description: Create or update the project constitution with principle inputs and template sync
argument-hint: "Optional principles or update instructions"
---

Create or update the project constitution from interactive or provided principle inputs, ensuring all dependent templates stay in sync.

## User Input

$ARGUMENTS

You **MUST** consider the user input before proceeding (if not empty).

## Outline

You are updating the project constitution at `.specify/memory/constitution.md`. This file is a TEMPLATE containing placeholder tokens in square brackets (e.g. `[PROJECT_NAME]`, `[PRINCIPLE_1_NAME]`). Your job is to (a) collect/derive concrete values, (b) fill the template precisely, and (c) propagate any amendments across dependent artifacts.

Follow this execution flow:

1. Load the existing constitution at `.specify/memory/constitution.md`.
   - Identify every placeholder token of the form `[ALL_CAPS_IDENTIFIER]`.
   - If the user requires more or fewer principles than the template, respect that.

2. Collect/derive values for placeholders:
   - Use user input values when supplied.
   - Otherwise infer from existing repo context.
   - `CONSTITUTION_VERSION` must increment with semantic versioning (MAJOR/MINOR/PATCH).

3. Draft the updated constitution content:
   - Replace every placeholder with concrete text.
   - Preserve heading hierarchy.
   - Ensure each Principle section has: name, rules, rationale.

4. Consistency propagation:
   - Read and update `.specify/templates/plan-template.md`, `spec-template.md`, `tasks-template.md` for alignment.
   - Read command files in `.specify/templates/commands/*.md` to verify no outdated references.
   - Update runtime guidance docs if needed.

5. Produce a Sync Impact Report (prepend as HTML comment):
   - Version change, modified/added/removed principles, templates requiring updates.

6. Validation:
   - No remaining unexplained bracket tokens.
   - Version line matches report.
   - Dates ISO format YYYY-MM-DD.

7. Write the completed constitution back to `.specify/memory/constitution.md`.

8. Output summary: new version, bump rationale, files flagged for follow-up, suggested commit message.
