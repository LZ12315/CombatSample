#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class ActionSequenceTrackLaneView : VisualElement
{
    private readonly Dictionary<string, ActionSequenceTrackLaneRow> rowsByKey = new Dictionary<string, ActionSequenceTrackLaneRow>();
    private readonly List<ActionSequenceTrackLaneRow> orderedRows = new List<ActionSequenceTrackLaneRow>();
    private readonly HashSet<string> seenKeys = new HashSet<string>();

    public ActionSequenceTrackLaneView()
    {
        AddToClassList("asv2-track-lane-view");
    }

    public int RowCount => rowsByKey.Count;

    public event Action<ActionSequenceTrackSnapshot> TrackSelected;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot> ClipSelected;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, Vector2> ClipContextRequested;
    public event Action<ActionSequenceTrackSnapshot, int, Vector2> TrackContextRequested;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, ActionSequenceDisplayClip, ActionSequenceClipTimingEditMode> ClipTimingPreviewStarted;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, int, int> ClipTimingPreviewChanged;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, int, int> ClipTimingPreviewCommitted;
    public event Action ClipTimingPreviewCancelled;

    public ActionSequenceTrackLaneRow GetRow(string renderKey)
    {
        rowsByKey.TryGetValue(renderKey, out ActionSequenceTrackLaneRow row);
        return row;
    }

    public void CancelActiveClipGesture()
    {
        foreach (ActionSequenceTrackLaneRow row in rowsByKey.Values)
            row.CancelActiveClipGesture();
    }

    public void Reconcile(IReadOnlyList<ActionSequenceDisplayTrack> tracks, ActionSequenceTimelineTransform transform)
    {
        Reconcile(tracks, transform, null);
    }

    public void Reconcile(IReadOnlyList<ActionSequenceDisplayTrack> tracks, ActionSequenceTimelineTransform transform, ActionSequenceEditorState state)
    {
        seenKeys.Clear();
        orderedRows.Clear();

        for (int i = 0; i < tracks.Count; i++)
        {
            ActionSequenceDisplayTrack track = tracks[i];
            seenKeys.Add(track.RenderKey);
            if (!rowsByKey.TryGetValue(track.RenderKey, out ActionSequenceTrackLaneRow row))
            {
                row = new ActionSequenceTrackLaneRow();
                row.TrackSelected += snapshot => TrackSelected?.Invoke(snapshot);
                row.ClipSelected += (clip, owner) => ClipSelected?.Invoke(clip, owner);
                row.ClipContextRequested += (clip, owner, position) => ClipContextRequested?.Invoke(clip, owner, position);
                row.ContextRequested += (snapshot, frame, position) => TrackContextRequested?.Invoke(snapshot, frame, position);
                row.ClipTimingPreviewStarted += (clip, owner, displayClip, mode) => ClipTimingPreviewStarted?.Invoke(clip, owner, displayClip, mode);
                row.ClipTimingPreviewChanged += (clip, owner, start, end) => ClipTimingPreviewChanged?.Invoke(clip, owner, start, end);
                row.ClipTimingPreviewCommitted += (clip, owner, start, end) => ClipTimingPreviewCommitted?.Invoke(clip, owner, start, end);
                row.ClipTimingPreviewCancelled += () => ClipTimingPreviewCancelled?.Invoke();
                rowsByKey.Add(track.RenderKey, row);
            }

            row.Bind(track, transform, state);
            orderedRows.Add(row);
        }

        RemoveMissingRows();
        ReorderRows();
    }

    public void RefreshGeometry(IReadOnlyList<ActionSequenceDisplayTrack> tracks, ActionSequenceTimelineTransform transform)
    {
        RefreshGeometry(tracks, transform, null);
    }

    public void RefreshGeometry(IReadOnlyList<ActionSequenceDisplayTrack> tracks, ActionSequenceTimelineTransform transform, ActionSequenceEditorState state)
    {
        for (int i = 0; i < tracks.Count; i++)
        {
            ActionSequenceDisplayTrack track = tracks[i];
            if (rowsByKey.TryGetValue(track.RenderKey, out ActionSequenceTrackLaneRow row))
                row.RefreshGeometry(track, transform, state);
        }
    }

    private void RemoveMissingRows()
    {
        var stale = new List<string>();
        foreach (KeyValuePair<string, ActionSequenceTrackLaneRow> pair in rowsByKey)
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
            ActionSequenceTrackLaneRow row = orderedRows[i];
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
