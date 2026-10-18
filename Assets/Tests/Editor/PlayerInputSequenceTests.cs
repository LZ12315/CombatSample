using NUnit.Framework;
using UnityEngine;

public sealed class PlayerInputSequenceTests
{
    private const Enums.InputButton Attack = Enums.InputButton.LightAttack;
    private static InputEventClip Press => new InputEventClip { button = Attack };
    private static InputEventClip Enter(InputDirection direction) => new InputEventClip
        { type = InputEventType.DirectionEntered, direction = direction };
    private static InputSequenceCondition Sequence(params InputEventClip[] clips) => new InputSequenceCondition { clips = clips };
    private static InputDirectionFrame Frame(int target = 1, bool rotated = false) => new InputDirectionFrame(
        target, 1, 0, rotated ? Vector3.right : Vector3.forward,
        rotated ? Vector3.back : Vector3.right, Vector3.forward);

    private static PlayerInputDirections Start(PlayerInputBuffer buffer)
    {
        buffer.BeginFixedTick(0d);
        var directions = new PlayerInputDirections();
        directions.Publish(buffer, Frame());
        return directions;
    }

    [TestCase(0.5d, 0.5f, true)]
    [TestCase(0.5001d, 0.5f, false)]
    [TestCase(0.6d, 0.7f, true)]
    [TestCase(0.7d, 0.7f, true)]
    public void Window_UsesWholeSequenceAndIncludesBoundary(double end, float window, bool expected)
    {
        var buffer = new PlayerInputBuffer();
        var directions = Start(buffer);
        buffer.QueueMove(Vector2.down, 0d);
        buffer.QueueMove(Vector2.up, 0.25d);
        buffer.QueueButton(Attack, true, end);
        buffer.BeginFixedTick(end);
        directions.Publish(buffer, Frame());
        var sequence = Sequence(Enter(InputDirection.Back), Enter(InputDirection.Forward), Press);
        sequence.maxSequenceTime = window;
        Assert.AreEqual(expected, sequence.TryMatchInput(buffer, out var match, directions));
        if (expected) Assert.IsTrue(match.TryConsume(buffer), "Old context steps must still be consumable.");
    }

    [Test]
    public void CompletionCache_UsesTerminalTimeAndMatchingDoesNotRenewIt()
    {
        var buffer = new PlayerInputBuffer();
        var directions = Start(buffer);
        buffer.QueueMove(Vector2.down, 0d);
        buffer.QueueMove(Vector2.up, 0.2d);
        buffer.QueueButton(Attack, true, 0.4d);
        var sequence = Sequence(Enter(InputDirection.Back), Enter(InputDirection.Forward), Press);
        buffer.BeginFixedTick(0.55d);
        directions.Publish(buffer, Frame());
        Assert.IsTrue(sequence.TryMatchInput(buffer, out var first, directions));
        Assert.IsTrue(sequence.TryMatchInput(buffer, out _, directions));
        buffer.BeginFixedTick(0.61d);
        directions.Publish(buffer, Frame());
        Assert.IsFalse(first.IsCurrentFor(buffer));
        Assert.IsFalse(sequence.TryMatchInput(buffer, out _, directions));
        Assert.IsTrue(buffer.IsHeld(Attack));
    }

    [Test]
    public void SameTimestamp_PreservesCaptureOrderAndConsumesOnlySelectedSteps()
    {
        var buffer = new PlayerInputBuffer();
        var directions = Start(buffer);
        buffer.QueueMove(Vector2.down, 1d);
        buffer.QueueButton(Enums.InputButton.Jump, true, 1d);
        buffer.QueueMove(Vector2.up, 1d);
        buffer.QueueButton(Attack, true, 1d);
        buffer.QueueButton(Attack, false, 1d);
        buffer.BeginFixedTick(1d);
        directions.Publish(buffer, Frame());
        var sequence = Sequence(Enter(InputDirection.Back), Enter(InputDirection.Forward), Press);
        Assert.IsTrue(sequence.TryMatchInput(buffer, out var match, directions));
        Assert.IsFalse(Sequence(Press, Enter(InputDirection.Forward)).TryMatchInput(buffer, out _, directions));
        Assert.IsTrue(match.TryConsume(buffer));
        Assert.IsFalse(sequence.TryMatchInput(buffer, out _, directions));
        Assert.IsTrue(buffer.TryFindEvent(Enums.InputButton.Jump, InputButtonEventKind.Pressed, out _));
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Released, out _));
        Assert.AreEqual(Vector2.up, buffer.Move);
    }

    [Test]
    public void MultipleStarts_SelectsRecentValidContextRatherThanFailingAtOldStart()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 0d);
        buffer.QueueButton(Attack, false, 0.01d);
        buffer.QueueButton(Attack, true, 0.8d);
        buffer.QueueButton(Attack, false, 0.81d);
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1d);
        Assert.IsTrue(Sequence(Press, Press).TryMatchInput(buffer, out var match));
        Assert.IsTrue(match.TryConsume(buffer));
        Assert.IsFalse(Sequence(Press).TryMatchInput(buffer, out _));
    }

    [Test]
    public void RepeatedClips_RequireDistinctEventsAndDirectionReentry()
    {
        var buffer = new PlayerInputBuffer();
        var directions = Start(buffer);
        buffer.QueueMove(Vector2.up, 1d);
        buffer.BeginFixedTick(1d);
        directions.Publish(buffer, Frame());
        directions.Publish(buffer, Frame());
        Assert.IsFalse(Sequence(Enter(InputDirection.Forward), Enter(InputDirection.Forward)).TryMatchInput(buffer, out _, directions));
        buffer.QueueMove(Vector2.zero, 1.02d);
        buffer.QueueMove(Vector2.up, 1.03d);
        buffer.BeginFixedTick(1.04d);
        directions.Publish(buffer, Frame());
        Assert.IsTrue(Sequence(Enter(InputDirection.Forward), Enter(InputDirection.Forward)).TryMatchInput(buffer, out _, directions));
    }

    [Test]
    public void DirectionStability_HoldAndBoundaryNoiseDoNotRepeatEntries()
    {
        var buffer = new PlayerInputBuffer();
        var directions = Start(buffer);
        buffer.QueueMove(Vector2.up, 1d);
        buffer.QueueMove(new Vector2(0.70f, 0.71f), 1.01d);
        buffer.QueueMove(new Vector2(0.71f, 0.70f), 1.02d);
        buffer.BeginFixedTick(1.03d);
        directions.Publish(buffer, Frame());
        var forward = Sequence(Enter(InputDirection.Forward));
        Assert.IsTrue(forward.TryMatchInput(buffer, out var first, directions));
        Assert.IsTrue(first.TryConsume(buffer));
        Assert.IsFalse(forward.TryMatchInput(buffer, out _, directions));
        buffer.BeginFixedTick(1.04d);
        directions.Publish(buffer, Frame(rotated: true));
        Assert.IsFalse(Sequence(Enter(InputDirection.Right)).TryMatchInput(buffer, out _, directions), "Rotating the reference is not a device edge.");
        Assert.IsFalse(Sequence(Enter(InputDirection.Back)).TryMatchInput(buffer, out _, directions));
        Assert.AreEqual(InputDirection.Back, directions.Current);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReferenceChange_InvalidatesDirectionReceiptsButPreservesButtonRequests(bool transientRelock)
    {
        var buffer = new PlayerInputBuffer();
        var directions = Start(buffer);
        buffer.QueueMove(Vector2.up, 1d);
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1d);
        directions.Publish(buffer, Frame());
        Assert.IsTrue(Sequence(Enter(InputDirection.Forward), Press).TryMatchInput(buffer, out var old, directions));
        var changed = transientRelock
            ? new InputDirectionFrame(1, 1, 0, Vector3.forward, Vector3.right, Vector3.forward, revision: 1)
            : Frame(2);
        directions.Publish(buffer, changed);
        Assert.IsFalse(old.TryConsume(buffer));
        Assert.IsFalse(Sequence(Enter(InputDirection.Forward)).TryMatchInput(buffer, out _, directions));
        Assert.IsTrue(Sequence(Press).TryMatchInput(buffer, out _, directions));
        directions.Publish(buffer, default);
        Assert.IsFalse(new InputStateCondition { type = InputStateType.Direction, direction = InputDirection.Neutral }
            .TryMatchInput(buffer, out _, directions), "No target is not a valid target-relative Neutral state.");
    }

    [Test]
    public void CurrentState_IsNotLimitedByDirectionEventAge()
    {
        var buffer = new PlayerInputBuffer();
        var directions = Start(buffer);
        buffer.QueueMove(Vector2.up, 1d);
        buffer.BeginFixedTick(1d);
        directions.Publish(buffer, Frame());
        buffer.QueueButton(Attack, true, 4d);
        buffer.BeginFixedTick(4d);
        directions.Publish(buffer, Frame());
        Assert.IsTrue(new InputStateCondition { type = InputStateType.Direction, direction = InputDirection.Forward }
            .TryMatchInput(buffer, out _, directions));
        Assert.IsTrue(Sequence(Press).TryMatchInput(buffer, out _));
        Assert.IsFalse(Sequence(Enter(InputDirection.Forward), Press).TryMatchInput(buffer, out _, directions));
    }

    [Test]
    public void GroupConsumption_RejectsPartialAndStaleReceipts()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.QueueButton(Enums.InputButton.Jump, true, 1d);
        buffer.BeginFixedTick(1d);
        Sequence(Press).TryMatchInput(buffer, out var attack);
        Sequence(new InputEventClip { button = Enums.InputButton.Jump }).TryMatchInput(buffer, out var jump);
        var group = new ActionEntryMatch(new System.Collections.Generic.List<PlayerInputMatch> { attack, jump });
        Assert.IsTrue(jump.TryConsume(buffer));
        Assert.IsFalse(group.TryConsume(buffer));
        Assert.IsTrue(attack.IsCurrentFor(buffer));
        buffer.BeginFixedTick(1.01d);
        Assert.IsFalse(attack.IsCurrentFor(buffer));
        Sequence(Press).TryMatchInput(buffer, out var next);
        buffer.Reset();
        Assert.IsFalse(next.TryConsume(buffer));
    }

    [Test]
    public void NewReference_DoesNotCombineOlderButtonsWithNewDirections()
    {
        var buffer = new PlayerInputBuffer();
        var directions = Start(buffer);
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1d);
        directions.Publish(buffer, Frame(2));
        buffer.QueueMove(Vector2.up, 1.01d);
        buffer.BeginFixedTick(1.02d);
        directions.Publish(buffer, Frame(2));
        Assert.IsFalse(Sequence(Press, Enter(InputDirection.Forward)).TryMatchInput(buffer, out _, directions));
        Assert.IsTrue(Sequence(Press).TryMatchInput(buffer, out _), "Button-only commands survive a direction reference change.");
    }

    [Test]
    public void NewReference_BaselinesHeldMoveWithoutInventingAnEnter()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueMove(Vector2.up, 1d);
        buffer.BeginFixedTick(1d);
        var directions = new PlayerInputDirections();
        directions.Publish(buffer, Frame());
        Assert.AreEqual(InputDirection.Forward, directions.Current);
        Assert.IsFalse(Sequence(Enter(InputDirection.Forward)).TryMatchInput(buffer, out _, directions));
        buffer.Reset();
        buffer.BeginFixedTick(1.01d);
        directions.Publish(buffer, Frame());
        Assert.AreEqual(InputDirection.Neutral, directions.Current);
    }

    [Test]
    public void CenterHysteresis_RejectsDriftUntilEnterThresholdIsCrossed()
    {
        var buffer = new PlayerInputBuffer();
        var directions = Start(buffer);
        buffer.QueueMove(new Vector2(0f, 0.11f), 1d);
        buffer.QueueMove(new Vector2(0f, 0.09f), 1.01d);
        buffer.QueueMove(new Vector2(0f, 0.11f), 1.02d);
        buffer.BeginFixedTick(1.03d);
        directions.Publish(buffer, Frame());
        Assert.AreEqual(InputDirection.Neutral, directions.Current);
        Assert.IsFalse(Sequence(Enter(InputDirection.Forward)).TryMatchInput(buffer, out _, directions));
        buffer.QueueMove(new Vector2(0f, 0.13f), 1.04d);
        buffer.BeginFixedTick(1.04d);
        directions.Publish(buffer, Frame());
        Assert.IsTrue(Sequence(Enter(InputDirection.Forward)).TryMatchInput(buffer, out _, directions));
    }

    [TestCase(0f)]
    [TestCase(2f)]
    public void SupportedWindowExtremes_HaveEnoughRetainedContext(float window)
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Enums.InputButton.Jump, true, 0d);
        buffer.QueueButton(Attack, true, window);
        buffer.BeginFixedTick(window + 0.2d);
        var sequence = Sequence(new InputEventClip { button = Enums.InputButton.Jump }, Press);
        sequence.maxSequenceTime = window;
        Assert.IsTrue(sequence.TryMatchInput(buffer, out var match));
        Assert.IsTrue(match.TryConsume(buffer));
        buffer.BeginFixedTick(window + 0.21d);
        Assert.IsFalse(sequence.TryMatchInput(buffer, out _));
    }

    [Test]
    public void OverlappingConditions_CommitTheUnionOnlyOnce()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1d);
        Sequence(Press).TryMatchInput(buffer, out var first);
        Sequence(Press).TryMatchInput(buffer, out var second);
        var group = new ActionEntryMatch(new System.Collections.Generic.List<PlayerInputMatch> { first, second });
        Assert.IsTrue(group.TryConsume(buffer));
        Assert.IsFalse(group.TryConsume(buffer));
    }

    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(2.01f)]
    public void InvalidWindows_FailClosed(float value)
    {
        var sequence = Sequence(Press);
        sequence.maxSequenceTime = value;
        Assert.IsFalse(sequence.IsConfigurationValid);
    }
}
