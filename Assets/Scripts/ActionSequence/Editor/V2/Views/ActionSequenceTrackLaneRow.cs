#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class ActionSequenceTrackLaneRow : VisualElement
{
    private readonly Dictionary<string, ActionSequenceClipView> clipsByKey = new Dictionary<string, ActionSequenceClipView>();
    private readonly List<ActionSequenceClipView> orderedClips = new List<ActionSequenceClipView>();
    private readonly HashSet<string> seenKeys = new HashSet<string>();
    private ActionSequenceTimelineTransform timelineTransform;

    public ActionSequenceTrackLaneRow()
    {
        AddToClassList("asv2-track-lane-row");
        style.position = Position.Relative;
        RegisterCallback<PointerDownEvent>(OnPointerDown);
    }

    public string RenderKey { get; private set; }
    public ActionSequenceTrackSnapshot Snapshot { get; private set; }
    public int ClipViewCount => clipsByKey.Count;

    public event Action<ActionSequenceTrackSnapshot> TrackSelected;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot> ClipSelected;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, Vector2> ClipContextRequested;
    public event Action<ActionSequenceTrackSnapshot, int, Vector2> ContextRequested;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, ActionSequenceDisplayClip, ActionSequenceClipTimingEditMode> ClipTimingPreviewStarted;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, int, int> ClipTimingPreviewChanged;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, int, int> ClipTimingPreviewCommitted;
    public event Action ClipTimingPreviewCancelled;

    public ActionSequenceClipView GetClipView(string renderKey)
    {
        clipsByKey.TryGetValue(renderKey, out ActionSequenceClipView view);
        return view;
    }

    public void CancelActiveClipGesture()
    {
        foreach (ActionSequenceClipView view in clipsByKey.Values)
            view.CancelGesture();
    }

    public void Bind(ActionSequenceDisplayTrack track, ActionSequenceTimelineTransform transform, ActionSequenceEditorState state)
    {
        RenderKey = track.RenderKey;
        ActionSequenceTrackSnapshot snapshot = track.Snapshot;
        Snapshot = snapshot;
        timelineTransform = transform;
        style.height = ActionSequenceViewUtility.GetTrackHeight(snapshot);
        SetClass("asv2-track-lane-muted", snapshot.Muted);
        SetClass("asv2-track-lane-locked", snapshot.Locked);
        SetClass("asv2-track-lane-collapsed", snapshot.Collapsed);

        ReconcileClips(track, transform, state);
    }

    public void RefreshGeometry(ActionSequenceDisplayTrack track, ActionSequenceTimelineTransform transform)
    {
        RefreshGeometry(track, transform, null);
    }

    public void RefreshGeometry(ActionSequenceDisplayTrack track, ActionSequenceTimelineTransform transform, ActionSequenceEditorState state)
    {
        style.height = ActionSequenceViewUtility.GetTrackHeight(track.Snapshot);
        IReadOnlyList<ActionSequenceDisplayClip> clips = track.Clips;
        for (int i = 0; i < clips.Count; i++)
        {
            if (clipsByKey.TryGetValue(clips[i].RenderKey, out ActionSequenceClipView view))
                view.RefreshGeometry(clips[i], transform, state);
        }
    }

    private void ReconcileClips(ActionSequenceDisplayTrack track, ActionSequenceTimelineTransform transform, ActionSequenceEditorState state)
    {
        seenKeys.Clear();
        orderedClips.Clear();

        IReadOnlyList<ActionSequenceDisplayClip> clips = track.Clips;
        for (int i = 0; i < clips.Count; i++)
        {
            ActionSequenceDisplayClip clip = clips[i];
            seenKeys.Add(clip.RenderKey);
            if (!clipsByKey.TryGetValue(clip.RenderKey, out ActionSequenceClipView view))
            {
                view = new ActionSequenceClipView();
                view.Selected += (snapshot, owner) => ClipSelected?.Invoke(snapshot, owner);
                view.ContextRequested += (snapshot, owner, position) => ClipContextRequested?.Invoke(snapshot, owner, position);
                view.TimingPreviewStarted += (snapshot, owner, displayClip, mode) => ClipTimingPreviewStarted?.Invoke(snapshot, owner, displayClip, mode);
                view.TimingPreviewChanged += (snapshot, owner, start, end) => ClipTimingPreviewChanged?.Invoke(snapshot, owner, start, end);
                view.TimingPreviewCommitted += (snapshot, owner, start, end) => ClipTimingPreviewCommitted?.Invoke(snapshot, owner, start, end);
                view.TimingPreviewCancelled += () => ClipTimingPreviewCancelled?.Invoke();
                clipsByKey.Add(clip.RenderKey, view);
            }

            view.Bind(clip, track.Snapshot, state != null && state.IsClipSelected(clip.Snapshot), state);
            view.RefreshGeometry(clip, transform, state);
            ActionSequenceViewUtility.SetDisplay(view, !track.Snapshot.Collapsed);
            orderedClips.Add(view);
        }

        RemoveMissingClipViews();
        ReorderClipViews();
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (Snapshot == null)
            return;

        int frame = 0;
        if (timelineTransform != null)
        {
            Vector2 local = this.WorldToLocal(evt.position);
            frame = Mathf.Max(0, Mathf.FloorToInt(timelineTransform.ViewportXToFrame(local.x)));
        }

        if (evt.button == 0)
        {
            TrackSelected?.Invoke(Snapshot);
            evt.StopPropagation();
        }
        else if (evt.button == 1)
        {
            TrackSelected?.Invoke(Snapshot);
            ContextRequested?.Invoke(Snapshot, frame, evt.position);
            evt.StopPropagation();
        }
    }

    private void RemoveMissingClipViews()
    {
        var stale = new List<string>();
        foreach (KeyValuePair<string, ActionSequenceClipView> pair in clipsByKey)
        {
            if (!seenKeys.Contains(pair.Key))
            {
                pair.Value.RemoveFromHierarchy();
                stale.Add(pair.Key);
            }
        }

        for (int i = 0; i < stale.Count; i++)
            clipsByKey.Remove(stale[i]);
    }

    private void ReorderClipViews()
    {
        for (int i = 0; i < orderedClips.Count; i++)
        {
            ActionSequenceClipView view = orderedClips[i];
            if (view.parent != this)
            {
                Insert(i, view);
                continue;
            }

            int currentIndex = IndexOf(view);
            if (currentIndex != i)
            {
                view.RemoveFromHierarchy();
                Insert(i, view);
            }
        }
    }

    private void SetClass(string className, bool enabled)
    {
        if (enabled)
            AddToClassList(className);
        else
            RemoveFromClassList(className);
    }
}
#endif
