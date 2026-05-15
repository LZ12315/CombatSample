---
id: task-20260515-actor-motor-timescale-motion-delta
title: Fix ActorMotor movement timescale delta semantics
summary: 修正 ActorMotor 中 MovementTimeScale 只缩放输出速度、未缩放运动通道步进时间的问题，避免 HitStop/HitStick 期间 Impulse、重力和阻尼被正常时间提前消耗。
status: review
current_round: 1
planner: ChatGPT
executor: Claude
reviewer:
created_at: 2026-05-15
updated_at: 2026-05-15
claimed_at:
completed_at:
---

# 任务：修正 ActorMotor MovementTimeScale 的运动时间语义

## 0. 任务属性 / Task Metadata

| 属性 / Field | 值 / Value |
| --- | --- |
| id | `task-20260515-actor-motor-timescale-motion-delta` |
| status | `review` |
| current_round | `1` |
| planner | `ChatGPT` |
| executor | `Claude` |
| reviewer |  |
| created_at | `2026-05-15` |
| updated_at | `2026-05-15` |
| completed_at |  |

---

## 第 1 轮 / Round 1

### 1. 计划 / Plan

Agent: ChatGPT  
Role: Planner  
Date: 2026-05-15

#### 1.1 目标 / Goal

修正 `ActorMotor` / `ActorMotionRuntime` 中 `MovementTimeScale` 的时间语义，使 HitStop / HitStick 等慢动作效果同时影响：

- 角色最终输出给 KCC 的 request velocity；
- Actor 内部运动通道的状态演化时间，包括 gravity、horizontal impulse drag、vertical impulse air drag。

当前问题是：`MovementTimeScale` 已经缩放了最终输出速度，但 `MotionRuntime.StepChannels(...)` 仍然使用 KCC 传入的真实 `deltaTime`。这会导致 HitStop 期间角色移动变慢，但 Impulse、重力和阻尼仍按正常时间消耗，表现为带 Impulse 的受击 Asset 距离或高度变短。

#### 1.2 非目标 / Non-goals

本任务不做以下事情：

- 不移除 `ActorMotor.MovementTimeScale`。
- 不回退 `ActionSpeedEffect` 对 `ActorMotor.SetMovementTimeScale(...)` 的调用。
- 不重做 HitStop / HitStick 配置系统。
- 不批量修改 ActionAsset / Timeline / playable 资源数值。
- 不重构 KCC 或修改 `KinematicCharacterController` 插件源码。
- 不在本轮重新设计 RootMotion 管线。
- 不改变 `ComposeKccVelocity(...)` 使用真实 `deltaTime` 将 RootMotion 位移换算为 KCC velocity 的架构语义，除非执行阶段发现明确 bug 并在报告中说明。

#### 1.3 需要先查看的文件或区域 / Files Or Areas To Inspect First

优先检查：

- `Assets/Scripts/Actor/ActorMotor.cs`
  - `Update()`
  - `UpdateVelocity(...)`
  - `AfterCharacterUpdate(...)`
  - `ComputeSolvedVelocity(...)`
- `Assets/Scripts/Actor/Motion/ActorMotionRuntime.cs`
  - `SetMovementTimeScale(...)`
  - `StepChannels(...)`
  - `ComposeKccVelocity(...)`
- `Assets/Scripts/Actor/Motion/MotionChannels.cs`
  - `StepGravity(...)`
  - `StepHorizontalDrag(...)`
  - `StepVerticalImpulse(...)`
  - `ComposeHorizontal(...)`
  - `ComposeVertical(...)`
- `Assets/Scripts/Actor/Motion/RootMotionBuffer.cs`
  - 确认 RootMotion snapshot / pending 消费语义
- `Assets/Scripts/Impact/Effects/ActionSpeedEffect.cs`
  - 确认 HitStop / HitStick 如何设置 `MovementTimeScale`
- 相关受击 Asset：
  - `Assets/Create/ActionAsset/Hit/Hit_Air_Launch/Hit_Air_Launch_Timeline.playable`
  - `Assets/Create/ActionAsset/Hit/Hit_Air_ReLaunch/Hit_Air_ReLaunch_Timeline.playable`
- 行为基线文档：
  - `Document/plans/actor-motion-validation.md`

#### 1.4 架构约束 / Architecture Constraints

需要明确区分两种时间：

`realDeltaTime = deltaTime`

`motionDeltaTime = realDeltaTime * MovementTimeScale`

语义约束：

- `realDeltaTime` 是 Unity / KCC 当前 tick 的真实时间。
- `motionDeltaTime` 是 ActorMotor 本地运动时间。
- 推进 Actor 内部运动状态时使用 `motionDeltaTime`。
- 与 KCC 真实 tick 对接、速度换算、solved velocity readout 时保留 `realDeltaTime`。

具体规则：

- `MotionRuntime.StepChannels(...)` 应使用 `motionDeltaTime`。
- `MotionChannels.StepGravity(...)`、`StepHorizontalDrag(...)`、`StepVerticalImpulse(...)` 接收到的 `dt` 应代表 Actor 本地运动时间。
- `ComposeKccVelocity(...)` 应继续接收真实 `deltaTime`，因为 KCC 后续会用真实 `deltaTime` 积分 velocity。
- RootMotion 分支中的 `PendingPosition / deltaTime * MovementTimeScale` 应保持真实 `deltaTime` 换算逻辑，避免慢动作被抵消。
- `ComputeSolvedVelocity(...)` 应继续使用真实 `deltaTime`，因为它是在用 KCC 最终真实帧位移反算 gameplay velocity。
- 不要把所有 `deltaTime` 无脑替换为 `motionDeltaTime`。

#### 1.5 允许修改范围 / Allowed Edit Scope

允许修改：

- `Assets/Scripts/Actor/ActorMotor.cs`
  - 在 `UpdateVelocity(...)` 中计算 `motionDeltaTime`。
  - 将 `MotionRuntime.StepChannels(deltaTime, ...)` 改为传入 `motionDeltaTime`。
  - 适当增加局部变量命名，使 `realDeltaTime` / `motionDeltaTime` 语义清晰。
  - 必要时补充简短注释，说明为什么 `StepChannels` 与 `ComposeKccVelocity` 使用不同 delta。
- `Assets/Scripts/Actor/Motion/ActorMotionRuntime.cs`
  - 可选：调整参数名或注释，例如把 `StepChannels(float deltaTime, ...)` 的参数命名为 `motionDeltaTime`。
  - 可选：补充 `ComposeKccVelocity(...)` 参数注释，说明这里的 `deltaTime` 是 KCC real delta。
- `Assets/Scripts/Actor/Motion/MotionChannels.cs`
  - 可选：更新注释，说明 `dt` 是 Actor motion delta。
- `Document/plans/actor-motion-validation.md`
  - 可选：补充一条验证记录或待验证项，记录 HitStop + Impulse 场景。

#### 1.6 禁止修改范围 / Forbidden Changes

禁止：

- 修改 `Assets/Plugins/KinematicCharacterController/` 下插件源码。
- 修改 hit action / reaction asset 里的 impulse force 数值来“调手感掩盖问题”。
- 移除或绕过 `MovementTimeScale`。
- 将 `ComposeKccVelocity(...)` 改为使用 `motionDeltaTime` 做 RootMotion 除法。
- 将 `ComputeSolvedVelocity(...)` 改为使用 `motionDeltaTime`。
- 在多个底层 channel 内部分散读取 `MovementTimeScale`，避免 time scale 语义扩散。
- 引入新的全局 time scale 或静态状态。
- 在未验证前修改 `ForceUnground` 的 pause 消费语义。

#### 1.7 预期行为 / Expected Behavior

修复后：

- HitStop / HitStick 期间，角色运动输出变慢。
- 同时，gravity、horizontal impulse drag、vertical impulse air drag 的演化也按 `MovementTimeScale` 变慢。
- 当 `MovementTimeScale == 0` 时，传给运动通道的 `motionDeltaTime == 0`，依赖 dt 的运动通道自然不推进。
- 带 Impulse 的受击 Asset 在 HitStop / HitStick 后不应明显丢失击退距离或 launch 高度。
- RootMotion Managed 模式下，RootMotion 位移仍按 `MovementTimeScale` 缩放输出。
- KCC 仍使用真实 `deltaTime` 做底层移动和碰撞解算。
- `CurrentVelocity` / solved velocity 读数仍表示真实世界速度，不因 slow motion 被反向放大。

#### 1.8 验收标准 / Acceptance Criteria

代码层面：

- `ActorMotor.UpdateVelocity(...)` 中存在清晰的 `motionDeltaTime = deltaTime * MovementTimeScale` 或等价逻辑。
- `MotionRuntime.StepChannels(...)` 使用 `motionDeltaTime`。
- `MotionRuntime.ComposeKccVelocity(...)` 仍使用真实 `deltaTime`。
- `ComputeSolvedVelocity(...)` 仍使用真实 `deltaTime`。
- 没有修改 KCC 插件源码。
- 没有修改受击 Asset 的 impulse force 数值来规避问题。

行为层面：

- 使用带 `SpeedEffectConfig(speedScale < 1, affectBothParties = true)` 的攻击命中目标后，受击者 HitStop / HitStick 期间变慢，但恢复后 Impulse 距离/高度不再明显缩水。
- `Hit_Air_Launch` / `Hit_Air_ReLaunch` 的 launch 高度在有 HitStop 和无 HitStop 对照下更接近。
- 普通 locomotion 慢动作仍然按预期变慢。
- 普通跳、二段跳、落地、撞天花板、RootMotion Managed / External 不出现明显回归。

#### 1.9 验证步骤 / Verification Steps

建议执行以下验证：

1. 代码检查
   - 确认 `StepChannels` 使用 `motionDeltaTime`。
   - 确认 `ComposeKccVelocity` 和 `ComputeSolvedVelocity` 保留真实 `deltaTime`。
   - 确认没有资源数值调参掩盖问题。

2. 手动场景验证
   - 使用一个带 `SpeedEffectConfig` 且 `affectBothParties = true` 的攻击命中目标。
   - 对比修复前后带 Impulse 的受击 Asset 飞行距离/高度。
   - 推荐检查：
     - `Hit_Air_Launch`
     - `Hit_Air_ReLaunch`

3. 对照验证
   - 临时关闭 target 受影响，例如将测试用命中效果的 `affectBothParties` 置为 false，观察受击者 Impulse 是否恢复正常。
   - 恢复 `affectBothParties = true` 后验证修复是否仍保持正常距离。

4. 回归验证
   - 普通地面移动。
   - 普通跳。
   - 二段跳。
   - 斜坡移动。
   - 落地。
   - 撞天花板。
   - RootMotion Managed。
   - RootMotion External。
   - Action 切换与 Velocity Owner。

5. Unity 验证记录
   - 如果无法运行 Unity，需要在执行报告中明确写 `未验证 Unity 运行时表现`。
   - 如果只做了代码静态检查，也需要明确写清楚验证范围。

#### 1.10 已知风险或问题 / Known Risks Or Questions

- `ForceUnground` 在 `MovementTimeScale == 0` 时是否应该立即消费，还是等恢复后消费，目前不在本任务第一轮强行修改；执行者如发现它影响 HitStop + launch 语义，需要在执行报告中单独说明。
- RootMotion snapshot 在完全暂停时是否应该消费，需要谨慎观察；本任务第一轮不主动重构 RootMotionBuffer。
- `PublishSolvedVelocity(...)` 的 smoothing delta 当前建议继续使用真实 `deltaTime`，因为它是 readout 层，不是运动能量演化层；如执行者发现动画读数受 HitStop 影响异常，应单独记录为后续任务。
- 慢动作 scale 很小但不为 0，例如 `0.05`，不应该被当成完全暂停。不要使用过大的 epsilon 把慢动作误判为 paused。
- 本任务的核心是修正时间语义，不是重新调手感。修复后如果手感变化，应优先确认是否恢复到正确物理语义，再决定是否另开调参任务。

### 2. 执行报告 / Execution Report

Agent: Claude  
Role: Executor  
Date: 2026-05-15  

---

#### 修改文件 / Changed Files

| 文件 | 修改 |
|------|------|
| `Assets/Scripts/Actor/ActorMotor.cs` | `UpdateVelocity` 中引入 `motionDeltaTime = deltaTime * MovementTimeScale`，传给 `StepChannels`；`ComposeKccVelocity` 和 `ComputeSolvedVelocity` 保持使用真实 `deltaTime` |
| `Assets/Scripts/Actor/Motion/ActorMotionRuntime.cs` | `StepChannels` 参数更名为 `motionDeltaTime`；为 `StepChannels` 和 `ComposeKccVelocity` 添加 XML 注释说明 delta 语义 |
| `Assets/Scripts/Actor/Motion/MotionChannels.cs` | `StepGravity`、`StepHorizontalDrag`、`StepVerticalImpulse` 三处注释更新，说明 `dt` 是 Actor 本地 motion delta |

---

#### 行为变化

**修复前：** `MovementTimeScale` 只缩放输出速度（`ComposeKccVelocity` 中的 `ts`），不缩放运动通道的时间演化。HitStop 期间 `StepChannels` 仍用真实 `deltaTime` 推进重力、Impulse 阻尼等，导致 Impulse 距离/高度被正常时间消耗掉。

**修复后：** `MovementTimeScale` 同时影响：
- 输出速度（`ComposeKccVelocity` 中的 `ts` 缩放），**保持不变**
- 运动通道演化时间（`StepChannels` 中的 `motionDeltaTime`），**新增**

`MovementTimeScale == 0` 时，`motionDeltaTime == 0`，重力/阻尼自然不推进。

---

#### 代码变更详述

**ActorMotor.UpdateVelocity**（ActorMotor.cs: 293-305）：

```csharp
// 新增 motionDeltaTime 计算
float motionDeltaTime = deltaTime * MovementTimeScale;

MotionRuntime.StepChannels(motionDeltaTime, grounded, config);

// ComposeKccVelocity 仍使用 deltaTime（KCC 真实 tick）
currentVelocity = MotionRuntime.ComposeKccVelocity(
    Motor, _locomotion.CachedVelocity, grounded, deltaTime);
```

**ActorMotionRuntime.StepChannels**（ActorMotionRuntime.cs: 143-163）：参数 `deltaTime` → `motionDeltaTime`，添加 XML 注释说明语义。

**ActorMotionRuntime.ComposeKccVelocity**（ActorMotionRuntime.cs: 165-168）：添加 XML 注释说明 `deltaTime` 是 KCC 真实 tick delta。

**MotionChannels** 三个 Step 方法注释更新，明确 `dt` 是 motion delta。

---

#### 未修改的内容（确认符合计划）

- `ComposeKccVelocity` 仍使用真实 `deltaTime` 做 RootMotion 位移→速度换算 ✓
- `ComputeSolvedVelocity` 仍使用真实 `deltaTime` ✓
- `PublishSolvedVelocity` 仍使用真实 `smoothingDeltaTime` ✓
- 未修改 KCC 插件源码 ✓
- 未修改受击 Asset 的 impulse force 数值 ✓
- 未移除或绕过 `MovementTimeScale` ✓
- `ForceUnground` 在当前第一轮保持不变 ✓

---

#### 验证执行

1. **代码审查** — 确认 `UpdateVelocity` 中 `StepChannels` 收到 `motionDeltaTime`，`ComposeKccVelocity` 收到真实 `deltaTime`
2. **git diff** — `git diff --stat -- Assets/Scripts/Actor/` 确认仅 3 个文件变更（+26 / -8 行）
3. **禁止范围检查** — 确认未触碰 KCC 插件、未修改 ActionAsset 数值
4. **Unity 运行时验证** — **未验证**。无法在 Unity Editor 中运行 HitStop/Impulse 场景。静态代码分析确认时间语义修正正确

---

#### 与计划的偏差

无偏差。所有修改严格在允许范围内，遵循架构约束中的 `realDeltaTime` / `motionDeltaTime` 区分。

---

#### 剩余风险

- `ForceUnground` 在 `MovementTimeScale == 0` 时的消费语义本轮未修改，执行报告已按要求记录
- 未在 Unity 中实测 HitStop + Impulse 对照效果（Hit_Air_Launch / Hit_Air_ReLaunch）
- `Update()` 中的 `dt = Time.deltaTime * MovementTimeScale` 用于 locomotion/facing —— 它使用的是 Unity 时间而非常数时间尺度，与 KCC tick 的 `deltaTime` 来源不同。本轮不修改此路径

### 3. 审查 / Review

未审查。
