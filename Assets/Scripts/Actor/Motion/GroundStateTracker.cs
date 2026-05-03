using System;

/// <summary>
/// [Obsolete] 地面状态追踪已由 ActorMovement.ApplyGroundingUpdate 接管。
/// 保留此文件仅作为迁移参考，后续版本将移除。
/// </summary>
[Obsolete("Ground state tracking is now handled by ActorMovement.ApplyGroundingUpdate.")]
public sealed class GroundStateTracker
{
    private ActorMovement.GroundState _state = ActorMovement.GroundState.Grounded;
    private int _airborneFrameCounter;
    private int _forceUngroundFrames;

    public ActorMovement.GroundState State => _state;

    public bool IsGrounded =>
        _state == ActorMovement.GroundState.Grounded ||
        _state == ActorMovement.GroundState.JustLanded;

    public bool IsAirborne =>
        _state == ActorMovement.GroundState.Airborne ||
        _state == ActorMovement.GroundState.JustLeftGround;

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

        if (_state == ActorMovement.GroundState.JustLanded)
            _state = ActorMovement.GroundState.Grounded;
        else if (_state == ActorMovement.GroundState.JustLeftGround)
            _state = ActorMovement.GroundState.Airborne;

        if (rawGrounded)
        {
            _airborneFrameCounter = 0;
            if (_state == ActorMovement.GroundState.Airborne)
            {
                _state = ActorMovement.GroundState.JustLanded;
                landed = true;
            }
        }
        else
        {
            if (dt > 1e-8f)
                _airborneFrameCounter++;

            if (_state == ActorMovement.GroundState.Grounded &&
                _airborneFrameCounter > airborneFrameThreshold)
            {
                _state = ActorMovement.GroundState.JustLeftGround;
                leftGround = true;
            }
        }
    }

    public bool ForceUnground(int frames)
    {
        bool wasGrounded =
            _state == ActorMovement.GroundState.Grounded ||
            _state == ActorMovement.GroundState.JustLanded;

        _forceUngroundFrames = frames > _forceUngroundFrames ? frames : _forceUngroundFrames;
        _airborneFrameCounter = 0;
        _state = ActorMovement.GroundState.Airborne;

        return wasGrounded;
    }
}
