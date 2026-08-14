using UnityEngine;

public static class ActionSequenceAnimationTimeUtility
{
    public static float GetFrameStartTime(
        ActionSequenceContext context,
        int clipStartFrame,
        float startOffsetSeconds,
        float playbackSpeed)
    {
        int localFrame = Mathf.Max(0, context.Frame - clipStartFrame);
        float speed = Mathf.Max(0f, playbackSpeed);
        return Mathf.Max(0f, startOffsetSeconds) +
               localFrame / (float)Mathf.Max(1, context.FrameRate) * speed;
    }

    public static float GetFrameEndTime(
        ActionSequenceContext context,
        int clipStartFrame,
        float startOffsetSeconds,
        float playbackSpeed)
    {
        if (context.IsPoseBaseline)
            return Mathf.Max(0f, startOffsetSeconds);

        int localFrame = Mathf.Max(0, context.Frame - clipStartFrame);
        float speed = Mathf.Max(0f, playbackSpeed);
        return Mathf.Max(0f, startOffsetSeconds) +
               (localFrame + 1) / (float)Mathf.Max(1, context.FrameRate) * speed;
    }
}
