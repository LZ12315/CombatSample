# Collaboration Protocol

## Purpose
This protocol lets multiple AI agents cooperate through repository files instead of fragile chat handoff.

## Principles
- Workspace boundary comes first. Use `agent-system/WORKSPACE_BOUNDARY.md` and current repository files to identify the active workspace.
- Role follows the current instruction, not the model name.
- Role transitions are user-gated. An agent may not automatically move from planning to execution, execution to review, review to another execution round, or review to archive.
- Task files are durable coordination records.
- Plans define constraints and acceptance criteria, not exhaustive implementation scripts.
- Execution stays inside the allowed scope.
- Review is a separate step when risk or iteration matters.
- Historical task content is append-only. Preserve how the solution evolved.

## Standard Flow
1. Planner creates or updates a task in `agent-tasks/active/`.
2. Executor finds or receives a task, claims it, and executes it.
3. Executor writes an execution report in the current round.
4. Reviewer reviews the result and writes a review.
5. If more work is needed, start a new round.
6. When accepted, mark the task `done`.
7. Archive completed tasks when the user asks for archiving.

## User-Gated Roles
Each phase starts only when the user explicitly asks for that phase.

- Publishing a task does not authorize execution.
- Executing a task does not authorize review.
- Reviewing a task does not authorize starting the next execution round.
- Marking a task `done` does not authorize archiving.

When the assigned phase is complete, stop and report the result to the user. Do not continue into the next phase on your own.

If the user's instruction is ambiguous, choose the narrower role and ask before crossing into another phase.

## Workspace-Boundary Safety
Before acting on a task, resolve the target from current workspace files, not shared chat history.

- If a prompt refers to another project or workspace, stop unless the user explicitly requested cross-workspace work.
- Task files must live under `agent-tasks/` in the current workspace.
- If a task id, file path, or plan cannot be found in this workspace, do not recreate it from memory. Ask the user.
- If the chat context and current workspace files disagree, trust current workspace files and ask for confirmation.

## Round Model
Each task can contain multiple rounds.

Each round contains:
- Plan
- Execution Report
- Review

Add a new round when a review requests changes, the plan changes materially, or the task needs another execution pass.

Do not erase old rounds. They are the project memory.

## Agent Signatures
Every plan, report, and review should include:
- Agent name
- Role
- Date or timestamp when useful

Use practical names such as `Codex`, `Claude`, `DeepSeek`, or the model/user-provided name.
