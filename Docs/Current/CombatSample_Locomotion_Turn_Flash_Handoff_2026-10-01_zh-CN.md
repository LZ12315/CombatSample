# Locomotion 转向闪帧问题交接（2026-10-01）

## 当前状态

继续在 `Locomotion` 分支工作。本文记录的代码基线是 `a72640008963d04fa713914503716f2952361767`，已推送至 `origin/Locomotion`。本次只添加交接文档，没有进一步修复代码。

**未关闭的问题：** Kiana Normal 保持移动输入、直接大幅反向时，用户仍看到短暂的站立／Idle 样姿态。最新 Trace 已覆盖反向过程；不能把现有参数平滑或静态编译通过当作此问题修复成功。

下一步重点是 **Start 中强反向的打断规则，以及 Start → Move 的实际姿态衔接**。尚未实施这一步，也尚未证明可见的站立姿态具体来自哪个动画帧。

先阅读仓库根目录 [AGENTS.md](../../AGENTS.md) 和以下文档，以当前源码为准：

- [架构](../Proposals/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)
- [Roadmap](../Proposals/CombatSample_Locomotion_Implementation_Roadmap_v1_zh-CN.md)
- [Motion 验证记录](Actor_Motion_Validation.md)，尤其是末尾「连续反向闪帧复查」。早期速度低谷推断已被后续现场证据限定，不能只读前一段便认定原因。

## 已确定的设计边界

移动仍走 Player／AI → ActorLocomotion → MotionRequest → ActorMotor → KCC；Tick 顺序为 Action → Motion → Animation → World。动画不得阻止 Gameplay 转向或替代移动积分。

有效 Move 必须独立支撑起步、停止和任意转向。Start／Stop／Pivot 各自可选，空列表合法；请求过渡时先准备有效 Clip，再提交状态。没有 Pivot 应正常回到 Move。非空但损坏的列表需要明确报告，不能提交空动画状态。

目前只保留基础播放、Animancer 基础 Sync 和 Move 速度匹配。脚相、Stop Distance Matching 已移除；此次排查不重新加入它们，不新增 Turn、通用能力框架或状态图编辑器。优先让核心逻辑清楚、稳定，避免用越来越多的补偿分支掩盖原因。

保留 Layer 0 会话、姿态保护、Layer 1 Action 覆盖、HitStop 和生命周期合同。Stop 完成后返回当前模型参数的 Move，没有强制零速锁存。资源修改后通过现有 Disable／Enable 重新绑定，不增加逐 Tick 热重载。

## 跨机器复现的资源前提

**当前 Kiana 复现配置没有提交。仅拉取分支不能复现相同现场。** 基线提交中的 `Locomotion_Kiana_Normal.asset` 仍是空 Move／Start／Stop／Pivot，MaxSpeed 为 5；本机工作区才配置了 Idle／Walk／Run、Start、Stop，MaxSpeed 为 7。本轮按原有资源约束不提交或重存这些资源。

另一台机器继续视觉排查前，需另行同步下列本地文件；新增 AnimationAsset 必须连同对应 `.meta` 一起带过去，以保持 GUID 与引用一致。不要在目标机器重新创建同名资源代替同步，也不要覆盖本机尚未保存的制作工作。

已跟踪但本地修改：

```text
Assets/Create/Animation/Kiana/Combat/Anim_Kiana_Attack_4A.asset
Assets/Create/Locomotion/Kiana/Locomotion_Kiana_Normal.asset
Assets/Resources/Animations/Kiana/Locomotion/Avatar_Kiana_C2_Ani_Idle_01.FBX.meta
Assets/Resources/Animations/Kiana/Locomotion/Avatar_Kiana_C2_Ani_RunBS_fix.FBX.meta
Assets/Resources/Animations/Kiana/Locomotion/Avatar_Kiana_C2_Ani_RunStopLeft_fix.FBX.meta
Assets/Resources/Animations/Kiana/Locomotion/Avatar_Kiana_C2_Ani_Walk.FBX.meta
```

尚未跟踪的新文件：

```text
Assets/Create/Animation/Kiana/Motion/Anim_Kiana_Idle.asset
Assets/Create/Animation/Kiana/Motion/Anim_Kiana_Idle.asset.meta
Assets/Create/Animation/Kiana/Motion/Anim_Kiana_Walk.asset
Assets/Create/Animation/Kiana/Motion/Anim_Kiana_Walk.asset.meta
Assets/Create/Animation/Kiana/Motion/Anim_kiana_Run.asset
Assets/Create/Animation/Kiana/Motion/Anim_kiana_Run.asset.meta
Assets/Create/Animation/Kiana/Motion/Anim_kiana_Run_Start.asset
Assets/Create/Animation/Kiana/Motion/Anim_kiana_Run_Start.asset.meta
Assets/Create/Animation/Kiana/Motion/Anim_kiana_Run_Stop.asset
Assets/Create/Animation/Kiana/Motion/Anim_kiana_Run_Stop.asset.meta
```

Attack_4A 的本地改动不作为本问题的已确认原因；列出它是为了完整记录尚未同步的工作区。

本机 Normal 配置：1D 阈值 Idle `0`、Walk `0.4`、Run `1`；Idle 不同步，Walk／Run 同步；Acceleration `20`、Deceleration `32`、RotateSpeed `600`、TurnResponseTime `0.08`，TransitionBlendDuration `0.1`；Start／Stop 方向均为局部前方，Pivot 为空。Start 引用 `Avatar_Kiana_C2_Ani_RunBS_fix.FBX`，非循环，长度约 `1.1667s`；Stop 引用 RunStopLeft_fix，长度约 `1.8667s`。

## 最新现场证据

用户最后提供的日志是 120 个有效动画 Tick。原始附件位于本机 Codex 附件目录，不随 Git 同步；下面保留后续排查所需的关键数值。数据来自 Graph Evaluate 后的诊断，**不是最终渲染 Pose 的证明**。

整个记录中 Asset 为 `Locomotion_Kiana_Normal`、会话 owner 为 `3`、Action owner 为 `0`；提交全部成功，Layer 0 权重为 `1`，Layer 1 权重为 `0`。Tick 1–54 保持输入强度 `1`，Tick 55 起释放输入。

| Tick | 状态／播放情况 | 参数与关键观察 |
| --- | --- | --- |
| 1 | Start，权重 1，时间 1.017s | 反向已经触发；积分前速度 `(-4.138, 0, -5.646)`，输入方向 `(0.573, 0, 0.820)`，dt 约 1/60s。 |
| 11 | Start，权重 1，时间 1.183s | 仍没有退出 Start；积分后速度 `(-0.670, 0, -0.914)`。 |
| 12 | Move 权重 0.167；淡出的 Start 权重 0.833，时间 1.200s | Move 参数 0.833，Idle 子样本权重 0；Move 子样本从时间 0 接入。 |
| 13 | Move 权重 0.333；Start 权重 0.667，时间 1.217s | 模型速度低谷约 0.067m/s，但 Move 参数 0.667，Idle 权重仍为 0。 |
| 14 | Move／Start 权重各 0.5 | 模型已向新方向加速；Move 参数 0.500，Idle 权重 0。 |
| 17 | Move 权重 1，Start 已淡出 | Move 参数 0.958，Idle 权重 0。 |
| 55 | 释放输入，进入 Stop 淡入 | Stop 从时间 0 播放；Move 仍有淡出权重。 |
| 120 | Stop 权重 1，时间 1.083s | 尚未达到约 1.8667s 的结束边界，本日志没有覆盖 Stop 完成。 |

Tick 12–54 的全部 43 个 Move 请求，Idle 子样本权重都为 0。这排除了**本次捕获中**「Move 在速度低谷混入 Idle」的解释；没有证明其他操作路径也不存在该问题。

Start／Stop 请求中的 `parameter=(0,0)` 是未使用的默认请求字段，不表示实际在播放 Idle。许多导入 Clip 都叫 `Take 001`，也不能凭这个名称判断是哪段动作。非循环 Start 的状态时间超过长度、淡出时继续推进，是已观察到的现象；单凭时间数值不能认定素材末尾是 Idle、动画循环错误或采样异常。

此前一份日志只记录了 60 个完全相同的静止 Tick，没有捕获操作，不能作为反向问题的证据。

## 当前实现与已有改动

- [LocomotionSetStateMachine.cs](../../Assets/Scripts/Actor/LocomotionSetStateMachine.cs)：状态机只负责决策和输入边沿。当前 Start／Pivot 在持续输入下仅在 Clip 完成时返回 Move；即使 Start 中出现强反向，也会更新反向锁存，却不打断 Start。这与最新记录相符。
- [LocomotionRuntime.cs](../../Assets/Scripts/Actor/LocomotionRuntime.cs)：选择与缓存素材，先准备 Clip 再提交状态；维护 Move 参数与速度匹配。已有水平 1D 参数按 0.1s 响应平滑，首次绑定按当前值建立基线，零 dt 冻结。它不修改 Gameplay 速度；2D 和 Air VerticalSpeed 未采用该平滑。
- [LocomotionRunner.cs](../../Assets/Scripts/Actor/Motion/LocomotionRunner.cs)：反向先减速到零，再利用本 Tick 剩余时间加速。该移动规则仍保留，不应为了掩盖动画闪帧改成绕过零速。
- [ActorAnimation.cs](../../Assets/Scripts/Actor/ActorAnimation.cs)：接入状态、参数、crossfade 和播放倍率；Runtime 不访问最终 Layer 或自行 Evaluate。
- [ActorSimulationRuntime.cs](../../Assets/Scripts/Actor/ActorSimulationRuntime.cs)：现有动画阶段统一 Evaluate 一次，随后输出诊断。
- [ActorLocomotion.cs](../../Assets/Scripts/Actor/ActorLocomotion.cs)：Editor 菜单 `Debug/Trace Next Turn or Release (120 Ticks)` 等待有速度时发生强反向或释放输入，再开始记录；暂停 Tick 不消费记录数量。

对角输入 `MoveStrength=1.00000012` 的浮点边界问题已经在输入生成处修正并提交，不是本次待处理项。此前还去掉了资源绑定时重复的汇总提示，保留 Runtime 的具体错误报告。

## 下一步工作

1. 先确认 Start 尾段、Move 接入初始帧和可见闪帧的时间对应关系；分别复现「Start 尚未完成时反向」和「已经稳定 Move 后反向」。最新日志只证明了前一种状态路径，不能外推后一种也有同样原因。
2. 调整 Start 中的强反向打断：出现反向边沿时，尝试有效 Pivot；没有可用 Pivot 则立即回到当前 Move。沿用已有速度至少 `0.5m/s`、夹角至少 `120°` 的判断，不添加另一组阈值或配置。普通持续输入下仍正常完成 Start。
3. 保持提出状态 → 准备有效 Clip → 提交状态的顺序。无 Pivot 时也消费本 Tick 边沿，避免延迟补播或同一次反向重复触发；明确 Clip 完成与反向同 Tick 的优先级。不要顺带改变 Pivot 的持续播放规则。
4. 补少量合同：Start 中反向且有／无 Pivot、边沿不重复、完成与反向同 Tick、零 dt、Action 打断。已有 `ContinuousReversal_BlendsThroughTheSpeedDipAndStopsPromptly` 用的是只有 Move 的合成配置，没有覆盖现场的 Start 路径；不要把它通过当作全部转向路径正确。
5. 必要时增加合成 Clip 的一次 Graph Evaluate 衔接检查；最终仍需看 Kiana 的实际姿态。若素材尾段与 Move 初始 Pose 不协调，记录具体时间与素材证据，再决定改动，不能仅靠状态枚举宣布修复。

不修改或重存用户现有 Asset、场景、Prefab、Importer、目录及 GUID。不要取消所有 Start、禁止 Idle、添加强制参数下限，或恢复脚相／距离匹配来掩盖当前问题。按当前仓库合同完成小范围改动；后续提交与推送遵循用户当时的授权。

## 同时出现的警告

这些需要独立处理，当前没有证据将它们与捕获中的 Kiana Normal 闪帧关联：

- Jaeger Normal 的 Move 为空，Start 有空动画／零方向条目；Jaeger Air 和 Kiana Air 的 Move 为空。前者是基础 Pose／已配置条目错误，空 Stop／Pivot 本身不是缺口。由作者配置资源，不能靠隐藏日志或攻击动画冒充基础 Pose。
- 场景存在旧 `ActionHitBoxBehavior/HitBoxUpdater` 的序列化脚本引用，运行时出现 Missing Script。当前代码没有该旧类型；没有在此次改动中清理场景，也不要为了消除警告恢复过时类或盲目批量删除组件。应在 Unity 中确认具体对象及现有 HitBox 流程，再单独处理。

## 验证记录与后续检查

此前 Runtime、Editor（含测试源码）和非 Editor／Player 静态编译通过；响应文件在原机器仓库外临时目录，未作为项目工具提交，另一台机器不应假设这些文件存在。没有运行 Unity Test Runner、命令行 Unity 构建或外部 NUnit 宿主。静态编译不证明 Graph 最终 Pose、crossfade 或角色视觉效果。

本次只核对了源码、最新日志、资源本地与已提交版本的差别，并添加此文档；检查文档差异格式及提交范围，不重新运行无关编译。

后续 Unity 人工检查：同步资源后，分别在起步中与稳定跑动中持续反向；连续改变方向；松开减速；Start／Stop 衔接；Action 覆盖及释放；HitStop；Disable／Enable。查看是否出现站立滑行、冻结姿态、旧过渡补播，并结合求值后 Trace 定位。修复后运行相关小型合同测试，再记录角色检查结果；问题目前保持未关闭。
