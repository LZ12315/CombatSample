using System;

/// <summary>
/// Owns GroundState, jumpCount, forced unground event suppression.
/// Emits OnLanded / OnLeftGround events.
/// </summary>
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
