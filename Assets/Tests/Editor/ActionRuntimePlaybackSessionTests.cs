#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Animancer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ActionRuntimePlaybackSessionTests
{
    private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] != null)
                UnityEngine.Object.DestroyImmediate(_objects[i]);
        }
        _objects.Clear();
    }

    [Test]
    public void Start_HandsAlreadyOpenedFrameZeroToFixedPipelineExactlyOnce()
    {
        var events = new List<string>();
        ActionAsset asset = CreateActionRuntimeAsset();
        AddLane(asset).EditorItems.Add(new ProbePointItem(events, 0));
        var action = asset.CreateActionInstance();
        var session = new ActionRuntimePlaybackSession(action, null, ActionContext.None);
        int completed = 0;
        session.Completed += _ => completed++;

        session.Start();

        CollectionAssert.AreEqual(new[] { "point:0" }, events);
        Assert.IsTrue(session.HasOpenFrame);
        Assert.IsTrue(session.TryPlayFrame(CombatSimulationTiming.FixedDeltaTime));
        CollectionAssert.AreEqual(new[] { "point:0" }, events);

        session.FinishFrame();

        Assert.AreEqual(1, completed);
        Assert.AreEqual(1d, session.NormalizedTime);
        session.Dispose();
    }

    [Test]
    public void HalfSpeed_UpdatesContinuousTimeWithoutRepeatingGameplayFrame()
    {
        var events = new List<string>();
        ActionAsset asset = CreateActionRuntimeAsset();
        GameplayLane lane = AddLane(asset);
        lane.EditorItems.Add(new ProbePointItem(events, 0));
        lane.EditorItems.Add(new ProbePointItem(events, 1));
        var session = new ActionRuntimePlaybackSession(asset.CreateActionInstance(), null, ActionContext.None);
        session.SetSpeed(0.5d);
        session.Start();
        session.FinishFrame();

        Assert.IsFalse(session.TryPlayFrame(CombatSimulationTiming.FixedDeltaTime));
        Assert.That(session.NormalizedTime, Is.GreaterThan(0d));
        CollectionAssert.AreEqual(new[] { "point:0" }, events);

        Assert.IsTrue(session.TryPlayFrame(CombatSimulationTiming.FixedDeltaTime));
        CollectionAssert.AreEqual(new[] { "point:0", "point:1" }, events);
        session.FinishFrame();
        session.Dispose();
    }

    [Test]
    public void Restart_DefersNextFrameZeroUntilNextActionAdvance()
    {
        var events = new List<string>();
        ActionAsset asset = CreateActionRuntimeAsset();
        AddLane(asset).EditorItems.Add(new ProbePointItem(events, 0));
        var session = new ActionRuntimePlaybackSession(asset.CreateActionInstance(), null, ActionContext.None);
        session.Completed += value => value.Restart();

        session.Start();
        session.FinishFrame();

        CollectionAssert.AreEqual(new[] { "point:0" }, events);
        Assert.IsTrue(session.IsPlaying);
        Assert.IsFalse(session.HasOpenFrame);
        Assert.AreEqual(0d, session.NormalizedTime);

        Assert.IsTrue(session.TryPlayFrame(CombatSimulationTiming.FixedDeltaTime));
        CollectionAssert.AreEqual(new[] { "point:0", "point:0" }, events);
        session.Dispose();
    }

    [Test]
    public void Stop_WithOpenFrame_UsesAbortCleanup()
    {
        var events = new List<string>();
        ActionAsset asset = CreateActionRuntimeAsset();
        AddLane(asset).EditorItems.Add(new ProbeRangeItem(events));
        var session = new ActionRuntimePlaybackSession(asset.CreateActionInstance(), null, ActionContext.None);

        session.Start();
        session.Stop(ActionPlaybackStopMode.Explicit);

        CollectionAssert.AreEqual(new[] { "enter", "tick", "abort" }, events);
        Assert.IsFalse(session.IsPlaying);
        session.Dispose();
    }

    [Test]
    public void CombinedMotionItems_PreserveVelocityOverrideAndReleaseOwnersOnAbort()
    {
        var actorObject = new GameObject("Combined Motion Actor");
        _objects.Add(actorObject);
        Actor actor = actorObject.AddComponent<Actor>();
        ActorMotor motor = actorObject.AddComponent<ActorMotor>();
        actor.actorMotor = motor;

        var clip = new AnimationClip();
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x",
            AnimationCurve.Linear(0f, 0f, 1f, 0f));
        _objects.Add(clip);
        var animation = ScriptableObject.CreateInstance<AnimationAsset>();
        animation.EditorSetClip(clip);
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(clip, 60, 1f, 1, "combined-motion-test",
            new[] { 0f, 1f }, new[] { Vector3.zero, Vector3.right },
            new[] { Quaternion.identity, Quaternion.identity });
        animation.EditorSetRootMotionData(trajectory);
        _objects.Add(animation);

        ActionAsset asset = CreateActionRuntimeAsset();
        var policy = new MotionPolicyItem();
        policy.EditorSetTiming(0, 3);
        policy.Config.useLocomotionScale = true;
        policy.Config.locomotionScale = 0f;
        AddLane(asset).EditorItems.Add(policy);

        var root = new RootMotionItem();
        root.EditorSetTiming(0, 3);
        root.Config.animationAsset = animation;
        AddLane(asset).EditorItems.Add(root);

        var velocity = new VelocityOverrideItem();
        velocity.EditorSetTiming(0, 3);
        velocity.Config.velocity.useHorizontalVelocity = true;
        velocity.Config.velocity.horizontalSpeed = 3f;
        AddLane(asset).EditorItems.Add(velocity);

        var session = new ActionRuntimePlaybackSession(asset.CreateActionInstance(), actor, ActionContext.None);
        try
        {
            session.Start();
            Assert.That(motor.MotionPolicy.LocomotionScale, Is.Zero);
            Assert.IsTrue(motor.Translation.HasTrajectoryRootMotionOwner);
            Assert.IsTrue(motor.Translation.HasHorizontalVelocityOwner);

            // Test Action ownership without requiring KCC startup or a physics world in EditMode.
            // A locomotion lock must not scale the velocity override owned by this Action.
            Vector3 composedVelocity = motor.Translation.ComposeHorizontal(Vector3.right * 4f, motor.MovementTimeScale);
            Assert.That(composedVelocity, Is.EqualTo(Vector3.forward * 3f));

            session.Stop(ActionPlaybackStopMode.Explicit);
            Assert.That(motor.MotionPolicy.LocomotionScale, Is.EqualTo(1f));
            Assert.IsFalse(motor.Translation.HasTrajectoryRootMotionOwner);
            Assert.IsFalse(motor.Translation.HasHorizontalVelocityOwner);
            Assert.That(motor.Translation.ComposeHorizontal(Vector3.right * 4f, motor.MovementTimeScale),
                Is.EqualTo(Vector3.right * 4f), "Stopping the Action restores fallback motion.");
        }
        finally
        {
            session.Dispose();
        }
    }

    [Test]
    public void ZeroSpeedAndPause_ResubmitTheCurrentPoseToActorAnimation()
    {
        var actorObject = new GameObject("ActionRuntime Frozen Pose Actor");
        _objects.Add(actorObject);
        var probe = new GameObject("PoseProbe");
        probe.transform.SetParent(actorObject.transform, false);
        Animator animator = actorObject.AddComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        AnimancerComponent animancer = actorObject.AddComponent<AnimancerComponent>();
        animancer.Animator = animator;
        ActorAnimation actorAnimation = actorObject.AddComponent<ActorAnimation>();
        actorObject.AddComponent<ActionPlayer>();
        Actor actor = actorObject.AddComponent<Actor>();

        var clip = new AnimationClip { name = "Frozen Pose Test Clip" };
        clip.SetCurve(
            "PoseProbe",
            typeof(Transform),
            "m_LocalPosition.x",
            AnimationCurve.Linear(0f, 2f, 1f, 3f));
        _objects.Add(clip);
        var baseClip = new AnimationClip { name = "Different Base Pose" };
        baseClip.SetCurve("PoseProbe", typeof(Transform), "m_LocalPosition.x",
            AnimationCurve.Linear(0f, -1f, 1f, -1f));
        _objects.Add(baseClip);
        animancer.Layers[0].Play(baseClip);
        AnimationAsset animationAsset = ScriptableObject.CreateInstance<AnimationAsset>();
        animationAsset.EditorSetClip(clip);
        _objects.Add(animationAsset);
        ActionAsset asset = CreateActionRuntimeAsset();
        asset.Timeline.EditorAnimationSegments.Add(new AnimationSegment());
        asset.Timeline.EditorAnimationSegments[0].EditorSetData(0, animationAsset, 0.25f, 1f, 1f);

        var session = new ActionRuntimePlaybackSession(asset.CreateActionInstance(), actor, ActionContext.None);
        try
        {
            actorAnimation.BeginFixedAnimationTick();
            session.Start();
            actorAnimation.Evaluate(0f);
            AssertPose(0.25f);
            session.FinishFrame();

            session.SetSpeed(0d);
            for (int tick = 0; tick < 3; tick++)
            {
                actorAnimation.BeginFixedAnimationTick();
                Assert.IsFalse(session.TryPlayFrame(CombatSimulationTiming.FixedDeltaTime));
                actorAnimation.Evaluate(0f);
                AssertPose(0.25f);
            }

            session.SetSpeed(1d);
            session.Pause();
            actorAnimation.BeginFixedAnimationTick();
            Assert.IsFalse(session.TryPlayFrame(CombatSimulationTiming.FixedDeltaTime));
            actorAnimation.Evaluate(0f);
            AssertPose(0.25f);

            session.Resume();
            session.SetSpeed(0.5d);
            actorAnimation.BeginFixedAnimationTick();
            Assert.IsFalse(session.TryPlayFrame(CombatSimulationTiming.FixedDeltaTime));
            actorAnimation.Evaluate(CombatSimulationTiming.FixedDeltaTime * 0.5f);
            AssertPose(0.25f + 0.5f / 60f);
            actorAnimation.BeginFixedAnimationTick();
            Assert.IsTrue(session.TryPlayFrame(CombatSimulationTiming.FixedDeltaTime));
            actorAnimation.Evaluate(CombatSimulationTiming.FixedDeltaTime * 0.5f);
            AssertPose(0.25f + 1f / 60f);
            session.FinishFrame();

            session.Dispose();
            actorAnimation.BeginFixedAnimationTick();
            actorAnimation.Evaluate(0f);
            Assert.That(actorAnimation.HasActiveActionOwner, Is.False,
                "Playback ownership ends immediately; the exit pose is visual only.");
            Assert.That(animancer.Layers[1].Weight, Is.EqualTo(1f));
            actorAnimation.Evaluate(0.075f);
            Assert.That(animancer.Layers[1].Weight, Is.GreaterThan(0f).And.LessThan(1f));
            actorAnimation.Evaluate(0.1f);
            Assert.That(animancer.Layers[1].Weight, Is.Zero);
            Assert.That(probe.transform.localPosition.x, Is.EqualTo(-1f).Within(1e-4f));
        }
        finally { session.Dispose(); }

        void AssertPose(float sourceTime)
        {
            AnimancerLayer layer = animancer.Layers[1];
            Assert.That(layer.Weight, Is.EqualTo(1f).Within(1e-5f));
            Assert.IsNotNull(layer.CurrentState);
            Assert.That(layer.CurrentState.Speed, Is.Zero);
            Assert.That(layer.CurrentState.Time, Is.EqualTo(sourceTime).Within(1e-5));
            Assert.That(probe.transform.localPosition.x, Is.EqualTo(2f + sourceTime).Within(1e-4f));
        }
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
        ActionAsset invalid = CreateActionRuntimeAsset();
        var segment = new AnimationSegment();
        segment.EditorSetData(0, null, 0f, 1f, 1f);
        invalid.Timeline.EditorAnimationSegments.Add(segment);
        Assert.IsFalse(player.TryBeginAction(invalid, ActionContext.ForSelf(actor), out _));
        Assert.AreEqual(0, starts);
        Assert.IsTrue(player.TryBeginAction(CreateActionRuntimeAsset(), ActionContext.ForSelf(actor), out string reason), reason);
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
        ActionAsset loop = CreateActionRuntimeAsset();
        JsonUtility.FromJsonOverwrite("{\"isLoop\":true}", loop);
        int starts = 0;
        player.OnActionStarted += _ => starts++;
        Assert.IsTrue(player.TryBeginAction(loop, ActionContext.ForSelf(actor), out string reason), reason);
        Assert.IsTrue(player.PlayActionFrame(CombatSimulationTiming.FixedDeltaTime));
        player.FinishActionFrame();
        Assert.IsTrue(player.PlayActionFrame(CombatSimulationTiming.FixedDeltaTime));
        player.FinishActionFrame();
        Assert.AreEqual(1, starts);
        Assert.IsTrue(player.TryBeginAction(CreateActionRuntimeAsset(), ActionContext.ForSelf(actor), out reason), reason);
        Assert.AreEqual(2, starts);
        player.StopAction();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalObserverFailure_DoesNotBlockOtherObserversOrStopAReplacement(bool interrupted)
    {
        ActionPlayer player = CreatePlayer(out Actor actor);
        ActionAsset action = CreateActionRuntimeAsset();
        ActionAsset replacement = CreateActionRuntimeAsset();
        if (interrupted)
            AddLane(action).EditorItems.Add(new CallbackPointItem(1,
                () => throw new InvalidOperationException("Injected playback failure.")));

        ActionInstance reported = null;
        int notifications = 0;
        Action<ActionInstance> first = finished =>
        {
            Assert.IsNull(player.CurrentAction, "Terminal observers run after finalization.");
            Assert.IsTrue(player.TryBeginAction(replacement, ActionContext.ForSelf(actor), out string reason), reason);
            throw new InvalidOperationException("Injected terminal observer failure.");
        };
        Action<ActionInstance> second = finished => { reported = finished; notifications++; };
        if (interrupted)
        {
            player.OnActionInterrupted += first;
            player.OnActionInterrupted += second;
        }
        else
        {
            player.OnActionFinished += first;
            player.OnActionFinished += second;
        }

        Assert.IsTrue(player.TryBeginAction(action, ActionContext.ForSelf(actor), out string error), error);
        ActionInstance original = player.CurrentAction;
        if (!interrupted)
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: Injected terminal observer failure.");
        Assert.IsTrue(player.PlayActionFrame(CombatSimulationTiming.FixedDeltaTime));
        player.FinishActionFrame();
        if (interrupted)
        {
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: Injected playback failure.");
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: Injected terminal observer failure.");
            Assert.IsFalse(player.PlayActionFrame(CombatSimulationTiming.FixedDeltaTime));
        }

        Assert.AreEqual(1, notifications);
        Assert.AreSame(original, reported);
        Assert.IsNull(original.Actor);
        Assert.AreSame(replacement, player.CurrentAction.Config);
        Assert.AreEqual(0, player.CurrentFrame);
        Assert.AreEqual(replacement.Timeline.DurationFrames, player.TotalFrames);
        player.StopAction();
    }

    private ActionPlayer CreatePlayer(out Actor actor)
    {
        var owner = new GameObject("Action start notification actor");
        _objects.Add(owner);
        ActionPlayer player = owner.AddComponent<ActionPlayer>();
        actor = owner.AddComponent<Actor>();
        actor.actionPlayer = player;
        // Ordinary MonoBehaviour Awake is not guaranteed by EditMode tests.
        JsonUtility.FromJsonOverwrite($"{{\"_actor\":{{\"instanceID\":{actor.GetInstanceID()}}}}}", player);
        return player;
    }

    private ActionAsset CreateActionRuntimeAsset()
    {
        ActionAsset asset = ScriptableObject.CreateInstance<ActionAsset>();
        asset.MotionPolicy.movement = ActionMovementControl.Allow;
        asset.MotionPolicy.turning = ActionTurnControl.Normal; // Playback tests use neutral motion unless explicitly overridden.
        _objects.Add(asset);
        return asset;
    }

    private static GameplayLane AddLane(ActionAsset asset)
    {
        var lane = new GameplayLane();
        asset.Timeline.EditorGameplayLanes.Add(lane);
        return lane;
    }

    [Serializable]
    private sealed class CallbackPointItem : PointGameplayItem
    {
        private readonly Action _callback;
        public CallbackPointItem(int frame, Action callback) { EditorSetFrame(frame); _callback = callback; }
        protected internal override IActionPointRuntime CreateRuntime() => new Runtime(_callback);
        private sealed class Runtime : IActionPointRuntime
        {
            private readonly Action _callback;
            public Runtime(Action callback) { _callback = callback; }
            public void Execute(ActionRuntimeContext context) => _callback();
        }
    }

    [Serializable]
    private sealed class ProbePointItem : PointGameplayItem
    {
        private readonly List<string> _events;

        public ProbePointItem(List<string> events, int frame)
        {
            _events = events;
            EditorSetFrame(frame);
        }

        protected internal override IActionPointRuntime CreateRuntime() => new Runtime(_events, Frame);

        private sealed class Runtime : IActionPointRuntime
        {
            private readonly List<string> _events;
            private readonly int _frame;

            public Runtime(List<string> events, int frame)
            {
                _events = events;
                _frame = frame;
            }

            public void Execute(ActionRuntimeContext context) => _events.Add($"point:{_frame}");
        }
    }

    [Serializable]
    private sealed class ProbeRangeItem : RangeGameplayItem
    {
        private readonly List<string> _events;

        public ProbeRangeItem(List<string> events)
        {
            _events = events;
            EditorSetTiming(0, 2);
        }

        protected internal override IActionRangeRuntime CreateRuntime() => new Runtime(_events);

        private sealed class Runtime : IActionRangeRuntime
        {
            private readonly List<string> _events;

            public Runtime(List<string> events) => _events = events;
            public void Enter(ActionRuntimeContext context) => _events.Add("enter");
            public void Tick(ActionRuntimeContext context, int localFrame) => _events.Add("tick");
            public void Exit(ActionRuntimeContext context, ActionRangeExitReason reason) => _events.Add("exit");
            public void Abort(ActionRuntimeContext context) => _events.Add("abort");
        }
    }
}
#endif
