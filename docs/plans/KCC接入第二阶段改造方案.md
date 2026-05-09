# KCC 接入第二阶段改造方案

## 一句话结论
现在这版 KCC 接入已经不是乱接，结构方向是对的。

`ActorMovement` 负责动作意图，`ActorMotor` 负责对接 KCC，`MotionChannels` 负责速度通道，这个分层是清楚的。

但它目前更像“第一阶段适配”：把旧的 CharacterController 运动模型搬到了 KCC 上。下一阶段要做的是让 KCC 的最终求解结果成为角色运动的唯一事实。

## 现在最重要的三个问题

### 1. 跳跃或挑飞首帧可能被地面状态吃掉

原因很直接：KCC 的本 tick 探地已经结束后，`ActorMotor.UpdateVelocity` 才调用 `Motor.ForceUnground()`。

这意味着本 tick 里仍然可能被当成“站在地面上”。随后地面分支会把 vertical 速度清零，所以跳跃、挑飞、起手浮空的第一帧可能没有真正的向上速度，地面状态也会晚一拍离开。

要改的点：
- 在调用 `Motor.ForceUnground()` 后，本 tick 立即用本地标记把 `isGrounded` 当成 `false`
- 同步通知 `ActorMovement`，让 gameplay 状态当帧就进入 `JustLeftGround`

### 2. `CurrentVelocity` 现在不是 KCC 的最终速度

原因是：现在在 `UpdateVelocity` 里就发布速度，但 KCC 真正的移动、撞墙、斜坡投影、刚体交互，都发生在 `UpdateVelocity` 返回之后。

所以现在 `CurrentVelocity` 更像“请求速度”，不是“实际速度”。这会影响动画 blend、`VelocityCondition`、撞墙后的跑步表现，以及 RootMotion 被墙挡住时的速度判断。

要改的点：
- `UpdateVelocity` 只负责生成送给 KCC 的 requested velocity
- `AfterCharacterUpdate` 里再根据 KCC 的最终位移发布 solved velocity

### 3. hit stop 时可能保留旧速度

原因是 `MovementTimeScale <= 0` 时，代码把 KCC velocity 置零后直接返回，但没有同步更新 `ActorMovement.CurrentVelocity`。

表现就是角色已经停住了，但条件系统、动画参数和调试面板还在读上一帧的速度。

要改的点：
- 暂停 tick 也必须走统一的速度发布路径
- `CurrentVelocity` 在 hit stop 时必须明确变成零

## 实现框架

这一阶段主要改两个文件：
- [ActorMotor.cs](D:/Project%20Library/Projects/Unity/CombatSample/Assets/Scripts/Actor/ActorMotor.cs)
- [ActorMovement.cs](D:/Project%20Library/Projects/Unity/CombatSample/Assets/Scripts/Actor/ActorMovement.cs)

### ActorMotor：从“提前发布速度”改成“最后发布结果”

新增运行时缓存：

```csharp
private Vector3 _motorFrameStartPosition;
private Vector3 _requestedVelocity;
private bool _hasRequestedVelocity;
private bool _forceUngroundedThisTick;
private bool _pausedThisTick;
```

`BeforeCharacterUpdate` 记录本 tick 起点：

```csharp
public void BeforeCharacterUpdate(float deltaTime)
{
    _motorFrameStartPosition = Motor.TransientPosition;
    _requestedVelocity = Vector3.zero;
    _hasRequestedVelocity = false;
    _forceUngroundedThisTick = false;
    _pausedThisTick = false;
}
```

`UpdateVelocity` 只计算送给 KCC 的速度，不在这里发布 `CurrentVelocity`：

```csharp
if (_movement == null || deltaTime <= 0f)
{
    currentVelocity = Vector3.zero;
    _requestedVelocity = Vector3.zero;
    _hasRequestedVelocity = true;
    _pausedThisTick = true;
    return;
}

float effDt = deltaTime * _movement.MovementTimeScale;
if (effDt <= 0.0001f)
{
    currentVelocity = Vector3.zero;
    _requestedVelocity = Vector3.zero;
    _hasRequestedVelocity = true;
    _pausedThisTick = true;
    return;
}

var preState = _movement.GetMovementState();
if (preState.ShouldForceUnground)
{
    Motor.ForceUnground(0.1f);
    _forceUngroundedThisTick = true;
    _movement.ApplyForcedUnground();
}

bool isGrounded = Motor.GroundingStatus.IsStableOnGround && !_forceUngroundedThisTick;
_movement.StepChannels(deltaTime, isGrounded, !isGrounded);

var state = _movement.GetMovementState();
currentVelocity = ComposeKccVelocity(state, isGrounded, deltaTime);
_requestedVelocity = currentVelocity;
_hasRequestedVelocity = true;
```

`ComposeKccVelocity` 把现有 RootMotion / External 分支抽成一个私有方法：

```csharp
private Vector3 ComposeKccVelocity(ActorMovement.MovementState state, bool isGrounded, float deltaTime)
{
    if (state.IsRootMotionManaged && state.RootMotionDelta.sqrMagnitude > 0.0001f)
    {
        Vector3 velocity = state.RootMotionDelta / deltaTime;
        if (isGrounded)
            velocity = Motor.GetDirectionTangentToSurface(velocity, Motor.GroundingStatus.GroundNormal) * velocity.magnitude;
        return velocity;
    }

    Vector3 horizontal = state.HorizontalVelocity;
    float vertical = state.VerticalVelocity;

    if (isGrounded)
    {
        horizontal = Motor.GetDirectionTangentToSurface(horizontal, Motor.GroundingStatus.GroundNormal) * horizontal.magnitude;
        vertical = 0f;
    }

    return horizontal + Motor.CharacterUp * vertical;
}
```

`AfterCharacterUpdate` 才发布 KCC 最终速度：

```csharp
public void AfterCharacterUpdate(float deltaTime)
{
    if (_movement == null)
        return;

    Vector3 finalVelocity = Vector3.zero;

    if (!_pausedThisTick && deltaTime > 0f)
    {
        Vector3 solvedDelta = Motor.TransientPosition - _motorFrameStartPosition;
        finalVelocity = solvedDelta / deltaTime;

        if (finalVelocity.sqrMagnitude < 0.000001f && _hasRequestedVelocity)
            finalVelocity = Motor.BaseVelocity;
    }

    bool isStableGrounded = Motor.GroundingStatus.IsStableOnGround && !_forceUngroundedThisTick;

    _movement.PublishMotorVelocity(finalVelocity, isStableGrounded);
    _movement.SignalMotorFrameEnd();
}
```

### ActorMovement：补一个主动离地接口

新增一个内部 API：

```csharp
internal void ApplyForcedUnground()
{
    if (_groundState == GroundState.Grounded || _groundState == GroundState.JustLanded)
    {
        _groundState = GroundState.JustLeftGround;
        OnLeftGround?.Invoke();
    }
}
```

原因是：`Motor.ForceUnground()` 影响的是 KCC 下一次探地，但动作条件和动画状态在当帧就需要知道“角色已经主动离地”。

`PublishMotorVelocity` 保留名字，但语义要明确成“发布 KCC 最终求解后的 gameplay velocity”：

```csharp
/// 发布 KCC 最终求解后的 gameplay velocity。
/// 动画、条件系统、调试面板都应该读这个值。
internal void PublishMotorVelocity(Vector3 velocity, bool isStableGrounded)
```

## 顺手清理项

- 删除或废弃 `GroundStateTracker`，因为现在已经由 `ActorMovement.ApplyGroundingUpdate` 接管
- `CharacterControllerRigidbodyPush` 改成 obsolete，后续用 KCC 的 rigidbody interaction 或 hit callback 方案替代
- `HitVfxFacingUtility` 里的 `TryCharacterControllerCenter` 改名为 `TryCharacterBodyCenter`
- `ActorMotor.IsColliderValidForCollisions` 增加 `LayerMask collisionMask = ~0`，先做 layer 级过滤

## 验收标准

- 地面跳跃首个 fixed tick，`CurrentVelocity.y > 0`
- 地面挑飞首个 fixed tick，角色不会被接地分支清掉 Y 速度
- `groundState` 在主动起跳或挑飞当帧进入 `JustLeftGround`
- hit stop 时 `CurrentVelocity == Vector3.zero`
- 撞墙时动画速度参数不再显示撞墙前的水平速度
- Managed RootMotion 攻击撞墙后，`CurrentVelocity` 反映被 KCC 裁剪后的实际位移
- `VelocityClip`、`ImpulseClip`、`Action` 切换后 owner 不泄漏
- `Assembly-CSharp.csproj` 编译通过

## 默认前提

- 不重写动作系统，Timeline、Action、RootMotion 配置格式保持兼容
- `CurrentVelocity` 统一定义为“KCC 最终求解后的 gameplay 速度”
- KCC 是运行时 Actor 的唯一物理执行层，旧 `CharacterController` 只作为迁移和兼容 fallback 存在
