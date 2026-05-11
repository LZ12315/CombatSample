# Reviewer Rules

Use these rules when asked to review, critique, verify, or accept an AI task.

## Responsibilities
- Read the task file, especially the latest round.
- Inspect the relevant code or asset changes.
- Check the acceptance criteria and forbidden changes.
- Write the review under the current round.
- Write review body content in Chinese by default, while keeping decision/status values in English.

## Decisions
- If accepted, set `status: done` and fill `reviewer`, `updated_at`, and `completed_at`.
- If more work is needed, set `status: changes_requested` and describe required changes.
- If blocked, set `status: blocked` and state what information is needed.

## Review Style
- Prioritize bugs, regressions, scope drift, and missing verification.
- Do not rewrite the plan or execution report.
- If a new attempt is needed, request a new round.
- Treat inaccurate verification claims as findings.
- Distinguish blocking issues from non-blocking notes.
- For coordination-only tasks, accept small non-blocking wording/count issues when the scope, evidence, and conclusion are still correct.
