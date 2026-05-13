# Executor Rules

Use these rules when asked to execute, claim, continue, or work on an AI task.

Execution must be explicitly requested by the user. Finding a `todo` or `changes_requested` task is not permission to execute it unless the user asked you to execute or claim a task.

Before claiming a task, confirm it belongs to the current workspace using `agent-system/WORKSPACE_BOUNDARY.md`. The task file should live under `agent-tasks/` in this workspace.

## Finding A Task
- If the user names a task, use that task.
- Otherwise scan `agent-tasks/active/` for tasks with `status: todo` or `status: changes_requested`.
- Do not claim a task assigned to another executor unless the user explicitly asks.

## Claiming
Before making edits:
- Set `executor` to your agent name.
- Set `status` to `claimed` or `in_progress`.
- Set `claimed_at` and `updated_at`.
- Update the visible task metadata table when front matter fields change.

## Execution
- Read the current round plan before editing.
- Stay inside the allowed edit scope.
- Follow `agent-system/rules/UNITY_RULES.md`.
- If the plan conflicts with the codebase or Unity serialization constraints, set `status: blocked` and explain why.
- Do not perform unrelated cleanup or broad refactors.
- Write task body updates in Chinese by default, while keeping paths, commands, code identifiers, and status values unchanged.

## Reporting
After execution:
- Set `status: review`.
- Update `updated_at`.
- Update the visible task metadata table.
- Write the execution report in the current round.
- Include changed files, behavior changes, verification performed, risks, and any plan deviation.
- Report only facts that were actually checked.
- For branch, worktree, tests, or file-scope claims, include the command or direct method used.
- If a check was not performed, write `未验证` or `未确认`.
- Do not guess changed-file categories. Use actual paths or categories from the tool output.
- If unrelated worktree changes exist, distinguish them from this task's allowed edit scope.
- Stop after writing the execution report. Do not write the review, mark the task `done`, archive the task, or start another round unless the user explicitly asks for that separate phase.
