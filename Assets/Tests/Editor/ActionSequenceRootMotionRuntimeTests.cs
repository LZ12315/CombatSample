using NUnit.Framework;
using UnityEngine;

public sealed class ActionSequenceRootMotionRuntimeTests
{
    private const float DeltaTime = 1f / 60f;

    [Test]
    public void RootMotionYawUtility_ExtractsPureYaw()
    {
        Assert.IsTrue(RootMotionYawUtility.TryExtractLocalYaw(
            Quaternion.Euler(0f, 70f, 0f),
            out Quaternion yaw));

        AssertQuaternion(Quaternion.Euler(0f, 70f, 0f), yaw);
    }

    [Test]
    public void RootMotionYawUtility_ExtractsTwistFromPitchRollSwing()
    {
        Quaternion source =
            Quaternion.AngleAxis(25f, Vector3.right) *
            Quaternion.AngleAxis(60f, Vector3.up);

        Assert.IsTrue(RootMotionYawUtility.TryExtractLocalYaw(source, out Quaternion yaw));

        AssertQuaternion(Quaternion.Euler(0f, 60f, 0f), yaw);
    }

    [Test]
    public void RootMotionYawUtility_NormalizesNegativeQuaternionHemisphere()
    {
        Quaternion source = Quaternion.Euler(0f, 35f, 0f);
        source = new Quaternion(-source.x, -source.y, -source.z, -source.w);

        Assert.IsTrue(RootMotionYawUtility.TryExtractLocalYaw(source, out Quaternion yaw));

        AssertQuaternion(Quaternion.Euler(0f, 35f, 0f), yaw);
    }

    [Test]
    public void RootMotionYawUtility_RejectsDegenerateQuaternion()
    {
        Assert.IsFalse(RootMotionYawUtility.TryExtractLocalYaw(new Quaternion(0f, 0f, 0f, 0f), out _));
    }

    [Test]
    public void RootMotionBuffer_TrajectoryOwnerSurvivesZeroDeltaTick()
    {
        var buffer = new RootMotionBuffer();
        MotionOwner owner = buffer.BeginTrajectory();

        buffer.BeginMotorTick();

        Assert.IsTrue(buffer.HasTrajectoryTick);
        Assert.AreEqual(Vector3.zero, buffer.TrajectoryLocalPosition);

        buffer.EndTrajectory(owner);
        buffer.BeginMotorTick();

        Assert.IsFalse(buffer.HasTrajectoryTick);
    }

    [Test]
    public void SelfRotationBuffer_RejectsSecondOwnerAndIgnoresStaleToken()
    {
        var buffer = new SelfRotationBuffer();
        Assert.IsTrue(buffer.TryBegin(out MotionOwner first));
        Assert.IsFalse(buffer.TryBegin(out MotionOwner second));
        Assert.IsFalse(second.IsValid);

        Assert.IsFalse(buffer.Submit(new MotionOwner(first.Id + 100), Quaternion.Euler(0f, 90f, 0f)));
        Assert.IsTrue(buffer.Submit(first, Quaternion.Euler(0f, 30f, 0f)));
        buffer.BeginMotorTick();

        Assert.IsTrue(buffer.HasTickOwner);
        AssertQuaternion(Quaternion.Euler(0f, 30f, 0f), buffer.TickLocalYawDelta);
    }

    [Test]
    public void SelfRotationBuffer_ActiveOwnerSurvivesZeroDeltaTick()
    {
        var buffer = new SelfRotationBuffer();
        Assert.IsTrue(buffer.TryBegin(out MotionOwner owner));

        buffer.BeginMotorTick();

        Assert.IsTrue(buffer.HasTickOwner);
        AssertQuaternion(Quaternion.identity, buffer.TickLocalYawDelta);

        Assert.IsTrue(buffer.End(owner));
        buffer.BeginMotorTick();

        Assert.IsFalse(buffer.HasTickOwner);
    }

    [Test]
    public void ActorMotionRuntime_SelfRotationOwnerSnapshotsYawAtMotorTick()
    {
        var runtime = new ActorMotionRuntime();
        Assert.IsTrue(runtime.TryBeginSelfRotation(out MotionOwner owner));
        Assert.IsTrue(runtime.SubmitSelfRotation(owner, Quaternion.Euler(0f, 45f, 0f)));

        runtime.BeginMotorTick();

        Assert.IsTrue(runtime.HasSelfRotationTick);
        AssertQuaternion(Quaternion.Euler(0f, 45f, 0f), runtime.SelfRotationLocalYawDelta);
        Assert.IsTrue(runtime.EndSelfRotation(owner));
        Assert.IsFalse(runtime.HasSelfRotationTick);
    }

    [Test]
    public void ActorMotor_SelfRotationOverridesFacingAndUsesTickStartRotation()
    {
        var gameObject = new GameObject("ActorMotorSelfRotationTest");
        try
        {
            var motor = gameObject.AddComponent<ActorMotor>();
            gameObject.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            motor.DebugFacing.Initialize(Quaternion.Euler(0f, 170f, 0f));
            Assert.IsTrue(motor.TryBeginSelfRotation(out MotionOwner owner));
            Assert.IsTrue(motor.SubmitSelfRotation(owner, Quaternion.Euler(0f, 60f, 0f)));

            motor.BeforeCharacterUpdate(DeltaTime);
            Quaternion rotation = Quaternion.identity;
            motor.UpdateRotation(ref rotation, DeltaTime);

            AssertQuaternion(Quaternion.Euler(0f, 75f, 0f), rotation);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ActorMotor_ActiveSelfRotationWithZeroDeltaKeepsTickStartRotation()
    {
        var gameObject = new GameObject("ActorMotorSelfRotationZeroDeltaTest");
        try
        {
            var motor = gameObject.AddComponent<ActorMotor>();
            gameObject.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            motor.DebugFacing.Initialize(Quaternion.Euler(0f, 170f, 0f));
            Assert.IsTrue(motor.TryBeginSelfRotation(out _));

            motor.BeforeCharacterUpdate(DeltaTime);
            Quaternion rotation = Quaternion.identity;
            motor.UpdateRotation(ref rotation, DeltaTime);

            AssertQuaternion(Quaternion.Euler(0f, 15f, 0f), rotation);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ActorMotor_EndSelfRotationSyncsFacingToActualRotation()
    {
        var gameObject = new GameObject("ActorMotorSelfRotationSyncTest");
        try
        {
            var motor = gameObject.AddComponent<ActorMotor>();
            motor.DebugFacing.Initialize(Quaternion.identity);
            Assert.IsTrue(motor.TryBeginSelfRotation(out MotionOwner owner));

            gameObject.transform.rotation = Quaternion.Euler(0f, 123f, 0f);
            motor.EndSelfRotation(owner);

            Quaternion rotation = Quaternion.identity;
            motor.UpdateRotation(ref rotation, DeltaTime);

            AssertQuaternion(Quaternion.Euler(0f, 123f, 0f), rotation);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void TrajectoryRootMotion_UsesTickStartRotationAndDoesNotUseMovementTimeScale()
    {
        var runtime = new ActorMotionRuntime();
        runtime.SetMovementTimeScale(0f);
        MotionOwner owner = runtime.BeginTrajectoryRootMotion();
        runtime.SubmitTrajectoryRootMotion(owner, Vector3.forward);
        runtime.BeginMotorTick();

        Vector3 velocity = runtime.ComposeKccVelocity(
            null,
            Vector3.zero,
            false,
            Quaternion.Euler(0f, 90f, 0f),
            DeltaTime);

        Assert.That(velocity.x, Is.EqualTo(60f).Within(0.0001f));
        Assert.That(velocity.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(velocity.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void TrajectoryRootMotion_AddsHorizontalImpulseAndKeepsVerticalChannels()
    {
        var runtime = new ActorMotionRuntime();
        runtime.AddHorizontalImpulse(Vector3.right * 3f);
        runtime.AddVerticalImpulse(5f);
        MotionOwner owner = runtime.BeginTrajectoryRootMotion();
        runtime.SubmitTrajectoryRootMotion(owner, Vector3.forward);
        runtime.BeginMotorTick();

        Vector3 velocity = runtime.ComposeKccVelocity(
            null,
            Vector3.zero,
            false,
            Quaternion.identity,
            DeltaTime);

        Assert.That(velocity.x, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(velocity.y, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(velocity.z, Is.EqualTo(60f).Within(0.0001f));
    }

    [Test]
    public void HorizontalVelocityOwnerOverridesTrajectoryRootMotionAndImpulse()
    {
        var runtime = new ActorMotionRuntime();
        runtime.AddHorizontalImpulse(Vector3.right * 3f);
        MotionOwner velocityOwner = runtime.BeginHorizontalVelocity();
        runtime.SetHorizontalVelocity(velocityOwner, Vector3.left * 7f);
        MotionOwner rootOwner = runtime.BeginTrajectoryRootMotion();
        runtime.SubmitTrajectoryRootMotion(rootOwner, Vector3.forward);
        runtime.BeginMotorTick();

        Vector3 velocity = runtime.ComposeKccVelocity(
            null,
            Vector3.zero,
            false,
            Quaternion.identity,
            DeltaTime);

        Assert.That(velocity.x, Is.EqualTo(-7f).Within(0.0001f));
        Assert.That(velocity.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(velocity.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void TrajectoryRootMotion_UsesTickStartRotationEvenWhenSelfRotationTurnsSameTick()
    {
        var runtime = new ActorMotionRuntime();
        MotionOwner rootOwner = runtime.BeginTrajectoryRootMotion();
        runtime.SubmitTrajectoryRootMotion(rootOwner, Vector3.forward);
        Assert.IsTrue(runtime.TryBeginSelfRotation(out MotionOwner rotationOwner));
        runtime.SubmitSelfRotation(rotationOwner, Quaternion.Euler(0f, 90f, 0f));
        runtime.BeginMotorTick();

        Vector3 velocity = runtime.ComposeKccVelocity(
            null,
            Vector3.zero,
            false,
            Quaternion.identity,
            DeltaTime);

        Assert.That(velocity.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(velocity.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(velocity.z, Is.EqualTo(60f).Within(0.0001f));
    }

    private static void AssertQuaternion(Quaternion expected, Quaternion actual)
    {
        Assert.LessOrEqual(
            Quaternion.Angle(expected, actual),
            0.01f,
            $"Expected {expected.eulerAngles}, got {actual.eulerAngles}.");
    }
}
