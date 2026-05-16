# Agent Entry

This file is intentionally short. Tooling discovers `AGENTS.md` at the repository root, so it stays here as the stable entry point.

The durable AI collaboration system lives in:
- `agent-system/`

Task files live in:
- `agent-tasks/active/`
- `agent-tasks/archive/`

Task console helper:
- `python Tool/agent_task.py list` lists active task files.
- `python Tool/agent_task.py next` shows the next executable active task.
- `python Tool/agent_task.py validate <task-file>` checks task front matter and basic state consistency.

Before planning, executing, reviewing, or archiving AI tasks, read:
- `agent-system/WORKSPACE_BOUNDARY.md`
- `agent-system/README.md`
- `agent-system/protocols/COLLABORATION_PROTOCOL.md`
- `agent-system/protocols/TASK_PROTOCOL.md`
- `agent-system/protocols/REVIEW_PROTOCOL.md`
- `agent-system/protocols/ARCHIVE_PROTOCOL.md`
- `agent-system/protocols/STATUS_GUIDE.md`
- The relevant role rule in `agent-system/rules/`

Core rules:
- The current workspace is the project boundary. Follow `agent-system/WORKSPACE_BOUNDARY.md`; do not use chat history from another project as fact for this workspace.
- Any agent can act as planner, executor, or reviewer. The current task and user instruction decide the role.
- Agents must not move themselves into another role. Publishing, executing, reviewing, and archiving are separate user-initiated actions.
- Use task markdown files as the source of truth for cross-agent handoff.
- When choosing an active task, prefer the task console helper over manually guessing from the directory listing.
- Keep task rounds append-only: plan, execution report, and review are added round by round until done.
- Do not overwrite another agent's historical plan, report, or review. Add a new round or append a dated note instead.
- Keep Unity edits local and reviewable. Follow `agent-system/rules/UNITY_RULES.md`.
