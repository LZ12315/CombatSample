#if UNITY_EDITOR
using UnityEngine;

internal static class ClipResizeManipulator
{
    public static void CalculateLeftTiming(
        ActionSequenceSnapshot sequence,
        int originalStartFrame,
        int originalEndFrame,
        int deltaFrames,
        out int startFrame,
        out int endFrame)
    {
        endFrame = originalEndFrame;
        if (sequence.DurationMode == ActionSequenceDurationMode.FixedFrames)
            endFrame = Mathf.Clamp(endFrame, 1, Mathf.Max(1, sequence.FixedDurationFrames));

        startFrame = originalStartFrame + deltaFrames;
        startFrame = Mathf.Clamp(startFrame, 0, Mathf.Max(0, endFrame - 1));
    }

    public static void CalculateRightTiming(
        ActionSequenceSnapshot sequence,
        int originalStartFrame,
        int originalEndFrame,
        int deltaFrames,
        out int startFrame,
        out int endFrame)
    {
        startFrame = Mathf.Max(0, originalStartFrame);
        if (sequence.DurationMode == ActionSequenceDurationMode.FixedFrames)
            startFrame = Mathf.Clamp(startFrame, 0, Mathf.Max(0, sequence.FixedDurationFrames - 1));

        endFrame = originalEndFrame + deltaFrames;
        if (sequence.DurationMode == ActionSequenceDurationMode.FixedFrames)
            endFrame = Mathf.Clamp(endFrame, startFrame + 1, Mathf.Max(startFrame + 1, sequence.FixedDurationFrames));
        else
            endFrame = Mathf.Max(startFrame + 1, endFrame);
    }
}
#endif
