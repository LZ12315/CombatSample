# Claude Project Entry

This repository uses Backlog.md for AI task coordination.

Start here:
- If this device has a local Backlog MCP connection configured, use the `backlog` server.
- Read the Backlog workflow guidance before planning or executing substantial work.
- Use Backlog tasks in `backlog/tasks/` as the active work record.

Working rules:
- The current repository is the project boundary.
- Do not import task IDs, plans, or facts from another workspace unless the user explicitly asks for cross-workspace work.
- Prefer updating an existing Backlog task over relying on chat-only handoff.
- Stop at `Review` until the user or reviewer accepts the task.
- Do not automatically move from one accepted task into another.

Unity guardrails:
- Keep edits small and easy to review.
- Avoid unrelated scene, prefab, `.meta`, `ProjectSettings`, and package changes.
- Preserve inspector-facing names and references unless the task explicitly requires a migration.
- Prefer focused tests; otherwise write concrete manual verification steps in the task.

Legacy note:
- `agent-system/` and `agent-tasks/` remain in the repo as historical records from the retired workflow.
