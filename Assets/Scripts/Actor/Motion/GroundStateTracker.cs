using System;

/// <summary>
/// [Obsolete] 地面状态追踪已由 ActorMotionRuntime 接管。
/// 保留此文件仅作为迁移参考，后续版本将移除。
/// </summary>
[Obsolete("Ground state tracking is now handled by ActorMotionRuntime.")]
public sealed class GroundStateTracker
{
    private ActorGroundState _state = ActorGroundState.Grounded;
    private int _airborneFrameCounter;
    private int _forceUngroundFrames;

    public ActorGroundState State => _state;

    public bool IsGrounded =>
        _state == ActorGroundState.Grounded ||
        _state == ActorGroundState.JustLanded;

    public bool IsAirborne =>
        _state == ActorGroundState.Airborne ||
        _state == ActorGroundState.JustLeftGround;

    public void Update(
        bool rawGrounded,
        float dt,
        int airborneFrameThreshold,
        out bool landed,
        out bool leftGround)
    {
        landed = false;
        leftGround = false;

        if (_forceUngroundFrames > 0)
        {
            rawGrounded = false;
            _forceUngroundFrames--;
        }

        if (_state == ActorGroundState.JustLanded)
            _state = ActorGroundState.Grounded;
        else if (_state == ActorGroundState.JustLeftGround)
            _state = ActorGroundState.Airborne;

        if (rawGrounded)
        {
            _airborneFrameCounter = 0;
            if (_state == ActorGroundState.Airborne)
            {
                _state = ActorGroundState.JustLanded;
                landed = true;
            }
        }
        else
        {
            if (dt > 1e-8f)
                _airborneFrameCounter++;

            if (_state == ActorGroundState.Grounded &&
                _airborneFrameCounter > airborneFrameThreshold)
            {
                _state = ActorGroundState.JustLeftGround;
                leftGround = true;
            }
        }
    }

    public bool ForceUnground(int frames)
    {
        bool wasGrounded =
            _state == ActorGroundState.Grounded ||
            _state == ActorGroundState.JustLanded;

        _forceUngroundFrames = frames > _forceUngroundFrames ? frames : _forceUngroundFrames;
        _airborneFrameCounter = 0;
        _state = ActorGroundState.Airborne;

        return wasGrounded;
    }
}
