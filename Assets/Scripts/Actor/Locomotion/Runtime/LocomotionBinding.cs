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
    internal LocomotionMove1DParameter Parameter1D { get; }
    internal LocomotionMove2DParameter Parameter2D { get; }
    internal AnimationClip IdleClip { get; }
    internal int SampleCount => _samples.Length;
    internal LocomotionBoundMoveSample GetSample(int index) => _samples[index];
    internal bool IsVertical => BlendType == LocomotionMoveBlendType.OneDimensional
        && Parameter1D == LocomotionMove1DParameter.VerticalSpeed;

    private LocomotionBoundMove(LocomotionMoveBlendType blendType, LocomotionMove1DParameter parameter1D,
        LocomotionMove2DParameter parameter2D, AnimationClip idleClip, LocomotionBoundMoveSample[] samples)
    {
        BlendType = blendType;
        Parameter1D = parameter1D;
        Parameter2D = parameter2D;
        IdleClip = idleClip;
        _samples = samples;
    }

    internal static LocomotionBoundMove Capture(LocomotionAsset asset, LocomotionBindingBuilder builder)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        AnimationClip idleClip = null;
        var definition = asset.Move;
        var blendType = definition?.BlendType ?? LocomotionMoveBlendType.OneDimensional;
        var parameter1D = definition?.OneDimensional?.Parameter ?? LocomotionMove1DParameter.HorizontalSpeed;
        var parameter2D = definition?.TwoDimensional?.Parameter ?? LocomotionMove2DParameter.LocalVelocity;
        var samples = new List<LocomotionBoundMoveSample>();
        bool vertical = blendType == LocomotionMoveBlendType.OneDimensional
            && parameter1D == LocomotionMove1DParameter.VerticalSpeed;
        if (blendType == LocomotionMoveBlendType.OneDimensional
            && Enum.IsDefined(typeof(LocomotionMove1DParameter), parameter1D)
            && definition?.OneDimensional?.Samples != null)
        {
            foreach (var sample in definition.OneDimensional.Samples)
            {
                if (sample == null || !LocomotionAnimationUtility.IsUsableClip(sample.Animation?.Clip)
                    || !LocomotionDataValidation.IsFinite(sample.Threshold)) continue;
                if (samples.Exists(existing => Mathf.Abs(existing.Threshold.x - sample.Threshold)
                    <= LocomotionDataValidation.ThresholdEpsilon)) continue;
                bool idle = !vertical && Mathf.Abs(sample.Threshold) <= LocomotionDataValidation.ThresholdEpsilon;
                var animation = builder.BindAnimation(sample.Animation);
                samples.Add(new LocomotionBoundMoveSample(animation, new Vector2(sample.Threshold, 0f), sample.Sync, idle));
                if (idle && idleClip == null) idleClip = animation.Clip;
            }
            samples.Sort((left, right) => left.Threshold.x.CompareTo(right.Threshold.x));
        }
        else if (blendType == LocomotionMoveBlendType.TwoDimensional
            && Enum.IsDefined(typeof(LocomotionMove2DParameter), parameter2D)
            && definition?.TwoDimensional?.Samples != null)
        {
            foreach (var sample in definition.TwoDimensional.Samples)
            {
                if (sample == null || !LocomotionAnimationUtility.IsUsableClip(sample.Animation?.Clip)
                    || !LocomotionDataValidation.IsFinite(sample.Threshold)
                    || !LocomotionDataValidation.IsFinite(sample.Threshold.sqrMagnitude)) continue;
                if (samples.Exists(existing => (existing.Threshold - sample.Threshold).sqrMagnitude
                    <= LocomotionDataValidation.ThresholdEpsilon * LocomotionDataValidation.ThresholdEpsilon)) continue;
                bool idle = sample.Threshold.sqrMagnitude <= LocomotionDataValidation.ThresholdEpsilon * LocomotionDataValidation.ThresholdEpsilon;
                var animation = builder.BindAnimation(sample.Animation);
                samples.Add(new LocomotionBoundMoveSample(animation, sample.Threshold, sample.Sync, idle));
                if (idle && idleClip == null) idleClip = animation.Clip;
            }
        }
        return new LocomotionBoundMove(blendType, parameter1D, parameter2D, idleClip, samples.ToArray());
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
                entry?.SourceLocalDirection ?? Vector2.zero, entry?.TargetLocalDirection ?? Vector2.zero);
        }
        start = BindUsableTransitions(start);
        stop = BindUsableTransitions(stop);
        pivot = BindUsableTransitions(pivot);
        return new LocomotionBoundSet(asset, move, start, stop, pivot);
    }

    private static LocomotionBoundTransition[] BindUsableTransitions(LocomotionBoundTransition[] samples) =>
        Array.FindAll(samples, sample => LocomotionAnimationUtility.IsUsableClip(sample.Clip)
            && LocomotionDataValidation.IsFinite(sample.Source)
            && LocomotionDataValidation.IsFinite(sample.Target));

}

internal readonly struct LocomotionBoundTransition
{
    internal LocomotionBoundTransition(LocomotionBoundAnimation animation, Vector2 source, Vector2 target)
    {
        Clip = animation.Clip;
        StopCurve = animation.StopCurve;
        FootPhase = animation.FootPhase;
        Source = source;
        Target = target;
    }

    internal AnimationStopDistanceCurve StopCurve { get; }
    internal AnimationFootPhaseTrack FootPhase { get; }
    internal AnimationClip Clip { get; }
    internal Vector2 Source { get; }
    internal Vector2 Target { get; }
}
