#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionSequenceEditorIdentityTests
{
    private readonly List<string> _assetsToDelete = new List<string>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _assetsToDelete.Count; i++)
            AssetDatabase.DeleteAsset(_assetsToDelete[i]);

        _assetsToDelete.Clear();
        Undo.ClearAll();
    }

    [Test]
    public void UpgradeMissingIds_GeneratesValidUniqueIdsForTracksTrackClipsAndLegacyClips()
    {
        ActionSequenceAsset asset = CreateAssetWithTrackClipAndLegacyClip();

        int changed = ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        List<string> ids = CollectIds(asset.Data);

        Assert.AreEqual(3, changed);
        Assert.AreEqual(3, ids.Count);
        AssertAllValid(ids);
        AssertAllUnique(ids);
    }

    [Test]
    public void UpgradeMissingIds_CoversDefaultTracks()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.Data.InitializeNewSequenceDefaults();

        int changed = ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        Assert.AreEqual(5, changed);
        for (int i = 0; i < asset.EditorTracks.Count; i++)
            Assert.IsTrue(ActionSequenceEditorIdentity.IsValidEditorId(asset.EditorTracks[i].EditorId));
    }

    [Test]
    public void UpgradeMissingIds_IsIdempotent()
    {
        ActionSequenceAsset asset = CreateAssetWithTrackClipAndLegacyClip();

        Assert.AreEqual(3, ActionSequenceEditorIdentity.UpgradeMissingIds(asset));
        List<string> firstIds = CollectIds(asset.Data);

        Assert.AreEqual(0, ActionSequenceEditorIdentity.UpgradeMissingIds(asset));
        CollectionAssert.AreEqual(firstIds, CollectIds(asset.Data));
    }

    [Test]
    public void Validate_ReportsMissingMalformedAndDuplicateWithoutMutation()
    {
        ActionSequenceAsset asset = CreateAssetWithTrackClipAndLegacyClip();
        string duplicateId = NewId();
        ActionSequenceTrackDefinition track = asset.EditorTracks[0];
        ActionSequenceClipDefinition clip = track.EditorClips[0];
        ActionSequenceClipDefinition legacyClip = asset.EditorClips[0];

        clip.EditorSetEditorId("not-a-guid");
        legacyClip.EditorSetEditorId(duplicateId);
        track.EditorSetEditorId(duplicateId);

        ActionSequenceEditorIdentityValidationResult result = ActionSequenceEditorIdentity.Validate(asset);

        Assert.AreEqual(0, result.MissingCount);
        Assert.AreEqual(1, result.MalformedCount);
        Assert.AreEqual(1, result.DuplicateCount);
        Assert.AreEqual("not-a-guid", clip.EditorId);
        Assert.AreEqual(duplicateId, track.EditorId);
        Assert.AreEqual(duplicateId, legacyClip.EditorId);
    }

    [Test]
    public void Validate_ReportsMissing()
    {
        ActionSequenceAsset asset = CreateAssetWithTrackClipAndLegacyClip();

        ActionSequenceEditorIdentityValidationResult result = ActionSequenceEditorIdentity.Validate(asset);

        Assert.AreEqual(3, result.MissingCount);
        Assert.AreEqual(0, result.MalformedCount);
        Assert.AreEqual(0, result.DuplicateCount);
    }

    [Test]
    public void RepairInvalidIds_KeepsFirstDuplicateAndRepairsLaterDuplicateMalformedAndMissing()
    {
        ActionSequenceAsset asset = CreateAssetWithTrackClipAndLegacyClip();
        string duplicateId = NewId();
        ActionSequenceTrackDefinition track = asset.EditorTracks[0];
        ActionSequenceClipDefinition clip = track.EditorClips[0];
        ActionSequenceClipDefinition legacyClip = asset.EditorClips[0];

        track.EditorSetEditorId(duplicateId);
        clip.EditorSetEditorId(duplicateId);
        legacyClip.EditorSetEditorId("BAD");
        asset.EditorTracks.Add(new ActionSequenceStateTrack());

        int changed = ActionSequenceEditorIdentity.RepairInvalidIds(asset);

        Assert.AreEqual(3, changed);
        Assert.AreEqual(duplicateId, track.EditorId);
        Assert.AreNotEqual(duplicateId, clip.EditorId);
        Assert.AreNotEqual("BAD", legacyClip.EditorId);
        AssertAllValid(CollectIds(asset.Data));
        AssertAllUnique(CollectIds(asset.Data));
    }

    [Test]
    public void Upgrade_CanUndoAndRedoAsOneOperation()
    {
        ActionSequenceAsset asset = CreateAssetWithTrackClipAndLegacyClip();

        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        List<string> upgradedIds = CollectIds(asset.Data);

        Undo.PerformUndo();
        Assert.AreEqual(0, CollectNonEmptyIds(asset.Data).Count);

        Undo.PerformRedo();
        CollectionAssert.AreEqual(upgradedIds, CollectIds(asset.Data));
    }

    [Test]
    public void Repair_CanUndoAndRedoAsOneOperation()
    {
        ActionSequenceAsset asset = CreateAssetWithTrackClipAndLegacyClip();
        string validId = NewId();
        asset.EditorTracks[0].EditorSetEditorId(validId);
        asset.EditorTracks[0].EditorClips[0].EditorSetEditorId(validId);
        asset.EditorClips[0].EditorSetEditorId("BAD");

        ActionSequenceEditorIdentity.RepairInvalidIds(asset);
        List<string> repairedIds = CollectIds(asset.Data);

        Undo.PerformUndo();
        Assert.AreEqual(validId, asset.EditorTracks[0].EditorId);
        Assert.AreEqual(validId, asset.EditorTracks[0].EditorClips[0].EditorId);
        Assert.AreEqual("BAD", asset.EditorClips[0].EditorId);

        Undo.PerformRedo();
        CollectionAssert.AreEqual(repairedIds, CollectIds(asset.Data));
    }

    [Test]
    public void ClipGuid_UsesSessionFallbackBeforeUpgradeAndEditorIdAfterUpgrade()
    {
        var clip = new ActionSequenceTagClipDefinition();

        string first = clip.Guid;
        string second = clip.Guid;
        string editorId = NewId();
        clip.EditorSetEditorId(editorId);

        Assert.AreEqual(first, second);
        Assert.AreEqual(editorId, clip.Guid);
    }

    [Test]
    public void Reorder_DoesNotChangeExistingIds()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();

        var firstTrack = new ActionSequenceStateTrack();
        var secondTrack = new ActionSequenceStateTrack();
        var firstClip = new ActionSequenceTagClipDefinition();
        var secondClip = new ActionSequenceTagClipDefinition();
        firstTrack.EditorClips.Add(firstClip);
        firstTrack.EditorClips.Add(secondClip);
        asset.EditorTracks.Add(firstTrack);
        asset.EditorTracks.Add(secondTrack);

        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        string firstTrackId = firstTrack.EditorId;
        string secondTrackId = secondTrack.EditorId;
        string firstClipId = firstClip.EditorId;
        string secondClipId = secondClip.EditorId;

        asset.EditorTracks.Reverse();
        firstTrack.EditorClips.Reverse();

        Assert.AreEqual(firstTrackId, firstTrack.EditorId);
        Assert.AreEqual(secondTrackId, secondTrack.EditorId);
        Assert.AreEqual(firstClipId, firstClip.EditorId);
        Assert.AreEqual(secondClipId, secondClip.EditorId);
    }

    [Test]
    public void Upgrade_ZeroTrackAssetRemainsZeroTrack()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();

        Assert.AreEqual(0, ActionSequenceEditorIdentity.UpgradeMissingIds(asset));
        Assert.AreEqual(0, asset.EditorTracks.Count);
    }

    [Test]
    public void IdentityOperations_DoNotChangeAuthoringData()
    {
        ActionSequenceAsset asset = CreateAssetWithTrackClipAndLegacyClip();
        ActionSequenceTrackDefinition track = asset.EditorTracks[0];
        ActionSequenceClipDefinition clip = track.EditorClips[0];
        track.displayName = "State A";
        clip.displayName = "Tag A";
        clip.startFrame = 4;
        clip.endFrame = 9;

        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        Assert.AreEqual(1, asset.EditorTracks.Count);
        Assert.AreEqual(1, track.EditorClips.Count);
        Assert.AreEqual(1, asset.EditorClips.Count);
        Assert.AreEqual("State A", track.displayName);
        Assert.AreEqual("Tag A", clip.displayName);
        Assert.AreEqual(4, clip.startFrame);
        Assert.AreEqual(9, clip.endFrame);
        Assert.AreEqual(ActionSequenceClipPhase.State, track.Phase);
        Assert.AreEqual(ActionSequenceClipPhase.State, clip.Phase);
    }

    [Test]
    public void Upgrade_SupportsSequenceActionAssetAndActionSequenceAsset()
    {
        ActionSequenceAsset sequenceAsset = CreateAssetWithTrackClipAndLegacyClip();
        ActionAsset actionAsset = ScriptableObject.CreateInstance<ActionAsset>();
        actionAsset.SetPlaybackBackend(ActionPlaybackBackend.Sequence);
        actionAsset.SequenceData.EditorTracks.Clear();
        actionAsset.SequenceData.EditorTracks.Add(new ActionSequenceStateTrack());

        Assert.AreEqual(3, ActionSequenceEditorIdentity.UpgradeMissingIds(sequenceAsset));
        Assert.AreEqual(1, ActionSequenceEditorIdentity.UpgradeMissingIds(actionAsset));
    }

    [Test]
    public void LegacyTimelineActionAsset_IsUnsupportedAndUnmodified()
    {
        ActionAsset actionAsset = ScriptableObject.CreateInstance<ActionAsset>();

        Assert.AreEqual(ActionSequenceEditorIdentityTargetStatus.Unsupported, ActionSequenceEditorIdentity.Validate(actionAsset).Status);
        Assert.AreEqual(0, ActionSequenceEditorIdentity.UpgradeMissingIds(actionAsset));
    }

    [Test]
    public void CreateSequenceActionAsset_FirstSaveContainsTrackIds()
    {
        string path = RegisterTempAsset("ActionIdentityAction");

        ActionAsset asset = ActionAssetCreater.CreateSequenceActionAsset(path);

        Assert.IsNotNull(asset);
        Assert.AreEqual(0, ActionSequenceEditorIdentity.Validate(asset).MissingCount);
        Assert.Greater(asset.SequenceData.EditorTracks.Count, 0);
        AssertAllValid(CollectIds(asset.SequenceData));
    }

    [Test]
    public void SaveImportReload_PreservesIds()
    {
        string path = RegisterTempAsset("ActionIdentityStandalone");
        ActionSequenceAsset asset = CreateAssetWithTrackClipAndLegacyClip();
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        List<string> beforeSave = CollectIds(asset.Data);

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        ActionSequenceAsset loaded = AssetDatabase.LoadAssetAtPath<ActionSequenceAsset>(path);
        CollectionAssert.AreEqual(beforeSave, CollectIds(loaded.Data));
    }

    private static ActionSequenceAsset CreateAssetWithTrackClipAndLegacyClip()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();

        var track = new ActionSequenceStateTrack();
        track.EditorClips.Add(new ActionSequenceTagClipDefinition { startFrame = 1, endFrame = 3 });
        asset.EditorTracks.Add(track);
        asset.EditorClips.Add(new ActionSequenceTagClipDefinition { startFrame = 2, endFrame = 4 });
        return asset;
    }

    private string RegisterTempAsset(string prefix)
    {
        string path = $"Assets/Tests/{prefix}_{System.Guid.NewGuid():N}.asset";
        _assetsToDelete.Add(path);
        return path;
    }

    private static string NewId()
    {
        return System.Guid.NewGuid().ToString("N");
    }

    private static List<string> CollectIds(ActionSequenceData data)
    {
        var ids = new List<string>();
        List<ActionSequenceTrackDefinition> tracks = data.EditorTracks;
        for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
        {
            ActionSequenceTrackDefinition track = tracks[trackIndex];
            if (track == null)
                continue;

            ids.Add(track.EditorId);
            List<ActionSequenceClipDefinition> clips = track.EditorClips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                if (clips[clipIndex] != null)
                    ids.Add(clips[clipIndex].EditorId);
            }
        }

        List<ActionSequenceClipDefinition> legacyClips = data.EditorClips;
        for (int i = 0; i < legacyClips.Count; i++)
        {
            if (legacyClips[i] != null)
                ids.Add(legacyClips[i].EditorId);
        }

        return ids;
    }

    private static List<string> CollectNonEmptyIds(ActionSequenceData data)
    {
        List<string> ids = CollectIds(data);
        for (int i = ids.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrEmpty(ids[i]))
                ids.RemoveAt(i);
        }

        return ids;
    }

    private static void AssertAllValid(List<string> ids)
    {
        for (int i = 0; i < ids.Count; i++)
            Assert.IsTrue(ActionSequenceEditorIdentity.IsValidEditorId(ids[i]), ids[i]);
    }

    private static void AssertAllUnique(List<string> ids)
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < ids.Count; i++)
            Assert.IsTrue(seen.Add(ids[i]), ids[i]);
    }
}
#endif
