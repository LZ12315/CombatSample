# CombatSample Action 最终架构 v1（冻结稿）

> 状态：**Approved / Frozen Design Target**  
> 目标分支：`FrameWork`  
> 日期：2026-08-30  
> 计划路径：`Docs/Proposals/CombatSample_Action_Final_Architecture_v1_zh-CN.md`

## 0. 文档权威与适用范围

本文定义下一代 Action authoring / playback 的冻结目标。

- 本文负责 **未来 Action authoring / playback 架构**。
- 当前 `CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md` 与 E3 已验证代码继续负责 **现有 Combat Runtime 的已验证 Domain / Authority / phase 基线**，尤其是 `CombatSimulationDriver`、`ActorAnimation`、`ActorMotor`、Motion Domain、Hit、Tag 与 60Hz fixed simulation。
- 如果两者在 Action authoring / playback 数据模型上冲突，以本文作为迁移目标；底层 Domain authority 与 phase 语义继续继承 E3，除非另行 Architecture Review。
- `Action Final Architecture v1` 是新的 Action architecture version line，不是旧 `ActionSequence Final Architecture v3` 的版本号倒退。
- 实施顺序与迁移阶段由 `CombatSample_Action_Implementation_Roadmap_v1_zh-CN.md` 定义。

---

# 1. 总体结构

```text
ActionAsset
    ↓
ActionStateManager
    ↓
ActionPlayer
    ↓
ActionRuntime
    ↓
ActionRuntimeScheduler
    ↓
GameplayItemRuntime
    ↓
ActorAnimation / ActorMotor / TagSystem / HitBoxSystem / ...
```

核心原则：

> **Action 系统负责“什么时候发生什么”；各 Gameplay Domain 负责“多个贡献合在一起后最终效果怎么算”。**

---

# 2. ActionAsset：唯一持久化 Authoring Source

```text
ActionAsset
├─ Priority
├─ EntryRules
├─ CancelRules / CancelWindows
├─ SelfTags
└─ Timeline
   ├─ AnimationSegments[]
   └─ GameplayLanes[]
       └─ GameplayItems[]
```

不再保存：

```text
Manual Duration
Per-Action FrameRate
Loop
ReenterRule
ExitConditions
ActionMotionConfig
Action-side AnimationConfig
animationKey
PlaybackBackend
ActionSequenceData
Legacy Timeline
```

Input Binding 不属于 `ActionAsset`。同一 Action 可以由玩家、AI、Combo、Event、Script、Network 等来源请求。

---

# 3. Action-level 规则

## 3.1 Priority

由 `ActionStateManager` 用于 Action arbitration。Scheduler 不读取 Priority 决定 Gameplay Item 执行顺序。

## 3.2 EntryRules

回答：当前是否允许开始这个 Action。它属于开始前 arbitration，不建模成 Frame 0 Item。

## 3.3 CancelRules / CancelWindows

回答：当前 Action 播放到这里时，新的 Action Request 是否允许替换它。

Cancel 规则可以读取当前 Action Frame 与自动推导的 Action Duration，但仍属于 `ActionStateManager`。

## 3.4 SelfTags

`SelfTags` 覆盖整个 ActionRuntime 生命周期：

```text
Begin → acquire
Completed / Interrupted → release
Aborted → best-effort release
```

只在部分 Timeline 范围存在的 Tag 使用 `TagItem`。

## 3.5 删除 ExitConditions

自然结束：Timeline 最后一帧完整 Finish 后 `Completed`。

提前正常结束：Cancel / Replace / Self Transition / Stop → `Interrupted`。

不再保留第三套每帧 `ExitConditions`。

---

# 4. Self Transition / Reenter

删除 `ReenterRule` 与 `AllowReenter`。

> **`RequestedAction == CurrentAction` 不做特殊处理。**

仍然正常走 Entry / Cancel / Arbitration。

如果 Self Transition 被接受：

```text
旧 ActionRuntime → Interrupted
新 ActionRuntime → Create → Begin → Frame 0
```

必须满足：

```text
same ActionAsset != same ActionRuntime
```

不能把旧 Runtime 的 Frame 直接重置为 0，因为旧 Runtime 可能仍持有 Motion owner、HitBox handle、Tag owner 与 Range runtime。

---

# 5. Action 切换的 Combat Phase 边界

Action 切换只允许发生在 **Action Phase 边界**。

禁止在以下阶段中途切换：

```text
Animation
Motion
World
Hit
Finish 中途
```

正常替换：

```text
Combat Tick N
↓
Action Phase start
↓
旧 ActionRuntime.Interrupt()
→ 清理 Active Range / owner / handle
↓
同一个 Action Phase
↓
新 ActionRuntime.Begin()
→ Open Frame 0
↓
Animation → Motion → World → Hit → Finish
```

因此允许同一 Combat Tick 无缝替换，不需要空白 Tick，但永远不允许在后续 phase 中途换 Action。Self Transition 同样遵守该规则。

---

# 6. Runtime 对象模型

```text
Actor
├─ ActionStateManager   = Action arbitration authority
└─ ActionPlayer         = actor-facing playback lifecycle
   └─ ActionRuntime     = 一次 Action execution
      └─ ActionRuntimeScheduler = Action time + timed content lifecycle
```

## 6.1 ActionStateManager

负责：Request、EntryRules、Priority、CancelRules / Windows、Action switching。

## 6.2 ActionPlayer

保持薄，只管理当前播放生命周期，例如 Begin / Stop / Pause / Resume / Speed / CurrentAction / CurrentFrame / IsPlaying。

## 6.3 ActionRuntime

持有本次执行的 `ActionAsset`、Actor、ActionContext、SelfTags、时间状态与 Scheduler；不成为 Animation / Movement / Hit / Tag 的新 Authority。

## 6.4 ActionRuntimeScheduler

只负责：

```text
Action time
Point timing
Range Enter / Tick / Exit timing
AnimationSegment resolution / source time
Duration / completion timing
```

不负责：Gameplay winner、Motion / Rotation / Tag composition、Hit resolution、Action arbitration。

Ownership 固定：

```text
ActionPlayer owns ActionRuntime
ActionRuntime owns ActionRuntimeScheduler
```

命令只向下；Scheduler 只能返回 Running / Completed 等结果，不能反向调用 `ActionPlayer` / `ActionStateManager`。

---

# 7. Action 终止结果

只有三种：

```text
Completed
Interrupted
Aborted
```

- **Completed**：最后一个 Action Frame 完整经过 Animation / Motion / World / Hit / Finish 后自然结束。
- **Interrupted**：Cancel / Replace / Self Transition / Manual Stop / 正常状态切换请求。立即结束 Active Range 并释放 scoped owner / handle；已发生的 Point、damage、impulse、world state 不 rollback。
- **Aborted**：Actor destroyed、Simulation invalid、Runtime exception、Shutdown。只做 best-effort cleanup，不伪装成正常 `Exit(Interrupted)`。

---

# 8. Combat Frame / Action Frame

统一：

```text
CombatSimulation = 60 Hz
Action Frame = Combat Frame 基准
```

`ActionAsset` 不保存自己的 FrameRate。

Gameplay 使用整数 Action Frame；Animation sampling 可以使用连续小数 Action Position。

---

# 9. Action Duration

不保存手工 Duration。

```text
ActionDurationFrames =
max(
    1,
    every AnimationSegment end,
    every Point Frame + 1,
    every Range StartFrame + DurationFrames
)
```

最短 Action = 1 Frame。空 Timeline 也完整执行 Frame 0 后结束。

Muted 内容仍参与 Duration 计算，但不执行、不 Preview。

---

# 10. Frame 生命周期

保留 E3 两阶段 frame transaction：

```text
Action Phase
├─ OpenFrame(N)
│  ├─ Point Execute
│  ├─ Range Enter
│  └─ Active Range Tick
↓
Animation
↓
Motion
↓
World
↓
Hit
↓
Finish
└─ FinishFrame(N)
   └─ Range Exit
```

Range 使用 `[StartFrame, EndExclusive)`。

一帧 Range `[20,21)`：Enter → Tick → World/Hit → Exit。Point 不等于一帧 Range。

Frame transaction 只定义 lifecycle sequencing，不提供 world-state rollback。

---

# 11. Point / Range Runtime

## 11.1 Point

```text
IActionPointRuntime
└─ Execute(ActionRuntimeContext context)
```

到目标 Frame 才 lazy create → Execute → discard。

V1 Point：`ImpulseItem`。

## 11.2 Range

```text
IActionRangeRuntime
├─ Enter(context)
├─ Tick(context, localFrame)
├─ Exit(context, ActionRangeExitReason)
└─ Abort(context)
```

`ActionRangeExitReason` 只暴露：

```text
Completed
Interrupted
```

Range 到 StartFrame 才 lazy create；未来尚未开始的 Item 如果 Action 提前结束，则永远不创建 Runtime。

`Exit` 主要负责正常 cleanup / release；精确帧的一次性 Gameplay 行为用 Point 表达。

---

# 12. Speed / Slow Motion / HitStop

V1 Gameplay speed：

```text
0 <= Speed <= 1
```

暂不支持 Gameplay `Speed > 1`。未来需要时应通过 Combat substep，而不是 Scheduler 在一个 Combat Tick 偷跑多个 Gameplay Frame。

`Speed < 1` 时，只有跨入新的整数 Action Frame 才发生 Point / Range lifecycle；未跨帧时 Active 状态保持，但 Tick 不重复调用。

Animation 使用连续 Action Position 平滑采样。

`Speed = 0`：Action Frame 不推进，Animation source time 保持，Point / Range 无新的 lifecycle transition。Action freeze 不自动等于 World / Gravity / Motor freeze。

---

# 13. Animation 是特殊 Timeline 内容

```text
Timeline
├─ ANIMATION
│  └─ AnimationSegments[]
└─ GAMEPLAY
   └─ GameplayLanes[]
```

Animation 不属于 GameplayLane / GameplayItem。

---

# 14. AnimationAsset

项目标准 Action animation resource：

```text
AnimationAsset
├─ AnimationClip
└─ Baked RootMotionData
```

它只是 resource + derived data bundle，不负责 Play / Stop / Transition / CrossFade / Loop / Timeline。

`AnimationAsset` 不限定只能被 Action 使用；未来其他系统可以复用。

RootMotionData 在 Editor / Bake pipeline 中预先生成或重建，Runtime 只读。缺失 / 无效数据属于 Authoring Validation 问题。

---

# 15. AnimationSegment

```text
AnimationSegment
├─ EditorId
├─ StartFrame
├─ AnimationAsset
├─ SourceStartTime
├─ SourceEndTime
└─ PlayRate
```

Source range 以秒保存。

```text
SourceDurationSeconds = SourceEndTime - SourceStartTime
TimelineDurationSeconds = SourceDurationSeconds / PlayRate
DerivedDurationFrames = ceil(TimelineDurationSeconds * 60)
SegmentEndExclusive = StartFrame + DerivedDurationFrames
```

AnimationSegment 不保存手工 Duration。

V1 支持 Multiple Segments、Gap、Move、Trim、PlayRate；不支持 overlap、crossfade、segment loop、segment mute。

Move 只改 StartFrame；Trim 改 SourceStart / SourceEnd，不偷偷改 PlayRate。

---

# 16. Animation Gap

- 第一个 Segment 前：Action 不提交 pose contribution，`ActorAnimation` 保持原本 / base animation。
- Segment 之间、以及最后 Segment 结束但 Action 继续时：持续采样上一 Segment 的 `SourceEndTime`，保持最终 Pose。

不增加额外 HoldPose runtime type。

---

# 17. Animation Runtime 链

```text
ActionRuntimeScheduler
↓
resolve current AnimationSegment
↓
compute SourceTime from continuous Action time
↓
submit (AnimationClip + SourceTime)
↓
ActorAnimation
↓
Animancer
```

Scheduler 不直接操作 Animancer；`ActorAnimation` 不需要理解 Timeline、Segment 或 RootMotionData。

---

# 18. Pose 与 RootMotion 解耦

共同资源：

```text
AnimationAsset
├─ AnimationClip      → Pose path
└─ RootMotionData     → Gameplay Motion path
```

运行路径完全分离：

```text
AnimationSegment → Scheduler → ActorAnimation
RootMotionItem   → AnimationAsset.RootMotionData → ActorMotor
```

Gameplay RootMotion / SelfRotation 的 timing 独立于 AnimationSegment。

---

# 19. GameplayLane

```text
GameplayLane
├─ EditorId
├─ Name
├─ Muted
└─ Items[]
```

Lane 只服务 Editor 组织，不具有 Type、Priority、ExecutionOrder、RuntimeType、MotionDomain、AllowedItemTypes 等 Gameplay 语义。

不同 Item 可以混放。Runtime 不创建 `GameplayLaneRuntime`。

```text
EffectiveMuted = Lane.Muted || Item.Muted
```

---

# 20. GameplayItem

建议使用基类 + `[SerializeReference]` 多态列表：

```text
GameplayItem
├─ EditorId
└─ Muted

PointGameplayItem
└─ Frame

RangeGameplayItem
├─ StartFrame
└─ DurationFrames
```

V1：

```text
Point: ImpulseItem
Range: HitBoxItem / RootMotionItem / SelfRotationItem /
       VelocityOverrideItem / MotionPolicyItem / TagItem
```

具体 Item 直接持有 inline concrete Config；Runtime 将 Config 视为 immutable。

具体 Item 自己创建 Point / Range Runtime，Scheduler 不维护 `GameplayItemType` switch / registry。

---

# 21. SelfRotation / MotionPolicy

`SelfRotationItem`：

```text
Source = RootMotion / Target / Direction
Mode   = Snap / RotateBySpeed
```

Frame 0 一帧 Range + `Snap` 可表达旧 `facingOnStart`。

`MotionPolicyItem`：

```text
LocomotionScale
AirLocomotionScale
GravityScale
```

旧 `ActionMotionConfig` 删除。映射：

```text
rootMotionMode → RootMotionItem / SelfRotation RootMotion source
suppressLocomotion → MotionPolicyItem LocomotionScale = 0
gravityScale → MotionPolicyItem
facingOnStart → Frame 0 SelfRotationItem Snap
horizontalMomentumInheritance / verticalMomentumInheritance → V1 删除
```

---

# 22. Gameplay Item 并发

> **所有当前 Active Gameplay Item 都执行 / submit。**

```text
Item A ──┐
Item B ──┼→ Receiver / Domain
Item C ──┘
```

最终 composition 属于 `ActorMotor`、TranslationDomain、RotationDomain、MotionPolicyState、TagSystem、HitBoxSystem 等 Receiver。

禁止：GameplayItem.Priority、GameplayLane.Priority、Timeline gameplay ExecutionOrder、Track-order winner semantics。

Gameplay overlap 本身不是 Validation error。

---

# 23. ActionRuntimeContext 权限边界

保持薄：

```text
ActionRuntimeContext
├─ Actor
├─ ActionContext(Target / Direction)
├─ Motor
├─ TagSystem
└─ HitBoxSystem
```

具体字段名实现时再定。

ItemRuntime 可以读取自己的 immutable Config、本次 ActionContext，并向合法 Receiver submit，保存 / 释放自己的 owner / handle。

Context 不提供：

```text
Scheduler
ActionPlayer
ActionStateManager
其他 ItemRuntime
Timeline authoring structure
万能 Service Locator
```

需要 Range local progress 时，由 Scheduler 在 `Tick` 直接传 `localFrame`。

HitBox Item 只负责 HitBox handle 的 active lifetime；真正 Physics / Hit / Damage 仍由 Hit System 在 Hit Phase 统一处理。

Tag Item 只 acquire / release 自己的 owner；同 Tag 多 owner 的最终存在性由 TagSystem 处理。

---

# 24. Scheduler Runtime Snapshot

ActionRuntime Begin 时：

```text
ActionAsset
↓
计算 Duration
构建 Timing / Animation Records
↓
ActionRuntimeScheduler Snapshot
```

概念数据：

```text
PointsByFrame
RangesStartingByFrame
RangesEndingByFrame
ActiveRanges
AnimationRecords
DurationFrames
```

规则：

- Snapshot 只是一份内存 Runtime 数据，不是新的持久 Asset。
- 不创建 TimelineAsset / CompiledActionAsset / ScheduleAsset / RuntimeAsset。
- Snapshot 保存 timing / authoring references，不提前创建所有 ItemRuntime。
- 一次 Action execution 中 Timeline / Config 视为 immutable。
- Runtime 已开始后 Editor 修改 `ActionAsset`，不影响当前 ActionRuntime，只影响下一次新建 Runtime。
- 所有内容参与 Duration；只有非 EffectiveMuted Gameplay 内容进入可执行 records。

---

# 25. EditorId

稳定隐藏 EditorId 保存于：

```text
AnimationSegment
GameplayLane
GameplayItem
```

只用于 Selection、Drag、Reorder、Copy/Paste、Duplicate、Undo、Validation / Repair。

没有 Runtime Gameplay 含义；Scheduler 不依赖它决定 Priority、ExecutionOrder、Owner 或 Runtime identity。

---

# 26. Editor 架构

三个 dockable window：

```text
Action Timeline
Action Details
Action Preview
```

共享 Editor-only Context：CurrentAction、CurrentFrame、Selection、PreviewCharacter；不序列化进 `ActionAsset`。

Editor 直接修改 `ActionAsset`，不存在第二份 Editor Timeline source / Compile roundtrip。

V1 Toolbar：

```text
First / Prev Frame / Play-Pause / Next Frame / Last / Preview Loop
```

没有 Snap 按钮；Action Frame 天然整数 Snap。Ctrl + Wheel mouse-anchored zoom。

V1 支持：

```text
Create via context menu
Point move
Range move / resize
Animation trim
vertical lane move
Delete / Duplicate / Copy-Paste
Single / Multi / Marquee selection
Group move
Lane add / rename / reorder / delete
Lane mute / Item mute
Undo / Redo
CurrentFrame scrub
```

V1 不做 multi-resize。删除非空 Lane 必须确认，不静默搬移 Item。

Interaction invariants：Pointer → Timeline content coord → Action Frame；Drag = Original Snapshot + Total Delta；连续操作用 Pointer Capture；单一 interaction state；一次 manipulation = 一个 Undo transaction；Selection 为共享 Editor state。

---

# 27. Preview

独立 Unity Preview Scene。

Preview 是 **current-frame visual evaluator**，不是从 Frame 0 replay 到 CurrentFrame 的 Gameplay simulation。

V1 顺序：

```text
Animation Pose
→ RootMotion Transform
→ SelfRotation
→ Active HitBox Gizmo
```

支持 Animation Pose、RootMotion current position/path、SelfRotation facing、Active HitBox。

不模拟 Impulse / Tag / Velocity / Damage history。Muted 内容不 Preview。

---

# 28. Validation

只检查 Authoring Data 本身是否成立：

```text
Missing reference
Range Duration <= 0
Invalid SourceRange
PlayRate <= 0
AnimationSegment overlap
Invalid Config
Missing / malformed / duplicate EditorId
Missing / invalid RootMotionData when required
```

不把 RootMotion / SelfRotation / MotionPolicy overlap 视为通用错误。

---

# 29. 旧系统迁移与退场

目标：

```text
旧 Action Data
↓
一次性 Migration
↓
新 ActionAsset 内嵌 Timeline
↓
验证
↓
删除 Legacy
```

不做长期 Runtime 双轨。

最终退出 Action path：

```text
Legacy Timeline
ActionSequenceData
TrackDefinition
ClipDefinition
ActionSequenceRuntime
IActionPlaybackSession
IFixedActionPlaybackSession
TimelineActionPlaybackSession
SequenceActionPlaybackSession
PlaybackBackend
AnimationPoseClip
ActionMotionConfig
Action-side AnimationConfig / animationKey / TransitionAsset dependency
```

旧 typed Track / Clip 迁移成新 authoring data，但 Track type / TrackIndex / ClipIndex / ExecutionOrder 不进入新模型。

---

# 30. E3 保留内容

继续保留已验证的：

```text
CombatSimulationDriver
ActorAnimation
ActorMotor
TranslationDomain
RotationDomain
MotionPolicyState
HitBox Runtime / Hit System
Tag System
```

替换的是 Action authoring、Action playback 与 Timeline scheduling 层，不推翻底层 Combat Runtime。

---

# 31. Locomotion Animation 延期

Action 路径本轮退出 `AnimationConfig`。

Locomotion 暂时可继续使用现有 `AnimationConfig`，因为 Mixer / Blend Tree / Profile / locomotion-specific blend semantics 需要单独设计。

项目级彻底删除 `AnimationConfig` 不属于本轮 Action V1。

---

# 32. V1 非目标

```text
Generic Action Loop
AnimationSegment Loop
Animation Crossfade
Animation Segment Overlap
Gameplay Speed > 1
Gameplay Priority / ExecutionOrder system
GameplayLane runtime semantics
Long-term Legacy compatibility backend
Locomotion animation redesign
```

---

# 33. Frozen Invariants

1. `ActionAsset` 是唯一持久化 Authoring Source。
2. Timeline 内嵌在 `ActionAsset`。
3. Gameplay frame 固定 60Hz。
4. Duration 自动推导，最短 1 Frame。
5. Generic Action Loop 不进 V1。
6. Animation 是特殊 Timeline 内容，不是 GameplayItem。
7. `AnimationAsset = AnimationClip + Baked RootMotionData`。
8. GameplayLane 只有组织意义。
9. GameplayItem 分 Point / Range。
10. 所有 Active Item 都 submit；最终 composition 属于 Receiver。
11. ItemRuntime 不访问 Scheduler / ActionPlayer / ActionStateManager。
12. Scheduler 只管理时间与 timed lifecycle。
13. Action 切换只发生在 Action Phase 边界。
14. Self Transition 正常走 arbitration；接受时 Interrupt 旧 Runtime 并创建新 Runtime。
15. Scheduler 在 Action Begin 时构建轻量 Snapshot；一次 execution 中 authoring data 视为 immutable。
16. ItemRuntime lazy-create。
17. Config 为 inline serialized immutable authoring data。
18. Editor 直接编辑 ActionAsset；EditorId 只有 Editor 语义。
19. Preview 是 current-frame visual evaluator。
20. 不做长期 Legacy 双轨；迁移后删除旧 Sequence / Playback backend。
21. 保留 E3 已验证的 ActorAnimation / ActorMotor / Hit / Tag Domain authority 与 phase ordering。

---

# 34. 配套实施文档

实施顺序、旁路验证、Asset Migration、Cutover、Legacy 删除与各 Stage Exit Criteria 由：

`Docs/Proposals/CombatSample_Action_Implementation_Roadmap_v1_zh-CN.md`

负责。

该 Roadmap 不得重新引入本文已经冻结并删除的长期兼容层、typed lane、generic loop、Gameplay priority、ReenterRule 或第二份 authoring source。
