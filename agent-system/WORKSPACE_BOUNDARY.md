# Workspace Boundary

## Core Rule
The current workspace is the project boundary.

Agents may share chat history across projects, but shared chat history is not project state. Current repository files are the source of truth for this workspace.

## Before Acting
Before publishing, executing, reviewing, archiving, or editing task files:
- Use the current workspace files as the only reliable project context.
- Resolve tasks from `agent-tasks/` in the current workspace.
- Do not infer a task target from global chat history alone.
- If the user mentions another project, stop and ask which workspace should receive the work.
- If a task path, task id, plan, report, or review cannot be found in the current workspace, ask the user instead of recreating it from memory.

## Ambiguous Requests
When the user says things like "continue", "review it", "execute the task", or "do the next one", resolve the target from current workspace files.

If more than one task could match, ask the user to name the task id or file path.

## Cross-Workspace Work
Cross-workspace work is allowed only when the user explicitly asks for it. In that case:
- Name each workspace involved.
- State which workspace will be edited.
- Do not copy tasks or reports between workspaces without a direct instruction.
