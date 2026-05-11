# CombatSample Agent Entry

This file is intentionally short. Tooling discovers `AGENTS.md` at the repository root, so it stays here as the stable entry point.

The durable AI collaboration system lives in:
- `agent-system/`

Task files live in:
- `agent-tasks/active/`
- `agent-tasks/archive/`
- `agent-tasks/lessons/`

Before planning, executing, reviewing, or archiving AI tasks, read:
- `agent-system/README.md`
- `agent-system/protocols/COLLABORATION_PROTOCOL.md`
- `agent-system/protocols/TASK_PROTOCOL.md`
- `agent-system/protocols/REVIEW_PROTOCOL.md`
- `agent-system/protocols/ARCHIVE_PROTOCOL.md`
- `agent-system/protocols/STATUS_GUIDE.md`
- The relevant role rule in `agent-system/rules/`

Core rules:
- Any agent can act as planner, executor, or reviewer. The current task and user instruction decide the role.
- Use task markdown files as the source of truth for cross-agent handoff.
- Keep task rounds append-only: plan, execution report, and review are added round by round until done.
- Do not overwrite another agent's historical plan, report, or review. Add a new round or append a dated note instead.
- Keep Unity edits local and reviewable. Follow `agent-system/rules/UNITY_RULES.md`.
