# Agent System

This directory contains the workspace-level AI collaboration system.

It is not general documentation. It defines how AI agents plan, execute, review, and archive tasks in this repository.

## Entry Points
Root-level entry files stay in the repository root because tools discover them there:
- `AGENTS.md`
- `CLAUDE.md`

Those files should stay short and point back to this directory.

## Directories
- `WORKSPACE_BOUNDARY.md` defines current-workspace safety rules.
- `protocols/` defines shared workflow rules.
- `rules/` defines role-specific and Unity-specific behavior.
- `templates/` stores task templates.
- `agent-tasks/active/` stores current tasks.
- `agent-tasks/archive/` stores completed task history.

## Task Console
A small local helper exists for inspecting active task files before agents choose work:

```bash
python tools/agent_task.py list
python tools/agent_task.py next
python tools/agent_task.py validate agent-tasks/active/task-xxx.md
```

Use it to list active tasks, select the next executable task, and catch obvious task metadata issues before handoff or review.

## Roles
Any capable agent can act as:
- Planner: creates or updates a task plan.
- Executor: claims a task, makes code or asset changes, and writes an execution report.
- Reviewer: checks the result, requests changes, or marks the task done.

Roles are not tied to model names. Codex, Claude, DeepSeek, or another agent may take any role when asked.

## Source Of Truth
Task markdown files are the source of truth for AI handoff.

Chat is for commands and clarification. The task file is where durable plan, report, review, and status live.

Workspace context comes from current repository files, not from shared chat history.
