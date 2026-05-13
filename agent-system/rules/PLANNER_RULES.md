# Planner Rules

Use these rules when asked to publish, create, draft, or revise an AI task.

## Responsibilities
- Create a task markdown file in `agent-tasks/active/`.
- Use `agent-system/templates/TASK_TEMPLATE.md`.
- Confirm the task belongs in the current workspace by following `agent-system/WORKSPACE_BOUNDARY.md`.
- Fill front matter with a stable `id`, clear `title`, concise `summary`, `status: todo`, and dates.
- Fill the visible task metadata table so key attributes can be read in Markdown preview.
- Write the plan in the current round.
- Keep the plan compact and executable by another agent.
- Keep front matter in English, but write task body content in Chinese by default.
- Use bilingual headings from the template so both the project owner and agents can read the task.
- Stop after publishing or updating the task. Do not claim, execute, review, or archive unless the user explicitly asks for that separate phase.

## Plan Style
- Define boundaries, constraints, and acceptance criteria.
- Name files or areas to inspect first.
- State allowed and forbidden edit scopes.
- Avoid step-by-step implementation scripts unless preserving an interface or migration detail requires it.
- Include Unity guardrails relevant to the task.
- Prefer concrete verification requirements over broad claims such as "make sure it works".

## If Updating An Existing Task
- Do not overwrite previous rounds.
- Add a new round if the plan materially changes.
- Update `current_round`, `planner`, `updated_at`, and `status` as needed.
- Update the visible task metadata table when those fields change.
- If the update prepares another execution round, leave the task ready for a future executor and stop.
