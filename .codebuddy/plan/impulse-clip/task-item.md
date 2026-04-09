# ImpulseClip 实施计划

## 背景

为类鬼泣 ACT 游戏实现受击冲量系统。ImpulseClip 是 Timeline Clip，放在受击 Action 的 Timeline 中，驱动角色的击飞/浮空/弹跳位移。

### 前置依赖（均已完成）

- ✅ `ActionEventContext` — 攻击方向 `Direction`、力度 `Magnitude`、攻击者 `Instigator`
- ✅ `ActionPlayableBase` — Clip 可通过 `actionInstance.EventContext` 读取上下文
- ✅ `ActorMovement` — 已有 `SetImpulseVelocity()`、`SetGravityScale()`、`LockFacing()`/`UnlockFacing()` 接口

### 设计决策（已确认）

| 决策点 | 结论 |
|--------|------|
| 击退/踉跄 | 用 RootMotion + SnapFacing，不用 ImpulseClip |
| 垂直冲量注入 | 走重力通道（`SetVerticalVelocity`），一次性注入后由重力自然衰减 |
| 水平衰减 | AnimationCurve 曲线控制 |
| 垂直衰减 | 物理驱动（初速度 + 重力），不用曲线 |
| 力度来源 | 写死在 Clip 配置上，不需要 `useContextMagnitude` |
| 方向来源 | 统一用 `EventContext.Direction`，不需要方向模式枚举 |
| 分阶段效果 | 串联多段 ImpulseClip 实现（浮空=上升+滞空+下落，弹跳=弹1+弹2+弹3） |
| 朝向锁定 | `lockFacing` 默认 true，ImpulseClip 不管朝向旋转 |
| gravityScale | Clip 级别固定值 |

### 四种效果的实现方式

| 效果 | 实现方式 |
|------|----------|
| **击退/踉跄** | RootMotion + MagnetismClip(SnapFacing)，**不需要 ImpulseClip** |
| **击飞** | 1 段 ImpulseClip（水平大 + 垂直小 + 正常重力） |
| **浮空** | 2~3 段 ImpulseClip 串联（上升 + 滞空 + 下落） |
| **下砸弹跳** | 2~3 段 ImpulseClip 串联（弹起1 + 弹起2 + 弹起3，力度递减） |

---

## 涉及文件

| 文件 | 路径 | 改动类型 |
|------|------|----------|
| `ImpulseConfig.cs` | `Assets/Scripts/Impulse/ImpulseConfig.cs` | **新建** |
| `ActionImpulseClip.cs` | `Assets/Scripts/TimelinePlayable/Impulse/ActionImpulseClip.cs` | **新建** |
| `ActionImpulseTrack.cs` | `Assets/Scripts/TimelinePlayable/Impulse/ActionImpulseTrack.cs` | **新建** |
| `ActionImpulseBehavior.cs` | `Assets/Scripts/TimelinePlayable/Impulse/ActionImpulseBehavior.cs` | **新建** |
| `ActorMovement.cs` | `Assets/Scripts/Actor/ActorMovement.cs` | **小改** — 新增 `SetVerticalVelocity()` 方法 |

---

## 任务清单

- [x] 1. ActorMovement — 新增 `SetVerticalVelocity()` 方法
   - 新增公开方法 `SetVerticalVelocity(float upSpeed)`
   - 实现：`_gravityVelocity = new Vector3(0f, upSpeed, 0f);`
   - 语义：直接覆盖重力通道的垂直速度，注入后由重力系统自然累积衰减
   - 用于击飞/浮空等需要垂直初速度的场景
   - 命名用 `Set` 而非 `Add`，因为是覆盖而非叠加（和 `SetGravityScale` 风格一致）

- [x] 2. 新建 `ImpulseConfig.cs` — 冲量配置数据类
   - 路径：`Assets/Scripts/Impulse/ImpulseConfig.cs`
   - 字段：
     - `[Header("水平冲量")]`
       - `float horizontalForce = 5f` — 水平初速度（米/秒），沿 EventContext.Direction 的水平投影方向
       - `AnimationCurve horizontalDecay` — 归一化时间 → 力度百分比（默认 1→0 线性衰减）
     - `[Header("垂直冲量")]`
       - `float verticalForce = 0f` — 垂直初速度（米/秒），正值=向上。仅在 Clip 开始时注入一次
     - `[Header("重力")]`
       - `float gravityScale = 1f` — 此 Clip 期间的重力缩放
     - `[Header("朝向")]`
       - `bool lockFacing = true` — 冲量期间是否锁定朝向
     - `[Header("调试")]`
       - `bool debugLog = false`

- [x] 3. 新建 `ActionImpulseClip.cs` — Timeline Clip 资产
   - 路径：`Assets/Scripts/TimelinePlayable/Impulse/ActionImpulseClip.cs`
   - 继承 `PlayableAsset`
   - 持有 `ImpulseConfig config` 字段
   - `CreatePlayable()` 中创建 `ScriptPlayable<ActionImpulseBehavior>`，将 config 传给 behavior

- [x] 4. 新建 `ActionImpulseTrack.cs` — Timeline Track
   - 路径：`Assets/Scripts/TimelinePlayable/Impulse/ActionImpulseTrack.cs`
   - 继承 `ActionTrackBase`
   - `[TrackColor(0.8f, 0.3f, 0.2f)]` — 红橙色，区别于 Magnetism 的绿色
   - `[TrackClipType(typeof(ActionImpulseClip))]`

- [x] 5. 新建 `ActionImpulseBehavior.cs` — 运行时逻辑
   - 路径：`Assets/Scripts/TimelinePlayable/Impulse/ActionImpulseBehavior.cs`
   - 继承 `ActionBehaviourBase`
   - 持有 `ImpulseConfig config` 字段（由 Clip 传入）
   - 私有字段：
     - `Vector3 _horizontalDirection` — 缓存的水平冲量方向
     - `double _clipDuration` — 缓存的 Clip 总时长
     - `bool _verticalInjected` — 垂直初速度是否已注入
     - `float _originalGravityScale` — 缓存原始重力缩放（用于恢复）
   - `OnClipStart(Playable playable)`：
     1. 从 `actionInstance.EventContext.Direction` 读取方向
     2. 水平化：`dir.y = 0; dir.Normalize()`
     3. 如果方向无效（sqrMagnitude < 0.001f），fallback 到 `-actor.transform.forward`（被打向后退）
     4. 缓存 `_horizontalDirection`
     5. 缓存 `_clipDuration = playable.GetDuration()`
     6. 如果 `config.lockFacing` → `actor.movement.LockFacing()`
     7. 注入垂直初速度：`actor.movement.SetVerticalVelocity(config.verticalForce)`
     8. 设置重力缩放：`actor.movement.SetGravityScale(config.gravityScale)`
     9. `_verticalInjected = true`
   - `OnClipUpdate(Playable playable, FrameData info)`：
     1. 计算归一化时间：`t = playable.GetTime() / _clipDuration`，clamp 到 [0,1]
     2. 从衰减曲线读取系数：`decay = config.horizontalDecay.Evaluate((float)t)`
     3. 计算水平速度：`horizontalVel = _horizontalDirection * config.horizontalForce * decay`
     4. 写入冲量通道：`actor.movement.SetImpulseVelocity(horizontalVel)`
     5. 注意：垂直分量不在这里处理，已在 OnClipStart 注入重力通道
   - `OnClipStop(bool isFinish)`：
     1. 如果 `config.lockFacing` → `actor.movement.UnlockFacing()`
     2. 恢复重力缩放：`actor.movement.SetGravityScale(1f)`
     3. 清理状态

- [x] 6. 编译验证
   - 确保项目无编译错误
   - 确保新文件与现有 Magnetism 系统的模式一致

---

## 架构对称性参考

ImpulseClip 的三层结构完全对称于 MagnetismClip：

```
Magnetism（朝向控制）          Impulse（位移控制）
─────────────────────         ─────────────────────
MagnetismConfig               ImpulseConfig
ActionMagnetismClip            ActionImpulseClip
ActionMagnetismTrack           ActionImpulseTrack
ActionMagnetismBehavior        ActionImpulseBehavior
ActionMagnetismSession         （不需要 Session，逻辑直接在 Behavior 中）
```

Impulse 不需要 Session 层的原因：Magnetism 的 Session 需要持续追踪目标位置（每帧重新计算方向），而 Impulse 的方向在 `OnClipStart` 时就确定了（来自 EventContext），之后只是衰减。逻辑足够简单，直接放在 Behavior 中即可。

---

## 运行时数据流

```
攻击方 HitData
    │
    ▼
ActorCombater.TakeDamage()
    │ 构造 ActionEventContext { Direction, Magnitude, Instigator, ... }
    ▼
ASM.SendEvent(hitTag, context)
    │
    ▼
受击 Action 开始播放 Timeline
    │
    ├── MagnetismClip → SnapFacing 到攻击者方向
    │
    └── ImpulseClip
        │
        ├── OnClipStart:
        │   ├── 读取 context.Direction → 水平方向
        │   ├── SetVerticalVelocity(verticalForce) → 注入重力通道
        │   ├── SetGravityScale(gravityScale) → 控制浮空/加速
        │   └── LockFacing() → 锁定朝向
        │
        ├── OnClipUpdate (每帧):
        │   └── SetImpulseVelocity(水平方向 * 力度 * 衰减曲线)
        │
        └── OnClipStop:
            ├── SetGravityScale(1f) → 恢复默认
            └── UnlockFacing() → 解锁朝向
```
