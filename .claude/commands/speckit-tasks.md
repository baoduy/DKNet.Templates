Generate an actionable, dependency-ordered tasks.md for the feature based on available design artifacts.

## User Input

$ARGUMENTS

You **MUST** consider the user input before proceeding (if not empty).

## Pre-Execution Checks

Check for extension hooks in `.specify/extensions.yml` under `hooks.before_tasks` key. Execute mandatory hooks before proceeding.

## Outline

1. **Setup**: Run `.specify/scripts/bash/check-prerequisites.sh --json` from repo root and parse FEATURE_DIR and AVAILABLE_DOCS list. All paths must be absolute.

2. **Load design documents**: Read from FEATURE_DIR:
   - **Required**: plan.md (tech stack, libraries, structure), spec.md (user stories with priorities)
   - **Optional**: data-model.md (entities), contracts/ (interface contracts), research.md (decisions), quickstart.md (test scenarios)

3. **Execute task generation workflow**:
   - Load plan.md and extract tech stack, libraries, project structure
   - Load spec.md and extract user stories with their priorities (P1, P2, P3, etc.)
   - If data-model.md exists: Extract entities and map to user stories
   - If contracts/ exists: Map interface contracts to user stories
   - If research.md exists: Extract decisions for setup tasks
   - Generate tasks organized by user story
   - Generate dependency graph showing user story completion order
   - Validate task completeness

4. **Generate tasks.md**: Use `.specify/templates/tasks-template.md` as structure:
   - Phase 1: Setup tasks (project initialization)
   - Phase 2: Foundational tasks (blocking prerequisites for all user stories)
   - Phase 3+: One phase per user story (in priority order from spec.md)
   - Final Phase: Polish & cross-cutting concerns

5. **Report**: Output path to generated tasks.md and summary:
   - Total task count, per user story count, parallel opportunities, suggested MVP scope

6. **Post-execution**: Check `.specify/extensions.yml` for `hooks.after_tasks`. Execute mandatory hooks.

## Task Format (REQUIRED)

Every task MUST strictly follow: `- [ ] [TaskID] [P?] [Story?] Description with file path`

- **Checkbox**: ALWAYS start with `- [ ]`
- **Task ID**: Sequential (T001, T002, T003...)
- **[P] marker**: Only if parallelizable
- **[Story] label**: REQUIRED for user story phases only ([US1], [US2], etc.)
- **Description**: Clear action with exact file path

### Phase Structure

- **Phase 1**: Setup (project initialization)
- **Phase 2**: Foundational (blocking prerequisites)
- **Phase 3+**: User Stories in priority order
- **Final Phase**: Polish & Cross-Cutting Concerns
