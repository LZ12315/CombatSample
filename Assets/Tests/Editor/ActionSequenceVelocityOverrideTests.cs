using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ActionSequenceVelocityOverrideTests
{
    [Test]
    public void TranslationDomain_BallisticSupportsCauseAgnosticAddAndSet()
    {
        var translation = new TranslationDomain();

        translation.AddBallisticVerticalVelocity(5f);
        translation.AddBallisticVerticalVelocity(-2f);
        Assert.AreEqual(3f, translation.BallisticVerticalVelocity);

        translation.SetBallisticVerticalVelocity(-7f);
        Assert.AreEqual(-7f, translation.BallisticVerticalVelocity);
    }

    [Test]
    public void TranslationDomain_VelocityOwnerRestoresCoveredOwner()
    {
        var translation = new TranslationDomain();
        MotionOwner a = translation.BeginHorizontalVelocity();
        translation.SetHorizontalVelocity(a, Vector3.right * 2f);
        MotionOwner b = translation.BeginHorizontalVelocity();
        translation.SetHorizontalVelocity(b, Vector3.left * 5f);

        Assert.AreEqual(Vector3.left * 5f, translation.ComposeHorizontal(Vector3.forward, 1f));

        translation.EndHorizontalVelocity(b);

        Assert.AreEqual(Vector3.right * 2f, translation.ComposeHorizontal(Vector3.forward, 1f));
    }

    [Test]
    public void TranslationDomain_VerticalOwnerFreezesAndResetsBallisticWhenLastOwnerEnds()
    {
        var translation = new TranslationDomain();
        translation.SetBallisticVerticalVelocity(5f);
        MotionOwner owner = translation.BeginVerticalVelocity();
        translation.SetVerticalVelocity(owner, 2f);

        translation.StepBallistic(1f, false, false, 1f, 0f);
        Assert.AreEqual(5f, translation.BallisticVerticalVelocity);
        Assert.AreEqual(2f, translation.ComposeVertical(1f));

        translation.EndVerticalVelocity(owner);
        Assert.AreEqual(0f, translation.BallisticVerticalVelocity);
        Assert.AreEqual(0f, translation.ComposeVertical(1f));
    }

    [Test]
    public void TranslationDomain_BackgroundHorizontalImpulseDecaysWhileCovered()
    {
        var translation = new TranslationDomain();
        translation.AddHorizontalImpulse(Vector3.right * 10f);
        MotionOwner owner = translation.BeginHorizontalVelocity();
        translation.SetHorizontalVelocity(owner, Vector3.zero);

        translation.StepHorizontalDrag(0.25f, 4f);
        translation.EndHorizontalVelocity(owner);

        Vector3 velocity = translation.ComposeHorizontal(Vector3.zero, 1f);
        Assert.That(velocity.x, Is.GreaterThan(0f).And.LessThan(10f));
    }

    [Test]
    public void MotionPolicyState_CombinesParameterOwnersByRule()
    {
        var policy = new MotionPolicyState();

        MotionOwner locomotionA = policy.BeginLocomotionScale(0.8f);
        MotionOwner locomotionB = policy.BeginLocomotionScale(0.5f);
        MotionOwner air = policy.BeginAirLocomotionScale(0.25f);
        MotionOwner gravityA = policy.BeginGravityScale(0.5f);
        MotionOwner gravityB = policy.BeginGravityScale(2f);

        Assert.AreEqual(0.5f, policy.LocomotionScale);
        Assert.AreEqual(0.25f, policy.AirLocomotionScale);
        Assert.AreEqual(1f, policy.GravityScale);

        Assert.IsTrue(policy.EndLocomotionScale(locomotionB));
        Assert.IsTrue(policy.UpdateGravityScale(gravityA, 0.25f));

        Assert.AreEqual(0.8f, policy.LocomotionScale);
        Assert.AreEqual(0.5f, policy.GravityScale);

        Assert.IsTrue(policy.EndLocomotionScale(locomotionA));
        Assert.IsTrue(policy.EndAirLocomotionScale(air));
        Assert.IsTrue(policy.EndGravityScale(gravityA));
        Assert.IsTrue(policy.EndGravityScale(gravityB));

        Assert.AreEqual(1f, policy.LocomotionScale);
        Assert.AreEqual(1f, policy.AirLocomotionScale);
        Assert.AreEqual(1f, policy.GravityScale);
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
            Assert.AreEqual(2f, motor.Translation.DebugOwnerVerticalVelocity);

            SetContextFrame(context, 1);
            runtime.OnTick(context);
            Assert.AreEqual(4f, motor.Translation.DebugOwnerVerticalVelocity);

            SetContextFrame(context, 2);
            runtime.OnTick(context);
            Assert.AreEqual(6f, motor.Translation.DebugOwnerVerticalVelocity);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
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
            AssertVector(Vector3.forward * 3f, motor.Translation.DebugOwnerHorizontalVelocity);

            actorObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            SetContextFrame(context, 1);
            runtime.OnTick(context);
            AssertVector(Vector3.right * 3f, motor.Translation.DebugOwnerHorizontalVelocity);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
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
            Object.DestroyImmediate(asset);
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
