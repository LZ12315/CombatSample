using System.Collections.Generic;
using NUnit.Framework;

public sealed class PlayerInputConditionTests
{
    private const Enums.InputButton Attack = Enums.InputButton.LightAttack;
    private static InputSequenceCondition Press() => new InputSequenceCondition
        { clips = new[] { new InputEventClip { button = Attack } } };

    private static PlayerInputBuffer Pressed()
    {
        var input = new PlayerInputBuffer();
        input.QueueButton(Attack, true, 1d);
        input.BeginFixedTick(1d);
        return input;
    }

    [Test]
    public void SharedCondition_ReturnsIndependentCandidateReceiptsForEachOwner()
    {
        var condition = Press();
        Assert.IsTrue(new ActionEntryMatch(new List<PlayerInputMatch>()).IsCurrentFor(null));
        PlayerInputBuffer first = Pressed();
        PlayerInputBuffer second = Pressed();
        Assert.IsTrue(condition.TryMatchInput(first, out var firstReceipt));
        Assert.IsTrue(condition.TryMatchInput(second, out var secondReceipt));
        var inputs = new List<PlayerInputMatch> { firstReceipt };
        var firstCandidate = new ActionEntryMatch(inputs);
        inputs.Clear();
        var secondCandidate = new ActionEntryMatch(new List<PlayerInputMatch> { secondReceipt });
        Assert.IsFalse(firstCandidate.TryConsume(second));
        Assert.IsTrue(firstCandidate.IsCurrentFor(first));
        Assert.IsTrue(firstCandidate.TryConsume(first));
        Assert.IsFalse(firstCandidate.TryConsume(first));
        Assert.IsTrue(secondCandidate.IsCurrentFor(second));
        Assert.IsTrue(secondCandidate.TryConsume(second));
    }

    [Test]
    public void HeldThreshold_IsARepeatableStateCheckIndependentOfCommandConsumption()
    {
        PlayerInputBuffer input = Pressed();
        var state = new InputStateCondition { button = Attack, minimumHoldSeconds = 0.5f };
        Assert.IsFalse(state.TryMatchInput(input, out _));
        Assert.IsTrue(input.TryConsumePress(Attack));
        input.BeginFixedTick(1.6d);
        Assert.IsTrue(state.TryMatchInput(input, out _));
        Assert.IsTrue(state.TryMatchInput(input, out _));
        Assert.IsFalse(state.ConsumesInput);
    }

    [Test]
    public void NotHeld_IsExplicitAndUnavailableInputNeverPasses()
    {
        var condition = new InputStateCondition { button = Attack, held = false };
        var input = new PlayerInputBuffer();
        Assert.IsTrue(condition.TryMatchInput(input, out _));
        Assert.IsFalse(condition.TryMatchInput(null, out _));
        input.QueueButton(Attack, true, 1d);
        input.BeginFixedTick(1d);
        Assert.IsFalse(condition.TryMatchInput(input, out _));
        condition.invertResult = true;
        Assert.IsFalse(condition.TryMatchInput(input, out _));
    }

    [Test]
    public void InvalidSelections_DoNotMatchOrBecomeValidThroughInversion()
    {
        PlayerInputBuffer input = Pressed();
        var condition = new InputSequenceCondition();
        Assert.IsFalse(condition.TryMatchInput(input, out _));
        condition.clips = new[] { new InputEventClip { button = Attack | Enums.InputButton.HeavyAttack } };
        Assert.IsFalse(condition.IsConfigurationValid);
        condition.clips = new[] { new InputEventClip { button = Attack, type = (InputEventType)99 } };
        Assert.IsFalse(condition.IsConfigurationValid);
        condition = Press();
        condition.invertResult = true;
        Assert.IsFalse(condition.TryMatchInput(input, out _));
        Assert.IsFalse(ActionCondition.TryValidateAll(new[] { condition }, false, out _));
        Assert.IsFalse(new InputStateCondition { button = Attack, minimumHoldSeconds = float.NaN }.IsConfigurationValid);
        Assert.IsFalse(new InputStateCondition { type = InputStateType.Direction, direction = (InputDirection)99 }.IsConfigurationValid);
        var notHeld = new InputStateCondition { button = Attack, held = false, minimumHoldSeconds = 1f };
        Assert.IsTrue(notHeld.TryMatchInput(new PlayerInputBuffer(), out _));
    }

    [Test]
    public void ConditionSets_RejectNullAndSkipOnlyPlayerChecksForEventSubmissions()
    {
        var incomplete = new InputSequenceCondition();
        Assert.IsFalse(ActionCondition.TryValidateAll(new ActionCondition[] { new AlwaysTrueCondition(), null }, false, out int index));
        Assert.AreEqual(1, index);
        Assert.IsFalse(ActionCondition.TryValidateAll(new[] { incomplete }, false, out _));
        Assert.IsTrue(ActionCondition.TryValidateAll(new[] { incomplete }, true, out _));
        Assert.IsFalse(ActionCondition.TryValidateAll(new ActionCondition[] { incomplete, null }, true, out _));
    }

    [Test]
    public void ExitConditions_AcceptStateButRejectConsumableSequence()
    {
        Assert.IsTrue(ActionCondition.TryValidateAll(new[] { new InputStateCondition { button = Attack, held = false } },
            false, out _, exit: true));
        Assert.IsFalse(ActionCondition.TryValidateAll(new[] { Press() }, false, out _, exit: true));
    }
}
