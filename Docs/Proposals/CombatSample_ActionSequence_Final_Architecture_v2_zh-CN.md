# CombatSample ActionSequence 最终架构 v2

> 状态：已批准的实施基线，分阶段实施中；Stage B 的 Driver 兼容切片与 60 Hz 世界固定时钟已落地
>
> 日期：2026-08-09
>
> 范围：ActionSequence、AnimationConfig、Root Motion、Locomotion、ActorMotor/KCC 与 HitBox 固定模拟顺序

本文档把 [`CombatSample_RootMotion_Final_Design_v1.md`](../CombatSample_RootMotion_Final_Design_v1.md) 的 Root Motion 技术方案与后续架构讨论合并为一份实施基线。

原 v1 文档中的 Baker、Trajectory 数据和刚体变换数学继续有效；当两份文档的运行时职责发生冲突时，以本文档为准。最重要的变化是：Root Motion 的位移和旋转不再由同一个 Sequence Clip 承担，Locomotion 也不再伪装成 Action。

本文档描述的是完整目标架构，不代表所有阶段都已经实现。第 11 节单独记录当前代码与目标之间的差异及已落地切片。

---

## 1. 最终决定速览

| 问题 | 最终决定 |
| --- | --- |
| Action 模型 | 只保留一种 `ActionAsset + ActionInstance`；`ActionSequence` 是唯一正式播放内容 |
| Locomotion | 独立 `LocomotionController`，不是 Action；`CurrentAction == null` 表示 Locomotion 域 |
| 动画目录 | 每个角色或动画资产族引用自己的 `AnimationConfig` |
| Pose | `AnimationPoseClip` 按 key 查询动画，只负责 Animancer Pose |
| Root Motion | `RootMotionClip` 按 key 查询烘焙数据，只负责 XZ 位移 |
| 自身旋转 | `SelfRotationClip` 独立负责烘焙旋转、朝 Target 或朝指定方向 |
| 垂直运动 | Root Motion Y 第一版不用；继续由 Gravity、VerticalImpulse、VerticalVelocity 负责 |
| 世界运动 | 所有请求进入 `ActorMotor`，由 KCC 决定实际位置；其他系统不直接改 Gameplay Transform |
| 固定模拟 | 项目侧 `CombatSimulationDriver` 是 Tick 调度者；KCC 是世界运动求解屏障，不是业务调度器 |
| HitBox | 使用同一 Sequence Frame 的骨骼 Pose，但必须在 KCC 与 Actor 互推完成后查询 |
| 动量交接 | Action 进入时不再按比例清除旧动量；Motor 已有 Impulse、Gravity 状态自然延续 |
| Legacy Timeline | 只作为现有资产迁移期兼容，不发展成第二种长期 Action 后端 |

第一版不引入 AnimAction、DefaultAction、Action 子类族、通用 Motion Graph、Root Motion Y、Motion Warping 或加权 Root Motion 混合。

---

## 2. 总体运行图

```text
Update
├─ ActorLogicInput 只采集并保存输入
└─ Locomotion 域的自然动画 / mixer 表现更新

CombatSimulationDriver.FixedUpdate（60 Hz）
│
├─ BeginTick：提交上一阶段排队的 Action / Locomotion 域切换
│
├─ PreWorldMotion
│  ├─ Locomotion 域：LocomotionController 仲裁 Mode 并提交 Motor 移动请求
│  └─ Action 域：ActionSequence 至多推进一个 Gameplay Frame
│     ├─ State / Tag
│     ├─ AnimationPoseClip → Animancer Pose
│     ├─ RootMotionClip → XZ 位移请求
│     └─ SelfRotationClip → 自身旋转请求
│
├─ WorldMotionCommit
│  ├─ KCC Simulate
│  ├─ ActorCollisionResolver
│  └─ Physics.SyncTransforms（全局一次）
│
├─ PostWorldMotion
│  ├─ 使用同一 Pose 和最终 Actor Root 查询 HitBox
│  └─ 统一结算命中；由命中产生的状态切换和运动排到下一 Tick
│
└─ KCC PostSimulationInterpolationUpdate
```

动画数据链独立于世界求解：

```text
Actor.AnimationConfig
        │
        ├─ key → TransitionAsset ─────────────→ AnimationPoseClip
        │
        └─ key → RootMotionTrajectory ─┬─────→ RootMotionClip（XZ）
                                      └─────→ SelfRotationClip（烘焙 Yaw）
```

Pose Clip、位移 Clip 和旋转 Clip 彼此不引用，也不要求相同 key、时间范围或播放映射。作者可以让它们对齐，也可以有意识地做成不同效果。

---

## 3. 权威边界

| 领域 | 权威 |
| --- | --- |
| 固定 Tick 顺序 | `CombatSimulationDriver` |
| Actor 当前处于 Action 还是 Locomotion | `ActionStateManager` |
| Action Gameplay Frame | `ActionSequence` |
| 骨骼 Pose | Animancer |
| 动画原始位移与旋转数据 | `RootMotionTrajectory` |
| Locomotion 模式、自然动画和移动输入解释 | `LocomotionController` |
| 运动通道合成 | `ActorMotor / ActorMotionRuntime` |
| 碰撞允许后的实际世界位置 | KCC + `ActorCollisionResolver` |

永久约束：

1. Sequence 不直接写 Actor Transform。
2. Animancer 不再为 Sequence 提供 Gameplay Root Motion。
3. RootMotionTrajectory 只提供 authored request，不决定是否能穿过墙壁。
4. ActorMotor/KCC 之外的 Gameplay 系统不得直接移动 Actor Root。
5. KCC 不认识 Action、Sequence、Tag 或 HitBox；依赖方向始终是项目调用 KCC。

---

## 4. 单一 Action 与 LocomotionController

### 4.1 单一 Action 模型

目标架构只有：

```text
ActionAsset
└─ ActionSequenceData

ActionInstance
ActionPlayer
```

不创建：

- `SequenceAction` / `SequenceActionInstance`；
- `AnimAction` / `AnimActionInstance`；
- `LegacyTimelineAction` / `LegacyTimelineActionInstance`；
- 用于保底的 `DefaultAction`。

简单动作也使用一个最小 Sequence。现有 Timeline Session 是迁移期适配器，不是长期可扩展的多后端插件体系。

`ActionSequenceAsset` 和 `ActionSequenceRunner` 只用于独立预览、编辑器验证和测试，不构成第二种生产 Action。正式 Gameplay Action 的唯一数据根是 `ActionAsset` 内嵌的 `ActionSequenceData`。

Action 的 SelfTags、Priority、Entry/Exit、CancelRules 和事件触发等公共仲裁数据仍保留在唯一的 `ActionAsset` 上，不下沉到某一种 Clip。

### 4.2 两个互斥域

Actor 任一时刻只处于一个前台域：

```text
StateKind.Action       CurrentAction != null
StateKind.Locomotion   CurrentAction == null
```

Action 和 Locomotion 的 Priority 不跨域比较：

- Locomotion 活跃时，任意满足 EntryConditions 的 Action 都可以进入；Action 候选只在彼此之间比较 Action Priority。
- Action 活跃时，LocomotionController 不会自行抢占 Action。
- Action 自然结束后进入 Locomotion。
- Action 也可以通过 `CancelTargetKind.Locomotion` 在取消窗口内提前返回 Locomotion。

### 4.3 ActorLogicInput

`ActorLogicInput` 继续在普通 `Update` 中采集输入、维护输入缓冲并计算最新 `LocomotionIntent`，但只把结果保存在自己身上，不再直接调用 ActorMotor。

`LocomotionController` 在需要时通过：

```csharp
actor.GetComponent<ActorLogicInput>()
```

读取最新 Intent。Action 期间输入仍持续更新，因此回到 Locomotion 时可以立刻使用最新方向。

### 4.4 LocomotionModeAsset

每个 Mode 是独立 ScriptableObject，至少包含：

```text
Priority
EntryConditions
SelfTags
AnimationConfig Key
Locomotion 参数
```

`LocomotionController` 只保存可用 Mode 列表和一个必需的 Fallback Mode。Controller 自身不额外提供 SelfTags，也不隐式添加 `Action.Move`；只有当前 ModeAsset 明确配置的 SelfTags 会写入 Actor。

选择规则：

1. Locomotion 域每个逻辑 Tick 重新检查当前 Mode 和候选 Mode。
2. 当前 Mode 仍合法时，只有更高 Priority 的合法 Mode 可以抢占。
3. 当前 Mode 已失效时，在合法 Mode 中选择最高 Priority。
4. 同 Priority 时优先保持当前 Mode，避免抖动。
5. 真正切换 Mode 时才执行 Condition `OnClaim`；普通轮询不消费条件。
6. 普通 Mode 的空 EntryConditions 表示配置错误、不可进入。
7. Fallback Mode 必须无条件可进入且 Priority 最低，因此合法配置下始终有候选。

如果 Fallback 缺失、失效或错误配置，Controller 必须报告配置错误；在这种防御性异常状态下，无 Mode 中标就保持当前域状态，不猜测一个 Mode。

Animancer 也遵循域权威：

- Locomotion 域只有 `LocomotionController` 可以发起播放和更新 locomotion mixer；
- 进入 Action 时，Controller 停止继续操作 Graph 和提交移动，但不必硬停旧动画 state，Action 可以从当前 Pose 淡入；
- 返回 Locomotion 时，Controller 从最新 Intent 选中 Mode，再淡入其动画；
- 任一时刻只有当前域能继续操控 Animancer，旧域只允许作为 transition 的淡出来源。

### 4.5 CancelRule 与 Conditions

所有 CancelRule 增加自己的 Conditions。固定求值顺序为：

```text
Cancel Window
    ↓
CancelRule Conditions
    ↓
目标 Action EntryConditions
或 Locomotion Mode EntryConditions
    ↓
Priority
    ↓
胜选 Rule 与目标 Conditions 的 OnClaim
```

空列表语义：

| 位置 | 空 Conditions |
| --- | --- |
| Action EntryConditions | `false` |
| CancelRule Conditions | `true` |
| 普通 LocomotionMode EntryConditions | 无效 / `false` |
| Fallback Mode | 无条件 `true` |

`CancelTargetKind.Locomotion` 打开整个 Locomotion 域，不在 CancelRule 中指定具体 Mode。最终 Mode 由各自 EntryConditions 和 Priority 选择。合法配置下 Fallback 保证有候选；如果 Fallback 缺失或失效导致无人中标，取消失败、当前 Action 继续，并报告配置错误。

Conditions 按需检查，不注册到全局 Tick，也不拥有自己的 Update。只有最终成功的转换才能消费输入或执行其他 `OnClaim` 副作用；成功后同时对胜选 CancelRule 的 Conditions 与胜选 Action/Mode 的 EntryConditions 执行 `OnClaim`。

---

## 5. AnimationConfig 与 Root Motion Baker

### 5.1 AnimationConfig 是什么

每个角色或动画资产族有一份 `AnimationConfig`，Actor 持有它的引用。它是长期存在的动画目录资产，不属于某个 ActionSequence，也不是每次播放创建的运行时实例。

建议使用可序列化 Entry 列表，并在运行时构建只读字典：

```text
AnimationConfig
└─ Entries
   ├─ Key: "LightAttack_1"
   │  ├─ TransitionAsset
   │  └─ RootMotionTrajectory（可空）
   └─ Key: "RunForward"
      ├─ TransitionAsset
      └─ RootMotionTrajectory（可空）
```

Key 必须唯一。运行时 Clip 只通过 Actor 上的 AnimationConfig 查找，不保存角色 Prefab、Animator 或 Avatar 的运行时副本。

### 5.2 BakeProfile 与运行时配置分离

Reference Character Prefab、Animator、Avatar、采样率和 Validator 参数属于 Editor-only `RootMotionBakeProfile`，不进入运行时 Entry。每个 AnimationConfig 必须能通过一个明确的 editor-only authoring link 找到唯一 BakeProfile，Inspector 的 Bake/Rebake 都使用该 link；不能按命名猜 Profile。具体 link 存在 editor metadata、子资产还是 `#if UNITY_EDITOR` 字段属于实现细节，运行时查询 API 不暴露它。这样可以避免把烘焙依赖带进运行时 Entry。

每个 Rig / Avatar Family 使用自己的 BakeProfile。不同体型是否共用同一 trajectory 必须通过 Validator 证明；不使用猜测性的 `humanScale` 修正。

### 5.3 作者工作流

作者只在 Entry 中填写 `TransitionAsset`，然后在 AnimationConfig Inspector 中执行：

```text
Bake / Rebake / Bake All
```

对于能唯一解析出一个 AnimationClip 的 Transition，Baker 自动取得源 Clip。Entry 中不再要求作者重复填写一个 `RootMotionSourceClip`。

生成的 `RootMotionTrajectory` 是独立 `.asset`，由工具确定性创建或更新在该 AnimationConfig 对应的 `Generated/` 目录，并自动回填引用。作者不手工创建、命名或拖拽配对。

Mixer 或 Directional Transition 第一版可以 Pose-only，但不能直接 Bake Gameplay Root Motion。需要位移时，RootMotionClip 使用另一个能唯一解析到单 Clip 的 key；不在运行时混合多个子动画 trajectory。

### 5.4 Baker 合同

```text
AnimationClip
    +
RootMotionBakeProfile
    ↓
实例化干净 Reference Rig
    ↓
Manual PlayableGraph
Evaluate(0)
连续 Evaluate(dt)
    ↓
记录 Unity 最终求值后的累计 Root Transform
    ↓
RootMotionTrajectory
```

Baker 不解析 FBX，不从 Hips/Pelvis 猜位移，也不重写 Mecanim/Humanoid 规则。Avatar、Retarget 和 Import Settings 的解释交给 Unity。

默认采样率 60 Hz；只有高速 Dash 或快速转身经 Validator 证明误差过大时，单独提高到 120 Hz。

### 5.5 Trajectory 数据与数学

Trajectory 永久保存：

```text
累计 XYZ
完整 Quaternion
float32
M(0) = Identity
```

其中 `M(t)` 表示动画起点到时间 `t` 的累计 Root Transform。

区间增量必须使用：

```text
Delta(t0, t1) = Inverse(M(t0)) * M(t1)
```

对应：

```csharp
deltaRotation = Quaternion.Inverse(r0) * r1;
deltaPosition = Quaternion.Inverse(r0) * (p1 - p0);
```

不能使用简单世界位置相减。多个刚体 Delta 的组合也必须使用正确的 SE(3) 组合：

```text
AB.Position = A.Position + A.Rotation * B.Position
AB.Rotation = A.Rotation * B.Rotation
```

### 5.6 Stale 与失败策略

Trajectory metadata 至少覆盖：

```text
Source Clip
BakeProfile / Reference Rig / Avatar
Sample Rate
Duration
Baker Version
Dependency Hash
相关 Importer 依赖
```

Transition 的源 Clip、Avatar、Reference Rig、相关 Import Settings 或 Baker 格式改变后，Entry 必须显示 stale。

启用的 RootMotionClip 或使用烘焙旋转的 SelfRotationClip 如果遇到缺 key、缺 trajectory 或 stale bake，整个 Action 拒绝开始并输出包含 Actor、Action、Clip 和 key 的明确诊断。禁止静默原地播放，也禁止回退到 Animator Root Motion。

Validator 必须使用独立求值路径与 Unity 原始结果对照，不能用 Baker 自己的 samples 验证自己。

---

## 6. Sequence Clip 合同

### 6.1 AnimationPoseClip

`AnimationPoseClip` 位于 AnimationTrack：

1. 保存 AnimationConfig key 和自己的 AnimationTimeMapping 参数。
2. 从 Actor.AnimationConfig 取得 Transition。
3. 由 Sequence 映射当前 clip-local animation time。
4. Animancer state 使用 `Speed = 0`、显式设置 `Time` 并 `Evaluate()`。
5. 只更新 Pose，不产生 Gameplay Root Motion。

Sequence Pose backend 必须屏蔽 `ActorRootMotionRelay` 的 Animator delta，避免 Pose Evaluate 与 trajectory 双重移动。屏蔽必须在进入 Sequence session 时、Frame 0 第一次 Animation phase Evaluate 之前完成；不能等 RootMotionClip 在后续 Motion phase 取得 owner。只有离开整个 Sequence 播放域后，Legacy Animator Root Motion source 才允许恢复。

Session activation 可以调用一个明确的 pose-only `PrepareBaseline`：为覆盖 Frame 0 左边界的 AnimationPoseClip 创建/淡入 paused Animancer state、设置 `t = 0` 并 Evaluate。它不等价于 Gameplay Clip `OnEnter`，不得触发 Tag、Motion、HitBox 或其他 Gameplay 副作用；Frame 0 真正提交时，Pose Clip 复用该 prepared state，不再次 Play 或重复 crossfade。

### 6.2 RootMotionClip

`RootMotionClip` 位于 MotionTrack，只负责 XZ 位移：

1. 保存自己的 AnimationConfig key 和 AnimationTimeMapping。
2. 取得对应 RootMotionTrajectory。
3. 使用前一 animation time 与当前 animation time 执行 `Extract(t0, t1)`。
4. 丢弃 Y 和旋转，只提交 local XZ displacement。
5. ActorMotor 使用本 Tick 起始旋转把 local displacement 转到世界空间，再交给 KCC。

位移必须使用旋转请求生效前的 Tick 起始旋转：

```text
worldDeltaPosition = tickStartRotation * localXZDelta
```

不能先应用本 Tick SelfRotation，再用旋转后的方向转换同一段位移；否则转弯轨迹会提前转一帧。

RootMotionClip 不读取 AnimationPoseClip，也不自动与其对齐。即使本 Tick displacement 为零，Clip 仍然持有位移权，不能泄漏 Locomotion base motion。

同一个 Tick 最多一个 RootMotionClip 持有 authored displacement。资产期重叠由 Validator 报错；运行时拒绝冲突并输出诊断，不做优先级竞争或混合。

### 6.3 SelfRotationClip

`SelfRotationClip` 位于 MotionTrack，只负责 Actor 绕自身 Up 轴的左右转身。第一版不应用 Pitch/Roll。

旋转来源：

| Source | 行为 |
| --- | --- |
| Authored | 使用自己 key 对应 trajectory 的烘焙 Yaw delta |
| Target | 每个 Sequence Gameplay Frame 朝当前目标方向计算 |
| Direction | 朝 Context、输入快照或配置方向计算 |

Target/Direction 先把目标世界方向投影到 Tick 起始 `CharacterUp` 的平面，再计算它相对 Tick 起始 forward 的 signed yaw。`Snap` 使用完整 signed yaw；`RotateBySpeed` 把它钳制到本 frame 允许的最大角度。两者最终都转换为 local `Quaternion.AngleAxis(deltaYaw, Vector3.up)`，而不是向 Motor 提交另一种“绝对旋转”命令。

Target 和 Direction 支持：

- `Snap`：跨到该 Gameplay Frame 时一次提交到目标 Yaw 所需的 local delta；
- `RotateBySpeed`：每跨一个 Gameplay Frame，最多旋转 `angularSpeed / 60` 度来接近该帧目标。

Authored 来源对 trajectory 区间执行 `Extract(t0, t1)`，再从相对 Quaternion 中以 swing-twist decomposition 提取绕 local `Vector3.up` 的 twist；不能直接相减 Euler Y。具体算法固定为：把 Quaternion 向量部投影到 Up 轴，与原 `w` 组成并归一化 twist，再选择与 Identity 同半球的短弧。twist 范数接近零或采样间连续性不成立时，Validator 阻断启用该 Authored Clip，运行时也 fail-fast，禁止静默当成 Identity。Pitch/Roll 的 swing 只保留在 Pose，不进入 Gameplay rotation。

三种来源最终都在 Sequence Gameplay Frame 推进时形成一次有限旋转请求：

```text
newRotation = tickStartRotation * localYawDelta
```

Motor 原样提交这次旋转，不再乘 `MovementTimeScale`，也不在另一个 Update 中重复 RotateTowards。Sequence 减速会减少 Gameplay Frame 推进次数，HitStop 不推进 Frame，因此旋转自然与 Sequence 同步；这里不新增第二套作者可见时钟。

SelfRotationClip 与 RootMotionClip 完全独立，可以使用不同 key、范围和映射。同一个 Tick 最多一个 SelfRotationClip 持有旋转权；重叠时 Validator 报错，运行时拒绝后来者。

SelfRotationClip 随 Sequence 速度推进，HitStop 时冻结。退出旋转权时，Facing/Locomotion 的内部 baseline 必须同步到 KCC 最终实际朝向，避免动作结束后弹回旧方向。

### 6.4 AnimationTimeMapping

Pose、位移和烘焙旋转统一复用一个小型映射逻辑：

```text
Sequence Frame
Clip Start Frame
Playback Speed
Start Offset / Trim
Animation Duration
    ↓
clip-local animation time
```

映射逻辑不能分别复制在三个 Clip Runtime 中。

Gameplay Track 统一使用零起点半开区间 `[startFrame, endFrame)`，其中一个 frame 表示一个 60 Hz 模拟区间。提交 Gameplay frame `f` 时，从边界 `f` Advance 到 `f + 1`，Pose、authored XZ 和 authored Yaw 都使用同一个右边界结果。完成该 frame 的 PostWorld HitBox/Resolve 后，才退出 `endFrame == f + 1` 的 Clip。

例如 `[0, 3)`：

```text
Session activation pose baseline: t = 0
Frame 0: 0/60 → 1/60
Frame 1: 1/60 → 2/60
Frame 2: 2/60 → 3/60
PostWorld(Frame 2) 之后 Exit
```

这一定义避免首段或末段 trajectory 少算一帧，也确保最后一帧 HitBox 不会因 Action 提前 Complete 而丢失。

Session activation 与 Gameplay Frame Advance 是两个明确步骤：

1. `Activate` 在 BeginTick 只建立 Action 域、Action 自身的 SelfTags，并让 Frame 0 的 Pose source 在左边界 `t = 0` 建立可显示的起始 Pose；不调用 Gameplay Clip `OnEnter/OnTick`，不取得 Motion owner，不写 Impulse/ForceUnground，也不 Query HitBox。
2. Sequence accumulator 初值为 0；每个 Combat Tick 加入当 Tick 的 Sequence speed。累计值达到 1 才提交一个 Gameplay frame 并减 1。
3. 提交 frame `f` 时，`startFrame == f` 的 Clip 才正式 `OnEnter`，随后执行该 frame 的 PreWorld/PostWorld；因此 1 倍速在激活 Tick 提交 Frame 0，0.5 倍速通常在第二个 Tick 提交 Frame 0，0 倍速保持在 activation baseline。
4. 未提交 Gameplay frame 的 Tick 只保持 Action 域、SelfTags 和起始/上次 Pose；不会重复执行 Sequence Clip 生命周期或 Gameplay 输出。

`Advance` 和 `Seek` 必须分开：

- Advance 更新 Pose，并提交经过的 Root Motion / Self Rotation delta。
- Seek、Editor Scrub 和 SetTime 只更新 Pose，不移动或旋转 Gameplay Actor。
- Cancel 只保留已经交给 KCC 的运动；剩余 trajectory 直接丢弃。

如果 Action 在完整 HitStop 中激活，上述同一规则自然使它停在 activation baseline。恢复后的第一个累计值达到 1 的 Tick 才提交 Frame 0 一次；若此前取消，则没有 Sequence Gameplay 副作用需要偿还。

### 6.5 其他 Motion Clip

现有 Impulse、Gravity、VelocityOwner 和 MotionChannels 语义不在本轮重新设计。

- Sequence ImpulseClip 在自己首个参与的已提交 Gameplay frame 的 PreWorld 中调用 `AddImpulse/ForceUnground` 一次；activation baseline 不调用它，因此不需要新增 Motor pending-impulse staging buffer。
- Timeline VelocityClip 的 Sequence 版本留到真正迁移该功能时单独设计。
- GravityScale 如果以后需要时间区间控制，应使用明确的 MotionTrack Clip，不再放回整招 ActionMotionConfig。
- 第一版不为未来 Motion Clip 创建通用命令图、两套作者可见时钟或每 Clip HitStop 开关。

---

## 7. ActorMotor 合成合同

### 7.1 保留现有骨架

以下现有结构继续保留：

- `ActorMotor` 作为唯一 KCC `ICharacterController` 入口；
- `ActorMotionRuntime` 作为纯 C# 状态根；
- `MotionChannels` 的水平/垂直 VelocityOwner token；
- HorizontalImpulse、VerticalImpulse、Gravity、Drag 和 GroundingRuntime；
- `RootMotionBuffer` 的 pending → tick snapshot 生命周期；
- KCC 请求速度与最终实际速度分离。

Root Motion 接入是局部修改，不是 KCC 或整个 MotionChannels 重写。

### 7.2 水平与垂直合成

第一版固定规则：

```text
水平 VelocityOwner 存在
    → HorizontalVelocityOverride

否则
    →（RootMotion displacement owner active ? RootMotion XZ : 当前域的 Base XZ）
      + HorizontalImpulse

垂直 VelocityOwner 存在
    → VerticalVelocityOverride

否则
    → Gravity + VerticalImpulse
```

在 Action 域没有 RootMotionClip、VelocityOwner 或其他 base source 时，Base XZ 为零；LocomotionController 已释放 Locomotion 通道，不会继续提交新移动。

Root Motion Y 永远不进入第一版运行时。斜坡带来的世界 Y 位移仍由 KCC 的地面投影和碰撞求解产生，这不属于 Root Motion Y。

缩放点必须固定在 Motor 内，避免把 Root Motion 与其他速率一起误缩放。第一版水平公式为：

```text
if HorizontalVelocityOwner:
    horizontal = ownerVelocity * MovementTimeScale
else:
    base = rootOwner
        ? (tickStartRotation * localRootXZ) / realFixedDeltaTime
        : domainBaseVelocity * MovementTimeScale
    horizontal = base + horizontalImpulseVelocity * MovementTimeScale

horizontal = ProjectOnStableGroundTangent(horizontal)
```

这表示：RootMotion 是已经积分好的有限位移，不再缩放；HorizontalImpulse 可以与 RootMotion 相加；HorizontalVelocityOwner 存在时覆盖二者。覆盖依据 owner 是否 active，而不是本 Tick 数值是否为零。VelocityOwner 覆盖期间已经 Extract 的 RootMotion delta 在该 Tick 消费并丢弃，不能缓存到 owner 退出后偿还；HorizontalImpulse 仍按现有规则演化。垂直通道继续沿用现有 VelocityOwner 覆盖 Gravity + VerticalImpulse 的规则。

### 7.3 Action 入场与动量

删除 `horizontalMomentumInheritance` 和 `verticalMomentumInheritance` 的新架构配置。

Action 进入时：

- LocomotionController 停止提交并释放自己的通道；
- 旧 Action 的 Clip 必须释放各自 Owner；
- Motor 中已有 HorizontalImpulse、VerticalImpulse 和 Gravity 状态不按比例清除，继续按现有规则演化；
- 新 Action 的 RootMotion、Velocity 或 Impulse Clip 再显式改变运动。

例如，从跳跃进入空中攻击时不会因为 Action 入场而突然清掉上升或下落状态。

### 7.4 Trajectory source gating

Animator Root Motion 与 Trajectory Root Motion 必须明确互斥。不能通过“本 Tick displacement 是否非零”判断 owner，因为原地帧、纯旋转和 HitStop 都可能产生零位移。

Motor 需要明确的 source/owner 合同。Trajectory owner 进入、取消、退出或异常清理时必须清除 pending 数据，禁止 stale delta 在下一 Action 泄漏。Legacy Animator Root Motion 可以在迁移期保留，但 Sequence Pose 播放期间不得应用。

### 7.5 时间缩放

Sequence 速度已经改变 `previousTime → currentTime`，因此 trajectory 提取出来的是本 Tick 完整 authored displacement：

```text
requestedVelocity = extractedDisplacement / realFixedDeltaTime
```

不得再乘一次 `MovementTimeScale`。现有 Gravity、Impulse、Velocity 和 Locomotion 的 MovementTimeScale 语义保持不变。

完整 HitStop 下 Sequence 不前进，因此 RootMotionClip 和 SelfRotationClip 不产生新 delta。当前 `ActionSpeedEffect` 同时控制 ActionPlayer 与 ActorMotor 的行为先保持兼容；本阶段不借机建设新的通用时间框架。

这里的 `MovementTimeScale` 只指现有 Motor 行为。第一版不新增 `ActorLocalTimeScale`、Delta/Rate/Event 命令分类、作者可选时间域或两套可配置时钟；Clip 不选择自己的时间域。

### 7.6 Requested 与 Actual

Trajectory 和 MotionChannels 产生 Requested Motion，KCC 与 ActorCollisionResolver 产生 Actual Motion。

如果动画请求前进 1 米而 KCC 只允许 0.6 米，剩余 0.4 米直接丢弃；下一 Tick 不补偿，也不在 Action 结束时偿还。

Gameplay `CurrentVelocity` 以整个 WorldMotionCommit 后的最终世界位移为准。当前代码在 KCC 内部发布速度、随后 ActorCollisionResolver 仍可能修正位置；目标实现必须在 resolver 后通过 `FinalizeWorldMotion(realFixedDeltaTime)` 发布 final world velocity 并结束 Motor Tick。若保留 KCC-only solved velocity，它只能作为命名明确的诊断值。Grounding 仍由 KCC callback 发布，resolver 后不假装重新完成一次 ground probe。

---

## 8. CombatSimulationDriver

### 8.1 为什么需要 Driver

Driver 的作用只是把存在数据依赖的 Gameplay 阶段排成显式顺序，不是接管项目中所有 MonoBehaviour。

普通系统继续使用自己的 Unity 生命周期：

- Update：输入采样、非权威表现逻辑；
- LateUpdate：Camera、UI 和表现跟随；
- 自然播放的 Locomotion 动画可以保留在表现更新路径；
- VFX、Audio、编辑器和不影响本 Tick world solve 的逻辑不注册 Driver。

每个 Actor 只通过一个 `ActorSimulationRuntime` 参与固定模拟。Clip、Condition、ASM 等不各自向 Driver 注册回调，也不允许任意 Mono 把 delegate 塞进全局 phase。

### 8.2 Driver 归属

`CombatSimulationDriver` 是项目侧显式组件，放在 `Manager.prefab`，场景中必须恰好一个。它关闭 KCC `AutoSimulation`，使用 KCC 已公开的：

```text
PreSimulationInterpolationUpdate
Simulate
PostSimulationInterpolationUpdate
```

不修改 vendor KCC 代码，不让 KCC 反向依赖 CombatSample。

启动合同必须早于首个 FixedUpdate：Driver 在 `Awake` 中调用 `KinematicCharacterSystem.EnsureCreation()`、验证场景中只有一个 Driver，并立即设置 `Settings.AutoSimulation = false`。缺失、重复或无法取得 KCC system 时 fail-fast，不能等第一次 Driver FixedUpdate 才关闭自动模拟。

### 8.3 固定顺序

```text
1. 建立本 Tick actor 快照；注册/注销延迟到安全边界
2. 若 KCC Settings.Interpolate：KCC.PreSimulationInterpolationUpdate，并记录本 Tick 已执行
3. 所有 Actor BeginTick
   - 提交上一 Tick 排队的 Action / Locomotion 切换
   - 锁定本 Tick StateKind、CurrentAction 和 Sequence Frame
4. 所有 Actor PreWorldMotion
   - State / Tag
   - Pose Evaluate
   - RootMotion / SelfRotation / Impulse 等请求
5. KinematicCharacterSystem.Simulate
6. ActorCollisionResolver.Resolve
7. Finalize 最终世界位移/速度 readout；Grounding 仍由 KCC callback 发布
8. Physics.SyncTransforms（项目当前 Auto Sync 关闭，因此集中一次）
9. 所有 Actor PostWorldMotion
   - 保持同一 Sequence Frame 和 Pose
   - HitBox Query
10. 统一 Hit Resolve / Impact
11. 所有 Actor EndTick
12. 仅当第 2 步确实执行过 Pre：在 finally 中执行匹配的 KCC.PostSimulationInterpolationUpdate
```

HitBox 必须发生在第 12 步之前，因为 KCC PostSimulationInterpolationUpdate 会把 Transform 暂时还原到 Tick 起点用于渲染插值。

Driver 使用一个最外层 transaction guard。成功路径对每个已 Begin 的 Actor 恰好调用一次 EndTick。任一 PreWorld、KCC、Resolver、Query 或 Resolve 阶段抛异常时：

1. 丢弃尚未提交的 HitIntent、状态切换和延迟运动；
2. 对本 Tick 已 Begin 的 Actor 逐个执行 exactly-once `AbortTick/ExitAll`，释放 Action/Locomotion owner、清除 trajectory pending、HitBox dedup 和未消费的 action-scoped 状态；
3. 如果本 Tick 执行过 interpolation Pre，执行匹配的 Post；
4. 最后处理延迟注册/注销，记录完整诊断并禁用 Driver，避免在部分 world solve 后继续运行一个已不可置信的模拟。

已经在异常前提交到外部对象的副作用不尝试回滚，因此异常属于 fail-fast 配置/程序错误，不是正常 Gameplay 分支。

### 8.4 PreWorld 与 PostWorld

当前 Sequence 的 Phase 排序不足以形成物理屏障。目标 Runtime 必须把同一 Gameplay Frame 拆成两部分：

```text
PreWorld：State → Animation → Motion
WorldMotionCommit
PostWorld：HitBox → Cleanup/结果
```

中间不能推进到下一 Sequence Frame，也不能重新 Evaluate 不同 Pose。

如果该 Actor 本 Tick 的 Sequence 累加器没有跨过一个 Gameplay Frame，则不执行该 Actor 的 Sequence State/Pose/Motion/HitBox `OnTick`，也不重复查询同一骨骼帧。已经存在于 Motor 内的 Velocity owner、Impulse、Gravity 等状态仍按它们当前规则运行。

Clip 生命周期也必须跨过完整屏障：`startFrame == f` 的 Clip 在 frame `f` 的 PreWorld 前进入；`endFrame == f + 1` 的 Clip 保持到 frame `f` 的 Hit Query/Resolve 完成，再在 EndTick 退出。当前 frame 是 Action 最后一帧时，`Complete → ExitAll → ActionFinished` 同样延迟到该 frame 的 EndTick commit。若取消请求在 BeginTick 被正式接受，则在本 frame PreWorld 之前清理并且不再执行被取消 Action 的任何输出。

所有预先编排的 RootMotion、SelfRotation、Velocity 和 Impulse 在 PreWorld 提交。HitBox 命中后产生的目标 Impulse、受击 Action、死亡或其他状态变更，从下一 BeginTick 生效；第一版不为此做同 Tick 第二次 KCC solve。

命中处理采用 Query 与 Resolve 分离：先让所有 HitBox 基于同一个已提交世界状态生成 `HitIntent`，再按稳定键（Tick、attacker stable id、clip/hitbox stable id、target stable id）排序结算。这样结果不依赖 Actor 注册或容器遍历顺序；“死亡是否取消同 Tick 后续命中、是否允许相杀”仍是明确的 Gameplay Resolve policy，不能只靠 Query/Resolve 分离暗中决定。

### 8.5 60 Hz 与掉帧

Combat Fixed Tick、KCC 和 Gameplay Sequence 统一为 60 Hz。Unity `Fixed Timestep` 已迁移为 `1 / 60`，不再使用 50 Hz KCC 配 60 Hz Sequence。Gameplay Sequence 的 Validator 与启动校验仍需在后续阶段落实 `frameRate == 60` 门禁。

第一版 Sequence 全局运行速度只支持 `0..1`。每个 Combat Tick 最多推进一个 Gameplay Frame：

- 低于 1 倍速时使用固定帧累加器，未跨帧的 Tick 不产生新的 Sequence Gameplay 输出；
- HitStop 为 0 时不跨帧；
- 渲染掉帧由 Unity 在 fixed catch-up 预算内连续执行多个完整 Fixed Tick 追赶，每个 Tick 都包含完整的 Pose → Motion → KCC → HitBox；超过 Unity `Maximum Allowed Timestep` 的部分让世界时间变慢，也不能折叠到一次 KCC；
- 禁止在一次 KCC commit 前用 `while` 折叠多个 Gameplay Frame；
- Sequence session speed 超过 1 时第一版由 Validator 和运行时明确拒绝，不静默折帧；该限制不反向破坏迁移期 Legacy Timeline 的既有 speed 行为。

AnimationTimeMapping 自己的 clip-local playback speed 可以超过 1，因为它只决定一个 Sequence Frame 映射多少动画时间，不会让一次 world commit 跨多个 Gameplay Frame。

---

## 9. HitBox 与骨骼同步

一个 Action Gameplay Frame 的正确关系是：

```text
Sequence Frame N
    ↓
Animancer Evaluate Pose N
    ↓
RootMotion / SelfRotation N
    ↓
KCC + Actor 互推得到最终 Root N
    ↓
Physics.SyncTransforms
    ↓
用 Pose N 的骨骼 + 最终 Root N 查询 HitBox
```

这同时保证：

- HitBox 不使用上一帧 Root Motion；
- HitBox 不使用下一帧骨骼 Pose；
- 撞墙时攻击范围跟随 KCC 实际位置，而不是动画期望位置；
- 开启或关闭 KCC interpolation 都不会改变 Gameplay query 所见的模拟姿态。

每个 HitBox Clip 保留自己的单次目标去重集合。取消、自然完成、Disable 和异常退出都必须清理集合及骨骼引用。

权威 Combat Physics Query 只允许发生在 Driver 的 `Physics.SyncTransforms` 之后、匹配的 KCC `PostSimulationInterpolationUpdate` 之前。普通 Update/LateUpdate 不得执行依赖“本 Tick 最终 Actor 位置”的 Gameplay 命中查询；渲染插值 Transform 与 physics broadphase 在这一阶段可能有意不同。

---

## 10. ActionMotionConfig 的最终去向

新架构不再在 ActionAsset 表面保留一份整招级 `ActionMotionConfig`。

| 旧字段 | 新归属 |
| --- | --- |
| `rootMotionMode` | 是否存在有效 RootMotionClip |
| `suppressLocomotion` | Action / Locomotion 域互斥的固定规则 |
| `facingOnStart` | Frame 0 的 SelfRotationClip，可用 Snap |
| `gravityScale` | 需要时由明确时间范围的 GravityScaleClip 表达 |
| 水平/垂直动量继承 | 删除；Motor 原状态自然延续 |

在对应 Clip、域切换和清理合同落地以前，当前字段仍作为 Legacy 兼容层存在，不能先删序列化字段或破坏旧 ActionAsset。

---

## 11. 当前仓库审计

以下是 2026-08-10 的代码事实，不是尚未落地目标的完成声明：

| 当前实现 | 与目标的差异 |
| --- | --- |
| `ActionAsset` 同时保存 Timeline、backend enum 和 SequenceData | 仍是迁移期双后端；最终只保留内嵌 SequenceData |
| `ActionPlayer.Update()` 用 `Time.deltaTime` 推进 Session | 权威 Sequence 推进尚未进入 CombatSimulationDriver |
| Sequence `Start()` 立即执行 Frame 0 | Frame 0 目前可能在 LateUpdate/任意 BeginAction 调用阶段执行 |
| `ActionSequenceRuntime.Tick()` 可以在一次调用中 while 补多帧 | 不能保证每个 gameplay frame 都经过一次 KCC 和 HitBox barrier |
| Unity Fixed Timestep 已为 `1 / 60`，Sequence 默认 60 Hz | 世界固定时钟已统一；Sequence 资产与启动门禁尚未落实 |
| SequenceData 允许任意正 frameRate，Sequence session speed 也未拒绝大于 1 | Gameplay Sequence 的 60 Hz 与 `0..1` 限制尚未形成 Validator/运行时门禁 |
| `CombatSimulationDriver` 已在 Manager 上唯一接管 KCC，关闭 AutoSimulation 并调用公开手动模拟 API | 当前仍是保持旧行为的兼容切片，尚未接入 Actor/Sequence PreWorld 与 PostWorld |
| ActorCollisionResolver 已移除独立 `FixedUpdate`，由 Driver 在 KCC interpolation Post 后显式调用 | 已消除第二入口；最终 WorldMotionCommit 仍需把 Resolver、SyncTransforms 和 HitBox 放到 interpolation Post 之前 |
| Physics `m_AutoSyncTransforms = 0` | HitBox 前尚无 Driver 统一调用 `Physics.SyncTransforms` |
| `ActionSequenceRunner` 使用独立 FixedUpdate -40 | 只能作为调试/预览入口，不能成为第二套生产权威 |
| ActorLogicInput 每 Update 直接 `SetLocomotionIntent` 到 Motor | 尚未改为只保存，LocomotionController 尚不存在 |
| ASM 的 Locomotion start context、Action `facingOnStart` 与相关 Condition 仍从 ActorMotor 读取 Intent | ActorLogicInput 停止推 Motor 时必须一起改读其最新保存值 |
| ASM 在 LateUpdate 仲裁并立即 BeginAction | 尚未改为请求排队、BeginTick 提交 |
| CancelRule 只有 Specific/Tag/Any，没有 Conditions 或 Locomotion target | Locomotion 取消合同尚未实现 |
| Animancer Clip 直接保存 TransitionAsset | AnimationConfig key 查询尚未实现 |
| RootMotionTrajectory、Baker、RootMotionClip、SelfRotationClip 均不存在 | 本文的数据与 Clip 管线全部待实现 |
| RootMotionBuffer 只接 Animator delta，并简单累加 position | source gating 与正确 trajectory 数学尚未实现 |
| 现有 Animator Root Motion 分支按 position 非零提前 return，并乘 MovementTimeScale | 若直接复用它接 trajectory，会吞掉其他通道、用数值误判 owner 并二次缩放 authored displacement |
| ActorMotor 在普通 Update 计算 Locomotion/Facing | 权威计算尚未进入 PreWorldMotion |
| HitBox Clip 在 Sequence OnTick 中立即 Query 并 TakeDamage | 尚未移动到 KCC 后，也没有集中 SyncTransforms 或 Query/Resolve 分离 |
| ActionMotionConfig 仍由 ActionInstance OnEnter/Exit 整招应用 | 尚未迁移到域规则与具体 Clip |
| Action 结束时 `ActionPlayer` 调用 `ClearTransientTags()` 全清 | 目标需要按 owner 释放 Action tags，并在域切换事务中原子写入胜选 Mode tags |

仓库目前已有 70 个 ActionAsset；其中仅 3 个显式选择 Sequence backend，67 个仍按 Legacy Timeline 路径运行。`Assets/Create/ActionAssets` 下 68 个 Action 都仍保存 Timeline 引用。因此不能先删除 Legacy 字段、PlayableDirector 或 Timeline Session。

当前已有 84 个 ActionSequence/ActionPlayer Editor 测试，主要覆盖编辑器、固定帧 Runtime 和 ActionPlayer 生命周期；尚没有覆盖新 Root Motion/Motor/Driver 管线的自动测试。本次文档工作没有运行 Unity Test Runner。

关键证据入口：

- Action 双后端与播放时机：`Assets/Scripts/ActionSystem/ActionAsset.cs`、`Assets/Scripts/Actor/ActionPlayer.cs`、`Assets/Scripts/Actor/ActionStateManager.cs`；
- Sequence Frame 0、while 补帧与 Clip phase：`Assets/Scripts/Actor/SequenceActionPlaybackSession.cs`、`Assets/Scripts/ActionSequence/ActionSequenceRuntime.cs`；
- KCC 自动/手动模拟与插值：`Assets/Plugins/KinematicCharacterController/Core/KinematicCharacterSystem.cs`、`KCCSettings.cs`；
- Motor/Root Motion 合成：`Assets/Scripts/Actor/ActorMotor.cs`、`Assets/Scripts/Actor/Motion/ActorMotionRuntime.cs`、`RootMotionBuffer.cs`、`MotionChannels.cs`；
- 当前输入、HitBox 与时间设置：`Assets/Scripts/Actor/ActorLogicInput.cs`、`Assets/Scripts/ActionSequence/Clips/ActionSequenceHitBoxClipDefinition.cs`、`ProjectSettings/TimeManager.asset`、`ProjectSettings/DynamicsManager.asset`。

---

## 12. Legacy Timeline 迁移

迁移保持同一个 ActionAsset GUID，不创建替代 Action 类型：

1. 暂停新增 Legacy Timeline 内容，普通创建入口默认只创建 Sequence Action。
2. 先补齐 AnimationConfig、Driver、RootMotion/SelfRotation、LocomotionController 和所需 Sequence Clip。
3. 对每个 Timeline 做只读审计，列出所有 Track、Clip、时间、ClipIn、速度和不支持项。
4. 转换器统一定义 seconds → frame 的取整规则、`[startFrame, endFrame)` 区间、ClipIn 与 TimeScale 映射；同一输入重复转换必须得到相同结果，不重复追加 Clip。
5. 转换结果写入同一 ActionAsset 的内嵌 SequenceData；源 Timeline 暂时保留用于对照。
6. 任何不支持 Track 都必须阻断写入后的 backend 切换，转换器输出完整报告，禁止静默跳过。
7. Idle、Run 和方向 Mixer 等旧 Locomotion Action 不转成 ActionSequence：把它们的 Priority、EntryConditions、SelfTags、动画和移动参数迁到 `LocomotionModeAsset`，再从 ActionList 移除。
8. 指向旧 Locomotion Action 的 SpecificAction 或 `Action.Move` Tag CancelRule 改为 `CancelTargetKind.Locomotion + rule conditions`；引用报告归零前不删除旧资产。
9. 逐资产验证 Pose、Tag、HitBox、运动、取消、HitStop、结束和中断清理后，再切换 backend。
10. 所有引用完成迁移后，统一删除 backend enum、Timeline 字段、Timeline Session、PlayableDirector 强依赖、Timeline Playable tracks/clips、编辑器 helper 以及旧创建/打开入口。

现有 Sequence 尚未覆盖 Timeline 的全部能力；Velocity、Magnetism、Effect、ContinuousAnimancer 和 Cleanup 等内容需要先有明确的 Sequence 对应物或人工迁移方案。

Legacy Timeline 当前也没有保证“编辑器标记的第 N 帧 Pose → Animator Root Motion → KCC → Trigger HitBox”严格发生在同一个固定 Tick：Director/Animancer 多为自然时间推进，Animator delta 由下一次 Motor Tick 消费，HitBox 依赖独立 Update/Physics 生命周期。因此迁移目标是保持作者可观察的动作语义，不复刻旧后端偶然形成的延迟或顺序 bug。一帧 Clip、TimeScale 大于 1、掉帧、撞墙和中断必须逐项人工回归。

---

## 13. 实施顺序

### Stage A：冻结基线

- 为现有 MotionChannels、Grounding、Impulse、VelocityOwner 和 Action 切换建立自动化回归测试。
- 把当前 Root Motion、HitStop、斜坡、跳跃和 Actor 互推手动基线重新跑一遍。
- Legacy 创建入口明确标记，不继续扩大迁移量。

### Stage B：固定模拟骨架

- 已完成：新增 Manager 上唯一 CombatSimulationDriver。
- 新增每 Actor 一个 ActorSimulationRuntime。
- 已完成：在 Driver Awake 中验证唯一实例并关闭 KCC AutoSimulation，接管公开手动 Simulate 顺序。
- 将 ActorCollisionResolver、SyncTransforms 和 Sequence Pre/Post 屏障接入；HitBox 只进入 PostWorld。
- 已完成：把 Fixed Timestep 统一到 60 Hz。
- Gameplay Sequence Validator/启动入口拒绝非 60 Hz 数据，Sequence session 唯一速度入口拒绝大于 1。

### Stage C：AnimationConfig 与 Baker

- 实现 AnimationConfig、Entry 和 Actor 引用。
- 实现 BakeProfile、Baker、Trajectory、Sample/Extract、metadata 和 Validator。
- 实现 Generated 资产工作流与缺失/stale 启动阻断。

### Stage D：Sequence Pose / Motion

- AnimationPoseClip 改为 key 查询。
- 实现 RootMotionClip 和 SelfRotationClip。
- 局部升级 RootMotionBuffer、ActorMotionRuntime、ActorMotor、Facing handoff 和 source gating。
- 验证同帧 Pose、位移、旋转、KCC 和 HitBox。
- 固化 `[startFrame,endFrame)`、Frame 0 activation/freeze、最后一帧 PostWorld 后清理的测试。

### Stage E：Locomotion 与 ASM

- ActorLogicInput 改为只保存 Intent。
- ASM start context、Direction Condition 和 Legacy facing fallback 一并改读 ActorLogicInput 的最新 Intent。
- 实现 LocomotionController / LocomotionModeAsset / Fallback。
- ASM 增加 StateKind、BeginTick 提交、CancelRule Conditions 和 Locomotion target。
- Tag 改为按 owner 释放，并在 Action/Mode 域切换时事务式提交。
- 移除新 Action 对整招 MotionConfig 和动量继承的依赖。

### Stage F：内容迁移与清理

- 原地迁移 Action 与 Locomotion Timeline。
- 完成逐资产验证。
- 最后删除 Legacy Timeline runtime 和旧序列化字段。

每个 Stage 应独立可验证，不把 Driver、Baker、Locomotion 和全部内容迁移塞进同一个修改。

---

## 14. 第一版明确不做

- Root Motion Y、Pitch/Roll Gameplay Rotation；
- Vault、Climb、Traversal、Execution 的完整 Root Motion；
- RootMotion 与 LocomotionInput 同时作为 base motion；
- 多个 RootMotionClip 或多个 SelfRotationClip 加权混合；
- 根据 Animancer blend weight 自动混合 trajectory；
- Motion Warping、Attack Snap、距离缩放；
- Gameplay Reverse；
- Gameplay Loop 的完整实现；
- 通用 Motion Modifier Graph / DAG；
- 为每种 MonoBehaviour 注册全局 Update callback；
- 动态 substep、rollback 或通用物理调度器；
- Sequence 全局速度大于 1；
- 新的 AnimAction、LegacyTimelineAction 或 DefaultAction 体系；
- 借 Root Motion 工作重写现有 Velocity、Impulse、Gravity 或 KCC 算法。

---

## 15. Definition of Done

### 数据与 Baker

- 真实 Reference Avatar 上能生成 `M(0)=Identity` 的 trajectory。
- `Sample(float)`、`Extract(t0,t1)` 和连续 SE(3) Delta 组合测试通过。
- Translation、Rotation、Translation+Rotation、in-place、Root Y、快速转身、Humanoid 和可用 Generic 资产通过独立 Validator。
- source、Avatar 或 Import Settings 改变后能可靠显示 stale。
- 生成资产无需作者手工命名或配对。

### Runtime

- Pose Clip、RootMotionClip、SelfRotationClip 可独立使用不同 key 和范围。
- RootMotion 只影响 XZ，SelfRotation 只影响 Yaw，垂直通道保持现有行为。
- Authored SelfRotation 从 Quaternion 稳定提取 Yaw；同 Tick XZ 使用 tick-start rotation，不被本 Tick Yaw 提前旋转。
- RootMotion + HorizontalImpulse、HorizontalVelocityOverride、Gravity、VerticalImpulse 的固定合成规则通过测试。
- 零位移 RootMotion frame 仍保持 owner；VelocityOverride 覆盖期间不会积欠 RootMotion delta。
- Animator Root Motion 与 trajectory 永不双应用。
- RootMotion delta 不发生 MovementTimeScale 二次缩放。
- Action Cancel、自然结束、Disable 和异常退出不残留 owner 或 pending delta。
- 撞墙后的 blocked displacement 不补偿。
- SelfRotation 结束后朝向不弹回。

### Tick 与 HitBox

- Sequence Frame N 的 Pose/Motion 在同一 Combat Tick 被 KCC 消费。
- HitBox 使用 Frame N 的骨骼和 KCC+互推后的最终 Root。
- Physics Auto Sync 关闭时仍能查询到本 Tick 正确 Collider 位置。
- KCC interpolation 开/关不改变 Gameplay 命中结果。
- 渲染掉帧时，每个补做的 Gameplay Frame 都经历完整 world commit。
- 0.5 倍速未进帧的 Tick 不重复 Pose/HitBox OnTick；HitStop 不跳事件、不重复 Impulse、不补偿冻结时间。
- Action 在 HitStop 或低速未进帧时只建立 activation baseline；Impulse/ForceUnground 直到 Frame 0 真正提交才调用，提前取消不会残留副作用。
- 最后一帧 HitBox 在 Action Complete/ExitAll 前完成。
- HitIntent 使用稳定键结算，同 Tick 结果不依赖 Actor 注册或容器遍历顺序；死亡/相杀遵循显式 Resolve policy。

### Action 与 Locomotion

- Action 可以从任意合法 LocomotionMode 进入。
- Action 自然结束或命中 Locomotion CancelRule 后正确选择 Mode。
- Fallback 缺失/失效会被 Validator 阻断；损坏配置下无人中标时取消失败且不消费 Condition。
- Action 期间输入持续更新，但 LocomotionController 不向 Motor 或 Animancer 争夺权威。
- 返回 Locomotion 时从 KCC 最终朝向和最新 Intent 平滑接管。
- Action 入场不再清除已有垂直/水平动量状态。

---

## 16. 最终一句话

> AnimationConfig 告诉角色有哪些 Pose 和烘焙运动；ActionSequence 决定当前 Gameplay Frame；Pose、位移和自身旋转由独立 Clip 明确提交；CombatSimulationDriver 先让 ActorMotor/KCC 得到最终世界状态，再用同一 Pose 做 HitBox；没有 Action 时，LocomotionController 独立接管角色。
