using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerInputBufferTests
{
    private const Enums.InputButton Attack = Enums.InputButton.LightAttack;

    [Test]
    public void QuickTap_PublishesBothEdgesAndTheirIdentityOnlyAtFixedBoundary()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.QueueButton(Attack, false, 1.01d);
        Assert.IsEmpty(buffer.Events);
        Assert.IsFalse(buffer.IsHeld(Attack));
        buffer.BeginFixedTick(1.02d);
        CollectionAssert.AreEqual(new[] { InputButtonEventKind.Pressed, InputButtonEventKind.Released },
            buffer.Events.Select(e => e.Kind));
        Assert.AreEqual(buffer.Events[0].PressId, buffer.Events[1].PressId);
        Assert.That(buffer.Events[1].HeldSeconds, Is.EqualTo(0.01d).Within(1e-9));
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out _));
        Assert.IsFalse(buffer.IsHeld(Attack));
    }

    [Test]
    public void Holding_DoesNotRepeatPressOrInventAnAutomaticRelease()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1d);
        buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out var press);
        Assert.IsTrue(buffer.TryConsume(press));
        buffer.BeginFixedTick(5d);
        Assert.IsTrue(buffer.IsHeld(Attack));
        Assert.AreEqual(4d, buffer.HeldSeconds(Attack));
        Assert.IsEmpty(buffer.Events, "Holding alone must not synthesize gameplay events.");
        buffer.QueueButton(Attack, false, 5.01d);
        buffer.BeginFixedTick(5.02d);
        Assert.IsFalse(buffer.IsHeld(Attack));
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Released, out var release));
        Assert.That(release.Input.HeldSeconds, Is.EqualTo(4.01d).Within(1e-9));
        Assert.AreEqual(press.Input.PressId, release.Input.PressId);
    }

    [TestCase(1)]
    [TestCase(30)]
    public void ReleaseDuration_UsesEventTimesRatherThanTheNumberOfTicks(int ticks)
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.QueueButton(Attack, false, 1.04d);
        for (int i = 1; i <= ticks; i++) buffer.BeginFixedTick(1d + 0.1d * i / ticks);
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Released, out var release));
        Assert.That(release.Input.HeldSeconds, Is.EqualTo(0.04d).Within(1e-9));
    }

    [Test]
    public void Expiration_UsesRealtimeAtTheFixedBoundaryWithoutUpdateCleanup()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1.19d);
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out _));
        buffer.BeginFixedTick(1.21d);
        Assert.IsFalse(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out _));
        Assert.IsTrue(buffer.IsHeld(Attack), "Command expiration is not a physical release.");
    }

    [Test]
    public void RapidClicks_RemainDistinctAndAreConsumedOldestFirst()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.QueueButton(Attack, false, 1.01d);
        buffer.QueueButton(Attack, true, 1.02d);
        buffer.QueueButton(Attack, false, 1.03d);
        buffer.BeginFixedTick(1.04d);
        buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out var first);
        Assert.IsTrue(buffer.TryConsume(first));
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out var second));
        Assert.Less(first.Input.Time, second.Input.Time);
        Assert.AreNotEqual(first.Input.PressId, second.Input.PressId);
        Assert.IsTrue(buffer.TryConsume(second));
        Assert.IsFalse(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out _));
    }

    [Test]
    public void MatchingIsReadOnlyAndConsumptionIsAtomicAndOnceOnly()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1d);
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out var first));
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out var second));
        Assert.IsFalse(buffer.TryConsume(new[] { first, default(PlayerInputEventMatch) }));
        Assert.IsTrue(buffer.CanConsume(first), "A failed group must not partially consume.");
        Assert.IsTrue(buffer.TryConsume(new[] { first, first }));
        Assert.IsFalse(buffer.TryConsume(second));
        Assert.IsFalse(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out _));
    }

    [Test]
    public void ConsumedPress_KeepsItsReleaseAvailableForChargeLogic()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1d);
        buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out var press);
        buffer.TryConsume(press);
        buffer.QueueButton(Attack, false, 1.1d);
        buffer.BeginFixedTick(1.1d);
        Assert.IsFalse(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out _));
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Released, out var release));
        Assert.AreEqual(press.Input.PressId, release.Input.PressId);
    }

    [Test]
    public void Reset_DropsOldOwnershipAndRequiresHeldButtonsToReleaseBeforeRearming()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1d);
        buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out var oldMatch);
        buffer.Reset(Attack);
        Assert.IsFalse(buffer.TryConsume(oldMatch));
        buffer.QueueButton(Attack, true, 1.1d);
        buffer.BeginFixedTick(1.1d);
        Assert.IsEmpty(buffer.Events);
        buffer.QueueButton(Attack, false, 1.2d);
        buffer.BeginFixedTick(1.2d);
        Assert.IsEmpty(buffer.Events, "Rearming must not invent a release attack.");
        buffer.QueueButton(Attack, true, 1.3d);
        buffer.BeginFixedTick(1.3d);
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out _));
    }

    [Test]
    public void Receipts_ExpireAtNextTickButTheUnconsumedEventCanBeMatchedAgain()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 1d);
        buffer.BeginFixedTick(1d);
        buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out var oldMatch);
        buffer.BeginFixedTick(1.05d);
        Assert.IsFalse(buffer.TryConsume(oldMatch));
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out var current));
        Assert.AreEqual(oldMatch.Input.Id, current.Input.Id);
        Assert.IsTrue(buffer.TryConsume(current));
    }

    [Test]
    public void Move_IsAContinuousSnapshotWithoutInventingButtonEvents()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueMove(Vector2.up, 1d);
        Assert.AreEqual(Vector2.zero, buffer.Move);
        buffer.BeginFixedTick(1d);
        Assert.AreEqual(Vector2.up, buffer.Move);
        buffer.BeginFixedTick(1.1d);
        Assert.AreEqual(Vector2.up, buffer.Move);
        Assert.IsEmpty(buffer.Events);
        buffer.Reset();
        Assert.AreEqual(Vector2.zero, buffer.Move);
    }

    [Test]
    public void FutureEvents_AreNotPublishedBeforeTheirTimestamp()
    {
        var buffer = new PlayerInputBuffer();
        buffer.QueueButton(Attack, true, 2d);
        buffer.BeginFixedTick(1d);
        Assert.IsEmpty(buffer.Events);
        buffer.BeginFixedTick(2d);
        Assert.IsTrue(buffer.TryFindEvent(Attack, InputButtonEventKind.Pressed, out _));
    }
}
