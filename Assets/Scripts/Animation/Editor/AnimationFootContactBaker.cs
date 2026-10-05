#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contact candidates are height valleys following an observable descent.
/// A 10% height excursion separates steps from small motion within a planted pose.
/// These are editable estimates, not measurements of sole contact with a floor.
/// </summary>
internal static class AnimationFootContactBaker
{
    private const float TimeEpsilon = 1e-5f;

    internal static AnimationFootMarker[] Generate(IReadOnlyList<float> times,
        IReadOnlyList<float> leftHeights, IReadOnlyList<float> rightHeights, bool looping = false)
    {
        var markers = new List<AnimationFootMarker>();
        Add(times, leftHeights, AnimationFoot.Left, looping, markers);
        Add(times, rightHeights, AnimationFoot.Right, looping, markers);
        markers.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Foot.CompareTo(b.Foot));
        return markers.ToArray();
    }

    private static void Add(IReadOnlyList<float> times, IReadOnlyList<float> heights,
        AnimationFoot foot, bool looping, List<AnimationFootMarker> markers)
    {
        if (times == null || heights == null || heights.Count != times.Count || heights.Count < 2) return;
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        int peak = 0;
        for (int i = 0; i < heights.Count; i++)
        {
            if (!RootMotionTransform.IsFinite(heights[i]) || !RootMotionTransform.IsFinite(times[i])
                || (i > 0 && times[i] <= times[i - 1])) return;
            if (heights[i] > heights[peak]) peak = i;
            min = Mathf.Min(min, heights[i]); max = Mathf.Max(max, heights[i]);
        }
        if (max - min <= 1e-5f) return; // A stationary foot has no observable gait cycle.

        // Ignore a sub-frame duration remainder: it cannot confirm a new contact.
        int count = heights.Count;
        while (count > 2 && times[count - 1] - times[count - 2] <= TimeEpsilon) count--;
        int period = count - 1; // The loop endpoint repeats its beginning.
        int start = looping ? peak % period : 0;
        float excursion = (max - min) * 0.1f;
        float high = heights[start];
        int lowest = -1;
        // Starting at a loop's peak lets valleys across its seam complete normally.
        for (int step = 1; step < count; step++)
        {
            int i = looping ? (start + step) % period : step;
            if (lowest < 0)
            {
                high = Mathf.Max(high, heights[i]);
                if (high - heights[i] >= excursion) lowest = i;
            }
            else if (heights[i] < heights[lowest]) lowest = i;
            else if (heights[i] - heights[lowest] >= excursion)
            {
                markers.Add(new AnimationFootMarker(times[lowest], foot));
                lowest = -1;
                high = heights[i];
            }
        }
        // A non-looping Stop may finish with the foot planted. A descent still
        // reaching its minimum at the last sample is incomplete, not a contact.
        if (!looping && lowest >= 0 && times[count - 1] - times[lowest] > TimeEpsilon)
            markers.Add(new AnimationFootMarker(times[lowest], foot));
    }
}
#endif
