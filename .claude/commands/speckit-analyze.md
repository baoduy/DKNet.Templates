Perform a non-destructive cross-artifact consistency and quality analysis across spec.md, plan.md, and tasks.md after task generation.

## User Input

$ARGUMENTS

You **MUST** consider the user input before proceeding (if not empty).

## Goal

Identify inconsistencies, duplications, ambiguities, and underspecified items across the three core artifacts (`spec.md`, `plan.md`, `tasks.md`) before implementation. This command MUST run only after tasks have been successfully produced.

## Operating Constraints

**STRICTLY READ-ONLY**: Do **not** modify any files. Output a structured analysis report.

**Constitution Authority**: The project constitution (`.specify/memory/constitution.md`) is **non-negotiable**. Constitution conflicts are automatically CRITICAL.

## Execution Steps

### 1. Initialize Analysis Context

Run `.specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks` once from repo root and parse JSON for FEATURE_DIR and AVAILABLE_DOCS.

### 2. Load Artifacts

Load only the minimal necessary context from each artifact (spec.md, plan.md, tasks.md, constitution).

### 3. Build Semantic Models

Create internal representations:
- Requirements inventory with stable keys
- User story/action inventory with acceptance criteria
- Task coverage mapping
- Constitution rule set

### 4. Detection Passes

Focus on high-signal findings. Limit to 50 findings total.

- **A. Duplication Detection**: Near-duplicate requirements
- **B. Ambiguity Detection**: Vague adjectives, unresolved placeholders
- **C. Underspecification**: Missing measurable outcomes, unmapped tasks
- **D. Constitution Alignment**: Conflicts with MUST principles
- **E. Coverage Gaps**: Requirements with zero tasks, orphan tasks
- **F. Inconsistency**: Terminology drift, entity mismatches, ordering contradictions

### 5. Severity Assignment

- **CRITICAL**: Violates constitution MUST, missing core artifact, zero coverage on core requirement
- **HIGH**: Duplicate/conflicting requirement, ambiguous security/performance attribute
- **MEDIUM**: Terminology drift, missing non-functional coverage
- **LOW**: Style/wording improvements

### 6. Produce Analysis Report

Output a Markdown report with findings table, coverage summary, constitution alignment issues, unmapped tasks, and metrics.

### 7. Provide Next Actions

- If CRITICAL issues: Recommend resolving before implementation
- If only LOW/MEDIUM: User may proceed with improvement suggestions

### 8. Offer Remediation

Ask the user if they want concrete remediation edit suggestions (do NOT apply automatically).
