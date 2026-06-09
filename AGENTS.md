# Agent Entry

This repository now uses Backlog.md as the primary AI collaboration system.

Primary workflow:
- Use Backlog.md as the shared task system. If a local MCP connection is configured, use the `backlog` MCP server.
- Read the shared workflow instructions from Backlog docs or the local Backlog task records.
- Use `backlog/` task files as the source of truth for active work.
- Keep completed Backlog tasks as the durable iteration record for later archive export.

Workspace boundary:
- The current repository is the project boundary.
- Do not treat chat history from another project as fact for this workspace.
- If the user mentions another workspace, stop and ask which repository should be edited.

Task handling:
- Prefer existing Backlog tasks over ad hoc chat memory.
- If no suitable task exists, create or update one in Backlog before substantial implementation.
- Treat `Review` as the handoff state before `Done`.
- Do not mark a task `Done` until the user or reviewer accepts the result.
- Do not automatically start the next task after finishing one.

Unity guardrails:
- Keep Unity edits small, local, and reviewable.
- Do not edit generated output such as `Library/`, `Temp/`, `obj/`, `.csproj`, or `.sln` files.
- Do not make unrelated prefab, scene, `.meta`, `ProjectSettings`, or package changes.
- Preserve serialized field names, public APIs, prefab references, and scene references unless the task explicitly requires changing them.
- Prefer focused EditMode or PlayMode tests when practical. If they are not practical, record exact manual validation steps.

Legacy note:
- `agent-system/` and `agent-tasks/` are legacy records from the previous workflow.
- Existing files under `agent-tasks/archive/` are historical raw material, not the source of truth for new work.
