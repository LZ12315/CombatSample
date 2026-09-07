#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ActionRuntimeSchedulerTests
{
    [Test]
    public void EmptyTimeline_OpensFrameZeroAndCompletesOnlyAfterFinish()
    {
        ActionAsset asset = CreateAsset();
        var runtime = new ActionRuntime(asset, null, ActionContext.None);

        runtime.Begin();

        Assert.AreEqual(0, runtime.CurrentFrame);
        Assert.AreEqual(1, runtime.DurationFrames);
        Assert.IsTrue(runtime.HasOpenFrame);
        Assert.AreEqual(ActionRuntimeState.Running, runtime.State);

        Assert.IsTrue(runtime.Finish());
        Assert.AreEqual(ActionRuntimeState.Completed, runtime.State);
        Assert.AreEqual(ActionRuntimeTerminationResult.Completed, runtime.TerminationResult);
        Destroy(asset);
    }

    [Test]
    public void PointAndRange_UseOpenThenFinishHalfOpenLifecycle()
    {
        var events = new List<string>();
        ActionAsset asset = CreateAsset();
        GameplayLane lane = AddLane(asset);
        lane.EditorItems.Add(new ProbePointItem(events, "point", 0));
        lane.EditorItems.Add(new ProbeRangeItem(events, "range", 0, 2));
        var runtime = new ActionRuntime(asset, null, ActionContext.None);

        runtime.Begin();
        CollectionAssert.AreEqual(new[] { "point:execute", "range:enter", "range:tick:0" }, events);
        Assert.IsFalse(runtime.Finish());
        Assert.AreEqual(ActionRuntimeState.Running, runtime.State);

        Assert.IsTrue(runtime.Advance());
        CollectionAssert.AreEqual(
            new[] { "point:execute", "range:enter", "range:tick:0", "range:tick:1" },
            events);
        Assert.IsTrue(runtime.Finish());
        CollectionAssert.AreEqual(
            new[] { "point:execute", "range:enter", "range:tick:0", "range:tick:1", "range:exit:Completed" },
            events);
        Destroy(asset);
    }

    [Test]
    public void OneFrameRange_TicksBeforeItsCompletedExit()
    {
        var events = new List<string>();
        ActionAsset asset = CreateAsset();
        AddLane(asset).EditorItems.Add(new ProbeRangeItem(events, "one", 0, 1));
        var runtime = new ActionRuntime(asset, null, ActionContext.None);

        runtime.Begin();
        Assert.IsTrue(runtime.Finish());

        CollectionAssert.AreEqual(
            new[] { "one:enter", "one:tick:0", "one:exit:Completed" },
            events);
        Destroy(asset);
    }

    [Test]
    public void MutedContentContributesDurationButCreatesNoRuntime()
    {
        var events = new List<string>();
        ActionAsset asset = CreateAsset();
        GameplayLane lane = AddLane(asset);
        lane.EditorSetMuted(true);
        lane.EditorItems.Add(new ProbeRangeItem(events, "muted", 2, 3));
        var runtime = new ActionRuntime(asset, null, ActionContext.None);

        runtime.Begin();
        Assert.AreEqual(5, runtime.DurationFrames);
        for (int frame = 0; frame < 5; frame++)
        {
            Assert.AreEqual(frame == 4, runtime.Finish());
            if (frame < 4)
                Assert.IsTrue(runtime.Advance());
        }

        Assert.IsEmpty(events);
        Destroy(asset);
    }

    [Test]
    public void SpeedPauseAndResume_OpenOnlyNewIntegerFrames()
    {
        var events = new List<string>();
        ActionAsset asset = CreateAsset();
        GameplayLane lane = AddLane(asset);
        lane.EditorItems.Add(new ProbePointItem(events, "one", 1));
        lane.EditorItems.Add(new ProbePointItem(events, "two", 2));
        var runtime = new ActionRuntime(asset, null, ActionContext.None);

        runtime.Begin();
        runtime.Finish();
        runtime.SetSpeed(0f);
        Assert.IsFalse(runtime.Advance());
        Assert.AreEqual(0d, runtime.ContinuousPosition);
        runtime.SetSpeed(0.5f);
        Assert.IsFalse(runtime.Advance());
        Assert.AreEqual(0.5d, runtime.ContinuousPosition);
        Assert.IsEmpty(events);

        Assert.IsTrue(runtime.Advance());
        CollectionAssert.AreEqual(new[] { "one:execute" }, events);
        runtime.Finish();
        runtime.Pause();
        Assert.IsFalse(runtime.Advance());
        Assert.AreEqual(1d, runtime.ContinuousPosition);
        runtime.Resume();
        Assert.IsFalse(runtime.Advance());
        Assert.IsTrue(runtime.Advance());
        CollectionAssert.AreEqual(new[] { "one:execute", "two:execute" }, events);
        Assert.IsTrue(runtime.Finish());
        Destroy(asset);
    }

    [Test]
    public void BeginSnapshot_IsUnaffectedByLaterTimingMuteAndAnimationEdits()
    {
        var events = new List<string>();
        ActionAsset asset = CreateAsset();
        GameplayLane lane = AddLane(asset);
        var point = new ProbePointItem(events, "snapshot", 1);
        lane.EditorItems.Add(point);
        AnimationClip clip = new AnimationClip();
        clip.SetCurve(
            string.Empty,
            typeof(Transform),
            "m_LocalPosition.x",
            AnimationCurve.Linear(0f, 0f, 1f, 1f));
        AnimationAsset animationAsset = ScriptableObject.CreateInstance<AnimationAsset>();
        animationAsset.EditorSetClip(clip);
        var segment = new AnimationSegment();
        segment.EditorSetData(0, animationAsset, 0f, 2f / 60f, 1f);
        asset.Timeline.EditorAnimationSegments.Add(segment);
        var runtime = new ActionRuntime(asset, null, ActionContext.None);

        runtime.Begin();
        point.EditorSetFrame(8);
        lane.EditorSetMuted(true);
        segment.EditorSetData(4, null, 0f, 1f, 1f);

        Assert.AreEqual(0, runtime.AnimationRecords[0].StartFrame);
        Assert.AreEqual(2, runtime.AnimationRecords[0].EndFrameExclusive);
        Assert.AreEqual(2, runtime.DurationFrames);
        runtime.Finish();
        Assert.IsTrue(runtime.Advance());
        CollectionAssert.AreEqual(new[] { "snapshot:execute" }, events);
        runtime.Finish();
        Destroy(animationAsset);
        Destroy(clip);
        Destroy(asset);
    }

    [Test]
    public void RangeEnterFailure_AbortsThePartiallyEnteredRuntime()
    {
        var events = new List<string>();
        ActionAsset asset = CreateAsset();
        AddLane(asset).EditorItems.Add(new FailingEnterRangeItem(events));
        var runtime = new ActionRuntime(asset, null, ActionContext.None);

        Assert.Throws<InvalidOperationException>(() => runtime.Begin());
        CollectionAssert.AreEqual(new[] { "enter", "abort" }, events);
        Assert.AreEqual(ActionRuntimeState.Aborted, runtime.State);
        Assert.AreEqual(ActionRuntimeTerminationResult.Aborted, runtime.TerminationResult);
        Destroy(asset);
    }

    [Test]
    public void InterruptAndAbort_UseDistinctCleanupAndNeverCreateFutureRanges()
    {
        var interruptedEvents = new List<string>();
        ActionAsset interruptedAsset = CreateAsset();
        AddLane(interruptedAsset).EditorItems.Add(new ProbeRangeItem(interruptedEvents, "active", 0, 3));
        var interrupted = new ActionRuntime(interruptedAsset, null, ActionContext.None);
        interrupted.Begin();
        Assert.IsFalse(interrupted.Interrupt(), "an open frame cannot be normally interrupted");
        interrupted.Abort();
        CollectionAssert.AreEqual(new[] { "active:enter", "active:tick:0", "active:abort" }, interruptedEvents);
        Assert.AreEqual(ActionRuntimeTerminationResult.Aborted, interrupted.TerminationResult);
        Destroy(interruptedAsset);

        var futureEvents = new List<string>();
        ActionAsset interruptAsset = CreateAsset();
        GameplayLane lane = AddLane(interruptAsset);
        lane.EditorItems.Add(new ProbeRangeItem(futureEvents, "active", 0, 3));
        lane.EditorItems.Add(new ProbeRangeItem(futureEvents, "future", 2, 1));
        var runtime = new ActionRuntime(interruptAsset, null, ActionContext.None);
        runtime.Begin();
        runtime.Finish();
        Assert.IsTrue(runtime.Interrupt());
        CollectionAssert.AreEqual(new[] { "active:enter", "active:tick:0", "active:exit:Interrupted" }, futureEvents);
        Assert.AreEqual(ActionRuntimeTerminationResult.Interrupted, runtime.TerminationResult);
        Destroy(interruptAsset);
    }

    [Test]
    public void SameTickReplacement_InterruptsOldBeforeNewBeginsFrameZero()
    {
        var events = new List<string>();
        ActionAsset oldAsset = CreateAsset();
        AddLane(oldAsset).EditorItems.Add(new ProbeRangeItem(events, "old", 0, 2));
        var oldRuntime = new ActionRuntime(oldAsset, null, ActionContext.None);
        oldRuntime.Begin();
        oldRuntime.Finish();
        Assert.IsTrue(oldRuntime.Interrupt());

        ActionAsset newAsset = CreateAsset();
        AddLane(newAsset).EditorItems.Add(new ProbePointItem(events, "new", 0));
        var newRuntime = new ActionRuntime(newAsset, null, ActionContext.None);
        newRuntime.Begin();

        CollectionAssert.AreEqual(
            new[] { "old:enter", "old:tick:0", "old:exit:Interrupted", "new:execute" },
            events);
        Destroy(oldAsset);
        Destroy(newAsset);
    }

    [Test]
    public void SetSpeed_RejectsValuesOutsideClosedUnitInterval()
    {
        ActionAsset asset = CreateAsset();
        var runtime = new ActionRuntime(asset, null, ActionContext.None);

        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.SetSpeed(-0.01f));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.SetSpeed(1.01f));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.SetSpeed(float.NaN));
        Destroy(asset);
    }

    private static ActionAsset CreateAsset() => ScriptableObject.CreateInstance<ActionAsset>();

    private static GameplayLane AddLane(ActionAsset asset)
    {
        var lane = new GameplayLane();
        asset.Timeline.EditorGameplayLanes.Add(lane);
        return lane;
    }

    private static void Destroy(UnityEngine.Object value)
    {
        if (value != null)
            UnityEngine.Object.DestroyImmediate(value);
    }

    [Serializable]
    private sealed class ProbePointItem : PointGameplayItem
    {
        private readonly List<string> _events;
        private readonly string _name;

        public ProbePointItem(List<string> events, string name, int frame)
        {
            _events = events;
            _name = name;
            EditorSetFrame(frame);
        }

        protected internal override IActionPointRuntime CreateRuntime() => new ProbePointRuntime(_events, _name);
    }

    [Serializable]
    private sealed class ProbeRangeItem : RangeGameplayItem
    {
        private readonly List<string> _events;
        private readonly string _name;

        public ProbeRangeItem(List<string> events, string name, int startFrame, int durationFrames)
        {
            _events = events;
            _name = name;
            EditorSetTiming(startFrame, durationFrames);
        }

        protected internal override IActionRangeRuntime CreateRuntime() => new ProbeRangeRuntime(_events, _name);
    }

    private sealed class ProbePointRuntime : IActionPointRuntime
    {
        private readonly List<string> _events;
        private readonly string _name;

        public ProbePointRuntime(List<string> events, string name)
        {
            _events = events;
            _name = name;
        }

        public void Execute(ActionRuntimeContext context) => _events.Add($"{_name}:execute");
    }

    private sealed class ProbeRangeRuntime : IActionRangeRuntime
    {
        private readonly List<string> _events;
        private readonly string _name;

        public ProbeRangeRuntime(List<string> events, string name)
        {
            _events = events;
            _name = name;
        }

        public void Enter(ActionRuntimeContext context) => _events.Add($"{_name}:enter");
        public void Tick(ActionRuntimeContext context, int localFrame) => _events.Add($"{_name}:tick:{localFrame}");
        public void Exit(ActionRuntimeContext context, ActionRangeExitReason reason) => _events.Add($"{_name}:exit:{reason}");
        public void Abort(ActionRuntimeContext context) => _events.Add($"{_name}:abort");
    }

    [Serializable]
    private sealed class FailingEnterRangeItem : RangeGameplayItem
    {
        private readonly List<string> _events;

        public FailingEnterRangeItem(List<string> events)
        {
            _events = events;
            EditorSetTiming(0, 1);
        }

        protected internal override IActionRangeRuntime CreateRuntime() => new FailingEnterRangeRuntime(_events);
    }

    private sealed class FailingEnterRangeRuntime : IActionRangeRuntime
    {
        private readonly List<string> _events;

        public FailingEnterRangeRuntime(List<string> events) => _events = events;

        public void Enter(ActionRuntimeContext context)
        {
            _events.Add("enter");
            throw new InvalidOperationException("Probe Enter failure.");
        }

        public void Tick(ActionRuntimeContext context, int localFrame) { }
        public void Exit(ActionRuntimeContext context, ActionRangeExitReason reason) { }
        public void Abort(ActionRuntimeContext context) => _events.Add("abort");
    }
}
#endif
