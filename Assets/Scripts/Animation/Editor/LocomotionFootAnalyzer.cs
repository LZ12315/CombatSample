#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Captured from the same manual graph as the proven root trajectory bake.</summary>
public sealed class LocomotionFootSamples
{
    public readonly List<float> Times = new List<float>();
    public readonly List<Vector3> Left = new List<Vector3>();
    public readonly List<Vector3> Right = new List<Vector3>();
    public float LegLength { get; private set; }
    private Transform _left;
    private Transform _right;
    private Vector3 _origin;
    private Quaternion _inverseRotation;

    public bool Bind(Animator animator, out string reason)
    {
        reason = "Foot analysis requires a Humanoid Animator with mapped left/right feet and a left leg.";
        if (!animator.isHuman) return false;
        _left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        _right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        Transform upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        if (_left == null || _right == null || upper == null || lower == null) return false;
        LegLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, _left.position);
        if (!RootMotionTransform.IsFinite(LegLength) || LegLength < 0.01f) return false;
        _origin = animator.transform.position;
        _inverseRotation = Quaternion.Inverse(animator.transform.rotation);
        reason = string.Empty;
        return true;
    }

    public void Capture(float time)
    {
        Times.Add(time);
        Left.Add(_inverseRotation * (_left.position - _origin));
        Right.Add(_inverseRotation * (_right.position - _origin));
    }
}

public static class LocomotionFootAnalyzer
{
    public static bool TryAnalyze(IReadOnlyList<float> times, IReadOnlyList<Vector3> left,
        IReadOnlyList<Vector3> right, float legLength, AnimationRigBakeSettings settings,
        bool looping, LocomotionAnimationRole role, out LocomotionAnalyzedMotion result, out string reason)
    {
        result = null;
        reason = "Foot samples or contact analysis settings are invalid.";
        if (times == null || times.Count < 3 || left == null || right == null
            || left.Count != times.Count || right.Count != times.Count || settings == null
            || !RootMotionTransform.IsFinite(legLength) || legLength <= 0f
            || !RootMotionTransform.IsFinite(settings.FootContactHeightRatio) || settings.FootContactHeightRatio < 0f
            || !RootMotionTransform.IsFinite(settings.FootVerticalSpeedRatio) || settings.FootVerticalSpeedRatio < 0f
            || !RootMotionTransform.IsFinite(settings.MinimumFootContactSeconds) || settings.MinimumFootContactSeconds <= 0f)
            return false;
        for (int i = 0; i < times.Count; i++)
            if (!RootMotionTransform.IsFinite(times[i]) || (i > 0 && times[i] <= times[i - 1])
                || !Finite(left[i]) || !Finite(right[i])) return false;
        if (Mathf.Abs(times[0]) > 0.0001f) return false;
        float duration = times[times.Count - 1];
        FootContactInterval[] l = FindContacts(times, left, duration, legLength, settings, looping);
        FootContactInterval[] r = FindContacts(times, right, duration, legLength, settings, looping);
        if (l.Length == 0 || r.Length == 0) { reason = "No reliable left/right contact pair; correct the analysis settings or mark contacts manually."; return false; }
        result = new LocomotionAnalyzedMotion { LeftContacts = l, RightContacts = r };
        if (role == LocomotionAnimationRole.MoveCycle)
        {
            if (!looping || l.Length != 1 || r.Length != 1)
            { result = null; reason = "Automatic Move analysis requires one left/right gait cycle in a looping clip; use manual cycle anchors for ambiguous samples."; return false; }
            float start = l[0].Start;
            float middle = r[0].Start;
            if (middle <= start) middle += duration;
            var cycle = new LocomotionCycleMapping(new FootPhaseAnchor(start, 0f),
                new FootPhaseAnchor(middle, 0.5f), new FootPhaseAnchor(start + duration, 1f));
            if (!cycle.Validate(duration, out reason)) { result = null; return false; }
            result.Cycle = cycle;
        }
        else if (role == LocomotionAnimationRole.Transition)
        {
            // Only a candidate: the author must confirm the semantic exit phase.
            var contacts = new List<KeyValuePair<float, bool>>();
            foreach (var contact in l) contacts.Add(new KeyValuePair<float, bool>(contact.Start, true));
            foreach (var contact in r) contacts.Add(new KeyValuePair<float, bool>(contact.Start, false));
            contacts.Sort((a, b) => a.Key.CompareTo(b.Key));
            var last = contacts[contacts.Count - 1];
            var previous = contacts[contacts.Count - 2];
            float interval = last.Key - previous.Key;
            if (last.Value != previous.Value && interval > 0.01f && duration - last.Key <= interval * 2f)
            {
                result.HasExitPhaseCandidate = true;
                result.ExitPhaseCandidate = Mathf.Repeat((last.Value ? 0f : 0.5f)
                    + 0.5f * (duration - last.Key) / interval, 1f);
            }
        }
        reason = string.Empty;
        return true;
    }

    private static FootContactInterval[] FindContacts(IReadOnlyList<float> times, IReadOnlyList<Vector3> positions,
        float duration, float legLength, AnimationRigBakeSettings settings, bool looping)
    {
        float minimum = float.PositiveInfinity;
        for (int i = 0; i < positions.Count; i++) minimum = Mathf.Min(minimum, positions[i].y);
        var intervals = new List<FootContactInterval>();
        float start = -1f;
        for (int i = 0; i < times.Count; i++)
        {
            int before = Mathf.Max(0, i - 1), after = Mathf.Min(times.Count - 1, i + 1);
            float verticalSpeed = Mathf.Abs((positions[after].y - positions[before].y) / (times[after] - times[before]));
            bool contact = positions[i].y - minimum <= settings.FootContactHeightRatio * legLength
                && verticalSpeed <= settings.FootVerticalSpeedRatio * legLength;
            if (contact && start < 0f) start = times[i];
            if (!contact && start >= 0f)
            { intervals.Add(new FootContactInterval(start, times[i])); start = -1f; }
        }
        if (start >= 0f) intervals.Add(new FootContactInterval(start, duration));
        if (looping && intervals.Count > 1 && intervals[0].Start == 0f && intervals[intervals.Count - 1].End == duration)
        {
            var last = intervals[intervals.Count - 1];
            last.End = duration + intervals[0].End;
            intervals[intervals.Count - 1] = last;
            intervals.RemoveAt(0);
        }
        intervals.RemoveAll(c => c.End - c.Start < settings.MinimumFootContactSeconds);
        return intervals.ToArray();
    }

    private static bool Finite(Vector3 v) => RootMotionTransform.IsFinite(v.x)
        && RootMotionTransform.IsFinite(v.y) && RootMotionTransform.IsFinite(v.z);
}
#endif
