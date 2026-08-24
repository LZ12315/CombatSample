# CombatSample Final Architecture v3

> 状态：Review Draft；已完成第一轮 Architecture Review 与 Driver 7-Phase Model 修订，仍不是 E3 Implementation Plan。
>
> 日期：2026-08-25
>
> 主要输入：[`CombatSample_E3_PreDesign_Checkpoint_zh-CN.md`](CombatSample_E3_PreDesign_Checkpoint_zh-CN.md)、[`CombatSample_Final_v3_Source_Audit_zh-CN.md`](CombatSample_Final_v3_Source_Audit_zh-CN.md)。
>
> 历史来源：[`CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md`](CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md) 与 [`../CombatSample_RootMotion_Final_Design_v1.md`](../CombatSample_RootMotion_Final_Design_v1.md) 中仍被 Source Audit 明确保留的部分。
>
> 本 Draft 获得确认前，E3 Pre-Design Checkpoint 仍是 E3 冲突项的最高设计依据；确认后，本文将成为长期架构基线。

---

# 1. 架构总原则与 Authority

CombatSample 的核心原则是：

> **统一生命周期，分离控制通道。**

Action / ActionSequence 可以统一一招行为的时间和生命周期，但不拥有整个 Actor 的 Animation、Movement、Rotation、HitBox、Tags 等全部控制权。

```text
ActionInstance / ActionSequence lifecycle
        │
        ├── Animation contribution
        ├── Translation contribution
        ├── Rotation contribution
        ├── MotionPolicy contribution
        ├── HitBox window
        ├── Tags
        └── other scoped contributions
```

各领域拥有自己的 authority 与固定仲裁：

```text
Input authority              → PlayerInputController / AI producer
Locomotion profile authority → ActorLocomotion
Action arbitration authority → ActionStateManager
Action playback authority    → ActionPlayer
Action time authority        → ActionSequence
Movement authority           → ActorMotor
Animation authority          → ActorAnimation
Authored root data authority → RootMotionTrajectory
World movement result        → KCC + ActorCollisionResolver
Hit query state              → ActorHitBoxRuntime
Hit resolution               → CombatHitBuffer
World execution order        → CombatSimulationDriver
```

`ActionStateManager / ActionPlayer / ActionSequence` 的 authority 只覆盖 Action 的选择、播放生命周期与 fixed-frame 内容时间，不因此获得 Animation / Movement / Rotation / HitBox 的全局 authority。

不得重新建立：

```text
StateKind.Action / StateKind.Locomotion 作为 whole-actor 互斥 ownership
通用 Gameplay StateSystem
通用 phase callback scheduler
一个 God Component 同时拥有 locomotion / movement / animation
```

---

# 2. Actor 级目标结构

Actor 在 locomotion / movement / animation 侧的三个核心 Gameplay / Runtime Domain 固定为：

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
│    └─ Supporting State
│
└─ ActorAnimation
     = Animation Authority
     ├─ Locomotion Base
     └─ Action Override
```

Action Domain 继续由已有职责组成，不塞进上述三个 Domain：

```text
ActionStateManager
= Action candidate collection + fixed-tick arbitration

ActionPlayer
= CurrentAction + Begin / Stop + playback lifecycle

ActionSequence
= fixed-frame content time + Clip lifecycle
```

`ActorSimulationRuntime` 继续作为一个 Actor 参加 Combat fixed simulation 的唯一入口，但它不是新的 Gameplay Domain。它只把 Driver 的固定阶段转发到该 Actor 已有的 Action、Locomotion、Animation、Motor 与 HitBox 子系统。

核心数据流：

```text
Player / AI
    │
    ▼
LocomotionIntent ───────────────────────→ ActorMotor
                                             ▲
                                             │ LocomotionTuning
                                      ActorLocomotion
                                       │           │
                                       │           └→ LocomotionAnimationProfile
                                       │                         │
                                       └─────────────────────────▼
                                                        ActorAnimation
                                                             ▲
                                                             │ Action Pose
                                                      Action / Sequence

ActionStateManager
→ ActionPlayer
→ Action / Sequence
   ├→ Animation contribution ──────────→ ActorAnimation
   ├→ Translation contributions ──────→ ActorMotor
   ├→ Rotation contributions ─────────→ ActorMotor
   ├→ MotionPolicy contributions ─────→ ActorMotor
   └→ HitBox windows ─────────────────→ ActorHitBoxRuntime
```

---

# 3. Input 与 LocomotionIntent

## 3.1 Player

玩家链路固定为：

```text
Input System
→ PlayerInputController
→ PlayerLocomotionIntentResolver
→ LocomotionIntent
→ ActorMotor
```

职责：

```text
PlayerInputController
= raw input + input history owner

PlayerLocomotionIntentResolver
= raw move + Camera/Lock context → LocomotionIntent
```

`PlayerInputController` 负责真实玩家输入和输入历史；`PlayerLocomotionIntentResolver` 只负责解释玩家输入，不成为通用 Actor input abstraction。

## 3.2 AI

AI 不模拟 joystick / button：

```text
AI / BehaviorTree
→ LocomotionIntent
→ ActorMotor
```

Player 与 AI 只在 Intent 生产侧不同，ActorMotor 不知道 Intent 来源。

## 3.3 固定约束

- `ActorLogicInput` 不再是 runtime authority。
- `ActorLocomotion` 不是 Intent 到 Motor 的必经中转层。
- 不建立通用 `InputProvider`、`CommandBus`、`IntentSourceManager`。
- raw input / input history 使用输入自己的时间语义；HitStop 冻结 Combat simulation time，不要求停止真实输入采集。
- Combat Tick 内必须先生产本 Tick Intent，再由后续 Action / Locomotion / Motor 阶段消费本 Tick 状态。

---

# 4. ActorLocomotion

## 4.1 定位

`ActorLocomotion` 是 Actor prefab 上的 Unity Component，代表稳定的 **Locomotion Gameplay Domain**。

它回答：

> 当前 Actor 采用哪套 locomotion behavior / profile？

它不回答：

```text
输入来自谁
最终速度是多少
最终 Rotation 是多少
Animancer Graph 怎么执行
Action 是否拥有整个角色
```

## 4.2 LocomotionModeAsset

`LocomotionModeAsset` 是纯 ScriptableObject 配置，不包含 runtime 行为。

第一版字段：

```text
Priority
EntryConditions
SelfTags
LocomotionTuning
LocomotionAnimationProfile
```

`LocomotionTuning` 至少包含：

```text
MoveSpeed
AirControlFactor
RotateSpeed
```

长期原则：

> **Mode 是一套 Locomotion Profile，不是每一个动画状态。**

合理：

```text
Normal
LockOn
Air
```

通常不需要：

```text
IdleMode
RunMode
WalkMode
```

Idle / Run / Strafe 等应优先作为当前 Profile 内的 locomotion animation / behavior 表现。

## 4.3 Mode selection

每个 Combat Tick：

```text
满足 EntryConditions 的 Mode
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

`fallbackMode` 为显式配置，不使用 `Priority = -999 + AlwaysTrue` 之类隐式约定。

## 4.4 Mode lifecycle

只有真实 Mode 切换才发生生命周期：

```text
Old Mode Tags release
→ CurrentMode = New Mode
→ New Mode Tags acquire
→ ActorMotor 使用新的 LocomotionTuning
→ ActorAnimation 使用新的 LocomotionAnimationProfile
```

Gameplay 观察上这是一次原子切换，不暴露旧 Tags 已释放、新 Tags 尚未建立的中间状态。

`LocomotionModeAsset` 不实现 `OnEnter / OnExit`；生命周期统一由 `ActorLocomotion` 管理。

## 4.5 与 Action 的同 Tick 依赖方向

Driver 固定先完成本 Tick `ActorLocomotion` Mode selection，再进入 Action arbitration。

因此 Mode selection 可以读取 Tick 开始时已经存在的稳定 Actor 状态 / Tags，但不得依赖“本 Tick 新 Action 开始后才产生”的 side effect。

固定依赖方向：

```text
Stable Actor State + Intent
→ ActorLocomotion Mode selection
→ Action arbitration may read CurrentMode / mode tags
```

Action 不通过整体切换 LocomotionMode 来夺取 movement control。Action 对本 Tick movement 的临时影响继续使用：

```text
MotionPolicy
Translation contribution
Rotation contribution
```

这样避免形成：

```text
LocomotionMode selection
↔ same-tick Action side effects
```

的循环依赖。

---

# 5. ActorMotor

## 5.1 Authority

`ActorMotor` 是：

```text
Actor movement authority
+
KCC Adapter
```

它的两个 first-class Domain 固定为：

```text
Translation
Rotation
```

Grounding、Velocity Readout、MotionPolicy 是 Supporting State，不与 Translation / Rotation 同级扩张成新的 Gameplay Domain。

目标结构：

```text
ActorMotor
├─ LocomotionRunner
├─ Translation Domain
│    ├─ Locomotion
│    ├─ Root Motion
│    ├─ Horizontal Impulse
│    ├─ Horizontal Velocity Owner
│    ├─ BallisticVerticalVelocity
│    └─ Vertical Velocity Owner
├─ Rotation Domain
│    ├─ Locomotion Rotation
│    ├─ Root Rotation
│    └─ Scripted Rotation
└─ Supporting State
     ├─ Grounding
     ├─ Velocity Readout
     └─ MotionPolicy
```

现有 `LocomotionRuntime / FacingRuntime / ActorMotionRuntime / SelfRotationBuffer` 只是迁移前代码结构，不是 v3 长期一级概念。

## 5.2 Producer 与 Motor 的边界

固定原则：

> Producer 解析 Gameplay 语义；Motor 只接收已经解析好的物理量并做状态维护、仲裁与 Compose。

因此 Motor 不理解：

```text
Attack
LockOn
Target
Direction
Snap
RotateBySpeed
```

例如 Target / Direction / Snap / RotateBySpeed 必须在 Scripted Rotation producer 侧解析为本 Tick local yaw delta 后，再提交给 Motor。

---

# 6. LocomotionRunner

`LocomotionRunner` 是 ActorMotor 内部的 locomotion interpreter / producer，不是独立 movement authority。

```text
LocomotionIntent
+
LocomotionTuning
+
Grounded / Airborne
+
Current Rotation
+
Effective MotionPolicy
        ↓
LocomotionRunner
        ├→ Locomotion Translation: world planar velocity
        └→ Locomotion Rotation: local yaw delta
        ↓
ActorMotor arbitration
```

`MoveSpeed / AirControlFactor / RotateSpeed` 属于 LocomotionTuning。

MotionPolicy 只表达临时 Gameplay permission / constraint，不保存角色基础 locomotion 能力。

---

# 7. Translation Domain

## 7.1 Submission 与 Compose 分离

固定原则：

> Producer 独立 Submit；每个 Channel 独立维护状态；只有最终 Compose 决定当前实际 contribution。

高优先级 Channel active 时，不得阻止低优先级 Channel：

- 接受新的提交；
- 更新自己的缓存 / owner state；
- 推进自己的生命周期。

被覆盖的 contribution 不积累 missed delta，也不在恢复后 catch-up。

## 7.2 数据单位与坐标空间合同

Translation Channel 不允许把 `delta / velocity / local / world` 隐式混用。

第一版固定合同：

```text
Locomotion
→ world planar velocity
→ m/s

HorizontalImpulse
→ world planar velocity state
→ m/s

HorizontalVelocityOwner
→ world planar velocity
→ m/s

Root Motion
→ local planar displacement over current Sequence interval
→ meters

BallisticVerticalVelocity
→ scalar velocity along CharacterUp
→ m/s

VerticalVelocityOwner
→ scalar velocity along CharacterUp
→ m/s
```

其中 Root Motion 是唯一以“区间 displacement”提交的水平 authored source。它在 ActorMotor Compose boundary 转换为 KCC 请求速度：

```text
worldDelta
= TickStartRotation * RootMotionLocalDelta

rootMotionVelocity
= worldDelta / KccDeltaTime
```

`RootMotionLocalDelta` 已经是当前 `Extract(t0,t1)` 得到的当前区间数据，因此 time scale / playback speed 通过“查询了哪个时间区间”表达，不再对 displacement 额外乘一次速度倍率。

Motor 对 Translation 的最终输出统一为 KCC 可消费的 world-space `RequestedVelocity`。

## 7.3 水平 Translation

来源：

```text
Locomotion
HorizontalImpulse
Root Motion
HorizontalVelocityOwner
```

固定仲裁：

```text
if HorizontalVelocityOwner exists
    Horizontal = TopHorizontalVelocityOwner
else if RootMotion exists
    Horizontal = RootMotionVelocity(TopRootMotion)
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
A Begin → A
B Begin → B
B End   → A
```

非栈顶 owner 可以继续更新自己的当前值。

### Root Motion

Root Motion 同样使用可恢复 LIFO stack。

```text
A ---------------------
      B -------

output:
A A A | B B B | A A A
```

A 被覆盖期间 Sequence 时间与 A 的当前 Tick submission 可以继续推进；这些被覆盖 delta 直接丢弃。B 结束后，A 从当前时间 / 当前 Tick 恢复，不补过去位移。

### HorizontalImpulse

HorizontalImpulse 是独立 additive / decaying state。

它只在最终落到：

```text
Locomotion + HorizontalImpulse
```

这一分支时参与输出。

Root Motion / HorizontalVelocityOwner 覆盖期间，它仍可以接收提交并按自己的规则衰减。

不得：

```text
RootMotion Begin → Clear Impulse
RootMotion active → Reject Impulse
RootMotion active → Add HorizontalImpulse into final RootMotion branch
VelocityOwner active → Reject lower submissions
```

## 7.4 垂直 Translation

自由弹道统一为：

```text
BallisticVerticalVelocity
```

不再长期保留：

```text
GravityAccumulator + VerticalImpulseVelocity
```

Gravity 是 Ballistic 的持续演化：

```text
BallisticVerticalVelocity
+= PhysicsGravityY * EffectiveGravityScale * simulationDt
```

Jump / DoubleJump / Launcher / Hit / Impulse 等 Gameplay producer 只通过事件式操作修改 Ballistic。

第一版至少支持：

```text
Add: velocity += value
Set: velocity = value
```

Ballistic 本身不认识 Jump / Hit / Launcher 等 Gameplay 标签。

### VerticalVelocityOwner

最终：

```text
if VerticalVelocityOwner exists
    Vertical = TopOwnerVelocity
else
    Vertical = BallisticVerticalVelocity
```

VerticalVelocityOwner 使用可恢复 LIFO stack；非栈顶 owner 可以继续更新。

只要 owner stack 非空：

```text
BallisticVerticalVelocity freeze
Gravity 不继续积分
```

owner count 从 `> 0` 变为 `0` 时：

```text
BallisticVerticalVelocity = 0
```

默认：

```text
不恢复接管前 Ballistic velocity
不继承最后一个 owner velocity
```

只有出现真实玩法需求时，才增加新的 finish / handoff policy。

## 7.5 Grounded

稳定接地时：

```text
Final Vertical = 0
BallisticVerticalVelocity = 0
Grounded 期间不积累 Gravity
```

Grounding 不拥有 VerticalVelocityOwner 生命周期，因此 Grounded 不得自动：

```text
End owner
Clear owner velocity
Reject owner submission
```

有效向上的 Ballistic / owner write 在接地状态下需要明确的 ForceUnground 类机制。

---

# 8. Rotation Domain

来源固定为：

```text
Locomotion Rotation
Root Rotation
Scripted Rotation
```

优先级：

```text
Scripted Rotation
>
Root Rotation
>
Locomotion Rotation
```

三个 Channel 的统一 Motor 数据合同都是：

```text
this tick's local yaw delta
```

最终：

```text
RequestedRotation
= TickStartRotation * WinningLocalYawDelta
```

不同 Rotation channel 不叠加。

## 8.1 Locomotion Rotation

`LocomotionRunner` 根据 facing intent、Current Rotation 与 RotateSpeed 等 tuning 计算 local yaw delta。

## 8.2 Root Rotation

Root Rotation 来自 `RootMotionTrajectory` 的 baked Yaw，并使用独立的可恢复 LIFO owner stack。

被 Scripted Rotation 覆盖期间，Root Rotation 可以继续计算 / 提交当前 Tick delta，但不积累 missed yaw。

## 8.3 Scripted Rotation

Scripted Rotation producer 在 Motor 外解析：

```text
Target
Direction
Snap
RotateBySpeed
```

然后只向 Motor 提交 local yaw delta。

Scripted Rotation 使用自己的可恢复 LIFO owner stack。

Root Rotation 与 Scripted Rotation 不使用一个全局“最后 Begin 胜出”的 stack；固定 precedence 始终是：

```text
Scripted stack nonempty
→ Scripted top
else Root Rotation stack nonempty
→ Root top
else
→ Locomotion Rotation
```

现有 `SelfRotationClip` 的长期语义应拆解为：

```text
RootRotation source
→ Root Rotation

Target / Direction source
→ Scripted Rotation producer

Snap / RotateBySpeed
→ producer-side resolution mode
```

`Facing` 只保留 locomotion/base facing 语义，不再是 ActorMotor 总 Rotation 模型。

---

# 9. MotionPolicy

## 9.1 定位

`MotionPolicy` 是 ActorMotor supporting state，不创建 `MotionPolicyRuntime` 一级框架。

它不负责：

```text
Tick scheduler
Gameplay decision
Intent interpretation
Translation / Rotation arbitration
Action knowledge
```

它只做：

```text
neutral values
+
scoped parameter modifiers
→ effective values
```

## 9.2 第一版参数

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

Grounded locomotion：

```text
LocomotionVelocity
× EffectiveLocomotionScale
```

Airborne locomotion：

```text
DesiredVelocity
× BaseAirControlFactor
× min(EffectiveLocomotionScale, EffectiveAirLocomotionScale)
```

Gravity：

```text
BallisticVerticalVelocity
+= PhysicsGravityY * EffectiveGravityScale * simulationDt
```

`GravityScale` 不影响 VerticalVelocityOwner。

MotionPolicy 不负责 Rotation / Facing。

## 9.3 Ownership

Policy 使用 **参数级独立 ownership**：

```text
LocomotionScale owners
AirLocomotionScale owners
GravityScale owners
```

一个 authoring Clip 可以配置多个字段，但 runtime 取得的是各参数自己的 token；不建立“一整个 MotionPolicyModifier = 一个 owner”。

## 9.4 Authoring

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

ActionAsset 本身不配置整招 MotionPolicy。

`ImpulseClip.GravityScale` 的 token 生命周期第一版固定等于该 Clip 的 active interval：

```text
Clip Enter
→ acquire GravityScale token

Clip active
→ token contributes

Clip Exit / Cancel
→ release token
```

`ImpulseClip` 不因为一次 Ballistic 写入就自动拥有后续整个 ballistic lifecycle。未来如果出现“离开 Clip 后仍持续特殊重力”的真实玩法，应使用显式的独立 Policy window，而不是偷偷延长 ImpulseClip ownership。

---

# 10. Grounding、Requested 与 Actual

KCC 是碰撞允许后的实际世界运动事实来源。

Motor 输出：

```text
RequestedVelocity
RequestedRotation
```

KCC + ActorCollisionResolver 输出：

```text
Actual World Position / Rotation
Actual solved movement
```

因此：

> **Requested Motion != Actual Motion。**

例如 authored motion 请求前进 1m，而碰撞只允许 0.6m：

```text
0.6m = actual
0.4m = discarded
```

被挡住的位移不积欠、不在下一 Tick 补偿，也不在 Action 结束后偿还。

`VelocityReadout` 应表达 world solve 后的 Gameplay movement 结果，而不是把通道合成出的 request velocity 冒充为实际速度。具体 publish API 与内部缓存形式属于 implementation detail。

---

# 11. ActorAnimation

## 11.1 Authority

`ActorAnimation` 是 Actor prefab 上的 Unity Component，也是 Actor 唯一 Animancer authority。

```text
ActorAnimation
├─ Locomotion Base
├─ Action Override
└─ Animancer
```

外部系统不得直接操作 Animancer Graph / State / Evaluate。

`ActorAnimation` 只管理 animation state、blend 与 evaluate，不理解 Attack / Dodge / Combo / LocomotionMode EntryCondition 等 Gameplay 语义。

## 11.2 Locomotion Base

Locomotion animation 是 persistent Base：

```text
ActorLocomotion
→ LocomotionAnimationProfile
→ ActorAnimation Locomotion Base
```

Action Override active 时，Locomotion 仍继续按 Combat simulation time 更新自己的最新状态。

Action 结束不需要 `RestartLocomotion()`。

## 11.3 Action Override

Action animation 是 Temporary Override。

Action playback lifecycle 开始时取得一个轻量 Action owner token；Complete / Cancel 时释放。

Token 只解决 ownership protection：旧 Action 的迟到 submit / cleanup 不得影响已经开始的新 Action。

不把 token 扩张成通用 Animation Session framework。

## 11.4 Action Pose

`AnimationPoseClip` 只提交当前 Action Pose 数据：

```text
animation key
sampleTime
necessary mixer parameter
```

`sampleTime` 是 Sequence-authoritative 的精确动画采样秒数。

PoseClip 不拥有：

```text
Action lifecycle
Animancer authority
Evaluate
Graph lifecycle
```

第一版固定：

```text
同一个 Actor / 同一个 Tick
→ 最多一个有效 Action Pose
```

PoseClip overlap 是 authoring error，不建立 priority stack。

Action lifecycle 中本 Tick 没有有效 Pose submission：

```text
Action Override 不贡献 Pose
→ 露出当前 Locomotion Base
```

不自动 Hold Last Pose。

Action A → B 交接：

```text
A Pose
→ direct crossfade
→ B Pose
```

不强制经过 Locomotion 中间态。

## 11.5 时间、Blend 与 Evaluate

两个时间模型明确分开：

```text
Action Animation
→ Sequence sampleTime authoritative

Locomotion Animation
→ Combat simulation dt continuous
```

Animation blend / crossfade 的推进同样使用 Combat simulation dt，不使用 render-frame `Time.deltaTime` 作为权威时间。

HitStop 冻结：

```text
Action sample progression
Locomotion animation progression
Animation blend / crossfade progression
```

Locomotion 不得改用 Unity render frame time 偷偷推进。

每个 Actor 每个 Combat Tick 由 `ActorAnimation` 集中 `Evaluate()` 一次。

```text
PoseClip / ActorLocomotion
→ update / submit state only

ActorAnimation
→ resolve / blend
→ Evaluate once
```

---

# 12. Action / ActionSequence

## 12.1 单一 Action 模型与 Authority

长期 Action 模型继续保持：

```text
ActionAsset
└─ ActionSequenceData

ActionInstance
ActionStateManager
ActionPlayer
ActionSequence
```

职责边界：

```text
ActionStateManager
= fixed-tick Action candidate collection + arbitration

ActionPlayer
= CurrentAction + Begin / Stop + playback lifecycle

ActionInstance
= one playback instance + scoped Action lifecycle state

ActionSequence
= fixed-frame content time + Clip Enter / Tick / Exit
```

不发展：

```text
AnimAction
SequenceAction
DefaultAction
LegacyTimelineAction
```

等并行 Action 子类族。

Legacy Timeline 只作为迁移期兼容 backend，不是第二个长期玩法架构。

## 12.2 Action arbitration / playback

`ActionStateManager` 保留现有 fixed-tick 仲裁职责：

```text
Poll candidates
Event candidates
External requests
Cancel candidates
        ↓
fixed-tick candidate pool
        ↓
Action arbitration
        ↓
chosen Action
        ↓
ActionPlayer.BeginAction
```

Action arbitration 不即时绕过 Driver phase，也不因为 External / Event 来源不同而建立另一套播放路径。

`ActionPlayer` 负责 Action playback lifecycle，但不是 Animation authority，也不是 Movement authority。它不得因为“播放动作”就直接拥有 Animancer Graph 或整个 ActorMotor policy。

## 12.3 Sequence 职责

`ActionSequence` 是 Action Gameplay time authority，只解释 fixed frame 并驱动普通 Clip 生命周期：

```text
Enter
Tick
Exit
```

Sequence 不认识：

```text
KCC
Physics barrier
Hit Resolve
global phase scheduler
```

世界阶段由 CombatSimulationDriver 持有。

## 12.4 Contribution contract

Sequence Clip 应直接表达自己的局部 contribution：

```text
AnimationPoseClip
→ Action Pose → ActorAnimation

RootMotionClip
→ baked XZ → Root Motion channel

SelfRotationClip / rotation authoring
├→ RootRotation source → Root Rotation channel
└→ Target / Direction → Scripted Rotation producer

VelocityClip
→ HorizontalVelocityOwner and/or VerticalVelocityOwner

ImpulseClip
→ HorizontalImpulse and/or Ballistic operation
→ optional GravityScale token during Clip active interval

MotionPolicyClip
→ parameter-level MotionPolicy tokens

HitBoxClip
→ activate / deactivate ActorHitBoxRuntime window
```

Action / Sequence lifecycle 可以帮助这些 contribution 取得和释放 token，但不得把它们合并为一个“Action owns everything”的总 owner。

## 12.5 Action-level MotionConfig 的迁移边界

旧代码中的整招运动配置，例如：

```text
ActionAsset.ActionMotionConfig
ActionInstance.ApplyMotionConfig()
ActionInstance.RestoreMotionConfig()
```

属于 migration source，不是 v3 长期 Action contract。

长期不得在 Action Enter / Exit 通过一次整体操作做：

```text
ClearVelocityOwners
SetRootMotionApplyMode
SetLocomotionSuppressed
SetGravityScale / restore to 1
whole-action Facing ownership
```

这些语义分别迁移到：

```text
Locomotion suppression / permission
→ MotionPolicyClip

Authored horizontal movement
→ Root Motion / HorizontalVelocityOwner

Ballistic / impulse
→ ImpulseClip / Ballistic operation

Scripted facing / snap
→ Scripted Rotation producer

Root Rotation
→ Root Rotation channel
```

`ActionAsset.SelfTags` 仍可以作为整个 Action playback 生命周期的 scoped Tags；这与 whole-action movement ownership 是不同职责。

为保护已有 Unity serialized asset，旧 `ActionMotionConfig` 字段 / API 可以在迁移期间暂存，但只能作为 compatibility 数据源；新 Action 内容不得继续依赖它作为长期 authoring 入口。

## 12.6 Fixed frame

当前项目 Combat simulation 与 Gameplay Sequence 使用 60Hz policy。

60Hz 是项目 timestep policy；Driver 的根本身份仍然是显式 execution order，而不是“60Hz manager”。

低速、HitStop、取消等不得让一个 Gameplay Frame 的局部副作用被重复执行。Action animation 可以按 Sequence 的 animation-time mapping 精确提交 Pose；Gameplay Clip lifecycle 仍保持 fixed-frame semantics。

---

# 13. Root Motion / Root Rotation 数据管线

## 13.1 四个 Authority

```text
ActionSequence
= time authority

ActorAnimation / Animancer
= pose authority

RootMotionTrajectory
= authored motion data authority

ActorMotor / KCC
= gameplay world-state authority
```

Animation 不反向产生 Gameplay movement。

## 13.2 AnimationConfig 与 Baker 边界

长期冻结的是 **RootMotionTrajectory 数据合同、Unity Import / Avatar 语义、依赖身份与验证要求**，不是某一个具体 extraction backend。

当前 CombatSample 推荐 Editor Bake 路径：

```text
AnimationClip
+
AnimationConfig Editor Bake Context
+
Reference Rig / Avatar family
        ↓
validated extraction backend
        ↓
Unity 最终求值语义下的 cumulative Root Transform
        ↓
RootMotionTrajectory
```

对于当前 Humanoid 内容，`Reference Rig + Manual PlayableGraph + continuous Evaluate(dt)` 是推荐 backend，因为它能够吸收 Avatar / retarget / importer 的最终 Unity 求值语义。

Generic 可以复用同一 Baker 外壳并配置明确 Generic Root Node；如果未来验证证明直接读取 Unity 导入后的 Root curves 更合适，也允许作为内部 backend。backend 选择属于 Editor implementation detail，不暴露为 Runtime architecture。

所有 backend 都必须满足：

```text
不解析 FBX 作为 gameplay truth
不从 Hips / Pelvis 猜 Root Motion
尊重 Unity Import / Avatar semantics
依赖变化可判定 trajectory stale
输出相同 RootMotionTrajectory contract
存在独立 validator / oracle 验证
```

## 13.3 Trajectory 数据合同

Trajectory 保存累计变换：

```text
M(t) = animation start → time t 的 cumulative root transform
M(0) = Identity
```

数据继续保存：

```text
XYZ position
full Quaternion
```

即使 runtime 第一版不使用 Y / Pitch / Roll，也不因此破坏 baked source data。

区间 delta 必须使用：

```text
Delta(t0,t1)
= Inverse(M(t0)) * M(t1)
```

对应：

```csharp
deltaRotation = Quaternion.Inverse(r0) * r1;
deltaPosition = Quaternion.Inverse(r0) * (p1 - p0);
```

多个 delta 组合使用正确 SE(3) composition：

```text
AB.Position = A.Position + A.Rotation * B.Position
AB.Rotation = A.Rotation * B.Rotation
```

Sample 插值：

```text
Position → Lerp
Rotation → Slerp
```

## 13.4 Runtime 消费

术语固定：

```text
Root Motion
= baked XZ displacement

Root Rotation
= baked Yaw
```

两者分别进入 ActorMotor Translation / Rotation Domain，拥有独立 arbitration / owner stack。

Runtime 第一版：

```text
Root Motion Y → discard
Pitch / Roll gameplay rotation → not consumed
```

未来 Traversal 如果真的需要新的垂直 authored movement，应基于明确的新玩法需求设计，不通过恢复旧 `RootMotionPolicy` 自动扩张当前通道。

## 13.5 Advance / Seek / Cancel

```text
Advance
→ 时间真实经过
→ 可以产生 gameplay Root Motion / Root Rotation

Seek / Editor Scrub / SetTime
→ 只更新时间 / Pose
→ 不移动 Gameplay Actor
```

Cancel 只保留已经交给世界求解的 motion；剩余 trajectory 直接丢弃。

## 13.6 明确删除 Animator Gameplay Root Motion

长期删除：

```text
OnAnimatorMove
Animator.deltaPosition
Animator.deltaRotation
ActorRootMotionRelay
RootMotionApplyMode.Managed 作为 gameplay source
```

未来 motion source 必须先产生明确数据，再显式提交 ActorMotor。

Pose crossfade / Animancer blend weight 不自动混合 Gameplay trajectory。

---

# 14. Hit Detection / Hit Resolution

## 14.1 HitBoxClip 与 ActorHitBoxRuntime

`HitBoxClip` 只描述：

```text
active window
shape/config
attack data reference
```

实际运行态属于 Actor：

```text
ActorHitBoxRuntime
├─ active shapes
├─ previous / current shape state when needed
├─ attempted/already-hit targets
└─ query context
```

Clip Enter / Exit 激活和关闭 window；Clip 不自行决定全局 Physics Query 时机。

需要连续判定的 shape 使用 previous → current sweep 等对应查询方式，避免把高速移动退化成只看单点最终位置。

## 14.2 全局 Query barrier

固定底线：

```text
ALL Movement complete
→ Physics.SyncTransforms if needed
→ ALL HitDetection
→ HitResolution
```

HitDetection 使用：

```text
当前 Tick 已 Evaluate 的 Skeleton Pose
+
KCC / Resolver 最终 Actor Root
```

因此不能在 movement 前、普通 Update 或每个 Actor 自己的纵向 pipeline 中完成权威 hit query。

## 14.3 CombatHitBuffer

所有 ActorHitBoxRuntime 先把本 Tick PendingHit 写入一个强类型 `CombatHitBuffer`，然后统一排序 / Resolve。

稳定排序继续使用明确稳定键，例如：

```text
AttackerStableId
→ HitBox / Clip StableId
→ TargetStableId
```

Resolve 与 Query 分离，避免一个 Actor 的伤害副作用影响另一个 Actor 是否获得本 Tick Query 机会。

已收集 Hit 不因攻击者在同 Tick 后续死亡而自动取消，因此允许相杀；目标已经死亡后，后续 Hit 不重复扣血 / Impact / Death。

不建立通用 Gameplay Intent Pool / Transaction framework。

---

# 15. CombatSimulationDriver

## 15.1 根本职责与边界

`CombatSimulationDriver` 的根本职责是：

> **拥有 Combat World 的 Phase Order，而不是拥有 Actor 内部 Domain。**

60Hz 是 timestep policy，不是 Driver 的身份。

Driver 负责决定“什么阶段先发生、什么阶段后发生”，但不负责理解某个 Actor 内部如何完成 Animation、Motion、Action 或 HitBox 的具体工作。

固定边界：

```text
CombatSimulationDriver
= World Phase Order

ActorSimulationRuntime
= per-Actor simulation boundary / phase routing

Actor internal domains
= Action / ActorLocomotion / ActorAnimation / ActorMotor / HitBox runtime
```

因此 Driver 可以直接知道真正的 **world-level system / barrier**，例如：

```text
PlayerInputController（全局玩家输入 producer）
KinematicCharacterSystem
ActorCollisionResolver
Physics.SyncTransforms
CombatHitBuffer
```

但 Driver 不直接编排：

```text
ActorLocomotion
ActionStateManager
ActionPlayer
ActorAnimation
ActorMotor
ActorHitBoxRuntime
LocomotionRunner
MotionPolicy
Translation / Rotation internals
```

这些 per-Actor subsystem 只能通过 `ActorSimulationRuntime` 的明确 phase entry 进入固定模拟。

> **Driver 知道 Phase，不知道 Actor 内部 Subsystem。**

Driver 也不接管普通 Camera / UI / VFX / Audio 的 Unity lifecycle，并继续禁止任意子系统向 Driver 注册 generic phase callback。

## 15.2 正式 7-Phase Model

Combat fixed simulation 的一级 Phase Model 固定为：

```text
1. Input / Control
2. Action
3. Animation
4. Motion
5. World
6. Hit
7. Finish
```

含义：

```text
Input / Control
= 生产本 Tick control intent，并选择稳定 locomotion profile

Action
= fixed-tick Action arbitration + ActionSequence contribution production

Animation
= 汇总本 Tick animation state / pose contribution，并得到最终 skeleton pose

Motion
= ActorMotor 推进运动状态、完成 Translation / Rotation arbitration、产出 requested motion

World
= KCC / actor collision / transform sync，得到最终 world result

Hit
= 基于最终 skeleton + world transform 做统一 query，并统一 Resolve

Finish
= 关闭本 Tick frame lifecycle，执行 frame-end Exit / Action completion 等收尾
```

这七个 Phase 是长期架构语言。以后新增 fixed-simulation 行为，首先判断它属于哪个现有 Phase；不得因为新增一个 subsystem 就机械增加一个新的 Driver 一级 Phase。

只有出现无法由现有 Phase 表达、并且确实需要新的 **world-level ordering barrier** 的需求时，才重新讨论 Phase Model。

## 15.3 Global Barrier 原则

一级 Phase 始终遵守：

```text
ALL Actors Phase A
→ ALL Actors Phase B
→ ALL Actors Phase C
```

禁止：

```text
Actor A: Control → Action → Animation → Motion → Hit
Actor B: Control → Action → Animation → Motion → Hit
```

一个一级 Phase 内部允许存在必要的固定 sub-barrier，但 sub-barrier 同样由 Driver / ActorSimulationRuntime contract 明确表达，而不是依赖 Component execution order。

例如 Action Phase 第一版需要：

```text
ALL Action Decision
→ ALL ActionSequence Advance / contributions
```

Hit Phase 需要：

```text
ALL Hit Query
→ CombatHitBuffer Resolve
```

这些属于一级 Phase 内部的 execution contract，不因此扩张新的顶层 Phase。

## 15.4 Detailed Execution Contract

7-Phase Model 外围允许存在 KCC interpolation 与 tick snapshot 的边界操作；它们不是新的 Gameplay Phase。

完整第一版执行合同：

```text
Tick Boundary Setup
- Capture Actor Snapshot
- KCC PreSimulationInterpolationUpdate（若启用）

Phase 1 — Input / Control
- Player / AI 生产 LocomotionIntent
- ALL ActorSimulationRuntime.Control
  - ActorLocomotion Mode selection / profile update

Phase 2 — Action
- ALL ActorSimulationRuntime.DecideAction
- ALL ActorSimulationRuntime.AdvanceAction
  - ActionPlayer / ActionSequence fixed-frame contributions

Phase 3 — Animation
- ALL ActorSimulationRuntime.EvaluateAnimation
  - update current Locomotion Base state
  - resolve Locomotion Base + Action Override
  - ActorAnimation Evaluate exactly once

Phase 4 — Motion
- ALL ActorSimulationRuntime.PrepareMotion
  - LocomotionRunner
  - Translation / Rotation state evolution
  - MotionPolicy effective values
  - final RequestedVelocity / RequestedRotation

Phase 5 — World
- KinematicCharacterSystem.Simulate
- ActorCollisionResolver.ResolveFixedStep
- Physics.SyncTransforms（需要时）

Phase 6 — Hit
- ALL ActorSimulationRuntime.DetectHits
- CombatHitBuffer.Resolve

Phase 7 — Finish
- ALL ActorSimulationRuntime.FinishFrame
  - frame-end Clip Exit / Action completion

Tick Boundary Teardown
- KCC PostSimulationInterpolationUpdate（与 Pre 配对）
```

上述 `Control / DecideAction / AdvanceAction / EvaluateAnimation / PrepareMotion / DetectHits / FinishFrame` 是 **架构级 phase entry 的示意名称**，不冻结最终 public API 方法名。

重要的是依赖边界：Driver 对 Actor 只看到 `ActorSimulationRuntime`，由 Runtime 再把对应阶段路由到 Actor 内部 owner。

## 15.5 ActorSimulationRuntime 的定位

`ActorSimulationRuntime` 的价值不是增加一个新的 Runtime Domain，而是防止 Driver 随着系统增加而不断认识 Actor 内部细节。

结构固定理解为：

```text
CombatSimulationDriver
        │
        │  world phase
        ▼
ActorSimulationRuntime
        │
        ├→ Action domain
        ├→ ActorLocomotion
        ├→ ActorAnimation
        ├→ ActorMotor
        └→ ActorHitBoxRuntime
```

它只做：

```text
持有 / 解析该 Actor 的 simulation references
把 world phase 转发给正确 domain
保护 per-tick lifecycle / abort cleanup
```

它不做：

```text
Gameplay arbitration
Movement arbitration
Animation blending
Locomotion mode decision
Hit result resolution
```

这些职责仍由各自 Domain owner 持有。

同样，不建立 `IPhaseListener`、callback registry、generic scheduler 等为了减少几行显式代码而引入的抽象。固定少量 phase entry 比动态注册关系更容易阅读、验证和维护。

## 15.6 Animation / Motion 内部顺序不泄漏到 Driver

v3 文档需要描述内部依赖，但这些描述不等于 Driver 必须逐项直接调用 subsystem。

例如 Animation Phase 的 Actor 内部合同可以是：

```text
update Locomotion Base
→ resolve Action Override
→ ActorAnimation.Evaluate
```

第一版这些操作没有跨 Actor dependency，因此可以由单个：

```text
ActorSimulationRuntime.EvaluateAnimation(simulationDt)
```

在该 Actor 内部连续完成。

同理 Motion Phase 内部：

```text
LocomotionRunner
→ channel evolution
→ Translation / Rotation arbitration
→ Requested Motion
```

属于：

```text
ActorSimulationRuntime.PrepareMotion(simulationDt)
→ ActorMotor
```

Driver 不认识 `LocomotionRunner / MotionPolicy / Translation / Rotation`。

原则：

> **v3 中的 subsystem 顺序描述 dependency contract，不代表 Driver 对这些 subsystem 建立直接依赖。**

## 15.7 Mode → Action 的依赖方向

Input / Control Phase 的 `ActorLocomotion` selection 在 Action Phase arbitration 之前完成，这是固定顺序，不是偶然的 Component execution order。

本 Tick 新 Action 的 Enter / SelfTags / Clip contribution 不反向重新触发本 Tick Mode selection。

因此：

```text
Tick-start stable state
+ current Intent
→ CurrentMode N
→ Action arbitration N may consume CurrentMode N
→ Action N contributions
```

如果 Action 需要限制自由移动、改变 authored movement 或控制朝向，应直接提交 MotionPolicy / Translation / Rotation contribution，而不是要求 ActorLocomotion 在同 Tick 重新选一次 Mode。

## 15.8 Produce → Consume

同一个 Combat Tick 内，本 Tick producer 的输出应由本 Tick consumer 使用。

典型：

```text
Produce Intent N
→ Consume Intent N
→ expire / replace according to channel contract
```

Action / Sequence、Locomotion、Animation、Motor、HitBox 的顺序都必须通过 7-Phase Model 与必要 sub-barrier 表达，而不是依赖 MonoBehaviour Script Execution Order 的偶然关系。

## 15.9 Combat simulation time / HitStop

Driver 的 phase barrier 与 Actor 的 simulation time 是两个概念。

HitStop / actor-local combat time freeze 时，Driver 仍可以维持全局阶段顺序，但被冻结 Actor 的 simulation time 不推进。

至少冻结：

```text
ActionSequence advance
Action animation sample progression
Locomotion animation progression
Animation blend / crossfade progression
LocomotionRunner 的时间性演化
Ballistic gravity evolution
HorizontalImpulse decay / other temporal channel evolution
authored Root Motion / Root Rotation interval advancement
```

不要求冻结：

```text
raw player input capture
input history collection
Driver world phase execution itself
```

实现可以选择在某些 Actor phase 中快速 no-op，但不得让 frozen Actor 因 Driver 仍执行而重复 Gameplay Frame side effect，也不得让 Animation 已冻结而 Gravity / Impulse 在后台继续演化。

---

# 16. Legacy Migration Boundary

Legacy Timeline、旧 Motor runtime、Animator Gameplay Root Motion、整招 Action MotionConfig 等仍可能存在于当前代码或资产中，但不再决定长期架构。

迁移原则：

```text
Legacy Timeline
→ migration compatibility only
→ no new long-term gameplay backend

ActorLogicInput
→ compatibility shell only
→ no runtime authority

LocomotionRuntime / FacingRuntime / ActorMotionRuntime
→ implementation migration source
→ not v3 domain model

ActionMotionConfig / ActionInstance whole-action motor rewrite
→ serialized compatibility / migration source only
→ not v3 Action contract

Animator Gameplay Root Motion
→ remove
```

内容迁移必须保持 Unity 资产 GUID / serialized reference 安全，不能为了代码整洁提前删除仍被旧内容引用的兼容字段或 backend。

最终清理应发生在对应内容和运行路径完成迁移、验证之后。

---

# 17. 第一版明确不做

除非出现真实玩法需求，不引入：

```text
Generic Gameplay StateSystem
Action / Locomotion whole-character ownership transfer
Generic Animation Layer Framework
Animation Contribution Stack
Generic Motion Graph / DAG
Generic Driver callback scheduler
Input Provider / Command Bus abstraction
Root Motion Y gameplay consumption
Pitch / Roll gameplay Root Rotation
weighted Root Motion blending by Animancer weight
Motion Warping
RootMotionPolicy 恢复为通用 runtime policy
复杂 Vertical owner finish / inherit policy
Mode Stack / Locomotion Transition Graph
Action-level MotionConfig 作为长期 movement authority
```

原则是：

> 已经存在明确领域 owner 的地方，用固定语义解决；没有真实需求的地方，不提前搭通用框架。

---

# 18. Validation / Architecture Invariants

v3 实现与后续重构必须至少守住以下不变量。

## 18.1 Authority

```text
Action selection
→ only ActionStateManager fixed-tick arbitration path

Action playback lifecycle
→ ActionPlayer

Gameplay Actor Root movement
→ only ActorMotor / KCC path

Animancer Graph / Evaluate
→ only ActorAnimation

Gameplay Root Motion
→ only baked data → ActorMotor

Gameplay Hit Query
→ only post-movement Driver phase
```

## 18.2 Translation

```text
HorizontalVelocityOwner > Root Motion > Locomotion + HorizontalImpulse
VerticalVelocityOwner ? owner : BallisticVerticalVelocity
```

数据合同：

```text
Locomotion / HorizontalImpulse / HorizontalVelocityOwner
→ world planar velocity, m/s

Root Motion
→ local planar displacement, meters / current interval

Ballistic / VerticalVelocityOwner
→ scalar CharacterUp velocity, m/s
```

低优先级 Channel 被覆盖时仍接受 submission / update，但不 catch-up。

Root Motion active 时 HorizontalImpulse 不进入最终 Root Motion 分支。

## 18.3 Rotation

```text
Scripted Rotation > Root Rotation > Locomotion Rotation
```

所有 Channel 输出 local yaw delta；Motor 不理解 Target / Direction / Snap。

## 18.4 Grounding

```text
Grounded
→ final vertical 0
→ Ballistic 0
→ no gravity accumulation
```

Grounded 不结束 VerticalVelocityOwner。

## 18.5 Animation

```text
Locomotion Base persistent
Action Override temporary
max one Action Pose per Actor per Tick
no Pose → reveal Locomotion
ActorAnimation Evaluate once per Combat Tick
blend progression uses Combat simulation dt
```

HitStop 冻结 Action、Locomotion animation 与 crossfade simulation time。

## 18.6 Root data

```text
M(0)=Identity
Delta(t0,t1)=Inverse(M(t0))*M(t1)
Root Motion = XZ
Root Rotation = Yaw
blocked displacement never repaid
```

Baker backend 可以演进，但所有 backend 必须输出同一 trajectory contract，并尊重 Unity Import / Rig / Avatar dependency identity 与独立验证要求。

## 18.7 Hit

```text
ALL movement
→ sync
→ ALL query
→ stable Resolve
```

同 Tick 结果不得依赖 Actor 注册顺序、Physics 返回容器顺序或 per-Actor 纵向执行顺序。

## 18.8 Time Domain

```text
raw input time
≠ Combat simulation time
≠ Unity render-frame time
```

HitStop 可以让 Actor Combat simulation dt = 0，而不停止 raw input capture。所有依赖 Combat simulation time 的 Gameplay / Animation / Motion temporal evolution 必须一起冻结，不能各自偷用 render delta。

## 18.9 Action / Locomotion dependency

```text
ActorLocomotion Mode selection
→ Action arbitration
→ Action contributions
```

本 Tick Action side effects 不反向重跑本 Tick Mode selection。

Action 的 movement 权限与覆盖通过独立 control channels 表达，不通过 `ActionMode / LocomotionMode` whole-actor ownership transfer。

## 18.10 Driver Boundary / 7-Phase Model

```text
CombatSimulationDriver
→ owns Input/Control → Action → Animation → Motion → World → Hit → Finish order

per-Actor execution
→ only through ActorSimulationRuntime
```

Driver 不得随着系统增加而直接依赖 `ActorLocomotion / ActionStateManager / ActionPlayer / ActorAnimation / ActorMotor / ActorHitBoxRuntime` 等 Actor 内部 subsystem。

一级 Phase 的数量由 world-level ordering dependency 决定，不由“项目里有多少 subsystem”决定。

不得通过 generic callback / phase listener registry 隐藏真实执行顺序；固定显式 phase entry 是当前长期方案。

---

# 19. Implementation Gaps：不是 Open Architecture

以下当前可能尚未落地，但只属于后续 E3 Implementation Plan，不重新开放上述架构：

```text
ActorLocomotion Component 实现 / prefab migration
ActorAnimation Component 实现 / Animancer authority migration
ActorMotor 从旧 Runtime 划分迁到 Translation / Rotation Domain
Translation 单位 / 坐标合同统一
BallisticVerticalVelocity 替代旧 vertical state
Root Motion owner stack
Root Rotation owner stack
Scripted Rotation owner stack
MotionPolicy / MotionPolicyClip
Animation centralized Evaluate phase
ActionMotionConfig / ActionInstance whole-action motor rewrite 迁移
旧 RootMotion + HorizontalImpulse compose 修正
旧 Animator Gameplay Root Motion 删除
旧 ActorLogicInput runtime path 清理
Legacy Timeline 内容迁移
HitStop / simulation time contract 统一
CombatSimulationDriver 收敛为 7-Phase façade
ActorSimulationRuntime 补齐明确 phase routing，避免 Driver 直接认识 Actor 内部 subsystem
```

已有 `ActionStateManager / ActionPlayer` 的 fixed-tick action arbitration / playback 职责属于应保留并接入 v3 的现有基础，不是需要删除的旧系统。

仍可在 implementation 阶段确定的内容：

```text
ActorAnimation public API 方法名
Action owner token 具体 struct / generation
LocomotionAnimationProfile 数据结构
crossfade 参数具体配置位置
Pose mixer parameter 数据结构
MotionPolicy token/container 类型
ActorSimulationRuntime phase entry 的最终方法名
Driver 各 Phase helper 的具体代码组织
serialized authored order 的具体实现
Inspector validation 细节
Baker backend 的内部组织方式
迁移提交切片与测试顺序
```

这些实现选择必须服从本文 Domain / authority / arbitration，不得为了局部方便重新引入旧的 whole-domain ownership、Generic Runtime、generic scheduler 或 God Component。

---

# 20. Final Architecture v3 一句话

> **Player / AI 产生 LocomotionIntent；ActorLocomotion 先选择当前 Locomotion Profile；ActionStateManager 在 fixed Tick 仲裁 Action 并由 ActionPlayer 管理播放生命周期；ActionSequence 以 fixed frame 产生局部 Gameplay contribution；ActorAnimation 独占动画表现；ActorMotor 以 Translation / Rotation 两大 Domain 固定仲裁全部运动请求并交给 KCC；CombatSimulationDriver 只拥有 Input / Control → Action → Animation → Motion → World → Hit → Finish 的 World Phase Order，并通过 ActorSimulationRuntime 进入每个 Actor，使所有 Actor 在统一 barrier 与 Combat simulation time 下完成确定性模拟。**