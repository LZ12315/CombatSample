using System;
using Animancer;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Published World feedback is useful only for the same uninterrupted locomotion context.</summary>
public sealed class LocomotionVelocityFeedback
{
    private bool _hasPrevious;
    private MotionStateSnapshot _policy;
    private ActorGroundState _ground;
    private int _action;
    public void Reset() => _hasPrevious = false;

    public bool TryGetSpeed(in LocomotionRuntimeAnimationContext context, out float speed)
    {
        speed = 0f;
        if (!LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f) { Reset(); return false; }
        var motor = context.Motor;
        bool continuous = _hasPrevious && _ground == motor.GroundState && _action == context.ActionOwnerId
            && SamePolicy(_policy, motor.MotionState);
        _hasPrevious = true;
        _policy = motor.MotionState;
        _ground = motor.GroundState;
        _action = context.ActionOwnerId;
        var result = motor.PreviousResult;
        if (!continuous || context.ActionOwnerId != 0 || !result.IsValid
            || result.HorizontalSource != HorizontalMotionSource.Locomotion
            || !SamePolicy(result.MotionState, motor.MotionState)
            || result.MotionState.HasHorizontalVelocityOwner || result.MotionState.HasTrajectoryRootMotionOwner
            || result.HadSignificantHorizontalImpulse || result.HadPlatformCarry || result.HadActorSeparation
            || result.HasUnknownExternalDisplacement || result.WasGroundedAtStart != motor.IsGrounded
            || result.IsGroundedAfterSolve != motor.IsGrounded
            || result.MotionState.MovementTimeScale <= 0f) return false;
        speed = Vector3.ProjectOnPlane(result.ActualSolvedVelocity, motor.CharacterUp).magnitude
            / result.MotionState.MovementTimeScale;
        return LocomotionDataValidation.IsFinite(speed);
    }

    private static bool SamePolicy(in MotionStateSnapshot a, in MotionStateSnapshot b) =>
        a.LocomotionScale == b.LocomotionScale && a.AirLocomotionScale == b.AirLocomotionScale
        && a.GravityScale == b.GravityScale && a.MovementTimeScale == b.MovementTimeScale
        && a.HasHorizontalVelocityOwner == b.HasHorizontalVelocityOwner
        && a.HasTrajectoryRootMotionOwner == b.HasTrajectoryRootMotionOwner
        && a.HasRootRotationOwner == b.HasRootRotationOwner && a.HasScriptedRotationOwner == b.HasScriptedRotationOwner;
}

public static class LocomotionPlayRateMatching
{
    public const float MinimumRate = 0.5f;
    public const float MaximumRate = 1.5f;
    public const float SmoothSeconds = 0.1f;
    public const float MinimumReferenceSpeed = 0.1f;
    public static float Update(float previous, float actualSpeed, float referenceSpeed, float dt)
    {
        if (!LocomotionDataValidation.IsFinite(dt) || dt <= 0f) return previous;
        if (!LocomotionDataValidation.IsFinite(referenceSpeed) || referenceSpeed < MinimumReferenceSpeed
            || !LocomotionDataValidation.IsFinite(actualSpeed) || actualSpeed < 0f) return 1f;
        float target = Mathf.Clamp(actualSpeed / referenceSpeed, MinimumRate, MaximumRate);
        return Mathf.Lerp(previous, target, 1f - Mathf.Exp(-dt / SmoothSeconds));
    }
}

/// <summary>Runtime-owned cache. ActorAnimation prepares it after applying the current mixer parameter.</summary>
public sealed class LocomotionMovePlayback
{
    private readonly ManualMixerState _mixer;
    private readonly AnimationAsset[] _animations;
    private readonly bool[] _idle;
    private readonly LocomotionCycleMapping[] _cycles;
    private readonly RootMotionTrajectory[] _trajectories;
    private readonly string[] _cycleIssues;
    private readonly string[] _trajectoryIssues;
    private readonly string[] _queryIssues;
    private readonly Action<string> _report;
    private readonly bool _vertical;
    private readonly LocomotionVelocityFeedback _feedback = new LocomotionVelocityFeedback();
    private readonly double[] _sampleStarts;
    private readonly double[] _sampleEnds;
    private bool _groupValid = true;
    private int _members;
    private double _phase;
    private float _frequency;

    public bool SemanticSyncActive { get; private set; }
    public double Phase => _phase;
    public float PlayRate { get; private set; } = 1f;
    public float ReferenceSpeed { get; private set; }

    public LocomotionMovePlayback(ManualMixerState mixer, AnimationAsset[] animations, bool[] sync,
        bool[] idle, bool vertical, Action<string> report, string[] entryLabels = null)
    {
        _mixer = mixer;
        _animations = animations;
        _idle = idle;
        _vertical = vertical;
        _report = report;
        int count = animations.Length;
        _cycles = new LocomotionCycleMapping[count];
        _trajectories = new RootMotionTrajectory[count];
        _cycleIssues = new string[count];
        _trajectoryIssues = new string[count];
        _queryIssues = new string[count];
        _sampleStarts = new double[count];
        _sampleEnds = new double[count];
        for (int i = 0; i < count; i++)
        {
            AnimationAsset animation = animations[i];
            string label = entryLabels != null ? entryLabels[i] : $"Move[{i}]";
            _queryIssues[i] = $"{label} '{animation.name}': trajectory reference query failed; retaining PlayRate 1.";
            if (sync[i] && !idle[i] && !vertical)
            {
                _members++;
                string reason = "Locomotion metadata is missing or stale.";
                if (!animation.IsLocomotionDataCurrent
                    || !animation.LocomotionData.TryGetCycle(animation.Clip, out _cycles[i], out reason))
                {
                    _groupValid = false;
                    _cycleIssues[i] = $"{label} '{animation.name}': {reason} Whole group retains basic synchronization.";
                }
            }
            if (idle[i] || vertical) continue;
            RootMotionTrajectory trajectory = animation.RootMotionData;
            if (trajectory == null || trajectory.SourceClip != animation.Clip
                || Mathf.Abs(trajectory.Duration - animation.Clip.length) > 0.001f || !trajectory.ValidateData().IsValid)
                _trajectoryIssues[i] = $"{label} '{animation.name}': valid source-matched Root Motion trajectory is required for speed matching.";
            else _trajectories[i] = trajectory;
        }
    }

    public void Reset()
    {
        _feedback.Reset();
        _phase = 0d;
        _frequency = 0f;
        PlayRate = 1f;
        ReferenceSpeed = 0f;
        SemanticSyncActive = false;
        for (int i = 0; i < _mixer.ChildCount; i++)
        {
            var child = _mixer.GetChild(i);
            child.Speed = 1f;
            if (child.Playable.IsValid()) child.Playable.SetSpeed(1f);
        }
    }

    public void SuspendFeedback() => _feedback.Reset();

    public void Prepare(in LocomotionRuntimeAnimationContext context, float? entryPhase)
    {
        if (!LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f) { SuspendFeedback(); return; }
        bool qualified = _feedback.TryGetSpeed(context, out float actualSpeed);
        if (_vertical) { PlayRate = 1f; _mixer.Speed = 1f; return; }
        SemanticSyncActive = _members > 0 && _groupValid;
        if (context.Motor.IsGrounded)
            for (int i = 0; i < _cycleIssues.Length; i++)
                if (_cycleIssues[i] != null) _report?.Invoke(_cycleIssues[i]);
        if (SemanticSyncActive && entryPhase.HasValue) _phase = Mathf.Repeat(entryPhase.Value, 1f);
        float totalWeight = 0f, frequency = 0f;
        if (SemanticSyncActive)
        {
            for (int i = 0; i < _cycles.Length; i++)
            {
                if (_cycles[i] == null) continue;
                float weight = _mixer.GetChild(i).Weight;
                totalWeight += weight;
                frequency += weight / _cycles[i].Duration;
            }
            frequency = totalWeight > LocomotionAnimationUtility.WeightEpsilon ? frequency / totalWeight : 0f;
        }

        double nextNeutralPhase = _phase + frequency * context.DeltaTime;
        Vector3 reference = Vector3.zero;
        bool movingWeight = false;
        bool referenceValid = _groupValid || _members == 0;
        for (int i = 0; i < _animations.Length; i++)
        {
            AnimancerState child = _mixer.GetChild(i);
            _sampleStarts[i] = SemanticSyncActive && _cycles[i] != null ? _cycles[i].PhaseToTime(_phase) : child.TimeD;
            _sampleEnds[i] = SemanticSyncActive && _cycles[i] != null
                ? _cycles[i].PhaseToTime(nextNeutralPhase) : child.TimeD + context.DeltaTime;
            if (_idle[i] || child.Weight <= LocomotionAnimationUtility.WeightEpsilon) continue;
            movingWeight = true;
            if (_trajectories[i] == null)
            { referenceValid = false; _report?.Invoke(_trajectoryIssues[i]); continue; }
            RootMotionTransform delta;
            bool valid = child.IsLooping
                ? _trajectories[i].TryExtractLooping(_sampleStarts[i], _sampleEnds[i], out delta)
                : _trajectories[i].TryExtract((float)_sampleStarts[i], (float)_sampleEnds[i], out delta);
            if (!valid) { referenceValid = false; _report?.Invoke(_queryIssues[i]); continue; }
            reference += delta.Position * (child.Weight / context.DeltaTime);
        }
        ReferenceSpeed = new Vector2(reference.x, reference.z).magnitude;
        if (!LocomotionDataValidation.IsFinite(ReferenceSpeed))
        {
            referenceValid = false;
            _report?.Invoke("Move group produced a non-finite reference speed; retaining PlayRate 1.");
        }
        if (movingWeight && referenceValid && ReferenceSpeed < LocomotionPlayRateMatching.MinimumReferenceSpeed
            && context.PolicyVelocity.magnitude > 0.1f && context.ActionOwnerId == 0)
            _report?.Invoke("Move reference speed is below 0.1m/s; retaining PlayRate 1. Check in-place clips and trajectory data.");
        PlayRate = qualified && referenceValid
            ? LocomotionPlayRateMatching.Update(PlayRate, actualSpeed, ReferenceSpeed, context.DeltaTime) : 1f;
        _mixer.Speed = PlayRate;

        _frequency = frequency;
    }

    /// <summary>Called by the state in Animancer's pre-update; zero graph dt never advances phase.</summary>
    public void UpdateSemanticSync(float graphDeltaTime, float effectiveSpeed)
    {
        if (!SemanticSyncActive || graphDeltaTime <= 0f || effectiveSpeed <= 0f || !_mixer.IsPlaying) return;
        double nextPhase = _phase + _frequency * effectiveSpeed * graphDeltaTime;
        for (int i = 0; i < _cycles.Length; i++)
        {
            if (_cycles[i] == null) continue;
            AnimancerState child = _mixer.GetChild(i);
            double time = _cycles[i].PhaseToTime(_phase);
            double end = _cycles[i].PhaseToTime(nextPhase);
            child.TimeD = time;
            child.Speed = (float)((end - time) / (graphDeltaTime * effectiveSpeed));
            // Built-in synchronization changes the native speed without changing child.Speed.
            child.Playable.SetSpeed(child.Speed);
        }
        _phase = nextPhase;
    }
}

// We own only the synchronization hook; Animancer continues to calculate 1D/2D weights.
public sealed class LocomotionLinearMixerState : LinearMixerState
{
    public LocomotionMovePlayback Playback { get; set; }
    public override void Update()
    {
        if (Playback?.SemanticSyncActive != true) { base.Update(); return; }
        RecalculateWeights();
        Playback.UpdateSemanticSync(AnimancerGraph.DeltaTime, CalculateRealEffectiveSpeed());
    }
}

public sealed class LocomotionDirectionalMixerState : DirectionalMixerState
{
    public LocomotionMovePlayback Playback { get; set; }
    public override void Update()
    {
        if (Playback?.SemanticSyncActive != true) { base.Update(); return; }
        RecalculateWeights();
        Playback.UpdateSemanticSync(AnimancerGraph.DeltaTime, CalculateRealEffectiveSpeed());
    }
}
