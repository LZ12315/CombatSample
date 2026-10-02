# CombatSample Locomotion 最终架构 v1

> 状态：**基础 Locomotion 与 Move 速度匹配代码已落地；Unity 合同测试、素材接入和角色视觉验收尚待完成。脚相与 Stop Distance Matching 已退出当前实现。** 本文记录当前职责和数据模型；[Implementation Roadmap v1](CombatSample_Locomotion_Implementation_Roadmap_v1_zh-CN.md)记录迁移顺序。阶段 0 的历史盘点保留当时名称和事实，不随本次设计回写。

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

Locomotion 动画的 Root Motion 轨迹用于 Move 速度匹配，不直接推动 Capsule。Action 的 `RootMotionItem` 仍是独立 Motion Source，并按现有优先级真正驱动 Motor。

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

### 输入与资源的合法使用边界（2026-09-29 收敛）

- 同一 Actor 同时只有一个移动控制者；同一 AI 图同时只有一个活动移动任务。输入不是多 owner 仲裁系统，多个控制者并行争用属于错误使用。
- 单一输入缓冲保存最新提交。公开 `SetLocomotionIntent` 是一次性输入；持续 AI 输入保持到更新、释放或生命周期清理。新提交直接替换旧提交，不叠加优先级、不暂存旧控制者、不在单次输入结束后恢复旧输入。
- Control 锁定本 Tick 快照，之后的提交或释放影响下一 Tick；不能重开已积分的 Tick。零有效 dt 保留未消费的单次输入；新的提交或明确释放优先，不因冻结而恢复已释放输入。
- `ClearLocomotionIntent` 只释放输入，现有模型速度由正常 Motion 减速；存在有效 Stop 时可以播放停止过渡，否则 Move 随模型速度降到 Idle。Disable 或 Driver abort 才清理 Tick、Runtime 和共享模型速度。
- MovementConfig 必须合法；非法输入方向、强度或配置明确失败，不使用默认速度、默认减速度或 Idle 修正错误数据。`Sanitize` 保留现有入口，但非法配置会抛错。
- Locomotion 资源在 Runtime 生命周期内视为固定配置。运行期间不支持修改 Clip、样本或 Rig并即时热刷新。修改后重新开始运行，或禁用再启用 ActorLocomotion，以 Dispose 旧 Runtime 后重新绑定。正常 Asset 切换、Action 覆盖、HitStop、Disable/Enable 和 Graph 重建继续受生命周期合同保护。
- 制作工具负责依赖和过期提示；Runtime 在绑定时验证所需数据并缓存。Move 轨迹复用 Bake 来源校验，不增加第二份人工资格确认。Player 使用已制作的烘焙资源，不运行 Editor 依赖分析。
- 有效 Clip 的基础播放与可选匹配明确区分：缺少有效 Move 轨迹时以中性倍率播放并报告。Ground 的零速 Pose 和非零 Move Pose 必须成立；Start/Stop/Pivot 的空列表合法。配置损坏时报告具体条目并拒绝对应动画组，不剔除坏样本拼凑成功。姿态保护只保护生命周期或基础播放失败，不用于正常缺少可选过渡的情况，也不代表错误配置通过验收。

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
- `LocomotionSetAsset` 在持续 Move 上提供可选的 Start/Stop/Pivot；只有 Move 的 Set 同样是完整的基础播放配置。

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
- HorizontalSpeed 和 LocalVelocity 同时需要至少一个非零移动样本；VerticalSpeed 提供适合空中的基础 Pose，不强制要求地面 Idle；
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

方向是在进入状态时，相对于角色 Facing 的二维局部运动方向：`x` 为右，`y` 为前。Start 使用目标 Intent；Stop 使用积分前的模型速度；Pivot 使用旧运动方向和新目标方向。Pivot 的有符号角度由两向量计算，不再单独重复配置 `TurnAngle`。

Start/Stop/Pivot 可独立留空，空列表表示没有该表现能力。非空组的所有条目必须有有效 Clip 和方向；任一损坏条目使该组不可用并报告错误，其他有效组及基础 Move 继续使用。基础过渡不要求 Root Motion 轨迹。

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

- 静止时出现有效移动意图，存在可播放的 Start 才进入 Start，否则继续 Move；
- 失去移动意图且仍有速度，存在可播放的 Stop 才进入 Stop，否则 Move 随模型减速；
- 有速度时目标方向发生足够强的反转，存在可播放的 Pivot 才进入 Pivot，否则继续 Move 并正常 Gameplay 转向；
- 新输入可打断 Stop；Action/Asset 切换可以中断所有过渡；
- Runtime 先取得下一状态建议，选择并准备有效 Clip 后才提交状态。未进入的过渡不排队，输入边沿仍在有效 Tick 消费；零 dt 不推进决策；
- 所有过渡完成后返回使用当前模型参数的 Move，不锁存零速；Start/Pivot 释放输入但没有 Stop 时同样返回 Move；
- 状态拓扑、优先级、打断规则和阈值由代码统一维护，不做成任意 Condition 图；
- Locomotion 的基础播放有效性由核心 Pose 能力决定。可选列表为空是正式路径；基础 Move 无法成立时报告错误，不启动可选过渡，不改变 Gameplay 积分。

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

LocomotionAsset 保存 Move threshold/sync，以及 Start/Stop/Pivot 的方向关系。AnimationAsset 保存 Clip 与累计 Root Motion 轨迹。相同 AnimationAsset 被多个 LocomotionAsset 引用时不重复录入轨迹。

### 7.1 Move velocity matching

Animation Runtime 使用上一 Tick 已发布且来源合格的 `ActualSolvedVelocity` 微调 Move，而不是使用输入冒充真实运动。水平 owner、Action Root Motion、显著冲量、平台携带、Actor 分离或未知来源不能作为自主步速。当前 Policy、Ground 或 Asset 改变使旧反馈失效时，先用模型速度和中性 PlayRate，取得新合格结果后再平滑恢复。

Mixer 始终按模型速度选择样本，再按当前权重、Clip 完整周期的 Root Motion 位移和 Animancer 基础同步速率计算参考速度。上一 Tick 合格世界速度只调整有界 PlayRate；缺轨迹或参考速度不足则保持倍率 1。普通 Locomotion 不向 Motor 提交动画 Root Motion。

1D HorizontalSpeed 的视觉参数先限制在零速至最高样本阈值范围，再按整个范围 / 0.1s 限制每 Tick 的参数变化。这样持续反向时的短暂模型速度低谷不会立即占满 Idle 权重；模型速度归零后，参数最多再用 0.1s 到达零，不积累超出 Mixer 范围的速度延迟。平滑只使用动画有效 dt，不改变 Gameplay 积分、过渡触发或朝向。首 Tick 从当前值建立，新会话和 Graph 重建重置缓存；零 dt 保留参数。2D 方向参数与 Air VerticalSpeed 保持原语义。该规则改善短暂低谷，不保证所有低速配置都不混入 Idle，角色观感仍须验证。

### 7.2 基础 Stop

存在有效 Stop 时，Set 按积分前的移动方向选择 Clip，从时间 0 按速度 1 播放至 Clip 结束，再返回当前模型参数的 Move。没有 Stop 或 Stop 提前完成而模型仍有速度时，Move 继续表现减速，模型归零后显示 Idle。新输入、Action 覆盖和 Asset 切换沿基础状态机打断，不预测剩余停止距离，也不改变 Gameplay 刹车。

## 8. 资源合同与范围

正式角色使用同一套运行逻辑。Ground 需要有效零速 Pose 和非零 Move Pose，Air 需要适合空中的有效 Move。Start/Stop/Pivot 独立可选，不要求填满固定模板；已配置条目的 Clip 或方向损坏才是配置错误。用于速度匹配的 Move 样本需要有效 Root Motion 轨迹；轨迹不足不阻止基础播放。基础 Pose 缺失时的错误保护不计作验收通过。

本轮包括：

- 唯一 Intent/Motion Request 链；
- `LocomotionMixerAsset` 与 `LocomotionSetAsset`；
- Gameplay 加减速、转向和空中控制；
- Layer 0 Move/Start/Stop/Pivot；
- Air Move Mixer，Jump/Land Action；
- Move velocity matching。

本轮不包括：脚相同步、Stop/Start Distance Matching、Stride/Orientation Warping、Foot IK/Foot Lock、Motion Matching/Pose Search、复杂 Trajectory Matching，以及任意可视化 Locomotion 状态图编辑器。

## 9. 当前实现状态

| 已落地 | 后续工作 |
| --- | --- |
| Intent 与 Motion Request 已归 ActorLocomotion；选择发生在 `Motor.BeginMotion` 之后。 | 在 Unity 中确认编译、序列化迁移和 Kiana/Jaeger 场景行为。 |
| `LocomotionAsset` 已成为抽象基类；Mixer/Set、共享 MoveDefinition 与内嵌 MovementConfig 已建立；基础动画 Runtime 已接入 Layer 0。 | 在 Unity Test Runner 和角色资源上验证实际动画行为。 |
| ActorLocomotion 已使用单一候选列表，并按 Conditions、Priority、当前候选稳定保持和 authored order 选择。 | 为后续 Lock-on、武器姿态等资产补充实际候选与条件。 |
| Facing 已由 Player/AI Intent 显式提交；Runner 不再读取动画类型。 | 按具体角色手感调整速度、加减速与转向配置。 |
| Ground Set 与 Air Mixer 已作为平行候选接入；Jump/Land 仍属于 Action；Set 过渡独立可选。 | 补齐基础 Ground/Air Pose；按角色需要配置有效过渡，空可选列表不列为缺口。 |
| Runtime 工厂、每 Actor 缓存、Layer 0 会话与 Set 瞬态生命周期已建立；Move 轨迹速度匹配已独立于脚相。 | 角色资源、Unity Test Runner 与速度/停止视觉检查仍待整体验收。 |

主要代码入口：[ActorLocomotion](../../Assets/Scripts/Actor/ActorLocomotion.cs)、[ActorMotor](../../Assets/Scripts/Actor/ActorMotor.cs)、[ActorSimulationRuntime](../../Assets/Scripts/Actor/ActorSimulationRuntime.cs)、[ActorAnimation](../../Assets/Scripts/Actor/ActorAnimation.cs)、[LocomotionRunner](../../Assets/Scripts/Actor/Motion/LocomotionRunner.cs)、[AnimationAsset](../../Assets/Scripts/Animation/AnimationAsset.cs)。
