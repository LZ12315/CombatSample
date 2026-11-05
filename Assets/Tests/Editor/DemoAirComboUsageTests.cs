#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class DemoAirComboUsageTests
{
    private readonly List<Object> _objects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i] != null)
                Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [Test]
    public void OpenerConsumesImmediately_ContinuationDoesNotConsume_OnlyLandingRestores()
    {
        var state = new DemoAirComboUsageState();
        ActionAsset opener = CreateAction();
        ActionAsset continuation = CreateAction();

        Assert.IsTrue(state.CanStart(true));
        Assert.IsTrue(state.CanStart(true), "Checking availability must not consume it.");
        state.RecordStart(opener, opener, true);
        Assert.AreEqual(1, state.StartsThisAirtime);
        Assert.IsFalse(state.CanStart(true));

        state.RecordStart(continuation, opener, true);
        Assert.AreEqual(1, state.StartsThisAirtime);
        Assert.IsFalse(state.CanStart(true), "Changing Action in midair is not a refund.");

        state.Land();
        Assert.AreEqual(0, state.StartsThisAirtime);
        Assert.IsFalse(state.CanStart(false), "Being grounded does not allow an air combo.");
        Assert.IsTrue(state.CanStart(true), "The next airtime has a fresh allowance.");
    }

    [Test]
    public void OnlyTheConfiguredOpenerStartedInAirIsCounted_ActorsDoNotShareUsage()
    {
        var first = new DemoAirComboUsageState();
        var second = new DemoAirComboUsageState();
        ActionAsset opener = CreateAction();
        ActionAsset other = CreateAction();

        first.RecordStart(other, opener, true);
        first.RecordStart(opener, opener, false);
        first.RecordStart(opener, null, true);
        Assert.AreEqual(0, first.StartsThisAirtime);
        first.RecordStart(opener, opener, true);
        Assert.IsFalse(first.CanStart(true));
        Assert.IsTrue(second.CanStart(true));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConditionRejectsMissingOrUnconfiguredTracker_EvenWhenInverted(bool invert)
    {
        var owner = new GameObject("Demo air combo condition actor");
        _objects.Add(owner);
        Actor actor = owner.AddComponent<Actor>();
        var condition = new DemoAirComboAvailableCondition { invertResult = invert };
        Assert.IsFalse(condition.Check(actor));
        owner.AddComponent<DemoAirComboUsage>();
        Assert.IsFalse(condition.Check(actor));
    }

    [Test]
    public void FailedStartDoesNotNotify_ValidStartNotifiesWithSynchronizedState()
    {
        ActionPlayer player = CreatePlayer(out Actor actor);
        int starts = 0;
        ActionInstance observedAction = null;
        ActionInstance observedCurrentAction = null;
        int observedFrame = -1;
        int observedDuration = -1;
        player.OnActionStarted += action =>
        {
            starts++;
            observedAction = action;
            observedCurrentAction = player.CurrentAction;
            observedFrame = player.CurrentFrame;
            observedDuration = player.TotalFrames;
        };
        ActionAsset invalid = CreateAction();
        var segment = new AnimationSegment();
        segment.EditorSetData(0, null, 0f, 1f, 1f);
        invalid.Timeline.EditorAnimationSegments.Add(segment);
        Assert.IsFalse(player.TryBeginAction(invalid, ActionContext.ForSelf(actor), out _));
        Assert.AreEqual(0, starts);
        Assert.IsTrue(player.TryBeginAction(CreateAction(), ActionContext.ForSelf(actor), out string reason), reason);
        Assert.AreEqual(1, starts);
        Assert.AreSame(observedAction, observedCurrentAction);
        Assert.AreEqual(0, observedFrame);
        Assert.AreEqual(observedAction.Config.Timeline.DurationFrames, observedDuration);
        player.StopAction();
        Assert.AreEqual(1, starts);
    }

    [Test]
    public void LoopReplayDoesNotNotifyAnotherStart_ExplicitReplacementDoes()
    {
        ActionPlayer player = CreatePlayer(out Actor actor);
        ActionAsset loop = CreateAction();
        JsonUtility.FromJsonOverwrite("{\"isLoop\":true}", loop);
        int starts = 0;
        player.OnActionStarted += _ => starts++;
        Assert.IsTrue(player.TryBeginAction(loop, ActionContext.ForSelf(actor), out string reason), reason);
        Assert.IsTrue(player.PlayActionFrame(CombatSimulationTiming.FixedDeltaTime));
        player.FinishActionFrame();
        Assert.IsTrue(player.PlayActionFrame(CombatSimulationTiming.FixedDeltaTime));
        player.FinishActionFrame();
        Assert.AreEqual(1, starts);
        Assert.IsTrue(player.TryBeginAction(CreateAction(), ActionContext.ForSelf(actor), out reason), reason);
        Assert.AreEqual(2, starts);
        player.StopAction();
    }

    private ActionPlayer CreatePlayer(out Actor actor)
    {
        var owner = new GameObject("Demo start notification actor");
        _objects.Add(owner);
        ActionPlayer player = owner.AddComponent<ActionPlayer>();
        actor = owner.AddComponent<Actor>();
        actor.actionPlayer = player;
        // Ordinary MonoBehaviour Awake is not guaranteed by EditMode tests.
        JsonUtility.FromJsonOverwrite($"{{\"_actor\":{{\"instanceID\":{actor.GetInstanceID()}}}}}", player);
        return player;
    }

    private ActionAsset CreateAction()
    {
        var action = ScriptableObject.CreateInstance<ActionAsset>();
        action.MotionPolicy.movement = ActionMovementControl.Allow;
        action.MotionPolicy.turning = ActionTurnControl.Normal;
        _objects.Add(action);
        return action;
    }
}
#endif
