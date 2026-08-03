using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ActionSequenceRuntimeTests
{
    [Test]
    public void StepFrame_ProcessesFrameZeroAndHalfOpenInterval()
    {
        ActionSequenceAsset asset = CreateAsset(
            3,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 2));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        Assert.IsTrue(runtime.StepFrame(context));
        Assert.IsTrue(runtime.StepFrame(context));
        Assert.IsTrue(runtime.StepFrame(context));

        CollectionAssert.AreEqual(
            new[]
            {
                "A:enter:0",
                "A:tick:0",
                "A:tick:1",
                "A:exit:2:True",
            },
            events);
    }

    [Test]
    public void StepFrame_OrdersSameFrameClipsByPhase()
    {
        ActionSequenceAsset asset = CreateAsset(
            1,
            new ProbeClipDefinition("M", ActionSequenceClipPhase.Motion, 0, 1),
            new ProbeClipDefinition("H", ActionSequenceClipPhase.HitBox, 0, 1),
            new ProbeClipDefinition("S", ActionSequenceClipPhase.State, 0, 1));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        runtime.StepFrame(context);

        CollectionAssert.AreEqual(
            new[]
            {
                "S:enter:0",
                "M:enter:0",
                "H:enter:0",
                "S:tick:0",
                "M:tick:0",
                "H:tick:0",
                "H:exit:1:True",
                "M:exit:1:True",
                "S:exit:1:True",
            },
            events);
    }

    [Test]
    public void Tick_ProcessesAllCrossedFrames()
    {
        ActionSequenceAsset asset = CreateAsset(
            5,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 1, 2));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        int processed = runtime.Tick(context, 3f / 60f);

        Assert.AreEqual(3, processed);
        Assert.AreEqual(2, runtime.CurrentFrame);
        CollectionAssert.AreEqual(
            new[]
            {
                "A:enter:1",
                "A:tick:1",
                "A:exit:2:True",
            },
            events);
    }

    [Test]
    public void Cancel_ExitsActiveClipsAsInterrupted()
    {
        ActionSequenceAsset asset = CreateAsset(
            5,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 5));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        runtime.StepFrame(context);
        runtime.Cancel(context);

        CollectionAssert.AreEqual(
            new[]
            {
                "A:enter:0",
                "A:tick:0",
                "A:exit:0:False",
            },
            events);
        Assert.IsFalse(runtime.IsPlaying);
        Assert.IsTrue(runtime.IsComplete);
    }

    [Test]
    public void Runtimes_DoNotShareClipRuntimeState()
    {
        ActionSequenceAsset asset = CreateAsset(
            2,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 2));
        var first = new ActionSequenceRuntime(asset);
        var second = new ActionSequenceRuntime(asset);
        var firstEvents = new List<string>();
        var secondEvents = new List<string>();

        first.StepFrame(new ActionSequenceContext { UserData = firstEvents });
        second.StepFrame(new ActionSequenceContext { UserData = secondEvents });

        CollectionAssert.AreEqual(new[] { "A:enter:0", "A:tick:0" }, firstEvents);
        CollectionAssert.AreEqual(new[] { "A:enter:0", "A:tick:0" }, secondEvents);
    }

    [Test]
    public void Command_MigratesLegacyFlatClipsIntoTracks()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        var tagClip = new ActionSequenceTagClipDefinition { startFrame = 1, endFrame = 3 };

        asset.EditorClips.Add(tagClip);
        ActionSequenceEditorCommands.MigrateLegacyClips(asset);

        Assert.AreEqual(0, asset.EditorClips.Count);
        Assert.AreEqual(1, asset.Clips.Count);
        Assert.AreSame(tagClip, asset.Clips[0]);
    }

    [Test]
    public void Normalize_DoesNotCreateDefaultTracks()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.Data.Normalize();

        Assert.AreEqual(0, asset.EditorTracks.Count);
    }

    [Test]
    public void DurationMode_AutoFromClipsUsesMaximumClipEndFrame()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorSetDurationMode(ActionSequenceDurationMode.AutoFromClips);

        var track = new ActionSequenceStateTrack();
        track.TryAddClip(new ActionSequenceTagClipDefinition { startFrame = 1, endFrame = 4 });
        track.TryAddClip(new ActionSequenceTagClipDefinition { startFrame = 3, endFrame = 9 });
        asset.EditorTracks.Add(track);

        Assert.AreEqual(9, asset.DurationFrames);
    }

    [Test]
    public void Normalize_AutoDurationDoesNotClampClipToFixedDurationField()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorSetTiming(60, 3);
        asset.EditorSetDurationMode(ActionSequenceDurationMode.AutoFromClips);
        asset.EditorTracks.Clear();

        var track = new ActionSequenceStateTrack();
        var clip = new ActionSequenceTagClipDefinition { startFrame = 1, endFrame = 10 };
        track.TryAddClip(clip);
        asset.EditorTracks.Add(track);

        asset.Data.Normalize();

        Assert.AreEqual(10, clip.EndFrame);
        Assert.AreEqual(10, asset.DurationFrames);
    }

    [Test]
    public void Track_AllowsOnlyConfiguredClipTypes()
    {
        var animationTrack = new ActionSequenceAnimationTrack();

        Assert.IsTrue(animationTrack.AllowsClipType(typeof(ActionSequenceAnimancerClipDefinition)));
        Assert.IsFalse(animationTrack.AllowsClipType(typeof(ActionSequenceHitBoxClipDefinition)));
    }

    [Test]
    public void Track_TryAddClipRejectsInvalidClipType()
    {
        var animationTrack = new ActionSequenceAnimationTrack();

        Assert.IsFalse(animationTrack.TryAddClip(new ActionSequenceHitBoxClipDefinition()));
        Assert.AreEqual(0, animationTrack.Clips.Count);
    }

    [Test]
    public void Normalize_DoesNotDeleteIllegalClipFromTrack()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();

        var animationTrack = new ActionSequenceAnimationTrack();
        var illegalClip = new ActionSequenceHitBoxClipDefinition { startFrame = 0, endFrame = 1 };
        animationTrack.EditorClips.Add(illegalClip);
        asset.EditorTracks.Add(animationTrack);

        asset.Data.Normalize();
        List<ActionSequenceValidationIssue> issues = asset.Data.Validate();

        Assert.AreEqual(1, animationTrack.Clips.Count);
        Assert.AreSame(illegalClip, animationTrack.Clips[0]);
        Assert.IsTrue(issues.Exists(issue => issue.Severity == ActionSequenceValidationSeverity.Error));
    }

    [Test]
    public void EditorSelection_TrackTypeDiscoveryOnlyIncludesCreatableTrackTypes()
    {
        Assert.IsTrue(ActionSequenceEditorSelection.IsCreatableTrackType(typeof(ActionSequenceAnimationTrack)));
        Assert.IsFalse(ActionSequenceEditorSelection.IsCreatableTrackType(typeof(ProbeTrackDefinition)));
        CollectionAssert.Contains(ActionSequenceEditorSelection.GetTrackTypes(), typeof(ActionSequenceAnimationTrack));
        CollectionAssert.DoesNotContain(ActionSequenceEditorSelection.GetTrackTypes(), typeof(ProbeTrackDefinition));
    }

    [Test]
    public void Runtime_OrdersSamePhaseByTrackOrderBeforeStartFrame()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorSetTiming(60, 1);
        asset.EditorTracks.Clear();

        asset.EditorTracks.Add(new ProbeTrackDefinition(
            ActionSequenceClipPhase.State,
            new ProbeClipDefinition("B", ActionSequenceClipPhase.State, 0, 1)));
        asset.EditorTracks.Add(new ProbeTrackDefinition(
            ActionSequenceClipPhase.State,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 1)));

        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();

        runtime.StepFrame(new ActionSequenceContext { UserData = events });

        CollectionAssert.AreEqual(
            new[]
            {
                "B:enter:0",
                "A:enter:0",
                "B:tick:0",
                "A:tick:0",
                "A:exit:1:True",
                "B:exit:1:True",
            },
            events);
    }

    [Test]
    public void Runtime_SkipsMutedTracks()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorSetTiming(60, 2);
        asset.EditorTracks.Clear();
        asset.EditorTracks.Add(new ProbeTrackDefinition(
            ActionSequenceClipPhase.State,
            new ProbeClipDefinition("A", ActionSequenceClipPhase.State, 0, 2))
        {
            muted = true
        });

        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();

        runtime.StepFrame(new ActionSequenceContext { UserData = events });

        Assert.AreEqual(0, events.Count);
    }

    [Test]
    public void Runtime_DoesNotMutateInvalidTiming()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorSetTiming(60, 3);
        asset.EditorTracks.Clear();
        var clip = new ProbeClipDefinition("A", ActionSequenceClipPhase.State, -5, 20);
        asset.EditorTracks.Add(new ProbeTrackDefinition(ActionSequenceClipPhase.State, clip));

        _ = new ActionSequenceRuntime(asset);

        Assert.AreEqual(-5, clip.startFrame);
        Assert.AreEqual(20, clip.endFrame);
    }

    private static ActionSequenceAsset CreateAsset(int durationFrames, params ActionSequenceClipDefinition[] clips)
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorSetTiming(60, durationFrames);
        asset.EditorTracks.Clear();

        for (int i = 0; i < clips.Length; i++)
        {
            ActionSequenceClipDefinition clip = clips[i];
            ProbeTrackDefinition track = FindProbeTrack(asset, clip.Phase);
            if (track == null)
            {
                track = new ProbeTrackDefinition(clip.Phase);
                asset.EditorTracks.Add(track);
            }

            track.AddClip(clip);
        }

        return asset;
    }

    private static ProbeTrackDefinition FindProbeTrack(ActionSequenceAsset asset, ActionSequenceClipPhase phase)
    {
        for (int i = 0; i < asset.EditorTracks.Count; i++)
        {
            if (asset.EditorTracks[i] is ProbeTrackDefinition track && track.Phase == phase)
                return track;
        }

        return null;
    }

    private sealed class ProbeTrackDefinition : ActionSequenceTrackDefinition
    {
        private readonly ActionSequenceClipPhase _phase;
        private static readonly System.Type[] ClipTypes = { typeof(ProbeClipDefinition) };

        public ProbeTrackDefinition(ActionSequenceClipPhase phase, params ActionSequenceClipDefinition[] clips)
        {
            _phase = phase;
            for (int i = 0; i < clips.Length; i++)
                AddClip(clips[i]);
        }

        public override ActionSequenceClipPhase Phase => _phase;
        public override System.Type[] AllowedClipTypes => ClipTypes;
    }

    private sealed class ProbeClipDefinition : ActionSequenceClipDefinition
    {
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
                Events(context).Add($"{_id}:enter:{context.Frame}");
            }

            public override void OnTick(ActionSequenceContext context)
            {
                Events(context).Add($"{_id}:tick:{context.Frame}");
            }

            public override void OnExit(ActionSequenceContext context, bool completed)
            {
                Events(context).Add($"{_id}:exit:{context.Frame}:{completed}");
            }

            private static List<string> Events(ActionSequenceContext context)
            {
                return (List<string>)context.UserData;
            }
        }
    }
}
