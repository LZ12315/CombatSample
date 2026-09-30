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
        if (!LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f)
        { Reset(); return false; }
        var motor = context.Motor;
        bool continuous = _hasPrevious && _ground == motor.GroundState && _action == context.ActionOwnerId
            && SamePolicy(_policy, motor.MotionState);
        _hasPrevious = true;
        _policy = motor.MotionState;
        _ground = motor.GroundState;
        _action = context.ActionOwnerId;
        var result = motor.PreviousResult;
        if (!continuous || context.ActionOwnerId != 0 || !IsAutonomousResult(result)
            || !SamePolicy(result.MotionState, motor.MotionState)
            || result.WasGroundedAtStart != motor.IsGrounded
            || result.IsGroundedAfterSolve != motor.IsGrounded) return false;
        speed = Vector3.ProjectOnPlane(result.ActualSolvedVelocity, motor.CharacterUp).magnitude
            / result.MotionState.MovementTimeScale;
        return LocomotionDataValidation.IsFinite(speed);
    }

    internal static bool IsAutonomousResult(in MotorMotionResult result) => result.IsValid
        && result.HorizontalSource == HorizontalMotionSource.Locomotion
        && !result.MotionState.HasHorizontalVelocityOwner && !result.MotionState.HasTrajectoryRootMotionOwner
        && !result.HadSignificantHorizontalImpulse && !result.HadPlatformCarry && !result.HadActorSeparation
        && !result.HasUnknownExternalDisplacement && result.MotionState.MovementTimeScale > 0f;

    public static bool SamePolicy(in MotionStateSnapshot a, in MotionStateSnapshot b) =>
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

/// <summary>Per-Actor Move speed matching. Animancer owns mixer weights and basic child synchronization.</summary>
public sealed class LocomotionMovePlayback
{
    private readonly ManualMixerState _mixer;
    private readonly bool[] _sync;
    private readonly bool[] _idle;
    private readonly bool[] _hasTrajectory;
    private readonly float[] _durations;
    private readonly Vector3[] _cycleDisplacements;
    private readonly bool _vertical;
    private readonly Action<string> _report;
    private readonly LocomotionVelocityFeedback _feedback = new();

    public float PlayRate { get; private set; } = 1f;
    public float ReferenceSpeed { get; private set; }

    public LocomotionMovePlayback(ManualMixerState mixer, AnimationAsset[] animations, bool[] sync,
        bool[] idle, bool vertical, Action<string> report, string[] entryLabels = null)
        : this(mixer, animations, sync, idle, vertical, report, entryLabels, QueryTrajectory) { }

    internal delegate bool TrajectoryQuery(AnimationAsset animation, out RootMotionTrajectory trajectory, out string reason);
    private static bool QueryTrajectory(AnimationAsset animation, out RootMotionTrajectory trajectory, out string reason) =>
        animation.TryGetLocomotionTrajectory(out trajectory, out reason);

    internal LocomotionMovePlayback(ManualMixerState mixer, AnimationAsset[] animations, bool[] sync,
        bool[] idle, bool vertical, Action<string> report, string[] entryLabels, TrajectoryQuery queryTrajectory)
    {
        _mixer = mixer;
        _sync = sync;
        _idle = idle;
        _vertical = vertical;
        _report = report;
        int count = animations.Length;
        _hasTrajectory = new bool[count];
        _durations = new float[count];
        _cycleDisplacements = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            AnimationAsset animation = animations[i];
            _durations[i] = animation.Clip.length;
            if (idle[i] || vertical) continue;
            string label = entryLabels != null ? entryLabels[i] : $"Move[{i}]";
            if (!animation.Clip.isLooping)
            {
                _report?.Invoke($"{label} '{animation.name}': non-looping Move has no steady cycle speed. Retaining PlayRate 1 when weighted.");
                continue;
            }
            if (!queryTrajectory(animation, out RootMotionTrajectory trajectory, out string reason))
            {
                _report?.Invoke($"{label} '{animation.name}': {reason} Retaining PlayRate 1.");
                continue;
            }
            if (!trajectory.TryExtract(0f, _durations[i], out RootMotionTransform cycle))
            {
                _report?.Invoke($"{label} '{animation.name}': full-clip Root Motion query failed. Retaining PlayRate 1.");
                continue;
            }
            _cycleDisplacements[i] = cycle.Position;
            _hasTrajectory[i] = true;
        }
    }

    public void Reset()
    {
        _feedback.Reset();
        PlayRate = 1f;
        ReferenceSpeed = 0f;
        _mixer.Speed = 1f;
        for (int i = 0; i < _mixer.ChildCount; i++)
        {
            var child = _mixer.GetChild(i);
            child.Speed = 1f;
            if (child.Playable.IsValid()) child.Playable.SetSpeed(1f);
        }
    }

    public void SuspendFeedback() => _feedback.Reset();

    public void Prepare(in LocomotionRuntimeAnimationContext context)
    {
        if (!LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f)
        { SuspendFeedback(); return; }
        bool qualified = _feedback.TryGetSpeed(context, out float actualSpeed);
        if (_vertical) { PlayRate = 1f; _mixer.Speed = 1f; return; }

        // Animancer's synchronized children share a weighted normalized speed.
        float syncWeight = 0f, weightedFrequency = 0f;
        for (int i = 0; i < _sync.Length; i++)
        {
            if (!_sync[i]) continue;
            float weight = _mixer.GetChild(i).Weight;
            syncWeight += weight;
            weightedFrequency += weight / _durations[i];
        }
        float syncFrequency = syncWeight > LocomotionAnimationUtility.WeightEpsilon
            ? weightedFrequency / syncWeight : 0f;

        Vector3 reference = Vector3.zero;
        bool movingWeight = false, referenceValid = true;
        for (int i = 0; i < _idle.Length; i++)
        {
            float weight = _mixer.GetChild(i).Weight;
            if (_idle[i] || weight <= LocomotionAnimationUtility.WeightEpsilon) continue;
            movingWeight = true;
            if (!_hasTrajectory[i]) { referenceValid = false; continue; }
            float cyclesPerSecond = _sync[i] ? syncFrequency : 1f / _durations[i];
            reference += _cycleDisplacements[i] * (weight * cyclesPerSecond);
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
    }
}
