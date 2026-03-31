Execute the implementation plan by processing and executing all tasks defined in tasks.md.

## User Input

$ARGUMENTS

You **MUST** consider the user input before proceeding (if not empty).

## Pre-Execution Checks

Check for extension hooks in `.specify/extensions.yml` under `hooks.before_implement` key. Execute mandatory hooks before proceeding.

## Outline

1. Run `.specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks` from repo root and parse FEATURE_DIR and AVAILABLE_DOCS list. All paths must be absolute.

2. **Check checklists status** (if FEATURE_DIR/checklists/ exists):
   - Scan all checklist files, count total/completed/incomplete items
   - If any checklist is incomplete: **STOP** and ask user whether to proceed
   - If all checklists are complete: Automatically proceed

3. Load and analyze the implementation context:
   - **REQUIRED**: tasks.md, plan.md
   - **IF EXISTS**: data-model.md, contracts/, research.md, quickstart.md

4. **Project Setup Verification**: Create/verify ignore files based on project tech stack.

5. Parse tasks.md structure and extract:
   - Task phases, dependencies, details, execution flow

6. Execute implementation following the task plan:
   - Phase-by-phase execution
   - Respect dependencies (sequential vs parallel [P])
   - File-based coordination (same-file tasks run sequentially)
   - Validation checkpoints after each phase

7. Implementation execution rules:
   - Setup first → Tests before code (if TDD) → Core development → Integration → Polish

8. Progress tracking and error handling:
   - Report progress after each completed task
   - Halt on non-parallel task failures
   - Mark completed tasks as [X] in tasks.md
   - Provide clear error messages with debugging context

9. Completion validation:
   - Verify all required tasks completed
   - Check features match specification
   - Validate tests pass
   - Report final status summary

10. **Post-execution**: Check `.specify/extensions.yml` for `hooks.after_implement`. Execute mandatory hooks.
