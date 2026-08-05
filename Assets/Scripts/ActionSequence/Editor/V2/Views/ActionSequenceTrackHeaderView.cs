#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class ActionSequenceTrackHeaderView : VisualElement
{
    private readonly Dictionary<string, ActionSequenceTrackHeaderRow> rowsByKey = new Dictionary<string, ActionSequenceTrackHeaderRow>();
    private readonly List<ActionSequenceTrackHeaderRow> orderedRows = new List<ActionSequenceTrackHeaderRow>();
    private readonly HashSet<string> seenKeys = new HashSet<string>();

    public ActionSequenceTrackHeaderView()
    {
        AddToClassList("asv2-track-header-view");
    }

    public int RowCount => rowsByKey.Count;

    public event Action<ActionSequenceTrackSnapshot> TrackSelected;
    public event Action<ActionSequenceTrackSnapshot> AddClipRequested;
    public event Action<ActionSequenceTrackSnapshot, bool> MuteChanged;
    public event Action<ActionSequenceTrackSnapshot, bool> LockChanged;
    public event Action<ActionSequenceTrackSnapshot, bool> CollapseChanged;
    public event Action<ActionSequenceTrackSnapshot, Vector2> ContextRequested;

    public ActionSequenceTrackHeaderRow GetRow(string renderKey)
    {
        rowsByKey.TryGetValue(renderKey, out ActionSequenceTrackHeaderRow row);
        return row;
    }

    public void Reconcile(IReadOnlyList<ActionSequenceDisplayTrack> tracks)
    {
        Reconcile(tracks, null);
    }

    public void Reconcile(IReadOnlyList<ActionSequenceDisplayTrack> tracks, ActionSequenceEditorState state, ActionSequenceValidationPresentation validation = null)
    {
        seenKeys.Clear();
        orderedRows.Clear();

        for (int i = 0; i < tracks.Count; i++)
        {
            ActionSequenceDisplayTrack track = tracks[i];
            seenKeys.Add(track.RenderKey);
            if (!rowsByKey.TryGetValue(track.RenderKey, out ActionSequenceTrackHeaderRow row))
            {
                row = new ActionSequenceTrackHeaderRow();
                row.Selected += snapshot => TrackSelected?.Invoke(snapshot);
                row.AddClipRequested += snapshot => AddClipRequested?.Invoke(snapshot);
                row.MuteChanged += (snapshot, value) => MuteChanged?.Invoke(snapshot, value);
                row.LockChanged += (snapshot, value) => LockChanged?.Invoke(snapshot, value);
                row.CollapseChanged += (snapshot, value) => CollapseChanged?.Invoke(snapshot, value);
                row.ContextRequested += (snapshot, position) => ContextRequested?.Invoke(snapshot, position);
                rowsByKey.Add(track.RenderKey, row);
            }

            row.Bind(track, state != null && state.IsTrackSelected(track.Snapshot), validation);
            orderedRows.Add(row);
        }

        RemoveMissingRows();
        ReorderRows();
    }

    private void RemoveMissingRows()
    {
        var stale = new List<string>();
        foreach (KeyValuePair<string, ActionSequenceTrackHeaderRow> pair in rowsByKey)
        {
            if (!seenKeys.Contains(pair.Key))
            {
                pair.Value.RemoveFromHierarchy();
                stale.Add(pair.Key);
            }
        }

        for (int i = 0; i < stale.Count; i++)
            rowsByKey.Remove(stale[i]);
    }

    private void ReorderRows()
    {
        for (int i = 0; i < orderedRows.Count; i++)
        {
            ActionSequenceTrackHeaderRow row = orderedRows[i];
            if (row.parent != this)
            {
                Insert(i, row);
                continue;
            }

            int currentIndex = IndexOf(row);
            if (currentIndex != i)
            {
                row.RemoveFromHierarchy();
                Insert(i, row);
            }
        }
    }
}
#endif
