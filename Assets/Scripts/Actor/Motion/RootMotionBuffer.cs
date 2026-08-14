using UnityEngine;

/// <summary>
/// 缓存 Animator 输出的原始 RootMotion 位移/旋转。
/// 是否把这些 RootMotion 应用到 KCC，由 ActorMotionRuntime 的 RootMotionApplyMode 决定。
///
/// 使用 snapshot 模式防止 stale delta：BeginMotorTick() 对当前累积值做快照并清空缓冲区，
/// 之后的 KCC tick 只消费快照。即使 EndMotorTick 因异常被跳过，残留数据也不会泄漏到下一帧。
/// </summary>
public sealed class RootMotionBuffer
{
    private int _nextOwnerId = 1;

    private Vector3 _pendingAnimatorPosition;
    private Quaternion _pendingAnimatorRotation = Quaternion.identity;

    private Vector3 _tickAnimatorPosition;
    private Quaternion _tickAnimatorRotation = Quaternion.identity;

    private MotionOwner _trajectoryOwner;
    private Vector3 _pendingTrajectoryLocalPosition;

    private MotionOwner _tickTrajectoryOwner;
    private Vector3 _tickTrajectoryLocalPosition;

    public Vector3 PendingPosition => _tickAnimatorPosition;
    public Quaternion PendingRotation => _tickAnimatorRotation;
    public bool HasTrajectoryTick => _tickTrajectoryOwner.IsValid;
    public Vector3 TrajectoryLocalPosition => _tickTrajectoryLocalPosition;

    public void AddAnimatorDelta(
        Vector3 deltaPosition,
        Quaternion deltaRotation)
    {
        _pendingAnimatorPosition += deltaPosition;
        _pendingAnimatorRotation = deltaRotation * _pendingAnimatorRotation;
    }

    public void ClearAnimator()
    {
        _pendingAnimatorPosition = Vector3.zero;
        _pendingAnimatorRotation = Quaternion.identity;
        _tickAnimatorPosition = Vector3.zero;
        _tickAnimatorRotation = Quaternion.identity;
    }

    public MotionOwner BeginTrajectory()
    {
        if (_trajectoryOwner.IsValid)
        {
            Debug.LogWarning(
                $"[RootMotionBuffer] Replacing active trajectory owner id={_trajectoryOwner.Id}. " +
                "Root motion trajectory is single-slot; old owner will not regain control automatically.");
        }

        _trajectoryOwner = NewOwner();
        _pendingTrajectoryLocalPosition = Vector3.zero;
        return _trajectoryOwner;
    }

    public bool SubmitTrajectory(MotionOwner owner, Vector3 localPositionDelta)
    {
        if (!IsCurrent(owner, _trajectoryOwner))
            return false;

        _pendingTrajectoryLocalPosition += localPositionDelta;
        return true;
    }

    public void EndTrajectory(MotionOwner owner)
    {
        if (!IsCurrent(owner, _trajectoryOwner))
            return;

        _trajectoryOwner = default;
        _pendingTrajectoryLocalPosition = Vector3.zero;
        _tickTrajectoryOwner = default;
        _tickTrajectoryLocalPosition = Vector3.zero;
    }

    /// <summary>
    /// 在 KCC tick 开始时调用：快照当前累积值供本帧消费，清空缓冲区继续接收下一帧的 root motion。
    /// </summary>
    public void BeginMotorTick()
    {
        _tickAnimatorPosition = _pendingAnimatorPosition;
        _tickAnimatorRotation = _pendingAnimatorRotation;
        _pendingAnimatorPosition = Vector3.zero;
        _pendingAnimatorRotation = Quaternion.identity;

        _tickTrajectoryOwner = _trajectoryOwner;
        _tickTrajectoryLocalPosition = _pendingTrajectoryLocalPosition;
        _pendingTrajectoryLocalPosition = Vector3.zero;
    }

    private MotionOwner NewOwner()
    {
        if (_nextOwnerId == int.MaxValue)
            _nextOwnerId = 1;

        return new MotionOwner(_nextOwnerId++);
    }

    private static bool IsCurrent(MotionOwner owner, MotionOwner current)
    {
        return owner.IsValid && owner.Id == current.Id;
    }
}
