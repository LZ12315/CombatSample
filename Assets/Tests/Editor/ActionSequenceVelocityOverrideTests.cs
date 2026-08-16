using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ActionSequenceVelocityOverrideTests
{
    private const float DeltaTime = 1f / 60f;

    [Test]
    public void MotionChannels_VelocityOwnerRestoresCoveredOwner()
    {
        var channels = new MotionChannels();
        MotionOwner a = channels.BeginHorizontalVelocity();
        channels.SetHorizontalVelocity(a, Vector3.right * 2f);
        MotionOwner b = channels.BeginHorizontalVelocity();
        channels.SetHorizontalVelocity(b, Vector3.left * 5f);

        Assert.AreEqual(Vector3.left * 5f, channels.ComposeHorizontal(Vector3.forward, 1f));

        channels.EndHorizontalVelocity(b);

        Assert.AreEqual(Vector3.right * 2f, channels.ComposeHorizontal(Vector3.forward, 1f));
    }

    [Test]
    public void MotionChannels_CoveredOwnerKeepsUpdatingCachedVelocity()
    {
        var channels = new MotionChannels();
        MotionOwner a = channels.BeginHorizontalVelocity();
        channels.SetHorizontalVelocity(a, Vector3.right * 2f);
        MotionOwner b = channels.BeginHorizontalVelocity();
        channels.SetHorizontalVelocity(b, Vector3.forward * 3f);
        channels.SetHorizontalVelocity(a, Vector3.left * 7f);

        channels.EndHorizontalVelocity(b);

        Assert.AreEqual(Vector3.left * 7f, channels.ComposeHorizontal(Vector3.zero, 1f));
    }

    [Test]
    public void MotionChannels_ZeroVelocityStillOwnsAxisAndAxesAreIndependent()
    {
        var channels = new MotionChannels();
        channels.AddHorizontalImpulse(Vector3.right * 4f);
        MotionOwner horizontal = channels.BeginHorizontalVelocity();
        channels.SetHorizontalVelocity(horizontal, Vector3.zero);
        MotionOwner vertical = channels.BeginVerticalVelocity();
        channels.SetVerticalVelocity(vertical, 6f);

        Assert.AreEqual(Vector3.zero, channels.ComposeHorizontal(Vector3.forward * 10f, 1f));
        Assert.AreEqual(6f, channels.ComposeVertical(1f));

        channels.EndHorizontalVelocity(horizontal);

        Assert.AreEqual(Vector3.forward * 10f + Vector3.right * 4f, channels.ComposeHorizontal(Vector3.forward * 10f, 1f));
        Assert.AreEqual(6f, channels.ComposeVertical(1f));
    }

    [Test]
    public void MotionChannels_StaleTokenAndRepeatedReleaseDoNotAffectStackTop()
    {
        var channels = new MotionChannels();
        MotionOwner a = channels.BeginHorizontalVelocity();
        MotionOwner b = channels.BeginHorizontalVelocity();
        channels.SetHorizontalVelocity(a, Vector3.right * 9f);
        channels.SetHorizontalVelocity(b, Vector3.forward * 3f);

        channels.SetHorizontalVelocity(new MotionOwner(a.Id + b.Id + 1000), Vector3.left * 20f);
        channels.EndHorizontalVelocity(a);
        channels.EndHorizontalVelocity(a);

        Assert.AreEqual(Vector3.forward * 3f, channels.ComposeHorizontal(Vector3.zero, 1f));
        Assert.AreEqual(1, channels.DebugHorizontalVelocityOwnerCount);
    }

    [Test]
    public void ActorMotionRuntime_HorizontalVelocityOverridesRootMotionAndDoesNotCompensate()
    {
        var runtime = new ActorMotionRuntime();
        MotionOwner rootOwner = runtime.BeginTrajectoryRootMotion();
        runtime.SubmitTrajectoryRootMotion(rootOwner, Vector3.forward);
        MotionOwner velocityOwner = runtime.BeginHorizontalVelocity();
        runtime.SetHorizontalVelocity(velocityOwner, Vector3.right * 4f);
        runtime.BeginMotorTick();

        Vector3 covered = runtime.ComposeKccVelocity(
            null,
            Vector3.zero,
            false,
            Quaternion.identity,
            DeltaTime);

        runtime.EndHorizontalVelocity(velocityOwner);
        runtime.BeginMotorTick();

        Vector3 afterExit = runtime.ComposeKccVelocity(
            null,
            Vector3.zero,
            false,
            Quaternion.identity,
            DeltaTime);

        Assert.AreEqual(Vector3.right * 4f, covered);
        Assert.AreEqual(Vector3.zero, afterExit);
    }

    [Test]
    public void ActorMotionRuntime_BackgroundHorizontalImpulseDecaysWhileCovered()
    {
        var runtime = new ActorMotionRuntime();
        runtime.AddHorizontalImpulse(Vector3.right * 10f);
        MotionOwner owner = runtime.BeginHorizontalVelocity();
        runtime.SetHorizontalVelocity(owner, Vector3.zero);

        runtime.StepChannels(
            0.25f,
            false,
            new ActorMotionRuntimeConfig(4f, 0f, 0.1f));
        runtime.EndHorizontalVelocity(owner);

        Vector3 velocity = runtime.ComposeKccVelocity(
            null,
            Vector3.zero,
            false,
            Quaternion.identity,
            DeltaTime);

        Assert.That(velocity.x, Is.GreaterThan(0f).And.LessThan(10f));
    }

    [Test]
    public void ActorMotionRuntime_VerticalVelocityPausesGravity()
    {
        var runtime = new ActorMotionRuntime();
        MotionOwner owner = runtime.BeginVerticalVelocity();
        runtime.SetVerticalVelocity(owner, 0f);

        runtime.StepChannels(
            1f,
            false,
            new ActorMotionRuntimeConfig(0f, 0f, 0.1f));
        runtime.EndVerticalVelocity(owner);

        Vector3 velocity = runtime.ComposeKccVelocity(
            null,
            Vector3.zero,
            false,
            Quaternion.identity,
            DeltaTime);

        Assert.That(velocity.y, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void VelocityOverrideClip_RequiresContextDirectionOnlyWhenHorizontalUsesContext()
    {
        var clip = CreateVelocityClip(useHorizontal: true, useVertical: false);
        clip.config.directionMode = MotionDirectionMode.FromContext;

        Assert.AreEqual(ActionContextFieldMask.Direction, clip.RequiredContextFields);

        clip.config.useHorizontalVelocity = false;
        clip.config.useVerticalVelocity = true;

        Assert.AreEqual(ActionContextFieldMask.None, clip.RequiredContextFields);
    }

    [Test]
    public void VelocityOverrideClip_RejectsDisabledAxesAndInvalidPreset()
    {
        var clip = CreateVelocityClip(useHorizontal: false, useVertical: false);
        Assert.IsFalse(clip.HasAnyAxis);

        clip.config.useHorizontalVelocity = true;
        clip.config.directionMode = MotionDirectionMode.LocalHorizontal;
        clip.config.localHorizontalDirection = Vector3.zero;

        Assert.IsFalse(clip.HasValidPresetLocalDirection());
    }

    [Test]
    public void VelocityOverrideClip_CurvesSampleFirstMiddleLastAndSingleFrame()
    {
        var actorObject = new GameObject("VelocityOverride Curve Actor");
        try
        {
            Actor actor = actorObject.AddComponent<Actor>();
            ActorMotor motor = actorObject.AddComponent<ActorMotor>();
            actor.actorMotor = motor;

            var clip = CreateVelocityClip(useHorizontal: false, useVertical: true);
            clip.startFrame = 0;
            clip.endFrame = 3;
            clip.config.verticalSpeed = 2f;
            clip.config.verticalCurve = AnimationCurve.Linear(0f, 1f, 1f, 3f);
            ActionSequenceClipRuntime runtime = clip.CreateRuntime();
            ActionSequenceContext context = CreateContext(actor);

            runtime.OnEnter(context);

            SetContextFrame(context, 0);
            runtime.OnTick(context);
            Assert.AreEqual(2f, motor.DebugChannels.DebugOwnerVerticalVelocity);

            SetContextFrame(context, 1);
            runtime.OnTick(context);
            Assert.AreEqual(4f, motor.DebugChannels.DebugOwnerVerticalVelocity);

            SetContextFrame(context, 2);
            runtime.OnTick(context);
            Assert.AreEqual(6f, motor.DebugChannels.DebugOwnerVerticalVelocity);

            runtime.OnExit(context, true);

            var oneFrame = CreateVelocityClip(useHorizontal: false, useVertical: true);
            oneFrame.startFrame = 0;
            oneFrame.endFrame = 1;
            oneFrame.config.verticalSpeed = 2f;
            oneFrame.config.verticalCurve = AnimationCurve.Linear(0f, 1f, 1f, 5f);
            ActionSequenceClipRuntime oneFrameRuntime = oneFrame.CreateRuntime();
            oneFrameRuntime.OnEnter(context);
            SetContextFrame(context, 0);
            oneFrameRuntime.OnTick(context);

            Assert.AreEqual(2f, motor.DebugChannels.DebugOwnerVerticalVelocity);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void VelocityOverrideClip_PresetLocalUsesCurrentHeadingWhenSampled()
    {
        var actorObject = new GameObject("VelocityOverride Preset Actor");
        try
        {
            Actor actor = actorObject.AddComponent<Actor>();
            ActorMotor motor = actorObject.AddComponent<ActorMotor>();
            actor.actorMotor = motor;

            var clip = CreateVelocityClip(useHorizontal: true, useVertical: false);
            clip.config.localHorizontalDirection = Vector3.forward;
            clip.config.horizontalSpeed = 3f;
            ActionSequenceClipRuntime runtime = clip.CreateRuntime();
            ActionSequenceContext context = CreateContext(actor);

            runtime.OnEnter(context);

            actorObject.transform.rotation = Quaternion.identity;
            SetContextFrame(context, 0);
            runtime.OnTick(context);
            AssertVector(Vector3.forward * 3f, motor.DebugChannels.DebugOwnerHorizontalVelocity);

            actorObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            SetContextFrame(context, 1);
            runtime.OnTick(context);
            AssertVector(Vector3.right * 3f, motor.DebugChannels.DebugOwnerHorizontalVelocity);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void VelocityOverrideClip_ContextDirectionUsesStartupWorldDirection()
    {
        var actorObject = new GameObject("VelocityOverride Context Actor");
        try
        {
            Actor actor = actorObject.AddComponent<Actor>();
            ActorMotor motor = actorObject.AddComponent<ActorMotor>();
            actor.actorMotor = motor;

            var clip = CreateVelocityClip(useHorizontal: true, useVertical: false);
            clip.config.directionMode = MotionDirectionMode.FromContext;
            clip.config.horizontalSpeed = 5f;
            ActionSequenceClipRuntime runtime = clip.CreateRuntime();
            ActionSequenceContext context = CreateContext(actor, ActionContext.ForSelf(actor).WithDirection(Vector3.left));

            runtime.OnEnter(context);
            actorObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            SetContextFrame(context, 0);
            runtime.OnTick(context);

            AssertVector(Vector3.left * 5f, motor.DebugChannels.DebugOwnerHorizontalVelocity);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void VelocityOverrideClip_PoseRefreshDoesNotEnterOrUpdateMotionClip()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        try
        {
            asset.EditorSetTiming(60, 2);
            asset.EditorTracks.Clear();
            var track = new ActionSequenceMotionTrack();
            track.TryAddClip(CreateVelocityClip(useHorizontal: false, useVertical: true));
            asset.EditorTracks.Add(track);

            var runtime = new ActionSequenceRuntime(asset);
            var context = new ActionSequenceContext();

            Assert.IsTrue(runtime.ApplyPoseBaseline(context));
            Assert.IsTrue(runtime.RefreshPose(context, 0.5f));
            Assert.AreEqual(-1, runtime.CurrentFrame);
            Assert.IsFalse(runtime.HasOpenFrame);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void MotionTrack_AllowsVelocityOverrideClip()
    {
        var motionTrack = new ActionSequenceMotionTrack();
        Assert.IsTrue(motionTrack.AllowsClipType(typeof(ActionSequenceVelocityOverrideClipDefinition)));
    }

    private static ActionSequenceVelocityOverrideClipDefinition CreateVelocityClip(bool useHorizontal, bool useVertical)
    {
        return new ActionSequenceVelocityOverrideClipDefinition
        {
            startFrame = 0,
            endFrame = 2,
            config = new VelocityConfig
            {
                useHorizontalVelocity = useHorizontal,
                useVerticalVelocity = useVertical,
                directionMode = MotionDirectionMode.LocalHorizontal,
                localHorizontalDirection = Vector3.forward,
                horizontalSpeed = 1f,
                verticalSpeed = 1f,
            },
        };
    }

    private static ActionSequenceContext CreateContext(Actor actor, ActionContext actionContext = default)
    {
        var context = new ActionSequenceContext
        {
            Actor = actor,
            Context = actionContext,
        };
        SetContextFrameRate(context, 60);
        return context;
    }

    private static void SetContextFrame(ActionSequenceContext context, int frame)
    {
        typeof(ActionSequenceContext)
            .GetProperty(nameof(ActionSequenceContext.Frame), BindingFlags.Instance | BindingFlags.Public)
            .SetValue(context, frame);
    }

    private static void SetContextFrameRate(ActionSequenceContext context, int frameRate)
    {
        typeof(ActionSequenceContext)
            .GetProperty(nameof(ActionSequenceContext.FrameRate), BindingFlags.Instance | BindingFlags.Public)
            .SetValue(context, frameRate);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.That(Vector3.Distance(expected, actual), Is.LessThanOrEqualTo(0.0001f), $"Expected {expected}, got {actual}.");
    }
}
