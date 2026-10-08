using System.Collections.Generic;
using UnityEngine;

public enum ActionAnimationSampleKind
{
    Segment,
    Hold,
}

/// <summary>Resolved fixed-time animation pose with no player or Animator side effects.</summary>
public readonly struct ActionAnimationSample
{
    public ActionAnimationSample(AnimationClip clip, float sourceTime, ActionAnimationSampleKind kind)
    {
        Clip = clip;
        SourceTime = sourceTime;
        Kind = kind;
    }

    public AnimationClip Clip { get; }
    public float SourceTime { get; }
    public ActionAnimationSampleKind Kind { get; }
}

/// <summary>The single SourceTime rule used by Action runtime and editor preview.</summary>
public static class ActionAnimationSampleResolver
{
    public static bool TryResolve(
        IReadOnlyList<ActionRuntimeAnimationRecord> records,
        double actionPosition,
        out ActionAnimationSample sample)
    {
        sample = default;
        ActionRuntimeAnimationRecord previous = default;
        bool hasPrevious = false;

        for (int i = 0; records != null && i < records.Count; i++)
        {
            ActionRuntimeAnimationRecord record = records[i];
            AnimationClip clip = record.AnimationAsset != null ? record.AnimationAsset.Clip : null;
            if (clip == null)
                continue;

            if (actionPosition >= record.StartFrame && actionPosition < record.EndFrameExclusive)
            {
                double sourceTime = record.SourceStartTime
                                    + (actionPosition - record.StartFrame)
                                    / ActionTimelineData.FrameRate
                                    * record.PlayRate;
                sample = new ActionAnimationSample(
                    clip,
                    Mathf.Clamp((float)sourceTime, record.SourceStartTime, record.SourceEndTime),
                    ActionAnimationSampleKind.Segment);
                return true;
            }

            if (actionPosition >= record.EndFrameExclusive
                && (!hasPrevious || record.EndFrameExclusive > previous.EndFrameExclusive))
            {
                previous = record;
                hasPrevious = true;
            }
        }

        if (!hasPrevious || previous.AnimationAsset == null || previous.AnimationAsset.Clip == null)
            return false;

        sample = new ActionAnimationSample(
            previous.AnimationAsset.Clip,
            previous.SourceEndTime,
            ActionAnimationSampleKind.Hold);
        return true;
    }
}
