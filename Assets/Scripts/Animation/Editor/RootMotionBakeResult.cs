#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RootMotionBakeResult
{
    private readonly float[] _sampleTimes;
    private readonly Vector3[] _cumulativePositions;
    private readonly Quaternion[] _cumulativeRotations;

    public AnimationClip SourceClip { get; }
    public AnimationConfig AnimationConfig { get; }
    public RootMotionBakeSettings Settings { get; }
    public int BakerVersion { get; }
    public int SampleRate { get; }
    public float Duration { get; }
    public int SampleCount => _sampleTimes.Length;
    public IReadOnlyList<float> SampleTimes => _sampleTimes;
    public IReadOnlyList<Vector3> CumulativePositions => _cumulativePositions;
    public IReadOnlyList<Quaternion> CumulativeRotations => _cumulativeRotations;

    internal RootMotionBakeResult(
        AnimationClip sourceClip,
        AnimationConfig animationConfig,
        int bakerVersion,
        int sampleRate,
        float duration,
        List<float> sampleTimes,
        List<Vector3> cumulativePositions,
        List<Quaternion> cumulativeRotations)
        : this(
            sourceClip,
            RootMotionBakeSettings.FromLegacy(animationConfig),
            animationConfig,
            bakerVersion,
            sampleRate,
            duration,
            sampleTimes,
            cumulativePositions,
            cumulativeRotations)
    {
    }

    internal RootMotionBakeResult(
        AnimationClip sourceClip,
        RootMotionBakeSettings settings,
        int bakerVersion,
        int sampleRate,
        float duration,
        List<float> sampleTimes,
        List<Vector3> cumulativePositions,
        List<Quaternion> cumulativeRotations)
        : this(
            sourceClip,
            settings,
            null,
            bakerVersion,
            sampleRate,
            duration,
            sampleTimes,
            cumulativePositions,
            cumulativeRotations)
    {
    }

    private RootMotionBakeResult(
        AnimationClip sourceClip,
        RootMotionBakeSettings settings,
        AnimationConfig animationConfig,
        int bakerVersion,
        int sampleRate,
        float duration,
        List<float> sampleTimes,
        List<Vector3> cumulativePositions,
        List<Quaternion> cumulativeRotations)
    {
        SourceClip = sourceClip;
        AnimationConfig = animationConfig;
        Settings = settings;
        BakerVersion = bakerVersion;
        SampleRate = sampleRate;
        Duration = duration;
        _sampleTimes = sampleTimes != null ? sampleTimes.ToArray() : Array.Empty<float>();
        _cumulativePositions = cumulativePositions != null ? cumulativePositions.ToArray() : Array.Empty<Vector3>();
        _cumulativeRotations = cumulativeRotations != null ? cumulativeRotations.ToArray() : Array.Empty<Quaternion>();
    }

    public void CopySamples(out float[] sampleTimes, out Vector3[] cumulativePositions, out Quaternion[] cumulativeRotations)
    {
        sampleTimes = (float[])_sampleTimes.Clone();
        cumulativePositions = (Vector3[])_cumulativePositions.Clone();
        cumulativeRotations = (Quaternion[])_cumulativeRotations.Clone();
    }
}
#endif
