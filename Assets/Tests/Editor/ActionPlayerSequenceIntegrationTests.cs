using System.Collections.Generic;
using System.Globalization;
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
    public void BeginAction_SequenceDefersFrameZeroUntilFixedTick()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action, ActionContext.None);

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
    public void OneFrameSequence_FinishesInTheSameFinishFrame()
    {
        ActionAsset action = CreateSequenceAction(
            1,
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 1));
        ActionPlayer player = CreatePlayer(out GameObject owner);
        int finishedCount = 0;
        player.OnActionFinished += _ => finishedCount++;

        try
        {
            player.BeginAction(action, ActionContext.None);

            Assert.AreEqual(0, finishedCount);
            Assert.IsNotNull(player.CurrentAction);
            Assert.AreEqual(0, ProbeClipDefinition.Events.Count);

            InvokePrivate(player, "PlayActionFrame", 1f / 60f);
            Assert.AreEqual(0, finishedCount);
            InvokePrivate(player, "FinishActionFrame");

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
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 3));
        ActionPlayer player = CreatePlayer(out GameObject owner);
        int finishedCount = 0;
        int interruptedCount = 0;
        player.OnActionFinished += _ => finishedCount++;
        player.OnActionInterrupted += _ => interruptedCount++;

        try
        {
            player.BeginAction(action, ActionContext.None);
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
    public void StopAction_AfterPlayFrameCancelsOpenFrameExactlyOnce()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("H", ActionSequenceTrackKind.HitBox, 0, 2),
            new ProbeClipDefinition("S", ActionSequenceTrackKind.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action, ActionContext.None);
            InvokePrivate(player, "PlayActionFrame", 1f / 60f);
            player.StopAction();

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
                ProbeClipDefinition.Events);
            Assert.IsNull(player.CurrentAction);

            InvokePrivate(player, "FinishActionFrame");
            Assert.AreEqual(6, ProbeClipDefinition.Events.Count);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void FixedTick_PlayFrameTicksAllActiveSequenceClips()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("H", ActionSequenceTrackKind.HitBox, 0, 2),
            new ProbeClipDefinition("S", ActionSequenceTrackKind.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action, ActionContext.None);
            InvokePrivate(player, "PlayActionFrame", 1f / 60f);

            CollectionAssert.AreEqual(
                new[] { "H:enter:0", "S:enter:0", "H:tick:0", "S:tick:0" },
                ProbeClipDefinition.Events);

            InvokePrivate(player, "FinishActionFrame");
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
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.SetBaseSpeed(0.5);
            player.BeginAction(action, ActionContext.None);

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
    public void HalfSpeed_RefreshesAnimationPoseBeforeFirstGameplayFrame()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new PoseRefreshProbeClipDefinition("A", ActionSequenceTrackKind.Animation, 0, 2),
            new ProbeClipDefinition("S", ActionSequenceTrackKind.State, 0, 2),
            new ProbeClipDefinition("M", ActionSequenceTrackKind.Motion, 0, 2),
            new ProbeClipDefinition("H", ActionSequenceTrackKind.HitBox, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.SetBaseSpeed(0.5);
            player.BeginAction(action, ActionContext.None);

            ExecuteFixedTick(player);

            CollectionAssert.AreEqual(
                new[]
                {
                    "A:enter:0:0:baseline",
                    "A:tick:0:0:baseline",
                    "A:tick:0:0.5:refresh",
                },
                ProbeClipDefinition.Events);
            Assert.AreEqual(0, player.CurrentFrame);
            Assert.AreEqual(0f, player.CurrentAction.RuntimeData.normalizedTime);

            ExecuteFixedTick(player);

            CollectionAssert.AreEqual(
                new[]
                {
                    "A:enter:0:0:baseline",
                    "A:tick:0:0:baseline",
                    "A:tick:0:0.5:refresh",
                    "S:enter:0",
                    "M:enter:0",
                    "H:enter:0",
                    "A:tick:0:1:frame",
                    "S:tick:0",
                    "M:tick:0",
                    "H:tick:0",
                },
                ProbeClipDefinition.Events);
            Assert.Greater(player.CurrentAction.RuntimeData.normalizedTime, 0f);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void HalfSpeed_DoesNotEnterSelfRotationBeforeFirstGameplayFrame()
    {
        var selfRotationClip = new ActionSequenceSelfRotationClipDefinition { startFrame = 0, endFrame = 2 };
        SetPrivateField(selfRotationClip, "animationKey", "attack");
        ActionAsset action = CreateSequenceAction(
            2,
            new PoseRefreshProbeClipDefinition("A", ActionSequenceTrackKind.Animation, 0, 2),
            selfRotationClip);
        AnimationConfig config = CreateAnimationConfig(new AnimationConfigEntry("attack", null, CreateTrajectory()));
        ActionPlayer player = CreatePlayerWithAnimationConfig(out GameObject owner, config);

        try
        {
            player.SetBaseSpeed(0.5);
            player.BeginAction(action, ActionContext.None);

            ExecuteFixedTick(player);

            CollectionAssert.AreEqual(
                new[]
                {
                    "A:enter:0:0:baseline",
                    "A:tick:0:0:baseline",
                    "A:tick:0:0.5:refresh",
                },
                ProbeClipDefinition.Events);
            Assert.IsNotNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ZeroSpeed_DoesNotEnterFrameZero()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.SetBaseSpeed(0.0);
            player.BeginAction(action, ActionContext.None);

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
    public void ZeroSpeed_AppliesOnlyPoseBaselineWithoutFractionalRefresh()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new PoseRefreshProbeClipDefinition("A", ActionSequenceTrackKind.Animation, 0, 2),
            new ProbeClipDefinition("S", ActionSequenceTrackKind.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.SetBaseSpeed(0.0);
            player.BeginAction(action, ActionContext.None);

            ExecuteFixedTick(player);
            ExecuteFixedTick(player);

            CollectionAssert.AreEqual(
                new[]
                {
                    "A:enter:0:0:baseline",
                    "A:tick:0:0:baseline",
                },
                ProbeClipDefinition.Events);
            Assert.IsNotNull(player.CurrentAction);
            Assert.AreEqual(0f, player.CurrentAction.RuntimeData.normalizedTime);
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
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action, ActionContext.None);
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
    public void Pause_DoesNotRefreshFractionalPose()
    {
        ActionAsset action = CreateSequenceAction(
            2,
            new PoseRefreshProbeClipDefinition("A", ActionSequenceTrackKind.Animation, 0, 2));
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.SetBaseSpeed(0.5);
            player.BeginAction(action, ActionContext.None);
            player.Pause();

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
    public void SequenceSpeedAboveOne_LogsAndInterruptsAction()
    {
        ActionAsset action = CreateSequenceAction(2);
        ActionPlayer player = CreatePlayer(out GameObject owner);
        int interruptedCount = 0;
        player.OnActionInterrupted += _ => interruptedCount++;

        try
        {
            player.BeginAction(action, ActionContext.None);
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
        SetPrivateField(action.SequenceData, "frameRate", 30);
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("Gameplay Sequence .*60 Hz.*30 Hz"));

            player.BeginAction(action, ActionContext.None);

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
    public void SequencePoseClip_WithoutAnimationConfig_IsRejectedBeforeActionEnter()
    {
        var poseClip = new ActionSequenceAnimationPoseClipDefinition { startFrame = 0, endFrame = 1 };
        SetPrivateField(poseClip, "animationKey", "attack");
        ActionAsset action = CreateSequenceAction(1, poseClip);
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("AnimationConfig"));

            player.BeginAction(action, ActionContext.None);

            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequencePoseClip_MissingKey_IsRejectedBeforeActionEnter()
    {
        var poseClip = new ActionSequenceAnimationPoseClipDefinition { startFrame = 0, endFrame = 1 };
        SetPrivateField(poseClip, "animationKey", "missing");
        ActionAsset action = CreateSequenceAction(1, poseClip);
        ActionPlayer player = CreatePlayerWithAnimationConfig(out GameObject owner, CreateAnimationConfig());

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("missing.*Transition"));

            player.BeginAction(action, ActionContext.None);

            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequenceRootMotionClip_MissingTrajectory_IsRejectedBeforeActionEnter()
    {
        var rootClip = new ActionSequenceRootMotionClipDefinition { startFrame = 0, endFrame = 1 };
        SetPrivateField(rootClip, "animationKey", "missing");
        ActionAsset action = CreateSequenceAction(1, rootClip);
        ActionPlayer player = CreatePlayerWithAnimationConfig(out GameObject owner, CreateAnimationConfig());

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("missing.*RootMotionTrajectory"));

            player.BeginAction(action, ActionContext.None);

            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequenceRootMotionClip_OverlappingIntervalsAreRejectedBeforeActionEnter()
    {
        var first = new ActionSequenceRootMotionClipDefinition { startFrame = 0, endFrame = 2 };
        var second = new ActionSequenceRootMotionClipDefinition { startFrame = 1, endFrame = 3 };
        SetPrivateField(first, "animationKey", "attack");
        SetPrivateField(second, "animationKey", "attack");
        ActionAsset action = CreateSequenceAction(3, first, second);
        AnimationConfig config = CreateAnimationConfig(new AnimationConfigEntry("attack", null, CreateTrajectory()));
        ActionPlayer player = CreatePlayerWithAnimationConfig(out GameObject owner, config);

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("RootMotionClip.*\\[0, 2\\).*\\[1, 3\\)"));

            player.BeginAction(action, ActionContext.None);

            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequenceSelfRotationClip_MissingTrajectory_IsRejectedBeforeActionEnter()
    {
        var selfRotationClip = new ActionSequenceSelfRotationClipDefinition { startFrame = 0, endFrame = 1 };
        SetPrivateField(selfRotationClip, "animationKey", "missing");
        ActionAsset action = CreateSequenceAction(1, selfRotationClip);
        ActionPlayer player = CreatePlayerWithAnimationConfig(out GameObject owner, CreateAnimationConfig());

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("missing.*RootMotionTrajectory"));

            player.BeginAction(action, ActionContext.None);

            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequenceSelfRotationClip_TargetSourceDoesNotRequireAnimationConfig()
    {
        var selfRotationClip = new ActionSequenceSelfRotationClipDefinition { startFrame = 0, endFrame = 1 };
        SetPrivateField(selfRotationClip, "source", SelfRotationSource.Target);
        SetPrivateField(selfRotationClip, "targetSource", SelfRotationTargetSource.ContextTarget);
        ActionAsset action = CreateSequenceAction(1, selfRotationClip);
        ActionPlayer player = CreatePlayer(out GameObject owner);
        var target = new GameObject("SelfRotation Context Target");

        try
        {
            player.BeginAction(action, ActionContext.ForParticipants(null, target));

            Assert.IsNotNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequenceSelfRotationClip_ContextTargetMissingContext_IsRejectedBeforeActionEnter()
    {
        var selfRotationClip = new ActionSequenceSelfRotationClipDefinition { startFrame = 0, endFrame = 1 };
        SetPrivateField(selfRotationClip, "source", SelfRotationSource.Target);
        SetPrivateField(selfRotationClip, "targetSource", SelfRotationTargetSource.ContextTarget);
        ActionAsset action = CreateSequenceAction(1, selfRotationClip);
        action.name = "SelfRotation Requires Target";
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                "Action 'SelfRotation Requires Target' requires context fields Target but the candidate context does not provide them.");

            player.BeginAction(action, ActionContext.None);

            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequenceSelfRotationClip_InvalidPresetLocal_IsRejectedBeforeActionEnter()
    {
        var selfRotationClip = new ActionSequenceSelfRotationClipDefinition { startFrame = 0, endFrame = 1 };
        SetPrivateField(selfRotationClip, "source", SelfRotationSource.Direction);
        SetPrivateField(selfRotationClip, "directionSource", SelfRotationDirectionSource.PresetLocal);
        SetPrivateField(selfRotationClip, "presetLocalDirection", Vector3.zero);
        ActionAsset action = CreateSequenceAction(1, selfRotationClip);
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("SelfRotationClip.*PresetLocal"));

            player.BeginAction(action, ActionContext.None);

            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void SequenceSelfRotationClip_OverlappingIntervalsAreRejectedBeforeActionEnter()
    {
        var first = new ActionSequenceSelfRotationClipDefinition { startFrame = 0, endFrame = 2 };
        var second = new ActionSequenceSelfRotationClipDefinition { startFrame = 1, endFrame = 3 };
        SetPrivateField(first, "animationKey", "attack");
        SetPrivateField(second, "animationKey", "attack");
        ActionAsset action = CreateSequenceAction(3, first, second);
        AnimationConfig config = CreateAnimationConfig(new AnimationConfigEntry("attack", null, CreateYawTrajectory()));
        ActionPlayer player = CreatePlayerWithAnimationConfig(out GameObject owner, config);

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("SelfRotationClip.*\\[0, 2\\).*\\[1, 3\\)"));

            player.BeginAction(action, ActionContext.None);

            Assert.IsNull(player.CurrentAction);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void LoopSequence_RestartsWithoutExecutingNewFrameZeroInSameTick()
    {
        ActionAsset action = CreateSequenceAction(
            1,
            new ProbeClipDefinition("A", ActionSequenceTrackKind.State, 0, 1));
        SetPrivateField(action, "isLoop", true);
        ActionPlayer player = CreatePlayer(out GameObject owner);

        try
        {
            player.BeginAction(action, ActionContext.None);
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

    private static ActionPlayer CreatePlayerWithAnimationConfig(out GameObject owner, AnimationConfig config)
    {
        ActionPlayer player = CreatePlayer(out owner);
        Actor actor = owner.AddComponent<Actor>();
        SetPrivateField(actor, "animationConfig", config);
        SetPrivateField(player, "_actor", actor);
        return player;
    }

    private static void ExecuteFixedTick(ActionPlayer player)
    {
        InvokePrivate(player, "PlayActionFrame", 1f / 60f);
        InvokePrivate(player, "FinishActionFrame");
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
            ProbeTrackDefinition track = FindTrack(action, clip.Kind);
            if (track == null)
            {
                track = new ProbeTrackDefinition(clip.Kind);
                action.SequenceData.EditorTracks.Add(track);
            }

            track.AddClip(clip);
        }

        return action;
    }

    private static AnimationConfig CreateAnimationConfig(params AnimationConfigEntry[] entries)
    {
        AnimationConfig config = ScriptableObject.CreateInstance<AnimationConfig>();
        config.EditorSetEntries(entries);
        return config;
    }

    private static RootMotionTrajectory CreateTrajectory()
    {
        var clip = new AnimationClip();
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(
            clip,
            60,
            1f,
            1,
            "test-hash",
            new[] { 0f, 1f },
            new[] { Vector3.zero, Vector3.forward },
            new[] { Quaternion.identity, Quaternion.identity });
        return trajectory;
    }

    private static RootMotionTrajectory CreateYawTrajectory()
    {
        var clip = new AnimationClip();
        var trajectory = new RootMotionTrajectory();
        trajectory.EditorSetData(
            clip,
            60,
            1f,
            1,
            "test-hash",
            new[] { 0f, 1f },
            new[] { Vector3.zero, Vector3.zero },
            new[] { Quaternion.identity, Quaternion.Euler(0f, 90f, 0f) });
        return trajectory;
    }

    private static ProbeTrackDefinition FindTrack(ActionAsset action, ActionSequenceTrackKind phase)
    {
        for (int i = 0; i < action.SequenceData.EditorTracks.Count; i++)
        {
            if (action.SequenceData.EditorTracks[i] is ProbeTrackDefinition track && track.Kind == phase)
                return track;
        }

        return null;
    }

    private sealed class ProbeTrackDefinition : ActionSequenceTrackDefinition
    {
        private static readonly System.Type[] ClipTypes =
        {
            typeof(ProbeClipDefinition),
            typeof(PoseRefreshProbeClipDefinition),
            typeof(ActionSequenceAnimationPoseClipDefinition),
            typeof(ActionSequenceRootMotionClipDefinition),
            typeof(ActionSequenceSelfRotationClipDefinition),
        };
        private readonly ActionSequenceTrackKind _phase;

        public ProbeTrackDefinition(ActionSequenceTrackKind phase)
        {
            _phase = phase;
        }

        public override ActionSequenceTrackKind Kind => _phase;
        public override System.Type[] AllowedClipTypes => ClipTypes;
    }

    private sealed class ProbeClipDefinition : ActionSequenceClipDefinition
    {
        public static readonly List<string> Events = new List<string>();
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
                ProbeClipDefinition.Events.Add($"{_id}:enter:{context.Frame}:{FormatPoseFrame(context)}:{State(context)}");
            }

            public override void OnTick(ActionSequenceContext context)
            {
                ProbeClipDefinition.Events.Add($"{_id}:tick:{context.Frame}:{FormatPoseFrame(context)}:{State(context)}");
            }

            private static string State(ActionSequenceContext context)
            {
                if (context.IsPoseBaseline)
                    return "baseline";
                return context.IsPoseRefresh ? "refresh" : "frame";
            }

            private static string FormatPoseFrame(ActionSequenceContext context)
            {
                return context.PoseFrame.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }
    }
}
