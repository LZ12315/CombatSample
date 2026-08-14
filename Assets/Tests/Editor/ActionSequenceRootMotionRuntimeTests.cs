using NUnit.Framework;
using UnityEngine;

public sealed class ActionSequenceRootMotionRuntimeTests
{
    private const float DeltaTime = 1f / 60f;

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
}
