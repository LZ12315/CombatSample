#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class ActionSequenceClipView : VisualElement
{
    private readonly Label label;
    private readonly ActionSequenceIssueBadge issueBadge;
    private readonly VisualElement leftHandle;
    private readonly VisualElement rightHandle;
    private string phaseClass;
    private ActionSequenceDisplayClip displayClip;
    private ActionSequenceTimelineTransform timelineTransform;
    private ActionSequenceSnapshot sequence;
    private bool pointerCaptured;
    private bool previewStarted;
    private int pointerId;
    private Vector2 pointerDownPosition;
    private ActionSequenceClipTimingEditMode editMode;

    public string RenderKey { get; private set; }
    public ActionSequenceClipSnapshot Snapshot { get; private set; }
    public ActionSequenceTrackSnapshot Track { get; private set; }

    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot> Selected;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, Vector2> ContextRequested;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, ActionSequenceDisplayClip, ActionSequenceClipTimingEditMode> TimingPreviewStarted;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, int, int> TimingPreviewChanged;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, int, int> TimingPreviewCommitted;
    public event Action TimingPreviewCancelled;

    public ActionSequenceClipView()
    {
        AddToClassList("asv2-clip");
        style.position = Position.Absolute;
        style.height = ActionSequenceViewUtility.ClipHeight;

        label = new Label();
        label.AddToClassList("asv2-clip-label");
        Add(label);

        issueBadge = new ActionSequenceIssueBadge();
        issueBadge.AddToClassList("asv2-clip-issue-badge");
        Add(issueBadge);

        leftHandle = new VisualElement();
        leftHandle.AddToClassList("asv2-clip-resize-handle");
        leftHandle.AddToClassList("asv2-clip-resize-left");
        leftHandle.pickingMode = PickingMode.Ignore;
        Add(leftHandle);

        rightHandle = new VisualElement();
        rightHandle.AddToClassList("asv2-clip-resize-handle");
        rightHandle.AddToClassList("asv2-clip-resize-right");
        rightHandle.pickingMode = PickingMode.Ignore;
        Add(rightHandle);

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    public void Bind(ActionSequenceDisplayClip clip, ActionSequenceTrackSnapshot track, bool selected, ActionSequenceEditorState state = null, ActionSequenceValidationPresentation validation = null)
    {
        displayClip = clip;
        Snapshot = clip.Snapshot;
        Track = track;
        RenderKey = clip.RenderKey;
        timelineTransform = state?.Transform;
        sequence = state != null && state.IsSupported ? state.Document.Sequence : default;
        string displayName = ActionSequenceViewUtility.GetClipDisplayName(clip.Snapshot);
        label.text = displayName;
        label.tooltip = displayName;
        issueBadge.Refresh(validation != null ? validation.GetClipIssueState(clip.Snapshot) : default);

        SetClass("asv2-clip-muted", track.Muted);
        SetClass("asv2-clip-locked", track.Locked);
        SetClass("asv2-clip-invalid", clip.Snapshot.IsNull || clip.Snapshot.MissingType || !clip.Snapshot.AllowedByTrack || !clip.Snapshot.PhaseMatchesTrack);
        SetClass("asv2-selected", selected);
        SetClass("asv2-clip-preview", state != null && state.InteractionPreview.IsActive && state.InteractionPreview.RenderKey == RenderKey);

        if (!string.IsNullOrEmpty(phaseClass))
            RemoveFromClassList(phaseClass);
        phaseClass = ActionSequenceViewUtility.GetPhaseClass(clip.Snapshot.Phase);
        AddToClassList(phaseClass);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (Snapshot == null || Track == null)
            return;

        if (evt.button == 0)
        {
            Selected?.Invoke(Snapshot, Track);
            TryBeginPointerGesture(evt);
            evt.StopPropagation();
        }
        else if (evt.button == 1)
        {
            Selected?.Invoke(Snapshot, Track);
            ContextRequested?.Invoke(Snapshot, Track, evt.position);
            evt.StopPropagation();
        }
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!pointerCaptured || evt.pointerId != pointerId || timelineTransform == null)
            return;

        Vector2 position = new Vector2(evt.position.x, evt.position.y);
        if (!previewStarted)
        {
            if ((position - pointerDownPosition).sqrMagnitude < 9f)
                return;

            previewStarted = true;
            TimingPreviewStarted?.Invoke(Snapshot, Track, displayClip, editMode);
        }

        CalculatePreviewTiming(position, out int startFrame, out int endFrame);
        TimingPreviewChanged?.Invoke(Snapshot, Track, startFrame, endFrame);
        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!pointerCaptured || evt.pointerId != pointerId)
            return;

        if (previewStarted)
        {
            Vector2 position = new Vector2(evt.position.x, evt.position.y);
            CalculatePreviewTiming(position, out int startFrame, out int endFrame);
            TimingPreviewCommitted?.Invoke(Snapshot, Track, startFrame, endFrame);
        }
        else
        {
            TimingPreviewCancelled?.Invoke();
        }

        ReleaseGesturePointer();
        evt.StopPropagation();
    }

    private void OnPointerCancel(PointerCancelEvent evt)
    {
        if (pointerCaptured && evt.pointerId == pointerId)
            CancelGesture();
    }

    private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        if (pointerCaptured)
            CancelGesture();
    }

    public void RefreshGeometry(ActionSequenceDisplayClip clip, ActionSequenceTimelineTransform transform)
    {
        RefreshGeometry(clip, transform, null);
    }

    public void RefreshGeometry(ActionSequenceDisplayClip clip, ActionSequenceTimelineTransform transform, ActionSequenceEditorState state)
    {
        int startFrame = clip.SafeStartFrame;
        int endFrame = clip.SafeEndFrame;
        if (state != null && state.InteractionPreview.IsActive && state.InteractionPreview.RenderKey == clip.RenderKey)
        {
            startFrame = state.InteractionPreview.StartFrame;
            endFrame = state.InteractionPreview.EndFrame;
        }

        float left = transform.FrameToViewportX(startFrame);
        float right = transform.FrameToViewportX(endFrame);
        style.left = left;
        style.top = ActionSequenceViewUtility.ClipTop;
        style.width = Mathf.Max(ActionSequenceViewUtility.MinimumClipWidth, right - left);
        SetClass("asv2-clip-preview", state != null && state.InteractionPreview.IsActive && state.InteractionPreview.RenderKey == clip.RenderKey);
    }

    public void CancelGesture()
    {
        if (!pointerCaptured)
            return;

        TimingPreviewCancelled?.Invoke();
        ReleaseGesturePointer();
    }

    private void TryBeginPointerGesture(PointerDownEvent evt)
    {
        if (timelineTransform == null || Track.Locked || Snapshot.IsNull || Snapshot.MissingType || string.IsNullOrEmpty(Snapshot.EditorId))
            return;

        Vector2 localPosition = this.WorldToLocal(new Vector2(evt.position.x, evt.position.y));
        editMode = ResolveEditMode(localPosition.x);
        pointerCaptured = true;
        previewStarted = false;
        pointerId = evt.pointerId;
        pointerDownPosition = new Vector2(evt.position.x, evt.position.y);
        this.CapturePointer(pointerId);
    }

    private ActionSequenceClipTimingEditMode ResolveEditMode(float localX)
    {
        float width = Mathf.Max(1f, resolvedStyle.width);
        float handleWidth = Mathf.Min(6f, width / 3f);
        if (localX <= handleWidth)
            return ActionSequenceClipTimingEditMode.ResizeLeft;
        if (localX >= width - handleWidth)
            return ActionSequenceClipTimingEditMode.ResizeRight;

        return ActionSequenceClipTimingEditMode.Move;
    }

    private void CalculatePreviewTiming(Vector2 pointerPosition, out int startFrame, out int endFrame)
    {
        int deltaFrames = Mathf.RoundToInt((pointerPosition.x - pointerDownPosition.x) / Mathf.Max(0.001f, timelineTransform.PixelsPerFrame));
        switch (editMode)
        {
            case ActionSequenceClipTimingEditMode.ResizeLeft:
                ClipResizeManipulator.CalculateLeftTiming(sequence, displayClip.SafeStartFrame, displayClip.SafeEndFrame, deltaFrames, out startFrame, out endFrame);
                break;
            case ActionSequenceClipTimingEditMode.ResizeRight:
                ClipResizeManipulator.CalculateRightTiming(sequence, displayClip.SafeStartFrame, displayClip.SafeEndFrame, deltaFrames, out startFrame, out endFrame);
                break;
            default:
                ClipMoveManipulator.CalculateTiming(sequence, displayClip.SafeStartFrame, displayClip.SafeEndFrame, deltaFrames, out startFrame, out endFrame);
                break;
        }
    }

    private void ReleaseGesturePointer()
    {
        bool hadCapture = pointerCaptured;
        pointerCaptured = false;
        previewStarted = false;
        if (hadCapture && this.HasPointerCapture(pointerId))
            this.ReleasePointer(pointerId);
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
