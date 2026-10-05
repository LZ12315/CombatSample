using UnityEngine;

/// <summary>Animation decisions only. It never integrates velocity or reads animation assets.</summary>
public sealed class LocomotionSetStateMachine
{
    private readonly LocomotionTransitionDecisionConfig _config;
    private bool _hadInput;
    private bool _pivotLatched;
    private int _actionOwnerId;

    public LocomotionSetState State { get; private set; }

    public LocomotionSetStateMachine() : this(LocomotionTransitionDecisionConfig.Default) { }

    public LocomotionSetStateMachine(LocomotionTransitionDecisionConfig config)
    {
        if (!config.IsValid)
            throw new System.ArgumentException("Locomotion transition decision configuration is invalid.", nameof(config));
        _config = config;
    }

    public void Reset()
    {
        State = LocomotionSetState.Move;
        _hadInput = false;
        _pivotLatched = false;
        _actionOwnerId = 0;
    }

    /// <summary>Consumes this tick's edges and proposes a state. Runtime must prepare its pose before committing.</summary>
    internal LocomotionSetState DecideNextState(in LocomotionRuntimeAnimationContext context, bool clipCompleted)
    {
        if (!LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f)
            return State;

        bool input = context.HasMovingInput;
        Vector3 source = context.VelocityBeforeMotion;
        source.y = 0f;
        Vector3 target = context.Intent.WorldMoveDirection;
        target.y = 0f;
        float speed = source.magnitude;
        bool pivot = input && speed >= _config.PivotMinimumSpeed
            && Vector3.Angle(source, target) >= _config.PivotMinimumAngleDegrees;
        bool pivotEdge = pivot && !_pivotLatched;
        _pivotLatched = pivot;

        LocomotionSetState next = State;
        if (context.ActionOwnerId != 0 || _actionOwnerId != 0)
        {
            // Rebaseline both on cover and release: no delayed Start/Stop/Pivot after an Action.
            next = LocomotionSetState.Move;
        }
        else if (!input)
        {
            if (State == LocomotionSetState.Stop && clipCompleted)
            {
                next = LocomotionSetState.Move;
            }
            else if (_hadInput || State == LocomotionSetState.Start || State == LocomotionSetState.Pivot)
            {
                next = speed > _config.StationarySpeed ? LocomotionSetState.Stop : LocomotionSetState.Move;
            }
        }
        else
        {
            if (State == LocomotionSetState.Stop)
                next = speed <= _config.StationarySpeed ? LocomotionSetState.Start
                    : pivotEdge ? LocomotionSetState.Pivot : LocomotionSetState.Move;
            else if (State == LocomotionSetState.Start)
            {
                // A reversal interrupts Start even when its clip completes on this tick.
                // The runtime falls back to Move if no usable Pivot was authored.
                if (pivotEdge)
                    next = LocomotionSetState.Pivot;
                else if (clipCompleted)
                    next = LocomotionSetState.Move;
            }
            else if (State == LocomotionSetState.Pivot)
            {
                if (clipCompleted)
                    next = LocomotionSetState.Move;
            }
            else if (!_hadInput && speed <= _config.StationarySpeed)
                next = LocomotionSetState.Start;
            else if (pivotEdge)
                next = LocomotionSetState.Pivot;
        }

        _hadInput = input;
        _actionOwnerId = context.ActionOwnerId;
        return next;
    }

    /// <returns>Whether a prepared state was entered and its clip needs to restart.</returns>
    internal bool CommitState(LocomotionSetState next)
    {
        bool changed = next != State;
        State = next;
        return changed;
    }
}
