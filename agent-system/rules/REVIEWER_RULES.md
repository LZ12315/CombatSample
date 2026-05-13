# Reviewer Rules

Use these rules when asked to review, critique, verify, or accept an AI task.

Review must be explicitly requested by the user. A task with `status: review` is ready for review, but it is not permission for an agent to review it without a user request.

Before reviewing a task, confirm it belongs to the current workspace using `agent-system/WORKSPACE_BOUNDARY.md`. The task file should live under `agent-tasks/` in this workspace.

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
- Update the visible task metadata table when front matter fields change.
- Stop after writing the review. Do not execute requested changes, start a new round, or archive the task unless the user explicitly asks for that separate phase.

## Review Style
- Prioritize bugs, regressions, scope drift, and missing verification.
- Do not rewrite the plan or execution report.
- If a new attempt is needed, request a new round.
- Treat inaccurate verification claims as findings.
- Distinguish blocking issues from non-blocking notes.
- For coordination-only tasks, accept small non-blocking wording/count issues when the scope, evidence, and conclusion are still correct.
