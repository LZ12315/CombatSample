# Agent System

This directory contains the project-level AI collaboration system for CombatSample.

It is not general documentation. It defines how AI agents plan, execute, review, archive, and learn from tasks in this repository.

## Entry Points
Root-level entry files stay in the repository root because tools discover them there:
- `AGENTS.md`
- `CLAUDE.md`

Those files should stay short and point back to this directory.

## Directories
- `protocols/` defines shared workflow rules.
- `rules/` defines role-specific and Unity-specific behavior.
- `templates/` stores task and lesson templates.
- `agent-tasks/active/` stores current tasks.
- `agent-tasks/archive/` stores completed task history.
- `agent-tasks/lessons/` stores distilled project lessons.

## Roles
Any capable agent can act as:
- Planner: creates or updates a task plan.
- Executor: claims a task, makes code or asset changes, and writes an execution report.
- Reviewer: checks the result, requests changes, or marks the task done.

Roles are not tied to model names. Codex, Claude, DeepSeek, or another agent may take any role when asked.

## Source Of Truth
Task markdown files are the source of truth for AI handoff.

Chat is for commands and clarification. The task file is where durable plan, report, review, status, and lessons live.
