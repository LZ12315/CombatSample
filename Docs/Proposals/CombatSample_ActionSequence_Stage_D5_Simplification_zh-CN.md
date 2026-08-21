# CombatSample ActionSequence Stage D5：固定帧架构简化

> 状态：已完成；focused tests、命令行编译、Unity Test Runner 全量 EditMode/PlayMode 与 Jaeger 手动回归均已通过
>
> 日期：2026-08-22
>
> 起点：`324acac7 Add D4.1 combat hit intent coverage`
>
> 范围：CombatSimulationDriver、ActorSimulationRuntime、ActionPlayer、ActionSequence、Sequence HitBox 与 CombatHitBuffer

## 1. 与长期路线的关系

本文是 [`CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md`](CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md) 的 Stage D5 实施计划，不是第二套架构。

文档层级固定为：

1. 主架构文档回答“最终系统是什么”，并且始终拥有长期职责定义的最高优先级。
2. 本文回答“如何从当前 D4.1 代码迁移到该目标”，负责文件范围、实施切片和验收。
3. D5 完成后进入 Stage E1；E1 在 D5 已建立的 Actor 固定帧入口最前面接入 `DecideAction`，不重新设计 Sequence 和 HitBox。

若本文与主架构文档发生职责冲突，以主架构文档为准；若实施中必须改变本文约定，先同时更新两份文档，再修改代码。

## 2. 背景与问题

D4/D4.1 已证明以下顺序可以稳定工作：

```text
Sequence Frame
    ↓
Pose / Motion
    ↓
KCC + ActorCollisionResolver
    ↓
Physics.SyncTransforms
    ↓
Hit Query
    ↓
统一 Hit Resolve
    ↓
Clip Exit / Action Complete
```

当前问题不在行为正确性，而在职责表达：

- Driver、ActorSimulationRuntime、ActionPlayer、Fixed Session 和 Sequence 都暴露一遍 PreWorld/PostWorld/EndTick。
- Sequence 同时理解时间轴、世界运动屏障、Physics Query 和 Hit Resolve，超过了时间轴播放器的职责。
- HitBoxClip 同时承担攻击窗口、Physics Query、Intent 生产、pending 状态和 Resolver 回执。
- `CombatTickTransaction` 当前只包装一个 Hit Buffer，却让调用链看起来像通用 Gameplay 事务系统。
- `ActionSequenceClipPhase` 同时表示编辑器轨道分类和运行时调度阶段，名称和用途混杂。

D5 的目标是保留已经验证的世界顺序，删除不需要的中间抽象，使核心控制流能够从 Driver 直接读懂。

## 3. 已批准的方向

### 3.1 固定 Tick 与 Gameplay Frame

- Driver 每个 Unity Fixed Tick 都运行，KCC 和世界运动保持 60 Hz。
- ActionPlayer 每 Tick 最多推进一个 Gameplay Frame。
- 只有真正推进 Gameplay Frame 时，Sequence 才执行 Gameplay Clip，Actor 才执行 Hit Query。
- `0 < speed < 1` 且未跨帧时，只允许 Animation-only PoseRefresh。
- HitStop 不重复产生 Clip 生命周期、运动请求或 Hit。
- Motor 中已经存在的 Velocity owner、Impulse 和 Gravity 继续按既有规则存在。

### 3.2 正常完成与取消

- 正常动作帧通过 `FinishActionFrame` 收尾。
- 死亡、Disable 或严重异常通过幂等 `CancelAction` 清理。
- Cancel 不回滚已经发生的移动、已经冻结到 Buffer 的 Hit 或已经结算的伤害。
- 输入取消、连招切换和普通受击反应由下一 Tick `DecideAction` 处理，不在 Resolve 中重入当前动作帧。
- public `ActionPlayer.StopAction()` 保留，内部与新的 Cancel 语义共用一条清理路径。

### 3.3 Actor 参与方式

- 每个 Actor 只注册一个 `ActorSimulationRuntime`。
- Driver 在 Tick 开始建立 Actor 快照。
- ASM、ActionPlayer、Motor、Sequence、Clip 和 HitBox 不向 Driver 注册回调。
- Actor 在 Tick 中 Disable 时仍由快照保证清理安全；重复 Cancel/Finish 必须 no-op。

### 3.4 稳定性边界

- Gameplay Clip 按 Track 顺序、Clip 顺序稳定执行。
- Physics 返回的 Collider 顺序不能影响代表 Collider 和 PendingHit 顺序。
- Hit Resolve 按 `AttackerStableId → ClipStableId(StringComparer.Ordinal) → TargetStableId` 排序。
- 只保证同一次运行内不受容器遍历顺序影响；不承诺跨机器、rollback 或 bitwise replay。

### 3.5 Runner 与预览

- ActionSequenceRunner 可以独立播放 Pose、Motion 和普通 Clip 生命周期。
- Runner 不拥有权威 ActorHitBoxRuntime 入口，因此不执行 Gameplay Physics Query，也不造成伤害。
- 不为 Runner 再实现一套简化 Driver。

## 4. 目标职责

```text
CombatSimulationDriver
    决定全世界的固定执行顺序

ActorSimulationRuntime
    代表一个 Actor 参加固定 Tick

ActionPlayer
    管理 Action、播放速度、Gameplay Frame 推进、完成和取消

ActionSequenceRuntime
    根据当前 Gameplay Frame 驱动 Clip Enter / Tick / Exit

ActionSequenceClipRuntime
    只实现自身局部行为

ActorMotor / KCC
    合成运动请求并决定最终世界位置

ActorHitBoxRuntime
    保存 active hitboxes，在最终世界位置上执行 Physics Query

CombatHitBuffer
    收集本 Tick PendingHit，并按稳定顺序统一 Resolve
```

核心 Gameplay 不使用通用事件总线或 phase scheduler。事件只用于已提交结果的 UI、Audio、VFX、Camera 和调试观察。

## 5. 目标固定帧流程

### 5.1 Stage D5 完成时

```text
Capture Actor Snapshot
    ↓
KCC Pre Interpolation（若启用）
    ↓
Play Action Frames
    ↓
Move Actors
    ├─ KCC Simulate
    ├─ ActorCollisionResolver
    └─ Physics.SyncTransforms
    ↓
Detect Hits
    ↓
Resolve Hits
    ↓
Finish Action Frames
    ↓
KCC Post Interpolation（与 Pre 配对）
```

### 5.2 Stage E1 接入后

E1 只在 `Play Action Frames` 前增加：

```text
Decide Actions
```

D5 不创建空 `DecideAction()`，也不提前迁移 ASM LateUpdate。E1 会把现有 ASM 仲裁真正迁入该入口。

## 6. Sequence 生命周期

### 6.1 Fixed Session

`IFixedActionPlaybackSession` 从：

```csharp
bool TryBeginFrame(float deltaSeconds);
void ExecutePreWorld();
void ExecutePostWorld(ICombatHitIntentSink sink);
void EndFrame();
void AbortFrame();
```

收敛为：

```csharp
bool TryPlayFrame(float deltaSeconds);
void FinishFrame();
void Cancel();
```

`HasOpenFrame` 可以保留为轻量防重复状态，但不再暴露 PreWorld/PostWorld 事务状态。

### 6.2 ActionSequenceRuntime

Gameplay 生命周期收敛为：

```csharp
bool PlayFrame(ActionSequenceContext context, float deltaTime, float speedScale);
void FinishFrame();
void Cancel(ActionSequenceContext context);
```

`PlayFrame(frame)`：

1. 设置当前待提交 Gameplay Frame。
2. 对 `startFrame == frame` 的 Clip 调用一次 `OnEnter`。
3. 按 Track/Clip 稳定顺序对 active Clip 调用一次 `OnTick`。
4. 保持该帧打开，等待 Driver 完成移动和命中。

`FinishFrame()`：

1. 对 `endFrame == frame + 1` 的 Clip 调用一次 `OnExit(completed: true)`。
2. 提交 `CurrentFrame`。
3. 最后一帧退出剩余 Clip，并完成 Sequence。

`Cancel()`：

1. 对所有 active Clip 调用一次 `OnExit(completed: false)`。
2. 丢弃未提交的 Sequence frame 状态。
3. 结束 Sequence，不回滚外部世界副作用。

### 6.3 Pose 特例

Frame 0 Pose baseline 与 fractional PoseRefresh 继续保留，但它们是动画表现入口，不是世界阶段：

- baseline 只建立 Frame 0 AnimationPoseClip 的起始 Pose；
- PoseRefresh 只刷新已 active AnimationPoseClip；
- 两者都不进入或退出 Gameplay Clip，不产生 Motion 或 Hit；
- 不在本阶段重新设计 Animancer 或 AnimationTimeMapping。

### 6.4 Track Kind

`ActionSequenceClipPhase` / `Phase` 改名为 `ActionSequenceTrackKind` / `Kind`，明确它只用于：

- Editor 轨道分组与颜色；
- Track 对 Clip 类型的约束；
- Legacy flat clip 迁移时选择目标 Track。

Runtime 不根据 Kind 分段执行，也不把 Kind 当作物理屏障。现有派生 Track 类型与序列化字段保持不变；旧 `ActionSequenceCleanupTrack` 类型保留兼容，不修改现有资产。

## 7. ActorHitBoxRuntime

### 7.1 所有权

每个 `ActorSimulationRuntime` 创建并持有一个纯 C# `ActorHitBoxRuntime`。生产 Sequence Session 通过 Actor 的 internal 入口取得它，并放入只对生产播放可见的 internal Context 引用；ActionSequenceRunner 不提供该引用。

不使用：

- static 全局 HitBox Runtime；
- Clip 向 Driver 注册；
- 每 Tick Sink 逐层传递；
- 为测试增加 public 调试 API。

### 7.2 API

```csharp
HitBoxHandle Activate(ActiveHitBoxDefinition definition);
void Deactivate(HitBoxHandle handle);
void DetectHits(CombatHitBuffer buffer);
void Clear();
```

### 7.3 ActiveHitBox

每个 active HitBox 至少保存：

- handle；
- owner Actor；
- Clip stable id；
- 已解析 Bone Transform；
- HitBox shape/config；
- AttackData；
- Impact effects；
- `HashSet<IDamageable> AttemptedTargets`。

每个 HitBox Clip 拥有自己的 active 实例和去重集合，因此两个不同 Clip 可以分别命中同一目标。

### 7.4 HitBoxClip

HitBoxClip Runtime 只保留 handle：

```csharp
OnEnter(context)
{
    handle = context.HitBoxes?.Activate(frozenDefinition);
}

OnTick(context)
{
}

OnExit(context, completed)
{
    context.HitBoxes?.Deactivate(handle);
    handle = default;
}
```

没有生产 HitBoxRuntime 时，Clip 保持无权威副作用；最多输出一次清楚诊断。

### 7.5 DetectHits

对每个 active HitBox：

1. 用最终 Bone Transform 构建世界 Capsule。
2. 执行 `Physics.OverlapCapsuleNonAlloc`。
3. 跳过自身 Collider、无 `IDamageable`、已死亡和已经尝试的目标。
4. 将同一 `IDamageable` 的多个 Collider 合并为一个候选。
5. 代表 Collider 取离查询中心最近者；距离平局取 Instance ID 较小者。
6. 候选按 Target Stable ID 排序，避免 Dictionary/Physics 遍历顺序泄漏。
7. 为每个候选创建 `PendingHit` 并写入 Buffer。
8. 只有成功写入 Buffer 后才加入 `AttemptedTargets`。

一旦写入 Buffer，本攻击窗口不再重试该目标；无敌、拒绝 Impact 或随后死亡也不恢复资格。

## 8. CombatHitBuffer

### 8.1 删除内容

- `ICombatHitIntentSink`；
- `ICombatHitIntentReceipt`；
- `CombatHitIntent.Receipt`；
- HitBox `_pendingTargets`；
- Resolve/Abort 回执；
- `CombatTickTransaction`。

### 8.2 PendingHit

`PendingHit` 只保存 Resolve 真正需要的冻结数据：

```text
AttackerStableId
ClipStableId
TargetStableId
AttackHitData
IDamageable Target
RepresentativeCollider
ImpactEffects
```

Tick ID 不进入每条 Hit；一个 `CombatHitBuffer` 只服务当前 Driver Tick。

### 8.3 Buffer API

```csharp
void Begin();
bool Add(in PendingHit hit);
void Resolve();
void Clear();
```

Resolve 规则：

- 先按稳定键排序，再逐条执行。
- Query 后攻击者死亡不取消已经收集的 Hit，允许相杀。
- 目标已死亡时跳过，不重复扣血、受击、Impact 或死亡。
- 首个把目标从存活变为死亡的 Hit 获得 `TargetKilled`。
- `ImpactAllowed` 时才触发 Impact。
- Resolve 期间再次 Add 明确抛错。
- Resolve 中异常时保留先前副作用，清空剩余 Buffer 并向 Driver 重抛。

## 9. Driver、ActorRuntime 与 ActionPlayer

### 9.1 ActorSimulationRuntime

D5 结束时提供：

```csharp
bool PlayActionFrame(float deltaSeconds);
void DetectHits(CombatHitBuffer buffer);
void FinishActionFrame();
void CancelAction();
```

内部只保存：

- Actor 与 ActionPlayer 引用；
- ActorHitBoxRuntime；
- 当前 Tick 是否真正播放了 Gameplay Frame；
- 必要的幂等清理状态。

它不保存伤害规则、ASM 仲裁规则或运动合成逻辑。

### 9.2 ActionPlayer

- 正式 Sequence 仍只由固定 Driver 推进。
- Legacy Timeline 继续走现有 Update 兼容路径，本阶段不迁移。
- `PlayActionFrame` 负责 speed accumulator、baseline、PoseRefresh 和调用 Fixed Session。
- `FinishActionFrame` 在 Resolve 后提交 Sequence frame，并同步 public playback state。
- `StopAction`、Disable 和异常统一复用幂等取消清理。
- 不改变 ActionAsset、ActionInstance、速度 modifier 和 public 查询字段的资产合同。

### 9.3 CombatSimulationDriver

Driver 直接写出五步业务顺序，不再使用 PreWorld/PostWorld 命名。KCC 的技术性 interpolation Pre/Post 仍保留，因为这是 vendor API 配对，不是 Gameplay phase。

异常路径：

1. 捕获本 Tick 异常。
2. 清空未结算 Hit。
3. 对 Actor 快照调用幂等 `CancelAction`。
4. 在 finally 中完成匹配的 KCC interpolation Post。
5. 清空快照并让 Driver faulted，避免继续运行不可信状态。

## 10. 当前代码到目标代码的映射

| 当前 | D5 目标 |
| --- | --- |
| `ActorSimulationRuntime.ExecutePreWorld` | `PlayActionFrame` |
| `ActorSimulationRuntime.ExecutePostWorld(sink)` | `DetectHits(buffer)` |
| `ActorSimulationRuntime.EndSimulationTick` | `FinishActionFrame` |
| `ActorSimulationRuntime.AbortSimulationTick` | `CancelAction` |
| `ActionPlayer.ExecuteSimulationPreWorld` | `PlayActionFrame` |
| `ActionPlayer.ExecuteSimulationPostWorldWithHitSink` | 删除 |
| `ActionPlayer.EndSimulationTick` | `FinishActionFrame` |
| `ActionPlayer.AbortSimulationTick` | `CancelAction` 共用路径 |
| `SequenceRuntime.BeginFrame + ExecutePreWorld` | `PlayFrame` |
| `SequenceRuntime.ExecutePostWorld` | 删除 |
| `SequenceRuntime.EndFrame` | `FinishFrame` |
| `SequenceRuntime.FrameTransactionState` | 一个轻量 open-frame 状态 |
| HitBoxClip Physics Query | `ActorHitBoxRuntime.DetectHits` |
| `CombatHitIntent + Sink + Receipt` | `PendingHit` |
| `CombatTickTransaction` | Driver 直接持有一个 `CombatHitBuffer` |
| Runtime Clip Phase 排序 | Track/Clip 顺序；Kind 仅用于 Editor 分类 |

## 11. 实施切片

### D5.1：合同测试与 Sequence 生命周期

- 先把 Runtime/ActionPlayer 测试改写为 `PlayFrame → FinishFrame / Cancel` 语言。
- 实现 Fixed Session、ActionPlayer 和 SequenceRuntime 简化。
- 保留 baseline、PoseRefresh、半开区间、最后一帧和 Loop 的既有行为。
- 将 Phase 重命名为 TrackKind，并完成 Editor/Validator 的机械迁移；不得产生资产 diff。

### D5.2：ActorHitBoxRuntime

- 新增 ActorHitBoxRuntime、ActiveHitBox 和 HitBoxHandle。
- HitBoxClip 改为 Activate/Deactivate。
- 将 Query、Collider 合并、代表 Collider 和去重迁入 ActorHitBoxRuntime。
- Runner/预览保持无权威 Hit。

### D5.3：CombatHitBuffer

- 新增 PendingHit 与简化 Buffer。
- 保留稳定排序、死亡、相杀和 Impact 规则。
- 删除 Sink、Receipt、pending 回执和 CombatTickTransaction。
- 更新 Resolver 与 HitBox 聚焦测试。

### D5.4：Driver 与 Actor 集成

- Driver 改成五步明确流程。
- ActorSimulationRuntime 接入 ActionPlayer 与 ActorHitBoxRuntime。
- Disable、死亡和异常统一走幂等 Cancel。
- 保留 Actor snapshot、KCC ownership、collision resolver、SyncTransforms 和 interpolation 配对。

### D5.5：验收与文档收口

- 运行 focused EditMode/PlayMode tests。
- 运行全部 EditMode 与 PlayMode tests。
- 重新执行 Jaeger 手动回归。
- 更新主架构文档 D5 状态、当前仓库审计和测试记录。
- 确认 D5 完成后再开始 E1。

## 11.1 当前实施记录（2026-08-22）

- D5.1 已落地：`ActionSequenceRuntime`、`ActionPlayer` 与 fixed session 已收敛为 `PlayFrame → FinishFrame / Cancel`；旧 `BeginFrame/PreWorld/PostWorld/EndFrame/AbortFrame` 运行时入口已从生产调用链移除。
- D5.2 已落地：每个 `ActorSimulationRuntime` 持有一个 `ActorHitBoxRuntime`；HitBoxClip 只负责激活/关闭攻击窗口，Physics Query 由 Driver 在移动和 `Physics.SyncTransforms` 后统一触发。
- D5.3 已落地：`CombatHitIntentBuffer` / Sink / Receipt / `CombatTickTransaction` 收敛为 `PendingHit + CombatHitBuffer`；稳定排序、死亡跳过、相杀和异常清理规则保留。
- D5.4 已落地：Driver 明确执行 `PlayActionFrame → KCC/Collision/SyncTransforms → DetectHits → ResolveHits → FinishActionFrame`；Actor 快照、KCC ownership、interpolation 配对与 fault 停止保留。
- 行为变化已记录：无敌或拒绝 Impact 的目标也视为本次 active HitBox window 已尝试，不在同一窗口内重试。
- 命令行验证已完成：`dotnet build Assembly-CSharp.csproj` 与 `dotnet build Assembly-CSharp-Editor.csproj` 通过。
- 已通过当前自动化门禁：focused tests、`Assembly-CSharp.csproj` 编译、`Assembly-CSharp-Editor.csproj` 编译、D5 范围 `git diff --check`。
- 已通过 Unity Editor 验收：全部 EditMode tests、全部 PlayMode tests，以及生产 runtime 改动后的 Jaeger 手动回归。

## 12. 测试计划

### Sequence 与 ActionPlayer

- 同帧 Clip 按 Track/Clip 顺序 Enter、Tick。
- Finish 只退出本帧到期 Clip；最后一帧才完成 Action。
- Cancel 对所有 active Clip exactly-once Exit(false)。
- Frame 0 baseline 只建立 Animation Pose。
- 0.5 倍速首 Tick 只 PoseRefresh，第二 Tick 只产生一次 Gameplay 输出。
- HitStop 不重复 Clip、Motion 或 Hit。
- Pause、Loop、非法速度和非 60 Hz 门禁保持现有行为。

### HitBox 与 Buffer

- 一个 Damageable 的多个 Collider 只产生一个 PendingHit。
- 代表 Collider 使用最近距离与 Instance ID 平局规则。
- 一个 active HitBox 对一个目标只尝试一次，包括无敌/拒绝 Impact。
- 两个不同 HitBox Clip 可以分别命中同一目标。
- producer 在 Resolve 前退出，已进入 Buffer 的 Hit 仍结算。
- 反转 Actor 注册、Collider 和 Add 顺序不改变 Resolve 结果。
- 同 Tick 相杀成立；死亡后的后续 Hit 跳过。
- 多个致死 Hit 只有第一个 `TargetKilled`。
- Resolve 重入和异常触发 fail-fast，不回滚先前副作用。

### Driver 固定帧

- Pose、RootMotion、SelfRotation、KCC 最终 Root 与 HitBox 使用同一 Gameplay Frame。
- RootMotion 使用 tick-start rotation。
- VelocityOverride 覆盖和退出恢复不变。
- 撞墙后 HitBox 使用实际 KCC 位置，blocked displacement 不偿还。
- 最后一帧顺序为 `Detect → Resolve → Clip Exit → ActionFinished`。
- interpolation 开关不改变命中集合。
- Resolve 中死亡 Disable 后，双方已收集 Hit 继续结算，取消清理 exactly once。
- Driver fault 后不残留 trajectory owner、velocity owner、active hitboxes 或 open session。

### Manual Jaeger

- RetreatAttack 动画、后退位移、SelfRotation、VelocityOverride；
- HitBox、Impact、相杀；
- 0.5 倍速、HitStop；
- 撞墙、最后一帧；
- 输入取消、死亡和 Disable；
- 场景退出后无 owner 或 active hitbox 泄漏。

## 13. 文件范围

预期生产代码范围：

- `Assets/Scripts/Actor/CombatSimulationDriver.cs`
- `Assets/Scripts/Actor/ActorSimulationRuntime.cs`
- `Assets/Scripts/Actor/Actor.cs`
- `Assets/Scripts/Actor/ActionPlayer.cs`
- `Assets/Scripts/Actor/ActionPlaybackSession.cs`
- `Assets/Scripts/Actor/SequenceActionPlaybackSession.cs`
- `Assets/Scripts/ActionSequence/ActionSequenceRuntime.cs`
- `Assets/Scripts/ActionSequence/ActionSequenceContext.cs`
- `Assets/Scripts/ActionSequence/ActionSequenceClipDefinition.cs`
- `Assets/Scripts/ActionSequence/ActionSequenceTrackDefinition.cs`
- `Assets/Scripts/ActionSequence/ActionSequenceData.cs`
- `Assets/Scripts/ActionSequence/ActionSequenceRunner.cs`
- `Assets/Scripts/ActionSequence/Clips/ActionSequenceHitBoxClipDefinition.cs`
- Phase → TrackKind 所直接影响的 ActionSequence Editor 文件
- 新增 `ActorHitBoxRuntime` 与 `CombatHitBuffer` 文件及对应 `.meta`

预期重点测试范围：

- `ActionSequenceRuntimeTests`
- `ActionPlayerSequenceIntegrationTests`
- `ActionSequenceHitBoxIntentIntegrationTests`（重命名为 HitBoxRuntime 语义）
- `CombatHitIntentResolverTests`（重命名为 CombatHitBuffer 语义）
- `CombatSimulationDriverTests`
- Phase → TrackKind 所直接影响的 Editor/Validator tests

不得修改：

- Scene、Prefab、Graph；
- ActionAsset、ActionList；
- AnimationConfig 与内嵌 trajectory；
- ProjectSettings；
- Renderer、Input 资产；
- Legacy Timeline runtime 和内容资产；
- vendor KCC、Animancer、TagTree 代码。

## 14. 资产与 API 边界

- 不增加或迁移序列化字段。
- 保留现有派生 Track/Clip 类型名和 managed-reference 资产数据。
- 不修改 ActionAsset GUID 或 `.meta` 身份。
- Phase → TrackKind 是源码/API 名称迁移，不改变序列化数据；提交前必须验证现有 Jaeger ActionAsset 无 diff。
- public `ActionPlayer.StopAction`、播放速度 modifier 和 Action 查询 API 保留。
- 不为测试增加 public Gameplay 调试接口。

## 15. 工作区与提交策略

当前基线存在用户已有修改，D5 不得包含、还原或格式化它们：

- `Assets/Create/ActionAssets/Jaeger/Battle/Jaeger_RetreatAttack/Jaeger_RetreatAttack.asset`
- `Assets/Settings/Engine/URP-HighFidelity-Renderer.asset`
- `Assets/Settings/Input/PlayerInputControl.inputactions.meta`
- `ProjectSettings/TimeManager.asset`

实施时：

- 不使用 `git add -A` 或 `git add .`；
- 每个切片只显式暂存自己的代码、测试、文档和新 `.meta`；
- 每个切片保持可编译、可验证；
- 不 amend `324acac7`，D5 使用新的独立提交；
- 提交前运行 `git diff --check`，并分别检查 staged/unstaged 文件列表。

## 16. 完成标准

Stage D5 只有同时满足以下条件才算完成：

1. Driver 主流程可以直接读成 Play、Move、Detect、Resolve、Finish。
2. Sequence 不再出现 PreWorld、PostWorld、Hit Sink 或运行时 Phase 调度。
3. HitBoxClip 不再执行 Physics Query 或生产 Resolver 回执。
4. 每个 Actor 只有一个 ActorHitBoxRuntime，并只由 Driver 在移动后调用 DetectHits。
5. CombatHitBuffer 不依赖 Sink、Receipt 或 CombatTickTransaction。
6. D4/D4.1 保留的行为合同全部通过自动化和 Jaeger 手动回归。
7. 主架构文档与本文状态同步。
8. 工作区没有意外资产或设置 diff。

完成后，下一里程碑固定为 Stage E1：ASM 请求排队与 `ActorSimulationRuntime.DecideAction` 固定 Tick 提交。
