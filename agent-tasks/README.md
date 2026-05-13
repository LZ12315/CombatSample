# Agent Tasks

This directory stores AI task files for cross-agent collaboration.

## Directories
- `active/` contains tasks that are not finished or not archived.
- `archive/` contains completed task history.

## Rules
- Active tasks use the format in `agent-system/templates/TASK_TEMPLATE.md`.
- Task status is controlled by `agent-system/protocols/STATUS_GUIDE.md`.
- Task history is append-only by round.
- Completed tasks should be archived according to `agent-system/protocols/ARCHIVE_PROTOCOL.md`.
