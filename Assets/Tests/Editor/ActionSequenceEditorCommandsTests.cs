#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionSequenceEditorCommandsTests
{
    [TearDown]
    public void TearDown()
    {
        Undo.ClearAll();
    }

    [Test]
    public void AddTrack_InsertsAtPhaseGroupEndAndAssignsId()
    {
        ActionSequenceAsset asset = CreateAssetWithStateTrack();
        asset.EditorTracks.Add(new ActionSequenceHitBoxTrack());

        ActionSequenceEditorCommandResult result = ActionSequenceEditorCommands.AddTrack(asset, typeof(ActionSequenceMotionTrack));

        Assert.AreEqual(ActionSequenceEditorCommandStatus.Success, result.Status);
        Assert.AreEqual(ActionSequenceClipPhase.Motion, asset.EditorTracks[1].Phase);
        Assert.IsTrue(ActionSequenceEditorIdentity.IsValidEditorId(asset.EditorTracks[1].EditorId));
        Assert.AreEqual(ActionSequenceEditorDocumentItemKind.Track, result.SelectionSuggestion.Kind);
    }

    [Test]
    public void DuplicateTrackId_RejectedAsAmbiguous()
    {
        ActionSequenceAsset asset = CreateAssetWithStateTrack();
        asset.EditorTracks.Add(new ActionSequenceStateTrack());
        asset.EditorTracks[1].EditorSetEditorId(asset.EditorTracks[0].EditorId);

        ActionSequenceEditorCommandResult result = ActionSequenceEditorCommands.RenameTrack(asset, asset.EditorTracks[0].EditorId, "Renamed");

        Assert.AreEqual(ActionSequenceEditorCommandStatus.AmbiguousIdentity, result.Status);
    }

    [Test]
    public void LockedTrack_RejectsClipAddDeleteAndTimingButCanUnlock()
    {
        ActionSequenceAsset asset = CreateAssetWithStateTrack();
        string trackId = asset.EditorTracks[0].EditorId;
        string clipId = asset.EditorTracks[0].EditorClips[0].EditorId;
        asset.EditorTracks[0].locked = true;

        Assert.AreEqual(ActionSequenceEditorCommandStatus.Locked, ActionSequenceEditorCommands.AddClip(asset, trackId, typeof(ActionSequenceTagClipDefinition), 1, 2).Status);
        Assert.AreEqual(ActionSequenceEditorCommandStatus.Locked, ActionSequenceEditorCommands.SetClipTiming(asset, clipId, 1, 2).Status);
        Assert.AreEqual(ActionSequenceEditorCommandStatus.Locked, ActionSequenceEditorCommands.DeleteClip(asset, clipId).Status);

        ActionSequenceEditorCommandResult unlock = ActionSequenceEditorCommands.SetTrackLocked(asset, trackId, false);

        Assert.AreEqual(ActionSequenceEditorCommandStatus.Success, unlock.Status);
        Assert.IsFalse(asset.EditorTracks[0].locked);
    }

    [Test]
    public void DeleteNonEmptyTrack_RequiresConfirmationThenCanDeleteToZeroTracks()
    {
        ActionSequenceAsset asset = CreateAssetWithStateTrack();
        string trackId = asset.EditorTracks[0].EditorId;

        Assert.AreEqual(ActionSequenceEditorCommandStatus.ConfirmationRequired, ActionSequenceEditorCommands.DeleteTrack(asset, trackId).Status);

        ActionSequenceEditorCommandResult result = ActionSequenceEditorCommands.DeleteTrack(asset, trackId, true);

        Assert.AreEqual(ActionSequenceEditorCommandStatus.Success, result.Status);
        Assert.AreEqual(0, asset.EditorTracks.Count);
        Assert.AreEqual(ActionSequenceEditorDocumentItemKind.Sequence, result.SelectionSuggestion.Kind);
    }

    [Test]
    public void FixedDurationTiming_IsValidatedButDurationShrinkDoesNotClampExistingClip()
    {
        ActionSequenceAsset asset = CreateAssetWithStateTrack();
        asset.EditorSetDurationMode(ActionSequenceDurationMode.FixedFrames);
        asset.EditorSetTiming(60, 5);
        string trackId = asset.EditorTracks[0].EditorId;

        Assert.AreEqual(ActionSequenceEditorCommandStatus.InvalidTiming, ActionSequenceEditorCommands.AddClip(asset, trackId, typeof(ActionSequenceTagClipDefinition), 3, 8).Status);

        ActionSequenceEditorCommandResult shrink = ActionSequenceEditorCommands.SetFixedDurationFrames(asset, 1);

        Assert.AreEqual(ActionSequenceEditorCommandStatus.Success, shrink.Status);
        Assert.AreEqual(2, asset.EditorTracks[0].EditorClips[0].endFrame);
    }

    [Test]
    public void AddClip_GeneratesPersistentIdAndCanUndoRedo()
    {
        ActionSequenceAsset asset = CreateAssetWithStateTrack();
        string trackId = asset.EditorTracks[0].EditorId;

        ActionSequenceEditorCommandResult result = ActionSequenceEditorCommands.AddClip(asset, trackId, typeof(ActionSequenceTagClipDefinition), 2, 3);
        string clipId = result.AffectedClipId;

        Assert.AreEqual(ActionSequenceEditorCommandStatus.Success, result.Status);
        Assert.IsTrue(ActionSequenceEditorIdentity.IsValidEditorId(clipId));
        Assert.AreEqual(2, asset.EditorTracks[0].EditorClips.Count);

        Undo.PerformUndo();
        Assert.AreEqual(1, asset.EditorTracks[0].EditorClips.Count);

        Undo.PerformRedo();
        Assert.AreEqual(2, asset.EditorTracks[0].EditorClips.Count);
        Assert.AreEqual(clipId, asset.EditorTracks[0].EditorClips[1].EditorId);
    }

    [Test]
    public void MigrateLegacyClips_PreservesOrderAndIds()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();
        var first = new ActionSequenceTagClipDefinition { displayName = "First", startFrame = 0, endFrame = 1 };
        var second = new ActionSequenceTagClipDefinition { displayName = "Second", startFrame = 1, endFrame = 2 };
        asset.EditorClips.Add(first);
        asset.EditorClips.Add(second);
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        string firstId = first.EditorId;
        string secondId = second.EditorId;

        ActionSequenceEditorCommandResult result = ActionSequenceEditorCommands.MigrateLegacyClips(asset);

        Assert.AreEqual(ActionSequenceEditorCommandStatus.Success, result.Status);
        Assert.AreEqual(0, asset.EditorClips.Count);
        Assert.AreEqual(1, asset.EditorTracks.Count);
        Assert.AreSame(first, asset.EditorTracks[0].EditorClips[0]);
        Assert.AreSame(second, asset.EditorTracks[0].EditorClips[1]);
        Assert.AreEqual(firstId, first.EditorId);
        Assert.AreEqual(secondId, second.EditorId);
    }

    [Test]
    public void RepairTrackPhaseOrder_RejectsWhenLockedTrackWouldMove()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorTracks.Add(new ActionSequenceHitBoxTrack { locked = true });
        asset.EditorTracks.Add(new ActionSequenceStateTrack());
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        ActionSequenceEditorCommandResult result = ActionSequenceEditorCommands.RepairTrackPhaseOrder(asset);

        Assert.AreEqual(ActionSequenceEditorCommandStatus.Locked, result.Status);
    }

    private static ActionSequenceAsset CreateAssetWithStateTrack()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();
        asset.EditorSetDurationMode(ActionSequenceDurationMode.AutoFromClips);

        var track = new ActionSequenceStateTrack();
        track.EditorClips.Add(new ActionSequenceTagClipDefinition { startFrame = 0, endFrame = 2 });
        asset.EditorTracks.Add(track);
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        return asset;
    }
}
#endif
