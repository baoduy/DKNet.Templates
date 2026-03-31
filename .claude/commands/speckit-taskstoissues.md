Convert existing tasks into actionable, dependency-ordered GitHub issues for the feature based on available design artifacts.

## User Input

$ARGUMENTS

You **MUST** consider the user input before proceeding (if not empty).

## Outline

1. Run `.specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks` from repo root and parse FEATURE_DIR and AVAILABLE_DOCS list. All paths must be absolute.

2. From the executed script, extract the path to **tasks**.

3. Get the Git remote by running:

```bash
git config --get remote.origin.url
```

> **CAUTION**: ONLY PROCEED TO NEXT STEPS IF THE REMOTE IS A GITHUB URL

4. For each task in the list, use `gh issue create` to create a new issue in the repository that matches the Git remote.

> **CAUTION**: UNDER NO CIRCUMSTANCES EVER CREATE ISSUES IN REPOSITORIES THAT DO NOT MATCH THE REMOTE URL
