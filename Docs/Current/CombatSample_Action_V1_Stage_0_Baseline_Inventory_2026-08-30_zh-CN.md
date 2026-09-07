# CombatSample Action V1 Stage 0 — Baseline & Inventory

> 状态：**Stage 0 Exit Criteria 与 Unity 基线复验均已通过**
> 分支 / HEAD：`FrameWork` / `cdec51509ec8a8a237277a8ccbd3e45c5131c3ec`  
> 记录日期：2026-08-30  
> 实施依据：[Action Implementation Roadmap v1](../Proposals/CombatSample_Action_Implementation_Roadmap_v1_zh-CN.md) Stage 0  
> 架构目标：[Action Final Architecture v1](../Proposals/CombatSample_Action_Final_Architecture_v1_zh-CN.md)

## 1. 结论与范围

本记录固定 Action V1 迁移开始前的仓库事实；不改变 `ActionAsset`、不新建 Runtime、不会迁移资产，也不删除旧代码。

- 当前代码仍以 `ActionSequence` 为 enabled-build Action 的固定帧 playback backend；E3-H handoff 记录的 enabled-build closure 为 39 个 reachable Sequence `ActionAsset`，Legacy count 为 0。
- 全仓库 `Assets/Create/ActionAssets` 中共有 75 个 `ActionAsset`：41 个显式 Sequence backend，34 个 LegacyTimeline backend（默认值或 `0`）。全仓库数量不等同于 enabled-build closure，不能据此扩大 active migration scope。
- 当前目标不是继续扩展 Sequence，而是将它、Timeline 与两套 playback session 都迁出正式 Action path。Stage 1 之前不得增加新的旧 Action authoring/playback API 依赖。

## 2. Current Baseline

| 项目 | 当前事实 | 证据 |
| --- | --- | --- |
| Git 基线 | `FrameWork`，`cdec5150 Add Action v1 frozen architecture and implementation roadmap` | `git rev-parse HEAD` / `git log` |
| Unity | 2022.3.62f3 | `ProjectSettings/ProjectVersion.txt` |
| Combat timestep | `Time.fixedDeltaTime = 0.016666668`，即 60Hz | `ProjectSettings/TimeManager.asset` |
| Build 场景 | 唯一启用：`Assets/Scenes/MiHoYo_Release.unity` | `ProjectSettings/EditorBuildSettings.asset` |
| World phase | Input/Control → Action → Animation → Motion → World → Hit → Finish | `CombatSimulationDriver` |
| per-Actor 路由 | Driver 只通过 `ActorSimulationRuntime` 进入 Actor 内部 subsystem | `ActorSimulationRuntime` |
| Action arbitration | `ActionStateManager`，固定 Tick 的 candidate / cancel arbitration | `ActionStateManager` |
| Action playback | `ActionPlayer`，当前根据 `ActionPlaybackBackend` 创建 Timeline 或 Sequence session | `ActionPlayer` |
| Animation authority | `ActorAnimation` 独占 Animancer manual evaluate、Locomotion Base 与 Action Override | `ActorAnimation` |
| Movement authority | `ActorMotor` / KCC；Translation、Rotation、MotionPolicy 仍是 Domain owner | `ActorMotor`、`Actor/Motion/*` |
| Hit authority | World 后由 `ActorHitBoxRuntime` query，`CombatHitBuffer` stable resolve | `CombatSimulationDriver`、`ActorSimulationRuntime` |

### 2.1 当前 E3 验证入口

- Release / enabled-build 回归：`Assets/Scenes/MiHoYo_Release.unity`。
- 定向 Action / combat：`Assets/Scenes/Test/Combat_Test.unity`。
- 定向 Motor / KCC：`Assets/Scenes/Test/KCC_Migration_Test.unity`。
- 当前已验收的 E3 行为与人工检查矩阵见 [E3 v3 Validation Handoff](CombatSample_E3_v3_Validation_Handoff_2026-08-29_zh-CN.md) 和 [Actor Motion v3 验证清单](Actor_Motion_Validation.md)。

### 2.2 本次验证

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：成功，0 warning，0 error。
- 已静态复核 fixed timestep、Build Settings、Driver phase 顺序和 E3 Domain owner 代码路径。
- 本环境的静态与项目编译验证见上。随后已在具备 Unity 的工作站重跑第 2.1 节所列 E3 人工检查；四组行为均符合预期，Console 无新的 compile error、missing script、runtime exception。该人工确认完成后允许进入 Stage 1。

## 3. Legacy Dependency Inventory

下表是 Stage 7 Residual Audit 的初始固定集合。`最终处置`是当前冻结架构已经决定的方向，不表示本 Stage 可以删除任何内容。

| 类别 | 当前符号 / 路径 | 当前职责或引用面 | 最终处置 |
| --- | --- | --- | --- |
| Sequence data | `ActionSequenceData`、`ActionSequenceAsset`、`ActionSequenceTrackDefinition`、`ActionSequenceClipDefinition`、`ActionSequenceTrackKind`、`ActionSequenceDurationMode` | `ActionAsset`、runtime、V2 editor、migrator、tests | Stage 7 退出 Action path；由新 inline Timeline 替代 |
| Typed tracks / clips | `ActionSequenceState/Animation/Motion/HitBox/CleanupTrack` 与 10 个 `ActionSequence*ClipDefinition` | 旧 authoring、runtime scheduling、V2 editor、assets、tests | Stage 7 删除；不得映射为新 Timeline 的 runtime type / priority 语义 |
| Sequence runtime | `ActionSequenceRuntime`、`ActionSequenceClipRuntime`、`ActionSequenceContext`、`ActionSequenceAnimationTimeUtility`、`ActionSequenceRuntimeDiagnostics`、`ActionSequenceFrameTransactionState`、`ActionSequenceRunner` | `SequenceActionPlaybackSession`、clip runtime、tests；Runner 已标记 deprecated | Stage 2–4 完成替代后，Stage 7 删除 |
| Playback abstraction | `IActionPlaybackSession`、`IFixedActionPlaybackSession`、`SequenceActionPlaybackSession`、`TimelineActionPlaybackSession`、`ActionPlaybackStopMode` | `ActionPlayer` 的 backend 分派与生命周期 | Stage 7 删除；`ActionPlayer → ActionRuntime` 成为唯一链 |
| Playback selector | `ActionPlaybackBackend`、`ActionAsset._playbackBackend`、`ActionPlayer.CreateSession` | 选择 LegacyTimeline 或 Sequence | Stage 7 删除；禁止长期 `if new / else legacy` |
| Legacy Timeline | `ActionAsset._timelineAsset`、`TimelineActionPlaybackSession`、`Assets/Scripts/TimelinePlayable/`、Timeline picker/helper/preview | 75/75 ActionAsset 均仍持有 Timeline reference | Stage 6 作为 migration source；Stage 7 删除 Action path 依赖与旧 editor |
| Whole-action motion | `ActionMotionConfig`、`ActionFacingOnStart`、`ActionAsset._motionConfig` | 71/75 asset 显式持有；E3 migrator 读取并转换为 Sequence clips | Stage 6 迁成 Item；Stage 7 删除 |
| Old Action rules | `isLoop`、`_allowReenterWhilePlaying`、`_exitConditions`、`ActionData` / `ActionDataCondition` | 14 个 Loop、7 个 Reenter、1 个有 ExitConditions；`ActionInstance` 当前承载 RuntimeData / SelfTags | V1 删除 generic loop / ReenterRule / ExitConditions；`ActionInstance`、`ActionData` 为 Stage 2/7 residual-review candidate，不能提前删除 |
| Action animation path | Action-side `animationKey`、`ActionSequenceAnimationPose/RootMotion/RootRotation/SelfRotationClipDefinition`、`ActionPlayer` validation | 41 个 Sequence asset 使用 AnimationConfig lookup；pose 和 root data 按 key 查询 | Stage 6 转为 `AnimationAsset` references；Stage 7 删除 Action-side key/config path |
| AnimationConfig | `Actor.AnimationConfig`、`ActorAnimation`、Action clip/runtime、RootMotion bake/editor tooling | 同一 Actor config 目前同时服务 Action 与 Locomotion | Action-side 用法迁走；Locomotion usage 是本轮合法保留项 |
| Legacy Action editor | `ActionSequence/Editor/`（Prototype、V2、inspectors、commands、views、manipulators） | 当前 Sequence 正常 authoring 入口；typed Track/Clip 语义 | Stage 5 新 editor 成熟后，Stage 7 删除 / archive |
| E3 migration tooling | `BuildGameplayAuthorityCutoverMigrator` | E3-G historical conversion、active closure preview、AnimationConfig support asset bake | 不进入新 Runtime；Stage 7 仅可保留在明确 Legacy/Migration 历史目录 |
| Existing tests | `Assets/Tests/Editor/ActionSequence*`、`ActionStateManagerContextTests` 等 | 保护现有 E3/Sequence 契约 | Stage 2 起替换为新稳定 contract tests；不为保留旧测试维持旧 production API |

### 3.1 Action 与 Locomotion 的 `AnimationConfig` 边界

必须区分，不能将 `AnimationConfig` 全局视为可删除项：

- **Action-side（Action V1 迁移目标）**：`ActionPlayer` 的 Sequence validation，以及 AnimationPose / RootMotion / RootRotation / SelfRotation clips 通过 `animationKey` 在 `Actor.AnimationConfig` 查找 `TransitionAsset` 或 `RootMotionTrajectory`。
- **Locomotion-side（本轮合法保留）**：`ActorLocomotion` 生成 `LocomotionAnimationPose`，`ActorAnimation.SetLocomotionBase` 继续从 `Actor.AnimationConfig` 解析 Base pose。Action V1 不重做 locomotion Mixer / Blend Tree / Profile。
- **Bake / data contract（迁移输入，不是 Runtime bake）**：`RootMotionTrajectory` 与 `RootMotionBaker` / `RootMotionBakeWorkflow` 已提供 pre-baked root transform data。新 `AnimationAsset` 必须保留该数据合同和 editor bake 原则，不能让 Action Begin 现场 bake。

## 4. ActionAsset Migration Inventory

### 4.1 总览

| 指标 | 结果 |
| --- | ---: |
| ActionAsset 总数 | 75 |
| 明确 Sequence backend | 41 |
| LegacyTimeline backend（默认 / 0） | 34 |
| 持有 Timeline source reference | 75 |
| 显式序列化 `ActionMotionConfig` | 71 |
| Sequence fixed Duration | 40 |
| Sequence auto Duration | 1 |
| `isLoop = true` | 14 |
| `_allowReenterWhilePlaying = true` | 7 |
| 非空 ExitConditions | 1 |

4 个 `Sword_HeavyAttack_*` asset 未显式写出 `_motionConfig`，应在迁移时按 Unity 序列化默认值处理，而不是静默判作缺失或丢弃。

### 4.2 按内容组的 backend 清单

| 内容组 | 总数 | Sequence | LegacyTimeline |
| --- | ---: | ---: | ---: |
| `Boxing` | 13 | 1 | 12 |
| `E3G_BuildCutover` | 7 | 7 | 0 |
| `Hit_General` | 13 | 5 | 8 |
| `Jaeger` | 10 | 7 | 3 |
| `Kiana` | 22 | 21 | 1 |
| `Sword` | 10 | 0 | 10 |

#### Sequence asset 候选（41）

- `Boxing`：`Locomotion/Jump_Land/Jump_Land.asset`。
- `E3G_BuildCutover`：`Hit_Air_Hit`、`Hit_Air_Launch_Hard`、`Hit_Air_Loop`、`Hit_Air_ReLaunch`、`Hit_Air_Straining`、`Jump_Loop`、`Jump_Start`（均为 `_Sequence.asset`）。
- `Hit_General`：`Hit_Air_Grounded`、`Hit_Air_Launch_Light`、`Hit_Air_Straining_Loop`、`Hit_Bounded_Up`、`Hit_GetUp`。
- `Jaeger`：`Battle/Jaeger_Attack_1`、`Jaeger_Attack_2`、`Jaeger_DashAttack`、`Jaeger_RetreatAttack`；`Hit/Jaeger_Hit_Hard`、`Jaeger_Hit_Light`、`Jaeger_Hit_Medium`。
- `Kiana`：`Battle/Air/Kiana_AirAttack_1`、`_2`、`_3A`、`_3B`、`Kiana_RiderKick_Start`、`_Loop`、`_End`；`Battle/Kiana_Attack_1`、`_2`、`_3A`、`_3B`、`_4A`、`_4B`、`_5`、`Kiana_Shoryuken`；`Hit/Kiana_Hit_Hard`、`Kiana_Hit_Light`；`Locomotion/Kiana_Idle`、`Kiana_NormalLoco`、`Kiana_Slide`、`Kiana_Slide_Back`。

这些资产全都仍持有 Timeline reference、SequenceData、旧 Action-level fields 和 typed clips；它们不是 Action V1 已迁移资产。E3-H 所谓的 39 个 active Sequence 是此集合的 enabled-build dependency closure，具体 membership 必须由 Unity closure scan 在开始迁移前重新确认。

#### LegacyTimeline asset 候选（34）

- `Boxing`：`Combat/Boxing_AirAttack_1`、`_2`、`_3a`、`_3b`、`Boxing_LightAttack_1`、`_2`、`_3`；`Locomotion/Boxing_Idle`、`Boxing_NormalLoco`、`Boxing_NormalLoco_Dir8`、`Jump_Loop`、`Jump_Start`。
- `Hit_General`：`General_Hit_Hard`、`General_Hit_Light`、`General_Hit_Medium`、`Hit_Air_Hit`、`Hit_Air_Launch_Hard`、`Hit_Air_Loop`、`Hit_Air_ReLaunch`、`Hit_Air_Straining`。
- `Jaeger`：`Locomotion/Jaeger_Idle`、`Jaeger_Rest`、`Jaeger_Run`。
- `Kiana`：`Locomotion/Kiana_Dodge`。
- `Sword`：`Combat/Sword_HeavyAttack_1`、`_2`、`_3`、`_4`、`Sword_LightAttack_1`、`_2`、`_3`、`_4`；`Locomotion/Sword_Idle`、`Sword_Loco8_Normal`。

### 4.3 已使用的 Sequence clip 类型（41 个 Sequence asset 中的出现次数）

| 旧 clip 类型 | asset 出现数 | V1 迁移方向 |
| --- | ---: | --- |
| `ActionSequenceAnimationPoseClipDefinition` | 41 | `AnimationSegment` |
| `ActionSequenceFacingSnapClipDefinition` | 29 | Frame 0 `SelfRotationItem` / Snap |
| `ActionSequenceHitBoxClipDefinition` | 28 | `HitBoxItem` |
| `ActionSequenceImpulseClipDefinition` | 7 | `ImpulseItem` |
| `ActionSequenceMotionPolicyClipDefinition` | 40 | `MotionPolicyItem` |
| `ActionSequenceRootMotionClipDefinition` | 21 | `RootMotionItem` |
| `ActionSequenceRootRotationClipDefinition` | 21 | `SelfRotationItem`，RootMotion source |
| `ActionSequenceSelfRotationClipDefinition` | 13 | `SelfRotationItem`，Target / Direction / RootMotion source |
| `ActionSequenceTagClipDefinition` | 5 | `TagItem` |
| `ActionSequenceVelocityOverrideClipDefinition` | 11 | `VelocityOverrideItem` |

此表仅描述 migration source；新 V1 Timeline 不能保留 TrackKind、TrackIndex、ClipIndex 或 track execution-order 的 gameplay 语义。

## 5. Stage 7 Residual-Audit Seed

Stage 7 必须以本清单加上各 Stage 新发现项执行 residual audit。允许的残留只可归入：删除、明确 Legacy migration history、Locomotion 合法依赖、Archive 文档历史记录。以下不允许留在 Current runtime：

- `ActionPlaybackBackend` / Session 双轨；
- `ActionSequenceData`、typed Track/Clip、`ActionSequenceRuntime`；
- Timeline Action playback 与 `Assets/Scripts/TimelinePlayable/` 的 Action path；
- `ActionMotionConfig`、Action-side `animationKey` / `AnimationConfig` / `TransitionAsset` dependency；
- 用来保住旧 runtime 行为的 `Loop`、`Reenter`、`ExitConditions` 或 fallback 字段。

## 6. Stage 0 Exit Criteria

| Exit criterion | 状态 | 证据 / 后续 |
| --- | --- | --- |
| 当前项目编译通过 | Passed | 本次 `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q` 为 0 warning、0 error |
| E3 基线可重复 | Passed (manual Unity verification) | 已重跑 `MiHoYo_Release` 与当前 Motion validation checklist；四组 E3 行为通过，Console 无新错误 |
| 所有目标 ActionAsset 进入迁移清单 | Passed (static inventory) | 第 4 节覆盖 `Assets/Create/ActionAssets` 的 75 个 asset；enabled-build closure membership 待 Unity 复核 |
| Sequence / Legacy 使用情况明确 | Passed | 41 Sequence / 34 LegacyTimeline；第 4 节 |
| Action-side AnimationConfig 使用点明确 | Passed | 第 3.1 节 |
| 所有计划删除类型有依赖清单 | Passed (static inventory) | 第 3 节与第 5 节 |

Stage 0 的 Exit Criteria 已全部通过。后续 Stage 仅能在保持本记录所固定的 E3 baseline 前提下推进。
