using System;

/// <summary>
/// 管理 KCC 接地事实映射出来的 gameplay 接地状态。
/// 负责 GroundState、jumpCount，以及 OnLanded / OnLeftGround 事件发布。
/// </summary>
public sealed class GroundingRuntime
{
    public ActorGroundState State { get; private set; }
    public int JumpCount { get; private set; }
    public event Action OnLanded;
    public event Action OnLeftGround;

    private bool _leftGroundEventAlreadyEmittedByForceUnground;

    /// <summary>
    /// 同步 KCC 的稳定接地状态，并把 stable/unstable 边沿转换为 gameplay 事件。
    /// </summary>
    public void ApplyKccGrounding(bool isStableNow, bool wasStable)
    {
        AdvanceTransientState();

        if (isStableNow && !wasStable)
        {
            State = ActorGroundState.JustLanded;
            JumpCount = 0;
            _leftGroundEventAlreadyEmittedByForceUnground = false;
            OnLanded?.Invoke();
        }
        else if (!isStableNow && wasStable)
        {
            State = ActorGroundState.JustLeftGround;

            if (_leftGroundEventAlreadyEmittedByForceUnground)
                _leftGroundEventAlreadyEmittedByForceUnground = false;
            else
                OnLeftGround?.Invoke();
        }
    }

    /// <summary>
    /// 主动离地当帧立即进入 JustLeftGround 并发布 OnLeftGround。
    /// KCC 下一次 stable -> unstable 边沿只同步状态，不重复发布事件。
    /// </summary>
    public void ForceUngroundNow()
    {
        if (State is ActorGroundState.Grounded
            or ActorGroundState.JustLanded)
        {
            State = ActorGroundState.JustLeftGround;
            _leftGroundEventAlreadyEmittedByForceUnground = true;
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
        if (State == ActorGroundState.JustLanded)
            State = ActorGroundState.Grounded;
        else if (State == ActorGroundState.JustLeftGround)
            State = ActorGroundState.Airborne;
    }
}
