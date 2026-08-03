#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct ActionSequenceTimelineTick
{
    public ActionSequenceTimelineTick(int frame, float x, bool major, bool labelled)
    {
        Frame = frame;
        X = x;
        Major = major;
        Labelled = labelled;
    }

    public int Frame { get; }
    public float X { get; }
    public bool Major { get; }
    public bool Labelled { get; }
}

public sealed class ActionSequenceTimelineTransform
{
    public const float MinPixelsPerFrame = 0.5f;
    public const float MaxPixelsPerFrame = 64f;
    public const float FitHorizontalPadding = 24f;

    private float pixelsPerFrame = 8f;
    private float scrollX;
    private float viewportWidth = 1f;
    private int viewEndFrame = 60;

    public float PixelsPerFrame => pixelsPerFrame;
    public float ScrollX => scrollX;
    public float ViewportWidth => viewportWidth;
    public int ViewEndFrame => viewEndFrame;
    public float ContentWidth => Mathf.Max(viewportWidth, viewEndFrame * pixelsPerFrame);
    public int FirstVisibleFrame => Mathf.Max(0, Mathf.FloorToInt(ViewportXToFrame(0f)));
    public int LastVisibleFrame => Mathf.Min(viewEndFrame, Mathf.CeilToInt(ViewportXToFrame(viewportWidth)));
    public bool ShouldDrawPerFrameMinorGrid => pixelsPerFrame >= 8f;

    public void Configure(float newPixelsPerFrame, float newScrollX, float newViewportWidth, int newViewEndFrame)
    {
        viewportWidth = Mathf.Max(1f, newViewportWidth);
        viewEndFrame = Mathf.Max(1, newViewEndFrame);
        pixelsPerFrame = Mathf.Clamp(newPixelsPerFrame, MinPixelsPerFrame, MaxPixelsPerFrame);
        scrollX = ClampScroll(newScrollX);
    }

    public void SetViewportWidth(float width)
    {
        viewportWidth = Mathf.Max(1f, width);
        scrollX = ClampScroll(scrollX);
    }

    public void SetViewEndFrame(int endFrame)
    {
        viewEndFrame = Mathf.Max(1, endFrame);
        scrollX = ClampScroll(scrollX);
    }

    public void SetPixelsPerFrame(float value)
    {
        pixelsPerFrame = Mathf.Clamp(value, MinPixelsPerFrame, MaxPixelsPerFrame);
        scrollX = ClampScroll(scrollX);
    }

    public void ZoomAtViewportX(float viewportX, float requestedPixelsPerFrame)
    {
        viewportX = Mathf.Clamp(viewportX, 0f, viewportWidth);
        float anchorFrame = ViewportXToFrame(viewportX);
        pixelsPerFrame = Mathf.Clamp(requestedPixelsPerFrame, MinPixelsPerFrame, MaxPixelsPerFrame);
        scrollX = ClampScroll(anchorFrame * pixelsPerFrame - viewportX);
    }

    public void SetScrollX(float value)
    {
        scrollX = ClampScroll(value);
    }

    public void Pan(float deltaPixels)
    {
        scrollX = ClampScroll(scrollX + deltaPixels);
    }

    public void Fit(int requiredEndFrame)
    {
        viewEndFrame = Mathf.Max(1, requiredEndFrame);
        float availableWidth = Mathf.Max(1f, viewportWidth - FitHorizontalPadding * 2f);
        pixelsPerFrame = Mathf.Clamp(availableWidth / viewEndFrame, MinPixelsPerFrame, MaxPixelsPerFrame);
        scrollX = 0f;
    }

    public void FrameRange(int startFrame, int endFrame, float padding)
    {
        startFrame = Mathf.Max(0, startFrame);
        endFrame = Mathf.Max(startFrame + 1, endFrame);
        viewEndFrame = Mathf.Max(viewEndFrame, endFrame);

        float availableWidth = Mathf.Max(1f, viewportWidth - padding * 2f);
        int frameSpan = Mathf.Max(1, endFrame - startFrame);
        pixelsPerFrame = Mathf.Clamp(availableWidth / frameSpan, MinPixelsPerFrame, MaxPixelsPerFrame);
        scrollX = ClampScroll(startFrame * pixelsPerFrame - padding);
    }

    public float FrameToViewportX(float frame)
    {
        return frame * pixelsPerFrame - scrollX;
    }

    public float ViewportXToFrame(float viewportX)
    {
        return (viewportX + scrollX) / pixelsPerFrame;
    }

    public float ClampScroll(float value)
    {
        return Mathf.Clamp(value, 0f, Mathf.Max(0f, ContentWidth - viewportWidth));
    }

    public IReadOnlyList<ActionSequenceTimelineTick> BuildVisibleTicks()
    {
        var ticks = new List<ActionSequenceTimelineTick>();
        int minorStep = ChooseStepForMinimumPixels(8f);
        int labelledStep = ChooseStepForMinimumPixels(72f);
        int first = Mathf.Max(0, FloorToMultiple(FirstVisibleFrame, minorStep));
        int last = Mathf.Min(viewEndFrame, LastVisibleFrame + minorStep);

        for (int frame = first; frame <= last; frame += minorStep)
        {
            bool labelled = frame % labelledStep == 0;
            bool major = labelled || frame % (minorStep * 5) == 0;
            ticks.Add(new ActionSequenceTimelineTick(frame, FrameToViewportX(frame), major, labelled));
        }

        return ticks;
    }

    public int ChooseStepForMinimumPixels(float minimumPixels)
    {
        float minimumFrames = Mathf.Max(1f, minimumPixels / Mathf.Max(0.001f, pixelsPerFrame));
        int exponent = Mathf.FloorToInt(Mathf.Log10(minimumFrames));
        float magnitude = Mathf.Pow(10f, exponent);
        float[] multipliers = { 1f, 2f, 5f, 10f };

        for (int i = 0; i < multipliers.Length; i++)
        {
            int step = Mathf.Max(1, Mathf.RoundToInt(multipliers[i] * magnitude));
            if (step * pixelsPerFrame >= minimumPixels)
                return step;
        }

        return Mathf.Max(1, Mathf.RoundToInt(10f * magnitude));
    }

    private static int FloorToMultiple(int value, int multiple)
    {
        if (multiple <= 1)
            return value;

        return value - value % multiple;
    }
}
#endif
