# Executor Rules

Use these rules when asked to execute, claim, continue, or work on an AI task.

## Finding A Task
- If the user names a task, use that task.
- Otherwise scan `agent-tasks/active/` for tasks with `status: todo` or `status: changes_requested`.
- Do not claim a task assigned to another executor unless the user explicitly asks.

## Claiming
Before making edits:
- Set `executor` to your agent name.
- Set `status` to `claimed` or `in_progress`.
- Set `claimed_at` and `updated_at`.

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
- Write the execution report in the current round.
- Include changed files, behavior changes, verification performed, risks, and any plan deviation.
- Report only facts that were actually checked.
- For branch, worktree, tests, or file-scope claims, include the command or direct method used.
- If a check was not performed, write `未验证` or `未确认`.
- Do not guess changed-file categories. Use actual paths or categories from the tool output.
- If unrelated worktree changes exist, distinguish them from this task's allowed edit scope.
