using UnityEngine;

/// <summary>Distance owns only the braking clock. The zero-distance pose is sampled once,
/// then the ordinary clip clock owns the tail. Time never runs backwards within a Stop.</summary>
public sealed class LocomotionStopDistancePlayback
{
    private AnimationStopDistanceCurve _curve;
    private float _time;
    private bool _tail;
    public void Begin(AnimationStopDistanceCurve curve) { _curve = curve; _time = 0f; _tail = false; }
    public float? Sample(float? remainingDistance)
    {
        if (_curve == null || _tail) return null;
        if (!remainingDistance.HasValue || !RootMotionTransform.IsFinite(remainingDistance.Value)
            || remainingDistance.Value < 0f)
        { _curve = null; return null; }
        _time = Mathf.Max(_time, _curve.TimeAtDistance(remainingDistance.Value));
        if (remainingDistance.Value == 0f) _tail = true;
        return _time;
    }
}
