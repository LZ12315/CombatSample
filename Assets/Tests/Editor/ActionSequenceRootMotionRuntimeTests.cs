using System.Reflection;
using KinematicCharacterController;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
    public void TranslationDomain_TrajectoryOwnerSurvivesZeroDeltaTick()
    {
        var translation = new TranslationDomain();
        MotionOwner owner = translation.BeginTrajectoryRootMotion();

        translation.BeginMotionTick();

        Assert.IsTrue(translation.HasTrajectoryRootMotionTick);
        Assert.AreEqual(Vector3.zero, translation.TrajectoryRootMotionLocalPosition);

        Assert.IsTrue(translation.EndTrajectoryRootMotion(owner));
        translation.BeginMotionTick();

        Assert.IsFalse(translation.HasTrajectoryRootMotionTick);
    }

    [Test]
    public void TranslationDomain_UsesRecoverableLifoTrajectoryOwnersWithoutCatchup()
    {
        var translation = new TranslationDomain();
        MotionOwner a = translation.BeginTrajectoryRootMotion();
        translation.SubmitTrajectoryRootMotion(a, Vector3.forward);
        MotionOwner b = translation.BeginTrajectoryRootMotion();
        translation.SubmitTrajectoryRootMotion(a, Vector3.right * 2f);
        translation.SubmitTrajectoryRootMotion(b, Vector3.left);

        translation.BeginMotionTick();

        Assert.IsTrue(translation.HasTrajectoryRootMotionTick);
        Assert.AreEqual(Vector3.left, translation.TrajectoryRootMotionLocalPosition);

        translation.SubmitTrajectoryRootMotion(a, Vector3.forward * 3f);
        translation.EndTrajectoryRootMotion(b);
        translation.BeginMotionTick();

        Assert.IsTrue(translation.HasTrajectoryRootMotionTick);
        Assert.AreEqual(Vector3.forward * 3f, translation.TrajectoryRootMotionLocalPosition);
    }

    [Test]
    public void RotationDomain_UsesRecoverableLifoOwnersAndIgnoresStaleToken()
    {
        var rotation = new RotationDomain();
        Assert.IsTrue(rotation.BeginScriptedRotation(out MotionOwner first));
        Assert.IsTrue(rotation.BeginScriptedRotation(out MotionOwner second));

        Assert.IsFalse(rotation.SubmitScriptedRotation(new MotionOwner(first.Id + 100), Quaternion.Euler(0f, 90f, 0f)));
        Assert.IsTrue(rotation.SubmitScriptedRotation(first, Quaternion.Euler(0f, 30f, 0f)));
        Assert.IsTrue(rotation.SubmitScriptedRotation(second, Quaternion.Euler(0f, 60f, 0f)));
        rotation.BeginMotionTick();

        Assert.IsTrue(rotation.HasScriptedRotationTick);
        AssertQuaternion(Quaternion.Euler(0f, 60f, 0f), rotation.ScriptedRotationLocalYawDelta);

        Assert.IsTrue(rotation.EndScriptedRotation(second));
        Assert.IsTrue(rotation.SubmitScriptedRotation(first, Quaternion.Euler(0f, 15f, 0f)));
        rotation.BeginMotionTick();

        AssertQuaternion(Quaternion.Euler(0f, 15f, 0f), rotation.ScriptedRotationLocalYawDelta);
    }

    [Test]
    public void RotationDomain_UsesFixedScriptedRootLocomotionPrecedence()
    {
        var rotation = new RotationDomain();
        Quaternion tickStart = Quaternion.Euler(0f, 10f, 0f);
        Quaternion locomotion = Quaternion.Euler(0f, 80f, 0f);

        Assert.IsTrue(rotation.BeginRootRotation(out MotionOwner root));
        rotation.SubmitRootRotation(root, Quaternion.Euler(0f, 20f, 0f));
        rotation.BeginMotionTick();
        rotation.Prepare(tickStart, locomotion);
        AssertQuaternion(Quaternion.Euler(0f, 30f, 0f), rotation.RequestedRotation);

        Assert.IsTrue(rotation.BeginScriptedRotation(out MotionOwner scripted));
        rotation.SubmitScriptedRotation(scripted, Quaternion.Euler(0f, 40f, 0f));
        rotation.BeginMotionTick();
        rotation.Prepare(tickStart, locomotion);
        AssertQuaternion(Quaternion.Euler(0f, 50f, 0f), rotation.RequestedRotation);

        rotation.EndScriptedRotation(scripted);
        rotation.EndRootRotation(root);
        rotation.BeginMotionTick();
        rotation.Prepare(tickStart, locomotion);
        AssertQuaternion(locomotion, rotation.RequestedRotation);
    }

    [Test]
    public void ActorMotor_ScriptedRotationOverridesLocomotionAndUsesTickStartRotation()
    {
        var gameObject = new GameObject("ActorMotorScriptedRotationTest");
        try
        {
            var motor = gameObject.AddComponent<ActorMotor>();
            gameObject.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            SyncKccPose(gameObject);
            Assert.IsTrue(motor.BeginScriptedRotation(out MotionOwner owner));
            Assert.IsTrue(motor.SubmitScriptedRotation(owner, Quaternion.Euler(0f, 60f, 0f)));

            Quaternion rotation = ConsumePreparedRotation(gameObject, motor);

            AssertQuaternion(Quaternion.Euler(0f, 75f, 0f), rotation);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void SelfRotationClip_TargetMissingHoldsCurrentYawAndWarnsOnce()
    {
        var actorObject = new GameObject("SelfRotation MissingTarget Actor");
        var clip = new ActionSequenceSelfRotationClipDefinition { startFrame = 0, endFrame = 2 };
        try
        {
            Actor actor = actorObject.AddComponent<Actor>();
            ActorMotor motor = actorObject.AddComponent<ActorMotor>();
            actor.actorMotor = motor;
            actorObject.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            SyncKccPose(actorObject);
            SetPrivateField(clip, "source", SelfRotationSource.Target);
            SetPrivateField(clip, "targetSource", SelfRotationTargetSource.ContextTarget);

            ActionSequenceClipRuntime runtime = clip.CreateRuntime();
            var context = CreateContext(actor);
            runtime.OnEnter(context);

            LogAssert.Expect(
                LogType.Warning,
                "SelfRotationClip target source ContextTarget cannot rotate because target is missing.");

            SetContextFrame(context, 0);
            runtime.OnTick(context);
            Quaternion first = ConsumePreparedRotation(actorObject, motor);
            AssertQuaternion(Quaternion.Euler(0f, 35f, 0f), first);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void SelfRotationClip_RequiredContextFieldsFollowSelectedSource()
    {
        var clip = new ActionSequenceSelfRotationClipDefinition();

        SetPrivateField(clip, "source", SelfRotationSource.Target);
        SetPrivateField(clip, "targetSource", SelfRotationTargetSource.ContextInstigator);
        Assert.AreEqual(ActionContextFieldMask.Instigator, clip.RequiredContextFields);

        SetPrivateField(clip, "targetSource", SelfRotationTargetSource.ContextTarget);
        Assert.AreEqual(ActionContextFieldMask.Target, clip.RequiredContextFields);

        SetPrivateField(clip, "source", SelfRotationSource.Direction);
        SetPrivateField(clip, "directionSource", SelfRotationDirectionSource.ContextDirection);
        Assert.AreEqual(ActionContextFieldMask.Direction, clip.RequiredContextFields);

        SetPrivateField(clip, "directionSource", SelfRotationDirectionSource.PresetLocal);
        Assert.AreEqual(ActionContextFieldMask.None, clip.RequiredContextFields);
    }

    private static ActionSequenceContext CreateContext(Actor actor)
    {
        var context = new ActionSequenceContext
        {
            Actor = actor,
            Context = ActionContext.None,
        };
        SetContextFrameRate(context, 60);
        return context;
    }

    private static Quaternion ConsumePreparedRotation(GameObject actorObject, ActorMotor motor)
    {
        KinematicCharacterMotor kcc = SyncKccPose(actorObject);
        motor.BeforeCharacterUpdate(DeltaTime);
        Quaternion rotation = actorObject.transform.rotation;
        motor.UpdateRotation(ref rotation, DeltaTime);
        kcc.SetPositionAndRotation(actorObject.transform.position, rotation);
        return rotation;
    }

    private static KinematicCharacterMotor SyncKccPose(GameObject actorObject)
    {
        KinematicCharacterMotor kcc = actorObject.GetComponent<KinematicCharacterMotor>();
        if (kcc == null)
            kcc = actorObject.AddComponent<KinematicCharacterMotor>();

        kcc.SetPositionAndRotation(actorObject.transform.position, actorObject.transform.rotation);
        return kcc;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
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

    private static void AssertQuaternion(Quaternion expected, Quaternion actual)
    {
        Assert.LessOrEqual(
            Quaternion.Angle(expected, actual),
            0.01f,
            $"Expected {expected.eulerAngles}, got {actual.eulerAngles}.");
    }
}
