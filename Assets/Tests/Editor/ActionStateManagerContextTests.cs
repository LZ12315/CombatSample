using System;
using System.Collections.Generic;
using System.Reflection;
using DeiveEx.TagTree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
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
    public void PollCandidate_FreezesContextFromActorLogicInput()
    {
        TestRig rig = CreateRig(addLogicInput: true);
        ActionAsset action = CreateSequenceAction("Poll Locomotion", ActionTriggerMode.Poll);
        SetPrivateField(action, "_startContextMode", ActionStartContextMode.LocomotionIntent);
        rig.SetActionList(action);

        rig.LogicInput.InputMove(Vector2.right);
        InvokePrivate(rig.LogicInput, "Update");

        rig.RunActionStateManager();

        Assert.IsNotNull(rig.Player.CurrentAction);
        Assert.IsTrue(rig.Player.CurrentAction.Context.HasDirection);
        Assert.That(rig.Player.CurrentAction.Context.Direction.x, Is.GreaterThan(0.99f));
        Assert.IsTrue(rig.Player.CurrentAction.Context.HasMagnitude);
        Assert.That(rig.Player.CurrentAction.Context.Magnitude, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void PollCandidate_MissingActorLogicInputInvalidatesLocomotionContext()
    {
        TestRig rig = CreateRig(addLogicInput: false);
        ActionAsset action = CreateSequenceAction("Poll Locomotion Missing Input", ActionTriggerMode.Poll);
        SetPrivateField(action, "_startContextMode", ActionStartContextMode.LocomotionIntent);
        rig.SetActionList(action);

        LogAssert.Expect(
            LogType.Error,
            $"Action '{action.name}' uses StartContextMode.LocomotionIntent but Actor '{rig.Actor.name}' has no ActorLogicInput.");

        rig.RunActionStateManager();

        Assert.IsNull(rig.Player.CurrentAction);
    }

    [Test]
    public void EventCandidates_KeepTheContextFromTheWinningSubmission()
    {
        TestRig rig = CreateRig(addLogicInput: false);
        Tag eventTag = CreateTag("Tests.ActionContext.Event");
        TagReference eventRef = CreateTagReference(eventTag);
        ActionAsset high = CreateSequenceAction("High Event", ActionTriggerMode.Event, priorityValue: 10);
        ActionAsset low = CreateSequenceAction("Low Event", ActionTriggerMode.Event, priorityValue: 0);
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
        TestRig rig = CreateRig(addLogicInput: false);
        ActionAsset action = CreateSequenceAction("External Deferred", ActionTriggerMode.Poll);

        bool? result = null;
        rig.Asm.RequestExternalAction(action, ActionContext.ForSelf(rig.Actor), value => result = value);

        Assert.IsNull(result);
        Assert.IsNull(rig.Player.CurrentAction);

        rig.RunActionStateManager();

        Assert.AreEqual(true, result);
        Assert.AreSame(action, rig.Player.CurrentAction.Config);
    }

    [Test]
    public void ExternalRequests_ReportOnlyTheExactWinningRequest()
    {
        TestRig rig = CreateRig(addLogicInput: false);
        ActionAsset action = CreateSequenceAction("External", ActionTriggerMode.Poll);

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
        TestRig rig = CreateRig(addLogicInput: false);
        ActionAsset current = CreateSequenceAction("Current", ActionTriggerMode.Poll);
        ActionAsset requested = CreateSequenceAction("Requested", ActionTriggerMode.Poll);

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
        TestRig rig = CreateRig(addLogicInput: false);
        ActionAsset firstAction = CreateSequenceAction("First External", ActionTriggerMode.Poll, priorityValue: 10);
        ActionAsset secondAction = CreateSequenceAction("Second External", ActionTriggerMode.Poll, priorityValue: 20);

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
        TestRig rig = CreateRig(addLogicInput: false);
        ActionAsset action = CreateSequenceAction("External Callback Exception", ActionTriggerMode.Poll);

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
        TestRig rig = CreateRig(addLogicInput: false);
        ActionAsset action = CreateSequenceAction("External Disable", ActionTriggerMode.Poll);

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
    public void CandidateWithMissingRequiredContext_IsRejectedBeforeClaim()
    {
        TestRig rig = CreateRig(addLogicInput: false);
        ActionAsset action = CreateSequenceAction(
            "Requires Direction",
            ActionTriggerMode.Poll,
            priorityValue: 0,
            clips: new ActionSequenceClipDefinition[] { new RequiresDirectionClipDefinition() });
        rig.SetActionList(action);

        LogAssert.Expect(
            LogType.Error,
            $"Action '{action.name}' requires context fields Direction but the candidate context does not provide them.");

        rig.RunActionStateManager();

        Assert.IsNull(rig.Player.CurrentAction);
        Assert.AreEqual(0, ContextRecordingCondition.ClaimCount);
    }

    [Test]
    public void EntryConditionReceivesCandidateContext()
    {
        TestRig rig = CreateRig(addLogicInput: false);
        ActionAsset action = CreateSequenceAction("External Context", ActionTriggerMode.Poll);

        ActionContext context = ActionContext.ForSelf(rig.Actor).WithMagnitude(7f);
        rig.Asm.RequestExternalAction(action, context, _ => { });
        rig.RunActionStateManager();

        Assert.AreEqual(1, ContextRecordingCondition.Contexts.Count);
        Assert.AreEqual(7f, ContextRecordingCondition.Contexts[0].Magnitude);
    }

    private TestRig CreateRig(bool addLogicInput)
    {
        var owner = new GameObject("ActionStateManagerContextTests Actor");
        _objects.Add(owner);

        owner.AddComponent<PlayableDirector>();
        Actor actor = owner.AddComponent<Actor>();
        ActionPlayer player = owner.AddComponent<ActionPlayer>();
        ActionStateManager asm = owner.AddComponent<ActionStateManager>();
        ActorLogicInput logicInput = addLogicInput ? owner.AddComponent<ActorLogicInput>() : null;

        actor.actionPlayer = player;
        actor.actionManager = asm;
        SetPrivateField(player, "_actor", actor);
        SetPrivateField(asm, "_actor", actor);

        InvokePrivate(player, "Awake");
        if (logicInput != null)
            InvokePrivate(logicInput, "Awake");
        InvokePrivate(asm, "Awake");

        return new TestRig(actor, player, asm, logicInput, this);
    }

    private ActionAsset CreateSequenceAction(
        string name,
        ActionTriggerMode triggerMode,
        int priorityValue = 0,
        params ActionSequenceClipDefinition[] clips)
    {
        ActionAsset action = ScriptableObject.CreateInstance<ActionAsset>();
        action.name = name;
        _objects.Add(action);

        action.SetPlaybackBackend(ActionPlaybackBackend.Sequence);
        action.SequenceData.EditorSetTiming(60, 1);
        action.SequenceData.EditorTracks.Clear();
        if (clips != null && clips.Length > 0)
        {
            var track = new TestTrackDefinition(ActionSequenceTrackKind.State, clips);
            action.SequenceData.EditorTracks.Add(track);
        }

        SetPrivateField(action, "_triggerMode", triggerMode);
        SetPrivateField(action, "_priorityValue", priorityValue);
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
            ActionPlayer player,
            ActionStateManager asm,
            ActorLogicInput logicInput,
            ActionStateManagerContextTests owner)
        {
            Actor = actor;
            Player = player;
            Asm = asm;
            LogicInput = logicInput;
            _owner = owner;
        }

        public Actor Actor { get; }
        public ActionPlayer Player { get; }
        public ActionStateManager Asm { get; }
        public ActorLogicInput LogicInput { get; }

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

    private sealed class RequiresDirectionClipDefinition : ActionSequenceClipDefinition
    {
        public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.State;
        public override ActionContextFieldMask RequiredContextFields => ActionContextFieldMask.Direction;

        public override ActionSequenceClipRuntime CreateRuntime()
        {
            return new EmptyRuntime();
        }

        private sealed class EmptyRuntime : ActionSequenceClipRuntime
        {
        }
    }

    private sealed class TestTrackDefinition : ActionSequenceTrackDefinition
    {
        private static readonly Type[] ClipTypes = { typeof(RequiresDirectionClipDefinition) };
        private readonly ActionSequenceTrackKind _phase;

        public TestTrackDefinition(ActionSequenceTrackKind phase, params ActionSequenceClipDefinition[] clips)
        {
            _phase = phase;
            for (int i = 0; clips != null && i < clips.Length; i++)
                EditorClips.Add(clips[i]);
        }

        public override ActionSequenceTrackKind Kind => _phase;
        public override Type[] AllowedClipTypes => ClipTypes;
    }
}
