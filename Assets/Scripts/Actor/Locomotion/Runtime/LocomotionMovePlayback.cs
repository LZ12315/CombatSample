using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Per-Actor Move speed matching. Animancer owns mixer weights and basic child synchronization.</summary>
public sealed class LocomotionMovePlayback
{
    private readonly ManualMixerState _mixer;
    private readonly List<float> _previousChildWeights = new();
    private readonly bool[] _sync;
    private readonly bool[] _idle;
    private readonly bool[] _hasTrajectory;
    private readonly float[] _durations;
    private readonly Vector3[] _cycleDisplacements;
    private readonly bool _vertical;
    private readonly LocomotionVelocityFeedback _feedback = new();

    public float PlayRate { get; private set; } = 1f;
    public float ReferenceSpeed { get; private set; }

    public LocomotionMovePlayback(ManualMixerState mixer, AnimationAsset[] animations, bool[] sync,
        bool[] idle, bool vertical)
        : this(mixer, animations, sync, idle, vertical, null) { }

    internal delegate bool TrajectoryQuery(AnimationAsset animation, out RootMotionTrajectory trajectory, out string reason);
    internal LocomotionMovePlayback(ManualMixerState mixer, AnimationAsset[] animations, bool[] sync,
        bool[] idle, bool vertical, TrajectoryQuery queryTrajectory)
        : this(mixer, BindAnimations(animations, queryTrajectory), sync, idle, vertical) { }

    internal LocomotionMovePlayback(ManualMixerState mixer, LocomotionBoundAnimation[] animations, bool[] sync,
        bool[] idle, bool vertical)
    {
        _mixer = mixer;
        _sync = (bool[])sync.Clone();
        _idle = (bool[])idle.Clone();
        _vertical = vertical;
        int count = animations.Length;
        _hasTrajectory = new bool[count];
        _durations = new float[count];
        _cycleDisplacements = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            _durations[i] = animations[i].Duration;
            if (_idle[i] || vertical) continue;
            _hasTrajectory[i] = animations[i].HasCycleTrajectory;
            _cycleDisplacements[i] = animations[i].CycleDisplacement;
        }
    }

    private static LocomotionBoundAnimation[] BindAnimations(AnimationAsset[] animations, TrajectoryQuery query)
    {
        var bound = new LocomotionBoundAnimation[animations.Length];
        var builder = new LocomotionBindingBuilder(query);
        for (int i = 0; i < bound.Length; i++) bound[i] = builder.BindAnimation(animations[i]);
        return bound;
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

    internal void PrepareMove(Vector2 parameter, in LocomotionRuntimeAnimationContext context)
    {
        if (!LocomotionDataValidation.IsFinite(context.DeltaTime) || context.DeltaTime <= 0f)
        {
            SuspendFeedback();
            return;
        }
        ApplyParameter(_mixer, parameter, _previousChildWeights);
        Prepare(context);
    }

    internal static void ApplyParameter(AnimancerState state, Vector2 parameter, List<float> previousWeights)
    {
        previousWeights.Clear();
        for (int i = 0; i < state.ChildCount; i++)
            previousWeights.Add(state.GetChild(i).Weight);
        if (state is LinearMixerState linear)
        {
            linear.Parameter = parameter.x;
            linear.RecalculateWeights();
        }
        else if (state is DirectionalMixerState directional)
        {
            directional.Parameter = parameter;
            directional.RecalculateWeights();
        }
        for (int i = 0; i < state.ChildCount; i++)
        {
            AnimancerState child = state.GetChild(i);
            if (!child.IsLooping && previousWeights[i] <= LocomotionAnimationUtility.WeightEpsilon
                && child.Weight > LocomotionAnimationUtility.WeightEpsilon)
                child.TimeD = 0d;
        }
    }

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
        bool referenceValid = true;
        for (int i = 0; i < _idle.Length; i++)
        {
            float weight = _mixer.GetChild(i).Weight;
            if (_idle[i] || weight <= LocomotionAnimationUtility.WeightEpsilon) continue;
            if (!_hasTrajectory[i]) { referenceValid = false; continue; }
            float cyclesPerSecond = _sync[i] ? syncFrequency : 1f / _durations[i];
            reference += _cycleDisplacements[i] * (weight * cyclesPerSecond);
        }
        ReferenceSpeed = new Vector2(reference.x, reference.z).magnitude;
        if (!LocomotionDataValidation.IsFinite(ReferenceSpeed))
            referenceValid = false;
        PlayRate = qualified && referenceValid
            ? LocomotionPlayRateMatching.Update(PlayRate, actualSpeed, ReferenceSpeed, context.DeltaTime) : 1f;
        _mixer.Speed = PlayRate;
    }
}
