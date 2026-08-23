# CombatSample E3 前置架构决策 Checkpoint

> 状态：讨论 Checkpoint；仅记录截至 2026-08-23 已明确确认的结论、被后续讨论取代的旧方向，以及仍未收口的问题。
>
> 基线：Stage E2 已落地；E3 尚未开始正式实现。
>
> 目的：在继续讨论 MotionPolicy、AnimationRuntime、LocomotionController / ModeAsset 之前，冻结一份可回溯的决策快照，防止后续讨论重新引入已经被否决或修正的旧假设。
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

E2 之后，在正式进入 E3 `LocomotionController / LocomotionModeAsset` 实现前，对以下领域进行了连续架构复核：

- Simulation Driver 与 producer / consumer 顺序；
- UE 中 Locomotion / Montage / CharacterMovement 的职责边界；
- Locomotion 与 Action 的整体关系；
- Animation arbitration；
- Root Motion authority；
- `ActorMotor / ActorMotionRuntime` 的 channel / compose 模型；
- 水平 motion precedence；
- 类鬼泣式 3D 空战需要的垂直 motion 模型；
- MotionPolicy 的定位与生命周期方向。

本 Checkpoint 只固化已经明确确认的部分。

---

# 2. Confirmed Decisions

## 2.1 总体架构原则：统一生命周期，分离控制通道

Action 可以统一拥有 Gameplay 生命周期，但不得成为 Animation、Movement、HitBox、Facing、Tags 等所有子系统的统一状态 owner。

目标结构：

```text
ActionInstance / ActionSequence lifecycle
        │
        ├── Animation contribution
        ├── MotionPolicy contribution
        ├── Motion channel contribution
        ├── HitBox window
        ├── Tags
        └── 其他 scoped contribution
```

各领域独立仲裁：

```text
Animation arbitration
Movement arbitration
HitBox lifecycle / query
Tags / gameplay effects
```

不得建立一个“大 ActionMode / LocomotionMode 开关”去一次性切换所有系统的控制权。

核心原则：

> 统一生命周期，分离控制通道。

---

## 2.2 Simulation Driver：执行顺序是第一职责

`CombatSimulationDriver` 的身份是 Combat World scheduler；固定 60Hz 是 timestep policy，不是 Driver 存在的根本原因。

固定模拟必须保持全局 phase barrier：

```text
ALL Actors Control Production
→ ALL Actors Action / Sequence
→ ALL Actors Motion Requests
→ ALL Actors Movement / KCC
→ Physics Sync（需要时）
→ ALL Hit Detection
→ ALL Hit Resolution
```

禁止改成：

```text
Actor A Action -> Move -> Detect
Actor B Action -> Move -> Detect
```

每个 Actor 只通过一个 `ActorSimulationRuntime` 参与固定模拟；其他子系统不向 Driver 注册通用 phase callback。

KCC 是世界运动求解 barrier，不是 Gameplay scheduler。

---

## 2.3 Input / LocomotionIntent

玩家输入链保持：

```text
Input System
→ PlayerInputController
→ PlayerLocomotionIntentResolver
→ LocomotionIntent
→ ActorMotor
```

AI 保持独立生产：

```text
AI / BehaviorTree
→ LocomotionIntent
→ ActorMotor
```

已确认约束：

- `PlayerInputController` 是玩家 raw input 与 input history owner；
- `PlayerLocomotionIntentResolver` 只解释玩家输入；
- AI 不需要模拟 joystick / button；
- `ActorMotor` 不知道 intent 来源；
- 不引入 `IActorInputProvider`、通用 Command Bus、Intent Source Manager 等无现实需求的抽象；
- `ActorLogicInput` 不再是 runtime authority，只保留兼容壳直到安全清理。

`LocomotionController` 不是 `LocomotionIntent -> ActorMotor` 的必要中转层。Player / AI 可以持续直接向 Motor 提交 intent。

---

## 2.4 LocomotionController 的职责方向

`LocomotionController` 不负责“Action 期间阻止输入进入 Motor”。

其长期职责方向是：

```text
选择当前 Locomotion Mode
管理 Idle / Run / Strafe / Air 等 locomotion behavior
管理 locomotion animation / mixer
承载 locomotion-specific gameplay configuration
必要时产生自己的 MotionPolicy contribution
```

不得把它设计为：

```text
Input authority
ActorMotor replacement
Action / Locomotion 总状态切换器
```

`LocomotionModeAsset` 的既有方向仍有效：Mode 持有自己的 Priority、EntryConditions、SelfTags、AnimationConfig Key、Locomotion 参数；但其最终字段与 MotionPolicy / AnimationRuntime 的边界仍待后续讨论。

---

## 2.5 Animation：Base Locomotion + Temporary Action Override

Animation 不采用“Locomotion 停止 -> Action 接管整个 Animancer -> Action 结束后重新启动 Locomotion”的整体 ownership transfer。

高层模型：

```text
Locomotion animation
→ Base pose / base semantic channel

Action animation
→ Temporary override semantic channel
```

Locomotion 的 animation state 可以在 Action override 期间保持更新，从而 Action 淡出后自然露出最新 locomotion pose。

语义 ownership 必须分离：

```text
LocomotionController
→ 只请求 locomotion animation channel

Action / Sequence
→ 只请求 action animation channel
```

不得由两者直接争抢“整个 Animancer 的唯一控制权”。

---

## 2.6 ActorAnimationRuntime：明确方向，但 API 尚未冻结

已经形成的职责边界：

```text
LocomotionController ─┐
                      ├→ ActorAnimationRuntime → Animancer
Action / Sequence ────┘
```

`ActorAnimationRuntime` 应只拥有 animation 技术资源与 graph 操作，例如：

```text
semantic layer / channel
play / state
weight / fade
centralized Evaluate
```

它不得理解具体 Gameplay 语义，例如 Attack、Dodge、Hit、某个 Locomotion Mode 的进入条件。

Action animation session 的生命周期应由 ActionPlayer / Action runtime 管理；`AnimationPoseClip` 只描述当前区间的具体 action pose / time，不负责整个 Action override session 的 begin/end。

`Evaluate()` 必须从单个 PoseClip 中集中出去；最终应由 Actor 级 animation authority 在已知阶段统一 Evaluate。

但以下细节尚未冻结：

- 最小 API；
- Action layer / channel 的完整生命周期；
- Action A -> B handoff；
- fade / weight 的 fixed-tick 语义；
- Driver 中 `Evaluate()` 的确切 phase。

---

## 2.7 Root Motion authority

继续遵守既有原则：

> Sequence is authoritative; Animation is data.

Gameplay Root Motion 链路：

```text
Sequence fixed frame
→ RootMotionTrajectory
→ RootMotionClip（或未来其他显式 motion data producer）
→ ActorMotor
→ KCC
```

Animation 不再反向决定 Gameplay movement。

当前存在的：

```text
Animator
→ OnAnimatorMove
→ Animator.deltaPosition / deltaRotation
→ ActorRootMotionRelay
→ ActorMotor
```

不属于长期架构，后续应移除。

未来即使增加 RootMotionClip 之外的新 Root Motion 来源，也必须先产生明确 motion data，再显式提交给 Motor；Motor 不从 Animator 读取权威位移。

Trajectory Root Motion 只负责 XZ；Y 丢弃。

---

## 2.8 ActorMotor 总体框架：保留

现有 Motor 的大体结构是正确的，不做架构性推翻：

```text
Producer
→ 独立 Motion Channel
→ Channel 独立保存 / 演化自己的状态
→ ActorMotionRuntime / ActorMotor Compose
→ KCC
```

最重要的约束：

> Channel submission 与 final arbitration 必须分离。

某个高优先级 Channel 当前生效，不得阻止其他 Channel 正常接收提交、维护状态和推进生命周期。

例如：

```text
HorizontalVelocityOwner active
≠ 禁止 Trajectory 提交

Trajectory active
≠ 禁止 HorizontalImpulse 提交
```

跨 Channel 的“谁最终生效”只能存在于 Compose 规则中，不得散落在各 Producer / Channel 的提交逻辑里。

---

## 2.9 水平 Motion Channels

水平长期保留四类 contribution：

```text
Locomotion
HorizontalImpulse
Trajectory Root Motion
HorizontalVelocityOwner
```

### 2.9.1 最终水平 Compose

固定为：

```text
if HorizontalVelocityOwner exists
    Horizontal = TopHorizontalVelocityOwner
else if TrajectoryRootMotion exists
    Horizontal = TopTrajectoryRootMotion
else
    Horizontal = Locomotion + HorizontalImpulse
```

即：

```text
HorizontalVelocityOwner
        >
Trajectory Root Motion
        >
Locomotion + HorizontalImpulse
```

`HorizontalImpulse` 不再与 Trajectory Root Motion 相加。

注意：这是 Compose 规则，不是 submission rule。

Trajectory / VelocityOwner 生效期间，HorizontalImpulse 仍可被外部提交并维护自己的状态；只是当前 Compose 不读取它。

---

## 2.10 HorizontalVelocityOwner

`HorizontalVelocityOwner` 是可恢复的 LIFO stack：

```text
A Begin -> A
B Begin -> B 覆盖 A
B End   -> A 恢复
```

非栈顶 owner 可以继续被外部更新，只是不参与当前最终 Compose。

它对最终水平输出的优先级高于 Trajectory、Locomotion 与 HorizontalImpulse，但不得销毁或阻止这些 Channel 自己的状态。

---

## 2.11 Trajectory Root Motion Channel

当前 single-slot replace 规则需要修改为可恢复 LIFO stack：

```text
A Begin -> A
B Begin -> B 临时覆盖 A
B End   -> A 恢复
```

进一步约束：

- 被覆盖的 Trajectory owner 自身生命周期继续存在；
- 被覆盖期间 Sequence 时间可以继续推进；
- 被覆盖期间的 trajectory delta 不累计、不在恢复时补发；
- 恢复后只从当前 Tick / 当前 Sequence 时间继续提交；
- Trajectory 只产生 XZ movement；Y 永久丢弃。

Trajectory 与 HorizontalVelocityOwner 是不同 Channel。VelocityOwner 只是 Compose 上覆盖 Trajectory，不得清除 Trajectory owner stack。

---

## 2.12 HorizontalImpulse Channel

`HorizontalImpulse` 是独立状态，负责：

```text
接收外部 additive impulse
维护自己的 impulse velocity
按自己的内部 drag 规则衰减
```

它不属于 Locomotion，也不属于 Trajectory。

最终是否影响实际移动只由水平 Compose 决定：

```text
只有 Compose 落到 Locomotion 分支时
→ Locomotion + HorizontalImpulse
```

已明确否决：

```text
Trajectory Begin -> ClearHorizontalImpulse
Trajectory active -> Reject AddHorizontalImpulse
VelocityOwner active -> Reject AddHorizontalImpulse
```

Channel 不互相阻拦。

---

## 2.13 垂直 Motion：改为单一自由弹道状态 + scripted override

类鬼泣式 3D 空战下，当前长期并列：

```text
GravityAccumulator
+
VerticalImpulseVelocity
```

的模型不再作为目标架构。

自由弹道收敛为单一权威状态：

```text
BallisticVerticalVelocity
```

职责：

```text
Gravity
→ 持续演化 BallisticVerticalVelocity

Jump / Launch / Vertical impulse producer
→ 事件式修改 BallisticVerticalVelocity
```

最终不再使用：

```text
FinalVertical = GravityAccumulator + VerticalImpulseVelocity
```

作为长期模型。

---

## 2.14 BallisticVerticalVelocity 的外部修改

外部 Producer 在提交时决定本次操作语义；Ballistic channel 不理解 Jump、Launcher、Hit 等 Gameplay 概念。

至少支持：

```text
Add（默认）
velocity += value

Set
velocity = value
```

`Max / Min` 等操作只有在后续出现明确需求时再增加，不提前扩展。

Gravity 是 Channel 自身的持续演化：

```text
BallisticVerticalVelocity += Gravity * dt
```

---

## 2.15 VerticalVelocityOwner

语义分界：

```text
BallisticVerticalVelocity
= 自由弹道

VerticalVelocityOwner
= scripted / Action-driven 垂直轴临时接管
```

例如：

```text
Jump / DoubleJump / Launcher
→ Ballistic 写入

悬停 / 固定升降 / 下砸曲线
→ VerticalVelocityOwner
```

最终 Compose：

```text
if VerticalVelocityOwner exists
    Vertical = TopVerticalVelocityOwner
else
    Vertical = BallisticVerticalVelocity
```

`VerticalVelocityOwner` 使用可恢复 LIFO stack：

```text
A Begin -> A
B Begin -> B
B End   -> A 恢复
A End   -> 回到 Ballistic
```

非栈顶 owner 可以继续被外部更新，只是不参与当前 Compose。

---

## 2.16 VerticalVelocityOwner 与 Ballistic 的 handoff

只要存在至少一个 `VerticalVelocityOwner`：

```text
BallisticVerticalVelocity 冻结
Gravity 不在后台继续积分
```

当 owner stack 从非空变成空，即最后一个 owner 结束时：

```text
BallisticVerticalVelocity = 0
```

下一 Tick 再从 0 开始受 Gravity。

当前不默认继承：

```text
Owner 接管前的旧 Ballistic velocity
Owner 最后一帧 velocity
```

未来若出现明确玩法需求，可再引入显式 finish / handoff policy；当前不提前实现。

---

## 2.17 Grounded 对垂直系统的规则

Grounded 必须区分“最终物理约束”和“Channel 生命周期”。

### 最终物理约束

稳定接地时：

```text
Final Vertical = 0
```

### Ballistic 状态

稳定接地时：

```text
BallisticVerticalVelocity = 0
Grounded 期间不继续积累 Gravity
```

### VerticalVelocityOwner

Grounded 不得：

```text
自动销毁 owner
修改 owner velocity
阻止 VelocityClip / Producer 继续 Submit
```

例如：

```text
VelocityClip 持续提交 -20
Grounded = true

Channel state:
TopOwnerVelocity = -20

Final movement:
Vertical = 0
```

只要 Clip / Owner 自身仍存活，该请求仍然有效；如果之后重新离地，它可以再次参与 Compose。

向上的有效垂直控制仍必须具备主动离地能力；顶层 owner 在稳定接地时提交明显正速度时，应继续支持 ForceUnground 类机制。

---

## 2.18 MotionPolicy 的定位

MotionPolicy 的高层边界已经确认：

```text
LocomotionIntent
= Actor 想怎样移动

MotionPolicy
= 当前允许这些 movement contribution 怎样生效 / 允许哪些参数

MotionChannels
= 实际 motion contribution

ActorMotor
= 固定内部 arbitration / compose
```

即：

```text
Intent + EffectivePolicy + MotionChannels
→ ActorMotor fixed rules
→ KCC
```

MotionPolicy 可以改变参数，但不得改变已经冻结的 Motor channel precedence / semantic contract。

不得允许外部 Action 配置类似：

```text
“这次 Impulse 比 Trajectory 优先”
“这次改变 Motor precedence”
```

这类跨 Channel 仲裁规则必须固定在 Motor 内部。

---

## 2.19 MotionPolicy 生命周期方向

必须避免永久 setter + 人工恢复：

```text
SetLocomotionSuppressed(true)
...
希望某处记得 SetLocomotionSuppressed(false)
```

长期方向必须是 scoped / owned modifier：

```text
Acquire
→ optional Update
→ Release own token
```

典型生命周期：

```text
Action Begin
→ acquire Action-owned policy contribution

Clip Enter
→ acquire Clip-owned contribution

Clip Exit
→ release Clip contribution

Action Complete / Cancel
→ release all Action-owned contributions
```

该方向与现有 `SpeedModifierStack` / `MotionOwner` 的 ownership 思路一致。

但 Policy 具体字段、字段合成规则和 API 尚未冻结，见 Open Questions。

---

# 3. Superseded / Rejected Decisions

以下方向不得继续作为 E3 设计依据；其中部分仍可能存在于旧文档或当前兼容代码中。

## 3.1 输入与 Locomotion

以下已被取代：

```text
ActorLogicInput 作为 runtime input authority
LocomotionController 作为 LocomotionIntent -> ActorMotor 必经中转层
Player / AI 必须通过统一 Input Provider 抽象
```

当前基线以 E2 的 `PlayerInputController + PlayerLocomotionIntentResolver` 和 AI 独立 intent producer 为准。

---

## 3.2 Action / Locomotion 总状态切换

以下不得视为当前已批准架构：

```text
StateKind.Action / StateKind.Locomotion 已经批准并冻结
通用 Gameplay StateSystem
Action 进入时整体夺取所有子系统控制权
```

`StateKind` 虽存在于旧 Architecture v2 路线描述，但没有明确批准证据；后续设计不得把它当作既定前提。

---

## 3.3 Animation ownership

以下旧方向被当前“Base + Override / independent channels”思路取代：

```text
进入 Action 后 LocomotionController 完全停止操控 Graph
Action 独占整个 Animancer
Action 结束后显式重新启动 Locomotion animation
```

最终细节仍待 Animation 收口，但不再回到整体 ownership transfer 模型。

---

## 3.4 Animator Root Motion

以下不属于长期 Gameplay movement：

```text
OnAnimatorMove
Animator.deltaPosition / deltaRotation
ActorRootMotionRelay
RootMotionApplyMode.Managed 作为正式运动来源
```

该路径后续应删除，不再纳入最终 Motor arbitration contract。

---

## 3.5 水平 Motion 提交阻断

以下已明确否决：

```text
Trajectory Begin -> Clear HorizontalImpulse
Trajectory active -> Reject HorizontalImpulse submission
HorizontalVelocityOwner active -> Reject lower channel submission
```

Submission 与 Compose 必须分离。

---

## 3.6 垂直双状态模型

以下不再作为目标模型：

```text
GravityAccumulator
+
VerticalImpulseVelocity
→ 最终自由垂直速度
```

目标模型改为单一 `BallisticVerticalVelocity`。

---

## 3.7 Grounded 自动结束 scripted velocity

以下已否决：

```text
Grounded / Landed
→ ActorMotor 自动 EndVerticalVelocityOwner
```

Grounding 只约束最终物理输出和 Ballistic 状态，不接管外部 Owner 生命周期。

---

# 4. Open Questions

以下问题仍未冻结。后续讨论必须将其从 Open Question 明确转为 Confirmed Decision 后，才可进入最终架构文档。

## 4.1 MotionPolicy

需要逐项确认：

```text
Policy 到底包含哪些参数
LocomotionScale / SuppressLocomotion 如何表达
GravityScale
AirControlScale
FacingEnabled / Facing policy 是否属于 MotionPolicy
RootMotion 相关参数是否还需要存在
其他参数是否有真实需求
```

同时需要定义：

```text
同一参数多个 modifier 的合成规则
Min / Multiply / Override / Priority 等分别适用于哪些字段
Action default contribution
Clip contribution
token ownership / release
EffectivePolicy 何时求值
ActorMotor 如何读取
```

原则已确认，但具体模型尚未设计完成。

---

## 4.2 ActorAnimationRuntime

仍需确认：

```text
最小 public API
Locomotion semantic channel 的具体职责
Action semantic channel 的具体职责
Action animation session begin/end
AnimationPoseClip 是否允许 session 内存在空档
Action A -> Action B 是否保持 override channel 连续
fade / weight 如何按 fixed simulation time 推进
Evaluate 的 Driver phase
HitBox bone pose 与 Evaluate 的 tick 对齐关系
```

---

## 4.3 LocomotionController / LocomotionModeAsset

需要在新的“Intent 直接到 Motor + MotionPolicy + AnimationRuntime”框架下重新收口：

```text
Mode 的最小职责
ModeAsset 哪些字段属于 Gameplay
哪些字段属于 Animation
哪些字段转换为 MotionPolicy contribution
Mode Tags 的 ownership / release
Mode transition / Claim 生命周期
Fallback 的最终 contract
```

不得机械照搬旧 Architecture v2 中“Locomotion 域独占 Motor / Animancer”的描述。

---

## 4.4 Animation Evaluate 与 Driver 顺序

HitBox 可能依赖 bone Transform，因此最终必须明确：

```text
ALL Animation Requests
→ ALL Animation Evaluate
→ Movement
→ Physics Sync
→ HitDetection
```

或其他等价的固定顺序。

当前只确认 `Evaluate()` 需要集中；确切 phase 尚未冻结。

任何调整不得破坏已经冻结的：

```text
ALL movement complete
→ Physics sync if needed
→ ALL HitDetection
→ HitResolution
```

---

# 5. 当前目标架构快照

截至本 Checkpoint，可以用以下关系图表达当前已确认方向：

```text
                   INPUT / AI
                       │
                       ▼
               LocomotionIntent
                       │
                       │
Gameplay / Action      │
      │                │
      ├── MotionPolicy ┤
      │                │
      ├── Motion Channels ───────────┐
      │                              │
      │                        ActorMotor
      │                     fixed arbitration
      │                              │
      │                              ▼
      │                             KCC
      │
      ├── Animation Contribution ─→ ActorAnimationRuntime ─→ Animancer
      │
      ├── HitBox Window ───────────→ ActorHitBoxRuntime
      │
      ├── Tags
      └── other scoped contributions
```

Motor 内部：

```text
HORIZONTAL

HorizontalVelocityOwner
        >
Trajectory Root Motion
        >
Locomotion + HorizontalImpulse
```

```text
VERTICAL

BallisticVerticalVelocity
    ← Gravity continuous evolution
    ← external Add / Set

VerticalVelocityOwner Stack
    ↓

if owner exists
    Vertical = TopOwnerVelocity
else
    Vertical = BallisticVerticalVelocity

Grounded
    → FinalVertical = 0
    → BallisticVerticalVelocity = 0
```

---

# 6. 后续使用规则

1. 本文是 E3 正式实现前的讨论 checkpoint，不直接替代 `Final_Architecture_v2`。
2. 当旧架构文档与本文明确冲突时，E3 后续讨论不得继续把已被本 Checkpoint 标记为 superseded 的旧方向当作前提。
3. 后续每完成一个 Open Question，应先形成明确 Confirmed Decision，再进入实现计划。
4. MotionPolicy、AnimationRuntime、LocomotionController / ModeAsset 全部收口后，再统一生成新的 Final Architecture 版本；不要在旧 v2 上持续堆叠互相冲突的局部补丁。
5. E3 Implementation Plan 应建立在收口后的最终架构上，而不是直接建立在本 Checkpoint 的未决项上。
