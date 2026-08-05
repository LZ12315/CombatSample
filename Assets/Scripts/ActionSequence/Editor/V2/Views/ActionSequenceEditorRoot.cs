#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

internal sealed class ActionSequenceEditorRoot
{
    private readonly VisualElement root;
    private readonly VisualElement toolbarHost;
    private readonly VisualElement timelineHost;
    private readonly VisualElement timelineBody;
    private readonly VisualElement headerColumn;
    private readonly VisualElement rulerHost;
    private readonly ScrollView headerScroll;
    private readonly ScrollView laneScroll;
    private readonly Scroller horizontalScroller;
    private readonly ActionSequenceTrackHeaderView headerView;
    private readonly ActionSequenceTrackLaneView laneView;
    private readonly ActionSequenceRulerView rulerView;
    private readonly ActionSequenceGridView gridView;
    private readonly VisualElement playheadLayer;
    private readonly ActionSequencePlayheadView playheadView;
    private readonly VisualElement startGuide;
    private readonly VisualElement endGuide;
    private readonly Label previewLabel;
    private readonly ActionSequenceToolbar toolbar;
    private readonly ActionSequenceStatusBar statusBar;
    private readonly ActionSequenceValidationPanel validationPanel;
    private readonly PlayheadScrubManipulator scrubManipulator;

    private ActionSequenceEditorState state;
    private bool suppressScrollCallbacks;
    private bool panning;
    private Vector2 lastPointerPosition;

    public ActionSequenceEditorRoot(VisualElement rootElement)
    {
        root = rootElement;
        toolbarHost = root.Q<VisualElement>("toolbar-host") ?? CreateChild(root, "toolbar-host", "asv2-toolbar-host");
        timelineHost = root.Q<VisualElement>("timeline-host") ?? CreateChild(root, "timeline-host", "asv2-timeline-host");
        timelineBody = root.Q<VisualElement>("timeline-body") ?? CreateChild(timelineHost, "timeline-body", "asv2-timeline-body");
        headerColumn = root.Q<VisualElement>("track-header-column") ?? CreateChild(timelineBody, "track-header-column", "asv2-track-header-column");
        rulerHost = root.Q<VisualElement>("ruler-host") ?? CreateChild(timelineBody, "ruler-host", "asv2-ruler-host");

        headerScroll = root.Q<ScrollView>("header-scroll") ?? CreateScrollView("header-scroll");
        laneScroll = root.Q<ScrollView>("lane-scroll") ?? CreateScrollView("lane-scroll");
        horizontalScroller = root.Q<Scroller>("horizontal-scroller") ?? new Scroller();
        horizontalScroller.name = "horizontal-scroller";
        horizontalScroller.AddToClassList("asv2-horizontal-scroller");
        horizontalScroller.direction = SliderDirection.Horizontal;
        horizontalScroller.valueChanged += OnHorizontalScrollerValueChanged;

        headerView = new ActionSequenceTrackHeaderView();
        laneView = new ActionSequenceTrackLaneView();
        rulerView = new ActionSequenceRulerView();
        gridView = new ActionSequenceGridView();
        playheadLayer = new VisualElement();
        playheadLayer.name = "playhead-layer";
        playheadLayer.AddToClassList("asv2-playhead-layer");
        playheadLayer.pickingMode = PickingMode.Ignore;
        playheadLayer.style.position = Position.Absolute;
        playheadLayer.style.top = 0f;
        playheadLayer.style.right = 0f;
        playheadLayer.style.bottom = 16f;
        playheadLayer.style.overflow = Overflow.Hidden;
        playheadView = new ActionSequencePlayheadView();
        startGuide = new VisualElement();
        startGuide.AddToClassList("asv2-frame-guide");
        startGuide.pickingMode = PickingMode.Ignore;
        endGuide = new VisualElement();
        endGuide.AddToClassList("asv2-frame-guide");
        endGuide.pickingMode = PickingMode.Ignore;
        previewLabel = new Label();
        previewLabel.AddToClassList("asv2-frame-guide-label");
        previewLabel.pickingMode = PickingMode.Ignore;

        EnsureLayout();

        toolbar = new ActionSequenceToolbar(toolbarHost);
        statusBar = new ActionSequenceStatusBar(root);
        validationPanel = new ActionSequenceValidationPanel(root);
        toolbar.TargetChanged += target => TargetChanged?.Invoke(target);
        toolbar.AddTrackRequested += anchor => AddTrackRequested?.Invoke(anchor);
        toolbar.PlayPauseRequested += () => PlayPauseRequested?.Invoke();
        toolbar.StopRequested += () => StopRequested?.Invoke();
        toolbar.CurrentFrameChanged += frame => CurrentFrameChanged?.Invoke(frame);
        toolbar.ZoomChanged += value => ZoomChanged?.Invoke(GetViewportAnchorX(), value);
        toolbar.FitRequested += () => FitRequested?.Invoke();
        statusBar.IssuesRequested += () => validationPanel.Toggle();
        statusBar.RepairInvalidIdsRequested += () => RepairInvalidIdsRequested?.Invoke();
        validationPanel.LocateRequested += issue => ValidationIssueLocateRequested?.Invoke(issue);
        validationPanel.RepairRequested += commandId => RepairCommandRequested?.Invoke(commandId);
        headerView.TrackSelected += track => TrackSelected?.Invoke(track);
        headerView.AddClipRequested += track => AddClipRequested?.Invoke(track, 0, Vector2.zero);
        headerView.MuteChanged += (track, value) => TrackMuteChanged?.Invoke(track, value);
        headerView.LockChanged += (track, value) => TrackLockChanged?.Invoke(track, value);
        headerView.CollapseChanged += (track, value) => TrackCollapseChanged?.Invoke(track, value);
        headerView.ContextRequested += (track, position) => TrackContextRequested?.Invoke(track, 0, position);
        laneView.TrackSelected += track => TrackSelected?.Invoke(track);
        laneView.ClipSelected += (clip, track) => ClipSelected?.Invoke(clip, track);
        laneView.ClipContextRequested += (clip, track, position) => ClipContextRequested?.Invoke(clip, track, position);
        laneView.TrackContextRequested += (track, frame, position) => TrackContextRequested?.Invoke(track, frame, position);
        laneView.ClipTimingPreviewStarted += (clip, track, displayClip, mode) => ClipTimingPreviewStarted?.Invoke(clip, track, displayClip, mode);
        laneView.ClipTimingPreviewChanged += (clip, track, start, end) => ClipTimingPreviewChanged?.Invoke(clip, track, start, end);
        laneView.ClipTimingPreviewCommitted += (clip, track, start, end) => ClipTimingPreviewCommitted?.Invoke(clip, track, start, end);
        laneView.ClipTimingPreviewCancelled += () => ClipTimingPreviewCancelled?.Invoke();

        scrubManipulator = new PlayheadScrubManipulator(rulerHost, () => state);
        scrubManipulator.ScrubStarted += () => PlayheadScrubStarted?.Invoke();
        scrubManipulator.Scrubbed += frame => CurrentFrameChanged?.Invoke(frame);

        RegisterCallbacks();
    }

    public event Action<Object> TargetChanged;
    public event Action<VisualElement> AddTrackRequested;
    public event Action PlayPauseRequested;
    public event Action StopRequested;
    public event Action PlayheadScrubStarted;
    public event Action<int> CurrentFrameChanged;
    public event Action SequenceSelected;
    public event Action<ActionSequenceTrackSnapshot> TrackSelected;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot> ClipSelected;
    public event Action<ActionSequenceTrackSnapshot, int, Vector2> AddClipRequested;
    public event Action<ActionSequenceTrackSnapshot, bool> TrackMuteChanged;
    public event Action<ActionSequenceTrackSnapshot, bool> TrackLockChanged;
    public event Action<ActionSequenceTrackSnapshot, bool> TrackCollapseChanged;
    public event Action<ActionSequenceTrackSnapshot, int, Vector2> TrackContextRequested;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, Vector2> ClipContextRequested;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, ActionSequenceDisplayClip, ActionSequenceClipTimingEditMode> ClipTimingPreviewStarted;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, int, int> ClipTimingPreviewChanged;
    public event Action<ActionSequenceClipSnapshot, ActionSequenceTrackSnapshot, int, int> ClipTimingPreviewCommitted;
    public event Action ClipTimingPreviewCancelled;
    public event Action<float, float> ZoomChanged;
    public event Action<float> HorizontalScrollChanged;
    public event Action<float> VerticalScrollChanged;
    public event Action<float, float> ViewportChanged;
    public event Action FitRequested;
    public event Action RepairInvalidIdsRequested;
    public event Action<string> RepairCommandRequested;
    public event Action<ActionSequenceEditorValidationIssue> ValidationIssueLocateRequested;
    public event Action<ActionSequenceEditorShortcut> ShortcutRequested;

    public ActionSequenceTrackHeaderView HeaderView => headerView;
    public ActionSequenceTrackLaneView LaneView => laneView;

    public void SetTarget(Object target)
    {
        toolbar.SetTarget(target);
    }

    public void Refresh(ActionSequenceEditorState newState, ActionSequenceValidationPresentation validation)
    {
        state = newState;
        toolbar.Refresh(state);
        statusBar.Refresh(state, validation);
        validationPanel.Refresh(validation);

        bool supported = state != null && state.IsSupported;
        ActionSequenceViewUtility.SetDisplay(timelineHost, supported);
        if (!supported)
            return;

        ApplyHeaderWidth();
        headerView.Reconcile(state.DisplayTracks, state, validation);
        laneView.Reconcile(state.DisplayTracks, state.Transform, state, validation);
        rulerView.Refresh(state.Transform);
        gridView.Refresh(state);
        playheadView.Refresh(state);
        RefreshPreviewGuides();
        UpdateScrollersFromState();
    }

    public void RefreshViewportOnly(ActionSequenceEditorState newState)
    {
        state = newState;
        if (state == null || !state.IsSupported)
            return;

        toolbar.Refresh(state);
        rulerView.Refresh(state.Transform);
        gridView.Refresh(state);
        laneView.RefreshGeometry(state.DisplayTracks, state.Transform, state);
        playheadView.Refresh(state);
        RefreshPreviewGuides();
        UpdateScrollersFromState();
    }

    public void RefreshInteractionPreviewOnly(ActionSequenceEditorState newState)
    {
        state = newState;
        if (state == null || !state.IsSupported)
            return;

        toolbar.Refresh(state);
        rulerView.Refresh(state.Transform);
        gridView.Refresh(state);
        laneView.RefreshGeometry(state.DisplayTracks, state.Transform, state);
        RefreshPreviewGuides();
        UpdateScrollersFromState();
    }

    public void RefreshPlaybackOnly(ActionSequenceEditorState newState)
    {
        state = newState;
        toolbar.Refresh(state);
        playheadView.Refresh(state);
    }

    public void CancelActiveInteractionGesture()
    {
        laneView.CancelActiveClipGesture();
    }

    public void SetStatusMessage(string message)
    {
        statusBar.SetTransientMessage(message);
    }

    private void EnsureLayout()
    {
        root.AddToClassList("asv2-root");
        root.focusable = true;

        if (toolbarHost.parent == null)
            root.Add(toolbarHost);
        if (timelineHost.parent == null)
            root.Add(timelineHost);

        if (headerColumn.parent == null)
            timelineBody.Add(headerColumn);
        if (rulerHost.parent == null)
            timelineBody.Add(rulerHost);

        if (headerScroll.parent == null)
            headerColumn.Add(headerScroll);
        if (rulerView.parent == null)
            rulerHost.Add(rulerView);
        if (laneScroll.parent == null)
            timelineBody.Add(laneScroll);
        if (playheadLayer.parent == null)
            timelineBody.Add(playheadLayer);
        if (playheadView.parent == null)
            playheadLayer.Add(playheadView);
        if (horizontalScroller.parent == null)
            timelineHost.Add(horizontalScroller);

        headerScroll.contentContainer.Add(headerView);
        laneScroll.contentContainer.Add(gridView);
        laneScroll.contentContainer.Add(laneView);
        laneScroll.contentContainer.Add(startGuide);
        laneScroll.contentContainer.Add(endGuide);
        laneScroll.contentContainer.Add(previewLabel);

        headerScroll.mode = ScrollViewMode.Vertical;
        laneScroll.mode = ScrollViewMode.Vertical;
        headerScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        laneScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        headerScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        laneScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
    }

    private void RegisterCallbacks()
    {
        laneScroll.verticalScroller.valueChanged += OnLaneVerticalScrollChanged;
        headerScroll.verticalScroller.valueChanged += OnHeaderVerticalScrollChanged;
        laneScroll.RegisterCallback<GeometryChangedEvent>(OnLaneGeometryChanged);
        rulerHost.RegisterCallback<GeometryChangedEvent>(OnLaneGeometryChanged);
        laneScroll.RegisterCallback<WheelEvent>(OnWheel);
        rulerHost.RegisterCallback<WheelEvent>(OnWheel);
        laneScroll.RegisterCallback<PointerDownEvent>(OnPointerDown);
        laneScroll.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        laneScroll.RegisterCallback<PointerUpEvent>(OnPointerUp);
        laneScroll.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        root.RegisterCallback<PointerDownEvent>(_ => root.Focus(), TrickleDown.TrickleDown);
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);
    }

    private void OnLaneGeometryChanged(GeometryChangedEvent evt)
    {
        if (state == null)
            return;

        ViewportChanged?.Invoke(Mathf.Max(1f, laneScroll.resolvedStyle.width), Mathf.Max(1f, laneScroll.resolvedStyle.height));
    }

    private void OnHorizontalScrollerValueChanged(float value)
    {
        if (!suppressScrollCallbacks)
            HorizontalScrollChanged?.Invoke(value);
    }

    private void OnLaneVerticalScrollChanged(float value)
    {
        if (suppressScrollCallbacks)
            return;

        suppressScrollCallbacks = true;
        headerScroll.verticalScroller.value = value;
        suppressScrollCallbacks = false;
        VerticalScrollChanged?.Invoke(value);
    }

    private void OnHeaderVerticalScrollChanged(float value)
    {
        if (suppressScrollCallbacks)
            return;

        suppressScrollCallbacks = true;
        laneScroll.verticalScroller.value = value;
        suppressScrollCallbacks = false;
        VerticalScrollChanged?.Invoke(value);
    }

    private void OnWheel(WheelEvent evt)
    {
        if (state == null || !state.IsSupported)
            return;

        if (evt.ctrlKey || evt.commandKey)
        {
            float factor = Mathf.Pow(1.1f, -evt.delta.y);
            Vector2 local = rulerHost.WorldToLocal(evt.mousePosition);
            ZoomChanged?.Invoke(Mathf.Clamp(local.x, 0f, state.ViewportWidth), state.PixelsPerFrame * factor);
            evt.StopPropagation();
            return;
        }

        if (evt.shiftKey)
        {
            HorizontalScrollChanged?.Invoke(state.HorizontalScroll + evt.delta.y * 12f);
            evt.StopPropagation();
        }
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 2)
        {
            if (evt.button == 0)
            {
                SequenceSelected?.Invoke();
                evt.StopPropagation();
            }

            return;
        }

        panning = true;
        lastPointerPosition = new Vector2(evt.position.x, evt.position.y);
        laneScroll.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!panning || state == null)
            return;

        Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
        Vector2 delta = pointerPosition - lastPointerPosition;
        lastPointerPosition = pointerPosition;
        HorizontalScrollChanged?.Invoke(state.HorizontalScroll - delta.x);
        float vertical = Mathf.Max(0f, state.VerticalScroll - delta.y);
        SetVerticalScrollWithoutNotify(vertical);
        VerticalScrollChanged?.Invoke(vertical);
        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!panning)
            return;

        panning = false;
        laneScroll.ReleasePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerCancel(PointerCancelEvent evt)
    {
        panning = false;
        laneScroll.ReleasePointer(evt.pointerId);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (state == null || !state.IsSupported || IsTextEditing())
            return;

        ActionSequenceEditorShortcut shortcut = ActionSequenceEditorShortcut.None;
        switch (evt.keyCode)
        {
            case KeyCode.Space:
                shortcut = ActionSequenceEditorShortcut.PlayPause;
                break;
            case KeyCode.S:
                shortcut = ActionSequenceEditorShortcut.Stop;
                break;
            case KeyCode.LeftArrow:
                shortcut = evt.shiftKey ? ActionSequenceEditorShortcut.StepMajorLeft : ActionSequenceEditorShortcut.StepFrameLeft;
                break;
            case KeyCode.RightArrow:
                shortcut = evt.shiftKey ? ActionSequenceEditorShortcut.StepMajorRight : ActionSequenceEditorShortcut.StepFrameRight;
                break;
            case KeyCode.F:
                shortcut = ActionSequenceEditorShortcut.FrameSelection;
                break;
            case KeyCode.Delete:
            case KeyCode.Backspace:
                shortcut = ActionSequenceEditorShortcut.DeleteSelection;
                break;
            case KeyCode.L:
                shortcut = ActionSequenceEditorShortcut.ToggleLock;
                break;
            case KeyCode.M:
                shortcut = ActionSequenceEditorShortcut.ToggleMute;
                break;
            case KeyCode.Escape:
                shortcut = ActionSequenceEditorShortcut.CancelInteraction;
                break;
        }

        if (shortcut == ActionSequenceEditorShortcut.None)
            return;

        ShortcutRequested?.Invoke(shortcut);
        evt.StopPropagation();
        evt.PreventDefault();
    }

    private bool IsTextEditing()
    {
        Focusable focused = root.panel?.focusController?.focusedElement;
        for (var element = focused as VisualElement; element != null; element = element.parent)
        {
            string typeName = element.GetType().Name;
            if (typeName.Contains("TextField") || typeName.Contains("IntegerField") || typeName.Contains("FloatField")
                || typeName.Contains("DoubleField") || typeName.Contains("LongField"))
                return true;
        }

        return false;
    }

    private void UpdateScrollersFromState()
    {
        if (state == null)
            return;

        suppressScrollCallbacks = true;
        horizontalScroller.lowValue = 0f;
        horizontalScroller.highValue = Mathf.Max(0f, state.Transform.ContentWidth - state.Transform.ViewportWidth);
        horizontalScroller.Adjust(state.Transform.ViewportWidth / Mathf.Max(1f, state.Transform.ContentWidth));
        horizontalScroller.value = state.HorizontalScroll;
        SetVerticalScrollWithoutNotify(state.VerticalScroll);
        suppressScrollCallbacks = false;
    }

    private void SetVerticalScrollWithoutNotify(float value)
    {
        suppressScrollCallbacks = true;
        laneScroll.verticalScroller.value = value;
        headerScroll.verticalScroller.value = value;
        suppressScrollCallbacks = false;
    }

    private void ApplyHeaderWidth()
    {
        headerColumn.style.width = state.HeaderWidth;
        rulerHost.style.left = state.HeaderWidth;
        laneScroll.style.left = state.HeaderWidth;
        playheadLayer.style.left = state.HeaderWidth;
    }

    private void RefreshPreviewGuides()
    {
        bool visible = state != null && state.IsSupported && state.InteractionPreview.IsActive;
        ActionSequenceViewUtility.SetDisplay(startGuide, visible);
        ActionSequenceViewUtility.SetDisplay(endGuide, visible);
        ActionSequenceViewUtility.SetDisplay(previewLabel, visible);
        if (!visible)
            return;

        ActionSequenceClipTimingPreview preview = state.InteractionPreview;
        float startX = state.Transform.FrameToViewportX(preview.StartFrame);
        float endX = state.Transform.FrameToViewportX(preview.EndFrame);
        startGuide.style.left = startX;
        endGuide.style.left = endX;
        previewLabel.style.left = startX + 4f;
        previewLabel.text = $"{preview.StartFrame}-{preview.EndFrame}";
    }

    private float GetViewportAnchorX()
    {
        return state != null ? state.ViewportWidth * 0.5f : 0f;
    }

    private static VisualElement CreateChild(VisualElement parent, string name, string className)
    {
        var child = new VisualElement { name = name };
        child.AddToClassList(className);
        parent.Add(child);
        return child;
    }

    private static ScrollView CreateScrollView(string name)
    {
        var scrollView = new ScrollView { name = name };
        scrollView.AddToClassList("asv2-scroll-view");
        return scrollView;
    }
}

internal enum ActionSequenceEditorShortcut
{
    None,
    PlayPause,
    Stop,
    StepFrameLeft,
    StepFrameRight,
    StepMajorLeft,
    StepMajorRight,
    FrameSelection,
    DeleteSelection,
    ToggleLock,
    ToggleMute,
    CancelInteraction,
}
#endif
