#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Animancer;
using NUnit.Framework;
using UnityEngine;

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

    private ActionAsset CreateActionRuntimeAsset()
    {
        ActionAsset asset = ScriptableObject.CreateInstance<ActionAsset>();
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
