# CombatSample Root Motion Final Design v1

> 本文档是 CombatSample 的 Root Motion 最终架构方案。
> 方案综合了公司成熟项目的架构审查、MxM 源码调研，以及 CombatSample 当前 ActionSequence / ActorMotor / KCC 架构。
>
> **作者资产结构更新（2026-08-10）：** 本文关于独立 `RootMotionBakeProfile` 与独立 trajectory `.asset` 的描述已被 `Proposals/CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md` Stage C2.4 取代。当前实现由 AnimationConfig 直接保存 Editor Bake Context，并把生成的 RootMotionTrajectory 内嵌到对应 Entry；本文的连续 Graph 求值、Trajectory 数学和 Runtime 权威划分仍作为设计依据。

---

## 1. 最终架构

### Editor

```text
AnimationClip
    +
RootMotionBakeProfile
    └─ Reference Character Prefab
       └─ Animator / Avatar
               ↓
      Manual PlayableGraph
      Continuous Evaluate(dt)
               ↓
   Unity 计算最终 Root Motion
               ↓
   Position + Quaternion samples
               ↓
      RootMotionTrajectory
               ↓
          Validator
```

### Runtime

```text
                     ActionSequence
                     时间唯一权威
                          │
              ┌───────────┴───────────┐
              │                       │
              ▼                       ▼
          Animancer             AnimationTimeMapping
          Pose only                    │
                                       ▼
                              RootMotionTrajectory
                                       │
                               Extract(t0, t1)
                                       │
                               RootMotionPolicy
                                       │
                            Scale / Warp（以后）
                                       │
                                       ▼
                                  ActorMotor
                                       │
                                       ▼
                                      KCC
```

四条权威规则永久固定：

```text
ActionSequence   = 时间权威
Animancer        = Pose 权威
Trajectory       = authored motion 数据权威
ActorMotor / KCC = 世界位置权威
```

除了 ActorMotor/KCC，其他系统不得直接修改 Gameplay Actor 的 Transform。

---

## 2. Baker：不自己解决 Humanoid 黑魔法

Baker 输入：

```csharp
AnimationClip
RootMotionBakeProfile
```

其中 Profile 至少包含：

```csharp
GameObject ReferenceRigPrefab;
float SampleRate = 60f;
```

Reference Prefab 必须是干净的动画角色：

```text
Model
Skeleton
Animator
Avatar
```

不要带：

```text
ActorMotor
KCC
AI
Gameplay logic
ActorRootMotionRelay
```

Baker 流程：

```text
实例化 Reference Rig
↓
归零 Transform
↓
Animator
↓
Manual PlayableGraph
↓
AnimationClipPlayable
↓
Evaluate(0) 初始化
↓
连续 Evaluate(dt)
↓
记录 root Transform
```

原则：

- Humanoid / Avatar / Retarget / Root Transform Import Settings 全部交给 Unity 自己解释。
- Baker 不解析 FBX。
- Baker 不从 Hips/Pelvis 猜 Root Motion。
- Baker 不自己实现 Mecanim Root Motion 规则。
- 我们只记录 Unity 最终求值后的角色 Root Transform。

MxM 已经验证了这一类提取路线在成熟动画预处理系统中可行。

---

## 3. Humanoid / Generic / Avatar 最终策略

### Humanoid

```text
Clip
+
Reference Humanoid Prefab
+
真实 Avatar
→ 同一个 Baker
```

### Generic

第一版仍然：

```text
Clip
+
Reference Generic Prefab
→ 同一个 Baker
```

不要现在创建：

```text
HumanoidRootMotionBaker
GenericRootMotionBaker
```

除非之后实测证明必须分开。

### 不同体型

不做自动 `humanScale` 推测。

默认：

```text
一套动画资产族
→ 一个 Reference Rig / Avatar Family
→ 一套 trajectory
```

如果以后不同体型角色需要共用，先用 Validator 验证。

如果差异明显：

```text
新建 BakeProfile
→ 重新 Bake
```

不要通过一个缩放系数猜测最终 Root Motion。

---

## 4. RootMotionTrajectory 数据格式

唯一真实数据：

```csharp
[Serializable]
public struct RootMotionSample
{
    public float Time;
    public Vector3 Position;
    public Quaternion Rotation;
}
```

含义：

> 从动画起点到时间 `t` 为止的累计刚体变换。

定义：

```text
M(t) = cumulative root transform
M(0) = Identity
```

建议资产：

```csharp
public sealed class RootMotionTrajectory : ScriptableObject
{
    AnimationClip sourceClip;
    float duration;
    float sampleRate;
    RootMotionSample[] samples;

    RootMotionPose Sample(float time);
    RootMotionDelta Extract(float fromTime, float toTime);
}
```

第一版数据策略：

```text
Vector3 = XYZ 全存
Quaternion = 完整旋转全存
float32
不压缩
```

---

## 5. Extract 数学永久固定

这是整个系统最重要的数据合同。

```text
Delta(t0,t1)
=
Inverse(M(t0)) * M(t1)
```

对应：

```csharp
deltaRotation =
    Quaternion.Inverse(r0) * r1;

deltaPosition =
    Quaternion.Inverse(r0) * (p1 - p0);
```

不能简单：

```csharp
p1 - p0
```

因为动画可能边移动边旋转。

`RootMotionDelta`：

```csharp
public readonly struct RootMotionDelta
{
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
}
```

多个 delta 的组合也是刚体变换组合：

```csharp
AB.Position =
    A.Position + A.Rotation * B.Position;

AB.Rotation =
    A.Rotation * B.Rotation;
```

不能只把 Vector3 相加。

---

## 6. Sample

任意时间都可以查询：

```csharp
trajectory.Sample(0.43827f);
```

插值：

```text
Position → Vector3.Lerp
Rotation → Quaternion.Slerp
```

默认 Bake：

```text
60 Hz
```

对于高速 Dash / 大角度快速转身，如果 Validator 显示误差不够，再单独升：

```text
120 Hz
```

不需要整个项目无脑 120Hz。

---

## 7. AnimationTimeMapping

建立一个明确的小抽象：

```text
AnimationTimeMapping
```

职责：

```text
Sequence Frame
Playback Speed
Clip Start Offset
Trim
Animation Clip Duration
未来 Loop
        ↓
clip-local animation time
```

不要把这些规则散在各个 Clip Runtime 里。

使用方式：

```csharp
float previousTime = mapping.Map(previousFrame);
float currentTime  = mapping.Map(currentFrame);
```

Pose：

```csharp
state.Time = currentTime;
animancer.Evaluate();
```

Motion：

```csharp
RootMotionDelta delta =
    trajectory.Extract(previousTime, currentTime);
```

---

## 8. Advance 和 Seek 必须是两种不同操作

### Advance

时间真的经过：

```text
0.10 → 0.12
```

执行：

```text
更新 Pose
+
Extract Root Motion
+
提交 Motor
```

### Seek

只是跳时间：

```text
1.0 → 5.0
```

执行：

```text
只更新 Pose
不移动 Actor
```

语义表：

| 操作 | 语义 |
|---|---|
| 正常播放 | Advance |
| 一帧补多个 Sequence Frame | 多次 Advance |
| Pause | 无 motion |
| Resume | 从原时间继续 Advance |
| Editor Scrub | Seek |
| SetTime | Seek |
| Network correction | 世界状态 Override + reset baseline |
| Rollback replay | 恢复状态后重新 Advance |

---

## 9. Runtime 第一版只消费 XZ + Yaw

Trajectory 永远保存：

```text
XYZ
完整 Quaternion
```

但普通地面战斗：

```text
X/Z       → 使用
Y         → Motor / Gravity
Yaw       → 使用
Pitch/Roll→ Pose only
```

以后：

```text
Vault
Climb
Jump Attack
Execution
```

可以通过 RootMotionPolicy 开启 Y 或更完整 Rotation。

不要因为第一版不用 Y 就把 Y 从资产里删掉。

---

## 10. RootMotionPolicy

保持简单：

```csharp
public struct RootMotionPolicy
{
    public bool ApplyHorizontal;
    public RootMotionVerticalMode Vertical;
    public RootMotionRotationMode Rotation;
}
```

普通攻击：

```text
Horizontal = Animation
Vertical   = Ignore
Rotation   = Yaw
```

Traversal：

```text
Horizontal = Animation
Vertical   = Animation
Rotation   = Full / Custom
```

不要做通用 Modifier Graph。

---

## 11. RootMotionBuffer 必须升级

现有 Buffer 使用简单 position 累加，不适用于局部刚体运动组合。

新的 Buffer 应持有：

```csharp
RootMotionDelta _pending;
RootMotionDelta _tick;
```

统一通过：

```csharp
RootMotionMath.Compose(a, b)
```

组合。

保留原有 snapshot 生命周期：

```text
pending
↓
BeginMotorTick snapshot
↓
tick consumption
```

这个设计本身是好的。

---

## 12. ActorMotor 的 Root Rotation 必须修改

最终规则：

### 没有 RootMotionOwner

```text
FacingRuntime 控制朝向
```

### ActionSequence 拥有 Root Motion

```text
KCC 当前 Rotation
*
本 Tick authored deltaRotation
```

不要每帧重新从 FacingRuntime 的旧绝对朝向作为 baseline。

Owner 退出时：

```text
FacingRuntime.SyncTo(currentActorRotation)
```

避免：

```text
攻击动画最终转了 60°
↓
动作结束
↓
FacingRuntime 仍记着旧方向
↓
角色突然弹回去
```

---

## 13. RootMotionOwner

永久规则：

> 每一个 simulation tick 最多一个 Exclusive Authored Motion Owner。

例如：

```text
Locomotion
ActionSequence
Traversal
Scripted Movement
```

只能有一个作为主导 authored movement。

但是：

```text
Gravity
Impulse
Moving Platform
```

属于 Additive / Motor Policy，可以同时存在。

因此最终是：

```text
一个 Exclusive Base Motion
+
若干 Additive Effects
```

CombatSample 已经存在 MotionOwner / MotionChannels 思想，不需要另造大型 ownership framework。

---

## 14. Animator Root Motion 和 Trajectory 必须互斥

现有 Animator 路径：

```text
ActorRootMotionRelay
↓
Animator.deltaPosition
Animator.deltaRotation
↓
ActorMotor
```

可以保留，服务 legacy/runtime Animator RM。

但是：

```text
ActionSequence Trajectory active
```

时必须明确禁止 Animator RM 被同时应用。

否则：

```text
Trajectory 一次
+
Animator 一次
=
双重 Root Motion
```

建议 ActorMotor 明确区分 source，例如：

```text
External
AnimatorManaged
TrajectoryManaged
```

具体命名可按工程现有风格调整。

---

## 15. TimeScale 双重缩放必须修掉

Sequence 播放速度已经改变动画时间推进。

因此 baked trajectory：

```text
Extract(previousTime, currentTime)
```

已经自然包含了：

```text
0.5x
2x
HitStop
```

带来的时间变化。

最终规定：

> Trajectory Extract 出来的 displacement 不再乘 MovementTimeScale。

MovementTimeScale 仍可作用于：

```text
Gravity evolution
Impulse decay
Locomotion
```

但不能再缩放一次 authored displacement。

必须专项测试：

```text
1x
0.5x
0.1x HitStop
```

确保总运动距离没有发生二次缩放。

---

## 16. Pose Blend 与 Root Motion Blend 分开

最终规则：

```text
Pose
可以 Blend

Root Motion
不根据 Animancer weight 自动 Blend
```

有 Root Motion 的 Action clip 必须明确：

```text
RootMotionTrajectory owner
```

第一版不实现：

```text
A trajectory × 0.4
+
B trajectory × 0.6
```

Crossfade 只是视觉过渡。

Gameplay motion ownership 在明确 Tick 完成交接。

Additive / UpperBody：

```text
永远 Pose Only
```

---

## 17. Action Cancel

例如：

```text
动画 0 → 1.0s
0.43s 被 Cancel
```

只应用到：

```text
previousTime → 0.43
```

剩余：

```text
0.43 → 1.0
```

直接丢弃。

不“偿还动画剩余距离”。

下一 Action 从 KCC 当前实际状态开始。

---

## 18. Collision：Requested != Actual

Trajectory 输出：

```text
Requested Motion
```

KCC 输出：

```text
Actual Motion
```

例如：

```text
动画要求：1.0m
KCC 实际：0.6m
Blocked：0.4m
```

默认：

```text
0.4m 丢弃
```

绝不自动下一帧补偿。

未来可增加：

```text
RequestedDelta
AcceptedDelta
BlockedDelta
```

供：

```text
Debug
Warp failure
Action logic
```

使用。

---

## 19. Loop 现在固定合同，实现稍后

一圈累计 Transform：

```text
L = M(duration)
```

展开时间：

```text
t = kT + u
```

定义：

```text
MUnwrapped(t) = L^k * M(u)
```

所以：

```text
Extract(t0,t1)
=
Inverse(MUnwrapped(t0))
*
MUnwrapped(t1)
```

不能：

```text
loopPosition * cycleCount
```

因为 loop 本身可能带 rotation。

接口现在按这个模型设计。

第一阶段可以暂时 reject loop gameplay，但不能设计一个以后无法扩展的 Sample/Extract。

---

## 20. Reverse

数学层支持：

```text
Extract(t1,t0)
=
Inverse(Extract(t0,t1))
```

但：

```text
Gameplay Reverse
```

当前不开放。

Editor preview / debug 可以用。

真正 Gameplay reverse 还涉及：

```text
Events
Hitboxes
Cancel windows
Gameplay state
```

不是 Root Motion 自己的问题。

---

## 21. Scale / Warp / Snap 后续固定顺序

未来 pipeline：

```text
Extract Raw Local Motion
        ↓
Axis / Vertical Policy
        ↓
Authoring Scale
        ↓
Motion Warp
        ↓
Local → World
        ↓
ActorMotor
        ↓
KCC
```

顺序永久固定。

### Scale

定义这招本身应该走多远。

### Snap

Gameplay 决定目标落点。

### Warp

把原始 motion 在窗口内调整到目标。

### Motor

决定真实世界允许走到哪里。

Motion Warping 不认识：

```text
Enemy
CombatTarget
Attack
```

只认识：

```text
Current Transform
Target Transform
Warp Window
Raw Remaining Motion
```

---

## 22. Bake Stale 管理

Trajectory metadata：

```text
Source Clip
Bake Profile
Sample Rate
Baker Version
Dependency Hash
Duration
```

Dependency 至少涵盖：

```text
AnimationClip
Reference Prefab
Avatar
Importer relevant dependency
Sample Rate
Baker Format Version
```

第一版：

```text
Bake / Rebake button
+
Inspector stale warning
```

随后增加：

```text
Build Validation
```

发现 stale trajectory：

```text
Build fail / explicit error
```

不需要现在实现全自动 AssetPostprocessor。

---

## 23. Validator 是整个系统的技术闸门

必须有独立 Oracle。

Baker：

```text
Manual Graph
→ cumulative Reference Transform
```

Validator：

```text
重新创建独立实例
→ 连续 Unity animation evaluation
→ Animator deltaPosition / deltaRotation 或等价独立路径
→ accumulate
```

不能拿 Baker 自己生成的 sample 再验证自己。

测试至少包含：

```text
纯 Translation
纯 Rotation
Translation + Rotation
In-place
Root Y
快速转身
短 Clip
Humanoid
Generic（有资产就测）
不同 Bake Into Pose 设置
准确 t=0
准确 duration
0.5x / 2x mapping
```

如果在同 Unity / Avatar / Import Settings 下出现约：

```text
1cm
2°
```

这种误差，应优先认为存在实现问题，而不是普通浮点误差。

---

## 24. KCC 时序必须明确验证

必须保证：

```text
Sequence Advance N
↓
Submit delta N
↓
Motor Begin Tick
↓
KCC consume delta N
```

不能：

```text
KCC 先 snapshot
↓
Sequence 再提交
↓
下一帧才消费
```

否则会稳定延迟一 Tick。

不要靠默认 Script Execution Order 猜。

需要 PlayMode Test：

```text
Sequence frame N motion
=
same simulation tick KCC request
```

如果需要，再调整明确 orchestration。

---

## 25. 最终文件建议

```text
Assets/Scripts/RootMotion/

RootMotionTypes.cs
    RootMotionPose
    RootMotionDelta
    RootMotionMath

RootMotionTrajectory.cs

RootMotionPolicy.cs

Editor/
    RootMotionBakeProfile.cs
    RootMotionBaker.cs
    RootMotionValidator.cs
    RootMotionBakerWindow.cs
```

已有代码修改范围：

```text
ActionSequenceAnimancerClipDefinition.cs
RootMotionBuffer.cs
ActorMotionRuntime.cs
ActorMotor.cs
FacingRuntime.cs
ActorRootMotionRelay.cs（仅 source gating，如需要）
```

可能新增：

```text
AnimationTimeMapping.cs
```

不要修改无关 gameplay 系统。

---

## 26. 实施优先级

| 功能 | 决策 |
|---|---|
| Humanoid Reference Rig Baker | 现在 |
| Generic 同一 Baker | 现在至少保持兼容 |
| XYZ + Quaternion | 现在 |
| Sample / Extract | 现在 |
| 正确 SE(3) composition | 现在 |
| Validator | 现在 |
| Stale metadata | 现在基础版 |
| AnimationTimeMapping | 现在 |
| Sequence integration | 现在 |
| XZ + Yaw | 现在 |
| RootMotionOwner | 现在 |
| Facing handoff | 现在 |
| TimeScale 修正 | 现在 |
| Requested / Actual 可观测 | 现在 |
| Loop | 合同现在，功能稍后 |
| Root Y gameplay | 稍后 |
| Scale | 稍后 |
| Motion Warping | 稍后 |
| Attack Snap | 稍后 |
| Gameplay Reverse | 不开放 |
| RootMotion weighted blending | 不做 |
| Direct Curve Baker backend | 暂不做 |
| 压缩 | 不做 |
| 通用 Modifier DAG | 不做 |

---

## 27. Definition of Done

第一阶段真正完成必须满足：

- Baker 在真实 Reference Avatar 上成功生成 trajectory。
- `M(0) == Identity`。
- `Sample(float time)` 正确。
- `Extract(t0,t1)` 使用正确刚体变换数学。
- 连续多个 Delta 组合正确。
- Unity 原始 Root Motion 与 baked trajectory Validator 通过。
- Translation + Rotation 同时存在的攻击验证通过。
- ActionSequence 正常播放产生正确运动。
- 一次 Update 补多个 Sequence Frame 仍正确。
- Seek 不移动 Actor。
- Cancel 不补剩余 motion。
- 0.5x / HitStop 不发生 Root Motion 二次缩放。
- Root Rotation 能连续累计，并且 Action 结束后 Facing 不弹回。
- Animator RM 与 Trajectory RM 不会同时应用。
- KCC 撞墙后不偿还 blocked displacement。
- ActorMotor/KCC 仍然是唯一世界位置写入者。

---

## 最终一句话

> 动画告诉我们“这段动作原本想怎么移动”；ActionSequence 决定现在播放到哪里；Trajectory 根据这两个时间算出运动请求；ActorMotor/KCC 决定角色在真实游戏世界里到底能移动到哪里。

上游 Humanoid、Avatar、Retarget、Importer 的复杂计算交给 Unity。

下游的数据结构、时间语义、ownership、碰撞和后续 Motion Warping 规则由 CombatSample 自己控制。

这就是 CombatSample 后续 Root Motion 系统的最终基线。
