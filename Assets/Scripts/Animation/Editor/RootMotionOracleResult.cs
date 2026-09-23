#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RootMotionOracleResult
{
    private readonly float[] _sampleTimes;
    private readonly Vector3[] _cumulativePositions;
    private readonly Quaternion[] _cumulativeRotations;

    public AnimationClip SourceClip { get; }
    public RootMotionBakeSettings Settings { get; }
    public int SampleCount => _sampleTimes.Length;
    public IReadOnlyList<float> SampleTimes => _sampleTimes;
    public IReadOnlyList<Vector3> CumulativePositions => _cumulativePositions;
    public IReadOnlyList<Quaternion> CumulativeRotations => _cumulativeRotations;

    internal RootMotionOracleResult(
        AnimationClip sourceClip,
        RootMotionBakeSettings settings,
        List<float> sampleTimes,
        List<Vector3> cumulativePositions,
        List<Quaternion> cumulativeRotations)
    {
        SourceClip = sourceClip;
        Settings = settings;
        _sampleTimes = sampleTimes != null ? sampleTimes.ToArray() : Array.Empty<float>();
        _cumulativePositions = cumulativePositions != null ? cumulativePositions.ToArray() : Array.Empty<Vector3>();
        _cumulativeRotations = cumulativeRotations != null ? cumulativeRotations.ToArray() : Array.Empty<Quaternion>();
    }
}
#endif
