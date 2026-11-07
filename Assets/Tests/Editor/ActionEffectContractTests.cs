#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionEffectContractTests
{
    [Test]
    public void Range_RealizesOnceAndReleasesAtExit()
    {
        var buffer = new ActionEffectBuffer();
        var timeline = Timeline(out GameplayLane lane);
        AddShake(lane, 0, 2);
        var scheduler = Scheduler(timeline, buffer);
        var probe = new Probe();
        int starts = 0;
        scheduler.Begin();
        buffer.UpdateWorld(Vector3.right, null, (_, __, ___) => { starts++; return probe; });
        Assert.IsFalse(scheduler.Finish());
        Assert.AreEqual(0, probe.Disposals);
        scheduler.Advance(1f);
        buffer.UpdateWorld(Vector3.up, null, (_, __, ___) => { starts++; return probe; });
        Assert.AreEqual(1, starts);
        Assert.AreEqual(Vector3.up, probe.Position);
        Assert.IsTrue(scheduler.Finish());
        Assert.AreEqual(1, probe.Disposals);
    }

    [TestCase(false, false)] [TestCase(true, false)]
    [TestCase(false, true)] [TestCase(true, true)]
    public void InterruptionAndAbort_CancelPendingAndRealizedScopes(bool realize, bool abort)
    {
        var buffer = new ActionEffectBuffer();
        var timeline = Timeline(out GameplayLane lane);
        AddShake(lane, 0, 3);
        var scheduler = Scheduler(timeline, buffer);
        var probe = new Probe();
        scheduler.Begin();
        if (realize) buffer.UpdateWorld(Vector3.zero, null, (_, __, ___) => probe);
        if (abort) scheduler.Abort();
        else { scheduler.Finish(); scheduler.Interrupt(); }
        buffer.UpdateWorld(Vector3.zero, null, (_, __, ___) => { Assert.Fail(); return null; });
        Assert.AreEqual(realize ? 1 : 0, probe.Disposals);
    }

    [Test]
    public void OneFrameFinishedBeforeWorld_IsNeverResurrected()
    {
        var buffer = new ActionEffectBuffer();
        var timeline = Timeline(out GameplayLane lane); AddShake(lane, 0, 1);
        var scheduler = Scheduler(timeline, buffer);
        scheduler.Begin(); Assert.IsTrue(scheduler.Finish());
        buffer.UpdateWorld(Vector3.zero, null, (_, __, ___) => { Assert.Fail(); return null; });
    }

    [Test]
    public void CurveClock_TracksFractionalActionProgressAndHoldsWhenPaused()
    {
        var buffer = new ActionEffectBuffer();
        var timeline = Timeline(out GameplayLane lane);
        AddShake(lane, 0, 3);
        var scheduler = Scheduler(timeline, buffer);
        Func<float> clock = null;
        scheduler.Begin();
        buffer.UpdateWorld(Vector3.zero, null, (_, __, progress) => { clock = progress; return new Probe(); });
        Assert.AreEqual(0f, clock());
        scheduler.Finish();
        Assert.IsFalse(scheduler.Advance(0.5f));
        Assert.AreEqual(0.25f, clock());
        Assert.IsFalse(scheduler.Advance(0f));
        Assert.AreEqual(0.25f, clock());
        scheduler.Advance(0.5f);
        Assert.AreEqual(0.5f, clock());
        scheduler.Abort();
    }

    [Test]
    public void CurveTiming_UsesSchedulerSnapshot_NotLaterAuthoringEdits()
    {
        var buffer = new ActionEffectBuffer(); var timeline = Timeline(out GameplayLane lane);
        var item = AddShake(lane, 2, 3); var scheduler = Scheduler(timeline, buffer);
        item.EditorSetTiming(20, 100);
        scheduler.Begin(); scheduler.Finish(); scheduler.Advance(1f); scheduler.Finish(); scheduler.Advance(1f);
        Func<float> clock = null;
        buffer.UpdateWorld(Vector3.zero, null, (_, __, progress) => { clock = progress; return new Probe(); });
        Assert.AreEqual(0f, clock());
        scheduler.Finish(); scheduler.Advance(0.5f);
        Assert.AreEqual(0.25f, clock());
        scheduler.Abort();
    }

    [Test]
    public void Clear_ContinuesReleasingOtherRangesWhenOneDisposalThrows()
    {
        var buffer = new ActionEffectBuffer();
        buffer.BeginScreenShake(new ScreenShakeItemConfig(), () => 0f);
        buffer.BeginScreenShake(new ScreenShakeItemConfig(), () => 0f);
        var probe = new Probe(); int count = 0;
        buffer.UpdateWorld(Vector3.zero, null, (_, __, ___) => count++ == 0 ? (IActionPresentation)new FaultingPresentation() : probe);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
            new System.Text.RegularExpressions.Regex("Expected presentation disposal fault"));
        buffer.Clear();
        Assert.AreEqual(1, probe.Disposals);
        buffer.Clear(); Assert.AreEqual(1, probe.Disposals);
    }

    [TestCase(true, false)] [TestCase(false, true)]
    public void MutedItemOrLane_DoesNotStart(bool itemMuted, bool laneMuted)
    {
        var buffer = new ActionEffectBuffer();
        var timeline = Timeline(out GameplayLane lane);
        AddShake(lane, 0, 2).EditorSetMuted(itemMuted);
        lane.EditorSetMuted(laneMuted);
        Scheduler(timeline, buffer).Begin();
        buffer.UpdateWorld(Vector3.zero, null, (_, __, ___) => { Assert.Fail(); return null; });
    }

    [Test]
    public void IndependentScopesAndReentrantCancellation_ReleaseExactlyTheirOwnResources()
    {
        var buffer = new ActionEffectBuffer();
        IDisposable firstScope = buffer.BeginScreenShake(new ScreenShakeItemConfig(), () => 0f);
        IDisposable secondScope = buffer.BeginScreenShake(new ScreenShakeItemConfig(), () => 0f);
        var first = new Probe(); var second = new Probe(); int count = 0;
        buffer.UpdateWorld(Vector3.zero, null, (_, __, ___) => count++ == 0 ? first : second);
        firstScope.Dispose(); firstScope.Dispose();
        Assert.AreEqual(1, first.Disposals);
        Assert.AreEqual(0, second.Disposals);
        buffer.Clear(); secondScope.Dispose();
        Assert.AreEqual(1, second.Disposals);
        buffer.BeginScreenShake(new ScreenShakeItemConfig(), () => 0f);
        var reentrant = new Probe();
        buffer.UpdateWorld(Vector3.zero, null, (_, __, ___) => { buffer.Clear(); return reentrant; });
        Assert.AreEqual(1, reentrant.Disposals);
    }

    [TestCase(ActionScreenShakeDirection.Vertical, 0, 1, 0)]
    [TestCase(ActionScreenShakeDirection.Horizontal, 1, 0, 0)]
    [TestCase(ActionScreenShakeDirection.Forward, 0, 0, 1)]
    public void SignedCurve_ControlsDirectionWithoutHiddenOscillation(ActionScreenShakeDirection direction, int x, int y, int z)
    {
        var config = new ScreenShakeItemConfig { direction = direction, waveform = AnimationCurve.Linear(0f, -1f, 1f, 1f) };
        config.screenShake.intensity = 0.8f;
        Assert.IsTrue(ActionScreenShakeRequest.TryCreate(config, out var request));
        request.Sample(0f, out Vector3 start, out _);
        request.Sample(1f, out Vector3 end, out _);
        Assert.That(Vector3.Distance(-new Vector3(x,y,z) * 0.8f, start), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(new Vector3(x,y,z) * 0.8f, end), Is.LessThan(0.0001f));
        Assert.IsTrue(request.HasNonZeroEndpoint());
    }

    [Test]
    public void AxisAndRotationCurves_AreIndependentSnapshots()
    {
        var config = new ScreenShakeItemConfig { mode = ActionScreenShakeMode.IndependentAxes,
            positionX = AnimationCurve.Constant(0f, 1f, 1f), positionY = AnimationCurve.Constant(0f, 1f, -2f),
            useRotation = true, rotationAmplitude = 10f, roll = AnimationCurve.Constant(0f, 1f, 1f) };
        config.screenShake.intensity = 0.5f;
        Assert.IsTrue(ActionScreenShakeRequest.TryCreate(config, out var request));
        config.positionX.keys = AnimationCurve.Constant(0f, 1f, 99f).keys;
        config.screenShake.intensity = 2f;
        request.Sample(0.5f, out Vector3 position, out Quaternion rotation);
        Assert.AreEqual(new Vector3(0.5f, -1f, 0f), position);
        Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 0f, 10f), rotation), Is.LessThan(0.001f));
    }

    [Test]
    public void CustomDirectionAndInvalidConfiguration_AreExplicit()
    {
        var config = new ScreenShakeItemConfig { direction = ActionScreenShakeDirection.Custom,
            customDirection = new Vector3(1f, 1f, 0f), waveform = AnimationCurve.Constant(0f, 1f, 1f) };
        Assert.IsTrue(ActionScreenShakeRequest.TryCreate(config, out var request));
        request.Sample(0.5f, out Vector3 position, out _);
        Assert.That(Vector3.Distance(position.normalized, config.customDirection.normalized), Is.LessThan(0.0001f));
        config.customDirection = Vector3.zero;
        Assert.IsFalse(ActionScreenShakeRequest.TryCreate(config, out _, out string error));
        Assert.IsNotEmpty(error);
        config.direction = ActionScreenShakeDirection.Vertical;
        config.waveform = null;
        Assert.IsFalse(ActionScreenShakeRequest.TryCreate(config, out _));
    }

    [TestCase(false)] [TestCase(true)]
    public void LegacyPointTiming_RetainsFrameAndConfiguration(bool shake)
    {
        var source = ScriptableObject.CreateInstance<ActionAsset>(); var copy = ScriptableObject.CreateInstance<ActionAsset>();
        try
        {
            var lane = new GameplayLane(); source.Timeline.EditorGameplayLanes.Add(lane);
            if (shake) AddShake(lane, 7, 3).Config.screenShake.intensity = 0.7f;
            else { var effect = new EffectItem(); effect.EditorSetTiming(7, 3); effect.Config.scale = 1.7f; lane.EditorItems.Add(effect); }
            string json = EditorJsonUtility.ToJson(source).Replace("\"_frame\":-2147483648,", "")
                .Replace("\"_startFrame\":7", "\"_frame\":7").Replace("\"_durationFrames\":3,", "");
            EditorJsonUtility.FromJsonOverwrite(json, copy);
            var restored = (RangeGameplayItem)copy.Timeline.GameplayLanes[0].Items[0];
            Assert.AreEqual(7, restored.StartFrame); Assert.AreEqual(1, restored.DurationFrames);
            if (shake) Assert.AreEqual(0.7f, ((ScreenShakeItem)restored).Config.screenShake.intensity);
            else Assert.AreEqual(1.7f, ((EffectItem)restored).Config.scale);
        }
        finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(copy); }
    }

    private sealed class FaultingPresentation : IActionPresentation
    { public void Dispose() => throw new InvalidOperationException("Expected presentation disposal fault"); }

    private sealed class Probe : IActionSpatialPresentation
    {
        internal int Disposals; internal Vector3 Position;
        public void UpdateSourcePosition(Vector3 position) => Position = position;
        public void Dispose() => Disposals++;
    }
    private static ActionTimelineData Timeline(out GameplayLane lane)
    { var timeline = new ActionTimelineData(); lane = new GameplayLane(); timeline.EditorGameplayLanes.Add(lane); return timeline; }
    private static ScreenShakeItem AddShake(GameplayLane lane, int start, int duration)
    { var item = new ScreenShakeItem(); item.EditorSetTiming(start, duration); lane.EditorItems.Add(item); return item; }
    private static ActionRuntimeScheduler Scheduler(ActionTimelineData timeline, ActionEffectBuffer buffer)
    {
        ActionRuntimeScheduler scheduler = null;
        scheduler = new ActionRuntimeScheduler(timeline, new ActionRuntimeContext(null, ActionContext.None, buffer,
            () => scheduler?.ContinuousPosition ?? 0d));
        return scheduler;
    }
}
#endif
