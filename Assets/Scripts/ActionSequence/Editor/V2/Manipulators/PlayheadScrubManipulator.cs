#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class PlayheadScrubManipulator : IDisposable
{
    private readonly VisualElement target;
    private readonly Func<ActionSequenceEditorState> stateProvider;
    private bool scrubbing;
    private int pointerId;

    public PlayheadScrubManipulator(VisualElement target, Func<ActionSequenceEditorState> stateProvider)
    {
        this.target = target;
        this.stateProvider = stateProvider;
        target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
    }

    public event Action ScrubStarted;
    public event Action<int> Scrubbed;
    public event Action ScrubEnded;

    public void Cancel()
    {
        if (!scrubbing)
            return;

        scrubbing = false;
        if (target.HasPointerCapture(pointerId))
            target.ReleasePointer(pointerId);
        ScrubEnded?.Invoke();
    }

    public void Dispose()
    {
        Cancel();
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || !TryReadFrame(evt.position, out int frame))
            return;

        scrubbing = true;
        pointerId = evt.pointerId;
        target.CapturePointer(pointerId);
        ScrubStarted?.Invoke();
        Scrubbed?.Invoke(frame);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!scrubbing || evt.pointerId != pointerId || !TryReadFrame(evt.position, out int frame))
            return;

        Scrubbed?.Invoke(frame);
        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!scrubbing || evt.pointerId != pointerId)
            return;

        Cancel();
        evt.StopPropagation();
    }

    private void OnPointerCancel(PointerCancelEvent evt)
    {
        if (scrubbing && evt.pointerId == pointerId)
            Cancel();
    }

    private bool TryReadFrame(Vector3 worldPosition, out int frame)
    {
        frame = 0;
        ActionSequenceEditorState state = stateProvider();
        if (state == null || !state.IsSupported)
            return false;

        Vector2 local = target.WorldToLocal(new Vector2(worldPosition.x, worldPosition.y));
        frame = Mathf.Clamp(Mathf.RoundToInt(state.Transform.ViewportXToFrame(local.x)), 0, state.ViewEndFrame);
        return true;
    }
}
#endif
