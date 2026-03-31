---
description: Identify underspecified areas in feature specs with up to 5 targeted clarification questions
argument-hint: "Optional focus area. Example: security requirements for the auth feature"
---

Identify underspecified areas in the current feature spec by asking up to 5 highly targeted clarification questions and encoding answers back into the spec.

## User Input

$ARGUMENTS

You **MUST** consider the user input before proceeding (if not empty).

## Outline

Goal: Detect and reduce ambiguity or missing decision points in the active feature specification and record the clarifications directly in the spec file.

Note: This clarification workflow is expected to run (and be completed) BEFORE planning. If the user explicitly states they are skipping clarification, you may proceed, but must warn that downstream rework risk increases.

Execution steps:

1. Run `.specify/scripts/bash/check-prerequisites.sh --json --paths-only` from repo root **once**. Parse minimal JSON payload fields:
   - `FEATURE_DIR`
   - `FEATURE_SPEC`
   - If JSON parsing fails, abort and instruct user to run specification first.

2. Load the current spec file. Perform a structured ambiguity & coverage scan using this taxonomy. For each category, mark status: Clear / Partial / Missing.

   Categories:
   - Functional Scope & Behavior
   - Domain & Data Model
   - Interaction & UX Flow
   - Non-Functional Quality Attributes
   - Integration & External Dependencies
   - Edge Cases & Failure Handling
   - Constraints & Tradeoffs
   - Terminology & Consistency
   - Completion Signals

3. Generate (internally) a prioritized queue of candidate clarification questions (maximum 5). Apply these constraints:
    - Each question must be answerable with EITHER:
       - A short multiple-choice selection (2-5 options), OR
       - A one-word / short-phrase answer (<=5 words)
    - Only include questions whose answers materially impact architecture, data modeling, task decomposition, test design, UX behavior, operational readiness, or compliance validation.

4. Sequential questioning loop (interactive):
    - Present EXACTLY ONE question at a time.
    - For multiple-choice: Present a **recommended option** with reasoning, then a table of all options.
    - After the user answers: validate and record it in working memory.
    - Stop when: all critical ambiguities resolved, user signals completion, or 5 questions reached.

5. Integration after EACH accepted answer:
    - Ensure a `## Clarifications` section exists with a `### Session YYYY-MM-DD` subheading.
    - Append `- Q: <question> → A: <final answer>`.
    - Apply the clarification to the most appropriate spec section(s).
    - Save the spec file AFTER each integration.

6. Validation after each write:
   - Clarifications session contains exactly one bullet per accepted answer.
   - Updated sections contain no lingering vague placeholders.
   - Markdown structure valid.

7. Write the updated spec back to `FEATURE_SPEC`.

8. Report completion:
   - Number of questions asked & answered.
   - Path to updated spec.
   - Sections touched.
   - Coverage summary table.
   - Suggested next command.
