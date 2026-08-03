#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionSequenceEditorStateTests
{
    [TearDown]
    public void TearDown()
    {
        Undo.ClearAll();
    }

    [Test]
    public void WorkspaceRange_SupportsStandaloneAndSequenceActionTargets()
    {
        ActionSequenceAsset standalone = CreateAsset(ActionSequenceDurationMode.AutoFromClips, 60, 12);
        ActionAsset actionAsset = ScriptableObject.CreateInstance<ActionAsset>();
        actionAsset.SetPlaybackBackend(ActionPlaybackBackend.Sequence);
        actionAsset.SequenceData.EditorTracks.Clear();
        actionAsset.SequenceData.EditorTracks.Add(CreateStateTrack(0, 18));
        actionAsset.SequenceData.EditorSetDurationMode(ActionSequenceDurationMode.AutoFromClips);
        ActionSequenceEditorIdentity.UpgradeMissingIds(actionAsset);

        using var standaloneState = new ActionSequenceEditorState();
        using var actionState = new ActionSequenceEditorState();
        standaloneState.SetTarget(standalone);
        actionState.SetTarget(actionAsset);

        Assert.GreaterOrEqual(standaloneState.ViewEndFrame, 68);
        Assert.GreaterOrEqual(actionState.ViewEndFrame, 60);
        Assert.AreEqual(1, actionState.DisplayTracks.Count);
    }

    [Test]
    public void DisplayTracks_AreSortedByPhaseWithoutMutatingSerializedOrder()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorTracks.Add(new ActionSequenceHitBoxTrack());
        asset.EditorTracks.Add(new ActionSequenceStateTrack());
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);

        Assert.AreEqual(ActionSequenceClipPhase.State, state.DisplayTracks[0].Snapshot.Phase);
        Assert.AreEqual(ActionSequenceClipPhase.HitBox, asset.EditorTracks[0].Phase);
    }

    [Test]
    public void InvalidTiming_OnlyUsesTemporarySafeRenderInterval()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        ActionSequenceStateTrack track = CreateStateTrack(5, 4);
        asset.EditorTracks.Add(track);
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);

        ActionSequenceDisplayClip clip = state.DisplayTracks[0].Clips[0];
        Assert.AreEqual(5, clip.SafeStartFrame);
        Assert.AreEqual(6, clip.SafeEndFrame);
        Assert.AreEqual(4, track.EditorClips[0].endFrame);
    }

    [Test]
    public void FixedDurationOverflow_RemainsVisibleInWorkspace()
    {
        ActionSequenceAsset asset = CreateAsset(ActionSequenceDurationMode.FixedFrames, 5, 20);

        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);

        Assert.GreaterOrEqual(state.ViewEndFrame, 28);
        Assert.AreEqual(5, state.CalculateSequenceDurationFrames());
    }

    [Test]
    public void CurrentFrame_IsClampedToWorkspace()
    {
        ActionSequenceAsset asset = CreateAsset(ActionSequenceDurationMode.AutoFromClips, 60, 12);

        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);
        state.SetCurrentFrame(10000);

        Assert.AreEqual(state.ViewEndFrame, state.CurrentFrame);
    }

    [Test]
    public void TargetSwitch_StopsPlayback()
    {
        ActionSequenceAsset first = CreateAsset(ActionSequenceDurationMode.AutoFromClips, 60, 12);
        ActionSequenceAsset second = CreateAsset(ActionSequenceDurationMode.AutoFromClips, 60, 20);

        using var state = new ActionSequenceEditorState();
        state.SetTarget(first);
        Assert.IsTrue(state.TogglePlayback(0d, out _));

        state.SetTarget(second);

        Assert.IsFalse(state.IsPlaying);
    }

    [Test]
    public void InteractionPreview_GrowsAutoWorkspaceWithoutChangingDurationMode()
    {
        ActionSequenceAsset asset = CreateAsset(ActionSequenceDurationMode.AutoFromClips, 60, 12);

        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);
        ActionSequenceDisplayClip clip = state.DisplayTracks[0].Clips[0];

        state.BeginInteractionPreview(clip.Snapshot, clip, ActionSequenceClipTimingEditMode.Move);
        state.UpdateInteractionPreview(120, 124);

        Assert.GreaterOrEqual(state.ViewEndFrame, 132);
        Assert.AreEqual(ActionSequenceDurationMode.AutoFromClips, asset.DurationMode);
    }

    private static ActionSequenceAsset CreateAsset(ActionSequenceDurationMode mode, int fixedDuration, int clipEnd)
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();
        asset.EditorSetDurationMode(mode);
        asset.EditorSetTiming(60, fixedDuration);
        asset.EditorTracks.Add(CreateStateTrack(0, clipEnd));
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        return asset;
    }

    private static ActionSequenceStateTrack CreateStateTrack(int startFrame, int endFrame)
    {
        var track = new ActionSequenceStateTrack();
        track.EditorClips.Add(new ActionSequenceTagClipDefinition { startFrame = startFrame, endFrame = endFrame });
        return track;
    }
}
#endif
