#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class ActionSequenceSerializedDocumentTests
{
    [TearDown]
    public void TearDown()
    {
        UnityEditor.Undo.ClearAll();
    }

    [Test]
    public void Open_SupportsSequenceActionAssetAndStandaloneAssetButRejectsLegacyTimeline()
    {
        ActionSequenceAsset sequenceAsset = CreateSequenceAsset();
        ActionAsset actionAsset = ScriptableObject.CreateInstance<ActionAsset>();
        actionAsset.SetPlaybackBackend(ActionPlaybackBackend.Sequence);
        actionAsset.SequenceData.EditorTracks.Clear();
        actionAsset.SequenceData.EditorTracks.Add(new ActionSequenceStateTrack());
        ActionSequenceEditorIdentity.UpgradeMissingIds(actionAsset);

        using ActionSequenceSerializedDocument standaloneDocument = ActionSequenceSerializedDocument.Open(sequenceAsset);
        using ActionSequenceSerializedDocument actionDocument = ActionSequenceSerializedDocument.Open(actionAsset);
        using ActionSequenceSerializedDocument legacyDocument = ActionSequenceSerializedDocument.Open(ScriptableObject.CreateInstance<ActionAsset>());

        Assert.IsTrue(standaloneDocument.IsSupported);
        Assert.AreEqual("data", standaloneDocument.RootPropertyPath);
        Assert.IsTrue(actionDocument.IsSupported);
        Assert.AreEqual("_sequenceData", actionDocument.RootPropertyPath);
        Assert.IsFalse(legacyDocument.IsSupported);
    }

    [Test]
    public void ResolveTrackId_UsesStableIdAfterReorder()
    {
        ActionSequenceAsset asset = CreateSequenceAsset();
        string firstId = asset.EditorTracks[0].EditorId;

        asset.EditorTracks.Reverse();

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(asset);

        Assert.AreEqual(ActionSequenceEditorResolveStatus.Found, document.ResolveTrack(firstId, out int trackIndex));
        Assert.AreEqual(1, trackIndex);
    }

    [Test]
    public void DuplicateId_ResolvesAsAmbiguous()
    {
        ActionSequenceAsset asset = CreateSequenceAsset();
        string duplicate = asset.EditorTracks[0].EditorId;
        asset.EditorTracks[1].EditorSetEditorId(duplicate);

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(asset);

        Assert.AreEqual(ActionSequenceEditorResolveStatus.Ambiguous, document.ResolveTrack(duplicate, out _));
    }

    [Test]
    public void Snapshot_IncludesTrackClipAndLegacyData()
    {
        ActionSequenceAsset asset = CreateSequenceAsset();
        asset.EditorClips.Add(new ActionSequenceTagClipDefinition { startFrame = 4, endFrame = 6 });
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(asset);

        Assert.AreEqual(2, document.Tracks.Count);
        Assert.AreEqual(1, document.Tracks[0].Clips.Count);
        Assert.AreEqual(1, document.LegacyClips.Count);
        Assert.AreEqual(ActionSequenceClipPhase.State, document.Tracks[0].Phase);
        Assert.AreEqual(4, document.LegacyClips[0].StartFrame);
        Assert.AreEqual(6, document.LegacyClips[0].EndFrame);
    }

    [Test]
    public void Refresh_OnlyAdvancesRevisionWhenSnapshotChanges()
    {
        ActionSequenceAsset asset = CreateSequenceAsset();
        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(asset);
        int initialRevision = document.Revision;

        Assert.IsFalse(document.Refresh());
        Assert.AreEqual(initialRevision, document.Revision);

        asset.EditorTracks[0].displayName = "Changed";

        Assert.IsTrue(document.Refresh());
        Assert.AreEqual(initialRevision + 1, document.Revision);
    }

    private static ActionSequenceAsset CreateSequenceAsset()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();

        var first = new ActionSequenceStateTrack();
        first.EditorClips.Add(new ActionSequenceTagClipDefinition { startFrame = 0, endFrame = 1 });
        asset.EditorTracks.Add(first);
        asset.EditorTracks.Add(new ActionSequenceMotionTrack());
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        return asset;
    }
}
#endif
