# ActorMotor Runtime 架构落地计划

## 设计原则

- **KCC tick 状态归 ActorMotor 管**：凡是依赖 `PostGroundingUpdate / UpdateVelocity / AfterCharacterUpdate / OnMovementHit` 顺序的状态，都不再由 `ActorMovement` 拥有。
- **纯 C# runtime 按状态域拆分**：`ActorMotor` 不膨胀；它只调度，具体状态由小对象管理。
- **ActorMovement 第一阶段保留为兼容 facade**：外部继续用 `actor.movement.*`，但内部转发到 `ActorMotor` 的 runtime。
- **行为不变优先**：第一阶段不追求删光补丁，而是把补丁收进正确的状态对象里；后续再判断哪些可以自然删除。
- **不迁 Unity 序列化字段**：配置字段暂留 `ActorMovement`，避免 prefab/scene 数据风险。

## 目标代码结构

新增或调整为：

```text
ActorMotor : MonoBehaviour, ICharacterController
    owns ActorMotionRuntime
    reads high-level intent from ActorMovement
    drives runtime during KCC callbacks

ActorMotionRuntime : plain C# runtime root
    owns MotionChannels
    owns GroundingRuntime
    owns RootMotionBuffer
    owns VelocityReadout
    owns force/ceiling/movement time/gravity state

GroundingRuntime : plain C#
    owns GroundState
    owns jumpCount
    owns forced unground event suppression
    emits OnLanded / OnLeftGround

RootMotionBuffer : plain C#
    owns pending root position/rotation
    owns Y dead-zone accumulation

VelocityReadout : plain C#
    owns CurrentVelocity / horizontal / vertical speed
    owns vertical smoothing

ActorMovement : MonoBehaviour facade
    keeps serialized config
    keeps locomotion intent + facing pipeline for now
    forwards runtime APIs to ActorMotor.MotionRuntime
```

第一阶段不迁 `Facing`，所以 `ActorMotor.UpdateRotation` 仍然从 `ActorMovement` 取 pending facing rotation，但 root motion rotation 改从 runtime 取。

## 核心数据流

现在的流向要从：

```text
ActorMotor -> ActorMovement -> MotionChannels
ActorMotor <- ActorMovement.GetMovementState()
```

改成：

```text
External systems -> ActorMovement facade -> ActorMotor.MotionRuntime
ActorMotor KCC callbacks -> ActorMotionRuntime
ActorMotor reads ActorMovement cached locomotion/facing only
```

## 执行澄清

### 1. Runtime 配置值来源

`ActorMotionRuntime` 不持有 `ActorMovement` 引用，也不在构造时永久缓存配置值。

第一阶段采用明确参数/配置快照：

```csharp
public readonly struct ActorMotionRuntimeConfig
{
    public readonly float HorizontalDrag;
    public readonly float VerticalImpulseAirDrag;
    public readonly float VerticalSmoothTime;
    public readonly float RootMotionYDeadZone;
}
```

- `ActorMovement` 继续保存序列化配置字段，提供 `GetRuntimeConfig()`。
- `ActorMotor` 在 KCC tick 中读取 `_movement.GetRuntimeConfig()`，传给 runtime 的 `StepChannels` / `PublishSolvedVelocity` / RootMotion 相关方法。
- `gravityScale`、`movementTimeScale`、`rootMotionApplyMode` 是运行时策略状态，不是静态配置；迁入 `ActorMotionRuntime`，由 `ActorMovement.SetGravityScale` / `SetMovementTimeScale` / `SetRootMotionApplyMode` 转发设置。
- 不使用 `ActorMotionRuntime -> ActorMovement` 反向引用，避免重新制造所有权混乱。

### 2. ActorMovement 绑定 ActorMotor 的方式

绑定由 `ActorMotor.Awake` 主导：

```csharp
private void Awake()
{
    Motor = GetComponent<KinematicCharacterMotor>();
    MotionRuntime = new ActorMotionRuntime();

    var actor = GetComponent<Actor>();
    _movement = actor != null && actor.movement != null
        ? actor.movement
        : GetComponent<ActorMovement>();

    _movement?.BindMotor(this);
    Motor.CharacterController = this;
}
```

`ActorMovement` 增加内部绑定和懒查找兜底：

```csharp
private ActorMotor _boundMotor;

internal void BindMotor(ActorMotor motor)
{
    _boundMotor = motor;
}

private ActorMotor ResolveMotor()
{
    if (_boundMotor != null)
        return _boundMotor;

    _boundMotor = actor != null && actor.actorMotor != null
        ? actor.actorMotor
        : GetComponent<ActorMotor>();

    return _boundMotor;
}
```

不在 `Actor.Awake` 里做绑定，避免组件 Awake 顺序成为隐性依赖。

### 3. MovementState 的替代

删除 `MovementState` 后，由 `ActorMotionRuntime.ComposeKccVelocity` 内化原来的分支逻辑：

```csharp
public Vector3 ComposeKccVelocity(
    KinematicCharacterMotor motor,
    Vector3 locomotionVelocity,
    bool isGrounded,
    float deltaTime)
{
    if (_rootMotionApplyMode == ActorMovement.RootMotionApplyMode.Managed &&
        _rootMotion.PendingPosition.sqrMagnitude > 0.0001f)
    {
        Vector3 velocity = _rootMotion.PendingPosition / deltaTime;
        if (isGrounded)
            velocity = motor.GetDirectionTangentToSurface(
                velocity,
                motor.GroundingStatus.GroundNormal) * velocity.magnitude;
        return velocity;
    }

    Vector3 horizontal = _channels.ComposeHorizontal(locomotionVelocity);
    float vertical = _channels.ComposeVertical();

    if (isGrounded)
    {
        horizontal = motor.GetDirectionTangentToSurface(
            horizontal,
            motor.GroundingStatus.GroundNormal) * horizontal.magnitude;
        vertical = 0f;
    }

    return horizontal + motor.CharacterUp * vertical;
}
```

`RootMotionApplyMode` 由 runtime 保存，`ActorMovement.SetRootMotionApplyMode` 只转发设置。和现状一致，第一阶段只让 RootMotion position 的 Managed/External 影响 KCC velocity；RootMotion rotation 仍按当前逻辑参与 `UpdateRotation`。

### 4. BeginMotorTick 的职责

不要在 `UpdateVelocity` 第一行放一个可能为空的 `BeginKccTick()`。

改为在 `ActorMotor.BeforeCharacterUpdate` 调用：

```csharp
public void BeforeCharacterUpdate(float deltaTime)
{
    _motorFrameStartWorldPosition = transform.position;
    _requestedVelocity = Vector3.zero;
    _hasRequestedVelocity = false;
    _pausedThisTick = false;

    MotionRuntime.BeginMotorTick();
}
```

`BeginMotorTick()` 至少负责重置 runtime 的 per-tick 状态：

```csharp
public void BeginMotorTick()
{
    _forceUngroundedThisTick = false;
}
```

如果后续没有更多 per-tick runtime 状态，可以内联为 `MotionRuntime.ClearTickFlags()`，但第一阶段不保留空方法。

### 5. AfterCharacterUpdate 必须保留现有 solved velocity 逻辑

`ComputeSolvedVelocity` 不是新算法，直接保留当前逻辑：

```csharp
private Vector3 ComputeSolvedVelocity(float deltaTime)
{
    if (_pausedThisTick || deltaTime <= 0f)
        return Vector3.zero;

    Vector3 solvedDelta = Motor.TransientPosition - _motorFrameStartWorldPosition;
    Vector3 finalVelocity = solvedDelta / deltaTime;

    if (finalVelocity.sqrMagnitude < 0.000001f && _hasRequestedVelocity)
        finalVelocity = Motor.BaseVelocity;

    return finalVelocity;
}
```

`AfterCharacterUpdate` 继续做：

```csharp
Vector3 finalVelocity = ComputeSolvedVelocity(deltaTime);
bool grounded = Motor.GroundingStatus.IsStableOnGround &&
                !MotionRuntime.ForceUngroundedThisTick;

MotionRuntime.PublishSolvedVelocity(
    finalVelocity,
    grounded,
    Time.fixedDeltaTime,
    config.VerticalSmoothTime);

MotionRuntime.EndMotorTick();
```

### 6. HitStop / MovementTimeScale == 0 的守卫必须保留

`ActorMotor.UpdateVelocity` 继续保留当前早退逻辑，且早退必须发生在 consume forced unground 和 root motion velocity 计算之前：

```csharp
if (_movement == null || deltaTime <= 0f)
{
    SetPausedVelocity(ref currentVelocity);
    return;
}

float effDt = deltaTime * MotionRuntime.MovementTimeScale;
if (effDt <= 0.0001f)
{
    SetPausedVelocity(ref currentVelocity);
    return;
}
```

这样 paused tick 不会进入 `RootMotionDelta / deltaTime` 这类逻辑，也不会在 HitStop 中推进通道。`AfterCharacterUpdate` 仍会调用 `MotionRuntime.EndMotorTick()`，保持当前帧末清理语义。

### 7. `_motorFrameStartWorldPosition` 归属

`_motorFrameStartWorldPosition`、`_requestedVelocity`、`_hasRequestedVelocity`、`_pausedThisTick` 第一阶段保留在 `ActorMotor`。

理由：它们是 KCC bridge 为了计算 solved velocity 和处理 paused tick 的帧级胶水状态，不是独立运动域状态。runtime 只接收最终 solved velocity 并发布 readout。

### 8. OnAnimatorMove 到 RootMotionBuffer 的数据流

理解正确。第一阶段链路为：

```text
ActorMovement.OnAnimatorMove
    -> ResolveMotor().MotionRuntime.AddAnimatorDelta(
           animator.deltaPosition,
           animator.deltaRotation,
           config.RootMotionYDeadZone)
    -> RootMotionBuffer.ApplyYDeadZone(...)
```

`RootMotionBuffer` 持有 `_accumulatedYDelta` 状态；`RootMotionYDeadZone` 配置值仍来自 `ActorMovement` 的序列化字段。

伪代码：

```csharp
public void AddAnimatorDelta(
    Vector3 deltaPosition,
    Quaternion deltaRotation,
    float yDeadZone)
{
    _pendingPosition += ApplyYDeadZone(deltaPosition, yDeadZone);
    _pendingRotation = deltaRotation * _pendingRotation;
}

private Vector3 ApplyYDeadZone(Vector3 rawDelta, float yDeadZone)
{
    float currentYDelta = rawDelta.y;
    if (Mathf.Abs(currentYDelta) < yDeadZone)
    {
        _accumulatedYDelta += currentYDelta;
        if (Mathf.Abs(_accumulatedYDelta) >= yDeadZone)
        {
            rawDelta.y = _accumulatedYDelta;
            _accumulatedYDelta = 0f;
        }
        else
        {
            rawDelta.y = 0f;
        }
    }
    else
    {
        _accumulatedYDelta = 0f;
    }

    return rawDelta;
}
```

### 9. ActorMovement facade 的事件桥接

`OnLanded` / `OnLeftGround` 迁入 `GroundingRuntime` 后，`ActorMovement` 仍保留同名 public event，外部订阅代码不迁移。

`ActorMovement` 的事件实现改为自定义 add/remove，直接桥接到 runtime：

```csharp
public event Action OnLanded
{
    add
    {
        var runtime = ResolveMotor()?.MotionRuntime;
        if (runtime != null)
            runtime.OnLanded += value;
    }
    remove
    {
        var runtime = ResolveMotor()?.MotionRuntime;
        if (runtime != null)
            runtime.OnLanded -= value;
    }
}

public event Action OnLeftGround
{
    add
    {
        var runtime = ResolveMotor()?.MotionRuntime;
        if (runtime != null)
            runtime.OnLeftGround += value;
    }
    remove
    {
        var runtime = ResolveMotor()?.MotionRuntime;
        if (runtime != null)
            runtime.OnLeftGround -= value;
    }
}
```

`ActorMotionRuntime` 暴露只转发 `GroundingRuntime` 的事件：

```csharp
public event Action OnLanded
{
    add => _grounding.OnLanded += value;
    remove => _grounding.OnLanded -= value;
}

public event Action OnLeftGround
{
    add => _grounding.OnLeftGround += value;
    remove => _grounding.OnLeftGround -= value;
}
```

注意：如果未来存在“订阅发生在 `ActorMotor.Awake` 绑定前”的场景，需要在 `ActorMovement` 内部保存本地订阅表并在 `BindMotor` 时 replay。第一阶段按当前项目组件使用方式，采用直接桥接；如编译或运行检查发现早订阅，再补 replay。

`ActorMovement` 继续负责输入意图：

```csharp
// ActorMovement remains high-level input/facade.
public void SetLocomotionIntent(in LocomotionIntent intent)
{
    _locomotionIntent = intent;
    _hasLocomotionIntent = true;
}

internal Vector3 GetCachedLocomotionVelocity()
{
    return _cachedLocomotionVelocity;
}
```

但低层运动状态转发给 runtime：

```csharp
public void AddVerticalImpulse(float upwardSpeed)
{
    ResolveMotor()?.MotionRuntime.AddVerticalImpulse(upwardSpeed);
}

public Vector3 CurrentVelocity =>
    ResolveMotor()?.MotionRuntime.CurrentVelocity ?? Vector3.zero;

public GroundState groundState =>
    ResolveMotor()?.MotionRuntime.GroundState ?? GroundState.Grounded;
```

## 关键实现伪代码

`ActorMotor` 成为 runtime 调度者：

```csharp
public sealed class ActorMotor : MonoBehaviour, ICharacterController
{
    public ActorMotionRuntime MotionRuntime { get; private set; }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (_movement == null || deltaTime <= 0f)
        {
            SetPausedVelocity(ref currentVelocity);
            return;
        }

        float effDt = deltaTime * MotionRuntime.MovementTimeScale;
        if (effDt <= 0.0001f)
        {
            SetPausedVelocity(ref currentVelocity);
            return;
        }

        if (MotionRuntime.ConsumeForceUngroundRequest())
        {
            Motor.ForceUnground(0.1f);
            MotionRuntime.MarkForcedUngroundedThisTick();
        }

        bool grounded = Motor.GroundingStatus.IsStableOnGround &&
                        !MotionRuntime.ForceUngroundedThisTick;

        ActorMotionRuntimeConfig config = _movement.GetRuntimeConfig();
        MotionRuntime.StepChannels(deltaTime, grounded, config);

        Vector3 locomotion = _movement.GetCachedLocomotionVelocity();
        currentVelocity = MotionRuntime.ComposeKccVelocity(
            Motor,
            locomotion,
            grounded,
            deltaTime);
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        MotionRuntime.ApplyKccGrounding(
            Motor.GroundingStatus.IsStableOnGround,
            Motor.LastGroundingStatus.IsStableOnGround);
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        Vector3 solvedVelocity = ComputeSolvedVelocity(deltaTime);
        bool grounded = Motor.GroundingStatus.IsStableOnGround &&
                        !MotionRuntime.ForceUngroundedThisTick;

        ActorMotionRuntimeConfig config = _movement.GetRuntimeConfig();
        MotionRuntime.PublishSolvedVelocity(
            solvedVelocity,
            grounded,
            Time.fixedDeltaTime,
            config.VerticalSmoothTime);
        MotionRuntime.EndMotorTick();
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
        if (Vector3.Dot(hitNormal, Motor.CharacterUp) < -0.3f)
            MotionRuntime.SignalCeilingHit();
    }
}
```

`ActorMotionRuntime` 聚合 KCC 强绑定状态：

```csharp
public sealed class ActorMotionRuntime
{
    private readonly MotionChannels _channels = new();
    private readonly GroundingRuntime _grounding = new();
    private readonly RootMotionBuffer _rootMotion = new();
    private readonly VelocityReadout _velocity = new();

    private bool _pendingForceUnground;
    private bool _forceUngroundedThisTick;
    private bool _pendingCeilingHit;

    public ActorMovement.GroundState GroundState => _grounding.State;
    public Vector3 CurrentVelocity => _velocity.CurrentVelocity;
    public bool ForceUngroundedThisTick => _forceUngroundedThisTick;

    public void AddVerticalImpulse(float speed)
    {
        _channels.AddVerticalImpulse(speed);
        if (speed > 0f)
            _pendingForceUnground = true;
    }

    public bool ConsumeForceUngroundRequest()
    {
        bool result = _pendingForceUnground;
        _pendingForceUnground = false;
        return result;
    }

    public void MarkForcedUngroundedThisTick()
    {
        _forceUngroundedThisTick = true;
        _grounding.ForceUngroundNow();
    }

    public void StepChannels(
        float deltaTime,
        bool grounded,
        ActorMotionRuntimeConfig config)
    {
        float dt = deltaTime * MovementTimeScale;
        _channels.StepGravity(dt, grounded, GravityScale);
        _channels.StepHorizontalDrag(dt, config.HorizontalDrag);
        _channels.StepVerticalImpulseDecay(
            dt,
            !grounded,
            grounded,
            _pendingCeilingHit,
            config.VerticalImpulseAirDrag);

        _pendingCeilingHit = false;
    }

    public void PublishSolvedVelocity(
        Vector3 solvedVelocity,
        bool isStableGrounded,
        float smoothingDeltaTime,
        float verticalSmoothTime)
    {
        _velocity.Publish(
            solvedVelocity,
            isStableGrounded,
            smoothingDeltaTime,
            verticalSmoothTime);
    }

    public void EndMotorTick()
    {
        _forceUngroundedThisTick = false;
        _rootMotion.ClearAfterMotorTick();
    }
}
```

`VelocityReadout` 接管当前 `ActorMovement.PublishMotorVelocity` 的读数逻辑：

```csharp
public sealed class VelocityReadout
{
    private Vector3 _currentVelocity;
    private float _currentHorizontalSpeed;
    private float _currentVerticalSpeed;
    private float _smoothedVelocityY;
    private float _smoothedVelocityYRef;

    public Vector3 CurrentVelocity => _currentVelocity;
    public float CurrentHorizontalSpeed => _currentHorizontalSpeed;
    public float CurrentVerticalSpeed => _currentVerticalSpeed;

    public void Publish(
        Vector3 solvedVelocity,
        bool isStableGrounded,
        float deltaTime,
        float verticalSmoothTime)
    {
        float targetY = isStableGrounded ? 0f : solvedVelocity.y;
        _smoothedVelocityY = Mathf.SmoothDamp(
            _smoothedVelocityY,
            targetY,
            ref _smoothedVelocityYRef,
            verticalSmoothTime,
            float.MaxValue,
            deltaTime);

        _currentVelocity = new Vector3(
            solvedVelocity.x,
            _smoothedVelocityY,
            solvedVelocity.z);
        _currentHorizontalSpeed = new Vector2(
            solvedVelocity.x,
            solvedVelocity.z).magnitude;
        _currentVerticalSpeed = _smoothedVelocityY;
    }
}
```

`GroundingRuntime` 收拢当前分散的事件补丁：

```csharp
public sealed class GroundingRuntime
{
    public ActorMovement.GroundState State { get; private set; }
    public int JumpCount { get; private set; }
    public event Action OnLanded;
    public event Action OnLeftGround;

    private bool _suppressNextKccLeftGroundEvent;

    public void ApplyKccGrounding(bool isStableNow, bool wasStable)
    {
        AdvanceTransientState();

        if (isStableNow && !wasStable)
        {
            State = ActorMovement.GroundState.JustLanded;
            JumpCount = 0;
            _suppressNextKccLeftGroundEvent = false;
            OnLanded?.Invoke();
        }
        else if (!isStableNow && wasStable)
        {
            State = ActorMovement.GroundState.JustLeftGround;

            if (_suppressNextKccLeftGroundEvent)
                _suppressNextKccLeftGroundEvent = false;
            else
                OnLeftGround?.Invoke();
        }
    }

    public void ForceUngroundNow()
    {
        if (State is ActorMovement.GroundState.Grounded
            or ActorMovement.GroundState.JustLanded)
        {
            State = ActorMovement.GroundState.JustLeftGround;
            _suppressNextKccLeftGroundEvent = true;
            OnLeftGround?.Invoke();
        }
    }

    public bool CanJump(int maxJumpCount)
    {
        return JumpCount < maxJumpCount;
    }

    public void ConsumeJump()
    {
        JumpCount++;
    }

    private void AdvanceTransientState()
    {
        if (State == ActorMovement.GroundState.JustLanded)
            State = ActorMovement.GroundState.Grounded;
        else if (State == ActorMovement.GroundState.JustLeftGround)
            State = ActorMovement.GroundState.Airborne;
    }
}
```

`RootMotionBuffer` 接管 root motion 累积：

```csharp
public sealed class RootMotionBuffer
{
    private Vector3 _pendingPosition;
    private Quaternion _pendingRotation = Quaternion.identity;
    private float _accumulatedYDelta;

    public void AddAnimatorDelta(
        Vector3 deltaPosition,
        Quaternion deltaRotation,
        float yDeadZone)
    {
        _pendingPosition += ApplyYDeadZone(deltaPosition, yDeadZone);
        _pendingRotation = deltaRotation * _pendingRotation;
    }

    public Vector3 PendingPosition => _pendingPosition;
    public Quaternion PendingRotation => _pendingRotation;

    public void ClearAfterMotorTick()
    {
        _pendingPosition = Vector3.zero;
        _pendingRotation = Quaternion.identity;
    }

    private Vector3 ApplyYDeadZone(Vector3 rawDelta, float yDeadZone)
    {
        float currentYDelta = rawDelta.y;
        if (Mathf.Abs(currentYDelta) < yDeadZone)
        {
            _accumulatedYDelta += currentYDelta;
            if (Mathf.Abs(_accumulatedYDelta) >= yDeadZone)
            {
                rawDelta.y = _accumulatedYDelta;
                _accumulatedYDelta = 0f;
            }
            else
            {
                rawDelta.y = 0f;
            }
        }
        else
        {
            _accumulatedYDelta = 0f;
        }

        return rawDelta;
    }
}
```

## 迁移步骤

- 添加 `ActorMotionRuntime`、`GroundingRuntime`、`RootMotionBuffer`、`VelocityReadout`，先不删旧代码。
- `ActorMotor.Awake` 创建 runtime，并让 `ActorMovement` 绑定到当前 `ActorMotor`。
- 把 `MotionChannels` 字段从 `ActorMovement` 迁到 `ActorMotionRuntime`；`ActorMovement` 的 impulse/velocity owner API 改为转发。
- 把 `GroundState`、`jumpCount`、`OnLanded`、`OnLeftGround` 迁入 `GroundingRuntime`；`ActorMovement` 的属性和事件改为 facade。
- 把 `_pendingForceUnground`、`_forceUngroundedThisTick`、`_pendingCeilingHit` 收进 `ActorMotionRuntime`。
- 把 root motion pending delta、rotation、Y dead-zone 累积迁入 `RootMotionBuffer`；`ActorMovement.OnAnimatorMove` 只采集并转发。
- 把 `CurrentVelocity`、Y 平滑、水平/垂直速度读数迁入 `VelocityReadout`。
- 保留 `ActorMovement` 的 locomotion/facing `Update`，但其 `IsAirborne` 查询改为读取 runtime ground state。
- 删除或改名 `ActorMovement` 中只供 `ActorMotor` 调用的 internal 中转方法，如 `StepChannels`、`GetMovementState`、`SignalCeilingHit`、`PublishMotorVelocity`。

## 行为验收

- 外部调用点无需修改：`actor.movement.*` 仍可编译运行。
- 跳跃/上抛当帧进入 `JustLeftGround`，`OnLeftGround` 不双发。
- KCC 下一帧 stable->unstable 只更新状态，不重复发离地事件。
- 落地进入 `JustLanded`，下一次推进变为 `Grounded`，并重置 `jumpCount`。
- 接地时竖直通道继续清理下落残留，KCC 合成速度仍将 vertical 置 0。
- 撞天花板后 upward vertical impulse 被清掉。
- Managed RootMotion 仍由 KCC 消费，External 模式不消费 root motion。
- `CurrentVelocity` 仍代表 KCC solved velocity，而不是原始通道合成速度。
- `MovementTimeScale` 仍影响重力、冲量衰减、motion tick，不破坏 HitStop。

## Assumptions

- 第一阶段不迁移 serialized config 字段，避免 Unity 资源数据风险。
- 第一阶段不重写 facing 管线，只保留 `ActorMovement` 当前实现。
- 第一阶段不删除 `GroundStateTracker`、`MotionControlOwner` 等历史残留；只在必要时补注释或标记后续清理。
- 第一阶段目标是“所有权清晰 + 行为不变”，不是一次性完成最终命名和 API 清理。
