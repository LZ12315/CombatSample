using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 缓存 Animator 输出的原始 RootMotion 位移/旋转。
/// 是否把这些 RootMotion 提交到 Translation / Rotation Domain，暂由
/// ActorMotionRuntime compatibility façade 的 RootMotionApplyMode 决定。
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

    private readonly List<TrajectoryOwnerState> _trajectoryOwners = new();

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
        MotionOwner owner = NewOwner();
        _trajectoryOwners.Add(new TrajectoryOwnerState(owner, Vector3.zero));
        return owner;
    }

    public bool SubmitTrajectory(MotionOwner owner, Vector3 localPositionDelta)
    {
        int index = FindTrajectoryOwnerIndex(owner);
        if (index < 0)
            return false;

        TrajectoryOwnerState state = _trajectoryOwners[index];
        _trajectoryOwners[index] = new TrajectoryOwnerState(owner, state.PendingLocalPosition + localPositionDelta);
        return true;
    }

    public void EndTrajectory(MotionOwner owner)
    {
        int index = FindTrajectoryOwnerIndex(owner);
        if (index < 0)
            return;

        _trajectoryOwners.RemoveAt(index);
        if (IsCurrent(owner, _tickTrajectoryOwner))
        {
            _tickTrajectoryOwner = default;
            _tickTrajectoryLocalPosition = Vector3.zero;
        }
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

        if (_trajectoryOwners.Count > 0)
        {
            TrajectoryOwnerState top = _trajectoryOwners[_trajectoryOwners.Count - 1];
            _tickTrajectoryOwner = top.Owner;
            _tickTrajectoryLocalPosition = top.PendingLocalPosition;
        }
        else
        {
            _tickTrajectoryOwner = default;
            _tickTrajectoryLocalPosition = Vector3.zero;
        }

        for (int i = 0; i < _trajectoryOwners.Count; i++)
            _trajectoryOwners[i] = new TrajectoryOwnerState(_trajectoryOwners[i].Owner, Vector3.zero);
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

    private int FindTrajectoryOwnerIndex(MotionOwner owner)
    {
        if (!owner.IsValid)
            return -1;

        for (int i = _trajectoryOwners.Count - 1; i >= 0; i--)
        {
            if (_trajectoryOwners[i].Owner.Id == owner.Id)
                return i;
        }

        return -1;
    }

    private readonly struct TrajectoryOwnerState
    {
        public readonly MotionOwner Owner;
        public readonly Vector3 PendingLocalPosition;

        public TrajectoryOwnerState(MotionOwner owner, Vector3 pendingLocalPosition)
        {
            Owner = owner;
            PendingLocalPosition = pendingLocalPosition;
        }
    }
}
