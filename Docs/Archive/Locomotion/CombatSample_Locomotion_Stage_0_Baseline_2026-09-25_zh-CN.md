# CombatSample Locomotion 阶段 0：实施基线

> 归档于 2026-10-05。正文中的状态、待办和验证结果保留为当时记录；当前实现见[现行架构](../../Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)，最新验证与未完成检查见[提交审查](../../Current/CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。源码链接用于导航，历史成员和行号不代表当前代码。

> 状态：阶段 0 的仓库静态盘点已完成，尚未实施 Locomotion v1，也未在 Unity 中运行场景或验证动画视觉效果。基线：`GamePlay`，HEAD `729a7b0dcf9dcf01a8f5ba8cf777fb6e7874814e`，2026-09-25。工作区已有 Docs 修改和未跟踪的 Locomotion 架构、路线图；本盘点没有把它们当作已实现代码。[目标架构](../../Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)与[实施路线图](CombatSample_Locomotion_Implementation_Roadmap_v1_zh-CN.md)仍位于 Proposals。

## 1. 当前输入、模拟与姿态链

| 当前事实 | 代码依据 | 阶段 1 需保持或迁移的合同 |
| --- | --- | --- |
| 玩家由固定帧入口提交 Intent，AI 的 `MoveTowardCombatTargetTask`、`SetLocomotionIntentTask` 也直接调用 `ActorMotor.SetLocomotionIntent`。玩家取消输入可清除 Motor 的 Pending Intent。 | [PlayerInputController](../../../Assets/Scripts/Input/PlayerInputController.cs)、[MoveTowardCombatTargetTask](../../../Assets/Scripts/NodeCanvas/Action/MoveTowardCombatTargetTask.cs)、[SetLocomotionIntentTask](../../../Assets/Scripts/NodeCanvas/Action/SetLocomotionIntentTask.cs) | Player/AI 同帧快照改由 ActorLocomotion 持有；清理、冻结及无输入语义不得丢失。 |
| `ActorLocomotion` 在 Control 读取 Motor Pending Intent，按候选条件、优先级与 Fallback 选 Mode；Mode 变化时把 `LocomotionTuning` 推给 Motor。当前两个测试 Mode 都是无候选条件的 Fallback。 | [ActorLocomotion](../../../Assets/Scripts/Actor/ActorLocomotion.cs)、[LocomotionModeAsset](../../../Assets/Scripts/Actor/Locomotion/Configuration/LocomotionAsset.cs)、[Kiana Mode](../../../Assets/Create/Locomotion/Kiana/Locomotion_Kiana_Normal.asset)、[Jaeger Mode](../../../Assets/Create/Locomotion/Jaeger/Locomotion_Jaeger_Normal.asset) | 保留条件和标签规则；把 Config、Intent 与 Runner 所有权迁往 Locomotion，Motor 仅消费 Request。 |
| Action 的 `StartContextMode.LocomotionIntent` 和 `LocomotionIntentCondition` 读取 Motor Pending Intent。 | [ActionStateManager](../../../Assets/Scripts/Actor/ActionStateManager.cs)、[LocomotionIntentCondition](../../../Assets/Scripts/ActionSystem/Conditions/LocomotionIntentCondition.cs) | 迁移为读取与 Mode 选择同一 Tick 的 Locomotion 快照。 |
| Motor 内的 `LocomotionRunner` 在 Motion 阶段消费 Intent，直接计算 `方向 × 强度 × MoveSpeed × Policy`；无加减速状态。朝向通过 `RotateTowards` 更新。 | [LocomotionRunner](../../../Assets/Scripts/Actor/Motion/LocomotionRunner.cs)、[ActorMotor](../../../Assets/Scripts/Actor/ActorMotor.cs) | 迁移 Runner 时先维持旧响应可回归，再按阶段 2 引入速度状态；Policy 最终由 Motor 限制贡献。 |
| `CombatSimulationDriver` 顺序为 Control → Action → Animation → Motion → World → Hit → Finish。当前 Animation 阶段仅求值 Action/Base 层，正式 Locomotion Base 提交尚不存在。 | [CombatSimulationDriver](../../../Assets/Scripts/Actor/CombatSimulationDriver.cs)、[ActorSimulationRuntime](../../../Assets/Scripts/Actor/ActorSimulationRuntime.cs)、[ActorAnimation](../../../Assets/Scripts/Actor/ActorAnimation.cs) | 不改变阶段顺序；Animation 只能读取上一 Tick 的实际世界结果，Action 当 Tick 的 owner/Policy 必须先参与 Motion。 |

## 2. 当前运动、时间与生命周期合同

- 水平仲裁在 [ActorMotor.ComposeKccVelocity](../../../Assets/Scripts/Actor/ActorMotor.cs) 中：HorizontalVelocity owner ＞ Action trajectory Root Motion ＞ Locomotion + HorizontalImpulse；Root Motion 使用当 Tick 的 local delta 转换为世界速度。垂直由 VerticalVelocity owner 优先，否则 Ballistic；旋转由 [RotationDomain](../../../Assets/Scripts/Actor/Motion/RotationDomain.cs) 按 Scripted ＞ Root ＞ Locomotion 仲裁。被盖住的轨迹区间在 `BeginMotionTick` 消费，不补帧。[ActorMotionDomains](../../../Assets/Scripts/Actor/Motion/ActorMotionDomains.cs)记录各 owner 和 Policy。
- `MotionPolicyState` 对 LocomotionScale、AirLocomotionScale 取最小值，对 GravityScale 作乘积组合。当前 Runner 先乘 Locomotion Scale，Motor 再将最终贡献乘 MovementTimeScale；阶段 1 迁移后检查不会重复缩放。[ActorMotor](../../../Assets/Scripts/Actor/ActorMotor.cs)
- Motion 前处理 Action 当 Tick 的 ForceUnground，再判断 Ground；KCC 通过 `UpdateVelocity/UpdateRotation` 读取请求。World 阶段在 KCC 和 Actor 分离之后发布 Requested 与 `ActualSolvedVelocity`；后者由已解算位置差计算，不能当作尚未解算的请求。[ActorMotor](../../../Assets/Scripts/Actor/ActorMotor.cs)、[CombatSimulationDriver](../../../Assets/Scripts/Actor/CombatSimulationDriver.cs)
- `MovementTimeScale` 来自基础值与 modifier；零比例时 Motor 运动暂停，`ActorSimulationRuntime.Control` 暂停 Mode 选择，动画有效 dt 为零。Action 播放时动画 dt 还受 Action 播放速度和暂停状态约束。[ActorSimulationRuntime](../../../Assets/Scripts/Actor/ActorSimulationRuntime.cs)、[ActorMotor](../../../Assets/Scripts/Actor/ActorMotor.cs)
- 正式 Action `MotionPolicyItem`、`VelocityOverrideItem`、`RootMotionItem` 的运行时均在 Exit/Abort 释放 token；ActionRuntime 中断与 Abort、Actor/Driver 取消帧必须继续清理 owner、动画覆盖和待处理帧。[ActionGameplayItemRuntimes](../../../Assets/Scripts/ActionSystem/Runtime/ActionGameplayItemRuntimes.cs)、[ActionRuntime](../../../Assets/Scripts/ActionSystem/Runtime/ActionRuntime.cs)、[ActorSimulationRuntime](../../../Assets/Scripts/Actor/ActorSimulationRuntime.cs)

上述合同的历史人工验收及未变更的不变量见[当前 Actor Motion 验证清单](../../Current/Actor_Motion_Validation.md)。本阶段仅核对了代码和序列化资源，没有把旧人工记录当作本次运行结果。

## 3. 最小实施与验收样本

| 用途 | 选定样本与可核实事实 | 仍须完成 |
| --- | --- | --- |
| FreeMove | 场景中的 [Player prefab](../../../Assets/Prefabs/Actor/Player.prefab) 使用 [Kiana Fallback Mode](../../../Assets/Create/Locomotion/Kiana/Locomotion_Kiana_Normal.asset)，当前 MoveSpeed 5、AirControl 0.4、RotateSpeed 600。Kiana 有 Idle、Walk、Run FBX。 | 新 FreeMove AnimationSet、对应烘焙数据、起步/停步/急转相位和运行时视觉验收；角色姿态/输入映射需在 Unity 核对。 |
| Strafe | 场景中的 [Enemy prefab](../../../Assets/Prefabs/Actor/Enemy.prefab) 使用 [Jaeger Fallback Mode](../../../Assets/Create/Locomotion/Jaeger/Locomotion_Jaeger_Normal.asset)，当前 MoveSpeed 7、AirControl 0.4、RotateSpeed 600。Jaeger 有前/后/左/右 Walk FBX，AI 的 Intent 可指定独立 FacingDirection。作为 Strafe 配置与 AI 路径样本，现有场景并未证明其已启用目标 Strafe Mode。 | 创建并配置 Strafe Mode/2D AnimationSet，核对独立朝向、动作覆盖与场景行为。 |
| Action Root Motion | [Kiana_NormalAttack_1](../../../Assets/Create/Action/Kiana_New/Combat/Kiana_NormalAttack_1.asset) 是当前 inline ActionTimeline 格式，包含 RootMotionItem，引用已序列化烘焙轨迹的 AnimationAsset。 | Unity 中验证接管、贴墙和释放，不以 YAML 存在代替数据有效性测试。 |
| Policy + Velocity + RootMotion 联合覆盖 | [Jaeger_RetreatAttack](../../../Assets/Create/Action/Jaeger/Battle/Jaeger_RetreatAttack/Jaeger_RetreatAttack.asset) 的旧 ActionSequence 数据包含三类片段，可作为旧行为与参数参考；当前 inline ActionTimeline 数据中未找到同时包含三类 Item 的正式样本。 | 在阶段 1/集成验证前，用当前 ActionTimeline 格式准备最小联合样本，覆盖 owner 优先级、0 Scale、Abort/Disable。不能把旧 Sequence 资源当成已可运行的正式验收样本。 |

## 4. 资源覆盖盘点与并行制作清单

以下为仓库文件与资源类型盘点。`FBX` 文件存在不等于导入、重定向、相位、RootMotionData 或视觉效果已验收。当前 [AnimationAsset 目录](../../../Assets/Create/Animation/)可见 Kiana 普通攻击 1–5 和 Jaeger DashAttack 的烘焙资源；未发现 Locomotion 专用 AnimationAsset/AnimationSet。因此新系统要求的 Move velocity/Stop distance 数据仍须制作与烘焙。

| 角色/Mode 样本 | 仓库中已有可评估素材 | 明确缺口与验证任务 |
| --- | --- | --- |
| Kiana / FreeMove | [Kiana Locomotion FBX](../../../Assets/Resources/Animations/Kiana/Locomotion/)包含 Idle、Walk、Run、FastRun、左右 RunStop、Jump、FreeFall 等；旧 [Kiana_NormalLoco](../../../Assets/Create/Archive/Animancer/Kiana/Locomotion/Kiana_NormalLoco.asset)是一维 Walk/Run Mixer 资源。 | 用户确认缺 Start 与 Pivot，负责补足。现有 RunStop 左右脚的导入/骨架兼容、有效轨迹、BrakeEnd/SettleEnd、速度范围与衔接效果待验证；Move 循环速度和接触标记需标定。 |
| Jaeger / Strafe | [Jaeger Locomotion FBX](../../../Assets/Resources/Animations/StrikeJaeger/Locomotion/)包含四方向 Walk、Run_Start/Loop/End 和 Idle；旧 [Jaeger_Run](../../../Assets/Create/Archive/Animancer/Jaeger/Locomotion/Jaeger_Run.asset)只引用 Run_Loop。 | 四方向素材只证明文件存在，不证明 2D 步态、Stop/Pivot、Air 与所有接入相位已覆盖；需预览确认并按统一合同补足缺项。 |

统一覆盖表在阶段 2/3 创建 AnimationSet 时落到角色/Mode 资产：Idle、步态速度范围、方向性 Start/Stop、左右 Pivot、Jump/Fall/Land、左右脚接触与循环衔接、BrakeEnd/SettleEnd、有效 RootMotionData 和正常可接受的播放倍率。样本数量可因角色而异，行为与质量门槛相同。缺失素材由用户并行解决；代码阶段不等待素材齐全，角色效果与整体交付仍以接入验收为准。

## 5. 阶段 0 出口与阶段 1 入口

- **已完成：** 锁定分支/HEAD、当前输入调用点、Action 准入读取点、模式选择、阶段顺序、Motion 仲裁、Policy/时间/生命周期规则；选定 FreeMove、Strafe 和现有 RootMotion 样本，并识别当前格式缺少 Policy + Velocity + RootMotion 联合样本；列出资源制作和验证缺口。
- **未声称完成：** Unity PlayMode、动画预览、轨迹有效性与脚步质量验证；新 Locomotion 系统和任一角色的 V1 效果验收。
- **阶段 1 可直接开始：** 迁移 Intent 单一快照和 Motion Request；保留以上回归合同。联合 Action 样本与缺失动画可并行准备，在对应阶段的集成验收前补齐。
