using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Construction-only deduplication. Execution never retains the builder or authoring lists.</summary>
internal sealed class LocomotionBindingBuilder
{
    private readonly Dictionary<AnimationAsset, LocomotionBoundAnimation> _animations = new();
    private readonly LocomotionMovePlayback.TrajectoryQuery _trajectoryQuery;

    internal LocomotionBindingBuilder(LocomotionMovePlayback.TrajectoryQuery trajectoryQuery = null) =>
        _trajectoryQuery = trajectoryQuery;

    internal LocomotionBoundAnimation BindAnimation(AnimationAsset asset)
    {
        if (asset == null) return default;
        if (!_animations.TryGetValue(asset, out var animation))
        {
            animation = new LocomotionBoundAnimation(asset, _trajectoryQuery);
            _animations.Add(asset, animation);
        }
        return animation;
    }
}

internal sealed class LocomotionBoundMove
{
    private readonly LocomotionBoundMoveSample[] _samples;
    internal LocomotionMoveBlendType BlendType { get; }
    internal LocomotionMoveParameter XParameter { get; }
    internal LocomotionMoveParameter YParameter { get; }
    internal LocomotionMove2DAlgorithm Algorithm { get; }
    internal LocomotionTurnLeanConfig TurnLean { get; }
    internal bool UsesLean => XParameter == LocomotionMoveParameter.TurnLean
        || (BlendType == LocomotionMoveBlendType.TwoDimensional && YParameter == LocomotionMoveParameter.TurnLean);
    internal AnimationClip IdleClip { get; }
    internal int SampleCount => _samples.Length;
    internal LocomotionBoundMoveSample GetSample(int index) => _samples[index];
    // Pose-only and any vertical-speed space have no horizontal stride reference.
    internal bool MatchesHorizontalSpeed => XParameter != LocomotionMoveParameter.VerticalSpeed
        && (BlendType != LocomotionMoveBlendType.TwoDimensional || YParameter != LocomotionMoveParameter.VerticalSpeed)
        && (LocomotionMoveParameters.IsHorizontalMovement(XParameter)
            || (BlendType == LocomotionMoveBlendType.TwoDimensional && LocomotionMoveParameters.IsHorizontalMovement(YParameter)));

    private LocomotionBoundMove(LocomotionMoveBlendType blendType, LocomotionMoveParameter xParameter,
        LocomotionMoveParameter yParameter, LocomotionMove2DAlgorithm algorithm, LocomotionTurnLeanConfig turnLean,
        AnimationClip idleClip, LocomotionBoundMoveSample[] samples)
    {
        BlendType = blendType;
        XParameter = xParameter;
        YParameter = yParameter;
        Algorithm = algorithm;
        TurnLean = turnLean;
        IdleClip = idleClip;
        _samples = samples;
    }

    private static bool IsIdleThreshold(Vector2 threshold, LocomotionMoveBlendType blendType,
        LocomotionMoveParameter x, LocomotionMoveParameter y)
    {
        bool horizontalX = LocomotionMoveParameters.IsHorizontalMovement(x);
        bool horizontalY = blendType == LocomotionMoveBlendType.TwoDimensional && LocomotionMoveParameters.IsHorizontalMovement(y);
        bool vertical = x == LocomotionMoveParameter.VerticalSpeed
            || (blendType == LocomotionMoveBlendType.TwoDimensional && y == LocomotionMoveParameter.VerticalSpeed);
        // A lone local component being zero does not imply stationary movement.
        // Require a magnitude/strength source or both components of the same vector.
        bool twoD = blendType == LocomotionMoveBlendType.TwoDimensional;
        bool hasStationaryReference = IsMagnitude(x) || (twoD && (IsMagnitude(y)
            || IsPair(x, y, LocomotionMoveParameter.LocalInputX, LocomotionMoveParameter.LocalInputZ)
            || IsPair(x, y, LocomotionMoveParameter.LocalVelocityX, LocomotionMoveParameter.LocalVelocityZ)));
        var movementThreshold = new Vector2(horizontalX ? threshold.x : 0f, horizontalY ? threshold.y : 0f);
        return !vertical && hasStationaryReference
            && movementThreshold.sqrMagnitude <= LocomotionDataValidation.ThresholdEpsilon * LocomotionDataValidation.ThresholdEpsilon;
    }

    private static bool IsMagnitude(LocomotionMoveParameter source) =>
        source == LocomotionMoveParameter.HorizontalSpeed || source == LocomotionMoveParameter.InputStrength;

    private static bool IsPair(LocomotionMoveParameter x, LocomotionMoveParameter y,
        LocomotionMoveParameter first, LocomotionMoveParameter second) =>
        (x == first && y == second) || (x == second && y == first);

    internal static LocomotionBoundMove Capture(LocomotionAsset asset, LocomotionBindingBuilder builder)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        AnimationClip idleClip = null;
        var definition = asset.Move;
        var blendType = definition?.BlendType ?? LocomotionMoveBlendType.OneDimensional;
        var x = blendType == LocomotionMoveBlendType.TwoDimensional
            ? definition?.TwoDimensional?.XParameter ?? LocomotionMoveParameter.LocalVelocityX
            : definition?.OneDimensional?.Parameter ?? LocomotionMoveParameter.HorizontalSpeed;
        var y = definition?.TwoDimensional?.YParameter ?? LocomotionMoveParameter.LocalVelocityZ;
        var algorithm = definition?.TwoDimensional?.Algorithm ?? LocomotionMove2DAlgorithm.Directional;
        var lean = definition?.TurnLean ?? LocomotionTurnLeanConfig.Default;
        var samples = new List<LocomotionBoundMoveSample>();
        bool usesLean = x == LocomotionMoveParameter.TurnLean
            || (blendType == LocomotionMoveBlendType.TwoDimensional && y == LocomotionMoveParameter.TurnLean);
        // Invalid visual settings disable only the lean source (Update returns zero), not Move.
        // Binding runs once per Runtime, so this never logs every animation tick or graph rebuild.
        if (usesLean && !lean.IsValid)
            Debug.LogWarning($"[Locomotion] Asset '{asset.name}': invalid Turn Lean settings. TurnLean will be zero; Move samples remain available. "
                + "Full Lean Acceleration must be finite and positive; Minimum Speed and Smooth Seconds must be finite and non-negative.", asset);
        bool valid = LocomotionMoveParameters.IsValid(x);
        if (blendType == LocomotionMoveBlendType.OneDimensional && valid
            && definition?.OneDimensional?.Samples != null)
        {
            foreach (var sample in definition.OneDimensional.Samples)
            {
                if (sample == null || !LocomotionAnimationUtility.IsUsableClip(sample.Animation?.Clip)
                    || !LocomotionDataValidation.IsFinite(sample.Threshold)) continue;
                if (samples.Exists(existing => Mathf.Abs(existing.Threshold.x - sample.Threshold)
                    <= LocomotionDataValidation.ThresholdEpsilon)) continue;
                bool idle = IsIdleThreshold(new Vector2(sample.Threshold, 0f), blendType, x, y);
                var animation = builder.BindAnimation(sample.Animation);
                samples.Add(new LocomotionBoundMoveSample(animation, new Vector2(sample.Threshold, 0f), sample.Sync, idle));
                if (idle && idleClip == null) idleClip = animation.Clip;
            }
            samples.Sort((left, right) => left.Threshold.x.CompareTo(right.Threshold.x));
        }
        else if (blendType == LocomotionMoveBlendType.TwoDimensional
            && valid && LocomotionMoveParameters.IsValid(y)
            && Enum.IsDefined(typeof(LocomotionMove2DAlgorithm), algorithm)
            && definition?.TwoDimensional?.Samples != null)
        {
            foreach (var sample in definition.TwoDimensional.Samples)
            {
                if (sample == null || !LocomotionAnimationUtility.IsUsableClip(sample.Animation?.Clip)
                    || !LocomotionDataValidation.IsFinite(sample.Threshold)
                    || !LocomotionDataValidation.IsFinite(sample.Threshold.sqrMagnitude)) continue;
                if (samples.Exists(existing => (existing.Threshold - sample.Threshold).sqrMagnitude
                    <= LocomotionDataValidation.ThresholdEpsilon * LocomotionDataValidation.ThresholdEpsilon)) continue;
                bool idle = IsIdleThreshold(sample.Threshold, blendType, x, y);
                var animation = builder.BindAnimation(sample.Animation);
                samples.Add(new LocomotionBoundMoveSample(animation, sample.Threshold, sample.Sync, idle));
                if (idle && idleClip == null) idleClip = animation.Clip;
            }
        }
        return new LocomotionBoundMove(blendType, x, y, algorithm, lean, idleClip, samples.ToArray());
    }
}

internal readonly struct LocomotionBoundMoveSample
{
    public readonly LocomotionBoundAnimation Animation;
    public readonly Vector2 Threshold;
    public readonly bool Sync, Idle;
    public LocomotionBoundMoveSample(LocomotionBoundAnimation animation, Vector2 threshold, bool sync, bool idle)
    { Animation = animation; Threshold = threshold; Sync = sync; Idle = idle; }
}

internal sealed class LocomotionBoundSet
{
    private readonly IReadOnlyList<LocomotionBoundTransition> _start, _stop, _pivot;
    internal LocomotionBoundMove Move { get; }
    internal float BlendDuration { get; }
    internal LocomotionStopPlaybackMode StopMode { get; }
    internal LocomotionTransitionDecisionConfig DecisionConfig { get; }

    private LocomotionBoundSet(LocomotionSetAsset asset, LocomotionBoundMove move,
        LocomotionBoundTransition[] start, LocomotionBoundTransition[] stop, LocomotionBoundTransition[] pivot)
    {
        Move = move;
        BlendDuration = asset.TransitionBlendDuration;
        StopMode = asset.StopPlaybackMode;
        DecisionConfig = asset.TransitionDecisionConfig;
        _start = Array.AsReadOnly(start);
        _stop = Array.AsReadOnly(stop);
        _pivot = Array.AsReadOnly(pivot);
    }

    internal IReadOnlyList<LocomotionBoundTransition> GetTransitions(LocomotionSetState state) =>
        state == LocomotionSetState.Start ? _start : state == LocomotionSetState.Stop ? _stop : _pivot;

    internal static LocomotionBoundSet Capture(LocomotionSetAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        var builder = new LocomotionBindingBuilder();
        var move = LocomotionBoundMove.Capture(asset, builder);
        var start = new LocomotionBoundTransition[asset.Start?.Count ?? 0];
        var stop = new LocomotionBoundTransition[asset.Stop?.Count ?? 0];
        var pivot = new LocomotionBoundTransition[asset.Pivot?.Count ?? 0];
        for (int i = 0; i < start.Length; i++)
        {
            LocomotionStartEntry entry = asset.Start[i];
            start[i] = new LocomotionBoundTransition(builder.BindAnimation(entry?.Animation), Vector2.up,
                entry?.TargetLocalDirection ?? Vector2.zero);
        }
        for (int i = 0; i < stop.Length; i++)
        {
            LocomotionStopEntry entry = asset.Stop[i];
            stop[i] = new LocomotionBoundTransition(builder.BindAnimation(entry?.Animation),
                entry?.SourceLocalDirection ?? Vector2.zero, Vector2.up);
        }
        for (int i = 0; i < pivot.Length; i++)
        {
            LocomotionPivotEntry entry = asset.Pivot[i];
            pivot[i] = new LocomotionBoundTransition(builder.BindAnimation(entry?.Animation),
                Vector2.zero, Vector2.zero, entry?.SignedTurnAngleDegrees ?? 0f);
        }
        start = BindUsableTransitions(start);
        stop = BindUsableTransitions(stop);
        pivot = Array.FindAll(BindUsableTransitions(pivot), sample =>
            LocomotionDataValidation.IsFinite(sample.SignedTurnAngleDegrees)
            && Mathf.Abs(sample.SignedTurnAngleDegrees) > 0f && Mathf.Abs(sample.SignedTurnAngleDegrees) <= 180f);
        return new LocomotionBoundSet(asset, move, start, stop, pivot);
    }

    private static LocomotionBoundTransition[] BindUsableTransitions(LocomotionBoundTransition[] samples) =>
        Array.FindAll(samples, sample => LocomotionAnimationUtility.IsUsableClip(sample.Clip)
            && LocomotionDataValidation.IsFinite(sample.Source)
            && LocomotionDataValidation.IsFinite(sample.Target));

}

internal readonly struct LocomotionBoundTransition
{
    internal LocomotionBoundTransition(LocomotionBoundAnimation animation, Vector2 source, Vector2 target,
        float signedTurnAngleDegrees = 0f)
    {
        Clip = animation.Clip;
        StopCurve = animation.StopCurve;
        FootPhase = animation.FootPhase;
        Source = source;
        Target = target;
        SignedTurnAngleDegrees = signedTurnAngleDegrees;
    }

    internal float SignedTurnAngleDegrees { get; }
    internal AnimationStopDistanceCurve StopCurve { get; }
    internal AnimationFootPhaseTrack FootPhase { get; }
    internal AnimationClip Clip { get; }
    internal Vector2 Source { get; }
    internal Vector2 Target { get; }
}
