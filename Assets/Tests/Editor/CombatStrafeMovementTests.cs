using NodeCanvas.Tasks.Actions;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatStrafeMovementTests
{
    [TestCase(false, -1f)]
    [TestCase(true, 1f)]
    public void WithinBand_MovesSidewaysAndFacesTarget(bool moveRight, float expectedX)
    {
        // Height differences must not change horizontal steering.
        Assert.IsTrue(CombatStrafeMovement.TryBuildIntent(new Vector3(0f, 12f, -3f),
            new Vector3(0f, -7f, 0f), moveRight, 3f, 0.5f, 0.4f, out var intent));

        AssertVector(new Vector3(expectedX, 0f, 0f), intent.WorldMoveDirection);
        AssertVector(Vector3.forward, intent.FacingDirection);
        Assert.AreEqual(0.4f, intent.MoveStrength);
    }

    [TestCase(2f, -1f)]
    [TestCase(4f, 1f)]
    public void OutsideBand_AddsDistanceCorrectionWithoutLosingSidewaysMotion(float distance, float radialSign)
    {
        Assert.IsTrue(CombatStrafeMovement.TryBuildIntent(Vector3.back * distance, Vector3.zero,
            true, 3f, 0.5f, 0.4f, out var intent));

        Assert.That(intent.WorldMoveDirection.x, Is.GreaterThan(0f));
        Assert.That(intent.WorldMoveDirection.z * radialSign, Is.GreaterThan(0f));
        Assert.That(Mathf.Abs(intent.WorldMoveDirection.z), Is.LessThan(intent.WorldMoveDirection.x));
        Assert.That(intent.WorldMoveDirection.magnitude, Is.EqualTo(1f).Within(0.0001f));
        Assert.AreEqual(0f, intent.WorldMoveDirection.y);
        AssertVector(Vector3.forward, intent.FacingDirection);
    }

    [TestCase(2.5f, -0.0001f)]
    [TestCase(3.5f, 0.0001f)]
    public void BandBoundary_DistanceCorrectionStartsGradually(float boundary, float beyond)
    {
        Assert.IsTrue(CombatStrafeMovement.TryBuildIntent(Vector3.back * boundary, Vector3.zero,
            true, 3f, 0.5f, 0.4f, out var atBoundary));
        Assert.IsTrue(CombatStrafeMovement.TryBuildIntent(Vector3.back * (boundary + beyond), Vector3.zero,
            true, 3f, 0.5f, 0.4f, out var justOutside));

        AssertVector(Vector3.right, atBoundary.WorldMoveDirection);
        Assert.That(Vector3.Distance(atBoundary.WorldMoveDirection, justOutside.WorldMoveDirection),
            Is.LessThan(0.001f));
    }

    [Test]
    public void FarTarget_CorrectionRemainsBoundedRatherThanBecomingPureChase()
    {
        Assert.IsTrue(CombatStrafeMovement.TryBuildIntent(Vector3.back * 30f, Vector3.zero,
            true, 3f, 0.5f, 0.4f, out var intent));

        Assert.That(intent.WorldMoveDirection.x, Is.GreaterThan(0.7f));
        Assert.That(intent.WorldMoveDirection.z, Is.InRange(0f, 0.708f));
        Assert.That(intent.WorldMoveDirection.magnitude, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void SharedWorldOffset_DoesNotChangeMovementOrFacing()
    {
        Vector3 position = new Vector3(1f, 2f, -4f);
        Vector3 target = new Vector3(-2f, 7f, 1f);
        Vector3 offset = new Vector3(20f, -5f, 13f);
        Assert.IsTrue(CombatStrafeMovement.TryBuildIntent(position, target,
            false, 3f, 0.5f, 0.4f, out var original));
        Assert.IsTrue(CombatStrafeMovement.TryBuildIntent(position + offset, target + offset,
            false, 3f, 0.5f, 0.4f, out var translated));

        AssertVector(original.WorldMoveDirection, translated.WorldMoveDirection);
        AssertVector(original.FacingDirection, translated.FacingDirection);
    }

    [Test]
    public void ZeroStrength_StillProvidesFacingWithoutRequestingMovementSpeed()
    {
        Assert.IsTrue(CombatStrafeMovement.TryBuildIntent(Vector3.back * 3f, Vector3.zero,
            true, 3f, 0.5f, 0f, out var intent));

        Assert.AreEqual(0f, intent.MoveStrength);
        AssertVector(Vector3.forward, intent.FacingDirection);
    }

    [Test]
    public void InvalidOrCoincidentInput_IsRejectedWithIdleIntent()
    {
        AssertRejected(Vector3.zero, Vector3.zero, 3f, 0.5f, 0.4f);
        AssertRejected(Vector3.zero, new Vector3(0f, 10f, 0f), 3f, 0.5f, 0.4f);
        AssertRejected(new Vector3(float.NaN, 0f, 0f), Vector3.zero, 3f, 0.5f, 0.4f);
        AssertRejected(Vector3.back * 3f, Vector3.zero, 0f, 0.5f, 0.4f);
        AssertRejected(Vector3.back * 3f, Vector3.zero, float.PositiveInfinity, 0.5f, 0.4f);
        AssertRejected(Vector3.back * 3f, Vector3.zero, 3f, -1f, 0.4f);
        AssertRejected(Vector3.back * 3f, Vector3.zero, 3f, float.NaN, 0.4f);
        AssertRejected(Vector3.back * 3f, Vector3.zero, 3f, 0.5f, float.NaN);
        AssertRejected(Vector3.back * 3f, Vector3.zero, 3f, 0.5f, 1.1f);
    }

    private static void AssertRejected(Vector3 position, Vector3 target, float preferred, float tolerance, float strength)
    {
        Assert.IsFalse(CombatStrafeMovement.TryBuildIntent(position, target,
            true, preferred, tolerance, strength, out var intent));
        AssertVector(Vector3.zero, intent.WorldMoveDirection);
        AssertVector(Vector3.zero, intent.FacingDirection);
        Assert.AreEqual(0f, intent.MoveStrength);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.That((expected - actual).sqrMagnitude, Is.LessThan(0.00000001f));
    }
}
