# CombatSample Action 实施路线图 v1

> 状态：**Approved Implementation Roadmap**  
> 目标分支：`FrameWork`  
> 日期：2026-08-30  
> 计划路径：`Docs/Proposals/CombatSample_Action_Implementation_Roadmap_v1_zh-CN.md`

> 2026-09-08 状态说明：Stage 0–4 与 Timeline / Details 已形成 [Preview 前编辑器检查点](../Current/CombatSample_Action_V1_Editor_PrePreview_Checkpoint_2026-09-08_zh-CN.md)。Editor Preview 实现已撤下并暂停重新设计，因此不得自动进入 Preview 后续切片、Stage 5R.7、Stage 6 或 Stage 7。

## 0. 文档目的与权威关系

本文回答：

> **如何把当前 `FrameWork` 中已经验证的 E3 Action / Sequence 基线，安全迁移到《CombatSample Action 最终架构 v1》定义的新 Action 架构。**

本文不重新设计架构。架构原则以 `CombatSample_Action_Final_Architecture_v1_zh-CN.md` 为准。

如果实施方便性与冻结架构冲突：

- 优先调整实施方案；
- 不允许重新引入长期 `TrackKind`、Gameplay Priority、Legacy Runtime fallback、第二份 Timeline Asset、`ReenterRule`、Generic Action Loop 等已删除结构；
- 如果发现架构本身有真实问题，单独发起 Architecture Review，不在实施过程中静默改规则。

当前 E3 已验证的 `CombatSimulationDriver`、`ActorAnimation`、`ActorMotor`、Motion Domain、Hit、Tag 与 phase ordering 继续作为迁移基线。

---

# 1. 总体实施原则

## 1.1 每个 Stage 都保持项目可运行

```text
旧链继续可用
+
新链逐步建立
↓
验证
↓
一次明确 Cutover
↓
删除旧链
```

这是迁移期施工双轨，不是长期 Runtime 双轨。

## 1.2 新 Runtime 先旁路完成，再切主链

```text
新 Authoring Data
↓
ActionRuntime / Scheduler
↓
Gameplay Item Runtime
↓
Animation Runtime
↓
New Editor
↓
Asset Migration
↓
全量验证
↓
One-way Cutover
↓
Legacy Removal
```

## 1.3 回滚依赖版本控制

Stage 7 之后如果出现问题，使用 Git / branch 回退，不在产品代码中保留 Legacy fallback。

## 1.4 每个 Stage 必须满足 Exit Criteria

没有通过当前 Stage，就不进入下一 Stage。

---

# 2. Stage 0 — Baseline & Inventory

## Goal

固定当前 `FrameWork` 基线，完整清点旧 Action authoring / playback 依赖。

## Scope

记录：

```text
当前 FrameWork HEAD
Combat 60Hz
Driver phase order
当前 Action / Animation / Motion / Hit authority
当前 E3 validation 场景与验证方法
```

清点准备退出的结构及其引用：

```text
ActionSequenceData
TrackDefinition
ClipDefinition
ActionSequenceRuntime
PlaybackSession / PlaybackBackend
Legacy Timeline
ActionMotionConfig
Action-side AnimationConfig
animationKey
TransitionAsset
AnimationPoseClip
以及 Stage 0 实际扫描发现的其他 Legacy Action symbols
```

清点所有现有 `ActionAsset`：是否 Sequence / Legacy、使用哪些旧 Clip / Motion / Animation 数据。

把 `AnimationConfig` 使用点区分为 Action 与 Locomotion。

## Non-Goals

不改 ActionAsset 模型、不建 Scheduler、不迁 Asset、不删旧 Runtime、不改 Editor、不改 Animation 播放链。

## Guardrail

Stage 0 后旧系统允许继续运行，但禁止新功能继续增加对旧 Action authoring / playback API 的依赖。

## Deliverables

1. Current Baseline。
2. Legacy Dependency Inventory。
3. Action Asset Migration Inventory。

## Validation / Exit Criteria

1. 当前项目编译通过，E3 基线可重复。
2. 所有目标 ActionAsset 已进入迁移清单。
3. Sequence / Legacy 使用情况明确。
4. Action-side `AnimationConfig` 使用点明确。
5. 所有计划删除的 Runtime / Editor / Data 类型都有依赖清单。
6. 形成 Stage 7 Residual Audit 的完整 Legacy symbol 集合。

---

# 3. Stage 1 — New Authoring Data

## Goal

先落地冻结架构的数据结构与 `AnimationAsset` 资源 / Bake pipeline，不切 Runtime。

## Scope

建立：

```text
ActionAsset
└─ Timeline
   ├─ AnimationSegment[]
   └─ GameplayLane[]
       └─ GameplayItem[]
```

建立：

```text
AnimationAsset
├─ AnimationClip
└─ Baked RootMotionData

AnimationSegment
GameplayLane
GameplayItem
PointGameplayItem
RangeGameplayItem
```

V1 Item：

```text
ImpulseItem
HitBoxItem
RootMotionItem
SelfRotationItem
VelocityOverrideItem
MotionPolicyItem
TagItem
```

GameplayLane 使用 `[SerializeReference]` 多态 `GameplayItem` 列表；具体 Item 直接持有 inline concrete Config。

稳定隐藏 EditorId 放在 AnimationSegment / GameplayLane / GameplayItem。

## AnimationAsset Bake / Rebuild

Stage 1 就建立：

```text
AnimationClip
↓
Bake / Rebuild
↓
Baked RootMotionData
```

Runtime 不现场 Bake。后续 Stage 3 的 RootMotionItem 可以直接读取已验证的 RootMotionData。

基础 Validation：

```text
Missing reference
Invalid timing
Invalid SourceRange
PlayRate <= 0
Animation overlap
Invalid Config
Missing / duplicate EditorId
Missing / invalid RootMotionData when required
```

## Non-Goals

不切 Runtime、不执行 Gameplay Item、不播放新 AnimationSegment、不迁旧 Asset、不删旧 Sequence。

## Validation / Exit Criteria

1. 新 Timeline 可稳定 Serialize / Deserialize。
2. `[SerializeReference]` 多态 Item 可正确保存、Domain Reload、Undo / Redo。
3. EditorId 在 reorder / reload 后稳定。
4. AnimationAsset Bake / Rebuild 可重复，RootMotionData 可验证。
5. 新数据没有改变现有 Runtime 行为。

---

# 4. Stage 2 — ActionRuntime & Scheduler Core

## Goal

先把“一次 Action 怎么开始、怎么推进时间、怎么结束”做正确。

## Ownership

```text
ActionPlayer owns ActionRuntime
ActionRuntime owns ActionRuntimeScheduler
```

生命周期命令只向下；Scheduler 不反向调用 `ActionPlayer` / `ActionStateManager`。

## Scope

建立：

```text
ActionRuntime
ActionRuntimeContext
ActionRuntimeScheduler
IActionPointRuntime
IActionRangeRuntime
ActionRangeExitReason
```

实现：

```text
60Hz Action Frame
Continuous Action Position
Duration derive
Runtime Snapshot
Point timing
Range Enter / Tick / Exit
OpenFrame / FinishFrame
Completed / Interrupted / Aborted
Speed 0~1
```

## Runtime Snapshot

ActionRuntime Begin 时：

```text
ActionAsset
↓
计算 Duration
构建 Timing / Animation Records
↓
Scheduler Snapshot
```

一次 execution 中 authoring data 视为 immutable。Editor 在运行中改 ActionAsset 不影响当前 Runtime，只影响下一次新建 Runtime。

Snapshot 不提前创建全部 ItemRuntime，只保存 timing / authoring references。

## Lazy Runtime

```text
Point 到 Frame → Create → Execute → discard
Range 到 StartFrame → Create → Enter → Active → Exit / Abort → discard
```

未来尚未开始的 Item 如果 Action 提前结束，不创建 Runtime。

## Frame Lifecycle

```text
Action Phase
├─ OpenFrame(N)
│  ├─ Point Execute
│  ├─ Range Enter
│  └─ Active Range Tick
↓
Animation → Motion → World → Hit
↓
Finish
└─ FinishFrame(N)
   └─ Range Exit
```

Range 使用 `[StartFrame, EndExclusive)`；最后一个 Action Frame 必须完整经过 Hit / Finish 才能 Completed。

## Action Switching Boundary

Action 替换只允许发生在 **Action Phase 边界**：

```text
Action Phase start
→ old Runtime Interrupt + cleanup
→ same Action Phase new Runtime Begin / Frame 0
→ Animation → Motion → World → Hit → Finish
```

禁止在 Animation / Motion / World / Hit / Finish 中途切换。

因此同一 Combat Tick 可以无缝替换，没有空白 Tick。Self Transition 同样适用。

## Lifecycle Result

- Natural end：Scheduler 返回 Completed，由 ActionRuntime 完成 SelfTags 等生命周期，再由 ActionPlayer观察结果。
- Interrupt：Active Range `Exit(Interrupted)`，释放 scoped owner / handle。
- Abort：Active Range `Abort()`，best-effort cleanup，不伪装成正常 Exit。

## Test Strategy

先用测试专用 Point / Range runtime fixture 验证 Scheduler，不急着接真实 Domain。

## Non-Goals

真实 RootMotion / HitBox / Tag / MotionPolicy、ActorAnimation 提交、Timeline Editor、Asset Migration。

## Validation / Exit Criteria

必须覆盖：

```text
Point exactly once
one-frame Range
multi-frame Range
Range 在最后有效 Hit 后 Exit
empty Action = 1 Frame
last frame full Finish
Interrupt cleanup
Abort cleanup
Speed = 0
Speed = 0.5
Muted content affects Duration but does not execute
lazy runtime creation
runtime snapshot immutability
same-tick Action replacement at Action Phase boundary
禁止 mid-phase switch
```

Scheduler 不持有 arbitration 权限、不直接访问 Gameplay Domain、不反向控制上层。

---

# 5. Stage 3 — Gameplay Item Runtime Integration

## Goal

把真实 GameplayItemRuntime 接入现有 E3 Receiver / Domain，同时保持 Scheduler 纯粹。

## Integration Order

按调试复杂度建议：

```text
TagItem
→ MotionPolicyItem
→ ImpulseItem
→ VelocityOverrideItem
→ HitBoxItem
→ RootMotionItem
→ SelfRotationItem
```

这是施工顺序，不是 Gameplay priority。

## Core Rule

```text
Scheduler → ItemRuntime → Receiver / Domain
```

禁止在 Scheduler 内加入 RootMotion / HitBox / Tag 等特判。如果 Item 难以接入，优先修正 Receiver API / RuntimeContext 边界。

## ActionRuntimeContext

只提供 Actor、ActionContext(Target / Direction) 与合法 Receiver，例如 Motor / TagSystem / HitBoxSystem。

不提供 Scheduler、ActionPlayer、ActionStateManager、其他 ItemRuntime、万能 Service Locator。

Range local progress 由 Scheduler 在 `Tick` 直接传 `localFrame`。

## Receiver Composition

所有 Active Item 都 submit；最终 composition 属于各 Receiver / Domain，没有 GameplayItem / Lane Priority。

## Validation / Exit Criteria

```text
Tag owner acquire / release
MotionPolicy owner composition
Impulse exactly once
Velocity owner lifetime
HitBox active through Hit Phase
RootMotion reads AnimationAsset.RootMotionData and submits ActorMotor
SelfRotation timing / channel submission
multi-item overlap 全部 submit
Complete / Interrupt / Abort cleanup
```

Scheduler 不出现 Item type switch / Domain 特判。

---

# 6. Stage 4 — Animation Runtime

## Goal

建立新的 Action Pose sampling 链，并让 Pose 与 RootMotion Runtime 完全分离。

## Target Flow

```text
AnimationSegment
↓
ActionRuntimeScheduler
↓
AnimationClip + SourceTime
↓
ActorAnimation
↓
Animancer
```

同时：

```text
RootMotionItem
↓
AnimationAsset.RootMotionData
↓
ActorMotor
```

## AnimationAsset

Stage 1 已建立并完成 Bake / Rebuild。Stage 4 只消费资源，不现场 Bake。

## Animation Sampling

Scheduler 根据 Continuous Action Position：Resolve Segment / gap → 算 local time → 映射 SourceTime → submit `(AnimationClip, SourceTime)`。

`ActorAnimation` 负责 Animancer manual sampling / evaluation；Scheduler 不直接操作 Animancer。

## Gap Semantics

- 第一个 Segment 前：不提交 Pose，保持原本 / base animation。
- Segment 间及最后 Segment 结束后：持续采样上一 Segment 的 SourceEndTime，Hold final pose。

## Speed / HitStop

Gameplay lifecycle 仍只在整数 Action Frame 变化；Animation 用连续位置采样。`Speed = 0` 时 SourceTime 保持。

## Migration Boundary

旧 `animationKey / AnimationConfig / TransitionAsset` 暂时继续服务旧 Sequence，到 Stage 6 / 7 再迁移和删除 Action-side dependency。

## Validation / Exit Criteria

```text
single Segment
multiple Segments
before-first gap
between-Segment gap
hold final pose after last Segment
Trim
PlayRate
Speed = 0.5 continuous sampling
Speed = 0 hold
ActorAnimation 不理解 Timeline / RootMotionData
RootMotion 不由 ActorAnimation 自动产生
```

---

# 7. Stage 5 — New Action Editor

## Goal

在 Data / Runtime 稳定后建立唯一的新 Action authoring workflow。

## Windows

```text
Action Timeline
Action Details
Action Preview
```

共享 Editor-only Context：CurrentAction、CurrentFrame、Selection、PreviewCharacter；不写入 ActionAsset。

## Implementation Order

```text
Timeline shell
→ frame ruler / scroll / zoom / scrub
→ GameplayLane
→ selection
→ Point / Range move
→ Range resize / Animation trim
→ Details
→ Mute
→ Undo
→ Copy / Paste / Duplicate
→ Preview
→ Validation UI
```

## Required V1 Interaction

必须验收：

```text
Single / Multi / Marquee Selection
Point Move
Range Move / Left-Right Resize
Group Move
Animation Trim
Copy / Paste / Duplicate
Lane Add / Rename / Reorder / Delete
Lane Mute / Item Mute
Undo / Redo
CurrentFrame Scrub
Ctrl + Wheel mouse-anchored zoom
```

V1 不做 multi-resize。

Toolbar：

```text
First / Prev Frame / Play-Pause / Next Frame / Last / Preview Loop
```

没有 Snap 按钮；整数 Frame 天然 Snap。

## Interaction Invariants

Pointer → Timeline content coord → Action Frame；Drag = Original Snapshot + Total Delta；连续操作用 Pointer Capture；单一 interaction state；一次 manipulation = 一个 Undo transaction；Selection 为共享 Editor state。

## Preview

独立 Preview Scene，current-frame evaluator：

```text
Animation Pose
→ RootMotion Transform
→ SelfRotation
→ Active HitBox Gizmo
```

不 replay Impulse / Tag / Velocity / Damage history。

## Exit Criteria

1. Timeline / Details / Preview 可完成 V1 authoring workflow。
2. Editor 直接修改 ActionAsset，无第二份 Timeline 数据。
3. 上述交互清单全部通过。
4. Stable EditorId、Undo、copy-paste、Preview、Validation 稳定。
5. 新 Editor 足以支持正式 Asset Migration。

---

# 8. Stage 6 — Asset Migration

## Goal

把现有 ActionAsset 的旧 Sequence / Animation / Motion 数据就地迁到新 Timeline，同时保持 GUID 与外部引用不变。

## Core Strategy

```text
原 ActionAsset（GUID 不变）
├─ old data      // migration period only
└─ new Timeline  // migration result
```

迁移期允许短暂双数据，但新 Runtime 只认新 Timeline，不建立长期 fallback。

## Dry Run First

Migration Tool 必须先支持 Dry Run，只报告：会创建哪些 Segment / Item、哪些字段转换、哪些 intentional drop、哪些 unsupported、哪些 AnimationAsset 创建 / 复用。

Apply 后才写 Asset。

## Conversion

```text
AnimationPoseClip → AnimationSegment
HitBoxClip → HitBoxItem
RootMotionClip → RootMotionItem
旧 whole-action motion → MotionPolicyItem / SelfRotationItem / RootMotionItem
```

旧 Track 可生成同名 GameplayLane 仅用于视觉组织，但不迁 TrackKind / TrackIndex / ClipIndex / ExecutionOrder / Legacy runtime semantics。

## AnimationAsset Migration

旧：

```text
animationKey → AnimationConfig → AnimationClip + RootMotionData
```

新：

```text
AnimationAsset = AnimationClip + RootMotionData
```

相同资源尽量复用同一 AnimationAsset。

## No Silent Data Loss

所有 intentional drop 必须报告，例如 horizontal / vertical momentum inheritance。

真正无法转换：`Unsupported → Migration Failed`，不能跳过后显示 Success。

如果目标 ActionAsset 已有非空新 Timeline，默认拒绝覆盖；只有显式 Overwrite / Rebuild 才允许。

迁移时给新 AnimationSegment / GameplayLane / GameplayItem 生成稳定 EditorId。

## Migration Report

每个 Action 有独立 Report；项目级汇总 Total / Success / Warning / Failed。只要 `Failed > 0` 就不能进入 Stage 7。

## Differential Validation

比较行为，不要求内部数据结构相同：

```text
Action frame length
Animation timing
HitBox active frames
RootMotion
SelfRotation
MotionPolicy
Velocity
Impulse
Tags
Cancel timing
Completion timing
```

明确删除的行为按 Report 记录，不要求等价。

## Exit Criteria

1. 所有目标 ActionAsset 已迁移且 GUID / 外部引用不变。
2. Migration Failed = 0。
3. 所有 drop / warning 有明确报告。
4. AnimationAsset 全部生成或正确复用。
5. 新 Timeline Validation 全部通过。
6. E3 关键行为 differential validation 通过。
7. 新 Runtime 可以完整运行所有 Active ActionAsset。

---

# 9. Stage 7 — Cutover & Legacy Removal

## Goal

让新 Action Runtime 成为唯一正式执行路径，并彻底移除旧 Action authoring / playback 系统。

## One-way Cutover

```text
Stage 6 全量通过
↓
ActionPlayer 切唯一新 Runtime
↓
完整回归
↓
删除 Legacy
```

进入 Stage 7 后 Runtime 不保留旧 Sequence fallback。

## Runtime Cutover

最终正式链：

```text
ActionStateManager
→ ActionPlayer
→ ActionRuntime
→ ActionRuntimeScheduler
```

## Removal Group A — Old Runtime

删除：

```text
ActionSequenceRuntime
IActionPlaybackSession
IFixedActionPlaybackSession
SequenceActionPlaybackSession
TimelineActionPlaybackSession
PlaybackBackend
```

## Removal Group B — Old Authoring Data

删除：

```text
ActionSequenceData
TrackDefinition
ClipDefinition
TrackKind
ClipKind
Legacy Timeline fields
ActionMotionConfig
animationKey
AnimationPoseClip
```

最终 ActionAsset 不长期保留 `[Obsolete] oldSequenceData` 等兼容字段。

## Removal Group C — Old Editor

删除旧 Sequence Editor、typed Track / Clip editor、旧 inspectors、Legacy Timeline authoring UI。新 Timeline / Details / Preview 成为唯一正常入口。

## Action-side AnimationConfig

删除 Action → AnimationConfig / animationKey / TransitionAsset dependency。

Locomotion 如果仍依赖 `AnimationConfig` 可暂时保留，属于后续独立重构。

## Migration Tool

从普通工作流退出；可短期放在明确 Legacy / Migration 目录用于历史修复，但不参与 Runtime、不自动运行、不进入普通 Action Editor。

## Legacy Residual Audit

Residual Audit = **固定 Legacy Symbol 清单 + Stage 0 Inventory 实际发现的完整 Legacy 集合**。

每个残留都必须分类：

```text
删除
Legacy migration history
Locomotion 合法依赖
Archive 文档历史记录
```

不允许未知 Current-code / Runtime 残留。

## Documentation Cutover

Stage 7 同时处理文档状态：

- 旧 Sequence Editor Current 文档转入 Archive。
- 更新 `Docs/README.md` 的 Architecture Authority / Current / Proposals / Archive 关系。
- 迁移完成后，新 Action Architecture 成为已实现 Action authoring / playback 权威；Roadmap 转入 Archive 或标记 Completed。

## Final Runtime Regression

至少覆盖：

```text
Action enter / cancel / self transition
same-tick Action Phase replacement
Animation timing
HitBox active frames
RootMotion / SelfRotation
Velocity / Impulse
MotionPolicy / Tag lifetime
Slow motion / HitStop
Completed / Interrupted / Aborted
multi-actor
last-frame Hit / Finish ordering
```

## Exit Criteria

1. 所有 Active ActionAsset 只使用新 Timeline。
2. ActionPlayer 只有新 ActionRuntime 路径。
3. Runtime 不存在 Legacy fallback。
4. 旧 Sequence playback 类型全部删除。
5. 旧 typed Track / Clip authoring model 全部退出。
6. ActionMotionConfig 完全退出。
7. Action-side AnimationConfig / animationKey 完全退出。
8. 新 Editor 是唯一 Action authoring 入口。
9. Legacy residual audit 完成。
10. Documentation cutover 完成。
11. 全量 Runtime / Asset / Editor 回归通过。

---

# 10. Final Acceptance

整个 Action V1 Migration 只有在以下层面全部通过时才结束。

## Runtime

`ActionStateManager → ActionPlayer → ActionRuntime → ActionRuntimeScheduler` 成为唯一正式链；Frame lifecycle、Action Phase switching、slow motion、interrupt / abort、Receiver composition、Animation sampling 全部符合冻结架构。

## Assets

所有 Active ActionAsset 已迁移；无旧 Sequence / Legacy runtime-required 数据；AnimationAsset 完整；Authoring Validation 全部通过。

## Editor

Timeline / Details / Preview 为唯一正常工作流；不需要旧 Track / Clip Editor；V1 interaction、Undo、EditorId、Preview、Validation 稳定。

## Legacy Removal

旧 Runtime、旧 authoring model、旧 Action-side Animation path 已删除或明确只存在于 Archive / Legacy Migration 范围。

## Documentation

Docs 权威关系反映实际完成状态；旧 Sequence Current 文档不再被误认为现行 Action editor / playback 权威。

---

# 11. 非本路线图范围

```text
Generic Action Loop
AnimationSegment Loop
Animation Crossfade / overlap
Gameplay Speed > 1
Gameplay Priority / ExecutionOrder system
GameplayLane runtime semantics
长期 Legacy compatibility backend
Locomotion Animation redesign
项目级彻底删除 AnimationConfig
```

---

# 12. 最终目标结构

```text
ActionAsset = Action rules + 唯一 Timeline authoring data
ActionStateManager = Action arbitration
ActionPlayer = actor-facing playback lifecycle
ActionRuntime = 一次 Action execution
ActionRuntimeScheduler = Action time + timed content lifecycle
GameplayItemRuntime = submit contribution
AnimationSegment = pose sample timing
AnimationAsset = AnimationClip + Baked RootMotionData
ActorAnimation / ActorMotor / TagSystem / HitBoxSystem / ... = 各 Domain 最终 Authority
```

本路线图完成的标准不是“新系统能跑”，而是：

> **新系统成为唯一正式 Action 路径，旧 Action authoring / playback 结构已经退出，同时 E3 已验证的底层 Domain 与关键 Gameplay 行为没有被迁移过程破坏。**
