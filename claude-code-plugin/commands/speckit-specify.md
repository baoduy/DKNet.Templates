---
description: Create or update a feature specification from a natural language feature description
argument-hint: "Feature description. Example: User authentication with OAuth2 and MFA support"
---

Create or update the feature specification from a natural language feature description.

## User Input

$ARGUMENTS

You **MUST** consider the user input before proceeding (if not empty).

## Outline

The text the user typed after the command **is** the feature description. Assume you always have it available in this conversation. Do not ask the user to repeat it unless they provided an empty command.

Given that feature description, do this:

1. **Generate a concise short name** (2-4 words) for the branch:
   - Analyze the feature description and extract the most meaningful keywords
   - Create a 2-4 word short name that captures the essence of the feature
   - Use action-noun format when possible (e.g., "add-user-auth", "fix-payment-bug")
   - Preserve technical terms and acronyms (OAuth2, API, JWT, etc.)

2. **Create the feature branch** by running the script with `--short-name` (and `--json`), and do NOT pass `--number` (the script auto-detects the next globally available number across all branches and spec directories):

   ```bash
   .specify/scripts/bash/create-new-feature.sh "$ARGUMENTS" --json --short-name "<short-name>" "<feature description>"
   ```

   **IMPORTANT**:
   - Do NOT pass `--number` — the script determines the correct next number automatically
   - Always include the JSON flag (`--json`) so the output can be parsed reliably
   - You must only ever run this script once per feature
   - The JSON output will contain BRANCH_NAME and SPEC_FILE paths

3. Load `.specify/templates/spec-template.md` to understand required sections.

4. Follow this execution flow:

    1. Parse user description from Input
       If empty: ERROR "No feature description provided"
    2. Extract key concepts from description
       Identify: actors, actions, data, constraints
    3. For unclear aspects:
       - Make informed guesses based on context and industry standards
       - Only mark with [NEEDS CLARIFICATION: specific question] if:
         - The choice significantly impacts feature scope or user experience
         - Multiple reasonable interpretations exist with different implications
         - No reasonable default exists
       - **LIMIT: Maximum 3 [NEEDS CLARIFICATION] markers total**
       - Prioritize clarifications by impact: scope > security/privacy > user experience > technical details
    4. Fill User Scenarios & Testing section
       If no clear user flow: ERROR "Cannot determine user scenarios"
    5. Generate Functional Requirements
       Each requirement must be testable
       Use reasonable defaults for unspecified details (document assumptions in Assumptions section)
    6. Define Success Criteria
       Create measurable, technology-agnostic outcomes
    7. Identify Key Entities (if data involved)
    8. Return: SUCCESS (spec ready for planning)

5. Write the specification to SPEC_FILE using the template structure, replacing placeholders with concrete details.

6. **Specification Quality Validation**: After writing the initial spec, validate it against quality criteria:

   a. **Create Spec Quality Checklist** at `FEATURE_DIR/checklists/requirements.md`
   b. **Run Validation Check**: Review the spec against each checklist item
   c. **Handle Validation Results**:
      - If all items pass: Mark checklist complete and proceed
      - If items fail: List failing items, update spec, re-run validation (max 3 iterations)
      - If [NEEDS CLARIFICATION] markers remain: Present options to user (max 3 questions)

7. Report completion with branch name, spec file path, checklist results, and readiness for the next phase.

## Quick Guidelines

- Focus on **WHAT** users need and **WHY**.
- Avoid HOW to implement (no tech stack, APIs, code structure).
- Written for business stakeholders, not developers.
- DO NOT create any checklists that are embedded in the spec.

### Success Criteria Guidelines

Success criteria must be:
1. **Measurable**: Include specific metrics (time, percentage, count, rate)
2. **Technology-agnostic**: No mention of frameworks, languages, databases, or tools
3. **User-focused**: Describe outcomes from user/business perspective
4. **Verifiable**: Can be tested/validated without knowing implementation details
