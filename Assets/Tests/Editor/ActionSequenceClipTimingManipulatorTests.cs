#if UNITY_EDITOR
using NUnit.Framework;

public sealed class ActionSequenceClipTimingManipulatorTests
{
    [Test]
    public void Move_KeepsLengthAndClampsLeftInAutoDuration()
    {
        var sequence = new ActionSequenceSnapshot(60, ActionSequenceDurationMode.AutoFromClips, 1);

        ClipMoveManipulator.CalculateTiming(sequence, 5, 9, -20, out int start, out int end);

        Assert.AreEqual(0, start);
        Assert.AreEqual(4, end);
    }

    [Test]
    public void Move_KeepsLengthAndClampsToFixedDuration()
    {
        var sequence = new ActionSequenceSnapshot(60, ActionSequenceDurationMode.FixedFrames, 12);

        ClipMoveManipulator.CalculateTiming(sequence, 5, 9, 20, out int start, out int end);

        Assert.AreEqual(8, start);
        Assert.AreEqual(12, end);
    }

    [Test]
    public void ResizeLeft_KeepsEndAndMinimumOneFrame()
    {
        var sequence = new ActionSequenceSnapshot(60, ActionSequenceDurationMode.AutoFromClips, 1);

        ClipResizeManipulator.CalculateLeftTiming(sequence, 5, 9, 20, out int start, out int end);

        Assert.AreEqual(8, start);
        Assert.AreEqual(9, end);
    }

    [Test]
    public void ResizeRight_KeepsStartAndMinimumOneFrame()
    {
        var sequence = new ActionSequenceSnapshot(60, ActionSequenceDurationMode.AutoFromClips, 1);

        ClipResizeManipulator.CalculateRightTiming(sequence, 5, 9, -20, out int start, out int end);

        Assert.AreEqual(5, start);
        Assert.AreEqual(6, end);
    }

    [Test]
    public void ResizeRight_ClampsToFixedDuration()
    {
        var sequence = new ActionSequenceSnapshot(60, ActionSequenceDurationMode.FixedFrames, 12);

        ClipResizeManipulator.CalculateRightTiming(sequence, 5, 9, 20, out int start, out int end);

        Assert.AreEqual(5, start);
        Assert.AreEqual(12, end);
    }
}
#endif
