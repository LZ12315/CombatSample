#if UNITY_EDITOR
using UnityEngine;

internal static class ClipMoveManipulator
{
    public static void CalculateTiming(
        ActionSequenceSnapshot sequence,
        int originalStartFrame,
        int originalEndFrame,
        int deltaFrames,
        out int startFrame,
        out int endFrame)
    {
        int length = Mathf.Max(1, originalEndFrame - originalStartFrame);
        startFrame = originalStartFrame + deltaFrames;

        if (sequence.DurationMode == ActionSequenceDurationMode.FixedFrames)
        {
            int maxStart = Mathf.Max(0, sequence.FixedDurationFrames - length);
            startFrame = Mathf.Clamp(startFrame, 0, maxStart);
        }
        else
        {
            startFrame = Mathf.Max(0, startFrame);
        }

        endFrame = startFrame + length;
    }
}
#endif
