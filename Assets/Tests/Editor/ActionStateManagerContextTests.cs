using System;
using System.Collections.Generic;
using System.Reflection;
using DeiveEx.TagTree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ActionStateManagerContextTests
{
    private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        ContextRecordingCondition.Contexts.Clear();
        ContextRecordingCondition.ClaimCount = 0;

        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] != null)
                UnityEngine.Object.DestroyImmediate(_objects[i]);
        }

        _objects.Clear();
    }

    [Test]
    public void LateUpdate_NoLongerOwnsActionArbitration()
    {
        MethodInfo lateUpdate = typeof(ActionStateManager).GetMethod(
            "LateUpdate",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        Assert.IsNull(lateUpdate);
    }

    [Test]
    public void PollCandidate_FreezesContextFromPendingLocomotionIntent()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateAction("Poll Locomotion", ActionTriggerMode.Poll);
        SetPrivateField(action, "_startContextMode", ActionStartContextMode.LocomotionIntent);
        rig.SetActionList(action);

        rig.Locomotion.SetLocomotionIntent(new LocomotionIntent
        {
            WorldMoveDirection = Vector3.right,
            MoveStrength = 1f,
            FacingDirection = Vector3.zero,
        });
        rig.Locomotion.BeginControlTick();

        rig.RunActionStateManager();

        Assert.IsNotNull(rig.Player.CurrentAction);
        Assert.IsTrue(rig.Player.CurrentAction.Context.HasDirection);
        Assert.That(rig.Player.CurrentAction.Context.Direction.x, Is.GreaterThan(0.99f));
        Assert.IsTrue(rig.Player.CurrentAction.Context.HasMagnitude);
        Assert.That(rig.Player.CurrentAction.Context.Magnitude, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void PollCandidate_MissingPendingLocomotionIntentInvalidatesLocomotionContext()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateAction("Poll Locomotion Missing Input", ActionTriggerMode.Poll);
        SetPrivateField(action, "_startContextMode", ActionStartContextMode.LocomotionIntent);
        rig.SetActionList(action);

        rig.Locomotion.BeginControlTick();
        rig.RunActionStateManager();

        Assert.IsNull(rig.Player.CurrentAction);
    }

    [Test]
    public void LocomotionControlSnapshot_LateSubmissionBelongsToNextTick()
    {
        TestRig rig = CreateRig();
        rig.Locomotion.SetLocomotionIntent(new LocomotionIntent
        {
            WorldMoveDirection = Vector3.right,
            MoveStrength = 1f,
        });
        rig.Locomotion.BeginControlTick();
        rig.Locomotion.SetLocomotionIntent(new LocomotionIntent
        {
            WorldMoveDirection = Vector3.left,
            MoveStrength = 0.5f,
        });

        Assert.IsTrue(rig.Locomotion.TryGetControlIntent(out LocomotionIntent controlIntent));
        Assert.That(controlIntent.WorldMoveDirection.x, Is.EqualTo(1f));

        var context = new LocomotionMotionContext(0.02f, true, Vector3.up,
            Quaternion.identity, default, default);
        LocomotionMotionRequest first = rig.Locomotion.BuildMotionRequest(context);
        Assert.That(first.WorldPlanarVelocity.x, Is.EqualTo(0.4f).Within(0.001f));

        rig.Locomotion.BeginControlTick();
        LocomotionMotionRequest second = rig.Locomotion.BuildMotionRequest(context);
        Assert.That(second.WorldPlanarVelocity.x, Is.EqualTo(-0.15f).Within(0.001f));
    }

    [Test]
    public void LocomotionControlRelease_BrakesWhileDisableResetsSharedVelocity()
    {
        TestRig rig = CreateRig();
        var context = new LocomotionMotionContext(0.02f, true, Vector3.up, Quaternion.identity, default, default);
        for (int i = 0; i < 3; i++)
        {
            rig.Locomotion.SetLocomotionIntent(new LocomotionIntent { WorldMoveDirection = Vector3.right, MoveStrength = 1f });
            rig.Locomotion.BeginControlTick();
            rig.Locomotion.BuildMotionRequest(context);
        }
        float speed = rig.Locomotion.DebugLocomotionVelocity.x;
        LocomotionMotionRequest prepared = rig.Locomotion.BuildMotionRequest(context);
        rig.Locomotion.ClearLocomotionIntent();
        Assert.That(rig.Locomotion.DebugLocomotionVelocity.x, Is.EqualTo(speed));
        Assert.That(rig.Locomotion.BuildMotionRequest(context).WorldPlanarVelocity,
            Is.EqualTo(prepared.WorldPlanarVelocity), "Releasing input must not reopen an integrated tick.");
        rig.Locomotion.BeginControlTick();
        Assert.That(rig.Locomotion.TryGetControlIntent(out _), Is.False);
        float brakingSpeed = rig.Locomotion.BuildMotionRequest(context).WorldPlanarVelocity.x;
        Assert.That(brakingSpeed, Is.GreaterThan(0f).And.LessThan(speed));
        rig.Locomotion.enabled = false;
        Assert.That(rig.Locomotion.DebugLocomotionVelocity, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void LocomotionFreeze_PreservesIntentUntilPositiveMotionTick()
    {
        TestRig rig = CreateRig();
        rig.Locomotion.SetLocomotionIntent(new LocomotionIntent
        {
            WorldMoveDirection = Vector3.right,
            MoveStrength = 1f,
        });
        rig.Locomotion.BeginControlTick();

        LocomotionMotionRequest frozen = rig.Locomotion.BuildMotionRequest(
            new LocomotionMotionContext(0f, true, Vector3.up, Quaternion.identity, default, default));
        Assert.IsFalse(frozen.HasVelocity);

        rig.Locomotion.BeginControlTick();
        Assert.IsTrue(rig.Locomotion.TryGetControlIntent(out _));
        var running = new LocomotionMotionContext(0.02f, true, Vector3.up,
            Quaternion.identity, default, default);
        Assert.That(rig.Locomotion.BuildMotionRequest(running).WorldPlanarVelocity.x,
            Is.EqualTo(0.4f).Within(0.001f));

        rig.Locomotion.BeginControlTick();
        Assert.IsFalse(rig.Locomotion.TryGetControlIntent(out _));
        Assert.That(rig.Locomotion.BuildMotionRequest(running).WorldPlanarVelocity.sqrMagnitude,
            Is.EqualTo(0f));

        rig.Locomotion.SetLocomotionIntent(new LocomotionIntent
        {
            WorldMoveDirection = Vector3.left,
            MoveStrength = 1f,
        });
        rig.Locomotion.BeginControlTick();
        rig.Locomotion.ClearLocomotionIntent();
        Assert.IsTrue(rig.Locomotion.TryGetControlIntent(out _), "Release cannot change the locked snapshot.");
        rig.Locomotion.BuildMotionRequest(new LocomotionMotionContext(0f, true, Vector3.up,
            Quaternion.identity, default, default));
        rig.Locomotion.BeginControlTick();
        Assert.IsFalse(rig.Locomotion.TryGetControlIntent(out _));
        rig.Locomotion.SetLocomotionIntent(LocomotionIntent.Idle);
        rig.Locomotion.enabled = false;
        rig.Locomotion.enabled = true;
        rig.Locomotion.BeginControlTick();
        Assert.IsFalse(rig.Locomotion.TryGetControlIntent(out _));
    }

    [Test]
    public void MotorCompose_PolicyAndOwnerPriorityKeepRequestedSeparateFromActual()
    {
        TestRig rig = CreateRig();
        ActorMotor motor = rig.Motor;
        var locomotionRequest = new LocomotionMotionRequest(Vector3.right * 4f,
            Quaternion.identity, true, true);

        MotionOwner policy = motor.BeginLocomotionScale(0f);
        motor.BeginMotion(0.02f);
        motor.ComposeMotion(locomotionRequest);
        Assert.That(motor.RequestedVelocity.x, Is.EqualTo(0f).Within(0.001f));
        motor.CancelPreparedMotion();
        motor.EndLocomotionScale(policy);

        MotionOwner velocityOwner = motor.BeginHorizontalVelocity();
        motor.SetHorizontalVelocity(velocityOwner, Vector3.left * 3f);
        MotionOwner rootOwner = motor.BeginTrajectoryRootMotion();
        motor.SubmitTrajectoryRootMotion(rootOwner, Vector3.right * 0.2f);
        motor.BeginMotion(0.02f);
        motor.ComposeMotion(locomotionRequest);
        Assert.That(motor.RequestedVelocity.x, Is.EqualTo(-3f).Within(0.001f));

        motor.EndHorizontalVelocity(velocityOwner);
        motor.PublishWorldResult();
        Assert.That(motor.LastMotionResult.HorizontalSource,
            Is.EqualTo(HorizontalMotionSource.HorizontalVelocityOwner));
        Assert.That(motor.ActualSolvedVelocity.x, Is.EqualTo(0f).Within(0.001f));
        motor.EndTrajectoryRootMotion(rootOwner);

        rootOwner = motor.BeginTrajectoryRootMotion();
        motor.SubmitTrajectoryRootMotion(rootOwner, Vector3.right * 0.1f);
        motor.BeginMotion(0.02f);
        motor.ComposeMotion(locomotionRequest);
        Assert.That(motor.RequestedVelocity.x, Is.EqualTo(5f).Within(0.001f));
        motor.CancelPreparedMotion();
        motor.EndTrajectoryRootMotion(rootOwner);

        motor.SetMovementTimeScale(0.5f);
        LocomotionMotionContext halfSpeed = motor.BeginMotion(0.02f);
        Assert.That(halfSpeed.EffectiveDeltaTime, Is.EqualTo(0.01f).Within(0.0001f));
        motor.ComposeMotion(locomotionRequest);
        Assert.That(motor.RequestedVelocity.x, Is.EqualTo(2f).Within(0.001f));
        motor.CancelPreparedMotion();

        motor.SetMovementTimeScale(0f);
        Assert.That(motor.BeginMotion(0.02f).EffectiveDeltaTime, Is.Zero);
        motor.ComposeMotion(default);
        Assert.That(motor.RequestedVelocity.sqrMagnitude, Is.Zero);
        motor.CancelPreparedMotion();
    }

    [Test]
    public void EventCandidates_KeepTheContextFromTheWinningSubmission()
    {
        TestRig rig = CreateRig();
        Tag eventTag = CreateTag("Tests.ActionContext.Event");
        TagReference eventRef = CreateTagReference(eventTag);
        ActionAsset high = CreateAction("High Event", ActionTriggerMode.Event, priorityValue: 10);
        ActionAsset low = CreateAction("Low Event", ActionTriggerMode.Event, priorityValue: 0);
        SetPrivateField(high, "_eventTriggerTag", eventRef);
        SetPrivateField(low, "_eventTriggerTag", eventRef);
        rig.SetActionList(high, low);
        rig.RebuildEventMap();

        rig.Asm.SendEvent(eventTag, ActionContext.ForSelf(rig.Actor).WithMagnitude(1f));
        rig.Asm.SendEvent(eventTag, ActionContext.ForSelf(rig.Actor).WithMagnitude(2f));
        rig.RunActionStateManager();

        Assert.AreSame(high, rig.Player.CurrentAction.Config);
        Assert.IsTrue(rig.Player.CurrentAction.Context.HasMagnitude);
        Assert.AreEqual(1f, rig.Player.CurrentAction.Context.Magnitude);
    }

    [Test]
    public void ExternalRequest_WaitsUntilFixedDecideAction()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateAction("External Deferred", ActionTriggerMode.Poll);

        bool? result = null;
        rig.Asm.RequestExternalAction(action, ActionContext.ForSelf(rig.Actor), value => result = value);

        Assert.IsNull(result);
        Assert.IsNull(rig.Player.CurrentAction);

        rig.RunActionStateManager();

        Assert.AreEqual(true, result);
        Assert.AreSame(action, rig.Player.CurrentAction.Config);
    }

    [Test]
    public void ExternalRequest_RuntimeBeginFailureReportsFalseAndDoesNotClaimEntry()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateInvalidActionRuntimeAction("Invalid ActionRuntime");
        bool? result = null;
        LogAssert.Expect(
            LogType.Warning,
            $"Action '{action.name}' failed to start: ActionRuntime animation snapshot contains an invalid AnimationSegment.");

        rig.Asm.RequestExternalAction(
            action,
            ActionContext.ForSelf(rig.Actor),
            value => result = value);
        rig.RunActionStateManager();

        Assert.AreEqual(false, result);
        Assert.IsNull(rig.Player.CurrentAction);
        Assert.AreEqual(0, ContextRecordingCondition.ClaimCount);
    }

    [Test]
    public void ExternalRequest_ValidActionRuntimeActionUsesFixedSessionAndCompletes()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateValidActionRuntimeAction("Valid ActionRuntime");
        bool? result = null;

        rig.Asm.RequestExternalAction(
            action,
            ActionContext.ForSelf(rig.Actor),
            value => result = value);
        rig.RunActionStateManager();

        Assert.AreEqual(true, result);
        Assert.IsNotNull(rig.Player.CurrentAction);
        Assert.AreSame(action, rig.Player.CurrentAction.Config);
        Assert.AreEqual(1, ContextRecordingCondition.ClaimCount);

        Assert.IsTrue(rig.Player.PlayActionFrame(CombatSimulationTiming.FixedDeltaTime));
        rig.Player.FinishActionFrame();

        Assert.IsNull(rig.Player.CurrentAction);
    }

    [Test]
    public void TryBeginAction_RuntimeBeginFailureCleansUpReplacement()
    {
        TestRig rig = CreateRig();
        ActionAsset current = CreateAction("Current", ActionTriggerMode.Poll);
        ActionAsset invalid = CreateInvalidActionRuntimeAction("Invalid Replacement");
        rig.Player.BeginAction(current, ActionContext.ForSelf(rig.Actor));

        bool started = rig.Player.TryBeginAction(
            invalid,
            ActionContext.ForSelf(rig.Actor),
            out string failureReason);

        Assert.IsFalse(started);
        StringAssert.Contains("failed to start", failureReason);
        Assert.IsNull(rig.Player.CurrentAction);
    }

    [Test]
    public void ExternalRequests_ReportOnlyTheExactWinningRequest()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateAction("External", ActionTriggerMode.Poll);

        bool? first = null;
        bool? second = null;
        rig.Asm.RequestExternalAction(action, ActionContext.ForSelf(rig.Actor).WithMagnitude(1f), result => first = result);
        rig.Asm.RequestExternalAction(action, ActionContext.ForSelf(rig.Actor).WithMagnitude(2f), result => second = result);

        rig.RunActionStateManager();

        Assert.AreEqual(true, first);
        Assert.AreEqual(false, second);
        Assert.AreSame(action, rig.Player.CurrentAction.Config);
        Assert.AreEqual(1f, rig.Player.CurrentAction.Context.Magnitude);
    }

    [Test]
    public void ExternalRequest_OutsideCancelWindowFailsOnceAndDoesNotPersist()
    {
        TestRig rig = CreateRig();
        ActionAsset current = CreateAction("Current", ActionTriggerMode.Poll);
        ActionAsset requested = CreateAction("Requested", ActionTriggerMode.Poll);

        rig.Player.BeginAction(current, ActionContext.ForSelf(rig.Actor));

        int callbackCount = 0;
        bool? result = null;
        rig.Asm.RequestExternalAction(requested, ActionContext.ForSelf(rig.Actor), value =>
        {
            callbackCount++;
            result = value;
        });

        rig.RunActionStateManager();
        rig.RunActionStateManager();

        Assert.AreEqual(1, callbackCount);
        Assert.AreEqual(false, result);
        Assert.AreSame(current, rig.Player.CurrentAction.Config);
    }

    [Test]
    public void ExternalRequest_SubmittedDuringCallbackWaitsForNextDecideAction()
    {
        TestRig rig = CreateRig();
        ActionAsset firstAction = CreateAction("First External", ActionTriggerMode.Poll, priorityValue: 10);
        ActionAsset secondAction = CreateAction("Second External", ActionTriggerMode.Poll, priorityValue: 20);

        bool? first = null;
        bool? second = null;
        rig.Asm.RequestExternalAction(firstAction, ActionContext.ForSelf(rig.Actor), value =>
        {
            first = value;
            rig.Asm.RequestExternalAction(secondAction, ActionContext.ForSelf(rig.Actor), next => second = next);
        });

        rig.RunActionStateManager();

        Assert.AreEqual(true, first);
        Assert.IsNull(second);
        Assert.AreSame(firstAction, rig.Player.CurrentAction.Config);

        rig.Player.StopAction();
        rig.RunActionStateManager();

        Assert.AreEqual(true, second);
        Assert.AreSame(secondAction, rig.Player.CurrentAction.Config);
    }

    [Test]
    public void ExternalCallbackException_DoesNotBlockOtherCallbacks()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateAction("External Callback Exception", ActionTriggerMode.Poll);

        bool? second = null;
        LogAssert.Expect(LogType.Exception, "InvalidOperationException: Injected callback exception.");

        rig.Asm.RequestExternalAction(
            action,
            ActionContext.ForSelf(rig.Actor),
            _ => throw new InvalidOperationException("Injected callback exception."));
        rig.Asm.RequestExternalAction(action, ActionContext.ForSelf(rig.Actor), value => second = value);

        rig.RunActionStateManager();

        Assert.AreEqual(false, second);
        Assert.AreSame(action, rig.Player.CurrentAction.Config);
    }

    [Test]
    public void Disable_FailsQueuedExternalRequestExactlyOnce()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateAction("External Disable", ActionTriggerMode.Poll);

        int callbackCount = 0;
        bool? result = null;
        rig.Asm.RequestExternalAction(action, ActionContext.ForSelf(rig.Actor), value =>
        {
            callbackCount++;
            result = value;
        });

        rig.DisableActionStateManager();

        Assert.AreEqual(1, callbackCount);
        Assert.AreEqual(false, result);
        Assert.IsNull(rig.Player.CurrentAction);
    }

    [Test]
    public void ActionRuntimeCandidateWithMissingFutureContext_CanStart()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateValidActionRuntimeAction("ActionRuntime Requires Direction");
        var lane = new GameplayLane();
        var impulse = new ImpulseItem();
        impulse.Config.useHorizontalImpulse = true;
        impulse.Config.impulse.directionMode = ImpulseDirectionMode.FromContext;
        lane.EditorItems.Add(impulse);
        action.Timeline.EditorGameplayLanes.Add(lane);
        rig.SetActionList(action);

        rig.RunActionStateManager();

        Assert.AreSame(action, rig.Player.CurrentAction.Config);
        Assert.AreEqual(1, ContextRecordingCondition.ClaimCount);
    }

    [Test]
    public void EntryConditionReceivesCandidateContext()
    {
        TestRig rig = CreateRig();
        ActionAsset action = CreateAction("External Context", ActionTriggerMode.Poll);

        ActionContext context = ActionContext.ForSelf(rig.Actor).WithMagnitude(7f);
        rig.Asm.RequestExternalAction(action, context, _ => { });
        rig.RunActionStateManager();

        Assert.AreEqual(1, ContextRecordingCondition.Contexts.Count);
        Assert.AreEqual(7f, ContextRecordingCondition.Contexts[0].Magnitude);
    }

    [Test]
    public void CancelSpecificTarget_MatchesIdentityWithoutFilteringEventMode()
    {
        ActionAsset target = CreateAction("Specific Event Target", ActionTriggerMode.Event);
        ActionAsset other = CreateAction("Other Target", ActionTriggerMode.Poll);
        var rule = new CancelRule { targetKind = CancelTargetKind.SpecificAction, specificTarget = target };

        Assert.IsTrue(rule.MatchesTarget(target));
        Assert.IsFalse(rule.MatchesTarget(other));
        Assert.IsFalse(rule.MatchesTarget(null));
    }

    [TestCase(ActionTriggerMode.Poll, true)]
    [TestCase(ActionTriggerMode.Event, false)]
    public void CancelAnyTarget_ExcludesEventActions(ActionTriggerMode triggerMode, bool expected)
    {
        ActionAsset target = CreateAction("Any Target", triggerMode);
        var rule = new CancelRule { targetKind = CancelTargetKind.Any };

        Assert.AreEqual(expected, rule.MatchesTarget(target));
        Assert.IsFalse(rule.MatchesTarget(null));
    }

    [Test]
    public void CancelTagTarget_MatchesSelfTagHierarchyAndRejectsMissingTags()
    {
        Tag child = CreateTag("Test.Cancel.Target.Child");
        Tag parent = Tag.GetTagFromFullName("Test.Cancel.Target");
        ActionAsset target = CreateAction("Tagged Target", ActionTriggerMode.Poll);
        SetPrivateField(target, "_selfTags", new List<TagReference> { null, CreateTagReference(child) });
        var rule = new CancelRule
        {
            targetKind = CancelTargetKind.AnyWithTag,
            targetTag = CreateTagReference(parent),
        };

        Assert.IsTrue(rule.MatchesTarget(target));
        rule.targetTag = CreateTagReference(child);
        Assert.IsTrue(rule.MatchesTarget(target));
        rule.targetTag = null;
        Assert.IsFalse(rule.MatchesTarget(target));
        rule.targetTag = CreateTagReference(parent);
        SetPrivateField(target, "_selfTags", new List<TagReference>());
        Assert.IsFalse(rule.MatchesTarget(target));
    }

    private TestRig CreateRig()
    {
        var owner = new GameObject("ActionStateManagerContextTests Actor");
        _objects.Add(owner);

        Actor actor = owner.AddComponent<Actor>();
        ActorMotor motor = owner.AddComponent<ActorMotor>();
        ActorLocomotion locomotion = owner.AddComponent<ActorLocomotion>();
        LocomotionSetAsset locomotionAsset = ScriptableObject.CreateInstance<LocomotionSetAsset>();
        _objects.Add(locomotionAsset);
        SetPrivateField(locomotion, "locomotionAssets", new List<LocomotionAsset> { locomotionAsset });
        ActionPlayer player = owner.AddComponent<ActionPlayer>();
        ActionStateManager asm = owner.AddComponent<ActionStateManager>();

        actor.actorMotor = motor;
        actor.actorLocomotion = locomotion;
        actor.actionPlayer = player;
        actor.actionManager = asm;
        SetPrivateField(player, "_actor", actor);
        SetPrivateField(asm, "_actor", actor);

        InvokePrivate(player, "Awake");
        InvokePrivate(asm, "Awake");

        return new TestRig(actor, motor, locomotion, player, asm, this);
    }

    private ActionAsset CreateAction(
        string name,
        ActionTriggerMode triggerMode,
        int priorityValue = 0)
    {
        ActionAsset action = ScriptableObject.CreateInstance<ActionAsset>();
        action.name = name;
        _objects.Add(action);

        SetPrivateField(action, "_triggerMode", triggerMode);
        SetPrivateField(action, "_priorityValue", priorityValue);
        SetPrivateField(action, "_entryConditions", new List<ActionCondition> { new ContextRecordingCondition() });
        return action;
    }

    private ActionAsset CreateInvalidActionRuntimeAction(string name)
    {
        ActionAsset action = CreateValidActionRuntimeAction(name);
        var segment = new AnimationSegment();
        segment.EditorSetData(0, null, 0f, 1f, 1f);
        action.Timeline.EditorAnimationSegments.Add(segment);
        return action;
    }

    private ActionAsset CreateValidActionRuntimeAction(string name)
    {
        ActionAsset action = ScriptableObject.CreateInstance<ActionAsset>();
        action.name = name;
        _objects.Add(action);
        SetPrivateField(action, "_triggerMode", ActionTriggerMode.Poll);
        SetPrivateField(action, "_entryConditions", new List<ActionCondition> { new ContextRecordingCondition() });
        return action;
    }

    private static Tag CreateTag(string fullName)
    {
        Type tagManagerType = typeof(Tag).Assembly.GetType("DeiveEx.TagTree.TagManager");
        MethodInfo loadMethod = tagManagerType.GetMethod(
            "LoadTagsFromNames",
            BindingFlags.Static | BindingFlags.NonPublic);
        loadMethod.Invoke(null, new object[] { new[] { fullName } });
        return Tag.GetTagFromFullName(fullName);
    }

    private static TagReference CreateTagReference(Tag tag)
    {
        var reference = new TagReference();
        SetPrivateField(reference, "TagId", tag.Id);
        return reference;
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, arguments);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(target, value);
    }

    private sealed class TestRig
    {
        private readonly ActionStateManagerContextTests _owner;

        public TestRig(
            Actor actor,
            ActorMotor motor,
            ActorLocomotion locomotion,
            ActionPlayer player,
            ActionStateManager asm,
            ActionStateManagerContextTests owner)
        {
            Actor = actor;
            Motor = motor;
            Locomotion = locomotion;
            Player = player;
            Asm = asm;
            _owner = owner;
        }

        public Actor Actor { get; }
        public ActorMotor Motor { get; }
        public ActorLocomotion Locomotion { get; }
        public ActionPlayer Player { get; }
        public ActionStateManager Asm { get; }

        public void SetActionList(params ActionAsset[] actions)
        {
            ActionAssetList list = ScriptableObject.CreateInstance<ActionAssetList>();
            _owner._objects.Add(list);
            SetPrivateField(list, "_globalActions", new List<ActionAsset>(actions));
            SetPrivateField(list, "_allActionsCache", null);
            SetPrivateField(Asm, "_actionList", list);
            RebuildEventMap();
        }

        public void RebuildEventMap()
        {
            InvokePrivate(Asm, "BuildEventMap");
        }

        public void RunActionStateManager()
        {
            Asm.DecideAction();
        }

        public void DisableActionStateManager()
        {
            InvokePrivate(Asm, "OnDisable");
        }
    }

    private sealed class ContextRecordingCondition : ActionCondition
    {
        public static readonly List<ActionContext> Contexts = new List<ActionContext>();
        public static int ClaimCount;

        protected override bool OnCheck(Actor actor)
        {
            return true;
        }

        protected override bool OnCheck(Actor actor, ActionContext context)
        {
            Contexts.Add(context);
            return true;
        }

        public override void OnClaim(Actor actor)
        {
            ClaimCount++;
        }
    }

}
