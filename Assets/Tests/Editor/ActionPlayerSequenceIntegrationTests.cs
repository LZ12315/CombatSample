using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;

public sealed class ActionPlayerSequenceIntegrationTests
{
    [TearDown]
    public void TearDown()
    {
        ProbeClipDefinition.Events.Clear();
    }

    [Test]
    public void BeginAction_SequenceDefersFrameZeroUntilFixedPreWorld()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action);

            Assert.AreEqual(0, ProbeClipDefinition.Events.Count);
            Assert.AreSame(action, player.CurrentAction.Config);
            Assert.AreEqual(0, player.CurrentFrame);
            Assert.AreEqual(60, player.CurrentFrameRate);
            Assert.AreEqual(2, player.TotalFrames);
            Assert.AreEqual(0f, player.CurrentAction.RuntimeData.normalizedTime);

            InvokePrivate(player, "Update");
            Assert.AreEqual(0, ProbeClipDefinition.Events.Count);

            ExecuteFixedTick(player);

            CollectionAssert.AreEqual(new[] { "A:enter:0", "A:tick:0" }, ProbeClipDefinition.Events);
            Assert.Greater(player.CurrentAction.RuntimeData.normalizedTime, 0);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void OneFrameSequence_FinishesInTheSameEndFrame()
    {
        ActionAsset action = CreateSequenceAction(
            1,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 1));
        ActionPlayer player = CreatePlayer(out GameObject owner);
        int finishedCount = 0;
        player.OnActionFinished += _ => finishedCount++;

        try
        {
            player.BeginAction(action);

            Assert.AreEqual(0, finishedCount);
            Assert.IsNotNull(player.CurrentAction);
            Assert.AreEqual(0, ProbeClipDefinition.Events.Count);

            InvokePrivate(player, "ExecuteSimulationPreWorld", 1f / 60f);
            Assert.AreEqual(0, finishedCount);
            InvokePrivate(player, "ExecuteSimulationPostWorld");
            Assert.AreEqual(0, finishedCount);
            InvokePrivate(player, "EndSimulationTick");

            Assert.AreEqual(1, finishedCount);
            Assert.IsNull(player.CurrentAction);
            CollectionAssert.AreEqual(
                new[] { "A:enter:0", "A:tick:0", "A:exit:1:True" },
                ProbeClipDefinition.Events);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void StopAction_BeforeFirstFixedFrameHasNoSequenceClipSideEffects()
    {
        ActionAsset action = CreateSequenceAction(
            3,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 3));
        ActionPlayer player = CreatePlayer(out GameObject owner);
        int finishedCount = 0;
        int interruptedCount = 0;
        player.OnActionFinished += _ => finishedCount++;
        player.OnActionInterrupted += _ => interruptedCount++;

        try
        {
            player.BeginAction(action);
            player.StopAction();

            Assert.AreEqual(0, ProbeClipDefinition.Events.Count);
            Assert.AreEqual(0, finishedCount);
            Assert.AreEqual(0, interruptedCount);
            Assert.IsNull(player.CurrentAction);
            Assert.AreEqual(0, player.CurrentFrameRate);
            Assert.AreEqual(0, player.TotalFrames);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void StopAction_AfterPreWorldCancelsOpenFrameExactlyOnce()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("H", ActionSequenceClipPhase.HitBox, 0, 2),
            new ProbeClipDefinition("S", ActionSequenceClipPhase.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action);
            InvokePrivate(player, "ExecuteSimulationPreWorld", 1f / 60f);
            player.StopAction();

            CollectionAssert.AreEqual(
                new[]
                {
                    "S:enter:0",
                    "H:enter:0",
                    "S:tick:0",
                    "H:exit:0:False",
                    "S:exit:0:False",
                },
                ProbeClipDefinition.Events);
            Assert.IsNull(player.CurrentAction);

            InvokePrivate(player, "ExecuteSimulationPostWorld");
            InvokePrivate(player, "EndSimulationTick");
            Assert.AreEqual(5, ProbeClipDefinition.Events.Count);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void FixedTick_SeparatesPreWorldFromPostWorld()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("H", ActionSequenceClipPhase.HitBox, 0, 2),
            new ProbeClipDefinition("S", ActionSequenceClipPhase.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action);
            InvokePrivate(player, "ExecuteSimulationPreWorld", 1f / 60f);

            CollectionAssert.AreEqual(
                new[] { "S:enter:0", "H:enter:0", "S:tick:0" },
                ProbeClipDefinition.Events);

            InvokePrivate(player, "ExecuteSimulationPostWorld");

            CollectionAssert.AreEqual(
                new[] { "S:enter:0", "H:enter:0", "S:tick:0", "H:tick:0" },
                ProbeClipDefinition.Events);

            InvokePrivate(player, "EndSimulationTick");
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void HalfSpeed_AdvancesAtMostOneFrameEveryTwoFixedTicks()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.SetBaseSpeed(0.5);
            player.BeginAction(action);

            ExecuteFixedTick(player);
            Assert.AreEqual(0, ProbeClipDefinition.Events.Count);

            ExecuteFixedTick(player);
            CollectionAssert.AreEqual(new[] { "A:enter:0", "A:tick:0" }, ProbeClipDefinition.Events);

            ExecuteFixedTick(player);
            Assert.AreEqual(2, ProbeClipDefinition.Events.Count);

            ExecuteFixedTick(player);
            CollectionAssert.AreEqual(
                new[] { "A:enter:0", "A:tick:0", "A:tick:1", "A:exit:2:True" },
                ProbeClipDefinition.Events);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void ZeroSpeed_DoesNotEnterFrameZero()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.SetBaseSpeed(0.0);
            player.BeginAction(action);

            ExecuteFixedTick(player);
            ExecuteFixedTick(player);

            Assert.AreEqual(0, ProbeClipDefinition.Events.Count);
            Assert.IsNotNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void Pause_HoldsAccumulatorUntilResume()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action);
            player.Pause();
            ExecuteFixedTick(player);
            ExecuteFixedTick(player);
            Assert.AreEqual(0, ProbeClipDefinition.Events.Count);

            player.Resume();
            ExecuteFixedTick(player);
            CollectionAssert.AreEqual(new[] { "A:enter:0", "A:tick:0" }, ProbeClipDefinition.Events);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequenceSpeedAboveOne_LogsAndInterruptsAction()
    {
        ActionAsset action = CreateSequenceAction(2);
        ActionPlayer player = CreatePlayer(out GameObject owner);
        int interruptedCount = 0;
        player.OnActionInterrupted += _ => interruptedCount++;

        try
        {
            player.BeginAction(action);
            LogAssert.Expect(
                LogType.Exception,
                new Regex("ArgumentOutOfRangeException: Gameplay Sequence speed must be within"));

            player.SetBaseSpeed(2.0);

            Assert.AreEqual(1, interruptedCount);
            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequenceFrameRateOtherThanSixty_IsRejectedBeforeActionEnter()
    {
        ActionAsset action = CreateSequenceAction(2);
        action.SequenceData.EditorSetTiming(30, 2);
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                "Action 播放失败：Gameplay Sequence 必须使用 60 Hz，当前为 30 Hz。");

            player.BeginAction(action);

            Assert.IsNull(player.CurrentAction);
            Assert.AreEqual(0, ProbeClipDefinition.Events.Count);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void LoopSequence_RestartsWithoutExecutingNewFrameZeroInSameTick()
    {
        ActionAsset action = CreateSequenceAction(
            1,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 1));
        SetPrivateField(action, "isLoop", true);
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action);
            ExecuteFixedTick(player);

            CollectionAssert.AreEqual(
                new[] { "A:enter:0", "A:tick:0", "A:exit:1:True" },
                ProbeClipDefinition.Events);
            Assert.IsNotNull(player.CurrentAction);

            ExecuteFixedTick(player);

            CollectionAssert.AreEqual(
                new[]
                {
                    "A:enter:0", "A:tick:0", "A:exit:1:True",
                    "A:enter:0", "A:tick:0", "A:exit:1:True",
                },
                ProbeClipDefinition.Events);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    private static ActionPlayer CreatePlayer(out GameObject owner)
    {
        owner = new GameObject("ActionPlayerSequenceIntegrationTests");
        owner.AddComponent<PlayableDirector>();
        ActionPlayer player = owner.AddComponent<ActionPlayer>();
        InvokePrivate(player, "Awake");
        return player;
    }

    private static void ExecuteFixedTick(ActionPlayer player)
    {
        InvokePrivate(player, "ExecuteSimulationPreWorld", 1f / 60f);
        InvokePrivate(player, "ExecuteSimulationPostWorld");
        InvokePrivate(player, "EndSimulationTick");
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, arguments);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static ActionAsset CreateSequenceAction(int durationFrames, params ActionSequenceClipDefinition[] clips)
    {
        ActionAsset action = ScriptableObject.CreateInstance<ActionAsset>();
        action.SetPlaybackBackend(ActionPlaybackBackend.Sequence);
        action.SequenceData.EditorSetTiming(60, durationFrames);
        action.SequenceData.EditorTracks.Clear();

        for (int i = 0; i < clips.Length; i++)
        {
            ActionSequenceClipDefinition clip = clips[i];
            ProbeTrackDefinition track = FindTrack(action, clip.Phase);
            if (track == null)
            {
                track = new ProbeTrackDefinition(clip.Phase);
                action.SequenceData.EditorTracks.Add(track);
            }

            track.AddClip(clip);
        }

        return action;
    }

    private static ProbeTrackDefinition FindTrack(ActionAsset action, ActionSequenceClipPhase phase)
    {
        for (int i = 0; i < action.SequenceData.EditorTracks.Count; i++)
        {
            if (action.SequenceData.EditorTracks[i] is ProbeTrackDefinition track && track.Phase == phase)
                return track;
        }

        return null;
    }

    private sealed class ProbeTrackDefinition : ActionSequenceTrackDefinition
    {
        private static readonly System.Type[] ClipTypes = { typeof(ProbeClipDefinition) };
        private readonly ActionSequenceClipPhase _phase;

        public ProbeTrackDefinition(ActionSequenceClipPhase phase)
        {
            _phase = phase;
        }

        public override ActionSequenceClipPhase Phase => _phase;
        public override System.Type[] AllowedClipTypes => ClipTypes;
    }

    private sealed class ProbeClipDefinition : ActionSequenceClipDefinition
    {
        public static readonly List<string> Events = new List<string>();
        private readonly string _id;
        private readonly ActionSequenceClipPhase _phase;

        public ProbeClipDefinition(string id, ActionSequenceClipPhase phase, int start, int end)
        {
            _id = id;
            _phase = phase;
            startFrame = start;
            endFrame = end;
        }

        public override ActionSequenceClipPhase Phase => _phase;

        public override ActionSequenceClipRuntime CreateRuntime()
        {
            return new Runtime(_id);
        }

        private sealed class Runtime : ActionSequenceClipRuntime
        {
            private readonly string _id;

            public Runtime(string id)
            {
                _id = id;
            }

            public override void OnEnter(ActionSequenceContext context)
            {
                Events.Add($"{_id}:enter:{context.Frame}");
            }

            public override void OnTick(ActionSequenceContext context)
            {
                Events.Add($"{_id}:tick:{context.Frame}");
            }

            public override void OnExit(ActionSequenceContext context, bool completed)
            {
                Events.Add($"{_id}:exit:{context.Frame}:{completed}");
            }
        }
    }
}
