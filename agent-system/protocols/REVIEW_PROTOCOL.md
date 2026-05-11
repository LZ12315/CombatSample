# Review Protocol

## Review Goal
Reviewers protect the project from drift, regressions, and incomplete handoff.

## What To Check
- Did the executor stay inside the allowed edit scope?
- Did the implementation satisfy the acceptance criteria?
- Were Unity serialization, prefab, scene, and `.meta` changes handled carefully?
- Did the executor report changed files, verification, and risks?
- Are there untested behaviors that should block completion?
- Does the task need another round?

## Review Decisions
Use one of these decisions:
- `accepted`: The task can be marked `done`.
- `changes_requested`: More work is needed in a new round.
- `blocked`: The task cannot continue without user input or a revised plan.

## Writing Reviews
Write reviews in the current round under `## Review`.

Do not rewrite the execution report. If something is wrong, say so in the review and request a new round.
