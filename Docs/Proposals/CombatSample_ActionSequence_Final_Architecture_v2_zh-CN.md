# CombatSample ActionSequence 最终架构 v2

> 状态：已批准的实施基线，分阶段实施中；Stage B 固定接线、Stage C1–C2.4 AnimationConfig/Baker 作者工作流、Stage D1–D3 Pose/Motion 基础 Clip 已落地
>
> 日期：2026-08-10
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

过渡期实现允许 `ActorLogicInput` 同时保存 latest intent 并继续推送给 ActorMotor，以保持现有 Locomotion 行为。`ActionContext` 与 ASM 已改读 latest intent；等 `LocomotionController` 接管 Locomotion 域后，再移除直接推 Motor 的兼容路径。

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

### 4.6 ActionContext 与 ActionCandidate

`ActionContext` 是每次 Action 启动时冻结的一份只读快照，不是 Motion 或 Rotation 专用数据。它只表达本次 Action 调用的基础关系和主方向/主位置/主数值：

```text
Instigator
Target
Point
Direction
Magnitude
```

`Direction`、`Point`、`Magnitude` 使用显式 presence flag；`Instigator`、`Target` 的 presence 由对象引用是否非空自动体现。对象引用用 null 表达缺失。`Direction` 存世界空间归一化 3D 方向，零向量或非有限值是非法输入。`Point` 与 `Magnitude` 只要求有限，不做范围解释。

Context 的来源规则：

- Poll Action 根据 `StartContextMode` 在候选创建时冻结 Context。
- `StartContextMode.None` 创建 self/self 参与者。
- `StartContextMode.LocomotionIntent` 从 `ActorLogicInput.LatestLocomotionIntent` 读取方向与强度；缺少 `ActorLogicInput` 是配置错误，候选无效。
- Event 与 External Request 必须显式携带 Context。
- Loop 重播复用本次 Action 最初的 Context。

ASM 内部不再把三类来源都压扁成 `ActionAsset` 列表，而是在仲裁期间使用 `ActionCandidate`：

```text
ActionAsset
ActionContext
Origin(Poll/Event/External)
SubmissionOrder
ExternalRequest(optional)
```

Priority 仍只来自 ActionAsset；来源不额外加权。相同优先级按真实提交/创建顺序稳定选择。多个 External 请求即使指向同一个 Action，也按具体 request 实例回调：只有胜选的那一个返回 true。

Conditions 可以读取 Candidate Context；`OnClaim` 仍只接收 Actor，因为 Context 本身不可消费。Clip 可以声明 required context fields；缺字段的 Candidate 在 Claim 和 Action 启动前被拒绝。

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

### 5.2 AnimationConfig 自己拥有 Bake 上下文

Reference Character Prefab、Animator、Avatar、采样率和 Validator 参数由 `AnimationConfig` 的 Editor-only Bake Context 直接保存，不再创建独立 `RootMotionBakeProfile` 资产，也不进入运行时 Entry。Inspector 的 Bake/Rebake 始终使用当前 Config 的明确字段，不按命名或目录猜测上下文；Player 构建中的运行时查询 API 不暴露这些 Editor 字段。

每个角色或 Rig / Avatar Family 使用自己的 AnimationConfig。不同体型是否共用同一 trajectory 必须通过 Validator 证明；不使用猜测性的 `humanScale` 修正。

### 5.3 作者工作流

作者只在 Entry 中填写 `TransitionAsset`，然后在 AnimationConfig Inspector 中执行：

```text
Bake / Rebake / Bake All
```

对于能唯一解析出一个 AnimationClip 的 Transition，Baker 自动取得源 Clip。Entry 中不再要求作者重复填写一个 `RootMotionSourceClip`。

生成的 `RootMotionTrajectory` 是普通可序列化数据，直接内嵌在对应 `AnimationConfig.Entry` 中。一个 AnimationConfig 是动画引用、Bake 上下文和生成运动数据的唯一物理资产；不再创建 `Generated/` 目录或每动画一个 `.asset`。Entry 使用显式存在标记区分“尚未 Bake”和合法的零运动数据，不能依赖 Unity 对内嵌 class 的 null 序列化行为。

`Bake` 与 `Rebake` 使用同一操作：先在内存中完成 Bake、独立 Oracle Validate、DependencyHash 和结构校验，全部成功后才以一次 Config 修改替换 Entry 内的数据；失败保留上一份有效 trajectory。`Bake All` 处理所有可唯一解析到单 Clip 的 Entry，并明确跳过第一版只能 Pose-only 的多 Clip Transition。`Clear Data` 只清除该 Entry 的内嵌数据，不涉及磁盘孤儿或跨 Entry 所有权。

Mixer 或 Directional Transition 第一版可以 Pose-only，但不能直接 Bake Gameplay Root Motion。需要位移时，RootMotionClip 使用另一个能唯一解析到单 Clip 的 key；不在运行时混合多个子动画 trajectory。

### 5.4 Baker 合同

```text
AnimationClip
    +
AnimationConfig Editor Bake Context
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
Sample Rate
Duration
Baker Version
Dependency Hash
相关 Importer 依赖
```

Reference Rig、Avatar 与 Validator 容差由 AnimationConfig 保存，并作为 DependencyHash 输入而不是重复写进每个 Entry。Transition 的源 Clip、Avatar、Reference Rig、相关 Import Settings 或 Baker 格式改变后，Entry 必须显示 stale。

当前 `DependencyHash` 明确包含 Baker version、采样率、Validator 容差、源 Clip GUID/local id/依赖哈希、Reference Rig 与 Avatar 的 GUID/local id/依赖哈希。它不能包含 AnimationConfig 自身的依赖哈希，否则内嵌 trajectory 的每次成功写入都会让自己立即 stale。所有外部依赖必须是持久化资产，禁止用运行时 instance id 产生重启后变化的“伪稳定”结果。AnimationConfig Inspector 显示 `Ready / Missing / Stale / PoseOnly / Invalid`，并保留最近一次 Bake/Validate 的明确失败信息。

启用的 RootMotionClip 或使用烘焙旋转的 SelfRotationClip 如果遇到缺 key、缺 trajectory 或 stale bake，整个 Action 拒绝开始并输出包含 Actor、Action、Clip 和 key 的明确诊断。禁止静默原地播放，也禁止回退到 Animator Root Motion。

Validator 必须使用独立求值路径与 Unity 原始结果对照，不能用 Baker 自己的 samples 验证自己。

当前 Validator 会从同一 AnimationConfig Bake Context 重新实例化一份独立 Reference Rig，并创建自己的 Manual PlayableGraph。它通过 `OnAnimatorMove` 逐步读取 Unity 的 `Animator.deltaPosition / deltaRotation` 并累计，不调用 Baker，也不复用 Baker 的 Transform 捕获或累计数学。两条路径只共享源 Clip、Config Bake Context 与待比较的采样时间。

Validator 逐点比较累计位置与旋转，报告最大位置误差、最大旋转误差、各自发生的 sample index/time，以及 `M(0)`、Duration 和容差越界诊断。位置与角度容差由 AnimationConfig 明确配置。

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

当前已落地 D2.2：SelfRotationClip 具有两个主要配置维度：`Rotation Source = RootRotation / Target / Direction`，`Rotation Mode = Snap / RotateBySpeed`。所有来源先在整数 Gameplay Frame 计算目标 Yaw，再通过现有 SelfRotation owner 提交给 ActorMotor/KCC。

旋转来源：

| Source | 行为 |
| --- | --- |
| RootRotation | 使用自己 key 对应 trajectory 的烘焙 Yaw 累计目标 |
| Target | 每个 Sequence Gameplay Frame 朝当前目标方向计算 |
| Direction | 朝 Context 启动快照方向或配置方向计算 |

Target/Direction 先把目标世界方向投影到 Tick 起始 `CharacterUp` 的平面，再计算它相对 Tick 起始 forward 的 signed yaw。`Snap` 使用完整 signed yaw；`RotateBySpeed` 把它钳制到本 frame 允许的最大角度。两者最终都转换为 local `Quaternion.AngleAxis(deltaYaw, Vector3.up)`，而不是向 Motor 提交另一种“绝对旋转”命令。

Target 和 Direction 支持：

- `Snap`：跨到该 Gameplay Frame 时一次提交到目标 Yaw 所需的 local delta；
- `RotateBySpeed`：每跨一个 Gameplay Frame，最多旋转 `angularSpeed / 60` 度来接近该帧目标。

RootRotation 来源对 trajectory 区间执行 `Extract(t0, t1)`，再从相对 Quaternion 中以 swing-twist decomposition 提取绕 local `Vector3.up` 的 twist；不能直接相减 Euler Y。具体算法固定为：把 Quaternion 向量部投影到 Up 轴，与原 `w` 组成并归一化 twist，再选择与 Identity 同半球的短弧。twist 范数接近零或采样间连续性不成立时，Validator 阻断启用该 RootRotation Clip，运行时也 fail-fast，禁止静默当成 Identity。Pitch/Roll 的 swing 只保留在 Pose，不进入 Gameplay rotation。

RootRotation 的 `RotateBySpeed` 不会逐帧丢失被钳制的角度：Clip 进入时以当前 KCC 朝向建立基准，每个整数 Gameplay Frame 将烘焙 yaw delta 累计成 authored 目标朝向，再从当前实际朝向按角速度追赶该累计目标。Clip 退出时丢弃剩余未追上的角度，不在动作结束后补偿。

Target 支持 `CombatTarget`、`ContextInstigator`、`ContextTarget`。`CombatTarget` 每个 Gameplay Frame 重新读取 ActorCombater 的当前目标；Context 引用在 Action 启动时冻结，但每帧读取该对象的当前位置。目标缺失、销毁或水平距离退化时，该帧提交零旋转、保持当前朝向并只诊断一次；目标恢复后继续追踪。

Direction 支持 `PresetLocal` 和 `ContextDirection`。`PresetLocal` 在 Clip 进入时用当前 simulation rotation 转成固定世界方向，避免随 Actor 自身旋转而无限转动；`ContextDirection` 使用 Action 启动时冻结的世界方向。

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
4. 未提交 Gameplay frame 的 Tick 只保持 Action 域、SelfTags 和起始/上次 Gameplay Frame；当 Sequence speed 在 `(0, 1)` 时，可以执行 Animation-only PoseRefresh，以 fractional `PoseFrame` 平滑刷新视觉 Pose，但不得触发 State/Motion/HitBox/Cleanup、Clip enter/exit 或任何 Gameplay 输出。

`Advance` 和 `Seek` 必须分开：

- Advance 更新 Pose，并提交经过的 Root Motion / Self Rotation delta。
- Seek、Editor Scrub 和 SetTime 只更新 Pose，不移动或旋转 Gameplay Actor。
- Cancel 只保留已经交给 KCC 的运动；剩余 trajectory 直接丢弃。

如果 Action 在完整 HitStop 中激活，上述同一规则自然使它停在 activation baseline。恢复后的第一个累计值达到 1 的 Tick 才提交 Frame 0 一次；若此前取消，则没有 Sequence Gameplay 副作用需要偿还。

### 6.5 其他 Motion Clip

当前已落地 D3：`VelocityOverrideClip` 位于 MotionTrack，复用 Timeline 的 `VelocityConfig`，按区间对水平和/或垂直速度取得 owner。轴开关是唯一 authority 声明；开启后速度为 0 仍取得该轴控制权。两个轴都未开启是无效配置，Action 启动前阻断。

VelocityOverrideClip 的水平方向支持：

- `PresetLocal`：每个整数 Gameplay Frame 使用 Tick 起始 KCC 朝向把本地方向转换到世界方向；同 Tick SelfRotation 不会提前旋转本帧 Velocity；
- `ContextDirection`：使用 Action 启动时冻结的世界方向，并声明 `ActionContextFieldMask.Direction`。

速度曲线只在整数 Gameplay Frame 采样并更新缓存。第一帧采样 0，最后一帧采样 1，单帧 Clip 采样 0。未推进 Gameplay Frame、fractional PoseRefresh、Pause 和完整 HitStop 帧不更新曲线；Motor 继续使用该 owner 上一次缓存的速度。配置速度单位保持 m/s，Clip 不自行乘 Sequence speed，MovementTimeScale 仍由 Motor 在输出端统一应用。

`MotionChannels` 的水平/垂直 VelocityOwner 已升级为两个独立覆盖栈：后进入者覆盖，退出后恢复下层 owner 的当前缓存值。隐藏 owner 的 `SetVelocity` 仍更新自己的缓存；旧 token 的提交和释放不能影响当前栈顶；`ClearVelocityOwners` 清空两个轴。Sequence 同帧多个同轴 VelocityOverride 按现有稳定排序执行，后 Track、后 Clip 最后取得 owner 并获胜。不同轴互不竞争。

RootMotion 与水平 VelocityOverride 可以重叠，不报错；Velocity 覆盖期间当帧提取出的 RootMotion XZ delta 直接丢弃，退出后不补偿。HorizontalImpulse 在被遮住期间继续按现有规则衰减，覆盖结束后残余重新参与。VerticalVelocityOverride 活跃时 Gravity 暂停累计；VerticalImpulse 继续按现有阻力、落地和撞顶规则演化。

现有 Impulse、Gravity 和 MotionChannels 的非 VelocityOwner 语义不在本轮重新设计。

- Sequence ImpulseClip 在自己首个参与的已提交 Gameplay frame 的 PreWorld 中调用 `AddImpulse/ForceUnground` 一次；activation baseline 不调用它，因此不需要新增 Motor pending-impulse staging buffer。
- GravityScale 如果以后需要时间区间控制，应使用明确的 MotionTrack Clip，不再放回整招 ActionMotionConfig。
- 第一版不为未来 Motion Clip 创建通用命令图、两套作者可见时钟或每 Clip HitStop 开关。

---

## 7. ActorMotor 合成合同

### 7.1 保留现有骨架

以下现有结构继续保留：

- `ActorMotor` 作为唯一 KCC `ICharacterController` 入口；
- `ActorMotionRuntime` 作为纯 C# 状态根；
- `MotionChannels` 的水平/垂直 VelocityOwner token 与可恢复覆盖栈；
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

中间不能推进到下一 Sequence Frame，也不能为同一 Gameplay Frame 改用另一个命中 Pose。

如果该 Actor 本 Tick 的 Sequence 累加器没有跨过一个 Gameplay Frame，则不执行该 Actor 的 Sequence State/Motion/HitBox `OnTick`，也不重复查询同一骨骼帧。Animation phase 可以在 `0 < speed < 1` 时执行 PoseRefresh，只刷新已 active 的 AnimationPoseClip，不 enter/exit Clip，不推进 CurrentFrame 或 normalized time。已经存在于 Motor 内的 Velocity owner、Impulse、Gravity 等状态仍按它们当前规则运行。

Clip 生命周期也必须跨过完整屏障：`startFrame == f` 的 Clip 在 frame `f` 的 PreWorld 前进入；`endFrame == f + 1` 的 Clip 保持到 frame `f` 的 Hit Query/Resolve 完成，再在 EndTick 退出。当前 frame 是 Action 最后一帧时，`Complete → ExitAll → ActionFinished` 同样延迟到该 frame 的 EndTick commit。若取消请求在 BeginTick 被正式接受，则在本 frame PreWorld 之前清理并且不再执行被取消 Action 的任何输出。

所有预先编排的 RootMotion、SelfRotation、Velocity 和 Impulse 在 PreWorld 提交。HitBox 命中后产生的目标 Impulse、受击 Action、死亡或其他状态变更，从下一 BeginTick 生效；第一版不为此做同 Tick 第二次 KCC solve。

命中处理采用 Query 与 Resolve 分离：先让所有 HitBox 基于同一个已提交世界状态生成 `HitIntent`，再按稳定键（Tick、attacker stable id、clip/hitbox stable id、target stable id）排序结算。这样结果不依赖 Actor 注册或容器遍历顺序；“死亡是否取消同 Tick 后续命中、是否允许相杀”仍是明确的 Gameplay Resolve policy，不能只靠 Query/Resolve 分离暗中决定。

### 8.5 60 Hz 与掉帧

Combat Fixed Tick、KCC 和 Gameplay Sequence 统一为 60 Hz。Unity `Fixed Timestep` 已迁移为 `1 / 60`，不再使用 50 Hz KCC 配 60 Hz Sequence。Gameplay Sequence 的 Validator 与启动校验仍需在后续阶段落实 `frameRate == 60` 门禁。

第一版 Sequence 全局运行速度只支持 `0..1`。每个 Combat Tick 最多推进一个 Gameplay Frame：

- 低于 1 倍速时使用固定帧累加器，未跨帧的 Tick 不产生新的 Sequence Gameplay 输出，但可以用 fractional PoseFrame 做 Animation-only PoseRefresh；
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
| `ActionPlayer.Update()` 只继续轮询 Legacy Timeline；正式 Sequence 由 ActorSimulationRuntime 在 Driver 固定 Tick 推进 | Sequence 已脱离 Update；ASM/Action.OnEnter 仍保持当前提交时机 |
| Sequence `Start()` 只创建 Runtime；固定 Tick 中会先建立 Frame 0 Animation-only baseline，再按累加器决定是否提交 Gameplay Frame | 速度为 0 时只允许 Pose baseline，不进入 State/Motion/HitBox |
| `ActionSequenceRuntime` 的 `BeginFrame → ExecutePreWorld → ExecutePostWorld → EndFrame` 已接到 KCC world solve 两侧 | `CurrentFrame` 只在 EndFrame 提交；最后一帧在同一 EndFrame 完成 Action |
| `ActionSequenceRuntime.RefreshPose` 已支持未跨 Gameplay Frame 时的 Animation-only fractional pose refresh | 仅刷新已 active 的 AnimationPoseClip；State/Motion/HitBox/Clip lifecycle 与 RootMotion 不跟随 fractional time |
| `ActionSequenceRuntime.Tick()` 仍可在一次调用中 while 补多帧 | 仅供 ActionSequenceRunner 等非权威预览入口；正式 Action Session 每个世界 Tick 最多一帧 |
| Unity Fixed Timestep 已为 `1 / 60`，Sequence 默认 60 Hz | 世界固定时钟与正式播放启动门禁已统一；编辑器资产 Validator 提示尚未落实 |
| 正式 Sequence 启动时拒绝非 60 Hz 数据，Session 拒绝 `speed > 1` 并中断非法 Action | 运行时门禁已落实；编辑器 Validator 提示仍待补充 |
| `CombatSimulationDriver` 唯一接管 KCC，并通过每 Actor 一个纯 C# ActorSimulationRuntime 调用 Sequence phases | Clip/Condition 不注册 Driver；ASM BeginTick、Locomotion 与其他 Actor 子系统尚未接入该入口 |
| ActorCollisionResolver 已移除独立 `FixedUpdate`，由 Driver 在 KCC 后、interpolation Post 前显式调用 | Resolver 已进入 WorldMotionCommit；最终速度 readout 后置仍属于后续 Motor 阶段 |
| Physics `m_AutoSyncTransforms = 0` | Driver 已在 Resolver 后、PostWorld 前全局调用一次 `Physics.SyncTransforms` |
| `ActionSequenceRunner` 使用独立 FixedUpdate -40 | 只能作为调试/预览入口，不能成为第二套生产权威 |
| ActorLogicInput 每 Update 直接 `SetLocomotionIntent` 到 Motor | 尚未改为只保存，LocomotionController 尚不存在 |
| ASM 的 Locomotion start context、Action `facingOnStart` 与相关 Condition 仍从 ActorMotor 读取 Intent | ActorLogicInput 停止推 Motor 时必须一起改读其最新保存值 |
| ASM 在 LateUpdate 仲裁并立即 BeginAction | 尚未改为请求排队、BeginTick 提交 |
| CancelRule 只有 Specific/Tag/Any，没有 Conditions 或 Locomotion target | Locomotion 取消合同尚未实现 |
| `AnimationConfig`、Entry、Actor 可选引用、大小写敏感 key 查询与 Editor-only Bake Context 已实现 | Stage D1/D2.1 已接入 AnimationPoseClip、RootMotionClip 和 RootRotation SelfRotationClip 运行时 key 查询；Actor Prefab 仍需逐角色配置具体 AnimationConfig 引用 |
| `RootMotionTrajectory` 已保存累计 XYZ、完整 Quaternion 与基础 metadata，并提供 Sample/Extract/SE(3) 数学 | Stage D1 已实现 XZ displacement 消费；D2.1 已实现 RootRotation Yaw 消费；Root Y、Pitch/Roll gameplay 消费仍未实现 |
| AnimationConfig 内置 Bake Context、唯一 AnimationClip resolver、Manual PlayableGraph Baker、独立 Oracle Validator、Entry 内嵌 trajectory、Inspector Bake/Rebake/Bake All 与 DependencyHash/stale 已实现 | 运行时对 missing/stale trajectory 的 Action 启动阻断要随消费 Clip 在 Stage D 接入 |
| RootMotionBuffer 已分离 Legacy Animator delta 与 Sequence trajectory owner | Animator RootMotion 兼容路径仍保留；trajectory 当前只消费 XZ 位移，不消费 Y 或旋转 |
| Sequence RootMotionClip 使用 trajectory `Extract(t0,t1)`，提交 local XZ 给 ActorMotor | SelfRotation 的 RootRotation/Target/Direction 已落地；VelocityOverrideClip 已落地；Root Y、Motion Warp、RM+LocomotionInput 同时主导仍未实现 |
| ActorMotor 在普通 Update 计算 Locomotion/Facing | 权威计算尚未进入 PreWorldMotion |
| HitBox Clip 已在 KCC、Resolver 与 SyncTransforms 后的 Sequence PostWorld 中 Query | 仍是 Query 后立即 TakeDamage；HitIntent 收集、稳定排序与两阶段 Resolve 尚未实现 |
| ActionMotionConfig 仍由 ActionInstance OnEnter/Exit 整招应用 | 尚未迁移到域规则与具体 Clip |
| Action 结束时 `ActionPlayer` 调用 `ClearTransientTags()` 全清 | 目标需要按 owner 释放 Action tags，并在域切换事务中原子写入胜选 Mode tags |

仓库目前已有 70 个 ActionAsset；其中仅 3 个显式选择 Sequence backend，67 个仍按 Legacy Timeline 路径运行。`Assets/Create/ActionAssets` 下 68 个 Action 都仍保存 Timeline 引用。因此不能先删除 Legacy 字段、PlayableDirector 或 Timeline Session。

当前已有一组 ActionSequence/ActionPlayer Editor 测试，主要覆盖编辑器、固定帧 Runtime 和 ActionPlayer 生命周期；单帧 Runtime、固定 Session、Driver 屏障、AnimationConfig 歧义处理、Trajectory 刚体数学、Baker、独立 Oracle Validator 与内嵌写盘工作流均有聚焦测试。Bake 工作流测试覆盖 Config 内持久化、无 Generated 资产、Clip/Rig/设置依赖 stale、失败不覆盖、Clear Data 和 Bake All/Pose-only 跳过。Baker 与 Oracle 已在仓库 Kiana Humanoid Avatar 上验证同一份非零 Root Motion；synthetic fixture 覆盖 Translation+Rotation、in-place、Root Y 与快速转身。当前仓库 Generic FBX 的 `motionNodeName` 均为空，因此两条独立路径按 Unity Importer 语义都得到 Identity，不从名为 `root` 的骨骼猜运动。仓库仍缺少一份明确配置非零 Root Motion Node 的 Generic fixture；在不修改现有 FBX Import Settings 的约束下，本阶段如实记录该缺口，不伪造非零 Generic 结论。Stage D1 增加了 trajectory XZ 与 Motor 合成聚焦测试；Stage D3 增加了 VelocityOwner 栈恢复、VelocityOverrideClip 曲线采样、Context/Preset 方向和 RootMotion 覆盖语义的聚焦测试；完整场景 RootMotion/Velocity/HitBox/HitStop 手动回归仍未完成。

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

现有 Sequence 尚未覆盖 Timeline 的全部能力；Velocity 已有 Sequence VelocityOverrideClip，对应迁移仍需逐资产验证。Magnetism、Effect、ContinuousAnimancer 和 Cleanup 等内容需要先有明确的 Sequence 对应物或人工迁移方案。

Legacy Timeline 当前也没有保证“编辑器标记的第 N 帧 Pose → Animator Root Motion → KCC → Trigger HitBox”严格发生在同一个固定 Tick：Director/Animancer 多为自然时间推进，Animator delta 由下一次 Motor Tick 消费，HitBox 依赖独立 Update/Physics 生命周期。因此迁移目标是保持作者可观察的动作语义，不复刻旧后端偶然形成的延迟或顺序 bug。一帧 Clip、TimeScale 大于 1、掉帧、撞墙和中断必须逐项人工回归。

---

## 13. 实施顺序

### Stage A：冻结基线

- 为现有 MotionChannels、Grounding、Impulse、VelocityOwner 和 Action 切换建立自动化回归测试。
- 把当前 Root Motion、HitStop、斜坡、跳跃和 Actor 互推手动基线重新跑一遍。
- Legacy 创建入口明确标记，不继续扩大迁移量。

### Stage B：固定模拟骨架

- 已完成：新增 Manager 上唯一 CombatSimulationDriver。
- 已完成：每个 Actor 创建并注册一个纯 C# ActorSimulationRuntime，作为唯一 Actor 固定模拟入口。
- 已完成：在 Driver Awake 中验证唯一实例并关闭 KCC AutoSimulation，接管公开手动 Simulate 顺序。
- 已完成：ActionSequence 单帧拆为 BeginFrame、PreWorld、PostWorld、EndFrame，并由正式 Session 跨 KCC 屏障执行。
- 已完成：ActorCollisionResolver、SyncTransforms 和 Sequence Pre/Post 屏障接入；HitBox 只在 PostWorld Tick。
- 已完成：把 Fixed Timestep 统一到 60 Hz。
- 已完成（运行时）：Gameplay Sequence 启动入口拒绝非 60 Hz 数据，Sequence Session 拒绝大于 1 的速度；编辑器 Validator 提示待补。
- 已完成 B2.3：Sequence 在 `0 < speed < 1` 且未跨整数 Gameplay Frame 时执行 Animation-only fractional PoseRefresh，修复非 0 HitStop/慢速下 Pose 抽帧；RootMotion、HitBox 和 Motion 输出仍只跟整数 frame。

### Stage C：AnimationConfig 与 Baker

- 已完成 C1：实现 AnimationConfig、Entry、Actor 可选引用和稳定 key 查询；重复 key 拒绝解析。
- 已完成 C1：实现 RootMotionTrajectory、累计 XYZ/完整 Quaternion、基础 metadata、Sample/Extract/Compose/Inverse 与只读数据校验。
- 已完成 C2.1：实现 Reference Rig、采样率与 Validator 容差的 Editor-only Bake Context；单 Animator、有效 Avatar、单位 Scale 和干净 Rig 是硬门禁。
- 已完成 C2.1：实现唯一 AnimationClip resolver，以及以 `Evaluate(0)` 为基线、连续 Manual Graph Evaluate、精确采到 duration 的内存 Baker Core；Humanoid/Generic 使用同一实现。
- 已完成 C2.2：实现独立 Reference Rig + Manual PlayableGraph 的 Oracle Validator，通过 `Animator.deltaPosition/deltaRotation` 与 Baker 逐点对照并输出最大误差位置。
- C2.2 已验证 synthetic Translation+Rotation、in-place、Root Y、快速转身、真实 Humanoid 非零运动与当前 Generic Identity importer 语义；仓库缺少非零 Generic Root Motion Node fixture，留作新增测试资产后补验，不修改现有 FBX Import Settings。
- 已完成 C2.3：实现 Inspector Bake/Rebake/Bake All、DependencyHash 与 stale/invalid 状态。
- 已完成 C2.4：移除独立 BakeProfile 和 Generated trajectory 资产；Bake Context 归入 AnimationConfig，trajectory 作为普通序列化数据内嵌 Entry，并用显式存在标记稳定表达 Missing。
- C2.4 写盘采用先 Bake+Oracle Validate、后一次替换 Entry 数据的事务边界；失败不覆盖旧 trajectory，Clear Data 不产生磁盘孤儿。Action 启动时阻断 missing/stale 的运行时门禁随实际消费 Clip 在 Stage D 接入。

### Stage D：Sequence Pose / Motion

- 已完成 D1：AnimationPoseClip 改为通过 Actor.AnimationConfig key 查询 Transition，直接 TransitionAsset 字段退出正式 Clip。
- 已完成 D1：Sequence session 在第一次 Pose Evaluate 前压制 Animator RootMotion relay，并支持 Frame 0 Animation-only baseline。
- 已完成 D1：实现 RootMotionClip，通过 AnimationConfig key 查询内嵌 RootMotionTrajectory，按 `[startFrame,endFrame)` 映射 Extract 并只提交 local XZ 位移。
- 已完成 D1：局部升级 RootMotionBuffer、ActorMotionRuntime、ActorMotor，新增 trajectory owner/source gating；trajectory 位移不再乘 MovementTimeScale，并与水平 impulse/vertical channels 按 v1 合同合成。
- 已完成 D2.1：实现 RootRotation SelfRotationClip，通过 AnimationConfig key 查询 trajectory，按整数 Gameplay Frame 提取 local Up Yaw；ActorMotor 新增独立 SelfRotation owner/channel，SelfRotation 活跃时以 `tickStartRotation * localYawDelta` 接管 KCC rotation，并在退出/取消时同步 Facing baseline。
- 已完成 D2.2：SelfRotationClip 支持 `RootRotation / Target / Direction` 来源与 `Snap / RotateBySpeed` 旋转方式；Target/Direction 不依赖 AnimationConfig，Context 需求在 Action 启动前校验。
- 已完成 D3：实现 Sequence VelocityOverrideClip，复用 VelocityConfig，支持水平/垂直轴覆盖、PresetLocal/ContextDirection、整数 Gameplay Frame 曲线采样，以及 MotionChannels 每轴可恢复覆盖栈；Velocity/Velocity 和 RootMotion/HorizontalVelocity 重叠合法。
- 验证同帧 Pose、位移、旋转、KCC 和 HitBox。
- 固化 `[startFrame,endFrame)`、Frame 0 activation/freeze、最后一帧 PostWorld 后清理的测试。

### Stage E：Locomotion 与 ASM

- 已完成 E0：`ActionEventContext` 重命名并收敛为不可变 `ActionContext`；ASM 使用 `ActionCandidate` 携带 Context、来源、提交顺序和精确 External Request。
- 已完成 E0：Poll/Event/External/direct BeginAction 都显式携带启动 Context；Event 不再使用全局 pending context，External 回调绑定到胜选 request。
- 已完成 E0：Entry/Exit Condition 可读取 Context，`OnClaim` 保持 Actor-only；Sequence Clip 可声明 required context fields，缺字段会在 Claim 前阻断。
- 已完成 E0（兼容期）：ActorLogicInput 保存 `LatestLocomotionIntent`，ASM start context 和 Legacy facing fallback 改读它；正式 LocomotionController 前仍继续向 ActorMotor 推送 intent。
- ActorLogicInput 改为只保存 Intent，移除直接推 Motor 的兼容路径。
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
- RootRotation SelfRotation 从 Quaternion 稳定提取 Yaw；Target/Direction 投影到 CharacterUp 平面求 signed yaw；同 Tick XZ 使用 tick-start rotation，不被本 Tick Yaw 提前旋转。
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
- 0.5 倍速未进帧的 Tick 只允许 Animation-only fractional PoseRefresh，不重复 HitBox/Motion/State Gameplay OnTick；HitStop 不跳事件、不重复 Impulse、不补偿冻结时间。
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

---

## 17. Stage D4 落地记录：60 Hz 与 CombatHitIntent

Stage D4 将 Gameplay ActionSequence 的时间基准固定为项目常量 `CombatSimulationTiming.FrameRate = 60`、`FixedDeltaTime = 1/60`。`ActionSequenceData.frameRate` 暂时继续保留为隐藏序列化字段，用于识别和修复旧资产或损坏数据；作者 UI 不再提供可编辑 FPS，Validator 对 `<= 0` 和正数非 60 分别报错，并统一修复为 60。

HitBox 不再在 PostWorld 查询阶段直接调用 `TakeDamage`。生产 HitBox Clip 在 `CombatSimulationDriver` 提供的显式 `ICombatHitIntentSink` 中写入强类型 `CombatHitIntent`；Driver 在所有 PostWorld 查询完成、所有 EndFrame 之前统一 Resolve。独立 Runner 或无权威 sink 的预览路径不会回退成立即伤害，只输出诊断并跳过权威命中。

第一版 Resolve 顺序固定为 `Tick -> AttackerStableId -> ClipStableId(StringComparer.Ordinal) -> TargetStableId`。已收集的 intent 不因攻击者在同 tick 后续死亡而失效，因此允许相杀；目标第一次死亡之后，后续指向该目标的 intent 跳过，不重复扣血、受击、Impact 或死亡。

HitBox Runtime 现在区分 `_pendingTargets` 与 `_hitTargets`：同一 `IDamageable` 的多 collider 每帧只 enqueue 一次，代表 collider 选择离查询中心最近者，平局按 collider instance id。Resolver 回执 `ImpactAllowed` 时才提交到 `_hitTargets`；无敌、拒绝 impact、死亡跳过或 abort 只清 pending，允许后续 gameplay frame 重试。

### 17.1 Stage D4.1 验收补充

Stage D4.1 补齐了 `CombatHitIntent` 与固定帧路径的自动化验收。纯 Resolver 测试覆盖 Resolve 前 Abort、部分 Resolve 后异常、Resolve 期间重入 Enqueue、多个致死 Hit 的唯一 `TargetKilled` 归属，以及稳定排序不依赖 enqueue 顺序。

HitBox 集成测试覆盖同一 `IDamageable` 多 Collider 去重、代表 Collider 的最近/InstanceId 平局规则、无敌或拒绝 Impact 后下一 Gameplay Frame 可重试、生产 HitBox Runtime 在 Resolve 前退出时已冻结 Intent 仍结算、两个不同 HitBox Clip 可分别命中同一目标，以及最后一帧 `Query -> Resolve -> Exit/Complete` 顺序。

真实固定帧验收不再集中到单个大型 synthetic PlayMode fixture。现有 `CombatSimulationDriverPlayModeTests` 已覆盖真实 `FixedUpdate`、Driver 接管 KCC、插值配对、Sequence PreWorld/PostWorld 屏障与 fault 停止；D4.1 新增测试聚焦在 Resolver 与 HitBox/Intent 生命周期。RootMotion、SelfRotation、VelocityOverride、0.5 倍速、HitStop、撞墙、最后一帧和相杀的完整内容路径继续由 Jaeger 手动回归确认。

这些测试仍只声明单次运行内稳定性，不扩展到跨机器、rollback 或 bitwise replay；新增自动测试使用内存 Sequence/HitBox 与测试局部 Damageable，不依赖 Jaeger 或项目内容资产。
