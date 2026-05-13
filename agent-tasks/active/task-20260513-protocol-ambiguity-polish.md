---
id: task-20260513-protocol-ambiguity-polish
title: Protocol ambiguity polish
summary: Make small clarifications to the agent collaboration protocol without changing the core workflow.
status: done
current_round: 1
planner: Codex
executor: Claude
reviewer: Codex
created_at: 2026-05-13
updated_at: 2026-05-13
claimed_at: 2026-05-13
completed_at: 2026-05-13
---

# 第 1 轮 / Round 1

## 计划 / Plan

Agent: Codex
Role: Planner
Date: 2026-05-13

### 目标 / Goal

对当前 `agent-system/` 中几个已经暴露出来的小歧义做轻量修补，让协议更清楚、更不容易误读，同时保持系统精简。

重点处理：
- `accepted` 审查决策与 `done` 任务状态之间的关系。
- 归档与 lessons 提炼的用户门控边界。
- `LESSON_TEMPLATE.md` 是否需要更明确的 front matter / 命名线索。
- 避免旧任务正文中的历史概念误导新任务执行者。

### 非目标 / Non-goals

- 不改变核心工作流：发布、执行、审查、归档仍然由用户明确发起。
- 不恢复 `project_id` 或 `PROJECT_BOUNDARY.md`。
- 不引入脚本或自动化工具。
- 不归档任何任务。
- 不修改 `agent-tasks/active/` 中既有任务正文。
- 不修改 Unity 代码、资源、场景、Prefab、`.meta`、`ProjectSettings`、`Packages`、`.csproj`、`.sln` 或生成目录。

### 需要先查看的文件或区域 / Files Or Areas To Inspect First

- `agent-system/WORKSPACE_BOUNDARY.md`
- `agent-system/protocols/COLLABORATION_PROTOCOL.md`
- `agent-system/protocols/TASK_PROTOCOL.md`
- `agent-system/protocols/REVIEW_PROTOCOL.md`
- `agent-system/protocols/ARCHIVE_PROTOCOL.md`
- `agent-system/protocols/STATUS_GUIDE.md`
- `agent-system/rules/PLANNER_RULES.md`
- `agent-system/rules/EXECUTOR_RULES.md`
- `agent-system/rules/REVIEWER_RULES.md`
- `agent-system/templates/TASK_TEMPLATE.md`
- `agent-system/templates/LESSON_TEMPLATE.md`
- `agent-tasks/active/README.md`
- `agent-tasks/archive/README.md`
- `agent-tasks/lessons/README.md`

### 架构约束 / Architecture Constraints

- 当前工作区边界优先，不再绑定具体项目名。
- 保持任务文件为 source of truth。
- 保持多轮历史 append-only。
- 保持用户门控阶段，不允许 Agent 自行跨阶段。
- 本任务是“小修补”，不是重写协议；优先改少量文字，避免扩大系统。

### 允许修改范围 / Allowed Edit Scope

允许修改以下文档：
- `agent-system/protocols/REVIEW_PROTOCOL.md`
- `agent-system/protocols/ARCHIVE_PROTOCOL.md`
- `agent-system/protocols/TASK_PROTOCOL.md`
- `agent-system/templates/LESSON_TEMPLATE.md`
- `agent-tasks/active/README.md`
- `agent-tasks/archive/README.md`
- `agent-tasks/lessons/README.md`

如确有必要，也可以小幅修改：
- `agent-system/rules/REVIEWER_RULES.md`

### 禁止修改范围 / Forbidden Changes

- 禁止修改 `AGENTS.md`、`CLAUDE.md`，除非执行者在报告中说明确有必要并先停下请求用户确认。
- 禁止修改 `agent-system/WORKSPACE_BOUNDARY.md`。
- 禁止修改 `agent-system/templates/TASK_TEMPLATE.md`，除非只是同步上述协议歧义所必需。
- 禁止修改既有任务文件正文，包括：
  - `agent-tasks/active/task-20260511-test-agent-handoff.md`
  - `agent-tasks/active/task-20260513-agent-system-design-review.md`
- 禁止归档任务或创建 lessons 实例。
- 禁止修改任何 Unity 项目文件。

### 预期行为 / Expected Behavior

执行者应完成一次小范围文档修补，使后续 Agent 更容易理解：
- 审查报告可以写 `Decision: accepted`，但任务状态应设为 `done`。
- `archive` 和 `lessons` 仍然需要用户明确发起；归档不自动等于提炼 lessons。
- lessons 模板如果增强，应保持轻量，不变成复杂知识库格式。
- 旧任务正文是历史证据，不应作为新任务模板；新任务应以 `agent-system/templates/TASK_TEMPLATE.md` 为准。

### 验收标准 / Acceptance Criteria

- `REVIEW_PROTOCOL.md` 明确区分“审查决策”和“任务状态”。
- `ARCHIVE_PROTOCOL.md` 明确归档与 lessons 提炼的用户门控关系。
- `LESSON_TEMPLATE.md` 若被修改，应比当前更适合追溯来源任务，但仍保持简洁。
- README 或协议中应有一句提示：旧任务正文是历史记录，新任务以当前模板和协议为准。
- 不出现 `PROJECT_BOUNDARY.md` 或 `project_id` 作为新规则要求。
- 执行报告列出所有实际修改文件和验证方式。
- 除允许范围外没有其他文件被修改。

### 验证步骤 / Verification Steps

- 搜索确认协议中没有重新引入 `PROJECT_BOUNDARY.md`。
- 搜索确认通用模板或协议没有重新要求 `project_id`。
- 检查 `REVIEW_PROTOCOL.md`、`ARCHIVE_PROTOCOL.md`、`LESSON_TEMPLATE.md` 的实际内容。
- 检查 git 状态，确认只修改允许范围内的文档和本任务文件。
- 如果某项没有检查，明确写 `未验证`。

### 已知风险或问题 / Known Risks Or Questions

- 这类小修补容易越写越多；执行者应保持克制。
- 如果执行者认为需要更大重构，应写入报告并停止，不要直接扩大范围。
- 旧任务正文里仍会保留历史概念，这是 append-only 规则的一部分，不应强行清理。

## 执行报告 / Execution Report

Agent: Claude
Role: Executor
Date: 2026-05-13

### 修改文件 / Changed Files

| 文件 | 修改内容 |
|------|---------|
| `agent-system/protocols/REVIEW_PROTOCOL.md` | 在 `Review Decisions` 下方新增一句：审查决策（如 `accepted`）用于审查区，任务状态（`done` 等）用于 front matter，`accepted` → `status: done` |
| `agent-system/protocols/ARCHIVE_PROTOCOL.md` | 在 `Lessons` 节首新增一句：lessons 提炼是独立的用户门控动作，归档不自动触发 lessons 提炼 |
| `agent-system/templates/LESSON_TEMPLATE.md` | 增加 YAML front matter（`id`, `source_task`, `created_at`, `category`），将正文字段从列表格式改为小节标题格式，移除标题中的 `YYY-MM` 前缀 |
| `agent-system/protocols/TASK_PROTOCOL.md` | 在 `Body Structure` 下方新增一句：旧任务正文是历史记录，可能反映了不再适用的旧约定，新任务应以当前模板和协议为准 |
| `agent-tasks/active/README.md` | 新增两句：旧任务正文可能反映旧约定，使用 `TASK_TEMPLATE.md` 和当前 `protocols/` 作为新任务格式的权威来源 |

### 行为变化 / Behavior Changes

无核心工作流变更。所有修改为文档澄清，不影响用户门控阶段、多轮追加式历史、角色定义等核心机制。

### 已执行验证 / Verification Performed

1. **git diff 检查** — `git diff --stat` 确认本任务修改了 5 个文件。diff 详细内容已审查，确认每处修改符合计划要求。
2. **`PROJECT_BOUNDARY.md` 未重新引入** — `grep -r "PROJECT_BOUNDARY\.md" agent-system/protocols/` 返回空结果。
3. **`project_id` 未作为新要求引入** — `grep -r "project_id" agent-system/templates/` 返回空结果。`TASK_PROTOCOL.md` 中的 `project_id` 出现在 "New task files do not need `project_id`" 语句中（为用户此前修改，非本次任务引入，且含义是说不必填写，与验收标准一致）。
4. **REVIEW_PROTOCOL.md** — 已读取确认新增内容正确区分决策与状态。
5. **ARCHIVE_PROTOCOL.md** — 已读取确认 lessons 门控说明已添加。
6. **LESSON_TEMPLATE.md** — 已读取确认 front matter 完整，正文字段清晰。
7. **TASK_PROTOCOL.md** — 已读取确认旧任务正文提示已添加。
8. **active/README.md** — 已读取确认旧约定提示已添加。
9. **禁止修改范围** — 未修改 `AGENTS.md`、`CLAUDE.md`、`WORKSPACE_BOUNDARY.md`、`TASK_TEMPLATE.md`、既有任务正文、Unity 文件。
10. **未验证** — 未在 Unity Editor 中验证（本任务不涉及 Unity）。

### 与计划的偏差 / Plan Deviation

无偏差。严格在允许修改范围内执行四项修补。未扩大范围，未引入 `PROJECT_BOUNDARY.md` 或 `project_id` 作为新规则要求。

### 剩余风险 / Remaining Risks

- 各项修补均为小幅度文字添加，审查者可逐行对照验收标准确认。
- LESSON_TEMPLATE.md 的 front matter 增加了结构，但仍是 4 字段的轻量设计。如果后续需要更复杂的 lessons 系统，应由单独任务处理。

## 审查 / Review

Agent: Codex
Role: Reviewer
Date: 2026-05-13

### 决策 / Decision

accepted

### 发现或疑虑 / Findings Or Concerns

未发现阻塞问题。

实际修改保持在任务允许范围内，核心工作流没有被扩大或改写：
- `agent-system/protocols/REVIEW_PROTOCOL.md` 已明确区分审查决策 `accepted` 与 front matter 任务状态 `done`。
- `agent-system/protocols/ARCHIVE_PROTOCOL.md` 已明确 lessons 提炼是独立的用户门控动作，归档不会自动触发 lessons。
- `agent-system/templates/LESSON_TEMPLATE.md` 增加了轻量 front matter，并保留简洁正文结构，可以追溯来源任务。
- `agent-system/protocols/TASK_PROTOCOL.md` 与 `agent-tasks/active/README.md` 已说明旧任务正文是历史记录，新任务应以当前模板和协议为准。

已用 `rg -n "PROJECT_BOUNDARY\\.md|project_id" agent-system agent-tasks\\active\\README.md` 检查，未发现 `PROJECT_BOUNDARY.md` 被重新引入；`project_id` 只出现在 `TASK_PROTOCOL.md` 中“新任务不需要 `project_id`”的否定说明里，不构成新规则要求。

注意：当前 git 工作区存在其他历史修改和未跟踪文件，因此本次审查只对本任务相关文件和本任务 diff 作范围判断，不把全工作区状态视为本任务结果。

### 必要修改 / Required Changes

无。

### 是否可以标记为 `done`

可以。已将任务 front matter 更新为 `status: done`，并填写 `reviewer` 与 `completed_at`。
