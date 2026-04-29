/// <summary>
/// 地面状态机：依据 CharacterController.isGrounded 与离地帧滤波输出 <see cref="ActorMovement.GroundState"/>。
/// </summary>
public sealed class GroundStateTracker
{
    public ActorMovement.GroundState State { get; private set; } = ActorMovement.GroundState.Grounded;

    private int _airborneFrameCounter;

    public struct StepResult
    {
        public ActorMovement.GroundState State;
        public bool LandedThisFrame;
        public bool LeftGroundThisFrame;
    }

    /// <param name="rawGrounded">CC.isGrounded</param>
    /// <param name="dt">演化用 dt（HitStop 时可为 0）</param>
    /// <param name="airborneFrameThreshold">连续离地帧数阈值</param>
    public StepResult Step(bool rawGrounded, float dt, int airborneFrameThreshold)
    {
        bool landed = false;
        bool left = false;

        if (State == ActorMovement.GroundState.JustLanded)
            State = ActorMovement.GroundState.Grounded;
        else if (State == ActorMovement.GroundState.JustLeftGround)
            State = ActorMovement.GroundState.Airborne;

        if (rawGrounded)
        {
            _airborneFrameCounter = 0;
            if (State == ActorMovement.GroundState.Airborne)
            {
                State = ActorMovement.GroundState.JustLanded;
                landed = true;
            }
        }
        else
        {
            if (dt > 1e-8f)
                _airborneFrameCounter++;
            if (State == ActorMovement.GroundState.Grounded && _airborneFrameCounter > airborneFrameThreshold)
            {
                State = ActorMovement.GroundState.JustLeftGround;
                left = true;
            }
        }

        return new StepResult
        {
            State = State,
            LandedThisFrame = landed,
            LeftGroundThisFrame = left
        };
    }
}
