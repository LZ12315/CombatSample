# CombatSample Final Architecture v3 Source Audit

> 状态：v3 编写前的资料整理记录，不是 Final Architecture，也不是 Implementation Plan。
>
> 日期：2026-08-24
>
> 当前最高设计依据：[`CombatSample_E3_PreDesign_Checkpoint_zh-CN.md`](CombatSample_E3_PreDesign_Checkpoint_zh-CN.md)

## 1. 目的

Final Architecture v3 不应直接复制 v2，也不应丢掉已经验证过的 Root Motion / Driver / HitBox / ActionSequence 设计。

本文只做一件事：把现有 Docs 中与 v3 有关的来源按 **保留 / 已被取代 / 历史实现 / 待验证** 分类，避免旧文档中的过时职责重新进入 v3。

资料优先级：

```text
E3 Pre-Design Checkpoint
>
当前已验证代码事实 / Current 文档
>
Final Architecture v2 与 Root Motion v1 中仍有效的设计来源
>
Archive 历史记录
```

如果低优先级来源与高优先级来源冲突，不做折中拼接，以高优先级来源为准。

---

## 2. Final Architecture v2

来源：[`CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md`](CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md)

### 2.1 应进入 v3 的内容

以下仍是有效架构来源：

```text
ActionAsset + ActionInstance 的单一 Action 模型
ActionSequence 作为正式 fixed-frame action 内容
CombatSimulationDriver 是 Combat World scheduler
每 Actor 只通过 ActorSimulationRuntime 参加固定模拟
Sequence 只驱动 Frame 与 Clip Enter / Tick / Exit
KCC 是实际世界运动求解 authority
ActorHitBoxRuntime 持有 active hitbox runtime state
CombatHitBuffer 先收集、稳定排序、后 Resolve
Query 必须在 movement 完成和 Physics.SyncTransforms 之后
Legacy Timeline 只作为迁移兼容，不发展成第二个长期 backend
AnimationConfig / RootMotionTrajectory 数据目录与 Baker 工作流
Requested Motion 与 KCC Actual Motion 分离
```

### 2.2 已被 E3 Checkpoint 取代的内容

以下不得进入 v3：

```text
StateKind.Action / StateKind.Locomotion 作为 whole-actor 互斥域
CurrentAction == null 就表示 Locomotion 域的总体 ownership 模型
LocomotionController 在 Action 期间停止提交 locomotion / 停止操作 animation graph
ActorLogicInput 作为 locomotion runtime authority
LocomotionController 作为 Intent -> Motor 的必经路径
ActorMotionRuntime 作为长期一级 ActorMotor 架构边界
FacingRuntime 作为总旋转 authority
GravityAccumulator + VerticalImpulseVelocity 的长期垂直模型
Root Motion 与 HorizontalImpulse 相加的旧 compose
Action 通过 suppressLocomotion / 整招 MotionConfig 表达 movement ownership
Animator Root Motion 作为长期兼容 source
```

对应的新基线是：

```text
ActorLocomotion / ActorMotor / ActorAnimation 三个 Actor 级 Domain
LocomotionIntent 直接进入 ActorMotor
LocomotionRunner 解释 Intent + tuning
ActorMotor = Translation + Rotation 两大 Domain
BallisticVerticalVelocity + VerticalVelocityOwner
Scripted Rotation > Root Rotation > Locomotion Rotation
Root Motion > Locomotion + HorizontalImpulse
MotionPolicy 是 supporting state，不是 whole-action ownership
ActorAnimation = Locomotion Base + Action Override
```

### 2.3 可作为历史实现证据、但不能直接写成目标职责的部分

v2 的 Stage B～E、仓库审计、D4/D5 落地记录仍可用于解释迁移顺序和当前代码来源，但其中的类名、阶段名和过渡兼容逻辑不能自动升级为 v3 架构。

---

## 3. Root Motion Final Design v1

来源：[`../CombatSample_RootMotion_Final_Design_v1.md`](../CombatSample_RootMotion_Final_Design_v1.md)

### 3.1 应进入 v3 的内容

保留：

```text
Sequence = time authority
Animation = pose data / presentation
RootMotionTrajectory = authored motion data authority
ActorMotor / KCC = world-state authority

Manual PlayableGraph continuous Evaluate Baker
Reference Rig / Avatar family bake context
累计 Root Transform M(t)
M(0) = Identity
Delta(t0,t1) = Inverse(M(t0)) * M(t1)
正确的 SE(3) composition
Sample 的 Position Lerp / Rotation Slerp
Advance 与 Seek 分离
Cancel 不偿还剩余 trajectory
Requested Motion != Actual Motion
blocked displacement 不积欠、不补偿
Pose blending 与 Gameplay Root Motion ownership 分离
```

### 3.2 已被取代的 Runtime 设计

不得进入 v3：

```text
RootMotionPolicy 允许 Runtime Root Motion Y / Full Rotation 的旧规划
单一 Exclusive Authored Motion Owner 覆盖整个 movement 的模型
FacingRuntime 与 Root Motion owner 的同步方案
Animator Root Motion 可以长期保留并和 Trajectory source gating 共存
Root Motion position/rotation 作为一个统一 owner 的假设
```

当前设计已明确：

```text
Root Motion = baked XZ displacement
Root Rotation = baked Yaw
二者是 Translation / Rotation 中独立 channel
Animator gameplay Root Motion 路径长期删除
```

---

## 4. 已完成 Stage 文档

以下文档属于实施历史，已移动到 `Docs/Archive/`：

```text
CombatSample_ActionSequence_Stage_D5_Simplification_zh-CN.md
CombatSample_ActionSequence_Stage_E1_ASM_Fixed_Tick_zh-CN.md
CombatSample_ActionSequence_Stage_E2_Player_Input_Locomotion_zh-CN.md
```

它们仍可用于确认某一阶段为什么这样实现、当时测试过什么，但不再决定后续 E3/v3 的长期职责。

其中仍应继承的事实已经由 checkpoint 或 v3 输入覆盖，例如：

```text
D5 → Driver global barriers / Sequence simplification / post-movement hit detection
E1 → Action arbitration fixed-tick ownership
E2 → PlayerInputController / PlayerLocomotionIntentResolver / Motor intent lifecycle
```

---

## 5. Current 中已过期的 Runtime 文档

以下已移动到 Archive：

### Actor_Motion_Validation

原文把以下内容作为 current invariant：

```text
ActorMotionRuntime
GravityAccumulator
VerticalImpulse
Managed Animator Root Motion
LocomotionRuntime / FacingRuntime
```

这些都与当前 E3 target architecture 冲突，因此不能继续留在 Current。

E3 implementation 后必须重新建立验证清单，验证对象应改成：

```text
Translation arbitration
Rotation arbitration
BallisticVerticalVelocity
Grounded semantics
owner-stack covered semantics
MotionPolicy
KCC requested / actual readout
Root Motion / Root Rotation
HitStop
```

### Stage 0–7 Retrospective / Prototype Audit

二者保留历史价值，但时间基线早于 D5/E1/E2，不再承担“当前 runtime 状态”职责。

---

## 6. ActionSequence Editor 文档

以下继续留在 `Current/`：

```text
ActionSequence_Editor_Design_Spec.md
ActionSequence_Editor_V2_Architecture.md
ActionSequence_Editor_V2_Architecture_zh-CN.md
```

原因：它们主要描述已实现 Editor architecture / authoring language，与本轮 ActorMotor / ActorAnimation 重构正交。

v3 只应在必要时引用其作者语言和数据模型，不应把 Movement Domain 设计塞进 Editor 架构文档。

如果 v3 改变 PoseClip、MotionPolicyClip 等 Clip authoring contract，再对 Editor 文档做对应的小范围更新。

---

## 7. 事实型 Current 文档

以下继续保留，但其 verification date 早于本次 audit：

```text
Project_Structure.md
Scene_Ownership_Baseline_2026-08-02.md
```

在 v3 实施计划真正依赖文件布局、场景 ownership 或 BuildSettings 前，应重新验证，而不是从 2026-08-02 文档推断当前事实。

---

## 8. 历史帧表迁移草案

`帧表迁移完整落地方案_历史草案.md` 已明确 NOT APPROVED，并依赖已删除/过时 API，因此只保留在 Archive。

可以继承的只有问题意识：

```text
fixed-frame authoring
Config 与 Runtime state 分离
显式 Clip lifecycle
渐进迁移需要完整 unsupported-report
```

不得继承其具体 `FrameEvent*` 类族、旧 ActorMovement API 或当时的资产规模假设。

---

## 9. Final Architecture v3 编写输入

v3 应从以下结构开始，而不是从旧 Stage 编号开始：

```text
1. Authority / Simulation Domains
2. Input + LocomotionIntent
3. ActorLocomotion
4. ActorMotor
   - LocomotionRunner
   - Translation
   - Rotation
   - MotionPolicy
   - Grounding / Velocity Readout
5. ActorAnimation
6. Action / ActionSequence contribution contracts
7. Root Motion / Root Rotation data pipeline
8. Hit Detection / Hit Resolution
9. CombatSimulationDriver global order
10. Legacy migration boundaries
11. Validation / invariants
```

Stage 编号属于 Implementation Plan，不应继续污染 Final Architecture 的领域结构。

---

## 10. v3 之前仍需代码侧确认的事项

本次文档整理不声称以下当前代码已经实现目标架构：

```text
ActorLocomotion component 尚待实现/迁移
ActorAnimation component 尚待实现/迁移
ActorMotor 旧 Runtime 类划分仍待重构
BallisticVerticalVelocity 尚待替代旧 vertical state
Root Motion / Root Rotation owner stack 尚待落地
Scripted Rotation stack 尚待落地
MotionPolicy / MotionPolicyClip 尚待落地
Animation Evaluate phase 尚待按新 ActorAnimation authority 实现
```

这些是 v3 的 implementation gap，不是重新开放架构设计。

---

## 11. 结论

Final Architecture v3 应当是一次 **consolidation**，不是新的 brainstorming：

- 继承已经证明正确的 ActionSequence / Driver / KCC / HitBox / Baker 数学；
- 用 E3 checkpoint 替换 v2 中错误的 whole-domain ownership、旧 Motor runtime 与旧 animation/movement coupling；
- 删除已完成 Stage 计划对未来设计的权威性；
- 将实现差距放入后续 E3 Implementation Plan，而不是继续混进 Final Architecture。
