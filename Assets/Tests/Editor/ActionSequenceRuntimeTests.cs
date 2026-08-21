using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ActionSequenceRuntimeTests
{
    [Test]
    public void PlayFrame_EntersAndTicksClipsBeforeFinishFrame()
    {
        ActionSequenceAsset asset = CreateAsset(
            1,
            new ProbeClipDefinition("M", ActionSequenceTrackKind.Motion, 0, 1),
            new ProbeClipDefinition("C", ActionSequenceTrackKind.Cleanup, 0, 1),
            new ProbeClipDefinition("H", ActionSequenceTrackKind.HitBox, 0, 1),
            new ProbeClipDefinition("A", ActionSequenceTrackKind.Animation, 0, 1),
            new ProbeClipDefinition("S", ActionSequenceTrackKind.State, 0, 1));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        Assert.IsTrue(runtime.PlayFrame(context, 0.02f, 0.5f));

        CollectionAssert.AreEqual(
            new[]
            {
                "M:enter:0",
                "C:enter:0",
                "H:enter:0",
                "A:enter:0",
                "S:enter:0",
                "M:tick:0",
                "C:tick:0",
                "H:tick:0",
                "A:tick:0",
                "S:tick:0",
            },
            events);
        Assert.AreEqual(ActionSequenceFrameTransactionState.Open, runtime.FrameTransactionState);
        Assert.AreEqual(-1, runtime.CurrentFrame);
        Assert.AreEqual(0, runtime.PendingFrame);
        Assert.AreEqual(0.02f, context.DeltaTime);
        Assert.AreEqual(0.5f, context.SpeedScale);
        Assert.IsFalse(runtime.IsComplete);

        runtime.FinishFrame();

        CollectionAssert.AreEqual(
            new[]
            {
                "M:enter:0",
                "C:enter:0",
                "H:enter:0",
                "A:enter:0",
                "S:enter:0",
                "M:tick:0",
                "C:tick:0",
                "H:tick:0",
                "A:tick:0",
                "S:tick:0",
                "S:exit:1:True",
                "A:exit:1:True",
                "H:exit:1:True",
                "C:exit:1:True",
                "M:exit:1:True",
            },
            events);
        Assert.AreEqual(ActionSequenceFrameTransactionState.Idle, runtime.FrameTransactionState);
        Assert.AreEqual(0, runtime.CurrentFrame);
        Assert.AreEqual(-1, runtime.PendingFrame);
        Assert.IsTrue(runtime.IsComplete);
    }

    [Test]
    public void OpenFrame_RejectsCompetingAdvanceUntilFinish()
    {
        ActionSequenceAsset asset = CreateAsset(1);
        var runtime = new ActionSequenceRuntime(asset);
        var context = new ActionSequenceContext();

        Assert.Throws<System.InvalidOperationException>(() => runtime.FinishFrame());
        Assert.IsTrue(runtime.PlayFrame(context));
        Assert.Throws<System.InvalidOperationException>(() => runtime.PlayFrame(context));
        Assert.Throws<System.InvalidOperationException>(() => runtime.Tick(context, 1f / 60f));
        Assert.Throws<System.InvalidOperationException>(() => runtime.StepFrame(context));
        runtime.FinishFrame();
    }

    [Test]
    public void FrameTransaction_CancelCleansOpenFrameWithoutCommittingIt()
    {
        ActionSequenceAsset asset = CreateAsset(
            2,
            new ProbeClipDefinition("H", ActionSequenceTrackKind.HitBox, 0, 2),
            new ProbeClipDefinition("S", ActionSequenceTrackKind.State, 0, 2));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        runtime.PlayFrame(context);
        runtime.Cancel(context);

        CollectionAssert.AreEqual(
            new[]
            {
                "H:enter:0",
                "S:enter:0",
                "H:tick:0",
                "S:tick:0",
                "S:exit:0:False",
                "H:exit:0:False",
            },
            events);
        Assert.AreEqual(-1, runtime.CurrentFrame);
        Assert.AreEqual(ActionSequenceFrameTransactionState.Idle, runtime.FrameTransactionState);
        Assert.IsFalse(runtime.HasOpenFrame);
        Assert.IsTrue(runtime.IsComplete);
    }

    [Test]
    public void StepFrame_ExitsHalfOpenClipAtItsEndBoundary()
    {
        ActionSequenceAsset asset = CreateAsset(
            3,
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 1));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        runtime.StepFrame(context);

        CollectionAssert.AreEqual(
            new[]
            {
                "A:enter:0",
                "A:tick:0",
                "A:exit:1:True",
            },
            events);
        Assert.AreEqual(0, context.Frame);
        Assert.IsFalse(runtime.IsComplete);
    }

    [Test]
    public void StepFrame_ProcessesFrameZeroAndHalfOpenInterval()
    {
        ActionSequenceAsset asset = CreateAsset(
            3,
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 2));
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
    public void StepFrame_OrdersSameFrameClipsByTrackOrder()
    {
        ActionSequenceAsset asset = CreateAsset(
            1,
            new ProbeClipDefinition("M", ActionSequenceTrackKind.Motion, 0, 1),
            new ProbeClipDefinition("H", ActionSequenceTrackKind.HitBox, 0, 1),
            new ProbeClipDefinition("S", ActionSequenceTrackKind.State, 0, 1));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        runtime.StepFrame(context);

        CollectionAssert.AreEqual(
            new[]
            {
                "M:enter:0",
                "H:enter:0",
                "S:enter:0",
                "M:tick:0",
                "H:tick:0",
                "S:tick:0",
                "S:exit:1:True",
                "H:exit:1:True",
                "M:exit:1:True",
            },
            events);
    }

    [Test]
    public void Tick_ProcessesAllCrossedFrames()
    {
        ActionSequenceAsset asset = CreateAsset(
            5,
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 1, 2));
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
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 5));
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
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 2));
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
        var motionTrack = new ActionSequenceMotionTrack();

        Assert.IsTrue(animationTrack.AllowsClipType(typeof(ActionSequenceAnimationPoseClipDefinition)));
        Assert.IsFalse(animationTrack.AllowsClipType(typeof(ActionSequenceHitBoxClipDefinition)));
        Assert.IsTrue(motionTrack.AllowsClipType(typeof(ActionSequenceRootMotionClipDefinition)));
        Assert.IsTrue(motionTrack.AllowsClipType(typeof(ActionSequenceSelfRotationClipDefinition)));
    }

    [Test]
    public void ApplyPoseBaseline_OnlyEntersAndTicksFrameZeroAnimationClips()
    {
        ActionSequenceAsset asset = CreateAsset(
            2,
            new BaselineProbeClipDefinition("S", ActionSequenceTrackKind.State, 0, 2),
            new BaselineProbeClipDefinition("A", ActionSequenceTrackKind.Animation, 0, 2),
            new BaselineProbeClipDefinition("M", ActionSequenceTrackKind.Motion, 0, 2),
            new BaselineProbeClipDefinition("H", ActionSequenceTrackKind.HitBox, 0, 2));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        Assert.IsTrue(runtime.ApplyPoseBaseline(context));

        CollectionAssert.AreEqual(
            new[]
            {
                "A:enter:0:baseline",
                "A:tick:0:baseline",
            },
            events);
        Assert.AreEqual(-1, runtime.CurrentFrame);
        Assert.IsFalse(runtime.HasOpenFrame);

        Assert.IsTrue(runtime.PlayFrame(context));

        CollectionAssert.AreEqual(
            new[]
            {
                "A:enter:0:baseline",
                "A:tick:0:baseline",
                "S:enter:0:frame",
                "M:enter:0:frame",
                "H:enter:0:frame",
                "S:tick:0:frame",
                "A:tick:0:frame",
                "M:tick:0:frame",
                "H:tick:0:frame",
            },
            events);
    }

    [Test]
    public void RefreshPose_OnlyTicksActiveAnimationClipsWithoutAdvancingGameplayFrame()
    {
        ActionSequenceAsset asset = CreateAsset(
            3,
            new PoseRefreshProbeClipDefinition("S", ActionSequenceTrackKind.State, 0, 2),
            new PoseRefreshProbeClipDefinition("A", ActionSequenceTrackKind.Animation, 0, 2),
            new PoseRefreshProbeClipDefinition("M", ActionSequenceTrackKind.Motion, 0, 2),
            new PoseRefreshProbeClipDefinition("H", ActionSequenceTrackKind.HitBox, 0, 2));
        var runtime = new ActionSequenceRuntime(asset);
        var events = new List<string>();
        var context = new ActionSequenceContext { UserData = events };

        Assert.IsTrue(runtime.ApplyPoseBaseline(context));
        Assert.IsTrue(runtime.RefreshPose(context, 0.5f));

        CollectionAssert.AreEqual(
            new[]
            {
                "A:enter:0:0:baseline",
                "A:tick:0:0:baseline",
                "A:tick:0:0.5:refresh",
            },
            events);
        Assert.AreEqual(-1, runtime.CurrentFrame);
        Assert.AreEqual(0f, runtime.NormalizedTime);
        Assert.IsFalse(runtime.HasOpenFrame);
        Assert.IsFalse(runtime.IsComplete);
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
    public void Runtime_OrdersSameKindByTrackOrderBeforeStartFrame()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorSetTiming(60, 1);
        asset.EditorTracks.Clear();

        asset.EditorTracks.Add(new ProbeTrackDefinition(
            ActionSequenceTrackKind.State,
            new ProbeClipDefinition("B", ActionSequenceTrackKind.State, 0, 1)));
        asset.EditorTracks.Add(new ProbeTrackDefinition(
            ActionSequenceTrackKind.State,
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 1)));

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
            ActionSequenceTrackKind.State,
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 2))
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
        var clip = new ProbeClipDefinition("A", ActionSequenceTrackKind.State, -5, 20);
        asset.EditorTracks.Add(new ProbeTrackDefinition(ActionSequenceTrackKind.State, clip));

        _ = new ActionSequenceRuntime(asset);

        Assert.AreEqual(-5, clip.startFrame);
        Assert.AreEqual(20, clip.endFrame);
    }

    [Test]
    public void Runtime_ReportsDiagnosticsWithoutMutatingAsset()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorSetTiming(60, 3);
        asset.EditorTracks.Clear();
        var adjusted = new ProbeClipDefinition("A", ActionSequenceTrackKind.State, -2, 10);
        var skipped = new ProbeClipDefinition("B", ActionSequenceTrackKind.State, 5, 8);
        var nullRuntime = new NullRuntimeClipDefinition(0, 1);
        var disallowed = new DisallowedStateClipDefinition(0, 1);
        var track = new ProbeTrackDefinition(ActionSequenceTrackKind.State, adjusted, skipped);
        track.EditorClips.Add(nullRuntime);
        track.EditorClips.Add(disallowed);
        asset.EditorTracks.Add(track);
        asset.EditorClips.Add(new ProbeClipDefinition("Legacy", ActionSequenceTrackKind.State, 0, 1));

        var runtime = new ActionSequenceRuntime(asset);

        Assert.IsTrue(runtime.Diagnostics.HasIssues);
        AssertHasDiagnostic(runtime.Diagnostics, ActionSequenceRuntimeDiagnosticCode.TimingAdjusted);
        AssertHasDiagnostic(runtime.Diagnostics, ActionSequenceRuntimeDiagnosticCode.FixedDurationClipTruncated);
        AssertHasDiagnostic(runtime.Diagnostics, ActionSequenceRuntimeDiagnosticCode.FixedDurationClipSkipped);
        AssertHasDiagnostic(runtime.Diagnostics, ActionSequenceRuntimeDiagnosticCode.NullClipRuntime);
        AssertHasDiagnostic(runtime.Diagnostics, ActionSequenceRuntimeDiagnosticCode.DisallowedClipType);
        AssertHasDiagnostic(runtime.Diagnostics, ActionSequenceRuntimeDiagnosticCode.LegacyClipProjection);
        Assert.AreEqual(-2, adjusted.startFrame);
        Assert.AreEqual(10, adjusted.endFrame);
        Assert.AreEqual(5, skipped.startFrame);
        Assert.AreEqual(8, skipped.endFrame);
        Assert.AreEqual(1, asset.EditorClips.Count);
    }

    private static ActionSequenceAsset CreateAsset(int durationFrames, params ActionSequenceClipDefinition[] clips)
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorSetTiming(60, durationFrames);
        asset.EditorTracks.Clear();

        for (int i = 0; i < clips.Length; i++)
        {
            ActionSequenceClipDefinition clip = clips[i];
            ProbeTrackDefinition track = FindProbeTrack(asset, clip.Kind);
            if (track == null)
            {
                track = new ProbeTrackDefinition(clip.Kind);
                asset.EditorTracks.Add(track);
            }

            track.AddClip(clip);
        }

        return asset;
    }

    private static ProbeTrackDefinition FindProbeTrack(ActionSequenceAsset asset, ActionSequenceTrackKind phase)
    {
        for (int i = 0; i < asset.EditorTracks.Count; i++)
        {
            if (asset.EditorTracks[i] is ProbeTrackDefinition track && track.Kind == phase)
                return track;
        }

        return null;
    }

    private static void AssertHasDiagnostic(ActionSequenceRuntimeDiagnostics diagnostics, ActionSequenceRuntimeDiagnosticCode code)
    {
        for (int i = 0; i < diagnostics.Issues.Count; i++)
        {
            if (diagnostics.Issues[i].Code == code)
                return;
        }

        Assert.Fail($"Expected runtime diagnostic {code}.");
    }

    private sealed class ProbeTrackDefinition : ActionSequenceTrackDefinition
    {
        private readonly ActionSequenceTrackKind _phase;
        private static readonly System.Type[] ClipTypes =
        {
            typeof(ProbeClipDefinition),
            typeof(BaselineProbeClipDefinition),
            typeof(PoseRefreshProbeClipDefinition),
            typeof(NullRuntimeClipDefinition),
        };

        public ProbeTrackDefinition(ActionSequenceTrackKind phase, params ActionSequenceClipDefinition[] clips)
        {
            _phase = phase;
            for (int i = 0; i < clips.Length; i++)
                AddClip(clips[i]);
        }

        public override ActionSequenceTrackKind Kind => _phase;
        public override System.Type[] AllowedClipTypes => ClipTypes;
    }

    private sealed class ProbeClipDefinition : ActionSequenceClipDefinition
    {
        private readonly string _id;
        private readonly ActionSequenceTrackKind _phase;

        public ProbeClipDefinition(string id, ActionSequenceTrackKind phase, int start, int end)
        {
            _id = id;
            _phase = phase;
            startFrame = start;
            endFrame = end;
        }

        public override ActionSequenceTrackKind Kind => _phase;

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

    private sealed class BaselineProbeClipDefinition : ActionSequenceClipDefinition
    {
        private readonly string _id;
        private readonly ActionSequenceTrackKind _phase;

        public BaselineProbeClipDefinition(string id, ActionSequenceTrackKind phase, int start, int end)
        {
            _id = id;
            _phase = phase;
            startFrame = start;
            endFrame = end;
        }

        public override ActionSequenceTrackKind Kind => _phase;

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
                Events(context).Add($"{_id}:enter:{context.Frame}:{BaselineState(context)}");
            }

            public override void OnTick(ActionSequenceContext context)
            {
                Events(context).Add($"{_id}:tick:{context.Frame}:{BaselineState(context)}");
            }

            private static List<string> Events(ActionSequenceContext context)
            {
                return (List<string>)context.UserData;
            }

            private static string BaselineState(ActionSequenceContext context)
            {
                return context.IsPoseBaseline ? "baseline" : "frame";
            }
        }
    }

    private sealed class PoseRefreshProbeClipDefinition : ActionSequenceClipDefinition
    {
        private readonly string _id;
        private readonly ActionSequenceTrackKind _phase;

        public PoseRefreshProbeClipDefinition(string id, ActionSequenceTrackKind phase, int start, int end)
        {
            _id = id;
            _phase = phase;
            startFrame = start;
            endFrame = end;
        }

        public override ActionSequenceTrackKind Kind => _phase;

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
                Events(context).Add($"{_id}:enter:{context.Frame}:{FormatPoseFrame(context)}:{State(context)}");
            }

            public override void OnTick(ActionSequenceContext context)
            {
                Events(context).Add($"{_id}:tick:{context.Frame}:{FormatPoseFrame(context)}:{State(context)}");
            }

            private static List<string> Events(ActionSequenceContext context)
            {
                return (List<string>)context.UserData;
            }

            private static string State(ActionSequenceContext context)
            {
                if (context.IsPoseBaseline)
                    return "baseline";
                return context.IsPoseRefresh ? "refresh" : "frame";
            }

            private static string FormatPoseFrame(ActionSequenceContext context)
            {
                return context.PoseFrame.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    private sealed class DisallowedStateClipDefinition : ActionSequenceClipDefinition
    {
        public DisallowedStateClipDefinition(int start, int end)
        {
            startFrame = start;
            endFrame = end;
        }

        public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.State;

        public override ActionSequenceClipRuntime CreateRuntime()
        {
            return new Runtime();
        }

        private sealed class Runtime : ActionSequenceClipRuntime
        {
        }
    }

    private sealed class NullRuntimeClipDefinition : ActionSequenceClipDefinition
    {
        public NullRuntimeClipDefinition(int start, int end)
        {
            startFrame = start;
            endFrame = end;
        }

        public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.State;

        public override ActionSequenceClipRuntime CreateRuntime()
        {
            return null;
        }
    }
}
