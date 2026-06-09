---
id: doc-1
title: Workspace Notes
type: guide
created_date: '2026-06-09 12:21'
---

# Workspace Notes

## Current Workflow

This repository uses Backlog.md as the active AI collaboration system.

- Active work should be tracked in `backlog/tasks/`.
- If a local MCP connection is configured on the current device, use the `backlog` server.
- Treat `Review` as the handoff state before `Done`.

## Legacy Records

The previous custom workflow remains in the repository as historical material.

- `agent-system/` contains the retired collaboration rules.
- `agent-tasks/archive/` contains archived task records from that workflow.

Those files are useful as raw history, but they are not the source of truth for new work.

## Unity Guardrails

- Keep edits small, local, and reviewable.
- Avoid unrelated scene, prefab, `.meta`, `ProjectSettings`, and package changes.
- Preserve inspector-facing names and references unless the task explicitly requires a migration.
- Record exact manual validation steps when Unity-side automated verification is not practical.
