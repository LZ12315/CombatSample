using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public sealed class LocomotionSetRuntime : LocomotionAnimationRuntime
{
    private readonly LocomotionBoundSet _definition;
    private readonly LocomotionSetStateMachine _machine;
    private readonly Dictionary<AnimationClip, ClipState> _clips = new();
    private ClipState _transition;
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
        float? distance = PredictStopDistance(context);
        ClipState prepared = _transition;
        if (next != _machine.State)
        {
            LocomotionBoundTransition? sample = next == LocomotionSetState.Move ? null : SelectTransition(next, context, distance);
            prepared = sample.HasValue ? GetClipState(sample.Value.Clip) : null;
            _transitionFootBindings = prepared != null
                ? new[] { new LocomotionFootPhaseBinding(prepared, sample.Value.FootPhase) } : null;
            _stopPlayback.Begin(next == LocomotionSetState.Stop && distance.HasValue && sample.HasValue
                && _definition.StopMode == LocomotionStopPlaybackMode.Distance ? sample.Value.StopCurve : null);
            if (prepared == null)
                next = LocomotionSetState.Move;
        }
        bool entered = _machine.CommitState(next);
        _transition = next == LocomotionSetState.Move ? null : prepared;
        if (next == LocomotionSetState.Move)
            return move;

        return new LocomotionAnimationRequest(_transition, _definition.BlendDuration,
            entered, idleClip: move.IdleClip,
            sampleTime: next == LocomotionSetState.Stop ? _stopPlayback.Sample(distance) : null,
            footPhaseBindings: _transitionFootBindings);
    }

    protected override void OnDispose()
    {
        foreach (ClipState state in _clips.Values)
            LocomotionAnimationUtility.Destroy(state);
        _clips.Clear();
        _transition = null;
        _transitionFootBindings = null;
        _stopPlayback.Begin(null);
        base.OnDispose();
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
        Vector2 source = context.ToLocalDirection(context.VelocityBeforeMotion);
        Vector2 target = context.ToLocalDirection(context.Intent.WorldMoveDirection);
        LocomotionBoundTransition? best = null;
        float bestScore = float.PositiveInfinity, bestPhaseScore = float.PositiveInfinity;
        float currentPhase = context.SourceFootPhase.Phase;
        bool hasPhase = context.SourceFootPhase.IsKnown;
        var samples = _definition.GetTransitions(next);
        for (int i = 0; i < samples.Count; i++)
        {
            LocomotionBoundTransition sample = samples[i];
            float score = next == LocomotionSetState.Start ? Vector2.Angle(target, sample.Target)
                : next == LocomotionSetState.Stop ? Vector2.Angle(source, sample.Source)
                : Vector2.Angle(source, sample.Source) + Vector2.Angle(target, sample.Target);
            float phaseScore = float.PositiveInfinity;
            if (next == LocomotionSetState.Stop && hasPhase && sample.FootPhase != null)
            {
                float time = _definition.StopMode == LocomotionStopPlaybackMode.Distance
                    && distance.HasValue && sample.StopCurve != null
                    ? sample.StopCurve.TimeAtDistance(distance.Value) : 0f;
                if (sample.FootPhase.TrySample(time, out float phase))
                    phaseScore = AnimationFootPhaseTrack.Difference(currentPhase, phase);
            }
            // Direction first, foot phase only among equally close directions, then authored order.
            if (score < bestScore - 0.0001f || (Mathf.Abs(score - bestScore) <= 0.0001f && phaseScore < bestPhaseScore))
            { bestScore = score; bestPhaseScore = phaseScore; best = sample; }
        }
        return best;
    }

}
