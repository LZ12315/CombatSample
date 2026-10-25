using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public sealed class LocomotionSetRuntime : LocomotionAnimationRuntime
{
    private readonly LocomotionBoundSet _definition;
    private readonly LocomotionSetStateMachine _machine;
    private readonly Dictionary<AnimationClip, ClipState> _clips = new();
    private readonly Dictionary<LocomotionBoundTransition, LocomotionStartPoseState> _starts = new();
    private AnimancerState _transition;
    private LocomotionFootPhaseBinding[] _transitionFootBindings;
    private readonly LocomotionStopDistancePlayback _stopPlayback = new();

    public LocomotionSetRuntime(LocomotionSetAsset asset) : this(asset, LocomotionBoundSet.Capture(asset)) { }

    private LocomotionSetRuntime(LocomotionSetAsset asset, LocomotionBoundSet definition) : base(asset, definition.Move)
    {
        _definition = definition;
        _machine = new LocomotionSetStateMachine(definition.DecisionConfig);
    }
    public LocomotionSetState AnimationState => _machine.State;

    public override void ResetAnimation()
    {
        base.ResetAnimation();
        _machine.Reset();
        _transition = null;
        _stopPlayback.Begin(null);
        _transitionFootBindings = null;
        foreach (var state in _starts.Values) state.ResetSession();
    }

    public override LocomotionAnimationRequest UpdateAnimation(in LocomotionRuntimeAnimationContext context)
    {
        if (!IsEntered || !LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f)
            return default;

        if (LocomotionAnimationUtility.WasGraphDestroyed(_transition))
            ResetAnimation();
        LocomotionAnimationRequest move = MoveRequest(context, _definition.BlendDuration);
        if (move.State == null)
        {
            _machine.Reset();
            _transition = null;
            return move;
        }

        bool completed = _transition != null && LocomotionAnimationUtility.EvaluatedTime(_transition) >= _transition.Length;
        LocomotionSetState next = _machine.DecideNextState(context, completed);
        if (next == LocomotionSetState.Start && _transition is LocomotionStartPoseState currentStart
            && !currentStart.CanContinue(context))
            next = LocomotionSetState.Move;
        float? distance = PredictStopDistance(context);
        AnimancerState prepared = _transition;
        if (next != _machine.State)
        {
            LocomotionBoundTransition? sample = next == LocomotionSetState.Move ? null : SelectTransition(next, context, distance);
            prepared = sample.HasValue ? GetTransitionState(sample.Value) : null;
            AnimancerState leaf = prepared is LocomotionStartPoseState startPose ? startPose.Pose : prepared;
            _transitionFootBindings = prepared != null
                ? new[] { new LocomotionFootPhaseBinding(leaf, sample.Value.FootPhase) } : null;
            _stopPlayback.Begin(next == LocomotionSetState.Stop && distance.HasValue && sample.HasValue
                && _definition.StopMode == LocomotionStopPlaybackMode.Distance ? sample.Value.StopCurve : null);
            if (prepared == null)
                next = LocomotionSetState.Move;
        }
        bool entered = _machine.CommitState(next);
        _transition = next == LocomotionSetState.Move ? null : prepared;
        if (entered && _transition is LocomotionStartPoseState starting) starting.Begin(context);
        // Outgoing starts retain their own correction during fades, including Stop/Action interruption.
        foreach (var state in _starts.Values)
            if (!LocomotionAnimationUtility.WasGraphDestroyed(state)) state.Advance(context);
        if (next == LocomotionSetState.Move)
            return move;

        return new LocomotionAnimationRequest(_transition, _definition.BlendDuration,
            entered, idleClip: move.IdleClip,
            sampleTime: _transition is LocomotionStartPoseState aligned ? aligned.SampleTime
                : next == LocomotionSetState.Stop ? _stopPlayback.Sample(distance) : null,
            footPhaseBindings: _transitionFootBindings);
    }

    protected override void OnDispose()
    {
        foreach (ClipState state in _clips.Values)
            LocomotionAnimationUtility.Destroy(state);
        _clips.Clear();
        foreach (var state in _starts.Values) LocomotionAnimationUtility.Destroy(state);
        _starts.Clear();
        _transition = null;
        _transitionFootBindings = null;
        _stopPlayback.Begin(null);
        base.OnDispose();
    }

    private AnimancerState GetTransitionState(in LocomotionBoundTransition sample)
    {
        if (sample.StartYaw == null) return GetClipState(sample.Clip);
        if (_starts.TryGetValue(sample, out var state) && !LocomotionAnimationUtility.WasGraphDestroyed(state))
            return state;
        LocomotionAnimationUtility.Destroy(state);
        state = new LocomotionStartPoseState(sample.Clip, sample.StartYaw, sample.SignedTurnAngleDegrees);
        _starts[sample] = state;
        return state;
    }

    private ClipState GetClipState(AnimationClip clip)
    {
        if (clip == null)
            return null;
        if (_clips.TryGetValue(clip, out ClipState state)
            && !LocomotionAnimationUtility.WasGraphDestroyed(state))
            return state;
        LocomotionAnimationUtility.Destroy(state);
        state = new ClipState(clip);
        _clips[clip] = state;
        return state;
    }

    private float? PredictStopDistance(in LocomotionRuntimeAnimationContext context)
    {
        float scale = context.Motor.MotionState.LocomotionScale
            * (context.Motor.IsGrounded ? 1f : context.Motor.MotionState.AirLocomotionScale);
        float speed = Vector3.ProjectOnPlane(context.ModelVelocity, context.Motor.CharacterUp).magnitude;
        return LocomotionRunner.TryPredictStoppingDistance(speed, MovementConfig.Deceleration, scale,
            out float distance) ? distance : null;
    }

    private LocomotionBoundTransition? SelectTransition(LocomotionSetState next,
        in LocomotionRuntimeAnimationContext context, float? distance)
    {
        if (next == LocomotionSetState.Pivot)
            return SelectPivot(context);

        Vector2 source = context.ToLocalDirection(context.VelocityBeforeMotion);
        Vector2 target = context.ToLocalDirection(context.Intent.WorldMoveDirection);
        LocomotionBoundTransition? best = null;
        float bestScore = float.PositiveInfinity, bestPhaseScore = float.PositiveInfinity;
        int bestSidePenalty = int.MaxValue;
        float currentPhase = context.SourceFootPhase.Phase;
        bool hasPhase = context.SourceFootPhase.IsKnown;
        var samples = _definition.GetTransitions(next);
        for (int i = 0; i < samples.Count; i++)
        {
            LocomotionBoundTransition sample = samples[i];
            if (sample.StartYaw != null && !LocomotionStartAlignment.IsTurnToMove(context)) continue;
            int sidePenalty = sample.StartYaw != null
                ? LocomotionStartAlignment.TurnSidePenalty(sample.SignedTurnAngleDegrees, context.FacingYawDelta) : 0;
            float score = next == LocomotionSetState.Start ? Vector2.Angle(target, sample.Target)
                : Vector2.Angle(source, sample.Source);
            float phaseScore = float.PositiveInfinity;
            if (next == LocomotionSetState.Stop && hasPhase && sample.FootPhase != null)
            {
                float time = _definition.StopMode == LocomotionStopPlaybackMode.Distance
                    && distance.HasValue && sample.StopCurve != null
                    ? sample.StopCurve.TimeAtDistance(distance.Value) : 0f;
                if (sample.FootPhase.TrySample(time, out float phase))
                    phaseScore = AnimationFootPhaseTrack.Difference(currentPhase, phase);
            }
            // Direction first; Start turn side / Stop foot phase break equal-direction ties, then authored order.
            if (score < bestScore - 0.0001f || (Mathf.Abs(score - bestScore) <= 0.0001f
                && (sidePenalty < bestSidePenalty || (sidePenalty == bestSidePenalty && phaseScore < bestPhaseScore))))
            { bestScore = score; bestSidePenalty = sidePenalty; bestPhaseScore = phaseScore; best = sample; }
        }
        return best;
    }

    private LocomotionBoundTransition? SelectPivot(in LocomotionRuntimeAnimationContext context)
    {
        // A movement reversal while strafing is not necessarily a body turn.
        if (!LocomotionDataValidation.IsFinite(context.FacingYawDelta)
            || Mathf.Abs(context.FacingYawDelta) <= 0.0001f)
            return null;
        Vector3 up = context.Motor.CharacterUp;
        Vector3 facing = Vector3.ProjectOnPlane(context.Motor.CurrentWorldRotation * Vector3.forward, up);
        Vector3 target = Vector3.ProjectOnPlane(context.Intent.FacingDirection, up);
        if (!LocomotionAnimationUtility.IsValidPlanarDirection(target))
            return null;
        float angle = Vector3.Angle(facing, target);
        // The same minimum angle applies to movement reversal AND the required body turn.
        // A small lock-on facing correction must not turn a strafe reversal into a 180 Pivot.
        if (!LocomotionDataValidation.IsFinite(angle) || angle <= 0.0001f
            || angle < _definition.DecisionConfig.PivotMinimumAngleDegrees)
            return null;

        LocomotionBoundTransition? best = null;
        float bestScore = float.PositiveInfinity;
        var samples = _definition.GetTransitions(LocomotionSetState.Pivot);
        for (int i = 0; i < samples.Count; i++)
        {
            LocomotionBoundTransition sample = samples[i];
            if (Mathf.Sign(sample.SignedTurnAngleDegrees) != Mathf.Sign(context.FacingYawDelta))
                continue;
            // Compare magnitudes only AFTER choosing the Motor's side. Never wrap -180 to +180.
            float score = Mathf.Abs(Mathf.Abs(sample.SignedTurnAngleDegrees) - angle);
            if (score < bestScore - 0.0001f)
            { bestScore = score; best = sample; }
        }
        // No same-side clip means Move, not an animation turning against the Motor.
        return best;
    }

}
