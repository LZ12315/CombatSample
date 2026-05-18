---
id: task-20260519-kcc-actor-collision-resolver
title: KCC actor collision resolver
summary: Make KCC-driven Player and Enemy actors behave like game characters when colliding with each other: actors should not be stable ground for other actors, and horizontal actor overlaps should resolve through explicit priority/weight rules.
status: todo
current_round: 1
planner: Codex
executor:
reviewer:
created_at: 2026-05-19
updated_at: 2026-05-19
claimed_at:
completed_at:
---

# 任务：KCC Actor 互推与头顶滑落

## 0. 任务属性 / Task Metadata

| 属性 / Field | 值 / Value |
| --- | --- |
| id | `task-20260519-kcc-actor-collision-resolver` |
| status | `todo` |
| current_round | `1` |
| planner | Codex |
| executor |  |
| reviewer |  |
| created_at | `2026-05-19` |
| updated_at | `2026-05-19` |
| completed_at |  |

---

## 第 1 轮 / Round 1

### 1. 计划 / Plan

Agent: Codex  
Role: Planner  
Date: 2026-05-19

#### 1.1 目标 / Goal

为当前 KCC 驱动的 `Player` / `Enemy` 角色补齐角色间碰撞规则，使它们更接近动作游戏中的角色碰撞表现：

- 角色不能被另一个角色当作稳定地面、台阶或可站立平台；站到别人头顶时应继续滑落或被侧向排开。
- 两个角色水平接触或重叠时，应根据显式游戏规则互相挤开，而不是只让主动移动的一方被 KCC 挡住。
- 支持最小可用的优先级规则：高优先级 Actor 尽量保持位置，低优先级 Actor 被水平推开；同级 Actor 可以按权重或平分位移。
- 保持现有 KCC 负责地形、墙体、重力、台阶、速度投影等底层移动能力。

#### 1.2 非目标 / Non-goals

- 不切换到 Unity 内置 `CharacterController`。
- 不把 Player/Enemy 改成完整动态 Rigidbody 物理角色。
- 默认不修改 `Assets/Plugins/KinematicCharacterController/Core/` 中的 KCC Core 代码；只有在执行阶段证明项目层 hook 完全不足时，才允许提出单独方案。
- 不重写 `ActorMotor`、ActionSystem、AI 行为树或输入系统。
- 不一次性实现复杂群体避障、NavMesh 局部避让、阵型系统或战斗占位系统。
- 不改变攻击命中、受击、Timeline、RootMotion 的既有语义，除非它们暴露出与角色互推直接相关的必要接入点。

#### 1.3 需要先查看的文件或区域 / Files Or Areas To Inspect First

- `Assets/Scripts/Actor/ActorMotor.cs`
  - `IsColliderValidForCollisions`
  - `OnMovementHit`
  - `ProcessHitStabilityReport`
  - `ComputeSolvedVelocity`
- `Assets/Scripts/Actor/Motion/ActorMotionRuntime.cs`
- `Assets/Scripts/Actor/Motion/MotionChannels.cs`
- `Assets/Prefabs/Actor/Actor.prefab`
- `Assets/Prefabs/Actor/Player.prefab`
- `Assets/Prefabs/Actor/Enemy.prefab`
- `Assets/Plugins/KinematicCharacterController/Core/KinematicCharacterMotor.cs`
  - 只读参考默认 KCC 行为，避免直接修改。
- `ProjectSettings/TagManager.asset`
  - 确认 `Player`、`Enemy`、`Ground`、`Obstacle` 等 layer。
- 相关测试或验证场景：
  - `Assets/Scenes/Test/KCC_Migration_Test.unity`
  - `Assets/Scenes/Test/EnemyAI_Test.unity`
  - `Assets/Scenes/SampleScene.unity`

#### 1.4 架构约束 / Architecture Constraints

- 保持 KCC 作为角色移动底层；Actor 互推属于项目游戏规则层，不写进 KCC Core。
- 将两个问题分开处理：
  - 稳定地面判定：在 `ActorMotor.ProcessHitStabilityReport` 或相邻项目层逻辑中禁止 actor-on-actor stable ground。
  - 水平互推：新增独立 resolver，在 KCC tick 之后按 Actor 规则处理角色间水平重叠。
- 互推只应修改角色世界位置或通过 KCC 安全接口设置位置，不直接破坏 `ActorMotionRuntime` 的速度所有权模型。
- 互推修正默认只在水平面进行；不要把角色向上推成“站台阶”效果。
- 组件和字段命名应稳定，避免破坏 prefab/scene 引用。
- 设计应允许未来扩展 Boss 不可推动、霸体、倒地、重量、阵营、战斗状态修正等规则，但本轮只实现最小可用集。

#### 1.5 允许修改范围 / Allowed Edit Scope

- `Assets/Scripts/Actor/ActorMotor.cs`
  - 允许增加 actor-on-actor 稳定性修正。
  - 允许暴露必要的只读状态或安全位置修正入口。
- 新增小型项目层脚本，例如：
  - `Assets/Scripts/Actor/ActorCollisionBody.cs`
  - `Assets/Scripts/Actor/ActorCollisionResolver.cs`
  - 或放在现有更合适的 `Assets/Scripts/Actor/` 子目录下。
- 必要时可给 `Assets/Prefabs/Actor/Actor.prefab`、`Player.prefab`、`Enemy.prefab` 添加新组件或序列化字段。
- 必要时可添加 focused EditMode 测试或调试辅助脚本。
- 可添加简短调试 Gizmo 或日志开关，但默认不刷屏。

#### 1.6 禁止修改范围 / Forbidden Changes

- 禁止直接重构 KCC Core 或替换 KCC 插件。
- 禁止删除或大范围改写现有 `ActorMotor`、`ActorMotionRuntime`、`MotionChannels` 架构。
- 禁止修改无关场景、模型、动画、Timeline、ActionAsset、VFX、输入资源。
- 禁止修改 generated Unity/IDE 输出，例如 `Library/`、`Temp/`、`.csproj`、`.sln`。
- 禁止通过全局 singleton 或隐式硬编码场景对象实现 resolver；如果需要全局协调，使用清晰、局部、可审查的组件或已有系统模式。

#### 1.7 预期行为 / Expected Behavior

- Player 和 Enemy 胶囊头顶接触时，不会被 KCC 当作稳定地面；上方角色会继续受重力/侧向解算影响，最终滑落或被水平排开。
- Player 朝低优先级 Enemy 前进时，Player 路径尽量保持，Enemy 被水平挤开。
- Enemy 朝高优先级 Player 前进时，Player 尽量不动，Enemy 自己被水平挤开，后续 AI/locomotion 可继续尝试原路径。
- 同优先级 Actor 接触时，双方按权重分担水平修正；最小版本可以平分。
- 不应影响角色与地形、墙体、障碍物、台阶、移动平台的既有 KCC 行为。
- 不应让互推引入明显垂直弹跳、穿地、站到头顶、无限抖动或每帧越推越快。

#### 1.8 验收标准 / Acceptance Criteria

- `ActorMotor.ProcessHitStabilityReport` 或等效项目层逻辑能识别另一个 `ActorMotor`/`ActorCollisionBody` 的 collider，并使该命中不被视为稳定地面或有效台阶。
- 存在明确的 Actor 互推组件或 resolver，能处理至少 Player/Enemy 两类 KCC Actor 的水平分离。
- 互推规则至少支持：
  - 优先级字段。
  - 是否可推动别人。
  - 是否可被推动。
  - 同级分摊或权重分摊。
- 互推修正为水平修正，不主动增加垂直位移。
- 高优先级 Player 推低优先级 Enemy 的场景中，Enemy 被挤开，Player 不再被完全堵死。
- 低优先级 Enemy 推高优先级 Player 的场景中，Player 不被 Enemy 推走，Enemy 被修正到旁边或外侧。
- 上方角色落到另一个角色头顶时，不会稳定停在头顶。
- 现有 KCC 地形移动没有明显回归：走地、撞墙、落地、跳跃、基础台阶仍可用。
- 所有新增序列化字段有合理默认值，不要求手动修大量场景才能启动。

#### 1.9 验证步骤 / Verification Steps

执行者应至少完成以下验证，并在执行报告中写明实际命令或手动验证方法：

- 代码级检查：
  - 确认 `ActorMotor` 的 actor-on-actor 稳定性修正只作用于其他 Actor，不影响 Ground/Obstacle。
  - 确认 resolver 不修改 KCC Core。
  - 确认 resolver 的水平修正不会写入垂直位移。
- Unity 手动验证：
  - Player 正面走向静止 Enemy，Enemy 被水平挤开，Player 不被完全卡死。
  - Enemy 正面走向静止 Player，Player 不被明显推走，Enemy 自己被水平挤开。
  - 两个 Enemy 同级接触时不会长期重叠或剧烈抖动。
  - 一个 Actor 从上方落到另一个 Actor 头顶时不会稳定站住。
  - Player 正常撞墙、走地、跳跃、落地行为无明显回归。
- 若可行，添加 focused EditMode 测试覆盖纯数学分摊规则；若不 practical，必须提供具体手动测试步骤。

#### 1.10 已知风险或问题 / Known Risks Or Questions

- KCC 的 simulation 顺序会影响两个 Actor 的临时位置；resolver 应在所有 KCC motor tick 后运行，否则可能出现顺序偏差。
- 如果 resolver 直接改 transform，可能与 KCC 的 transient position/interpolation 不一致；执行阶段需选择与 KCC 状态同步的安全入口。
- Actor prefab 当前未必有 Rigidbody；不要为了互推盲目改成动态 Rigidbody。
- Prefab 添加组件会产生 Unity 序列化变更，必须保持范围小且列明确切资产路径。
- 角色被攻击、硬直、RootMotion、HitStop、霸体时的推挤优先级可能需要额外规则；本任务先提供默认可扩展字段，不要求一次覆盖全部战斗状态。
- 多角色密集堆叠可能需要迭代次数上限和最大修正距离，避免抖动或昂贵计算。

### 2. 执行报告 / Execution Report

未开始。

### 3. 审查 / Review

未审查。
