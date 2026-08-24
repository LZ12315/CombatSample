# CombatSample E3 Architecture Migration Roadmap

> 状态：**Active Migration Roadmap**。本文服从 [`Final Architecture v3`](CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md)，用于定义 E3 的阶段边界、依赖、迁移约束与 Exit Criteria。
>
> 日期：2026-08-25
>
> 代码基线快照：`FrameWork @ c1fda94e50aca23abe80243a681586a4e93a3a75`
>
> 本文**不是 Codex 细粒度 Implementation Plan**。具体文件、类、方法、测试实现、临时 adapter 与 commit 切片，应在每个 Stage 开始时由 Codex Plan Mode 基于当时最新代码重新规划。

---

# 1. 文档定位

E3 的三层工作分工固定为：

```text
Final Architecture v3
= 最终应该是什么
= Domain / Authority / Arbitration / Phase Contract

        ↓

E3 Architecture Migration Roadmap
= 按什么依赖顺序迁移
= 每个 Stage 解决哪些 Implementation Gaps
= 允许暂时保留什么
= Stage 完成时必须满足什么

        ↓

Codex Plan Mode
= 当前 Stage 具体怎么改
= 文件 / 类 / 方法 / 测试 / adapter / commit 顺序
```

本文回答：

- E3 分成哪些可验证的 Stage；
- Stage 之间的硬依赖；
- 每个 Stage 的 authority cutover 边界；
- 哪些旧路径可以暂时作为 compatibility source；
- 每个 Stage 的完成标准。

本文不冻结：

```text
具体文件拆分
具体 public/private API 名称
字段与容器类型
逐行迁移步骤
测试代码实现方式
临时 adapter 的具体形态
每个 commit 修改哪些文件
```

如果 Codex 审查当前代码后发现更安全的局部实现顺序，可以在一个 Stage 内调整；如果发现需要跨越另一个 Stage 的 authority boundary，先更新 Roadmap / 做显式 review，而不是把多个 Stage 静默揉成一次大改。

---

# 2. Authority 与冲突处理

优先级：

```text
Final Architecture v3
>
本 Roadmap 的 Stage 边界 / Exit Criteria
>
当前代码事实（描述“现在是什么”）
>
Codex 具体实现方案
```

规则：

- 当前代码与 v3 冲突，默认是 Implementation Gap。
- Roadmap 不得重新设计已冻结的 v3 Domain / authority / arbitration / 7-Phase Model。
- 如果实现事实证明某条 v3 规则不可成立，必须停止并显式进入 Architecture Review。
- 如果只是具体 API、类拆分、文件组织不同，不需要重新开放架构；Codex 可在 Stage 边界内选择更合适的实现。

---

# 3. 当前实现基线快照

以下只是 2026-08-25 的迁移起点，不是长期 authority。

## 3.1 Simulation Spine

当前 `CombatSimulationDriver` 已经拥有显式 fixed tick、KCC simulation ownership、Actor snapshot、Action → World → Hit → Finish 的基础 barrier；`ActorSimulationRuntime` 已作为每个 Actor 的单一 fixed-simulation 入口，但目前主要路由 Action playback 与 HitBox。

因此 E3 不需要推翻 Driver / Runtime，而是把它们收敛到 v3 的正式 7-Phase Model。

## 3.2 ActorMotor

当前 `ActorMotor` 仍围绕：

```text
LocomotionRuntime
FacingRuntime
ActorMotionRuntime
```

组织；Gameplay motion 的大部分状态推进和 Compose 仍发生在 KCC callbacks 中。

已有可复用基础包括：

- Horizontal / Vertical velocity owner 已有 LIFO 形态；
- HorizontalImpulse 已是独立状态；
- RootMotionTrajectory 已能按 Sequence 区间抽取 XZ；
- Grounding / VelocityReadout 已有独立实现经验；
- MovementTimeScale 已在 Gravity / Impulse temporal evolution 中被考虑。

主要差距包括：

- 自由垂直运动仍是 `GravityAccumulator + VerticalImpulseVelocity`；
- Root Motion 与 Scripted / Root Rotation ownership 尚未按 v3 分域；
- Root Motion 分支仍会叠加 HorizontalImpulse；
- Animator Gameplay Root Motion 仍存在；
- gameplay motion preparation 尚未正式成为 Driver 的 Motion Phase。

## 3.3 Animation

当前 `ActionSequenceAnimationPoseClipDefinition` 仍直接：

```text
actor.animancer.Play(...)
state.Time = sampleTime
actor.animancer.Evaluate()
```

还没有正式 `ActorAnimation` authority，也没有 persistent Locomotion Base + temporary Action Override 的统一入口。

## 3.4 Action / Sequence

当前 ActionSequence 已有：

```text
AnimationPose
HitBox
Impulse
RootMotion
SelfRotation
Tag
VelocityOverride
```

等 Clip 基础。

其中：

- RootMotionClip 已使用 baked `RootMotionTrajectory`；
- SelfRotationClip 已能在 producer 侧解析 RootRotation / Target / Direction / Snap / RotateBySpeed 语义，但最终仍提交到旧的统一 SelfRotation owner；
- VelocityOverrideClip 已有 scoped horizontal / vertical owner 生命周期；
- ImpulseClip 已是事件式写入的雏形。

因此 E3-F 的重点是**把已有 Clip 迁到 v3 native channel contract**，不是重新发明整个 Sequence 系统。

## 3.5 Whole-Action Motion ownership

`ActionInstance.OnEnter / OnExit` 仍通过 `ActionMotionConfig` 做：

```text
ClearVelocityOwners
ApplyMotionHandoff
SetRootMotionApplyMode
SetLocomotionSuppressed
SetGravityScale
SnapFacing
```

以及结束时 restore。

这是 v3 明确要求退出长期 authority 的旧路径，但由于已有资产仍可能序列化该配置，必须先建立替代 contribution，再迁内容，最后清理。

## 3.6 尚未建立的 v3 Domain

当前没有正式的：

```text
ActorLocomotion
ActorAnimation
MotionPolicy
```

这些属于 E3 的新增稳定边界，而不是另建 Generic Runtime Framework。

---

# 4. 全局迁移原则

所有 Stage 都必须遵守以下规则。

## 4.1 每个 Stage 都必须可验证

Stage 完成后项目应处于可编译、可进入目标验证场景、核心战斗路径可运行的状态。

不接受：

```text
“先把旧系统全部拆掉，后面几个 Stage 再恢复可运行”
```

## 4.2 同一职责只能有一个 authority

迁移可以暂时保留 compatibility API / serialized field，但不能长期形成双权威。

允许：

```text
旧 API
→ adapter
→ 新 Domain
```

不允许：

```text
旧 Runtime 自己算一份
+
新 Domain 再算一份
→ 最后靠调用顺序决定谁生效
```

## 4.3 Compatibility 只能单向收敛

旧结构可以作为迁移输入，但新实现不得反向依赖旧 architecture semantics。

典型：

```text
ActionMotionConfig serialized data
→ 临时迁移/兼容读取
→ native Clip / Policy contribution
```

而不是让新的 MotionPolicy 再包装成旧 whole-action MotionConfig。

## 4.4 资产安全优先于代码洁癖

仍被 Unity asset / prefab 引用的字段、类型、GUID 不提前删除。

顺序必须是：

```text
replacement path exists
→ content migrated
→ references verified
→ legacy runtime authority disabled
→ safe cleanup
```

## 4.5 Driver 不随 subsystem 数量膨胀

整个 E3 始终保持：

```text
Input / Control
→ Action
→ Animation
→ Motion
→ World
→ Hit
→ Finish
```

Driver 只拥有 World Phase Order；per-Actor subsystem 只能通过 `ActorSimulationRuntime` 进入。

不建立 generic callback / listener registry。

## 4.6 不重新实现已经成立的数据管线

`RootMotionTrajectory` 的累计数据合同、Sequence 区间抽取、Editor bake 基础属于应保留资产。

E3 重点是迁 runtime consumption / authority，不因为 Motor 重构就顺手重写 Baker。

## 4.7 Stage 是 Gate，不是 commit 数量

一个 Stage 可以由 Codex 拆成多个小 plan / commit。

只有当该 Stage 的 Exit Criteria 全部成立后，才视为通过 Gate 并进入下一 Stage。

---

# 5. Stage Dependency Map

硬依赖关系：

```text
E3-A  Simulation Spine / 7-Phase Façade
  │
  ├──────────────→ E3-D  ActorAnimation Authority
  │
  ▼
E3-B  ActorMotor Domain Foundation
  │
  ▼
E3-C  Motion Ownership / Arbitration / MotionPolicy
  │                    │
  │                    │
  └──────────┐         │
             ▼         │
      E3-E  ActorLocomotion
             ▲         │
             └── E3-D ─┘

E3-C + E3-D + E3-E
          ↓
E3-F  ActionSequence Contribution Migration
          ↓
E3-G  Authority Cutover / Legacy & Content Migration
          ↓
E3-H  Final Validation / Documentation Handoff
```

单分支推荐执行顺序：

```text
A → B → C → D → E → F → G → H
```

`E3-D` 在依赖上可以较早进行，但单分支顺序仍建议放在 Motor 基础稳定之后，减少同时存在多个大型重构面的调试成本。

---

# 6. E3-A — Simulation Spine / 7-Phase Façade

## Goal

让现有 Driver / `ActorSimulationRuntime` 正式表达 v3 的 7-Phase Model，同时尽量保持现有 gameplay 行为不变，为后续 Domain 迁移提供稳定插槽。

## Scope

- `CombatSimulationDriver` 的一级结构收敛到 7 个 Phase；
- tick snapshot / KCC interpolation 保持为 phase 外边界操作；
- `ActorSimulationRuntime` 补齐少量明确的 per-Actor phase routing；
- 保留现有 KCC world simulation ownership、collision resolver、Physics sync、HitBuffer barrier；
- 为后续 per-Actor simulation dt / HitStop time contract 保留清楚入口。

## Dependencies

只依赖 Approved Final Architecture v3 与当前已存在的 Driver / Runtime 基础。

## Migration Boundary

本 Stage **不要求**立即完成：

```text
ActorMotor 新 Domain
ActorAnimation
ActorLocomotion
Sequence Clip 新 contract
```

旧逻辑可以通过 Runtime phase entry 暂时桥接；但 Driver 本身不能因此直接认识这些内部 subsystem。

## Required Architecture Contracts

- Driver 只拥有 World Phase Order；
- per-Actor execution 只通过 `ActorSimulationRuntime`；
- `ALL Actors Phase A → ALL Actors Phase B`；
- Action Phase 内保留 `ALL Decide → ALL Advance` sub-barrier；
- Hit Phase 保留 `ALL Query → Resolve`；
- 不建立 generic scheduler / callback registry。

## Exit Criteria

- 顶层 fixed step 可以清楚映射到 `Input/Control → Action → Animation → Motion → World → Hit → Finish`；
- Driver 没有新增对 `ActorLocomotion / ActionStateManager / ActionPlayer / ActorAnimation / ActorMotor / HitBoxRuntime` 的直接编排依赖；
- 当前 Action decision、Sequence advance、KCC、collision resolution、Physics sync、Hit query / resolve、Finish 的相对语义没有被破坏；
- `ActorSimulationRuntime` 仍是唯一 per-Actor fixed-simulation boundary；
- 现有验证场景在行为保持目标下可运行。

## Deferred to Codex Plan Mode

- phase helper 是否拆方法；
- Runtime phase entry 的最终方法名；
- 现有 `PlayActionFrame / FinishActionFrame` 如何映射到新 façade；
- error / abort cleanup 的具体组织。

---

# 7. E3-B — ActorMotor Domain Foundation

## Goal

把 Actor movement 的长期心智模型从旧 Runtime 聚合迁到：

```text
ActorMotor
├─ LocomotionRunner
├─ Translation
├─ Rotation
└─ Supporting State
```

并让 Gameplay motion preparation 在 **Motion Phase** 完成，KCC 回归 world movement solver / adapter 角色。

## Scope

- 建立 `LocomotionRunner` 的纯内部职责；
- 建立 first-class Translation / Rotation Domain；
- 固定 Translation 的单位与坐标空间 contract；
- 自由垂直运动迁为单一 `BallisticVerticalVelocity`；
- 保留 Grounding / VelocityReadout 作为 Supporting State；
- 在 World/KCC 前产出 `RequestedVelocity / RequestedRotation`；
- world solve 后发布 Actual / solved movement readout；
- 保留 ForceUnground 等 KCC 所需桥接能力。

## Dependencies

- E3-A 已通过。

## Migration Boundary

允许暂时保留：

```text
LocomotionRuntime
FacingRuntime
ActorMotionRuntime
RootMotionBuffer
SelfRotationBuffer
现有 public Motor API
```

作为迁移 adapter / 数据来源，但它们不能继续定义长期 Domain 模型。

本 Stage 不要求完成所有 owner stack / MotionPolicy / Sequence Clip 迁移；这些属于 E3-C / E3-F。

## Required Architecture Contracts

- Producer 解析 Gameplay 语义，Motor 只接收 native motion data；
- Locomotion / HorizontalImpulse / HorizontalVelocityOwner = world planar velocity；
- Root Motion = local planar displacement / current interval；
- vertical = CharacterUp scalar velocity；
- `Requested Motion != Actual Motion`；
- blocked displacement 不积欠；
- Ballistic 不认识 Jump / Hit / Launcher 的 cause。

## Exit Criteria

- Gameplay 的 Translation / Rotation request 在 World Phase 前已经确定；
- KCC callback 不再是 Gravity、Locomotion、Rotation arbitration 等 Gameplay 决策的主要隐藏发生地，而是消费准备好的 request / 提供 solver feedback；
- 自由垂直运动只有一个 Ballistic authoritative state，不再依赖 `GravityAccumulator + VerticalImpulseVelocity` 的双状态语义；
- Grounded 时 final vertical 与 Ballistic reset 符合 v3；
- Jump / launcher / hit impulse 能通过 Ballistic 的 Add / Set 类操作表达；
- world solved velocity 与 requested velocity 能被明确区分；
- 基础 locomotion、jump、airborne、landing、collision 行为可验证。

## Deferred to Codex Plan Mode

- Translation / Rotation 内部类是否拆文件；
- compatibility API 如何映射；
- KCC callback 中保留多少 adapter glue；
- Ballistic operation 的最终 API 名称与类型。

---

# 8. E3-C — Motion Ownership / Arbitration / MotionPolicy

## Goal

让 ActorMotor 的全部 temporary movement control 使用 v3 的独立 Channel / owner / Policy 语义，并固定最终 arbitration。

## Scope

### Translation

- HorizontalVelocityOwner；
- Root Motion owner stack；
- HorizontalImpulse；
- VerticalVelocityOwner；
- BallisticVerticalVelocity。

### Rotation

- Locomotion Rotation；
- Root Rotation owner stack；
- Scripted Rotation owner stack。

### MotionPolicy

- `LocomotionScale`；
- `AirLocomotionScale`；
- `GravityScale`；
- 参数级 ownership 与 combine rule。

已有符合 v3 的局部实现应优先复用。例如现有 Horizontal / Vertical velocity owner 已有 LIFO 基础，不为了命名统一无意义重写。

## Dependencies

- E3-B 已通过。

## Migration Boundary

- 旧 Sequence Clip 可以暂时通过 compatibility Motor API 进入新 Channel；
- `ActionMotionConfig` 仍可作为 serialized compatibility source，直到 E3-F / G 完成内容迁移；
- 不允许新 Action / 新 gameplay code 继续扩张旧 whole-action movement API。

## Required Architecture Contracts

水平：

```text
HorizontalVelocityOwner
>
Root Motion
>
Locomotion + HorizontalImpulse
```

垂直：

```text
VerticalVelocityOwner exists
→ owner
else
→ BallisticVerticalVelocity
```

旋转：

```text
Scripted Rotation
>
Root Rotation
>
Locomotion Rotation
```

共同规则：

- submission 与 arbitration 分离；
- 被覆盖 Channel 继续接受 submission / 更新自己的状态；
- 不 catch-up missed delta / yaw；
- Root Motion / Root Rotation / Scripted Rotation 使用各自 recoverable LIFO ownership；
- 不用 started-last 跨 Domain 抢优先级。

MotionPolicy：

```text
LocomotionScale     → Min
AirLocomotionScale  → Min
GravityScale        → Multiply
```

## Exit Criteria

- Root Motion 生效时 HorizontalImpulse 不进入最终 Root Motion 分支，但 impulse 可以继续衰减；
- Root Motion Begin / VelocityOwner active 不会 Clear / Reject 低优先级 submission；
- Root Motion cover / resume 使用当前 Tick 数据，不补位移；
- Root Rotation / Scripted Rotation cover / resume 不补 yaw；
- Horizontal / Vertical velocity owner 可以 A → B cover → B end → A resume，且非 top owner 可继续更新；
- VerticalVelocityOwner active 时 Ballistic freeze；最后一个 owner 结束时 Ballistic reset 0，Gravity 下一 Tick 恢复；
- Grounded 不自动结束 VerticalVelocityOwner；
- MotionPolicy 为 neutral-by-default、无 Tick scheduler、无 Action knowledge；
- Rotation producer 向 Motor 提交 local yaw delta，Motor 不理解 Target / Direction / Snap / RotateBySpeed。

## Deferred to Codex Plan Mode

- owner token / generation 类型；
- stack/container 实现；
- MotionPolicy token 类型；
- compatibility API 是否保留原方法名；
- debug inspector / diagnostics 具体形式。

---

# 9. E3-D — ActorAnimation Authority

## Goal

建立 `ActorAnimation` 作为 Actor 唯一 Animancer authority，把动画从 Action Clip / Actor 等分散调用中收归：

```text
ActorAnimation
├─ Locomotion Base
└─ Action Override
```

## Scope

- ActorAnimation Component；
- persistent Locomotion Base；
- temporary Action Override；
- lightweight Action owner protection；
- Action Pose submission；
- Combat simulation dt 驱动 blend / locomotion animation progression；
- centralized `Evaluate()`；
- Action A → B direct crossfade；
- Action gap 暴露 Locomotion Base；
- 为后续 `LocomotionAnimationProfile` 接入提供稳定边界。

`AnimationPoseClip` 的迁移属于本 Stage，因为如果它仍直接操作 `actor.animancer`，就无法建立单一 animation authority。其他 Motion Clip 留到 E3-F。

## Dependencies

- E3-A 必须已通过；
- 推荐在 E3-B / C 后执行，以减少并行大改面，但不依赖 Motion arbitration 的内部实现。

## Migration Boundary

- `Actor.animancer` 的 serialized reference 可以暂时保留以保护 prefab/asset，但 ActionSequence runtime 不再直接把它当 authority；
- Legacy Timeline 的动画路径可以暂时隔离为 compatibility，最终在 E3-G 收口；
- ActorLocomotion 尚未完成时，可以使用明确的 temporary/default Locomotion Base 数据入口，但不得新建第二个 locomotion animation authority。

## Required Architecture Contracts

- 只有 ActorAnimation 可以 Play / Blend / 操作 Animancer state / Evaluate；
- Action sampleTime 由 Sequence 决定；
- Locomotion animation 使用 Combat simulation dt 连续推进；
- 同 Actor / 同 Tick 最多一个有效 Action Pose；
- no Pose = no Action Override contribution；
- PoseClip 不持有 graph lifecycle；
- HitStop 冻结 Action sample progression、Locomotion animation progression、crossfade progression。

## Exit Criteria

- ActionSequence Pose path 不再直接 `Play / Evaluate` Animancer；
- 每个 Actor 每个 Combat Tick 最多集中 Evaluate 一次；
- Action Override active 时 Locomotion Base 仍持续更新；
- Action 结束无需 `RestartLocomotion()`；
- A → B action handoff 不强制闪回 Locomotion；
- stale Action owner 不能覆盖新 Action pose 或错误 cleanup；
- PoseClip overlap 可被明确识别为 authoring error；
- Animation Phase 的 per-Actor 工作通过 `ActorSimulationRuntime` 进入，而不是 Driver 直接调用内部动画 subsystem。

## Deferred to Codex Plan Mode

- ActorAnimation public API；
- owner token struct；
- Animancer state / layer 的具体组织；
- crossfade 配置位置；
- LocomotionAnimationProfile 的具体数据结构。

---

# 10. E3-E — ActorLocomotion Domain / Profile Migration

## Goal

建立稳定的 `ActorLocomotion` Gameplay Domain，让它只回答：

> 当前 Actor 使用哪套 Locomotion Profile？

并把 tuning 与 animation profile 分别输出给 Motor / Animation。

## Scope

- `ActorLocomotion` Component；
- `LocomotionModeAsset`；
- explicit fallbackMode；
- deterministic mode selection；
- SelfTags lifecycle；
- `LocomotionTuning`；
- `LocomotionAnimationProfile`；
- prefab / validation actor 的 locomotion profile migration；
- Input / AI Intent 继续直接进入 Motor，不把 ActorLocomotion 变成 input provider。

## Dependencies

- E3-B 已建立 Motor tuning / locomotion boundary；
- E3-D 已建立 ActorAnimation profile boundary；
- E3-A 已建立 Input / Control Phase routing。

## Migration Boundary

- 现有 locomotion 内容可以在迁移过程中作为 profile 数据来源；
- 不把旧 locomotion Action / StateKind 语义包装成新的 Mode Stack；
- 如果旧资产仍存在，允许 compatibility 引用，但 target validation actors 的 runtime locomotion authority 必须迁到 ActorLocomotion。

## Required Architecture Contracts

选择规则：

```text
valid candidates
→ highest Priority

same Priority + CurrentMode still valid
→ keep CurrentMode

Current invalid + top tie
→ serialized authored order

none
→ explicit fallbackMode
```

Mode lifecycle：

```text
release old SelfTags
→ set CurrentMode
→ acquire new SelfTags
→ update Motor tuning
→ update ActorAnimation profile
```

同 Tick 依赖：

```text
Stable Actor State + Intent
→ ActorLocomotion selection
→ Action arbitration
```

Action side effects 不反向重跑本 Tick Mode selection。

## Exit Criteria

- target actors 每个 Combat Tick 在 Input / Control Phase 完成 deterministic Mode selection；
- Mode asset 是 data-only，没有 OnEnter / OnExit gameplay behavior；
- fallback 显式存在，不依赖隐藏 priority trick；
- Mode switch 的 tags / tuning / animation profile 对 gameplay 观察为一次原子切换；
- ActorMotor 的基础 MoveSpeed / AirControl / RotateSpeed 来自当前 Locomotion Profile，而不是 Action whole-mode ownership；
- ActorAnimation 能持续获得当前 Locomotion Base profile；
- Player 与 AI 仍只在 Intent producer 侧不同，Motor 不知道来源；
- 不引入 Mode Stack / Transition Graph / Command Queue / generic StateSystem。

## Deferred to Codex Plan Mode

- Mode asset 的 serialized layout；
- EntryCondition 容器与 Inspector；
- prefab migration 的具体批次；
- authored-order 的具体稳定实现。

---

# 11. E3-F — ActionSequence Contribution Migration

## Goal

让 ActionSequence 中的每个 gameplay Clip 直接向自己的 v3 Domain 提交局部 contribution，结束“Clip / Action 通过旧 Motor/Animancer 总入口间接控制整角”的迁移状态。

## Scope

现有 / 新增 contribution 对齐：

```text
AnimationPoseClip
→ ActorAnimation Action Pose

RootMotionClip
→ Root Motion channel

RootRotation authoring
→ Root Rotation channel

Target / Direction rotation
→ Scripted Rotation producer

VelocityClip
→ HorizontalVelocityOwner / VerticalVelocityOwner

ImpulseClip
→ HorizontalImpulse / Ballistic Add|Set
→ optional GravityScale token during Clip interval

MotionPolicyClip
→ parameter-level policy tokens

HitBoxClip
→ ActorHitBoxRuntime window

TagClip
→ scoped tag lifecycle
```

同时完成 Advance / Seek / Cancel 与 owner lifecycle 的统一。

## Dependencies

- E3-C Motion channels / Policy 完整；
- E3-D ActorAnimation authority 完整；
- E3-E ActorLocomotion 已建立；
- E3-A phase ordering 已稳定。

## Migration Boundary

- `ActionMotionConfig` 字段可以继续存在用于旧 asset compatibility，但**新内容停止使用**；
- 已迁移 Action 不得再依赖 `ActionInstance.ApplyMotionConfig / RestoreMotionConfig` 取得 movement authority；
- Legacy Timeline 仍可作为迁移 backend 存在到 E3-G；
- 不建立 Generic Contribution Stack；每个 Domain 使用自己的固定 contract。

## Required Architecture Contracts

- Action = lifecycle coordinator，不是 whole-actor movement / animation authority；
- Sequence = fixed-frame content time authority，不知道 KCC / Physics barrier / Hit Resolve；
- Root Motion / Root Rotation 使用 Sequence `Extract(t0,t1)` 当前区间；
- Seek / Scrub / SetTime 不移动 gameplay Actor；
- Cancel 丢弃未消费 trajectory；
- covered authored delta 不 catch-up；
- `ImpulseClip.GravityScale` ownership 只等于 Clip active interval；
- Pose / Motor / HitBox 各自由对应 Domain owner 消费。

## Exit Criteria

- 至少有一组代表性 Action 内容覆盖 Pose、Root Motion、Root Rotation/Scripted Rotation、Velocity owner、Impulse/Ballistic、MotionPolicy、HitBox；
- 代表性 Action 的 Enter / Exit 不再通过 whole-action Motor rewrite 获得运动控制；
- RootMotionClip 不把 HorizontalImpulse 加进 Root Motion 最终分支；
- SelfRotation authoring 的 RootRotation 与 Target/Direction 最终进入不同 Rotation channel；
- Velocity Clip cover / resume 符合 owner stack；
- Impulse 是事件式写入，不偷偷拥有后续整个 Ballistic lifecycle；
- MotionPolicyClip 的 token 在 Enter / Exit / Cancel 下无泄漏；
- HitBoxClip 只管理 active window，不自行决定 Physics query 时机；
- PoseClip 不直接操作 Animancer；
- Sequence gameplay frame side effect 在低速 / HitStop / Cancel 下不会重复执行。

## Deferred to Codex Plan Mode

- 是否原地迁现有 ClipDefinition 还是先 compatibility wrapper；
- Clip serialized field 的迁移方式；
- MotionPolicyClip Inspector；
- representative Action asset 选择与迁移批次。

---

# 12. E3-G — Authority Cutover / Legacy & Content Migration

## Goal

在所有 v3 replacement path 已经可用后，完成真正的 runtime authority cutover，并安全迁移剩余资产 / prefab，清除双权威。

## Scope

- `ActionMotionConfig` whole-action runtime control 退出；
- Animator Gameplay Root Motion 路径退出；
- `ActorRootMotionRelay / OnAnimatorMove / Animator.deltaPosition / deltaRotation` 不再是 gameplay source；
- `ActorLogicInput` runtime authority 清理；
- 旧 `LocomotionRuntime / FacingRuntime / ActorMotionRuntime / SelfRotationBuffer` 等只保留仍必要的 compatibility shell，或在引用安全后删除；
- Legacy Timeline gameplay content 迁到 ActionSequence，或明确隔离为非长期 compatibility；
- target prefab / ActionAsset / Sequence asset 完成内容迁移；
- Combat simulation time / HitStop 在 Action、Animation、LocomotionRunner、Ballistic、Impulse、Root Motion/Rotation interval 上统一收口。

## Dependencies

- E3-F 已通过；
- 所有 replacement Domain 已能独立运行。

## Migration Boundary

本 Stage 允许为 Unity serialization 保留**没有 runtime authority 的兼容字段 / 类型**。

“代码还存在”与“仍是 authority”必须明确区分。

只有确认引用安全后才做物理删除。

## Required Architecture Contracts

最终 authority 必须唯一：

```text
Action selection      → ActionStateManager
Action playback       → ActionPlayer
Action time           → ActionSequence
Locomotion profile    → ActorLocomotion
Animation             → ActorAnimation
Movement              → ActorMotor
Authored root data    → RootMotionTrajectory
World solve           → KCC + ActorCollisionResolver
Hit query             → post-World Hit Phase
Hit resolve           → CombatHitBuffer
World order           → CombatSimulationDriver
```

HitStop / actor-local freeze：

```text
Driver phases can still run
but actor simulation time does not advance
```

## Exit Criteria

- active E3 gameplay content 不再依赖 `ActionMotionConfig` 作为 movement authority；
- `ActionInstance` 不再在 Enter / Exit 整体 Clear / Suppress / SetGravity / SetRootMotionMode / SnapFacing；
- Animator Gameplay Root Motion 无 authoritative runtime path；
- ActionSequence Pose path 无直接 Animancer authority；
- `ActorLogicInput` 不再是 runtime locomotion authority；
- 旧 Motor Runtime 名称即使暂存，也不能决定最终 arbitration / ownership；
- Legacy Timeline 不再是 active gameplay 的第二套长期 backend；
- HitStop 下 Action、Animation、Locomotion temporal evolution、Gravity、Impulse decay、authored root interval 一致冻结；
- 没有已知 prefab / asset 因类型或字段清理产生 Missing Script / broken serialized reference。

## Deferred to Codex Plan Mode

- asset inventory / migration tooling；
- compatibility field 是否加 obsolete / hidden 标记；
- 旧文件实际删除批次；
- Timeline asset 的逐项迁移策略。

---

# 13. E3-H — Final Validation / Documentation Handoff

## Goal

证明实现已经满足 Final Architecture v3，而不是仅仅“代码看起来已经重构完”。

本 Stage 主要做验证、缺口修正、文档收口，不重新设计架构。

## Scope

### Driver / Determinism

验证：

```text
ALL Control
→ ALL Action
→ ALL Animation
→ ALL Motion
→ World
→ ALL Hit Query
→ Resolve
→ ALL Finish
```

并验证 Actor 注册顺序变化不改变同 Tick 战斗结果。

### Translation

验证：

- `HorizontalVelocityOwner > Root Motion > Locomotion + HorizontalImpulse`；
- Root Motion cover / resume 无 catch-up；
- HorizontalImpulse 被 cover 时仍正常维护；
- blocked displacement 不偿还；
- requested 与 actual 分离。

### Vertical / Grounding

验证：

- Jump / launcher / hit Add/Set；
- Vertical owner cover / nested resume；
- owner active 时 Ballistic freeze；
- last owner end → Ballistic 0；
- Grounded → final vertical 0 / Ballistic 0；
- Grounded 不结束 owner；
- ForceUnground 正常。

### Rotation

验证：

- `Scripted > Root > Locomotion`；
- Root / Scripted stack 独立；
- cover 期间无 missed yaw catch-up；
- Snap / RotateBySpeed 都在 producer 侧解析。

### Animation

验证：

- Locomotion Base persistent；
- Action gap 露出 Locomotion；
- A → B direct crossfade；
- stale owner protection；
- max one Action Pose / Tick；
- exactly one Evaluate / Actor / Combat Tick；
- HitStop freeze。

### Root data / Sequence

验证：

- `M(0)=Identity`；
- `Delta(t0,t1)=Inverse(M0)*M1`；
- runtime XZ + Yaw；
- Seek 不移动 Actor；
- Cancel 不偿还 trajectory；
- playback speed 通过查询区间表达，不重复缩放 displacement。

### Hit

验证：

- final skeleton pose + final actor root 后才 query；
- query / resolve 分离；
- stable ordering；
- 相杀语义；
- 高速 shape 若存在连续判定需求，使用满足 v3 的 previous→current sweep / equivalent query，而不是只依赖终点 overlap。

### Serialization / Content

验证：

- prefab / ActionAsset / Sequence asset 无 broken references；
- compatibility 字段若仍存在，明确 non-authoritative；
- Active content 不依赖 Legacy Timeline / old ActionMotionConfig authority。

## Dependencies

- E3-G 已通过。

## Exit Criteria

- v3 第 18 章 Architecture Invariants 有明确验证证据；
- E3 范围内没有已知 Architecture Implementation Gap；
- 旧 authority 已退出 active runtime；
- Actor Motion validation checklist 已按 v3 重建；
- `Project_Structure.md` / Scene Ownership 在实现期间若发生实质变化则已刷新；
- editor/runtime boundary 若受影响，相关 Current editor docs 已同步；
- Roadmap 可标记 Completed 并移入 Archive，Final Architecture v3 继续保留为长期 authority。

## Deferred to Codex Plan Mode

- 测试是 EditMode / PlayMode / scene validation / instrumentation 的具体组合；
- 自动化测试文件布局；
- validation scene 的具体操作步骤；
- diagnostics / assertion 的临时实现。

---

# 14. Stage Gate 使用方式

每次开始一个 Stage 时，Codex Plan Mode 应重新读取：

```text
1. Final Architecture v3
2. 本 Roadmap 的当前 Stage
3. FrameWork 当前代码
4. 上一 Stage 已落地的真实结果
```

Plan Mode 的任务不是证明 Roadmap 的旧代码假设永远正确，而是：

> 在不违反 v3 和当前 Stage Exit Criteria 的前提下，基于现在真实代码给出最安全的实施方案。

推荐给 Codex 的任务边界表达：

```text
只规划 E3-X。
Final Architecture v3 是架构 authority；
E3 Roadmap 的 E3-X 定义 scope / migration boundary / exit criteria。
先审当前代码，再给出文件级和步骤级计划。
不要提前实施 E3-(X+1) 的 authority cutover。
如果发现必须修改已冻结架构，停止并指出冲突，不要自行改架构。
```

Stage 通过后，再为下一 Stage 重新进入 Plan Mode。

---

# 15. E3 Overall Completion Criteria

只有同时满足以下条件，E3 才算整体完成：

```text
Driver
→ 7-Phase World order 清楚且保持简洁

ActorSimulationRuntime
→ 唯一 per-Actor fixed-simulation routing boundary

ActorLocomotion
→ 唯一 locomotion profile authority

ActorAnimation
→ 唯一 Animancer / Evaluate authority

ActorMotor
→ Translation / Rotation movement authority
→ v3 fixed arbitration

ActionStateManager / ActionPlayer / ActionSequence
→ 分别拥有 Action selection / lifecycle / fixed-frame time

ActionSequence Clips
→ 只提交局部 contribution

RootMotionTrajectory
→ authored root data authority

KCC + Resolver
→ actual world movement result

Hit
→ post-World query + stable Resolve

Legacy
→ 不再拥有 active runtime authority
```

同时：

- HitStop / Combat simulation time 在 Action、Animation、Motion 上一致；
- active content 已迁到 v3 contract；
- Unity serialized references 安全；
- Final Architecture v3 不需要因为实现方便而被偷偷改写。

E3 完成后，后续功能开发默认基于 v3 Domain 扩展，而不是继续维护两套过渡架构。
