using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;

public sealed class ActionPlayerSequenceIntegrationTests
{
    [TearDown]
    public void TearDown()
    {
        ProbeClipDefinition.Events.Clear();
    }

    [Test]
    public void BeginAction_SequenceExecutesFrameZeroImmediately()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action);

            CollectionAssert.AreEqual(new[] { "A:enter:0", "A:tick:0" }, ProbeClipDefinition.Events);
            Assert.AreSame(action, player.CurrentAction.Config);
            Assert.AreEqual(0, player.CurrentFrame);
            Assert.AreEqual(60, player.CurrentFrameRate);
            Assert.AreEqual(2, player.TotalFrames);
            Assert.Greater(player.CurrentAction.RuntimeData.normalizedTime, 0);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void BeginAction_OneFrameSequenceDefersFinishedEventUntilUpdate()
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
            CollectionAssert.AreEqual(
                new[] { "A:enter:0", "A:tick:0", "A:exit:1:True" },
                ProbeClipDefinition.Events);

            InvokePrivate(player, "Update");

            Assert.AreEqual(1, finishedCount);
            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void StopAction_SequenceCancelsActiveClipWithoutFinishedOrInterrupted()
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

            CollectionAssert.AreEqual(
                new[] { "A:enter:0", "A:tick:0", "A:exit:0:False" },
                ProbeClipDefinition.Events);
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

    private static ActionPlayer CreatePlayer(out GameObject owner)
    {
        owner = new GameObject("ActionPlayerSequenceIntegrationTests");
        owner.AddComponent<PlayableDirector>();
        ActionPlayer player = owner.AddComponent<ActionPlayer>();
        InvokePrivate(player, "Awake");
        return player;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
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
