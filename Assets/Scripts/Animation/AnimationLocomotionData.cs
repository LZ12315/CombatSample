using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnimationFoot { Left, Right }

[Serializable]
public struct AnimationFootMarker
{
    [SerializeField] private float time;
    [SerializeField] private AnimationFoot foot;
    public float Time => time;
    public AnimationFoot Foot => foot;
    public AnimationFootMarker(float time, AnimationFoot foot) { this.time = time; this.foot = foot; }
}

/// <summary>Source-bound automatic results. Author overrides live separately on AnimationAsset.</summary>
[Serializable]
public sealed class AnimationLocomotionData
{
    [SerializeField] private AnimationClip sourceClip;
    [SerializeField] private string trajectoryHash;
    [SerializeField] private string footSetup;
    [SerializeField] private float stopTime;
    [SerializeField] private AnimationFootMarker[] footMarkers = Array.Empty<AnimationFootMarker>();
    public AnimationClip SourceClip => sourceClip;
    public string TrajectoryHash => trajectoryHash;
    public string FootSetup => footSetup;
    public float StopTime => stopTime;
    public IReadOnlyList<AnimationFootMarker> FootMarkers => footMarkers ?? Array.Empty<AnimationFootMarker>();
#if UNITY_EDITOR
    public AnimationLocomotionData(AnimationClip clip, string hash, string setup, float stop,
        AnimationFootMarker[] markers)
    { sourceClip = clip; trajectoryHash = hash; footSetup = setup; stopTime = stop; footMarkers = markers ?? Array.Empty<AnimationFootMarker>(); }
#endif
}

/// <summary>Monotone planar path length remaining to the stop point. Built once when binding.</summary>
public sealed class AnimationStopDistanceCurve
{
    // Retained for existing callers; automatic detection no longer uses speed.
    [Obsolete("Automatic stop detection uses AutomaticStopPositionTolerance instead.")]
    public const float AutomaticStopSpeed = 0.05f;
    // Radius in metres around the final planar position. Authors can override the stop time.
    public const float AutomaticStopPositionTolerance = 0.02f;
    private readonly float[] _times;
    private readonly float[] _remaining;
    public IReadOnlyList<float> Times => _times;
    public IReadOnlyList<float> RemainingDistances => _remaining;
    public float StopTime => _times[_times.Length - 1];
    public float MaximumDistance => _remaining[0];
    private AnimationStopDistanceCurve(float[] times, float[] remaining) { _times = times; _remaining = remaining; }

    /// <summary>Earliest sample whose entire remaining planar trajectory stays within
    /// the tolerance radius of the final position. This estimates trajectory stability,
    /// not the semantic end of the braking action.</summary>
    public static float FindAutomaticStopTime(RootMotionTrajectory trajectory)
    {
        if (trajectory == null || !trajectory.TrySample(0f, out _)) return 0f;
        var times = trajectory.SampleTimes;
        var positions = trajectory.CumulativePositions;
        Vector3 finalPosition = positions[positions.Count - 1];
        float toleranceSquared = AutomaticStopPositionTolerance * AutomaticStopPositionTolerance;
        for (int i = times.Count - 2; i >= 0; i--)
        {
            Vector3 delta = positions[i] - finalPosition;
            delta.y = 0f;
            // Walking backward ensures every later sample is already inside the radius.
            if (delta.sqrMagnitude > toleranceSquared) return times[i + 1];
        }
        return 0f;
    }

    public static bool TryCreate(RootMotionTrajectory trajectory, float stopTime, out AnimationStopDistanceCurve curve)
    {
        curve = null;
        if (trajectory == null || !RootMotionTransform.IsFinite(stopTime)
            || stopTime <= 0f || !trajectory.TrySample(stopTime, out var stop)) return false;
        stopTime = Mathf.Min(stopTime, trajectory.Duration);
        var times = new List<float>();
        var positions = new List<Vector3>();
        for (int i = 0; i < trajectory.SampleCount && trajectory.SampleTimes[i] < stopTime; i++)
        { times.Add(trajectory.SampleTimes[i]); positions.Add(trajectory.CumulativePositions[i]); }
        times.Add(stopTime);
        positions.Add(stop.Position);
        if (times.Count < 2) return false;
        var remaining = new float[times.Count];
        for (int i = times.Count - 2; i >= 0; i--)
        {
            Vector3 delta = positions[i + 1] - positions[i];
            delta.y = 0f;
            remaining[i] = remaining[i + 1] + delta.magnitude;
        }
        if (!RootMotionTransform.IsFinite(remaining[0]) || remaining[0] <= 1e-5f) return false;
        curve = new AnimationStopDistanceCurve(times.ToArray(), remaining);
        return true;
    }

    public float TimeAtDistance(float distance)
    {
        if (distance <= 0f) return StopTime;
        if (distance >= MaximumDistance) return _times[0];
        int lo = 0, hi = _remaining.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (_remaining[mid] > distance) lo = mid; else hi = mid;
        }
        return Mathf.Lerp(_times[lo], _times[hi], (_remaining[lo] - distance) / (_remaining[lo] - _remaining[hi]));
    }
}

/// <summary>Left contact = 0, right contact = 0.5. Time is interpolated only between opposite feet.</summary>
public sealed class AnimationFootPhaseTrack
{
    private readonly AnimationFootMarker[] _markers;
    private readonly float _duration;
    private readonly bool _looping;
    public AnimationFootPhaseTrack(IReadOnlyList<AnimationFootMarker> markers, float duration, bool looping)
    {
        _duration = duration;
        _looping = looping;
        var usable = new List<AnimationFootMarker>();
        if (markers != null)
            for (int i = 0; i < markers.Count; i++)
                if (RootMotionTransform.IsFinite(markers[i].Time) && markers[i].Time >= 0f
                    && markers[i].Time <= duration && Enum.IsDefined(typeof(AnimationFoot), markers[i].Foot))
                    usable.Add(markers[i]);
        usable.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Foot.CompareTo(b.Foot));
        var unique = new List<AnimationFootMarker>();
        foreach (var marker in usable)
            if (unique.Count == 0 || marker.Time - unique[unique.Count - 1].Time > 1e-5f) unique.Add(marker);
        // The end of a loop is the same pose as its beginning, not another contact.
        if (looping && unique.Count > 1 && unique[0].Time == 0f && Mathf.Abs(unique[unique.Count - 1].Time - duration) < 1e-5f)
            unique.RemoveAt(unique.Count - 1);
        _markers = unique.ToArray();
    }

    public bool TrySample(float time, out float phase)
    {
        phase = 0f;
        if (_markers.Length == 0 || !RootMotionTransform.IsFinite(_duration) || _duration <= 0f || !RootMotionTransform.IsFinite(time)) return false;
        time = _looping ? Mathf.Repeat(time, _duration) : Mathf.Clamp(time, 0f, _duration);
        int lower = -1;
        for (int i = 0; i < _markers.Length && _markers[i].Time <= time; i++) lower = i;
        var a = _markers[lower >= 0 ? lower : _markers.Length - 1];
        int upper = lower + 1;
        var b = _markers[upper < _markers.Length ? upper : 0];
        if (!_looping && lower < 0) { phase = Base(b.Foot); return true; }
        if (!_looping && upper >= _markers.Length) { phase = Base(a.Foot); return true; }
        float aTime = a.Time - (lower < 0 ? _duration : 0f);
        float bTime = b.Time + (upper >= _markers.Length ? _duration : 0f);
        phase = a.Foot == b.Foot || bTime <= aTime ? Base(a.Foot)
            : Mathf.Repeat(Base(a.Foot) + 0.5f * (time - aTime) / (bTime - aTime), 1f);
        return true;
    }
    private static float Base(AnimationFoot foot) => foot == AnimationFoot.Left ? 0f : 0.5f;
    public static float Difference(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a * 360f, b * 360f)) / 360f;
}
