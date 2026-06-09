---
id: task-20260609-release-code-hotspot-review
title: Release Code Hotspot Review
summary: Review release-merge code hotspots before continuing feature development, focusing on regressions, ownership boundaries, and cleanup risks.
status: done
current_round: 1
planner: Codex
executor: Codex
reviewer: Owner
created_at: 2026-06-09
updated_at: 2026-06-09
claimed_at: 2026-06-09
completed_at: 2026-06-09
---

# 任务：Release 代码热点审查

## 0. 任务属性 / Task Metadata

| 属性 / Field | 值 / Value |
| --- | --- |
| id | `task-20260609-release-code-hotspot-review` |
| status | `done` |
| current_round | `1` |
| planner | `Codex` |
| executor | `Codex` |
| reviewer | `Owner` |
| created_at | `2026-06-09` |
| updated_at | `2026-06-09` |
| claimed_at | `2026-06-09` |
| completed_at | `2026-06-09` |

---

## 第 1 轮 / Round 1

### 1. 计划 / Plan

Agent: Codex
Role: Planner
Date: 2026-06-09

#### 1.1 目标 / Goal

审查 release merge 后留在 main 的关键代码热点，判断是否存在明显回归风险、职责混乱、Unity 生命周期风险、或需要先修再继续开发的问题。

#### 1.2 非目标 / Non-goals

- 不重构运行时代码。
- 不修改 scene、prefab、ProjectSettings 或资源目录。
- 不恢复已经归档的旧相机 sector/inertia 方向。
- 不做完整 Unity PlayMode 验证。

#### 1.3 需要先查看的文件或区域 / Files Or Areas To Inspect First

- `Assets/Scripts/Actor/ActorCollisionResolver.cs`
- `Assets/Scripts/Actor/SpeedModifierStack.cs`
- `Assets/Scripts/Camera/ActorCameraControl.SoftLockComposer.cs`
- `Assets/Scripts/UI/CombatHudController.cs`
- `Assets/Scripts/Editor/ReleaseArenaBaker.cs`
- 相关调用点、prefab/scene 引用、编译入口。

#### 1.4 架构约束 / Architecture Constraints

- Unity 资源和 serialized references 不随意移动或手改。
- 审查结果要能指导后续小任务拆分。
- 对当前 main 的事实以工作区文件为准，不使用旧分支任务记录作为事实。

#### 1.5 允许修改范围 / Allowed Edit Scope

- 当前任务文件。
- 如需记录结论，可新增或更新 `Docs/` 下的 cleanup/review 文档。

#### 1.6 禁止修改范围 / Forbidden Changes

- `Assets/**/*.unity`
- `Assets/**/*.prefab`
- `ProjectSettings/**`
- 运行时代码文件，除非用户另行要求修复。

#### 1.7 预期行为 / Expected Behavior

完成后应得到一份清晰的代码热点审查结果：哪些可以暂时保留，哪些需要开修复任务，哪些需要 Unity 验证。

#### 1.8 验收标准 / Acceptance Criteria

- 每个热点文件都有结论。
- 高风险发现包含文件路径和可定位的代码区域。
- 未验证区域明确标注。
- 不产生 Unity serialized 文件改动。

#### 1.9 验证步骤 / Verification Steps

- 使用 `rg`/`Select-String` 检查调用点与引用。
- 使用 `git diff --check` 校验文档改动。
- 尝试可行的轻量编译或说明无法编译原因。

#### 1.10 已知风险或问题 / Known Risks Or Questions

- 本地可能无法完整编译 Unity 生成的 `.csproj`。
- 不能在当前环境内直接进行 Unity Editor PlayMode 验证。

### 2. 执行报告 / Execution Report

Agent: Codex
Role: Executor
Date: 2026-06-09

#### 修改文件 / Changed Files

- `agent-tasks/active/task-20260609-release-code-hotspot-review.md`
- `agent-tasks/active/README.md`

未修改 Unity 代码、scene、prefab、ProjectSettings 或资源文件。

#### 检查范围 / Inspection Scope

已检查 release merge 热点及相关调用/序列化接入：

- `Assets/Scripts/Actor/ActorCollisionResolver.cs`
- `Assets/Scripts/Actor/SpeedModifierStack.cs`
- `Assets/Scripts/Actor/ActorMotor.cs`
- `Assets/Scripts/Actor/ActionPlayer.cs`
- `Assets/Scripts/Impact/Effects/ActionSpeedEffect.cs`
- `Assets/Scripts/Camera/ActorCameraControl.SoftLockComposer.cs`
- `Assets/Scripts/UI/CombatHudController.cs`
- `Assets/Scripts/Editor/ReleaseArenaBaker.cs`
- `Assets/Prefabs/Function/Manager.prefab`
- `Assets/Scenes/MiHoYo_Release.unity`
- `Assets/Scenes/MiHoYo_Test.unity`

#### 发现 / Findings

1. `CombatHudController` 有一个小但真实的生命周期风险。

   `OnDisable()` 只调用 `UnbindPlayer()` / `UnbindEnemy()`，不会把 `_playerView` / `_enemyView` 隐藏。如果只禁用 `CombatHudController` 组件而不是禁用整个 GameObject，已经创建出来的 Canvas/HealthBar 可能保持最后一次 alpha，形成残留 HUD。相关位置：

   - `Assets/Scripts/UI/CombatHudController.cs:67`
   - `Assets/Scripts/UI/CombatHudController.cs:215`
   - `Assets/Scripts/UI/CombatHudController.cs:267`
   - `Assets/Scripts/UI/CombatHudController.cs:390`

   建议后续开一个很小的修复任务：`OnDisable()` 在解绑前后显式 `SetVisible(false, true)`，并保护 `_playerView` / `_enemyView` 为空的初始化阶段。

2. `ReleaseArenaBaker.BakeActiveScene()` 是高影响工具入口，需要按工具边界使用。

   它会 `EnsureLayers()`、重建 `BossArena_RuinedSanctum_Environment`、`MarkSceneDirty()` 并 `SaveScene(SceneManager.GetActiveScene())`。默认命令行入口 `BakeDefaultScene()` 指向 `Assets/Scenes/MiHoYo_Release.unity` 是清晰的，但菜单入口会保存当前打开场景。相关位置：

   - `Assets/Scripts/Editor/ReleaseArenaBaker.cs:22`
   - `Assets/Scripts/Editor/ReleaseArenaBaker.cs:34`
   - `Assets/Scripts/Editor/ReleaseArenaBaker.cs:52`
   - `Assets/Scripts/Editor/ReleaseArenaBaker.cs:297`

   结论：不需要立刻改代码，但后续使用该菜单前必须确认 active scene。若团队要长期保留该工具，建议后续加确认弹窗或把菜单名改得更明确。

3. `ActorCollisionResolver` 当前接入方式成立，但需要 PlayMode 验证互推手感。

   `ActorMotor.OnEnable()` / `OnDisable()` 注册到静态 actor 列表；`Manager.prefab` 上挂有 `ActorCollisionResolver`，并且 `MiHoYo_Release` / `MiHoYo_Test` 都实例化了该 Manager prefab。`ActorMotor` 同时在 KCC 过滤中排除 actor-on-actor 作为墙体/稳定地面，闭环是合理的。相关位置：

   - `Assets/Scripts/Actor/ActorCollisionResolver.cs:27`
   - `Assets/Scripts/Actor/ActorCollisionResolver.cs:92`
   - `Assets/Scripts/Actor/ActorCollisionResolver.cs:341`
   - `Assets/Scripts/Actor/ActorMotor.cs:319`
   - `Assets/Scripts/Actor/ActorMotor.cs:431`
   - `Assets/Prefabs/Function/Manager.prefab:132`

   未发现静态接入缺失问题。剩余风险主要是 PlayMode：多 actor 密集重叠、墙角挤压、上下层接触时的手感与抖动。

4. `SpeedModifierStack` 的当前调用链清晰，未发现明显泄漏。

   `ActionPlayer` 和 `ActorMotor` 都通过 token 添加/移除/清空速度 modifier；`ActionSpeedEffect.Reset()` 释放 token；`ActorMotor.OnDisable()` 和 `ActionPlayer.OnDisable()` 都会清理栈。相关位置：

   - `Assets/Scripts/Actor/SpeedModifierStack.cs:43`
   - `Assets/Scripts/Actor/ActionPlayer.cs:191`
   - `Assets/Scripts/Actor/ActionPlayer.cs:223`
   - `Assets/Scripts/Actor/ActorMotor.cs:204`
   - `Assets/Scripts/Actor/ActorMotor.cs:236`
   - `Assets/Scripts/Impact/Effects/ActionSpeedEffect.cs:176`

   设计约束：`SpeedModifierToken` 只有本地 int id，不能跨 owner 使用。现有调用点没有发现跨 owner 误用。

5. `SoftLockComposer` 现在是基础稳定方案，不是旧 sector/inertia 方向。

   当前 soft lock 通过运行时 follow target + player/enemy framing target + TargetGroup 工作，`CombatLockComposer` 明确把 soft lock 委托给 `SoftLockComposer`。未发现旧任务里的 `effectiveSideAmount` / sector gate 残留。相关位置：

   - `Assets/Scripts/Camera/ActorCameraControl.SoftLockComposer.cs:26`
   - `Assets/Scripts/Camera/ActorCameraControl.SoftLockComposer.cs:90`
   - `Assets/Scripts/Camera/ActorCameraControl.SoftLockComposer.cs:133`
   - `Assets/Scripts/Camera/ActorCameraControl.CombatLockComposer.cs:29`

   小清理建议：`Refresh(..., bool instant, bool updateStickySide)` 里两个参数当前未使用，后续可在相机代码清理任务中移除或恢复其语义，避免继续误导。

#### 已执行验证 / Verification Performed

- `rg` 检查热点代码调用点。
- 用脚本 GUID 检查 `ActorCollisionResolver` / `CombatHudController` 的 prefab/scene 接入：
  - `ActorCollisionResolver` guid `3f2661531750408c8fb091c7d7c537ce` 出现在 `Assets/Prefabs/Function/Manager.prefab:132`。
  - `CombatHudController` guid `fd443a6c8d684b8fb3c40e256794d13c` 出现在 `Assets/Scenes/MiHoYo_Release.unity:11551` 和 `Assets/Scenes/MiHoYo_Test.unity:8881`。
- 尝试 `dotnet build CombatSample.sln`。
- 尝试 `dotnet build CombatSample.sln -p:RestoreFallbackFolders=`。

#### 验证失败 / Verification Blocked

两次 `dotnet build` 都在代码编译前失败，原因是本机 .NET/NuGet 环境引用了不存在的 fallback package folder：

```text
C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages
```

因此本轮没有得到 C# 编译级确认。需要后续在 Unity Editor 或修复本机 .NET fallback package 配置后再编译。

#### 建议下一步 / Recommended Next Steps

1. 优先修 `CombatHudController.OnDisable()` 残留 HUD 风险，这是最小、最明确的代码修复。
2. 给 `ReleaseArenaBaker.BakeActiveScene()` 加使用边界保护或确认弹窗，防止误保存非 release scene。
3. 在 Unity PlayMode 用 `MiHoYo_Test.unity` 做一轮 smoke：
   - 玩家/敌人互推、墙角挤压、上下层接触。
   - HitStop/HitStick 叠加、打断、角色禁用后的速度恢复。
   - HUD 敌方血条绑定、敌人死亡隐藏、锁定目标切换。
   - SoftLock 基础跟随和 release 场景镜头构图。

#### 与计划的偏差 / Deviation From Plan

无代码修复；本轮保持为审查和记录。

### 3. 审查 / Review

Agent: Owner
Role: Reviewer
Date: 2026-06-09

#### 决策 / Decision

`accepted`

#### 发现或疑虑 / Findings Or Concerns

Owner 接受本轮热点审查结论，并要求直接继续处理第一个明确修复点：`CombatHudController.OnDisable()` 禁用组件时 HUD 可能残留。

#### 必要修改 / Required Changes

- 新建小任务修复 `CombatHudController` 生命周期隐藏问题。

#### 是否可以标记为 done

可以。本审查任务已完成，后续修复另开任务跟踪。
