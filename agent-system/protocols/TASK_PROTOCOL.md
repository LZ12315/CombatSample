# Task Protocol

## Task Location
Active tasks live in:
- `agent-tasks/active/`

Completed task history lives in:
- `agent-tasks/archive/`

Lessons distilled from completed tasks live in:
- `agent-tasks/lessons/`

## File Naming
Use readable names:

```text
task-YYYYMMDD-short-topic.md
```

Example:

```text
task-20260511-actor-motor-input-decouple.md
```

## Front Matter
Every task file starts with YAML front matter.

Use English keys and stable English status values in front matter. This keeps tasks easy for agents and scripts to parse.

Required fields:

```yaml
---
id: task-YYYYMMDD-short-topic
title: Short task title
summary: One or two sentence summary
status: todo
current_round: 1
planner:
executor:
reviewer:
created_at: YYYY-MM-DD
updated_at: YYYY-MM-DD
claimed_at:
completed_at:
---
```

The front matter is the mutable task index. The body is the append-only work history.

## Language Policy
- Front matter keys and status values stay in English.
- File names and directory names stay in English.
- Task body content should be written in Chinese by default so the project owner can read it comfortably.
- Use bilingual section headings when they help agents parse the document, for example `## 计划 / Plan`.
- Keep code identifiers, paths, commands, API names, and exact tool output in their original language.
- If the user explicitly requests another language for a specific task, follow that request.

## Body Structure
Use rounds.

```md
# 第 1 轮 / Round 1

## 计划 / Plan

## 执行报告 / Execution Report

## 审查 / Review
```

If more work is needed, append:

```md
# 第 2 轮 / Round 2

## 计划 / Plan

## 执行报告 / Execution Report

## 审查 / Review
```

## Planning Requirements
The plan should include:
- 目标 / Goal
- 非目标 / Non-goals
- 需要先查看的文件或区域 / Files or areas to inspect first
- 架构约束 / Architecture constraints
- 允许修改范围 / Allowed edit scope
- 禁止修改范围 / Forbidden changes
- 预期行为 / Expected behavior
- 验收标准 / Acceptance criteria
- 验证步骤 / Verification steps
- 已知风险或问题 / Known risks or questions

Prefer clear boundaries and acceptance criteria over detailed code instructions.

## Claiming A Task
An executor claiming a task should:
- Choose a task with `status: todo` or `status: changes_requested`, unless the user specifies a task.
- Set `executor` to its agent name.
- Set `status` to `claimed` or `in_progress`.
- Set `claimed_at` and `updated_at`.

Do not claim a task already assigned to another executor unless the user explicitly asks.

## Execution Requirements
The execution report should include:
- Agent signature
- 修改文件 / Changed files
- 行为变化 / Behavior changes
- 已执行验证 / Verification performed
- 剩余风险或未验证区域 / Remaining risks or unverified areas
- 与计划的偏差及原因 / Any deviation from the plan and why

## Verification Accuracy
Verification reports must be factual and scoped.

- Only write a claim as confirmed if the agent actually inspected it.
- For branch, status, tests, or file-scope claims, include the command or direct method used.
- If something was not checked, write `未验证` or `未确认` instead of implying success.
- Do not guess examples of changed files. Use the actual paths or categories shown by the tool.
- Distinguish task-scoped verification from full-worktree verification. For example, say "本任务未修改 Unity 路径" instead of "工作区干净" when unrelated changes exist.
- If tool output conflicts with the previous report, correct it in a new round instead of rewriting history.

## Review Requirements
The review should include:
- Agent signature
- 决策 / Decision
- 发现或疑虑 / Findings or concerns
- 必要修改 / Required changes, if any
- 是否可以标记为 `done`

## Completion
A task is complete only when a reviewer or the user accepts it.

When complete:
- Set `status: done`.
- Set `completed_at`.
- Keep the task in `active/` until it is archived.
