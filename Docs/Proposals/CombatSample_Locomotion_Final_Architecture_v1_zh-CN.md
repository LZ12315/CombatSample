# CombatSample Locomotion 最终架构 v1

> 状态：**目标架构已冻结；阶段 1 主链、阶段 2 数据模型与阶段 3 基础动画 Runtime 已在工作区落地，Unity 合同测试、素材接入和场景视觉验收尚待完成。** 本文记录最终职责和数据模型；[Implementation Roadmap v1](CombatSample_Locomotion_Implementation_Roadmap_v1_zh-CN.md)记录迁移顺序。阶段 0 的历史盘点保留当时名称和事实，不随本次设计回写。

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

Locomotion 动画的 Root Motion 轨迹用于样本分析、速度匹配和距离匹配，不直接推动 Capsule。Action 的 `RootMotionItem` 仍是独立 Motion Source，并按现有优先级真正驱动 Motor。

## 2. 固定 Tick 的目标顺序

目标顺序是：

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

`LocomotionAsset` 是不可变配置和 Runtime 工厂，与 `ActionAsset` 对应。共同数据直接内嵌，不创建独立的 MovementConfig 或 AnimationSet 资源。

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

EntryConditions 为空表示这个显式候选无条件成立，但它仍参与同一套 Priority 和 authored order 选择；系统不再另设一个具有特殊地位的全局 Fallback。Grounded、Airborne、Lock-on、武器姿态等都是普通 Condition。没有候选是可诊断的配置错误：退出旧 Runtime、释放旧 SelfTags，并安全地不提交 Locomotion Motion/Animation，而不是静默选择错误资产。

### 4.2 两种具体 Asset

```text
LocomotionMixerAsset
└─ 只有持续 Move

LocomotionSetAsset
├─ 持续 Move
├─ Start[]
├─ Stop[]
├─ Pivot[]
└─ TransitionBlendDuration
```

二者是同一候选列表中的平行移动形态：

- `LocomotionMixerAsset` 适合只需要持续混合的形态，例如使用 VerticalSpeed 1D 的 Air locomotion；
- `LocomotionSetAsset` 适合需要 `Move / Start / Stop / Pivot` 内部生命周期的形态。

这不是 Ground/Air 类型划分。Ground 与 Air 由 Conditions 选择；Jump 和 Land 继续由 Action 实现。

### 4.3 MoveDefinition

`MoveDefinition` 位于基类，因为两种具体 Asset 都有 Move；它内部选择 1D 或 2D，但这个选择只描述 Move Mixer。

```text
MoveDefinition
├─ BlendType: OneDimensional | TwoDimensional
├─ 1D Parameter: HorizontalSpeed | VerticalSpeed
│  └─ Samples[]: AnimationAsset + float Threshold + bool Sync=true
└─ 2D Parameter: LocalVelocity
   └─ Samples[]: AnimationAsset + Vector2 Threshold + bool Sync=true
```

约束：

- HorizontalSpeed 阈值非负；VerticalSpeed 允许有符号值；
- 2D 的 `LocalVelocity` 使用角色当前 Facing 建立的局部空间；
- 阈值必须有限且不可重复；
- 每个样本的 `Sync` 默认开启，可单独退出同步；
- Idle 不设独立字段。1D HorizontalSpeed 的零阈值或 2D 的 `(0,0)` 样本就是 Idle；
- Loop 来自源 `AnimationClip`，不在 LocomotionAsset 重复配置；
- Runtime 创建并缓存 Animancer Mixer；不同步的样本调用 `DontSynchronize`。

1D/2D 是连续 Move 混合空间，不限制 Start/Stop/Pivot 的方向覆盖。典型 FreeMove 可以使用 1D Speed Move，同时拥有左前 Start 和 180° Pivot。

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

方向是在进入状态时，相对于角色 Facing 的二维局部运动方向：`x` 为右，`y` 为前。Start 使用目标 Intent；Stop 使用进入停止前的合格实际/模型速度；Pivot 使用旧运动方向和新目标方向。Pivot 的有符号角度由两向量计算，不再单独重复配置 `TurnAngle`。

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

速度和旋转积分器属于 ActorLocomotion 的共享运动状态，不属于某个具体 Asset Runtime。切换 Asset 时继承当前速度，避免每个 Runtime 保存一份过期速度或切换时归零。MovementConfig 的 MaxSpeed 是当前控制目标上限，不强制截断从上一状态继承的动量。

`LocomotionMixerRuntime` 始终更新 Move。`LocomotionSetRuntime` 内部拥有固定状态机：

```text
Move ↔ Start
Move ↔ Stop
Move ↔ Pivot
```

- 静止时出现有效移动意图，可进入 Start；
- 失去移动意图且仍有速度，可进入 Stop；
- 有速度时目标方向发生足够强的反转，可进入 Pivot；
- 新输入可打断 Stop；Action/Asset 切换可以中断所有过渡；
- 状态拓扑、优先级、打断规则和阈值由代码统一维护，不做成任意 Condition 图；
- 缺少必要动画是资源错误，不产生第二套“少素材”正式逻辑。

`ActorAnimation` 继续拥有 Animancer Graph。Runtime 只通过窄的 Layer 0 请求接口提交 Mixer、Clip、参数、时间和淡入淡出，不直接拥有最终动画图。

## 6. Gameplay 移动与 Motor 仲裁

共享运动积分器读取当前 Asset 的 MovementConfig：

```text
MaxSpeed
Acceleration
Deceleration
RotateSpeed
TurnResponseTime
```

Ground 与 Air 资产使用同一份简洁结构，各自直接配置自己的速度和响应；例如旧的空中控制系数在迁移时折算进 Air Asset 的 `MaxSpeed`，不再作为 Ground Asset 内的第二组分支参数。共享积分器用有效 dt 将 Intent 积分为未缩放的基础水平速度和目标旋转。Facing 是否更新只由 Intent 是否携带有效 Facing 决定。反向输入使速度向新目标向量连续变化；是否播放 Pivot 不改变 Gameplay 积分规则。

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

LocomotionAsset 只保存 Move threshold/sync，以及 Start/Stop/Pivot 的方向关系。`AnimationAsset` 保存或烘焙可复用事实：

- Clip 与累计 Root Motion 轨迹；
- 位移、速度和旋转曲线；
- 后续阶段加入的脚接触、脚相和循环映射；
- 必要的过渡语义标记，例如 Stop 的 BrakeEnd/SettleEnd。

能从 Clip 和轨迹稳定计算的内容自动烘焙；难以稳定推导的脚接触或语义点允许少量人工校正。相同 AnimationAsset 被多个 LocomotionAsset 引用时不重复录入这些事实。

### 7.1 Move velocity matching

Animation Runtime 使用上一 Tick 已发布且来源合格的 `ActualSolvedVelocity` 微调 Move，而不是使用输入冒充真实运动。水平 owner、Action Root Motion、显著冲量、平台携带、Actor 分离或未知来源不能作为自主步速。当前 Policy、Ground 或 Asset 改变使旧反馈失效时，先用模型速度和中性 PlayRate，取得新合格结果后再平滑恢复。

Mixer 先按模型/合格实际速度选择样本，再根据各子样本的权重、同步速率和轨迹参考速度求有界 PlayRate。普通 Locomotion 不向 Motor 提交动画 Root Motion。

### 7.2 Stop Distance Matching

Motion Runtime 使用与 Deceleration 积分一致的模型预测剩余停止距离。Stop AnimationAsset 提供到 BrakeEnd 的剩余水平路径长度查询；Runtime 在已选方向和脚相兼容的样本中反查起播位置，只允许时间向前推进。BrakeEnd 后按正常动画时间播放到 SettleEnd。

新输入、Asset/Ground/Policy/owner 变化可打断并重建 Stop；碰撞损失不追偿，动画匹配也不反向修改 Gameplay 停止距离。

## 8. 资源合同与范围

正式角色使用同一套运行逻辑。素材数量可以不同，但其 LocomotionAsset 必须覆盖承诺的 Move、Start、Stop、Pivot 方向关系，以及对应轨迹、脚相和标记。缺失素材属于制作缺口；运行时错误保护只能避免空白姿态或异常位移，不能作为正式降级体验。

本轮包括：

- 唯一 Intent/Motion Request 链；
- `LocomotionMixerAsset` 与 `LocomotionSetAsset`；
- Gameplay 加减速、转向和空中控制；
- Layer 0 Move/Start/Stop/Pivot；
- Air Move Mixer，Jump/Land Action；
- 最小脚相衔接、Move velocity matching、Stop Distance Matching。

本轮不包括：Stride/Orientation Warping、Foot IK/Foot Lock、Motion Matching/Pose Search、复杂 Trajectory Matching、Start Distance Matching，以及任意可视化 Locomotion 状态图编辑器。

## 9. 当前实现状态

| 已落地 | 后续工作 |
| --- | --- |
| Intent 与 Motion Request 已归 ActorLocomotion；选择发生在 `Motor.BeginMotion` 之后。 | 在 Unity 中确认编译、序列化迁移和 Kiana/Jaeger 场景行为。 |
| `LocomotionAsset` 已成为抽象基类；Mixer/Set、共享 MoveDefinition 与内嵌 MovementConfig 已建立；基础动画 Runtime 已接入 Layer 0。 | 在 Unity Test Runner 和角色资源上验证实际动画行为。 |
| ActorLocomotion 已使用单一候选列表，并按 Conditions、Priority、当前候选稳定保持和 authored order 选择。 | 为后续 Lock-on、武器姿态等资产补充实际候选与条件。 |
| Facing 已由 Player/AI Intent 显式提交；Runner 不再读取动画类型。 | 按具体角色手感调整速度、加减速与转向配置。 |
| Ground Set 与 Air Mixer 已作为平行候选接入；Jump/Land 仍属于 Action。 | 接入 Move、Start、Stop、Pivot 与 Air 的 AnimationAsset 样本；缺失样本仍是资源缺口。 |
| Runtime 工厂、每 Actor 缓存、Layer 0 会话与 Set 瞬态生命周期已建立；阶段 4 脚相元数据、共同同步、出口衔接与有界 Move 速度匹配代码已实现。 | 阶段 3/4 角色资源、Unity 测试与视觉出口仍待整体验收；阶段 5 再接入停止距离匹配。 |

主要代码入口：[ActorLocomotion](../../Assets/Scripts/Actor/ActorLocomotion.cs)、[ActorMotor](../../Assets/Scripts/Actor/ActorMotor.cs)、[ActorSimulationRuntime](../../Assets/Scripts/Actor/ActorSimulationRuntime.cs)、[ActorAnimation](../../Assets/Scripts/Actor/ActorAnimation.cs)、[LocomotionRunner](../../Assets/Scripts/Actor/Motion/LocomotionRunner.cs)、[AnimationAsset](../../Assets/Scripts/Animation/AnimationAsset.cs)。
