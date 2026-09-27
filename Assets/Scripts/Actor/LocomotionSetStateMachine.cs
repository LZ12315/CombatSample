using UnityEngine;

public enum LocomotionSetState
{
    Move,
    Start,
    Stop,
    Pivot,
}

/// <summary>Animation decisions only. It never integrates velocity or reads animation assets.</summary>
public sealed class LocomotionSetStateMachine
{
    private const float StationarySpeed = 0.1f;
    private const float PivotSpeed = 0.5f;
    private const float PivotDot = -0.5f;
    private bool _hadInput;
    private bool _pivotLatched;
    private bool _stopSettled;
    private int _actionOwnerId;

    public LocomotionSetState State { get; private set; }
    public bool UseZeroMoveParameter => _stopSettled;

    public void Reset()
    {
        State = LocomotionSetState.Move;
        _hadInput = false;
        _pivotLatched = false;
        _stopSettled = false;
        _actionOwnerId = 0;
    }

    /// <returns>Whether a new state was entered and its clip needs to start from zero.</returns>
    public bool Step(in LocomotionRuntimeAnimationContext context, bool clipCompleted)
    {
        if (!LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f)
            return false;

        bool input = context.HasMovingInput;
        Vector3 source = context.VelocityBeforeMotion;
        source.y = 0f;
        Vector3 target = context.Intent.WorldMoveDirection;
        target.y = 0f;
        float speed = source.magnitude;
        bool pivot = input && speed >= PivotSpeed && Vector3.Dot(source.normalized, target.normalized) <= PivotDot;
        bool pivotEdge = pivot && !_pivotLatched;
        _pivotLatched = pivot;

        LocomotionSetState next = State;
        if (context.ActionOwnerId != 0 || _actionOwnerId != 0)
        {
            // Rebaseline both on cover and release: no delayed Start/Stop/Pivot after an Action.
            next = LocomotionSetState.Move;
            _stopSettled = false;
        }
        else if (!input)
        {
            if (State == LocomotionSetState.Stop && clipCompleted)
            {
                next = LocomotionSetState.Move;
                _stopSettled = true;
            }
            else if (_hadInput || State == LocomotionSetState.Start || State == LocomotionSetState.Pivot)
            {
                next = speed > StationarySpeed ? LocomotionSetState.Stop : LocomotionSetState.Move;
            }
        }
        else
        {
            _stopSettled = false;
            if (State == LocomotionSetState.Stop)
                next = speed <= StationarySpeed ? LocomotionSetState.Start
                    : pivotEdge ? LocomotionSetState.Pivot : LocomotionSetState.Move;
            else if (State == LocomotionSetState.Start || State == LocomotionSetState.Pivot)
            {
                if (clipCompleted)
                    next = LocomotionSetState.Move;
            }
            else if (!_hadInput && speed <= StationarySpeed)
                next = LocomotionSetState.Start;
            else if (pivotEdge)
                next = LocomotionSetState.Pivot;
        }

        _hadInput = input;
        _actionOwnerId = context.ActionOwnerId;
        bool changed = next != State;
        State = next;
        return changed;
    }
}
