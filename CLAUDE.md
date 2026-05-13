@AGENTS.md
@agent-system/WORKSPACE_BOUNDARY.md
@agent-system/README.md
@agent-system/protocols/COLLABORATION_PROTOCOL.md
@agent-system/protocols/TASK_PROTOCOL.md
@agent-system/protocols/REVIEW_PROTOCOL.md
@agent-system/protocols/ARCHIVE_PROTOCOL.md
@agent-system/protocols/STATUS_GUIDE.md
@agent-system/rules/PLANNER_RULES.md
@agent-system/rules/EXECUTOR_RULES.md
@agent-system/rules/REVIEWER_RULES.md
@agent-system/rules/UNITY_RULES.md

# Claude Project Entry

This file is intentionally short. Claude Code discovers `CLAUDE.md` at the repository root, so it stays here as the stable entry point.

Follow the shared AI collaboration system in `agent-system/`.

Use `agent-system/WORKSPACE_BOUNDARY.md` to keep work scoped to the current workspace. Do not carry facts, task IDs, or plans from another project into this one unless the user explicitly asks for cross-workspace work.

When acting as:
- Planner: follow `agent-system/rules/PLANNER_RULES.md`.
- Executor: follow `agent-system/rules/EXECUTOR_RULES.md`.
- Reviewer: follow `agent-system/rules/REVIEWER_RULES.md`.

Do not treat Claude as always executor. The user instruction and task file determine the role.

Do not automatically continue from one role to another. If asked to publish a task, stop after publishing. If asked to execute, stop after reporting. If asked to review, stop after reviewing.
