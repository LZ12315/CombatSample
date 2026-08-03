#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionSequenceViewReconcileTests
{
    [TearDown]
    public void TearDown()
    {
        Undo.ClearAll();
    }

    [Test]
    public void SameIds_ReuseTrackAndClipViewsAfterRefresh()
    {
        ActionSequenceAsset asset = CreateTwoTrackAsset();
        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);

        var header = new ActionSequenceTrackHeaderView();
        var lanes = new ActionSequenceTrackLaneView();
        header.Reconcile(state.DisplayTracks);
        lanes.Reconcile(state.DisplayTracks, state.Transform);
        ActionSequenceTrackHeaderRow firstHeader = header.GetRow(state.DisplayTracks[0].RenderKey);
        ActionSequenceTrackLaneRow firstLane = lanes.GetRow(state.DisplayTracks[0].RenderKey);
        ActionSequenceClipView firstClip = firstLane.GetClipView(state.DisplayTracks[0].Clips[0].RenderKey);

        asset.EditorTracks[0].displayName = "Renamed";
        state.Refresh();
        header.Reconcile(state.DisplayTracks);
        lanes.Reconcile(state.DisplayTracks, state.Transform);

        Assert.AreSame(firstHeader, header.GetRow(state.DisplayTracks[0].RenderKey));
        Assert.AreSame(firstLane, lanes.GetRow(state.DisplayTracks[0].RenderKey));
        Assert.AreSame(firstClip, lanes.GetRow(state.DisplayTracks[0].RenderKey).GetClipView(state.DisplayTracks[0].Clips[0].RenderKey));
    }

    [Test]
    public void AddDeleteAndReorder_OnlyChangesAffectedViews()
    {
        ActionSequenceAsset asset = CreateTwoTrackAsset();
        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);

        var header = new ActionSequenceTrackHeaderView();
        header.Reconcile(state.DisplayTracks);
        ActionSequenceTrackHeaderRow stateRow = header.GetRow(state.DisplayTracks[0].RenderKey);

        asset.EditorTracks.Add(new ActionSequenceHitBoxTrack());
        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        state.Refresh();
        header.Reconcile(state.DisplayTracks);

        Assert.AreEqual(3, header.RowCount);
        Assert.AreSame(stateRow, header.GetRow(state.DisplayTracks[0].RenderKey));

        asset.EditorTracks.RemoveAt(2);
        state.Refresh();
        header.Reconcile(state.DisplayTracks);

        Assert.AreEqual(2, header.RowCount);

        asset.EditorTracks.Reverse();
        state.Refresh();
        header.Reconcile(state.DisplayTracks);

        Assert.AreSame(stateRow, header.GetRow(state.DisplayTracks[0].RenderKey));
        Assert.AreEqual(ActionSequenceClipPhase.State, state.DisplayTracks[0].Snapshot.Phase);
    }

    [Test]
    public void DuplicateAndMissingIds_UseFallbackKeysWithoutCrashing()
    {
        ActionSequenceAsset asset = CreateTwoTrackAsset();
        asset.EditorTracks[1].EditorSetEditorId(asset.EditorTracks[0].EditorId);
        asset.EditorTracks[0].EditorClips[0].EditorSetEditorId(string.Empty);

        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);

        var header = new ActionSequenceTrackHeaderView();
        var lanes = new ActionSequenceTrackLaneView();
        header.Reconcile(state.DisplayTracks);
        lanes.Reconcile(state.DisplayTracks, state.Transform);

        Assert.AreEqual(2, header.RowCount);
        Assert.AreNotEqual(state.DisplayTracks[0].RenderKey, state.DisplayTracks[1].RenderKey);
        Assert.IsNotNull(lanes.GetRow(state.DisplayTracks[0].RenderKey).GetClipView(state.DisplayTracks[0].Clips[0].RenderKey));
    }

    [Test]
    public void ZoomAndPan_UpdateGeometryWithoutRebuildingViews()
    {
        ActionSequenceAsset asset = CreateTwoTrackAsset();
        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);

        var lanes = new ActionSequenceTrackLaneView();
        lanes.Reconcile(state.DisplayTracks, state.Transform);
        ActionSequenceTrackLaneRow row = lanes.GetRow(state.DisplayTracks[0].RenderKey);
        ActionSequenceClipView clip = row.GetClipView(state.DisplayTracks[0].Clips[0].RenderKey);

        state.SetViewport(400f, 200f);
        state.ZoomAt(100f, 20f);
        state.SetHorizontalScroll(30f);
        lanes.RefreshGeometry(state.DisplayTracks, state.Transform);

        Assert.AreSame(row, lanes.GetRow(state.DisplayTracks[0].RenderKey));
        Assert.AreSame(clip, lanes.GetRow(state.DisplayTracks[0].RenderKey).GetClipView(state.DisplayTracks[0].Clips[0].RenderKey));
    }

    [Test]
    public void NullTrack_DisplaysPlaceholderView()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorTracks.Add(null);

        using var state = new ActionSequenceEditorState();
        state.SetTarget(asset);

        var header = new ActionSequenceTrackHeaderView();
        header.Reconcile(state.DisplayTracks);

        Assert.AreEqual(1, header.RowCount);
        Assert.IsNotNull(header.GetRow(state.DisplayTracks[0].RenderKey));
    }

    private static ActionSequenceAsset CreateTwoTrackAsset()
    {
        ActionSequenceAsset asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        asset.EditorTracks.Clear();
        asset.EditorClips.Clear();

        var stateTrack = new ActionSequenceStateTrack();
        stateTrack.EditorClips.Add(new ActionSequenceTagClipDefinition { startFrame = 0, endFrame = 6 });
        asset.EditorTracks.Add(stateTrack);

        var motionTrack = new ActionSequenceMotionTrack();
        motionTrack.EditorClips.Add(new ActionSequenceImpulseClipDefinition { startFrame = 2, endFrame = 4 });
        asset.EditorTracks.Add(motionTrack);

        ActionSequenceEditorIdentity.UpgradeMissingIds(asset);
        return asset;
    }
}
#endif
