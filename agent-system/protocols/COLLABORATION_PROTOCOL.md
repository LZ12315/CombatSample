# Collaboration Protocol

## Purpose
This protocol lets multiple AI agents cooperate through repository files instead of fragile chat handoff.

## Principles
- Role follows the current instruction, not the model name.
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
7. Archive completed tasks and extract lessons when useful.

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
