---
id: task-20260609-combat-hud-disable-hide
title: Combat HUD Disable Hide
summary: Fix CombatHudController lifecycle behavior so disabling or destroying the component immediately hides generated HUD views.
status: review
current_round: 1
planner: Codex
executor: Codex
reviewer:
created_at: 2026-06-09
updated_at: 2026-06-09
claimed_at: 2026-06-09
completed_at:
---

# 任务：Combat HUD 禁用时隐藏血条

## 0. 任务属性 / Task Metadata

| 属性 / Field | 值 / Value |
| --- | --- |
| id | `task-20260609-combat-hud-disable-hide` |
| status | `review` |
| current_round | `1` |
| planner | `Codex` |
| executor | `Codex` |
| reviewer |  |
| created_at | `2026-06-09` |
| updated_at | `2026-06-09` |
| claimed_at | `2026-06-09` |
| completed_at |  |

---

## 第 1 轮 / Round 1

### 1. 计划 / Plan

Agent: Codex
Role: Planner
Date: 2026-06-09

#### 1.1 目标 / Goal

修复 `CombatHudController` 生命周期问题：禁用或销毁组件时，已创建的 player/enemy HUD view 应立即隐藏，避免 Canvas/血条残留在画面上。

#### 1.2 非目标 / Non-goals

- 不重做 HUD 布局或视觉样式。
- 不修改 scene/prefab 中的 HUD 序列化引用。
- 不改变血量绑定、敌人目标跟随、血条缓动逻辑。

#### 1.3 需要先查看的文件或区域 / Files Or Areas To Inspect First

- `Assets/Scripts/UI/CombatHudController.cs`
- `agent-tasks/active/task-20260609-release-code-hotspot-review.md`

#### 1.4 架构约束 / Architecture Constraints

- 保持修复局部化。
- 不依赖 scene/prefab YAML 修改。
- 初始化前禁用组件时不能抛空引用。

#### 1.5 允许修改范围 / Allowed Edit Scope

- `Assets/Scripts/UI/CombatHudController.cs`
- 当前任务文件
- `agent-tasks/active/README.md`
- `agent-tasks/active/task-20260609-release-code-hotspot-review.md` 的收口状态

#### 1.6 禁止修改范围 / Forbidden Changes

- `Assets/**/*.unity`
- `Assets/**/*.prefab`
- `ProjectSettings/**`
- 非 HUD 生命周期相关代码

#### 1.7 预期行为 / Expected Behavior

- `CombatHudController` 已初始化后被 disabled，player/enemy HUD 立即不可见。
- 组件重新 enabled 后，仍按原逻辑重新绑定 player/enemy 并显示可用血条。
- 初始化前 disabled/destroyed 不抛异常。

#### 1.8 验收标准 / Acceptance Criteria

- `OnDisable()` 会立即隐藏已创建 HUD view。
- `OnDestroy()` 也不会留下已创建 HUD view。
- 空 view 情况使用 null-safe 调用。
- 不修改 Unity scene/prefab/ProjectSettings。

#### 1.9 验证步骤 / Verification Steps

- `git diff --check`
- 尝试可行的 C# 编译；如本地环境阻塞，记录原因。
- 静态检查改动只影响 HUD 生命周期。

#### 1.10 已知风险或问题 / Known Risks Or Questions

- 当前环境的 `dotnet build` 可能仍被缺失 NuGet fallback package folder 阻塞。

### 2. 执行报告 / Execution Report

Agent: Codex
Role: Executor
Date: 2026-06-09

#### 修改文件 / Changed Files

- `Assets/Scripts/UI/CombatHudController.cs`
- `agent-tasks/active/task-20260609-combat-hud-disable-hide.md`
- `agent-tasks/active/task-20260609-release-code-hotspot-review.md`
- `agent-tasks/active/README.md`

#### 行为变化 / Behavior Changes

- `CombatHudController.OnDisable()` 现在会先调用 `HideViewsInstant()`，立即把 player/enemy HUD view 隐藏，再解绑 health event。
- `CombatHudController.OnDestroy()` 同样先隐藏已创建 view，再解绑事件，避免只销毁组件时留下可见血条。
- `HideViewsInstant()` 使用 null-safe 调用；组件尚未初始化、`_playerView` / `_enemyView` 仍为空时不会抛异常。
- `OnEnable()` 原有重新绑定逻辑保持不变；组件重新启用后仍会根据 player/enemy 状态显示 HUD。

#### 已执行验证 / Verification Performed

- 已运行 `git diff --check`，通过，仅有 LF/CRLF warning。
- 已静态检查 `OnDisable()`、`OnDestroy()`、`HideViewsInstant()` 调用位置。
- 已确认本轮未修改 `Assets/**/*.unity`、`Assets/**/*.prefab` 或 `ProjectSettings/**`。
- 已尝试 `dotnet build Assembly-CSharp.csproj -p:RestoreFallbackFolders=`。

#### 验证失败 / Verification Blocked

`dotnet build Assembly-CSharp.csproj -p:RestoreFallbackFolders=` 仍在代码编译前失败，原因是本机 .NET/NuGet 环境引用了不存在的 fallback package folder：

```text
C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages
```

因此本轮未得到 C# 编译级确认。需要在 Unity Editor 或修复本机 .NET fallback package 配置后再做编译/PlayMode 验证。

#### 剩余风险 / Remaining Risks

- 未执行 Unity PlayMode 验证。
- 需要在 `Assets/Scenes/MiHoYo_Test.unity` 中手动确认：禁用 `CombatHudController` 组件时 HUD 立即消失，重新启用后血条正常恢复绑定。

#### 与计划的偏差 / Deviation From Plan

无偏差。

### 3. 审查 / Review

未审查。
