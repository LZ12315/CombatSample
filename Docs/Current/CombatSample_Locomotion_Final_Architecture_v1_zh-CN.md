# CombatSample Locomotion 当前架构

> 更新：2026-10-06。本文描述当前源码的职责与数据模型，包括 Stop 距离匹配、脚相择优、配置绑定和结构整理。配置入口见[制作工作流](CombatSample_Locomotion_Authoring_zh-CN.md)，本批验证与剩余检查见[提交审查](CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。[历史路线图](../Archive/Locomotion/CombatSample_Locomotion_Implementation_Roadmap_v1_zh-CN.md)保留迁移过程，不作为当前待办。

## 1. 系统目标与唯一链路

`ActorLocomotion` 是普通移动的唯一 Gameplay 入口。Player 和 AI 只提交 `LocomotionIntent`；Locomotion 锁定当 Tick Intent、选择 `LocomotionAsset`、运行对应 Runtime，并分别向 Motor 和 Animation 提交结果。`ActorMotor` 只负责运动来源仲裁、Policy、KCC 与世界结果；`ActorAnimation` 只负责最终动画图和 Layer。

```text
Player / AI
    ↓ LocomotionIntent
ActorLocomotion
    ├─ Asset selection → LocomotionRuntime
    ├─ LocomotionMotionRequest ─────────→ ActorMotor → KCC
    └─ LocomotionAnimationRequest ──────→ ActorAnimation Layer 0
                                             ↑ Action Layer 1

KCC solved result → Motor publishes world facts
                  → next Tick Locomotion read-only feedback
```

普通移动的 Gameplay 位移只走：

```text
Player / AI → ActorLocomotion → LocomotionMotionRequest → ActorMotor → KCC
```

Locomotion 动画的 Root Motion 轨迹用于 Move 速度匹配和 Stop 距离匹配，不直接推动 Capsule。Action 的 `RootMotionItem` 仍是独立 Motion Source，并按现有优先级真正驱动 Motor。

## 2. 固定 Tick 的执行顺序

执行顺序是：

```text
Control 锁定 Intent
    → Action 决策与推进
    → Motor.BeginMotion / ForceUnground / Ground 上下文
    → 选择 LocomotionAsset 并运行 Motion Runtime
    → Motor 合成 Motion
    → Locomotion 与 Action 更新 Animation
    → KCC / World
    → 发布结果 / Hit / Finish
```

| 步骤 | 合同 |
| --- | --- |
| Control | 将 Pending Intent 锁定为本 Tick 唯一快照。Control 之后的提交留到下一 Tick；有效 dt 为 0 时不消费 Pending Intent。 |
| Action | Action 准入读取同一份 Intent。它看到的是 Tick 开始时已经生效的 Locomotion SelfTags；本 Tick 后续 Asset 切换产生的标签从下一次 Action 决策开始可见。 |
| Begin Motion | Motor 先处理 Action 当 Tick 的 owner、Policy 和 `ForceUnground`，再产生只读 `LocomotionMotionContext`。 |
| Asset Selection | Locomotion 使用 Actor、锁定 Intent 和 Begin Motion 后的 Ground/Policy 事实选择 Asset。因此起跳当 Tick 可以直接选中 Air 候选。 |
| Motion | 当前 Runtime 计算未缩放的 Request；Motor 再应用 Policy、时间比例与运动来源优先级。 |
| Animation | Runtime 使用当前 Asset、当前 Intent、Action 后的只读 owner/Policy，以及上一 Tick 已发布的世界结果驱动 Layer 0。此时仍不能声称知道本 Tick 的 KCC 结果。 |
| World / Finish | KCC 和 Actor 分离完成后，Motor 发布 Requested、Actual、来源和同一求解区间的其他事实，供下一 Tick 读取。 |

一次 Tick 只做一次 Asset 选择，不在 Action 前后各选一次。这样不会在同 Tick 触发两次 `Exit/Enter`。`ActorSimulationRuntime` 只负责编排，不保存具体 Locomotion 状态，也不实现某种 Asset 的规则。

冻结时有效 dt 为 0：不消费 Intent、不推进移动积分、不推进 Locomotion 状态机、不补帧。Motor 只在最终世界速度上应用一次 `MovementTimeScale`。

## 3. Intent、Motion Request 与世界反馈

### 3.1 LocomotionIntent

`LocomotionIntent` 明确表达控制者的两个独立意图：

```text
WorldMoveDirection + MoveStrength = 想向哪里移动、移动多强
FacingDirection                   = 想面向哪里
```

`FacingDirection == Vector3.zero` 表示本 Tick 不请求新的面向，保持当前朝向。Locomotion 不再根据 FreeMove、Strafe 或 Move Mixer 类型替控制者猜测 Facing：

- 普通 Player 移动显式提交 `FacingDirection = WorldMoveDirection`；
- Lock-on Player 提交移动方向和目标朝向；
- Lock-on Idle 可以提交仅 Facing 的 Intent；
- AI 显式决定面向移动方向、目标方向，或提交零向量保持朝向。

因此资产中不需要 `FacingMode` 或 `LocomotionStyle`。1D/2D 只影响 Move 动画的混合输入，不决定 Gameplay 朝向。

### 输入与资源的合法使用边界

- 同一 Actor 同时只有一个移动控制者；同一 AI 图同时只有一个活动移动任务。输入不是多 owner 仲裁系统，多个控制者并行争用属于错误使用。
- 单一输入缓冲保存最新提交。公开 `SetLocomotionIntent` 是一次性输入；持续 AI 输入保持到更新、释放或生命周期清理。新提交直接替换旧提交，不叠加优先级、不暂存旧控制者、不在单次输入结束后恢复旧输入。
- Control 锁定本 Tick 快照，之后的提交或释放影响下一 Tick；不能重开已积分的 Tick。零有效 dt 保留未消费的单次输入；新的提交或明确释放优先，不因冻结而恢复已释放输入。
- `ClearLocomotionIntent` 只释放输入，现有模型速度由正常 Motion 减速；存在有效 Stop 时可以播放停止过渡，否则 Move 按所选参数源表现：速度模式随模型减速，输入模式立即归零。Disable 或 Driver abort 才清理 Tick、Runtime 和共享模型速度。
- MovementConfig 保留数值安全边界：有限、非负，允许加速度、减速度和转速为零。非法输入方向、强度或配置不进入运动计算，也不替换为另一套默认移动规则。
- Locomotion 资源在 Runtime 生命周期内视为固定配置。运行期间不支持修改 Clip、样本或 Rig 并即时热刷新。修改后重新开始运行，或禁用再启用 ActorLocomotion，以 Dispose 旧 Runtime 后重新绑定。正常 Asset 切换、Action 覆盖、HitStop、Disable/Enable 和 Graph 重建继续受生命周期合同保护。
- LocomotionAsset 的 Inspector 仅编辑数据，不进行素材覆盖或表现质量检查。Runtime 绑定保留空引用、有限数值和插值安全保护；轨迹读取仍复用 AnimationAsset 的 Bake 来源校验。
- 有效 Clip 可以基础播放。缺少有效 Move 轨迹时使用中性倍率，不输出配置告警；单样本、没有 Idle 或没有移动样本都不因此拒绝播放。不能播放的条目被跳过，其他可用条目继续绑定；重复阈值保留作者顺序中的第一条可用条目，避免插值除零。没有可用基础 Pose 时保留现有姿势保护。

### 3.2 LocomotionMotionRequest

Request 只包含：

- 世界空间水平速度及有效位；
- 目标世界旋转及有效位。

Request 不携带 MovementConfig、Asset 类型、动画、Policy、预测距离或时间比例。Motor 不反向读取 Locomotion 配置。

### 3.3 LocomotionMotionContext 与发布结果

只读 Context 至少提供：有效 dt、Ground、CharacterUp、上一 Tick 已发布结果，以及当前 Policy/Translation owner/Rotation owner 事实。Motor 在完成世界求解后发布：

- `RequestedVelocity` 与 `ActualSolvedVelocity`；
- 水平和旋转来源；
- Ground、Policy、MovementTimeScale；
- 冲量、平台携带、Actor 分离等外部运动事实；
- 来源无法确认时的 Unknown 标记。

本 Tick owner 已释放也不能改写上一 Tick 的来源。Motor 只记录运动事实；某个结果是否可用于动画匹配，由 Locomotion 判断。

## 4. LocomotionAsset 数据模型

### 4.1 抽象基类

`LocomotionAsset` 是作者配置和 Runtime 工厂，与 `ActionAsset` 对应。执行配置在 Runtime 构造时固定。共同数据直接内嵌，不创建独立的 MovementConfig 或 AnimationSet 资源。

```text
abstract LocomotionAsset
├─ Priority
├─ EntryConditions
├─ SelfTags
├─ MovementConfig
└─ MoveDefinition
```

一个 Actor 持有一个候选列表。选择规则为：

1. 只考虑 EntryConditions 全部通过的候选；
2. 选择最高 Priority；
3. 同优先级时，当前 Asset 仍合法则保持；
4. 否则选择 authored list 中最先出现的候选。

EntryConditions 为空表示这个显式候选无条件成立，它仍参与同一套 Priority 和 authored order 选择。没有候选时退出旧 Runtime、释放旧 SelfTags，停止提交 Locomotion Motion/Animation，不自动选其他默认资产，也不输出配置告警。

### 4.2 两种具体 Asset

```text
LocomotionMixerAsset
└─ 只有持续 Move

LocomotionSetAsset
├─ 持续 Move
├─ Start[]
├─ Stop[]
├─ Pivot[]
├─ TransitionBlendDuration
├─ StopPlaybackMode
└─ TransitionDecisionConfig
```

二者是同一候选列表中的平行移动形态：

- `LocomotionMixerAsset` 适合只需要持续混合的形态，例如使用 VerticalSpeed 1D 的 Air locomotion；
- `LocomotionSetAsset` 在持续 Move 上提供可选的 Start/Stop/Pivot；只有 Move 的 Set 同样是完整的基础播放配置。

这不是 Ground/Air 类型划分。Ground 与 Air 由 Conditions 选择；Jump 和 Land 继续由 Action 实现。

### 4.3 MoveDefinition

`MoveDefinition` 位于基类，因为两种具体 Asset 都有 Move；它内部选择 1D 或 2D，但这个选择只描述 Move Mixer。

```text
MoveDefinition
├─ BlendType: OneDimensional | TwoDimensional
├─ 1D Parameter: HorizontalSpeed | VerticalSpeed | InputStrength
│  └─ Samples[]: AnimationAsset + float Threshold + bool Sync=true
└─ 2D Parameter: LocalVelocity | LocalInput
   └─ Samples[]: AnimationAsset + Vector2 Threshold + bool Sync=true
```

约束：

- HorizontalSpeed 使用政策缩放后的水平模型速度；VerticalSpeed 使用 Motor 已准备的请求垂直速度并去除时间缩放，允许有符号值；这两个模式保留原有序列化值；
- InputStrength 直接使用 0～1 的 `LocomotionIntent.MoveStrength`；LocalInput 使用局部空间的输入方向 × 强度。输入值范围不限制作者填写样本阈值的范围。没有输入时参数为零，不改读残余速度，也不套用 Start/Stop/Pivot 的输入判定阈值；
- 2D 的 `LocalVelocity` 与 `LocalInput` 都使用角色当前 Facing 建立的局部空间；
- 绑定跳过非有限阈值和重复阈值；重复值保留作者顺序中的第一条可用样本；
- 每个样本的 `Sync` 默认开启，可单独退出同步；
- Idle 不设独立字段。零阈值／零方向样本参与 Move 的正常混合；交接保护不根据这些样本另选 Idle；
- 各参数模式允许任意可安全插值的有限样本阈值，不强制 Idle 与移动样本配对；单样本可播放。VerticalSpeed 不使用 Move 播放倍率匹配；
- Loop 来自源 `AnimationClip`，不在 LocomotionAsset 重复配置；
- Runtime 创建并缓存 Animancer Mixer；不同步的样本调用 `DontSynchronize`。

1D/2D 是连续 Move 混合空间，不限制 Start/Stop/Pivot 的方向覆盖。典型 FreeMove 可以使用 1D Speed Move，同时拥有左前 Start 和 180° Pivot。输入模式的 Threshold 使用归一化输入单位；速度模式的 Threshold 保持速度单位。Move 的播放倍率匹配仍独立使用实际速度。

### 4.4 LocomotionSetAsset 的过渡条目

```text
StartEntry
├─ AnimationAsset
└─ TargetLocalDirection

StopEntry
├─ AnimationAsset
└─ SourceLocalDirection

PivotEntry
├─ AnimationAsset
├─ SourceLocalDirection
└─ TargetLocalDirection
```

方向是在进入状态时，相对于角色 Facing 的二维局部运动方向：`x` 为右，`y` 为前。Start 使用目标 Intent；Stop 使用积分前的模型速度；Pivot 使用旧运动方向和新目标方向。Pivot 的有符号角度由两向量计算，不再单独重复配置 `TurnAngle`。

Start/Stop/Pivot 可独立留空。绑定时跳过缺 Clip、无有效时长或含非有限方向值的条目，其余条目继续按统一角度规则匹配；零方向没有额外的覆盖检查。基础过渡不要求 Root Motion 轨迹。

`TransitionBlendDuration` 是 Set 级的统一基础淡入淡出时间。第一版不为每条动画暴露 Fade、SpeedRange、Foot、ExitTime、PlaybackSpeed、Priority 或任意 Condition；实际制作证明需要后再扩展。

## 5. Asset 与 Runtime 的职责

每个 Asset 类型都有对应的运行时实现：

```text
LocomotionMixerAsset → LocomotionMixerRuntime
LocomotionSetAsset   → LocomotionSetRuntime
```

Asset 负责序列化配置并创建 Runtime；Runtime 保存每 Actor 的可变状态。概念接口包括：

```text
CreateRuntime
Enter / Exit
UpdateMotion
UpdateAnimation
Dispose
```

`ActorLocomotion` 为每个 Asset、每个 Actor 缓存一个 Runtime，负责选择、生命周期、Intent、SelfTags 和接线，不用 `is`/`switch` 判断具体 Asset 类型。Disable/Destroy 时退出并释放所有 Runtime。

Runtime 创建时复制运动参数、Move 定义、过渡条目、混合时长、Stop 模式和状态决策阈值；动画同时绑定 Clip、脚相轨道、停止距离曲线和 Move 周期速度数据。延迟创建 Mixer、退出再进入同一 Runtime 或重建图均只使用绑定数据。`Runtime.Asset` 保留资产身份，`CurrentMovementConfig` 展示当前 Runtime 实际使用的配置。修改作者资产只有新建 Runtime 后生效，没有运行时刷新入口。模式选择仍按角色当前状态求值。

速度和旋转积分器属于 ActorLocomotion 的共享运动状态，不属于某个具体 Asset Runtime。切换 Asset 时继承当前速度，避免每个 Runtime 保存一份过期速度或切换时归零。MovementConfig 的 MaxSpeed 是当前控制目标上限，不强制截断从上一状态继承的动量。

`LocomotionMixerRuntime` 始终更新 Move。`LocomotionSetRuntime` 内部拥有固定状态机：

```text
Move ↔ Start
Move ↔ Stop
Move ↔ Pivot
```

- 静止时出现有效移动意图，存在可播放的 Start 才进入 Start，否则继续 Move；
- 失去移动意图且仍有速度，存在可播放的 Stop 才进入 Stop，否则 Move 随模型减速；
- 有速度时目标方向发生足够强的反转，存在可播放的 Pivot 才进入 Pivot，否则继续 Move 并正常 Gameplay 转向；
- 新输入可打断 Stop；Action/Asset 切换可以中断所有过渡；
- Runtime 先取得下一状态建议，选择并准备有效 Clip 后才提交状态。未进入的过渡不排队，输入边沿仍在有效 Tick 消费；零 dt 不推进决策；
- 所有过渡完成后返回使用当前参数源的 Move，不锁存零速；Start/Pivot 释放输入但没有 Stop 时同样返回 Move；
- 状态拓扑、优先级和打断规则由代码统一维护；Set 的 `TransitionDecisionConfig` 配置静止速度（默认 0.1 m/s）、Pivot 最低速度（默认 0.5 m/s）和最小转角（默认 120°），不做成任意 Condition 图。Start 使用速度 ≤ 静止阈值，Stop 使用速度 > 静止阈值，Pivot 使用速度 ≥ 最低速度且转角 ≥ 最小转角；
- 基础 Move 没有任何可用样本时，不启动可选过渡；沿现有姿势保护路径处理，不输出配置告警，不改变 Gameplay 积分。

`ActorAnimation` 继续拥有 Animancer Graph。Runtime 只通过窄的 Layer 0 请求接口提交 Mixer、Clip、参数、时间和淡入淡出，不直接拥有最终动画图。

交接保护统一冻结基础层实际已求值的姿势：复制当前有贡献的动画叶节点、实际求值时间及混合权重，保留尚未结束的 crossfade。快照独立于 Runtime 节点，不含其事件、同步或淡入淡出；后续 Runtime 销毁不改变保护姿势。已经处于保护状态时复用快照。Action 覆盖仍独立；新基础动画提交后正常混合接入。

### 5.1 配置绑定与播放准备

Runtime 构造期间使用 `LocomotionBindingBuilder` 按 AnimationAsset 去重，取得一次轨迹资格结果，再生成脚相、Stop 曲线与周期位移。`LocomotionBoundMove`、`LocomotionBoundSet` 私有保存过滤后的样本与条目，复制参数源、方向、Sync、混合时长及决策配置；构建器和作者列表不进入执行阶段。运动配置继续由 Runtime 基类捕获，资产引用仅保留身份。

Runtime 持有绑定定义、决策状态和播放节点缓存。图失效后丢弃节点，下一次使用同一绑定定义重建；Exit 后重入仍使用原配置。ActorAnimation 先挂接状态、处理重播或显式采样，再执行 `LocomotionMovePlayback.PrepareMove`：更新参数 → 重算权重 → 重置重新入场的非循环样本 → 计算参考速度与倍率。无 MovePlayback 的兼容请求复用同一个参数处理函数。公开 `Prepare(context)` 继续支持已有调用。

### 5.2 Tick 与会话归属

ActorLocomotion 的 `LocomotionTickState` 只保存当前 Tick，阶段为未准备、输入已锁定、运动已准备、动画已提交；待提交输入继续由 IntentBuffer 管理。Build 缓存请求，只积分一次；动画快照固定保存输入、积分前后速度与 Motor 上下文。垂直速度、Action owner 和已求值脚相在动画阶段读取。零动画 dt 不提交或消费决策边沿；冻结恢复的一次性输入不能覆盖更晚的提交或清空。

模式切换、取消和 Disable 先结束播放会话、保护当前基础姿势，再由 Runtime.Exit 清执行状态。ActorAnimation 独立关闭或替换图时，以组件与 owner 通知 ActorLocomotion；接收者只清对应引用并 Reset 当前 Runtime，不回调发起者。首次播放会话不再次 Reset 已 Enter 的 Runtime。所有权检查、清理幂等和标签成对释放仍保留。

### 5.3 代码入口

| 位置 | 职责 |
| --- | --- |
| `Actor/ActorLocomotion.cs` | Tick、选择、Runtime 缓存、会话与标签协调 |
| `Actor/ActorLocomotion.Debug.cs` | 仅 Editor 的 Trace 与迁移检查 |
| `Actor/ActorAnimation.cs` | 动画图、层、混合、所有权、保护快照及最终求值 |
| `Actor/ActorMotor.cs`、`Actor/Motion/` | 共享积分与运动合同、Motor 仲裁和反馈 |
| `Actor/Locomotion/Configuration/` | 原有 Asset 与条件的序列化配置 |
| `Actor/Locomotion/Runtime/` | 只读绑定、共同动画 Runtime、Mixer/Set Runtime、状态机和播放准备 |
| `Actor/Locomotion/Contracts/` | 输入、运动/动画上下文、请求、状态和 owner 合同 |
| `Actor/Locomotion/Editor/` | Locomotion Asset Inspector |

实施与验证状态见[结构整理交付记录](../Archive/Locomotion/CombatSample_Locomotion_Structure_Refactor_2026-10-05_zh-CN.md)。

## 6. Gameplay 移动与 Motor 仲裁

共享运动积分器读取当前 Runtime 绑定的 MovementConfig：

```text
MaxSpeed
Acceleration
Deceleration
DirectionResponse
RotateSpeed
TurnResponseTime
```

Ground 与 Air 资产使用同一份简洁结构，各自直接配置自己的速度和响应；例如旧的空中控制系数在迁移时折算进 Air Asset 的 `MaxSpeed`，不再作为 Ground Asset 内的第二组分支参数。共享积分器用有效 dt 将 Intent 积分为未缩放的基础水平速度和目标旋转。Facing 是否更新只由 Intent 是否携带有效 Facing 决定。输入目标速度为输入方向乘输入强度与 MaxSpeed。目标速度归零时，速度按 Deceleration 匀减速到零，停止距离预测继续使用同一模型。有移动目标时，DirectionResponse（1/s）先以 `1 - exp(-DirectionResponse × dt)` 将当前速度向同速的输入方向对齐，再按对齐后的速度与目标速度大小使用 Acceleration／Deceleration 接近目标速度；45°、90°、180° 都采用同一规则，反向向量自然抵消。DirectionResponse 为 0 时关闭额外方向对齐，普通向量加减速仍有效。速度方向响应与 RotateSpeed／TurnResponseTime 的朝向旋转独立；是否播放 Pivot 不改变 Gameplay 积分规则。

Motor 消费 Request 后应用 Locomotion/Air Scale，并保留既有仲裁：

```text
水平：HorizontalVelocityOwner > Action RootMotion > Locomotion + HorizontalImpulse
垂直：VerticalVelocityOwner，否则 Ballistic
旋转：ScriptedRotation > RootRotation > LocomotionRotation
```

Motor/KCC 继续负责重力、斜坡、碰撞、ForceUnground 和实际速度发布。Action 覆盖不会停止 Locomotion 内部状态；覆盖结束后使用当时的 Request，不追偿被覆盖的位移。

## 7. 动画数据、选择语义与匹配

数据分成两层：

```text
LocomotionAsset
    描述动画在什么运动关系下被选择

AnimationAsset
    描述动画自身实际发生了什么
```

LocomotionAsset 保存 Move threshold/sync，以及 Start/Stop/Pivot 的方向关系。AnimationAsset 保存 Clip 与累计 Root Motion 轨迹。相同 AnimationAsset 被多个 LocomotionAsset 引用时不重复录入轨迹。

### 7.1 Move velocity matching

Animation Runtime 使用上一 Tick 已发布且来源合格的 `ActualSolvedVelocity` 微调 Move，而不是使用输入冒充真实运动。水平 owner、Action Root Motion、显著冲量、平台携带、Actor 分离或未知来源不能作为自主步速。当前 Policy、Ground 或 Asset 改变使旧反馈失效时，先用模型速度和中性 PlayRate，取得新合格结果后再平滑恢复。

Mixer 按配置的参数源（输入或速度）选择样本，再按当前权重、Clip 完整周期的 Root Motion 位移和 Animancer 基础同步速率计算参考速度。上一 Tick 合格世界速度只调整有界 PlayRate；缺轨迹或参考速度不足则保持倍率 1。普通 Locomotion 不向 Motor 提交动画 Root Motion。

HorizontalSpeed 直接使用政策缩放后的水平模型速度，不额外截断到最高样本阈值，也不内置参数平滑。样本阈值只决定 Mixer 权重；超出范围时由 Mixer 保持端点权重。速度过零时可以混入 Idle；若希望持续满输入时保持 Run，应选择 InputStrength。参数取值与状态 crossfade、播放倍率匹配是独立机制，后两者继续保留。

### 7.2 基础 Stop

存在有效 Stop 时，Set 先按积分前的移动方向选择最接近的方向，再在方向同分的素材中比较当前播放动画与候选实际起播点的脚相；仍同分时保留配置顺序。ActorAnimation 在本 Tick 提交新动画前遍历 Locomotion 基础层，按层权重 × 各级父节点权重 × 叶节点权重选择贡献最大的有效动画，并以其实际已求值时间读取绑定脚相。同权重保留图遍历顺序；主要动画没有有效脚相时返回未知，不改选较小权重的动画，Idle 和保护姿势同样适用。没有参考脚相时继续按方向和配置顺序选片。

`StopPlaybackMode` 显式选择 Time（旧配置默认）或 Distance。Time 从零按速度 1 播放。Distance 读取当前 Motion 积分后的模型速度、Runtime 绑定的 Deceleration 和当前 Policy，用线性减速模型 `speed² / (2 × deceleration) × locomotionScale` 预测剩余停止距离；以此反查候选 Stop 到停止点的平面路径长度曲线。距离超过曲线范围时取边界；Stop 中时间只能前进，零距离采样作者停止点一次，下一 Tick 收尾按普通时间播放。曲线缺失、原地动画或无法得到有限停止预测（例如 Deceleration 为零）时，该次 Stop 使用 Time。脚相只用于选片，不另建时钟。

没有 Stop 或 Stop 提前完成时，速度模式继续表现模型减速；输入模式在松键后使用零参数。新输入、Action 覆盖和 Asset 切换沿基础状态机打断。距离匹配不改变 Gameplay 刹车，也不利用碰撞后的实际速度修正动画轨迹。预测采用连续刹车距离，Motor 的离散积分与碰撞可能产生偏差。

## 8. 资源合同与功能范围

正式角色使用同一套运行逻辑。动画覆盖、方向选择和混合效果由作者运行后调整，Locomotion 不做资源表现验收。Start/Stop/Pivot 独立可选；Move 速度匹配需要可用轨迹，轨迹不足时保持倍率 1 并继续基本播放。数值安全、动画所有权与生命周期保护由代码保证。

当前包括：

- 唯一 Intent/Motion Request 链；
- `LocomotionMixerAsset` 与 `LocomotionSetAsset`；
- Gameplay 加减速、转向和空中控制；
- Layer 0 Move/Start/Stop/Pivot；
- Air Move Mixer，Jump/Land Action；
- Move velocity matching；
- Stop Distance Matching 与 Stop 脚相择优；
- AnimationAsset 根轨迹、自动停止点、脚标记、独立 Override 与原生 Clip 预览。

当前没有实现 Move 的脚标记同步、Start Distance Matching、Stride/Orientation Warping、Foot IK/Foot Lock、Motion Matching/Pose Search、复杂 Trajectory Matching，以及任意可视化 Locomotion 状态图编辑器。

## 9. 当前实现状态

| 已落地 | 验证与制作边界 |
| --- | --- |
| Intent、共享积分、模式选择、Motion/Animation 请求链已接入。 | 本轮 Runtime 和 Editor／测试程序集编译通过；完整 Unity 原生合同待执行。 |
| Mixer/Set、独立 Facing、可选 Start/Stop/Pivot、Move 参数源与速度匹配已接入。 | 角色可以按制作目标配置候选与素材；当前 Kiana 体感反馈不代表所有角色或模式已验收。 |
| Stop 距离时钟、主要动画脚相参考、自动数据与作者覆盖已接入。 | 最新 Left/Right 配置的停止衔接待验收；脚相择优是近似，不保证任意入场姿势完全无缝。 |
| 只读绑定、Tick 值结构、统一 Move 准备、独立保护快照与生命周期清理已整理。 | 图重建、混合交接、Sync 和 owner 清理仍需完整原生合同与对应人工回归。 |

主要代码入口：[ActorLocomotion](../../Assets/Scripts/Actor/ActorLocomotion.cs)、[ActorMotor](../../Assets/Scripts/Actor/ActorMotor.cs)、[ActorSimulationRuntime](../../Assets/Scripts/Actor/ActorSimulationRuntime.cs)、[ActorAnimation](../../Assets/Scripts/Actor/ActorAnimation.cs)、[LocomotionRunner](../../Assets/Scripts/Actor/Motion/LocomotionRunner.cs)、[AnimationAsset](../../Assets/Scripts/Animation/AnimationAsset.cs)。


## 10. AnimationAsset 的停止点与脚标记

同一次 Animation Bake 生成根轨迹、自动停止点和左右脚接触标记。自动停止点是最早使所有后续水平根位置均保持在最终水平根位置半径 2 cm 内的采样时刻，表达轨迹最终稳定；作者可以覆盖为自己的刹车结束点。全程已处于该区域内时返回 0，没有可用距离段时 Stop 使用 Time。停止距离曲线是到有效停止点的累计平面路径长度。脚标记读取固定地面上方向的脚世界高度，以高度范围的 10% 作为显著抬起／回落的高度差，在每次显著下降后的局部高度谷取一个候选，不要求所有步达到相同落点高度。非循环动画的初始站立不当作新落脚，末尾持续下降不生成未完成候选；循环素材跨接缝检测。它们是可修改的骨骼高度谷估计，不声称能判断所有素材的真实接触。

`AnimationRigAsset` 上一次性填写相对 Animator 的左右脚骨骼路径；Humanoid 空路径自动使用 LeftFoot/RightFoot。无法取得脚骨骼时仍烘焙根轨迹，脚标记为空，不阻止播放。脚路径变更后应重烘焙相关素材。

`AnimationAsset` Inspector 显示 Root X/Y/Z 曲线、剩余距离曲线、停止帧与脚接触帧列表。作者可以启用停止点或脚标记覆盖；同 Clip 重烘焙保留覆盖，关闭覆盖恢复自动数据，更换 Clip 后旧数据和覆盖不参与新 Clip 的运行。原生 AnimationClip Editor 提供预览，不另建时间轴，也不联动它的内部播放头。
