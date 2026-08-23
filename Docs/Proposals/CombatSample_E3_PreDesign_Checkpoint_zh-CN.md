# CombatSample E3 前置架构决策 Checkpoint

> 状态：讨论 Checkpoint；记录截至 2026-08-23 已明确确认的结论、已被取代的旧方向，以及仍未收口的问题。
>
> 基线：Stage E2 已落地；E3 尚未开始正式实现。
>
> 目的：冻结 E3 实现前已经收口的 Input / ActorLocomotion / ActorMotor / Animation / MotionPolicy / HitDetection 核心架构，作为 Final Architecture v3 的直接输入。
>
> 注意：本文不是 E3 Implementation Plan，也不是 Final Architecture v3。未列入“Confirmed Decisions”的内容不得视为已经批准。

---

## 1. Context

Stage E2 已经完成玩家输入所有权和固定 Tick `LocomotionIntent` 的切分：

```text
PlayerInputController
    = 玩家真实输入、raw move/look、按钮状态、输入历史 owner

PlayerLocomotionIntentResolver
    = raw move + Camera/Lock 上下文 -> LocomotionIntent

Enemy AI / BehaviorTree
    = 独立生产 LocomotionIntent

ActorMotor
    = LocomotionIntent 的消费者，不理解来源是 Player 还是 AI
```

E2 之后，对以下领域进行了连续架构复核并逐步收口：

- Simulation Driver 与 producer / consumer 顺序；
- ActorLocomotion 与 Action 的职责边界；
- ActorAnimation authority 与 Base / Override 模型；
- Root Motion authority；
- ActorMotor 的 Translation / Rotation 两大 Domain；
- LocomotionRunner；
- 水平 / 垂直 motion channel 与 compose；
- Root Rotation / Scripted Rotation arbitration；
- MotionPolicy 的参数、ownership 与配置入口；
- LocomotionModeAsset / Profile selection lifecycle。

---

# 2. Confirmed Decisions

## 2.1 总体原则：统一生命周期，分离控制通道

Action / Sequence 可以统一 Gameplay 生命周期，但不得成为 Animation、Movement、HitBox、Rotation、Tags 等所有子系统的统一状态 owner。

```text
ActionInstance / ActionSequence lifecycle
        │
        ├── Animation contribution
        ├── Motion channel contribution
        ├── MotionPolicy clip contribution
        ├── HitBox window
        ├── Tags
        └── other scoped contributions
```

各领域独立仲裁：

```text
Animation arbitration
Translation arbitration
Rotation arbitration
HitBox lifecycle / query
Tags / gameplay effects
```

不得建立一个“大 ActionMode / LocomotionMode 开关”一次性切换所有系统控制权。

> 统一生命周期，分离控制通道。

---

## 2.2 Simulation Driver：执行顺序是第一职责

`CombatSimulationDriver` 是 Combat World scheduler；固定 60Hz 是 timestep policy，不是 Driver 的根本身份。

必须保持全局 phase barrier：

```text
ALL Actors Control Production
→ ALL Actors Action / Sequence
→ ALL Actors Locomotion / Animation Requests
→ ALL Actors Animation Evaluate
→ ALL Actors Motion Requests / Motor Preparation
→ ALL Actors Movement / KCC
→ Physics Sync（需要时）
→ ALL Hit Detection
→ ALL Hit Resolution
```

禁止按 Actor 纵向执行：

```text
Actor A Action -> Move -> Detect
Actor B Action -> Move -> Detect
```

每个 Actor 只通过一个 `ActorSimulationRuntime` 参与固定模拟；子系统不向 Driver 注册通用 phase callback。

KCC 是世界运动求解 barrier，不是 Gameplay scheduler。

---

## 2.3 Input / LocomotionIntent

玩家输入链：

```text
Input System
→ PlayerInputController
→ PlayerLocomotionIntentResolver
→ LocomotionIntent
→ ActorMotor
```

AI 独立生产：

```text
AI / BehaviorTree
→ LocomotionIntent
→ ActorMotor
```

已确认：

- `PlayerInputController` 是 raw input 与 input history owner；
- `PlayerLocomotionIntentResolver` 只解释玩家输入；
- AI 不模拟 joystick / button；
- `ActorMotor` 不知道 intent 来源；
- 不引入无现实需求的通用 Input Provider / Command Bus / Intent Source Manager；
- `ActorLogicInput` 不再是 runtime authority；
- `ActorLocomotion` 不是 `LocomotionIntent -> ActorMotor` 的必经中转层。

---

## 2.4 ActorMotor：Movement Authority + KCC Adapter

ActorMotor 的目标心智模型已经重构为：

```text
ActorMotor
= Actor movement authority + KCC Adapter

                    ActorMotor
                       │
        ┌──────────────┴──────────────┐
        │                             │
    Translation                   Rotation
        │                             │
  multiple contributions        multiple contributions
        │                             │
  fixed arbitration             fixed arbitration
        │                             │
        └──────────────┬──────────────┘
                       ↓
                      KCC
```

Translation 与 Rotation 是 ActorMotor 的两个 first-class Domain。

目标结构：

```text
ActorMotor
├─ LocomotionRunner
│    LocomotionIntent
│    → Translation native data
│    → Rotation local yaw delta
│
├─ Translation Domain
│    ├─ Locomotion
│    ├─ Root Motion
│    ├─ Horizontal Impulse
│    ├─ Horizontal Velocity Owner
│    ├─ BallisticVerticalVelocity
│    └─ Vertical Velocity Owner
│
├─ Rotation Domain
│    ├─ Locomotion Rotation
│    ├─ Root Rotation
│    └─ Scripted Rotation
│
└─ Supporting State
     ├─ Grounding
     ├─ Velocity Readout
     └─ MotionPolicy
```

现有 `LocomotionRuntime / FacingRuntime / ActorMotionRuntime / SelfRotationBuffer` 的类划分不是长期架构边界；后续实现应从上述 Domain 模型反推结构，而不是保留旧 Runtime 名称作为一级概念。

---

## 2.5 LocomotionRunner

`LocomotionRunner` 是 ActorMotor 内部、KCC 之前的 locomotion interpreter / producer，不是平行 movement authority。

```text
LocomotionIntent
+
Locomotion tuning
+
Grounded / Airborne
+
Current Rotation
+
Effective MotionPolicy
        ↓
LocomotionRunner
        ├→ Locomotion Translation: Vector3 velocity
        └→ Locomotion Rotation: local yaw delta
        ↓
ActorMotor Translation / Rotation arbitration
```

Producer 负责解释 WHY / WHERE / HOW；Motor 只接收已经解析好的物理运动量并进行保存、仲裁、Compose。

因此 ActorMotor 不理解：

```text
Target
Direction
Snap
RotateBySpeed
Attack
LockOn
```

`BaseSpeed / AirControlFactor / RotateSpeed` 属于 locomotion tuning，而不是 MotionPolicy。

---

## 2.6 Root Motion authority

继续遵守：

> Sequence is authoritative; Animation is data.

Gameplay Root Motion：

```text
Sequence fixed frame
→ RootMotionTrajectory
→ RootMotionClip
→ ActorMotor
→ KCC
```

Animation 不反向决定 Gameplay movement。

长期删除：

```text
Animator
→ OnAnimatorMove
→ Animator.deltaPosition / deltaRotation
→ ActorRootMotionRelay
→ ActorMotor
```

未来任何 Root Motion 来源都必须先产生明确 motion data，再显式提交给 Motor。

术语统一：

- **Root Motion**：`RootMotionTrajectory` 烘焙得到的 XZ displacement；
- **Root Rotation**：同一 trajectory 烘焙得到的 Yaw rotation。

Root Motion 的 Y 永久丢弃。

---

## 2.7 Translation：submission 与 final arbitration 分离

固定原则：

> Producer 独立提交；各 Channel 独立维护状态；最终只有 Compose 决定哪些 contribution 生效。

高优先级 Channel 当前生效，不得阻止较低 Channel：

- 接收新提交；
- 更新自身状态；
- 推进自身生命周期。

被覆盖期间不产生 catch-up / missed delta 累积。

---

## 2.8 水平 Translation

水平 contribution：

```text
Locomotion
HorizontalImpulse
Root Motion
HorizontalVelocityOwner
```

最终 Compose 固定为：

```text
if HorizontalVelocityOwner exists
    Horizontal = TopHorizontalVelocityOwner
else if RootMotion exists
    Horizontal = TopRootMotion
else
    Horizontal = Locomotion + HorizontalImpulse
```

即：

```text
HorizontalVelocityOwner
>
Root Motion
>
Locomotion + HorizontalImpulse
```

### HorizontalVelocityOwner

使用可恢复 LIFO stack：

```text
A Begin -> A
B Begin -> B
B End   -> A
```

非栈顶 owner 可以继续更新。

### Root Motion

Root Motion owner 同样使用可恢复 LIFO stack：

```text
A ---------------------
      B -------

output:
A A A | B B B | A A A
```

被覆盖的 A：

- Sequence 时间继续推进；
- 可以继续提交当前 tick delta；
- 被覆盖 delta 不累计；
- B 结束后从当前时间继续，不补发过去位移。

### HorizontalImpulse

HorizontalImpulse 是独立 additive / decaying channel。

只有 Compose 落到 locomotion 分支时：

```text
Horizontal = Locomotion + HorizontalImpulse
```

Root Motion / HorizontalVelocityOwner 覆盖期间，Impulse 仍可继续接收提交并在后台按自己的规则衰减。

明确否决：

```text
RootMotion Begin -> ClearHorizontalImpulse
RootMotion active -> Reject AddHorizontalImpulse
HorizontalVelocityOwner active -> Reject lower submissions
```

---

## 2.9 垂直 Translation：Ballistic + VerticalVelocityOwner

长期删除：

```text
GravityAccumulator
+
VerticalImpulseVelocity
```

自由弹道收敛为：

```text
BallisticVerticalVelocity
```

Gravity 是 Ballistic 的持续演化：

```text
BallisticVerticalVelocity += PhysicsGravityY * EffectiveGravityScale * dt
```

Jump / DoubleJump / Launcher / Hit / Impulse 等 producer 通过事件式操作修改 Ballistic。

至少支持：

```text
Add（默认）
velocity += value

Set
velocity = value
```

Ballistic 不理解 Jump / Hit / Launcher 等 Gameplay 标签。

---

## 2.10 VerticalVelocityOwner

语义：

```text
BallisticVerticalVelocity
= 自由弹道

VerticalVelocityOwner
= scripted / Action-driven 垂直轴临时接管
```

最终：

```text
if VerticalVelocityOwner exists
    Vertical = TopVerticalVelocityOwner
else
    Vertical = BallisticVerticalVelocity
```

VerticalVelocityOwner 使用可恢复 LIFO stack；非栈顶 owner 可以继续更新。

只要 stack 非空：

```text
BallisticVerticalVelocity 冻结
Gravity 不继续积分
```

当 owner count 从 `> 0` 变成 `0`：

```text
BallisticVerticalVelocity = 0
```

默认不恢复 owner 接管前 Ballistic velocity，也不继承最后一个 owner velocity。

未来只有出现明确玩法需求时才增加 finish / handoff policy。

---

## 2.11 Grounded 与垂直系统

稳定接地：

```text
Final Vertical = 0
BallisticVerticalVelocity = 0
Grounded 期间不积累 Gravity
```

Grounded 不得：

```text
销毁 VerticalVelocityOwner
修改 owner velocity
阻止 producer 继续 Submit
```

例如：

```text
VelocityClip 持续提交 -20
Grounded = true

Owner state = -20
Final Vertical = 0
```

如果 owner 仍存活且 Actor 再次离地，`-20` 可以重新参与 Compose。

有效向上 Ballistic / owner 写入仍需要 ForceUnground 类机制。

---

## 2.12 Rotation Domain

Rotation 是与 Translation 并列的一等 Domain。

来源：

```text
Locomotion Rotation
Root Rotation
Scripted Rotation
```

固定 precedence：

```text
Scripted Rotation
>
Root Rotation
>
Locomotion Rotation
```

### 数据 contract

三个 Rotation channel 最终都提交：

```text
this tick's local yaw delta
```

- LocomotionRunner 解释 facing intent + current rotation + locomotion tuning，输出 local yaw delta；
- Root Rotation 输出 trajectory baked local yaw delta；
- Scripted producer 在 Motor 外解释 Target / Direction / Snap / RotateBySpeed，再输出 local yaw delta。

Motor 不理解这些 Gameplay 语义。

最终只使用 winning channel：

```text
RequestedRotation
= TickStartRotation * WinningLocalYawDelta
```

不同 Rotation channel 不相加、不相乘。

---

## 2.13 Rotation ownership / covered semantics

`Root Rotation` 使用自己的可恢复 LIFO owner stack。

`Scripted Rotation` 使用自己的可恢复 LIFO owner stack。

两个 stack 不合并为“最后 Begin 的全局 winner”，因为固定 precedence 必须始终保持：

```text
if Scripted stack nonempty
    use Scripted top
else if Root Rotation stack nonempty
    use Root Rotation top
else
    use Locomotion Rotation
```

被覆盖的 Rotation contribution：

- 可以继续计算 / 提交；
- 不累计 missed yaw；
- 恢复后只使用当前 tick 的 delta。

现有 `SelfRotationClip` 中混合的概念需要按长期语义拆开理解：

```text
RootRotation source
→ Root Rotation channel

Target / Direction source
→ Scripted Rotation producer

Snap / RotateBySpeed
→ producer-side resolution mode
```

---

## 2.14 MotionPolicy：Supporting State，而不是新 Runtime

MotionPolicy 属于 ActorMotor 的 supporting state，不创建新的“大 `MotionPolicyRuntime`”。

它不负责：

```text
Tick
Gameplay decision
Intent interpretation
Translation Compose
Rotation arbitration
Action knowledge
```

它只负责：

```text
neutral base values
+
scoped parameter modifiers
↓
effective values
```

默认 neutral value：

```text
LocomotionScale    = 1
AirLocomotionScale = 1
GravityScale       = 1
```

长期角色能力 / Mode tuning 不伪装成永久 Policy Modifier。

例如：

```text
BaseSpeed / BaseAirControlFactor / RotateSpeed
→ Locomotion tuning

BaseGravityScale（若角色确实需要）
→ Translation / Ballistic tuning

临时 gameplay constraint
→ MotionPolicy
```

---

## 2.15 MotionPolicy 当前字段与合成规则

第一版保留：

```text
LocomotionScale
range 0..1
neutral = 1
combine = Min

AirLocomotionScale
range 0..1
neutral = 1
combine = Min

GravityScale
range >= 0
neutral = 1
combine = Multiply
```

### LocomotionScale

表示当前 Gameplay 对普通 locomotion contribution 的最大允许比例，不是角色移动速度 buff / debuff 系统。

Grounded locomotion：

```text
LocomotionVelocity
× EffectiveLocomotionScale
```

### AirLocomotionScale

不是 `BaseAirControlFactor`。

```text
BaseAirControlFactor
= 角色 / LocomotionMode 本身的空中控制能力

AirLocomotionScale
= 当前 Gameplay 对空中 locomotion 的临时许可 / 约束
```

Airborne locomotion：

```text
DesiredVelocity
× BaseAirControlFactor
× min(EffectiveLocomotionScale, EffectiveAirLocomotionScale)
```

### GravityScale

只影响 Ballistic gravity evolution：

```text
BallisticVerticalVelocity
+= PhysicsGravityY * EffectiveGravityScale * dt
```

不影响 `VerticalVelocityOwner`。

当 VerticalVelocityOwner active 时，Ballistic 本来就被冻结，因此 GravityScale 此时不会推进 Ballistic。

Policy 不支持负 GravityScale；反向重力若未来需要，应作为显式 motion producer。

---

## 2.16 MotionPolicy ownership：参数级独立 owner

MotionPolicy 不使用“一份完整 Policy Modifier = 一个 owner”的模型。

每个参数独立 ownership、独立合成：

```text
LocomotionScale owners
AirLocomotionScale owners
GravityScale owners
```

一个 authoring Clip 可以同时配置多个字段，但 runtime ownership 仍是参数级独立 token。

例如：

```text
MotionPolicyClip
├→ LocomotionScale token
├→ AirLocomotionScale token
└→ GravityScale token
```

不同参数不得因为来自同一个 Clip 就被绑定为一整个 Policy owner。

---

## 2.17 MotionPolicy 配置入口

ActionAsset 本身不配置 MotionPolicy。

MotionPolicy 属于 Sequence 时间轴上的局部行为。

当前配置入口固定为：

```text
MotionPolicyClip
├─ optional LocomotionScale
├─ optional AirLocomotionScale
└─ optional GravityScale

ImpulseClip
└─ optional GravityScale

RootMotionClip
└─ no MotionPolicy fields

SelfRotationClip
└─ no MotionPolicy fields

VelocityClip
└─ no MotionPolicy fields
```

原则：

> 专用 `MotionPolicyClip` 可以表达纯 Policy 时间窗；其他 Clip 只暴露与自己行为存在明确直接关系的 Policy 参数，绝不统一嵌入完整 `MotionPolicyConfig`。

`ImpulseClip.GravityScale` 的理由是：Impulse 可以定义一段自由弹道的初始速度，同时允许定义该弹道后续 Gravity 演化比例。

Root Motion / Velocity Owner / Rotation 已有自己的 fixed arbitration，不用通过 LocomotionScale 等重复表达覆盖关系。

---

## 2.18 ActorAnimation：Actor 级 Animation Authority

长期名称使用 `ActorAnimation`，不再使用含义模糊的 `ActorAnimationRuntime` 作为架构一级概念。

`ActorAnimation` 是 Actor prefab 上的 Unity Component，也是 Actor 唯一的 Animancer authority：

```text
ActorAnimation
├─ Locomotion Base
├─ Action Override
└─ Animancer
```

外部系统不得直接操作 Animancer Graph / State / Evaluate。

高层规则固定为：

```text
Locomotion
→ persistent Base
→ 即使 Action Override active，也持续更新

Action
→ temporary Override
→ 有有效 Action Pose 时覆盖 Locomotion
→ 无有效 Action Pose 时露出当前 Locomotion
```

因此 Action 结束时不需要重新启动 Locomotion animation。

`ActorAnimation` 不理解：

```text
Attack
Dodge
Hit
Combo
LocomotionMode entry condition
```

它只管理 Animation state / blending / Animancer execution。

---

## 2.19 Action Animation ownership 与 PoseClip

Action animation ownership 属于 Action playback 生命周期，而不是 `AnimationPoseClip`。

Action 开始时取得一个轻量 Action owner token；Action Complete / Cancel 时释放。

该 token 只用于 ownership protection：旧 Action 的迟到 cleanup / submit 不得影响已经开始的新 Action。

`AnimationPoseClip` 只负责表达当前 Sequence Tick 的 Action Pose：

```text
animation key
+ Sequence-authoritative sampleTime
+ 必要 mixer parameter
```

`sampleTime` 的含义：

```text
当前动画应被精确采样到第几秒
```

Action animation 的时间模型固定为：

```text
Sequence fixed frame
→ sampleTime
→ Action Pose
```

Animancer 不自行推进 Action animation 时间。

PoseClip 不负责：

```text
Action ownership
Animancer.Play / Stop authority
Graph lifecycle
Evaluate
Action channel lifecycle
```

---

## 2.20 Action Override 的确定性规则

第一版明确保持简单，不建立 Animation priority stack / generic channel framework。

规则：

```text
同一个 Actor、同一个 Tick
→ 最多一个有效 Action PoseClip
```

PoseClip overlap 视为 authoring error，不做 priority / stack 仲裁。

Action Session / lifecycle 中出现 PoseClip 空档：

```text
没有 Action Pose 提交
→ Action Override 不贡献 Pose
→ 露出当前 Locomotion Base
```

不自动 Hold Last Pose。

Action A -> Action B 同 Tick交接：

```text
A Action Pose
→ direct crossfade
→ B Action Pose
```

不得强制经过 Locomotion 作为中间态，也不销毁重建一个通用 Animation stack。

---

## 2.21 Animation 时间与 Evaluate

两个时间模型明确分开：

```text
Action Animation
→ Sequence sampleTime 权威

Locomotion Animation
→ Combat simulation dt 连续推进
```

HitStop 冻结 gameplay / action / locomotion animation time；不得让 Locomotion 继续使用普通 Unity frame time 偷偷推进。

每个 Actor 每个 Combat Tick 由 `ActorAnimation` 统一 `Evaluate()` 一次。

PoseClip、ActorLocomotion 等 Producer 只更新 animation state / pose data，不自行 `Evaluate()`。

固定顺序：

```text
ALL Action / Sequence
→ ALL ActorLocomotion animation updates
→ ALL ActorAnimation Evaluate
→ ALL Motion / KCC
→ Physics Sync（需要时）
→ ALL HitDetection
→ HitResolution
```

这样当前 Tick 的 Skeleton Pose 在 HitDetection 前已确定，同时保留“所有 Movement 完成后再 HitDetection”的全局 barrier。

---

## 2.22 ActorLocomotion：Locomotion Gameplay Domain

原 `LocomotionController` 概念正式升格 / 改名为：

```text
ActorLocomotion
```

它是 Actor prefab 上的 Unity Component，并代表稳定的 Locomotion Gameplay Domain，而不是一个临时中转 Controller。

Actor 级核心职责分为：

```text
ActorLocomotion
= 当前采用哪套 locomotion behavior / profile

ActorMotor
= Actor 怎么实际移动

ActorAnimation
= Actor 最终怎么表现动画
```

三者保持独立，不合并成 God Component。

`ActorLocomotion` 拥有：

```text
可用 LocomotionMode / Profile 配置
CurrentMode
Mode selection
Mode lifecycle / Tags
```

它输出：

```text
LocomotionTuning
→ ActorMotor

LocomotionAnimationProfile
→ ActorAnimation
```

它不拦截 Player / AI 的 `LocomotionIntent`。

---

## 2.23 LocomotionModeAsset / Profile

`LocomotionModeAsset` 是纯 ScriptableObject 配置，不包含 runtime 行为。

第一版字段固定为：

```text
Priority
EntryConditions
SelfTags
LocomotionTuning
LocomotionAnimationProfile
```

其中 `LocomotionTuning` 至少包含：

```text
MoveSpeed
AirControlFactor
RotateSpeed
```

长期原则：

> Mode 是一套 Locomotion Profile，不是每一个动画状态。

合理示例：

```text
Normal
LockOn
Air
```

不应机械建立：

```text
IdleMode
RunMode
WalkMode
```

Idle / Run / Strafe 等通常属于当前 Profile 内的 locomotion animation / behavior 表现。

`LocomotionModeAsset` 不实现 `OnEnter / OnExit`；生命周期统一由 `ActorLocomotion` 管理。

---

## 2.24 LocomotionMode selection / lifecycle

每个 Combat Tick，`ActorLocomotion` 从满足 `EntryConditions` 的候选中选择 Mode。

确定性规则：

```text
满足条件的 Mode
→ Priority 高者胜

同 Priority
→ CurrentMode 仍合法时保持 CurrentMode

CurrentMode 已失效，多个候选同 Priority
→ serialized authored order 决定

没有普通候选
→ explicit fallbackMode
```

不建立：

```text
Mode Stack
Transition Graph
Command Queue
Generic Gameplay StateSystem
```

`fallbackMode` 是显式配置，不依赖 `Priority = -999 + AlwaysTrue` 等隐式约定。

Fallback 不参与普通 Priority 竞争，用于保证正常运行期间 `CurrentMode` 始终存在。

Mode 切换语义：

```text
Old Mode Tags release
→ CurrentMode 切换
→ New Mode Tags acquire
→ 更新 ActorMotor LocomotionTuning
→ 更新 ActorAnimation LocomotionAnimationProfile
```

对 Gameplay 观察应表现为一次原子 Mode 切换，不暴露“旧 Tag 已释放、新 Tag 尚未建立”的中间逻辑状态。

---

# 3. Superseded / Rejected Decisions

以下方向不得继续作为 E3 设计依据。

## 3.1 Input / Locomotion

```text
ActorLogicInput 作为 runtime input authority
LocomotionController 作为 Intent -> Motor 必经中转
Player / AI 必须走统一 Input Provider
```

均已取代。

---

## 3.2 通用 Gameplay StateSystem / 整体 ownership transfer

不得把以下当作已批准架构：

```text
StateKind.Action / StateKind.Locomotion 已冻结
通用 Gameplay StateSystem
Action 进入时整体夺取 Animation / Movement / HitBox / Rotation 控制权
```

---

## 3.3 旧 ActorMotor Runtime 划分

以下当前代码结构不再代表长期架构边界：

```text
ActorMotor
├─ LocomotionRuntime
├─ FacingRuntime
└─ ActorMotionRuntime
```

目标改为：

```text
ActorMotor
├─ LocomotionRunner
├─ Translation Domain
├─ Rotation Domain
└─ Supporting State
```

---

## 3.4 Animator Root Motion

以下不属于长期 Gameplay movement：

```text
OnAnimatorMove
Animator.deltaPosition / deltaRotation
ActorRootMotionRelay
RootMotionApplyMode.Managed 作为正式运动来源
```

---

## 3.5 Channel submission 阻断

明确否决：

```text
RootMotion Begin -> Clear HorizontalImpulse
RootMotion active -> Reject HorizontalImpulse
VelocityOwner active -> Reject lower channel submission
```

Submission 与 Compose 必须分离。

---

## 3.6 垂直双状态

不再使用：

```text
GravityAccumulator + VerticalImpulseVelocity
```

目标是 `BallisticVerticalVelocity`。

---

## 3.7 Grounded 自动结束 VerticalVelocityOwner

明确否决：

```text
Grounded / Landed
→ ActorMotor 自动 EndVerticalVelocityOwner
```

Grounding 不接管外部 owner 生命周期。

---

## 3.8 Facing 作为 Motor 总 Rotation 模型

`Facing` 只保留为 locomotion/base facing 语义，不再代表 ActorMotor 的总旋转系统。

总 Domain 名称固定为：

```text
Rotation
├─ Locomotion Rotation
├─ Root Rotation
└─ Scripted Rotation
```

---

## 3.9 通用 MotionPolicyModifier / Action-level Policy

以下方向已被后续讨论取代：

```text
ActionAsset 默认持有一整组 MotionPolicy contribution
一个来源 = 一个完整 MotionPolicyModifier owner
所有 Motion Clip 都暴露完整 MotionPolicyConfig
新建 MotionPolicyRuntime 作为一级 Runtime
```

当前基线是：

```text
Sequence Clip authoring
+
Policy parameter-level ownership
+
MotionPolicy 只是 ActorMotor supporting state
```

---

## 3.10 ActorAnimationRuntime / Session API 扩张

以下不作为长期一级架构概念：

```text
ActorAnimationRuntime 作为模糊 Runtime
通用 Animation Layer Framework
Animation Contribution Stack
PoseClip 自己 owning Action Override lifecycle
PoseClip 自己 Evaluate
BeginSession / SetPose / ClearPose 等大量流程 API 作为框架中心
```

当前基线是更简单的：

```text
ActorAnimation
├─ Locomotion Base
└─ Action Override
```

Action 使用轻量 owner token；PoseClip 只提交当前 Pose 数据；ActorAnimation 统一 Resolve / Blend / Evaluate。

---

## 3.11 LocomotionController 作为薄中间层

`LocomotionController` 名称与定位被 `ActorLocomotion` 取代。

不得重新退回：

```text
LocomotionController
= 只做几次转发的薄中间层
```

当前定位：

```text
ActorLocomotion
= Locomotion Gameplay Domain
= Profiles / CurrentMode / Selection / Tags owner
```

也不得为了“组件数量少”把 `ActorLocomotion + ActorAnimation` 或 `ActorLocomotion + ActorMotor` 合并成一个 God Component。

---

# 4. Open Questions

核心架构问题已经基本收口。剩余内容主要属于实现细节，不应重新开放上述 Domain / ownership 原则。

## 4.1 Animation 实现细节

```text
ActorAnimation 最小 public API 的最终方法签名
Action owner token 的具体 struct / generation 实现
LocomotionAnimationProfile 的具体 Animancer 数据结构
Action A -> B crossfade 参数具体从哪里配置
PoseClip mixer parameter 的最终数据表示
```

---

## 4.2 ActorLocomotion 实现细节

```text
LocomotionAnimationProfile 的具体字段
EntryConditions 的现有 Condition API 如何复用
SelfTags acquire / release 使用现有哪套 token API
fallbackMode 的 Inspector validation
serialized authored order 的稳定实现
```

---

## 4.3 ActorMotor / MotionPolicy 实现层收尾

```text
MotionPolicy 内部 token / container 具体类型
Effective Policy 在 modifier 变化时还是固定 Tick 求值
MotionPolicyClip authoring 的 optional field Inspector 表达
旧 LocomotionRuntime / FacingRuntime / ActorMotionRuntime 的迁移顺序
旧 SelfRotationBuffer 向 Root / Scripted Rotation owner stack 的迁移
```

---

# 5. 当前目标架构快照

Actor 级核心 Domain：

```text
Actor
├─ ActorLocomotion
│    = Locomotion Gameplay Domain
│    = Profiles / CurrentMode / Selection / Tags
│
├─ ActorMotor
│    = Movement Authority / KCC Adapter
│    ├─ LocomotionRunner
│    ├─ Translation
│    ├─ Rotation
│    └─ MotionPolicy
│
└─ ActorAnimation
     = Animation Authority
     ├─ Locomotion Base
     └─ Action Override
```

数据流：

```text
Player / AI
    │
    ▼
LocomotionIntent ───────────────────────→ ActorMotor
                                             ▲
                                             │ tuning
                                      ActorLocomotion
                                       │           │
                                       │           └→ AnimationProfile
                                       │                    │
                                       └────────────────────▼
                                                     ActorAnimation
                                                          ▲
                                                          │ Action Pose
                                                   Action / Sequence
```

Translation：

```text
HORIZONTAL

HorizontalVelocityOwner
>
Root Motion
>
Locomotion + HorizontalImpulse
```

```text
VERTICAL

VerticalVelocityOwner exists
    ? TopOwnerVelocity
    : BallisticVerticalVelocity
```

Rotation：

```text
Scripted Rotation
>
Root Rotation
>
Locomotion Rotation
```

MotionPolicy：

```text
LocomotionScale       Min
AirLocomotionScale    Min
GravityScale          Multiply

neutral = 1
parameter-level ownership
```

Animation：

```text
Locomotion Base
→ persistent / simulation dt

Action Override
→ Sequence sampleTime
→ max one PoseClip per Tick
→ no Pose = reveal Locomotion
```

---

# 6. 后续使用规则

1. 本文是 E3 正式实现前的讨论 checkpoint，不直接替代 `Final_Architecture_v2`。
2. 当旧架构文档与本文明确冲突时，以本文 Confirmed / Superseded 内容作为后续 E3 基线。
3. Translation / Vertical / Rotation / MotionPolicy / ActorAnimation / ActorLocomotion 的核心语义已经冻结；除非出现真实玩法需求或明确矛盾，不重新开放。
4. 下一步将本文整理进 `Final Architecture v3`，再据此制定 E3 Implementation Plan。
5. 实现过程中如果发现具体 API / 数据结构问题，优先在既有 Domain 边界内解决，不以实现困难为由重新引入 Generic Runtime / Callback Scheduler / God Component。
