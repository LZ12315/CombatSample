#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionSequenceEditorSelectionTests
{
    [TearDown]
    public void TearDown()
    {
        ActionSequenceEditorSelection.Clear();
    }

    [Test]
    public void StableClipSelection_ResolvesAfterTrackReorder()
    {
        ActionSequenceAsset asset = CreateAssetWithTwoTracks();
        ActionSequenceClipDefinition selectedClip = asset.EditorTracks[1].EditorClips[0];

        ActionSequenceEditorSelection.SelectClip(asset, selectedClip.EditorId);
        asset.EditorTracks.Reverse();

        var serializedObject = new SerializedObject(asset);
        SerializedProperty property = ActionSequenceEditorSelection.GetSelectedClipProperty(serializedObject);

        Assert.NotNull(property);
        Assert.AreSame(selectedClip, property.managedReferenceValue);
        Assert.AreEqual(ActionSequenceEditorSelection.SelectionKind.Clip, ActionSequenceEditorSelection.Kind);
    }

    [Test]
    public void LockedClipSelection_SelectsOwningTrack()
    {
        ActionSequenceAsset asset = CreateAssetWithTwoTracks();
        ActionSequenceTrackDefinition track = asset.EditorTracks[0];
        ActionSequenceClipDefinition clip = track.EditorClips[0];
        track.locked = true;

        ActionSequenceEditorSelection.SelectClip(asset, clip.EditorId);

        Assert.AreEqual(ActionSequenceEditorSelection.SelectionKind.Track, ActionSequenceEditorSelection.Kind);
        Assert.AreEqual(track.EditorId, ActionSequenceEditorSelection.TrackId);
        Assert.IsNull(ActionSequenceEditorSelection.ClipId);
    }

    [Test]
    public void DuplicateId_FallsBackToSequence()
    {
        ActionSequenceAsset asset = CreateAssetWithTwoTracks();
        string duplicate = asset.EditorTracks[0].EditorId;
        asset.EditorTracks[1].EditorSetEditorId(duplicate);

        ActionSequenceEditorSelection.SelectTrack(asset, duplicate);

        Assert.AreEqual(ActionSequenceEditorSelection.SelectionKind.Sequence, ActionSequenceEditorSelection.Kind);
        Assert.IsNull(ActionSequenceEditorSelection.TrackId);
    }

    [Test]
    public void LocalStateSelection_IsIndependentBetweenStates()
    {
        ActionSequenceAsset asset = CreateAssetWithTwoTracks();
        using var first = new ActionSequenceEditorState();
        using var second = new ActionSequenceEditorState();
        first.SetTarget(asset);
        second.SetTarget(asset);

        first.SelectTrack(asset.EditorTracks[0].EditorId);
        second.SelectTrack(asset.EditorTracks[1].EditorId);

        Assert.AreEqual(asset.EditorTracks[0].EditorId, first.LocalSelection.TrackId);
        Assert.AreEqual(asset.EditorTracks[1].EditorId, second.LocalSelection.TrackId);
    }

    [Test]
    public void PrototypePathFallback_StillFindsTrackWithoutStableId()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Add(new ActionSequenceStateTrack());
        var serializedObject = new SerializedObject(asset);
        SerializedProperty tracks = ActionSequenceEditorSelection.GetTracksProperty(serializedObject);
        SerializedProperty track = tracks.GetArrayElementAtIndex(0);

        ActionSequenceEditorSelection.SetTrack(asset, 0, track.propertyPath);

        Assert.AreEqual(ActionSequenceEditorSelection.SelectionKind.Track, ActionSequenceEditorSelection.Kind);
        Assert.AreSame(track.managedReferenceValue, ActionSequenceEditorSelection.GetSelectedTrackProperty(serializedObject).managedReferenceValue);
    }

    private static ActionSequenceAsset CreateAssetWithTwoTracks()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();

        var first = new ActionSequenceStateTrack();
        first.EditorClips.Add(new ActionSequenceTagClipDefinition { startFrame = 0, endFrame = 1 });
        asset.EditorTracks.Add(first);

        var second = new ActionSequenceMotionTrack();
        second.EditorClips.Add(new ActionSequenceImpulseClipDefinition { startFrame = 2, endFrame = 3 });
        asset.EditorTracks.Add(second);

        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        return asset;
    }
}
#endif
