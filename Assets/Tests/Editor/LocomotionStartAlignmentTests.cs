#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class LocomotionStartAlignmentTests
{
    [TestCase(-180f)]
    [TestCase(-90f)]
    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(180f)]
    public void StartAndEndFrames_AreIndependentOfMotorTurnProgress(float authoredTurn)
    {
        // Synthetic extraction baseline, deliberately not a direction-specific correction constant.
        const float baseline = 27f;
        foreach (float motorTurn in new[] { -37f, 0f, 63f, authoredTurn })
        {
            float entryOffset = LocomotionStartAlignment.PoseYaw(0f, authoredTurn, motorTurn, 0f);
            Assert.That(motorTurn + entryOffset, Is.EqualTo(0f).Within(0.0001f),
                "Entry pose belongs to the entry-world frame even if the Motor already rotated this tick.");
            float endOffset = LocomotionStartAlignment.PoseYaw(authoredTurn + baseline, authoredTurn, motorTurn, 1f);
            Assert.That(endOffset, Is.EqualTo(baseline).Within(0.0001f),
                "The ending pose must be in Motor-forward space even with a very slow/limited Motor turn.");
        }
    }

    [TestCase(0f)]
    [TestCase(137f)]
    [TestCase(-179f)]
    public void ReconstructedWorldHeading_DoesNotDependOnInitialActorYaw(float initialYaw)
    {
        const float phase = 0.4f, extracted = 92f, authored = 90f, motorTurn = 60f;
        float offset = LocomotionStartAlignment.PoseYaw(extracted, authored, motorTurn, phase);
        var actual = Quaternion.Euler(0f, initialYaw + motorTurn, 0f) * Quaternion.Euler(0f, offset, 0f);
        float warp = (motorTurn - authored) * Mathf.SmoothStep(0f, 1f, phase);
        var expected = Quaternion.Euler(0f, initialYaw + extracted + warp, 0f);
        Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.05f));
    }

    [Test]
    public void RearTurnCurve_PreservesItsPathAndIsAnImmutableSnapshot()
    {
        var clip = new AnimationClip();
        try
        {
            var trajectory = new RootMotionTrajectory();
            var times = new[] { 0f, 0.1f, 0.2f, 0.5f };
            var positions = new Vector3[4];
            var rotations = new[] { Quaternion.identity, Quaternion.Euler(0, 90, 0),
                Quaternion.Euler(0, 180, 0), Quaternion.Euler(0, 230, 0) };
            trajectory.EditorSetData(clip, 60, 0.5f, 1, "synthetic", times, positions, rotations);
            var curve = LocomotionStartYawCurve.Capture(trajectory);
            Assert.That(curve, Is.Not.Null);
            Assert.That(curve.Sample(0.5f), Is.EqualTo(230f).Within(0.001f));
            Assert.That(curve.Sample(0.35f), Is.EqualTo(205f).Within(0.001f));
            trajectory.EditorSetData(clip, 60, 0.5f, 1, "changed", times, positions,
                new[] { Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity });
            Assert.That(curve.Sample(1f), Is.EqualTo(230f).Within(0.001f));
            Assert.That(curve.Sample(-1f), Is.Zero);
        }
        finally { Object.DestroyImmediate(clip); }
    }

    [TestCase(-1f)]
    [TestCase(1f)]
    public void RearStartTies_PreferTheMotorSideWithoutWrapping180(float side)
    {
        Assert.That(Vector2.Angle(LocomotionStartAlignment.TargetDirection(-180f), Vector2.down), Is.LessThan(0.01f));
        Assert.That(Vector2.Angle(LocomotionStartAlignment.TargetDirection(180f), Vector2.down), Is.LessThan(0.01f));
        Assert.That(LocomotionStartAlignment.TurnSidePenalty(side * 180f, side * 3f), Is.Zero);
        Assert.That(LocomotionStartAlignment.TurnSidePenalty(-side * 180f, side * 3f), Is.EqualTo(1));
    }

    [Test]
    public void TurnToMove_DoesNotTreatStrafeOrRotationOverridesAsAFreeTurn()
    {
        var free = LocomotionSetDecisionTests.Context(Vector3.right, Vector3.zero);
        var strafe = LocomotionSetDecisionTests.Context(Vector3.right, Vector3.zero, facingDirection: Vector3.forward);
        Assert.That(LocomotionStartAlignment.IsTurnToMove(free), Is.True);
        Assert.That(LocomotionStartAlignment.IsTurnToMove(strafe), Is.False);
        var policy = new MotionStateSnapshot(1, 1, 1, 1, false, false, false, true);
        var motor = new LocomotionMotionContext(0.02f, true, Vector3.up, Quaternion.identity, default, policy);
        var overridden = new LocomotionRuntimeAnimationContext(free.Intent, true, Vector3.zero, Vector3.zero,
            motor, 0, 0, 0.02f);
        Assert.That(LocomotionStartAlignment.IsTurnToMove(overridden), Is.False);
    }

    [Test]
    public void OldStartEntries_RemainDirectionalWithoutADataMigration()
    {
        var entry = JsonUtility.FromJson<LocomotionStartEntry>("{\"targetLocalDirection\":{\"x\":1,\"y\":0}}");
        Assert.That(entry.FacingMode, Is.EqualTo(LocomotionStartFacingMode.Directional));
        Assert.That(entry.TargetLocalDirection, Is.EqualTo(Vector2.right));
    }
}
#endif
