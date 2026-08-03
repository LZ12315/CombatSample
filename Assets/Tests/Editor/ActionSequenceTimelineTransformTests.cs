#if UNITY_EDITOR
using NUnit.Framework;

public sealed class ActionSequenceTimelineTransformTests
{
    [Test]
    public void FrameAndViewportX_ConvertBothWays()
    {
        var transform = new ActionSequenceTimelineTransform();
        transform.Configure(10f, 25f, 200f, 100);

        float x = transform.FrameToViewportX(7f);

        Assert.AreEqual(45f, x, 0.001f);
        Assert.AreEqual(7f, transform.ViewportXToFrame(x), 0.001f);
    }

    [Test]
    public void Scroll_IsClampedToContentRange()
    {
        var transform = new ActionSequenceTimelineTransform();
        transform.Configure(10f, 10000f, 200f, 50);

        Assert.AreEqual(300f, transform.ScrollX, 0.001f);

        transform.SetScrollX(-20f);

        Assert.AreEqual(0f, transform.ScrollX, 0.001f);
    }

    [Test]
    public void ZoomAtViewportX_KeepsAnchorFrameStable()
    {
        var transform = new ActionSequenceTimelineTransform();
        transform.Configure(4f, 40f, 300f, 200);
        float anchorBefore = transform.ViewportXToFrame(120f);

        transform.ZoomAtViewportX(120f, 12f);

        Assert.AreEqual(anchorBefore, transform.ViewportXToFrame(120f), 0.001f);
    }

    [Test]
    public void Fit_UsesViewEndFrameAndPaddingWithoutChangingDuration()
    {
        var transform = new ActionSequenceTimelineTransform();
        transform.Configure(8f, 20f, 248f, 60);

        transform.Fit(100);

        Assert.AreEqual(2f, transform.PixelsPerFrame, 0.001f);
        Assert.AreEqual(0f, transform.ScrollX, 0.001f);
        Assert.AreEqual(100, transform.ViewEndFrame);
    }

    [Test]
    public void Ticks_UseReadableStepAndPerFrameGridAtCloseZoom()
    {
        var transform = new ActionSequenceTimelineTransform();
        transform.Configure(1f, 0f, 200f, 200);

        Assert.AreEqual(10, transform.ChooseStepForMinimumPixels(8f));
        Assert.IsFalse(transform.ShouldDrawPerFrameMinorGrid);

        transform.Configure(10f, 0f, 200f, 200);

        Assert.AreEqual(1, transform.ChooseStepForMinimumPixels(8f));
        Assert.IsTrue(transform.ShouldDrawPerFrameMinorGrid);
    }

    [Test]
    public void FrameRange_FitsRequestedFramesWithPadding()
    {
        var transform = new ActionSequenceTimelineTransform();
        transform.Configure(8f, 0f, 248f, 100);

        transform.FrameRange(10, 20, 24f);

        Assert.AreEqual(20f, transform.PixelsPerFrame, 0.001f);
        Assert.AreEqual(176f, transform.ScrollX, 0.001f);
        Assert.AreEqual(100, transform.ViewEndFrame);
    }
}
#endif
