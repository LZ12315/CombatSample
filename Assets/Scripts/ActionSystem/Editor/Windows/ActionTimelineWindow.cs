#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UIElements;

/// <summary>
/// Fixed-row Action Timeline. Presentation, pointer gestures, and authoring commands
/// are intentionally separated so Ghost feedback and the committed operation share the same rules.
/// </summary>
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp-Editor", sourceClassName: "ActionV1TimelineWindow")]
public sealed class ActionTimelineWindow : EditorWindow
{
    private enum InteractionState
    {
        Idle,
        PendingManipulation,
        Scrub,
        Pan,
        Marquee,
        Move,
        ResizeLeft,
        ResizeRight,
        TrimLeft,
        TrimRight,
        TimeNavigatorPan,
        TimeNavigatorResizeLeft,
        TimeNavigatorResizeRight,
        HeaderResize,
        InlineRename,
    }

    private sealed class ManipulationEntry
    {
        internal ActionDocumentEntry Entry;
        internal int StartFrame;
        internal int DurationFrames;
        internal int LaneIndex;
        internal VisualElement Ghost;
    }

    private sealed class PointDensityCluster
    {
        internal int LaneIndex;
        internal readonly List<ActionDocumentEntry> Entries = new List<ActionDocumentEntry>();
        internal string Key;
        internal bool MarkerHiddenByPrimary;
    }

    private sealed class EntryPresentation
    {
        internal Rect VisualRect;
        internal Rect HitRect;
        internal Vector2 PointCenter;
        internal bool IsPoint;
        internal bool Displayed = true;
    }

    private readonly Dictionary<string, VisualElement> _entryViews = new Dictionary<string, VisualElement>();
    private readonly Dictionary<string, VisualElement> _laneHeaderViews = new Dictionary<string, VisualElement>();
    private readonly Dictionary<string, ActionDocumentEntry> _entriesByDisplayKey = new Dictionary<string, ActionDocumentEntry>();
    private readonly Dictionary<string, EntryPresentation> _entryPresentation = new Dictionary<string, EntryPresentation>();
    private readonly Dictionary<string, Button> _pointDensityMarkers = new Dictionary<string, Button>();
    private readonly Dictionary<int, List<PointDensityCluster>> _densityClustersByLane = new Dictionary<int, List<PointDensityCluster>>();
    private readonly Dictionary<int, List<PointDensityCluster>> _hiddenDensityClustersByLane = new Dictionary<int, List<PointDensityCluster>>();
    private readonly Dictionary<int, List<Rect>> _reservedMarkerRectsByLane = new Dictionary<int, List<Rect>>();
    private ActionEditorDocument _document;
    private ActionTimelineGeometry _geometry;
    private VisualElement _identityBanner;
    private VisualElement _timelineHost;
    private VisualElement _corner;
    private Button _addLaneButton;
    private VisualElement _headerResizeHandle;
    private VisualElement _rulerViewport;
    private IMGUIContainer _rulerCanvas;
    private VisualElement _headerViewport;
    private VisualElement _headerContent;
    private ScrollView _contentScroll;
    private VisualElement _navigatorRow;
    private VisualElement _navigatorCorner;
    private VisualElement _timeNavigator;
    private VisualElement _timeNavigatorThumb;
    private VisualElement _timeNavigatorLeftHandle;
    private VisualElement _timeNavigatorRightHandle;
    private VisualElement _contentCanvas;
    private IMGUIContainer _gridCanvas;
    private VisualElement _entryLayer;
    private VisualElement _pointDensityLayer;
    private VisualElement _guideLayer;
    private VisualElement _ghostLayer;
    private VisualElement _emptyOverlay;
    private Label _emptyTitle;
    private Label _emptyBody;
    private VisualElement _postDurationShade;
    private VisualElement _durationLine;
    private VisualElement _horizonLine;
    private Label _geometryOverflowSentinel;
    private VisualElement _playhead;
    private VisualElement _rulerPlayhead;
    private VisualElement _marquee;
    private VisualElement _snapGuide;
    private Label _operationLabel;
    private Label _frameLabel;
    private Label _statusLabel;
    private Button _playButton;
    private Image _playButtonIcon;
    [SerializeField] private float _headerWidth = ActionEditorTheme.DefaultHeaderWidth;
    [SerializeField] private double _visibleStartFrame;
    [SerializeField] private double _visibleSpanFrames = 60d;
    [SerializeField] private double _workspaceEndFrame = 120d;
    [SerializeField] private double _navigationEndFrame = 60d;
    [SerializeField] private string _viewActionGuid;
    [SerializeField] private float _storedVerticalScroll;
    [SerializeField] private bool _hasStoredScroll;
    [SerializeField] private bool _hasTimeViewportState;
    [SerializeField] private bool _snapEnabled = true;
    private readonly ActionTimeViewport _timeViewport = new ActionTimeViewport();
    private int _capturedPointer = -1;
    private Vector2 _gestureStart;
    private Vector2 _gestureCurrent;
    private Vector2 _panStartScroll;
    private double _panStartVisibleFrame;
    private double _navigatorStartFrame;
    private double _navigatorStartSpan;
    private double _navigatorStartExtent;
    private float _navigatorStartTrackWidth;
    private Vector2 _headerResizeRootSize;
    private double _manipulationPixelsPerFrame;
    private double _wheelAnchorFrame;
    private float _wheelAnchorX = float.NaN;
    private double _lastWheelTime = double.NegativeInfinity;
    private bool _marqueeAdditive;
    private InteractionState _interaction;
    private VisualElement _captureTarget;
    private string _pendingEditorId;
    private ActionAsset _pendingAction;
    private InteractionState _pendingManipulationState;
    private bool _pendingCollapseSelection;
    private readonly List<ManipulationEntry> _manipulationEntries = new List<ManipulationEntry>();
    private ActionDocumentEntry _manipulationPrimary;
    private ActionTimelineOperationSnapshot _operationSnapshot;
    private ActionTimelineOperationInput _operationInput;
    private ActionTimelineOperationResult _operationResult;
    private int _previewFrameDelta;
    private int _previewLaneDelta;
    private bool _previewValid;
    private string _previewMessage;
    private TextField _renameField;
    private ActionDocumentEntry _renamingLane;
    private int _animationPickerControlId;
    private int _pendingAnimationFrame;
    private string _hoverDisplayKey;
    private string _transientDisplayKey;
    private string _statusNotice;
    private int? _activeSnapFrame;
    private Vector2 _lastPointerWorld;
    private bool _autoPanRegistered;
    private double _lastAutoPanTime;
    private bool _lastSnapInverted;
    private bool _syncingScroll;
    private bool _releasingPointerCapture;
    private bool _rebuildRestorePending;
    private bool _uiReady;
    private bool _refreshingViews;
    private ActionEditorChangeFlags _pendingContextChanges;
    private int _restoreGeneration;
    private int _viewportGeometryGeneration;
    private Vector2 _lastViewportSize = new Vector2(float.NaN, float.NaN);

    [MenuItem("Tools/CombatSample/Action Timeline")]
    public static void OpenFromMenu()
    {
        ActionTimelineWindow window = GetWindow<ActionTimelineWindow>("Action Timeline");
        if (ActionEditorContext.Shared.CurrentAction == null && Selection.activeObject is ActionAsset action)
            ActionEditorContext.Shared.SetAction(action);
        window.Show();
    }

    internal static void Open(ActionAsset action)
    {
        ActionTimelineWindow window = GetWindow<ActionTimelineWindow>("Action Timeline");
        ActionEditorContext.Shared.SetAction(action);
        window.Show();
    }

    internal static void OpenShared() => GetWindow<ActionTimelineWindow>("Action Timeline").Show();

    private void OnEnable()
    {
        _uiReady = false;
        _pendingContextChanges = ActionEditorChangeFlags.None;
        ActionEditorContext.Changed += OnContextChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        _uiReady = false;
        _pendingContextChanges = ActionEditorChangeFlags.None;
        CaptureWindowState();
        CancelInteraction();
        ActionEditorInteractionGate.Release(this);
        CancelAnimationPicker();
        SetHoveredEntry(null);
        ActionEditorPlayback.Stop();
        ActionEditorContext.Changed -= OnContextChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    public void CreateGUI()
    {
        _uiReady = false;
        _pendingContextChanges = ActionEditorChangeFlags.None;
        _headerWidth = Mathf.Clamp(_headerWidth, ActionEditorTheme.MinHeaderWidth, ActionEditorTheme.MaxHeaderWidth);
        rootVisualElement.Clear();
        ActionEditorTheme.Apply(rootVisualElement, "action-editor-timeline-window");
        rootVisualElement.focusable = true;
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);
        rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        rootVisualElement.RegisterCallback<DetachFromPanelEvent>(_ => CancelInteraction());
        rootVisualElement.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (!_releasingPointerCapture)
                CancelInteraction();
        });

        BuildChrome();
        BuildTimelineShell();
        BuildStatusBar();
        _uiReady = true;
        RefreshDocumentAndViews();
    }

    private void BuildChrome()
    {
        var transport = new Toolbar();
        transport.AddToClassList("action-editor-transport-bar");
        var playback = new VisualElement();
        playback.AddToClassList("action-editor-playback-group");
        playback.Add(TransportButton("Animation.FirstKey", "First frame", () => RunTimelineNavigation(() => ActionEditorContext.Shared.SetFrame(0))));
        playback.Add(TransportButton("Animation.PrevKey", "Previous frame", () => RunTimelineNavigation(() => ActionEditorContext.Shared.SetFrame(ActionEditorContext.Shared.CurrentFrame - 1))));
        _playButton = TransportButton(ActionEditorPlayback.IsPlaying ? "PauseButton" : "Animation.Play",
            "Play / Pause", TogglePlayback, out _playButtonIcon);
        playback.Add(_playButton);
        playback.Add(TransportButton("Animation.NextKey", "Next frame", () => RunTimelineNavigation(() => ActionEditorContext.Shared.SetFrame(ActionEditorContext.Shared.CurrentFrame + 1))));
        playback.Add(TransportButton("Animation.LastKey", "Last frame", () => RunTimelineNavigation(() => ActionEditorContext.Shared.SetFrame(Mathf.Max(0, (_document?.DurationFrames ?? 1) - 1)))));
        transport.Add(playback);
        _frameLabel = new Label("0") { tooltip = "Current frame" };
        _frameLabel.AddToClassList("action-editor-frame-label");
        transport.Add(_frameLabel);

        var options = new VisualElement();
        options.AddToClassList("action-editor-playback-options");

        var loop = new ToolbarToggle { text = "Loop", value = ActionEditorContext.Shared.PreviewLoop };
        loop.tooltip = "Loop editor frame playback";
        loop.AddToClassList("action-editor-loop-toggle");
        loop.RegisterValueChangedCallback(evt =>
        {
            if (HasActivePointerGesture)
            {
                loop.SetValueWithoutNotify(ActionEditorContext.Shared.PreviewLoop);
                return;
            }
            ActionEditorContext.Shared.SetPreviewLoop(evt.newValue);
        });
        options.Add(loop);

        var snap = new ToolbarToggle { text = "Snap", value = _snapEnabled };
        snap.tooltip = "Magnetic snapping (hold Ctrl/Cmd while dragging to invert)";
        snap.AddToClassList("action-editor-snap-toggle");
        snap.RegisterValueChangedCallback(evt => _snapEnabled = evt.newValue);
        options.Add(snap);
        options.Add(TransportButton("ViewToolZoom", "Fit the authoring horizon", FitAll));
        transport.Add(options);

        var actionField = new ObjectField
        {
            objectType = typeof(ActionAsset),
            allowSceneObjects = false,
            value = ActionEditorContext.Shared.CurrentAction,
            tooltip = "Current Action",
        };
        actionField.AddToClassList("action-editor-action-field");
        actionField.RegisterValueChangedCallback(evt =>
        {
            ActionEditorContext.Shared.SetAction(evt.newValue as ActionAsset);
        });
        transport.Add(actionField);
        rootVisualElement.Add(transport);

        _identityBanner = new VisualElement();
        _identityBanner.AddToClassList("action-editor-identity-banner");
        rootVisualElement.Add(_identityBanner);
    }

    private void BuildTimelineShell()
    {
        _timelineHost = new VisualElement();
        _timelineHost.AddToClassList("action-editor-timeline-main");
        _timelineHost.RegisterCallback<DragUpdatedEvent>(OnAnimationDragUpdated, TrickleDown.TrickleDown);
        _timelineHost.RegisterCallback<DragPerformEvent>(OnAnimationDragPerform, TrickleDown.TrickleDown);

        var topRow = new VisualElement();
        topRow.AddToClassList("action-editor-timeline-top-row");
        _corner = new VisualElement();
        _corner.AddToClassList("action-editor-timeline-corner");
        _addLaneButton = new Button(AddGameplayLane) { tooltip = "Add a Gameplay Lane" };
        _addLaneButton.AddToClassList("action-editor-add-lane");
        _addLaneButton.AddToClassList("action-editor-lane-command");
        SetButtonIcon(_addLaneButton, "CreateAddNew");
        _addLaneButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        VisualElement cornerActions = CreateLaneActions(_corner);
        cornerActions.Add(_addLaneButton);
        _headerResizeHandle = new VisualElement();
        _headerResizeHandle.AddToClassList("action-editor-header-resize-handle");
        _headerResizeHandle.RegisterCallback<PointerDownEvent>(BeginHeaderResize);
        _headerResizeHandle.RegisterCallback<PointerMoveEvent>(ContinueHeaderResize);
        _headerResizeHandle.RegisterCallback<PointerUpEvent>(EndHeaderResize);
        _corner.Add(_headerResizeHandle);
        topRow.Add(_corner);

        _rulerViewport = new VisualElement();
        _rulerViewport.AddToClassList("action-editor-ruler-viewport");
        _rulerCanvas = new IMGUIContainer(DrawRuler);
        _rulerCanvas.AddToClassList("action-editor-ruler-canvas");
        _rulerCanvas.RegisterCallback<PointerDownEvent>(BeginScrub);
        _rulerCanvas.RegisterCallback<PointerMoveEvent>(ContinueScrub);
        _rulerCanvas.RegisterCallback<PointerUpEvent>(EndScrub);
        _rulerViewport.RegisterCallback<WheelEvent>(OnTimelineWheel, TrickleDown.TrickleDown);
        _rulerViewport.Add(_rulerCanvas);
        topRow.Add(_rulerViewport);
        _timelineHost.Add(topRow);

        var bodyRow = new VisualElement();
        bodyRow.AddToClassList("action-editor-timeline-body-row");
        _headerViewport = new VisualElement();
        _headerViewport.AddToClassList("action-editor-header-viewport");
        _headerContent = new VisualElement();
        _headerContent.AddToClassList("action-editor-header-content");
        _headerViewport.Add(_headerContent);
        _headerViewport.RegisterCallback<WheelEvent>(OnHeaderWheel, TrickleDown.TrickleDown);
        bodyRow.Add(_headerViewport);

        _contentScroll = new ScrollView(ScrollViewMode.Vertical);
        _contentScroll.AddToClassList("action-editor-content-scroll");
        // Horizontal navigation is owned exclusively by the Time Range Navigator below. Unity can
        // still instantiate this child scroller while laying out a Vertical ScrollView, so hide it
        // explicitly as well as selecting Vertical mode.
        _contentScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        _contentScroll.horizontalScroller.style.display = DisplayStyle.None;
        _contentScroll.verticalScroller.valueChanged += OnVerticalScroll;
        _contentScroll.RegisterCallback<PointerDownEvent>(_ => CancelPendingScrollRestore(), TrickleDown.TrickleDown);
        _contentScroll.contentViewport.RegisterCallback<WheelEvent>(OnTimelineWheel, TrickleDown.TrickleDown);
        _contentScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
        _contentCanvas = new VisualElement();
        _contentCanvas.AddToClassList("action-editor-content-canvas");
        _contentCanvas.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown, TrickleDown.TrickleDown);
        _contentCanvas.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove);
        _contentCanvas.RegisterCallback<PointerUpEvent>(OnCanvasPointerUp);
        _gridCanvas = new IMGUIContainer(DrawGrid) { pickingMode = PickingMode.Ignore };
        _gridCanvas.AddToClassList("action-editor-grid-canvas");
        _contentCanvas.Add(_gridCanvas);
        _postDurationShade = new VisualElement { pickingMode = PickingMode.Ignore };
        _postDurationShade.AddToClassList("action-editor-post-duration");
        _contentCanvas.Add(_postDurationShade);
        _durationLine = new VisualElement { pickingMode = PickingMode.Ignore };
        _durationLine.AddToClassList("action-editor-duration-line");
        _contentCanvas.Add(_durationLine);
        _horizonLine = new VisualElement { pickingMode = PickingMode.Ignore };
        _horizonLine.AddToClassList("action-editor-horizon-line");
        _contentCanvas.Add(_horizonLine);

        _entryLayer = CreateContentLayer("action-editor-entry-layer", PickingMode.Ignore);
        _contentCanvas.Add(_entryLayer);
        _pointDensityLayer = CreateContentLayer("action-editor-point-density-layer", PickingMode.Ignore);
        _contentCanvas.Add(_pointDensityLayer);
        _ghostLayer = CreateContentLayer("action-editor-ghost-layer", PickingMode.Ignore);
        _contentCanvas.Add(_ghostLayer);
        _guideLayer = CreateContentLayer("action-editor-guide-layer", PickingMode.Ignore);
        _contentCanvas.Add(_guideLayer);

        _playhead = new VisualElement { pickingMode = PickingMode.Ignore };
        _playhead.AddToClassList("action-editor-playhead");
        _guideLayer.Add(_playhead);
        _marquee = new VisualElement { pickingMode = PickingMode.Ignore };
        _marquee.AddToClassList("action-editor-marquee");
        _marquee.style.display = DisplayStyle.None;
        _guideLayer.Add(_marquee);
        _snapGuide = new VisualElement { pickingMode = PickingMode.Ignore };
        _snapGuide.AddToClassList("action-editor-snap-guide");
        _snapGuide.style.display = DisplayStyle.None;
        _guideLayer.Add(_snapGuide);
        _operationLabel = new Label { pickingMode = PickingMode.Ignore };
        _operationLabel.AddToClassList("action-editor-operation-label");
        _operationLabel.style.display = DisplayStyle.None;
        _guideLayer.Add(_operationLabel);
        _contentScroll.Add(_contentCanvas);
        bodyRow.Add(_contentScroll);
        _timelineHost.Add(bodyRow);

        BuildTimeNavigator();

        _emptyOverlay = ActionEditorChrome.EmptyState("Choose an ActionAsset", "Select an ActionAsset above or in the Project window.");
        _emptyOverlay.pickingMode = PickingMode.Ignore;
        _emptyOverlay.style.position = Position.Absolute;
        _emptyOverlay.style.left = 0f;
        _emptyOverlay.style.right = 0f;
        _emptyOverlay.style.top = ActionEditorTheme.RulerHeight;
        _emptyOverlay.style.bottom = 0f;
        _timelineHost.Add(_emptyOverlay);
        _emptyTitle = _emptyOverlay.Q<Label>(className: "action-editor-empty-title");
        _emptyBody = _emptyOverlay.Q<Label>(className: "action-editor-empty-body");

        _geometryOverflowSentinel = new Label("DISPLAY LIMIT") { pickingMode = PickingMode.Ignore };
        _geometryOverflowSentinel.AddToClassList("action-editor-geometry-overflow");
        _geometryOverflowSentinel.tooltip = "Timeline coordinates beyond this presentation boundary keep their raw frame values but are pinned here for UI safety.";
        _guideLayer.Add(_geometryOverflowSentinel);

        _rulerPlayhead = new VisualElement { pickingMode = PickingMode.Ignore };
        _rulerPlayhead.AddToClassList("action-editor-ruler-playhead");
        _rulerViewport.Add(_rulerPlayhead);
        rootVisualElement.Add(_timelineHost);
        ApplyHeaderWidth();
    }

    private void BuildTimeNavigator()
    {
        _navigatorRow = new VisualElement();
        _navigatorRow.AddToClassList("action-editor-time-navigator-row");
        _navigatorCorner = new VisualElement();
        _navigatorCorner.AddToClassList("action-editor-time-navigator-corner");
        _navigatorRow.Add(_navigatorCorner);

        _timeNavigator = new VisualElement();
        _timeNavigator.AddToClassList("action-editor-time-navigator");
        _timeNavigator.RegisterCallback<GeometryChangedEvent>(_ => RefreshTimeNavigator());
        _timeNavigatorThumb = new VisualElement();
        _timeNavigatorThumb.AddToClassList("action-editor-time-navigator-thumb");
        _timeNavigatorThumb.RegisterCallback<PointerDownEvent>(evt => BeginNavigatorInteraction(evt, InteractionState.TimeNavigatorPan));
        _timeNavigatorThumb.RegisterCallback<PointerMoveEvent>(ContinueNavigatorInteraction);
        _timeNavigatorThumb.RegisterCallback<PointerUpEvent>(EndNavigatorInteraction);

        _timeNavigatorLeftHandle = new VisualElement();
        _timeNavigatorLeftHandle.AddToClassList("action-editor-time-navigator-handle");
        _timeNavigatorLeftHandle.AddToClassList("action-editor-time-navigator-handle-left");
        _timeNavigatorLeftHandle.RegisterCallback<PointerDownEvent>(evt => BeginNavigatorInteraction(evt, InteractionState.TimeNavigatorResizeLeft));
        _timeNavigatorLeftHandle.RegisterCallback<PointerMoveEvent>(ContinueNavigatorInteraction);
        _timeNavigatorLeftHandle.RegisterCallback<PointerUpEvent>(EndNavigatorInteraction);
        _timeNavigatorThumb.Add(_timeNavigatorLeftHandle);

        _timeNavigatorRightHandle = new VisualElement();
        _timeNavigatorRightHandle.AddToClassList("action-editor-time-navigator-handle");
        _timeNavigatorRightHandle.AddToClassList("action-editor-time-navigator-handle-right");
        _timeNavigatorRightHandle.RegisterCallback<PointerDownEvent>(evt => BeginNavigatorInteraction(evt, InteractionState.TimeNavigatorResizeRight));
        _timeNavigatorRightHandle.RegisterCallback<PointerMoveEvent>(ContinueNavigatorInteraction);
        _timeNavigatorRightHandle.RegisterCallback<PointerUpEvent>(EndNavigatorInteraction);
        _timeNavigatorThumb.Add(_timeNavigatorRightHandle);
        _timeNavigator.Add(_timeNavigatorThumb);
        _navigatorRow.Add(_timeNavigator);
        _timelineHost.Add(_navigatorRow);
    }

    private void BuildStatusBar()
    {
        var status = new VisualElement();
        status.AddToClassList("action-editor-status-bar");
        _statusLabel = new Label();
        status.Add(_statusLabel);
        rootVisualElement.Add(status);
    }

    private void RefreshDocumentAndViews()
    {
        if (!_uiReady || _contentCanvas == null || _refreshingViews)
            return;

        _refreshingViews = true;
        try
        {
            RefreshDocumentAndViewsCore();
        }
        finally
        {
            _refreshingViews = false;
        }

        FlushPendingContextChanges();
    }

    private void RefreshDocumentAndViewsCore()
    {

        float verticalScroll = _document == null && _hasStoredScroll
            ? _storedVerticalScroll
            : _contentScroll.scrollOffset.y;
        int restoreGeneration = ++_restoreGeneration;
        _rebuildRestorePending = true;
        _document = ActionEditorContext.Shared.Document;
        string actionGuid = CurrentActionGuid(_document.Asset);
        bool sameAction = string.Equals(_viewActionGuid, actionGuid, StringComparison.Ordinal);
        float viewportWidth = TimelineViewportWidth();
        _timeViewport.Initialize(
            _document.HorizonFrames,
            viewportWidth,
            _visibleStartFrame,
            _visibleSpanFrames,
            _workspaceEndFrame,
            _navigationEndFrame,
            sameAction && _hasTimeViewportState);
        _viewActionGuid = actionGuid;
        StoreTimeViewportState();
        _geometry = new ActionTimelineGeometry(
            _document.DurationFrames,
            _document.HorizonFrames,
            _document.Lanes.Count,
            _timeViewport.VisibleStartFrame,
            _timeViewport.VisibleSpanFrames,
            viewportWidth);
        ActionEditorChrome.SyncActionField(rootVisualElement);
        _addLaneButton?.SetEnabled(_document.Asset != null && _document.HasTimeline &&
                                   _document.Readiness != ActionEditorReadiness.IdentityBlocked);
        UpdateEmptyState();
        _transientDisplayKey = null;
        _statusNotice = null;
        _entryViews.Clear();
        _entryPresentation.Clear();
        _laneHeaderViews.Clear();
        _entriesByDisplayKey.Clear();
        _headerContent.Clear();
        ClearCanvasExceptInfrastructure();
        BuildIdentityBanner();
        BuildLaneHeaders();
        BuildEntries();
        FinalizeLayerOrder();
        RefreshGeometry();
        RefreshFramePresentation();
        RefreshSelectionPresentation();
        RefreshStatus();
        rootVisualElement.schedule.Execute(() =>
        {
            if (_contentScroll == null || restoreGeneration != _restoreGeneration)
                return;
            _rebuildRestorePending = false;
            ApplyVerticalScroll(verticalScroll, false);
        });
    }

    /// <summary>
    /// Details leaf fields commonly change content and preview inputs without changing
    /// timeline geometry. Rebuilding the complete visual tree here interrupts native
    /// controls in the other window, so keep this path intentionally narrow.
    /// </summary>
    private void RefreshContentOnly()
    {
        if (!_uiReady || _contentCanvas == null || _refreshingViews)
            return;

        _refreshingViews = true;
        try
        {
            _document = ActionEditorContext.Shared.Document;
            RefreshEntryContentPresentation();
            RefreshStatus();
        }
        finally
        {
            _refreshingViews = false;
        }

        FlushPendingContextChanges();
    }

    private void RefreshEntryContentPresentation()
    {
        foreach (KeyValuePair<string, VisualElement> pair in _entryViews)
        {
            ActionDocumentEntry entry = _document.ContentEntries.FirstOrDefault(candidate =>
                string.Equals(candidate.DisplayKey, pair.Key, StringComparison.Ordinal));
            if (entry == null)
                continue;

            VisualElement view = pair.Value;
            view.userData = entry;
            _entriesByDisplayKey[pair.Key] = entry;
            Label contentLabel = view.Q<Label>(className: "action-editor-entry-label");
            if (contentLabel != null)
                contentLabel.text = EntryLabel(entry, _geometry != null && _geometry.EntryRect(entry).Overflow);
            view.EnableInClassList("action-editor-muted", entry.Muted);
            view.tooltip = EntryTooltip(entry, false);
        }

        // Content/presentation changes do not change lane layout or interrupt playback.
        foreach (VisualElement header in _headerContent.Children())
        {
            if (!(header.userData is ActionDocumentEntry oldEntry)) continue;
            ActionDocumentEntry lane = _document.Lanes.FirstOrDefault(candidate =>
                string.Equals(candidate.DisplayKey, oldEntry.DisplayKey, StringComparison.Ordinal));
            if (lane == null) continue;
            header.userData = lane;
            header.EnableInClassList("action-editor-muted", lane.Muted);
            Label title = header.Q<Label>(className: "action-editor-lane-title");
            if (title != null)
            {
                title.text = EntryLabel(lane, false);
                title.tooltip = $"{title.text}\n{lane.AuthoringPath}\nDisplay: {DisplayStateText(lane.DisplayState)}\n{IdentityText(lane.IdentityState)}";
            }
            Button muteButton = header.Q<Button>(className: "action-editor-lane-mute");
            if (muteButton != null)
            {
                SetButtonIcon(muteButton, lane.Muted ? "animationvisibilitytoggleoff" : "animationvisibilitytoggleon");
                muteButton.tooltip = lane.Muted ? "Unmute Lane" : "Mute Lane";
            }
        }
    }

    private void UpdateEmptyState()
    {
        if (_emptyOverlay == null || _document == null)
            return;

        bool noAction = _document.Asset == null;
        bool missingTimeline = !noAction && !_document.HasTimeline;
        bool emptyTimeline = !noAction && _document.HasTimeline &&
                             _document.AnimationSegments.Count == 0 && _document.Lanes.Count == 0;
        _emptyOverlay.style.display = noAction || missingTimeline || emptyTimeline ? DisplayStyle.Flex : DisplayStyle.None;
        if (noAction)
        {
            _emptyTitle.text = "Choose an ActionAsset";
            _emptyBody.text = "Select an ActionAsset above or in the Project window.";
        }
        else if (missingTimeline)
        {
            _emptyTitle.text = "Timeline data is missing";
            _emptyBody.text = "The asset has no editable Action Timeline data.";
        }
        else if (emptyTimeline)
        {
            _emptyTitle.text = "Timeline is empty";
            _emptyBody.text = "Use + Lane, the Animation lane + button, or a lane context menu to create content.";
        }
    }

    private void ClearCanvasExceptInfrastructure()
    {
        _entryLayer?.Clear();
        _entryPresentation.Clear();
        _pointDensityLayer?.Clear();
        _pointDensityMarkers.Clear();
        _densityClustersByLane.Clear();
        _hiddenDensityClustersByLane.Clear();
        _reservedMarkerRectsByLane.Clear();
        _ghostLayer?.Clear();
        _hoverDisplayKey = null;
    }

    private void FinalizeLayerOrder()
    {
        _entryLayer?.BringToFront();
        _pointDensityLayer?.BringToFront();
        _ghostLayer?.BringToFront();
        _guideLayer?.BringToFront();
    }

    private void BuildIdentityBanner()
    {
        _identityBanner.Clear();
        bool blocked = _document != null && _document.Readiness == ActionEditorReadiness.IdentityBlocked;
        _identityBanner.style.display = blocked ? DisplayStyle.Flex : DisplayStyle.None;
        if (!blocked)
            return;

        var text = new HelpBox(
            "Some Timeline items do not have a safe editor identity. Repair Editor IDs before editing them.",
            HelpBoxMessageType.Error);
        text.AddToClassList("action-editor-banner-text");
        _identityBanner.Add(text);
        var repair = new Button(RepairIdentity) { text = "Repair Editor IDs" };
        repair.tooltip = "Generate new IDs only for missing, malformed, or later duplicate entries. This is one Undo step.";
        _identityBanner.Add(repair);
    }

    private void RepairIdentity()
    {
        if (HasActivePointerGesture)
        {
            SetStatusNotice("Finish or cancel the active Timeline gesture before repairing IDs.");
            return;
        }
        ActionAsset asset = _document?.Asset;
        if (ActionEditorCommands.RepairEditorIds(asset, out int repaired, out string message))
        {
            _transientDisplayKey = null;
            _statusNotice = message;
            Debug.Log($"[Action Authoring] {message} Asset: '{asset.name}'.", asset);
        }
        else
        {
            _statusNotice = message;
        }
        RefreshStatus();
    }

    private void BuildLaneHeaders()
    {
        VisualElement animationHeader = CreateLaneHeader(
            "Animation",
            string.Empty,
            ActionEditorTheme.AnimationLaneHeight,
            false,
            null);
        animationHeader.AddToClassList("action-editor-animation-header");
        _headerContent.Add(animationHeader);
        foreach (ActionDocumentEntry lane in _document.Lanes)
        {
            string meta = lane.Source == null ? "Unavailable" : string.Empty;
            VisualElement header = CreateLaneHeader(EntryLabel(lane, false), meta, ActionEditorTheme.GameplayLaneHeight, lane.Muted, lane);
            if (lane.Source is GameplayLane gameplayLane && lane.HasStableSelection)
            {
                VisualElement actions = CreateLaneActions(header);
                var mute = new Button(() => SetMuted(lane, !((GameplayLane)lane.Source).Muted))
                {
                    tooltip = gameplayLane.Muted ? "Unmute Lane" : "Mute Lane",
                    focusable = false,
                };
                mute.AddToClassList("action-editor-lane-mute");
                mute.AddToClassList("action-editor-lane-command");
                mute.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                SetButtonIcon(mute, gameplayLane.Muted ? "animationvisibilitytoggleoff" : "animationvisibilitytoggleon");
                mute.SetEnabled(CanAuthor(out _));
                actions.Add(mute);
                Button commands = LaneCommandButton("⋮", "Lane commands", () => ShowLaneMenu(lane));
                commands.AddToClassList("action-editor-lane-menu");
                header.Add(commands);
            }
            else if (lane.Source == null && _document.Readiness != ActionEditorReadiness.IdentityBlocked)
            {
                ActionEditorDocument sourceDocument = _document;
                CreateLaneActions(header).Add(LaneCommandButton("×", "Delete quarantined null Lane", () => DeleteNullEntry(sourceDocument, lane)));
            }
            _headerContent.Add(header);
        }
    }

    private VisualElement CreateLaneHeader(string title, string meta, float height, bool muted, ActionDocumentEntry entry)
    {
        var row = new VisualElement();
        row.AddToClassList("action-editor-lane-header");
        row.style.height = height;
        var surface = new VisualElement { pickingMode = PickingMode.Ignore };
        surface.AddToClassList("action-editor-lane-surface");
        row.Add(surface);
        row.RegisterCallback<PointerDownEvent>(_ => FocusTimelineCommands());
        if (muted) row.AddToClassList("action-editor-muted");
        if (entry != null && entry.IdentityState != ActionEditorIdentityState.Valid)
            row.AddToClassList("action-editor-invalid-identity");
        if (entry != null && entry.DisplayState != ActionEntryDisplayState.Normal)
            row.AddToClassList("action-editor-placeholder-header");
        var titleLabel = new Label(title);
        titleLabel.AddToClassList("action-editor-lane-title");
        titleLabel.tooltip = entry == null
            ? title
            : $"{title}\n{entry.AuthoringPath}\nDisplay: {DisplayStateText(entry.DisplayState)}\n{IdentityText(entry.IdentityState)}";
        row.Add(titleLabel);
        var metaLabel = new Label(meta);
        metaLabel.AddToClassList("action-editor-lane-meta");
        row.Add(metaLabel);
        if (entry != null)
        {
            row.userData = entry;
            row.RegisterCallback<PointerDownEvent>(evt => SelectEntry(evt, entry, true));
            titleLabel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (HasActivePointerGesture)
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                if (evt.button == 0 && evt.clickCount >= 2 && entry.Source is GameplayLane)
                {
                    if (entry.HasStableSelection)
                    {
                        ActionEditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, false, false);
                        BeginInlineRename(entry, row, titleLabel);
                    }
                    evt.StopPropagation();
                }
            });
            _laneHeaderViews[entry.DisplayKey] = row;
        }
        return row;
    }

    private static VisualElement CreateLaneActions(VisualElement parent)
    {
        var actions = new VisualElement();
        actions.AddToClassList("action-editor-lane-actions");
        parent.Add(actions);
        return actions;
    }

    private void BuildEntries()
    {
        foreach (ActionDocumentEntry entry in _document.AnimationSegments)
            AddEntry(entry, 0f, ActionEditorTheme.AnimationLaneHeight, true);
        foreach (ActionDocumentEntry entry in _document.GameplayItems)
        {
            float y = _geometry.LaneTop(entry.LaneIndex);
            AddEntry(entry, y, ActionEditorTheme.GameplayLaneHeight, false);
        }
    }

    private void AddEntry(ActionDocumentEntry entry, float rowTop, float rowHeight, bool animation)
    {
        bool pointEntry = entry.Source is PointGameplayItem;
        var view = new VisualElement { userData = entry };
        view.AddToClassList("action-editor-entry");
        view.AddToClassList(animation ? "action-editor-animation-entry" : ItemClass(entry.Source));
        if (pointEntry)
            view.AddToClassList("action-editor-point-entry");
        if (entry.Muted)
            view.AddToClassList("action-editor-muted");
        if (entry.IdentityState != ActionEditorIdentityState.Valid)
            view.AddToClassList("action-editor-invalid-identity");
        ApplyPlaceholderClasses(view, entry.DisplayState);
        if (pointEntry)
        {
            var diamond = new VisualElement { pickingMode = PickingMode.Ignore };
            diamond.AddToClassList("action-editor-point-diamond");
            view.Add(diamond);
        }

        VisualElement contentHost = view;
        if (!pointEntry)
        {
            view.AddToClassList("action-editor-range-entry");
            contentHost = new VisualElement { pickingMode = PickingMode.Ignore };
            contentHost.AddToClassList("action-editor-range-body");
            view.Add(contentHost);
        }

        var label = new Label(EntryLabel(entry, false)) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("action-editor-entry-label");
        if (pointEntry)
            label.AddToClassList("action-editor-point-label");
        contentHost.Add(label);
        view.tooltip = EntryTooltip(entry, false);
        if (entry.Source is RangeGameplayItem || entry.Source is AnimationSegment)
        {
            var leftHandle = new VisualElement { pickingMode = PickingMode.Ignore };
            leftHandle.AddToClassList("action-editor-entry-handle");
            leftHandle.AddToClassList("action-editor-entry-handle-left");
            contentHost.Add(leftHandle);
            var rightHandle = new VisualElement { pickingMode = PickingMode.Ignore };
            rightHandle.AddToClassList("action-editor-entry-handle");
            rightHandle.AddToClassList("action-editor-entry-handle-right");
            contentHost.Add(rightHandle);
        }
        view.RegisterCallback<PointerDownEvent>(evt => OnEntryPointerDown(evt, entry, view));
        view.RegisterCallback<PointerMoveEvent>(evt => OnEntryPointerMove(evt, entry, view));
        view.RegisterCallback<PointerUpEvent>(OnManipulationPointerUp);
        view.RegisterCallback<PointerEnterEvent>(_ => SetHoveredEntry(entry.DisplayKey));
        view.RegisterCallback<PointerLeaveEvent>(_ => ClearHoveredEntry(entry.DisplayKey));
        view.style.top = rowTop + 3f;
        view.style.height = Mathf.Max(18f, rowHeight - 6f);
        _entryLayer.Add(view);
        _entryViews[entry.DisplayKey] = view;
        _entriesByDisplayKey[entry.DisplayKey] = entry;
    }

    private void RefreshPointDensityPresentation(bool rebuildGroups = true)
    {
        if (_pointDensityLayer == null || _document == null || _geometry == null)
            return;

        List<PointDensityCluster> cachedClusters = rebuildGroups
            ? null
            : _densityClustersByLane.OrderBy(pair => pair.Key).SelectMany(pair => pair.Value).ToList();
        if (rebuildGroups)
            _densityClustersByLane.Clear();
        _hiddenDensityClustersByLane.Clear();
        _reservedMarkerRectsByLane.Clear();
        foreach (ActionDocumentEntry point in _document.GameplayItems.Where(entry => entry.Source is PointGameplayItem))
        {
            if (_entryViews.TryGetValue(point.DisplayKey, out VisualElement pointView))
            {
                pointView.style.display = DisplayStyle.Flex;
                pointView.EnableInClassList("action-editor-point-density-member", false);
            }
            if (_entryPresentation.TryGetValue(point.DisplayKey, out EntryPresentation presentation))
                presentation.Displayed = true;
        }

        string primaryId = ActionEditorContext.Shared.PrimarySelection.EditorId;
        ActionDocumentEntry primaryPoint = _document.GameplayItems.FirstOrDefault(entry =>
            entry.Source is PointGameplayItem && entry.HasStableSelection &&
            string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal));
        if (primaryPoint != null && _entryPresentation.TryGetValue(primaryPoint.DisplayKey, out EntryPresentation primaryPresentation))
            ReservedRects(primaryPoint.LaneIndex).Add(primaryPresentation.HitRect);

        const float collisionDistance = 26f;
        var activeClusters = new Dictionary<string, PointDensityCluster>();
        if (rebuildGroups)
        foreach (IGrouping<int, ActionDocumentEntry> lanePoints in _document.GameplayItems
                     .Where(entry => entry.Source is PointGameplayItem &&
                                     entry.DisplayState == ActionEntryDisplayState.Normal)
                     .GroupBy(entry => entry.LaneIndex))
        {
            var positioned = lanePoints
                .Select(entry => new
                {
                    Entry = entry,
                    X = _geometry.FrameToPixel(Math.Max(0L, entry.StartFrame), out bool overflow),
                    Overflow = overflow,
                })
                .Where(item => !item.Overflow && item.X >= -ActionTimelineGeometry.PointHitWidth &&
                               item.X <= _geometry.ContentWidth + ActionTimelineGeometry.PointHitWidth)
                .OrderBy(item => item.X)
                .ThenBy(item => item.Entry.ItemIndex)
                .ToList();

            foreach (List<int> indices in ActionTimelineInteractionMath.BoundedGroups(
                         positioned.Select(item => (double)item.X).ToList(), collisionDistance))
            {
                var cluster = new PointDensityCluster { LaneIndex = lanePoints.Key };
                foreach (int index in indices)
                    cluster.Entries.Add(positioned[index].Entry);
                RegisterPointDensityCluster(cluster, activeClusters, true);
            }
        }
        else if (cachedClusters != null)
        {
            foreach (PointDensityCluster cluster in cachedClusters)
                RegisterPointDensityCluster(cluster, activeClusters, false);
        }

        foreach (KeyValuePair<string, Button> stale in _pointDensityMarkers.Where(pair => !activeClusters.ContainsKey(pair.Key)).ToList())
        {
            stale.Value.RemoveFromHierarchy();
            _pointDensityMarkers.Remove(stale.Key);
        }
    }

    private void RegisterPointDensityCluster(PointDensityCluster cluster, Dictionary<string, PointDensityCluster> activeClusters,
        bool registerIndex)
    {
        if (cluster == null || cluster.Entries.Count < 2)
            return;

        cluster.Key = $"{cluster.LaneIndex}:{string.Join(",", cluster.Entries.Select(entry => entry.DisplayKey))}";
        activeClusters[cluster.Key] = cluster;
        if (registerIndex)
        {
            if (!_densityClustersByLane.TryGetValue(cluster.LaneIndex, out List<PointDensityCluster> laneClusters))
            {
                laneClusters = new List<PointDensityCluster>();
                _densityClustersByLane.Add(cluster.LaneIndex, laneClusters);
            }
            laneClusters.Add(cluster);
        }

        string primaryId = ActionEditorContext.Shared.PrimarySelection.EditorId;
        ActionDocumentEntry primary = cluster.Entries.FirstOrDefault(entry =>
            entry.HasStableSelection && string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal));
        ActionDocumentEntry focused = primary;
        if (focused == null)
            focused = cluster.Entries.FirstOrDefault(entry =>
                entry.HasStableSelection && ActionEditorContext.Shared.IsSelected(entry.EditorId));
        if (focused == null && !string.IsNullOrEmpty(_transientDisplayKey))
            focused = cluster.Entries.FirstOrDefault(entry => entry.DisplayKey == _transientDisplayKey);
        if (focused == null && !string.IsNullOrEmpty(_hoverDisplayKey))
            focused = cluster.Entries.FirstOrDefault(entry => entry.DisplayKey == _hoverDisplayKey);

        foreach (ActionDocumentEntry entry in cluster.Entries)
        {
            if (!_entryViews.TryGetValue(entry.DisplayKey, out VisualElement view))
                continue;
            view.EnableInClassList("action-editor-point-density-member", true);
            // The primary Point remains a real, directly draggable diamond at its source Frame.
            bool exposePrimary = ReferenceEquals(entry, primary);
            view.style.display = exposePrimary ? DisplayStyle.Flex : DisplayStyle.None;
            if (_entryPresentation.TryGetValue(entry.DisplayKey, out EntryPresentation presentation))
                presentation.Displayed = exposePrimary;
        }

        float firstX = _geometry.FrameToPixel(Math.Max(0L, cluster.Entries[0].StartFrame), out _);
        float lastX = _geometry.FrameToPixel(Math.Max(0L, cluster.Entries[cluster.Entries.Count - 1].StartFrame), out _);
        float anchorX = (firstX + lastX) * 0.5f;
        const float markerWidth = 24f;
        float markerLeft = anchorX - markerWidth * 0.5f;
        markerLeft = Mathf.Clamp(markerLeft, 0f, Mathf.Max(0f, _geometry.ContentWidth - markerWidth));
        var markerRect = new Rect(markerLeft, _geometry.LaneTop(cluster.LaneIndex) + 6f, markerWidth, 20f);
        List<Rect> reserved = ReservedRects(cluster.LaneIndex);
        bool markerHidden = !ActionTimelineInteractionMath.CanPlaceMarker(markerRect, reserved);
        cluster.MarkerHiddenByPrimary = markerHidden;
        if (markerHidden)
        {
            if (!_hiddenDensityClustersByLane.TryGetValue(cluster.LaneIndex, out List<PointDensityCluster> hidden))
            {
                hidden = new List<PointDensityCluster>();
                _hiddenDensityClustersByLane.Add(cluster.LaneIndex, hidden);
            }
            hidden.Add(cluster);
        }
        else
        {
            reserved.Add(markerRect);
        }

        if (!_pointDensityMarkers.TryGetValue(cluster.Key, out Button marker))
        {
            marker = new Button { text = string.Empty, focusable = false };
            marker.AddToClassList("action-editor-point-density-cluster");
            marker.clicked += () =>
            {
                if (marker.userData is PointDensityCluster current)
                    ShowPointDensityPicker(current);
            };
            var backDiamond = new VisualElement { pickingMode = PickingMode.Ignore };
            backDiamond.AddToClassList("action-editor-point-density-diamond-back");
            marker.Add(backDiamond);
            var frontDiamond = new VisualElement { pickingMode = PickingMode.Ignore };
            frontDiamond.AddToClassList("action-editor-point-density-diamond-front");
            marker.Add(frontDiamond);
            var count = new Label { pickingMode = PickingMode.Ignore };
            count.AddToClassList("action-editor-point-density-count");
            marker.Add(count);
            marker.RegisterCallback<PointerDownEvent>(_ => FocusTimelineCommands());
            _pointDensityLayer.Add(marker);
            _pointDensityMarkers.Add(cluster.Key, marker);
        }
        marker.userData = cluster;
        marker.Q<Label>(className: "action-editor-point-density-count").text = cluster.Entries.Count > 99 ? "99+" : cluster.Entries.Count.ToString();
        marker.EnableInClassList("action-editor-point-density-focused", focused != null);
        marker.style.left = markerLeft;
        marker.style.width = markerWidth;
        marker.style.top = _geometry.LaneTop(cluster.LaneIndex) + 6f;
        marker.style.display = markerHidden ? DisplayStyle.None : DisplayStyle.Flex;
        marker.tooltip =
            $"{cluster.Entries.Count} Point items are grouped only because the current zoom cannot display them separately.\n" +
            (focused != null ? $"Focused: {focused.DisplayName} at Frame {focused.StartFrame}.\n" : string.Empty) +
            "Display density only · no gameplay overlap or priority.";
    }

    private List<Rect> ReservedRects(int laneIndex)
    {
        if (!_reservedMarkerRectsByLane.TryGetValue(laneIndex, out List<Rect> reserved))
        {
            reserved = new List<Rect>();
            _reservedMarkerRectsByLane.Add(laneIndex, reserved);
        }
        return reserved;
    }

    private void ShowPointDensityPicker(PointDensityCluster cluster)
    {
        FocusTimelineCommands();
        var menu = new GenericMenu();
        menu.AddDisabledItem(new GUIContent("Display density only · no overlap or priority"));
        menu.AddSeparator(string.Empty);
        foreach (ActionDocumentEntry entry in cluster.Entries.OrderBy(item => item.ItemIndex))
        {
            ActionDocumentEntry captured = entry;
            string label = $"{entry.DisplayName} · Frame {entry.StartFrame} · {entry.AuthoringPath}";
            menu.AddItem(new GUIContent(label),
                entry.HasStableSelection && ActionEditorContext.Shared.IsSelected(entry.EditorId),
                () => NavigateToEntry(captured, true));
        }
        menu.ShowAsContext();
    }

    private void NavigateToEntry(ActionDocumentEntry entry, bool reveal)
    {
        entry = ResolveCurrentEntry(entry);
        if (entry == null || HasActivePointerGesture)
            return;

        FocusTimelineCommands();
        _statusNotice = null;
        if (entry.HasStableSelection)
        {
            _transientDisplayKey = null;
            ActionEditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, false, false);
        }
        else
        {
            _transientDisplayKey = entry.DisplayKey;
            _statusNotice = $"Located {entry.AuthoringPath}; stable selection is unavailable";
            RefreshSelectionPresentation();
        }
        if (reveal)
            FrameEntry(entry);
    }

    private ActionDocumentEntry ResolveCurrentEntry(ActionDocumentEntry stale)
    {
        if (stale == null || _document == null ||
            !ReferenceEquals(_document.Asset, ActionEditorContext.Shared.CurrentAction))
            return null;
        if (stale.HasStableSelection && _document.ById.TryGetValue(stale.EditorId, out ActionDocumentEntry byId) &&
            ReferenceEquals(byId.Source, stale.Source))
            return byId;
        return _document.ContentEntries.Concat(_document.Lanes).FirstOrDefault(candidate =>
            candidate.SelectionKind == stale.SelectionKind &&
            string.Equals(candidate.AuthoringPath, stale.AuthoringPath, StringComparison.Ordinal) &&
            ReferenceEquals(candidate.Source, stale.Source));
    }

    private void RefreshGeometry()
    {
        if (_document == null || _contentCanvas == null)
            return;
        float viewportWidth = TimelineViewportWidth();
        _timeViewport.SetViewportWidth(viewportWidth);
        _timeViewport.UpdateWorkspace(_document.HorizonFrames);
        StoreTimeViewportState();
        _geometry = new ActionTimelineGeometry(
            _document.DurationFrames,
            _document.HorizonFrames,
            _document.Lanes.Count,
            _timeViewport.VisibleStartFrame,
            _timeViewport.VisibleSpanFrames,
            viewportWidth);
        float width = _geometry.ContentWidth;
        float height = _geometry.ContentHeight;
        _contentCanvas.style.width = width;
        _contentCanvas.style.height = height;
        _headerContent.style.height = height;
        _rulerCanvas.style.width = width;
        _gridCanvas.style.width = width;
        _gridCanvas.style.height = height;
        SetLayerSize(_entryLayer, width, height);
        SetLayerSize(_pointDensityLayer, width, height);
        SetLayerSize(_ghostLayer, width, height);
        SetLayerSize(_guideLayer, width, height);

        float durationX = _geometry.FrameToPixel(_document.DurationFrames, out bool durationOverflow);
        _durationLine.style.display = durationOverflow ? DisplayStyle.None : DisplayStyle.Flex;
        _durationLine.style.left = durationX;
        _postDurationShade.style.display = durationOverflow ? DisplayStyle.None : DisplayStyle.Flex;
        _postDurationShade.style.left = durationX;
        _postDurationShade.style.width = Mathf.Max(0f, width - durationX);
        _postDurationShade.style.height = height;
        float horizonX = _geometry.FrameToPixel(_document.HorizonFrames, out bool horizonOverflow);
        bool horizonVisible = !horizonOverflow && horizonX >= 0f && horizonX <= width;
        _horizonLine.style.display = horizonVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _horizonLine.style.left = horizonX;
        bool hasPinnedOverflow = false;
        _geometryOverflowSentinel.style.display = DisplayStyle.None;
        _geometryOverflowSentinel.style.left = Mathf.Max(0f, width - 88f);
        _geometryOverflowSentinel.style.height = height;

        foreach (KeyValuePair<string, VisualElement> pair in _entryViews)
        {
            ActionDocumentEntry entry = _entriesByDisplayKey[pair.Key];
            ActionEntryGeometry layout = _geometry.EntryRect(entry);
            pair.Value.style.left = layout.Left;
            pair.Value.style.width = layout.Width;
            pair.Value.style.top = _geometry.LaneTop(entry.LaneIndex) + 3f;
            VisualElement rangeBody = pair.Value.Q<VisualElement>(className: "action-editor-range-body");
            if (rangeBody != null)
            {
                rangeBody.style.left = layout.VisualLeft;
                rangeBody.style.width = layout.VisualWidth;
            }
            pair.Value.EnableInClassList("action-editor-placeholder-overflow", layout.Overflow);
            hasPinnedOverflow |= layout.Overflow;
            Label label = pair.Value.Q<Label>(className: "action-editor-entry-label");
            if (label != null)
                label.text = EntryLabel(entry, layout.Overflow);
            if (entry.Source is PointGameplayItem)
                PositionPointDiamond(pair.Value, entry, layout);
            pair.Value.tooltip = EntryTooltip(entry, layout.Overflow);

            float rowTop = _geometry.LaneTop(entry.LaneIndex);
            float rowHeight = entry.LaneIndex < 0
                ? ActionEditorTheme.AnimationLaneHeight
                : ActionEditorTheme.GameplayLaneHeight;
            if (entry.Source is PointGameplayItem)
            {
                float pointX = _geometry.FrameToPixel(Math.Max(0L, entry.StartFrame), out _);
                var center = new Vector2(pointX, rowTop + 16f);
                _entryPresentation[entry.DisplayKey] = new EntryPresentation
                {
                    IsPoint = true,
                    PointCenter = center,
                    VisualRect = new Rect(center.x - 7.1f, center.y - 7.1f, 14.2f, 14.2f),
                    HitRect = new Rect(center.x - 7f, rowTop + 3f, 14f, Mathf.Max(18f, rowHeight - 6f)),
                    Displayed = pair.Value.resolvedStyle.display != DisplayStyle.None,
                };
            }
            else
            {
                _entryPresentation[entry.DisplayKey] = new EntryPresentation
                {
                    VisualRect = new Rect(layout.Left + layout.VisualLeft, rowTop + 3f,
                        Mathf.Max(1f, layout.VisualWidth), Mathf.Max(18f, rowHeight - 6f)),
                    HitRect = new Rect(layout.Left, rowTop + 3f, Mathf.Max(1f, layout.Width), Mathf.Max(18f, rowHeight - 6f)),
                    Displayed = pair.Value.resolvedStyle.display != DisplayStyle.None,
                };
            }
        }

        RefreshPointDensityPresentation();
        _geometryOverflowSentinel.style.display = hasPinnedOverflow ? DisplayStyle.Flex : DisplayStyle.None;
        RefreshEntryLayering();
        FinalizeLayerOrder();
        _rulerCanvas.MarkDirtyRepaint();
        _gridCanvas.MarkDirtyRepaint();
        ClampCurrentVerticalScroll();
        RefreshTimeNavigator();
        RefreshFramePresentation();
    }

    private void RefreshFramePresentation()
    {
        if (!_uiReady || _frameLabel == null || _document == null)
            return;

        int frame = ActionEditorContext.Shared.CurrentFrame;
        _frameLabel.text = frame.ToString();
        float x = _geometry != null
            ? Mathf.Min(Mathf.Max(0f, _geometry.ContentWidth - 1f), _geometry.FrameToPixel(frame, out _))
            : 0f;
        if (_playhead != null) _playhead.style.left = x;
        if (_rulerPlayhead != null) _rulerPlayhead.style.left = x;
        foreach (KeyValuePair<string, VisualElement> pair in _entryViews)
        {
            ActionDocumentEntry entry = _entriesByDisplayKey[pair.Key];
            pair.Value.EnableInClassList("action-editor-entry-active", frame >= entry.StartFrame && frame < entry.RawEndFrameExclusive);
            pair.Value.EnableInClassList("action-editor-entry-hold", false);
        }
        ActionDocumentEntry activeAnimation = _document.AnimationSegments.FirstOrDefault(entry =>
            entry.Source != null && frame >= entry.StartFrame && frame < entry.RawEndFrameExclusive);
        if (activeAnimation == null)
        {
            ActionDocumentEntry heldAnimation = _document.AnimationSegments
                .Where(entry => entry.Source != null && entry.RawEndFrameExclusive <= frame)
                .OrderBy(entry => entry.RawEndFrameExclusive)
                .LastOrDefault();
            if (heldAnimation != null && _entryViews.TryGetValue(heldAnimation.DisplayKey, out VisualElement heldView))
                heldView.EnableInClassList("action-editor-entry-hold", true);
        }
        RefreshStatus();
    }

    private void RefreshSelectionPresentation()
    {
        string primaryId = ActionEditorContext.Shared.PrimarySelection.EditorId;
        foreach (KeyValuePair<string, VisualElement> pair in _entryViews)
        {
            ActionDocumentEntry entry = _entriesByDisplayKey[pair.Key];
            bool selected = entry.HasStableSelection && ActionEditorContext.Shared.IsSelected(entry.EditorId);
            bool primary = selected && string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal);
            bool transient = string.Equals(_transientDisplayKey, entry.DisplayKey, StringComparison.Ordinal);
            pair.Value.EnableInClassList("action-editor-entry-selected", selected);
            pair.Value.EnableInClassList("action-editor-entry-primary", primary);
            pair.Value.EnableInClassList("action-editor-entry-transient", transient);
        }
        foreach (KeyValuePair<string, VisualElement> pair in _laneHeaderViews)
        {
            ActionDocumentEntry lane = _document.Lanes.FirstOrDefault(entry => entry.DisplayKey == pair.Key);
            bool selected = lane != null && lane.HasStableSelection && ActionEditorContext.Shared.IsSelected(lane.EditorId);
            bool transient = string.Equals(_transientDisplayKey, pair.Key, StringComparison.Ordinal);
            pair.Value.EnableInClassList("action-editor-lane-header-selected", selected || transient);
        }
        RefreshPointDensityPresentation(false);
        RefreshEntryLayering();
        FinalizeLayerOrder();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (_document == null)
            return;
        _statusLabel.text = $"Duration {_document.DurationFrames}f";
    }

    private bool CanAuthor(out string message)
    {
        if (HasActivePointerGesture && _interaction != InteractionState.PendingManipulation)
        {
            message = "Finish or cancel the active Timeline gesture first.";
            return false;
        }
        if (_document?.Asset == null || !_document.HasTimeline)
        {
            message = "Choose an ActionAsset with ActionRuntime Timeline data.";
            return false;
        }
        if (_document.Readiness == ActionEditorReadiness.IdentityBlocked)
        {
            message = "Repair Editor IDs before modifying the Timeline.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private bool HasActivePointerGesture => _interaction != InteractionState.Idle && _interaction != InteractionState.InlineRename;

    private bool TryBeginPointerGesture(PointerDownEvent evt, InteractionState state, VisualElement owner)
    {
        // Selection and viewport gestures only capture the pointer. Acquiring the asset edit
        // lock here needlessly disables Details even for a click on empty space.
        // Move/trim acquire that lock separately in BeginManipulation.
        if (evt == null || owner == null || _interaction != InteractionState.Idle ||
            ActionEditorInteractionGate.IsActive)
        {
            evt?.StopImmediatePropagation();
            return false;
        }
        _interaction = state;
        _capturedPointer = evt.pointerId;
        _captureTarget = owner;
        owner.CapturePointer(evt.pointerId);
        return true;
    }

    private void AddGameplayLane()
    {
        FocusTimelineCommands();
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        GameplayLane lane = ActionEditorCommands.AddLane(_document.Asset);
        SetStatusNotice(lane != null ? $"Added lane '{lane.Name}'." : "Could not add a Gameplay Lane.");
        if (lane != null) RevealById(lane.EditorId);
    }

    private void BeginAnimationPicker(int frame)
    {
        FocusTimelineCommands();
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        CancelAnimationPicker();
        _pendingAnimationFrame = Mathf.Max(0, frame);
        _animationPickerControlId = GetInstanceID();
        EditorGUIUtility.ShowObjectPicker<AnimationAsset>(null, false, string.Empty, _animationPickerControlId);
        SetStatusNotice($"Choose an AnimationAsset for Frame {_pendingAnimationFrame}.");
    }

    private bool CanDropAnimation(Vector2 panelPosition, out AnimationAsset animation, out int frame, out string message)
    {
        animation = null;
        frame = 0;
        message = "Drop one AnimationAsset onto the Animation track.";
        if (_geometry == null || _contentCanvas == null || _contentScroll == null)
            return false;

        Vector2 local = _contentCanvas.WorldToLocal(panelPosition);
        if (!_contentScroll.contentViewport.worldBound.Contains(panelPosition) ||
            local.y < 0f || local.y >= ActionEditorTheme.AnimationLaneHeight ||
            DragAndDrop.objectReferences.Length != 1 ||
            !(DragAndDrop.objectReferences[0] is AnimationAsset candidate))
            return false;

        if (!CanAuthor(out message))
            return false;
        animation = candidate;
        frame = _geometry.PixelToFrame(local.x);
        return ActionEditorCommands.CanAddAnimationSegment(_document.Asset, animation, frame, out message);
    }

    private void OnAnimationDragUpdated(DragUpdatedEvent evt)
    {
        bool allowed = CanDropAnimation(evt.mousePosition, out AnimationAsset animation, out int frame, out string message);
        DragAndDrop.visualMode = allowed ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        SetStatusNotice(allowed ? $"Drop '{animation.name}' at Frame {frame}." : message);
        evt.StopPropagation();
    }

    private void OnAnimationDragPerform(DragPerformEvent evt)
    {
        evt.StopPropagation();
        if (!CanDropAnimation(evt.mousePosition, out AnimationAsset animation, out int frame, out string message))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            SetStatusNotice(message);
            return;
        }

        DragAndDrop.AcceptDrag();
        CancelAnimationPicker();
        bool committed = ActionEditorCommands.AddAnimationSegment(_document.Asset, animation, frame, out message);
        SetStatusNotice(committed ? $"Added '{animation.name}' at Frame {frame}." : message);
        if (committed) RevealPrimarySelection();
    }

    private void OnExecuteCommand(ExecuteCommandEvent evt)
    {
        if (_animationPickerControlId == 0 ||
            (evt.commandName != "ObjectSelectorClosed" && evt.commandName != "ObjectSelectorUpdated") ||
            EditorGUIUtility.GetObjectPickerControlID() != _animationPickerControlId)
            return;

        evt.StopPropagation();
        if (evt.commandName != "ObjectSelectorClosed")
            return;

        AnimationAsset selected = EditorGUIUtility.GetObjectPickerObject() as AnimationAsset;
        int frame = _pendingAnimationFrame;
        CancelAnimationPicker();
        if (selected == null)
        {
            SetStatusNotice("Animation creation cancelled.");
            return;
        }
        if (!CanAuthor(out string readinessMessage))
        {
            SetStatusNotice(readinessMessage);
            return;
        }
        bool committed = ActionEditorCommands.AddAnimationSegment(_document.Asset, selected, frame, out string message);
        SetStatusNotice(committed ? $"Added '{selected.name}' at Frame {frame}." : message);
        if (committed) RevealPrimarySelection();
    }

    private void CancelAnimationPicker()
    {
        _animationPickerControlId = 0;
        _pendingAnimationFrame = 0;
    }

    private void ShowCanvasCreationMenu(Vector2 canvasPosition)
    {
        if (_geometry == null || HasActivePointerGesture)
            return;
        int frame = _geometry.PixelToFrame(canvasPosition.x);
        if (canvasPosition.y < ActionEditorTheme.AnimationLaneHeight)
        {
            var menu = new GenericMenu();
            AddAuthoringMenuItem(menu, "Add Animation Segment…", () => BeginAnimationPicker(frame));
            menu.ShowAsContext();
            return;
        }

        int laneIndex = Mathf.FloorToInt((canvasPosition.y - ActionEditorTheme.AnimationLaneHeight) /
                                         ActionEditorTheme.GameplayLaneHeight);
        ActionDocumentEntry lane = _document?.Lanes.FirstOrDefault(candidate => candidate.LaneIndex == laneIndex);
        if (lane != null)
            ShowItemCreationMenu(lane, frame);
    }

    private void ShowItemCreationMenu(ActionDocumentEntry lane, int frame)
    {
        if (HasActivePointerGesture)
            return;
        FocusTimelineCommands();
        var menu = new GenericMenu();
        if (lane == null || !lane.HasStableSelection || lane.Source is not GameplayLane)
        {
            menu.AddDisabledItem(new GUIContent("Target Lane is not editable"));
            menu.ShowAsContext();
            return;
        }
        AddAuthoringMenuItem(menu, "Point/Impulse", () => AddGameplayItem(lane, typeof(ImpulseItem), frame));
        menu.AddSeparator(string.Empty);
        AddAuthoringMenuItem(menu, "Collision/Hit Box", () => AddGameplayItem(lane, typeof(HitBoxItem), frame));
        menu.AddSeparator(string.Empty);
        AddAuthoringMenuItem(menu, "Motion/Root Motion", () => AddGameplayItem(lane, typeof(RootMotionItem), frame));
        AddAuthoringMenuItem(menu, "Motion/Self Rotation", () => AddGameplayItem(lane, typeof(SelfRotationItem), frame));
        AddAuthoringMenuItem(menu, "Motion/Velocity Override", () => AddGameplayItem(lane, typeof(VelocityOverrideItem), frame));
        menu.AddSeparator(string.Empty);
        AddAuthoringMenuItem(menu, "State/Motion Policy", () => AddGameplayItem(lane, typeof(MotionPolicyItem), frame));
        AddAuthoringMenuItem(menu, "State/Tag", () => AddGameplayItem(lane, typeof(TagItem), frame));
        menu.ShowAsContext();
    }

    private void AddAuthoringMenuItem(GenericMenu menu, string label, Action action)
    {
        if (CanAuthor(out _))
            menu.AddItem(new GUIContent(label), false, () => { FocusTimelineCommands(); action(); });
        else
            menu.AddDisabledItem(new GUIContent(label));
    }

    private void AddGameplayItem(ActionDocumentEntry lane, Type itemType, int frame)
    {
        lane = ResolveCurrentEntry(lane);
        if (!CanAuthor(out string message) || lane == null || !lane.HasStableSelection)
        {
            SetStatusNotice(string.IsNullOrEmpty(message) ? "The target Lane is not editable." : message);
            return;
        }
        GameplayItem item = ActionEditorCommands.AddItem(_document.Asset, lane.EditorId, itemType, frame, out message);
        SetStatusNotice(item != null ? $"Added {itemType.Name} at Frame {frame}." : message);
        if (item != null) RevealById(item.EditorId);
    }

    private void ShowLaneMenu(ActionDocumentEntry lane)
    {
        if (HasActivePointerGesture)
            return;
        FocusTimelineCommands();
        var menu = new GenericMenu();
        bool editable = CanAuthor(out _ ) && lane != null && lane.HasStableSelection && lane.Source is GameplayLane;
        if (editable)
        {
            menu.AddItem(new GUIContent("Rename"), false, () => StartRenameFromHeader(lane));
            menu.AddItem(new GUIContent("Move Up"), false, () => ReorderLane(lane, -1));
            menu.AddItem(new GUIContent("Move Down"), false, () => ReorderLane(lane, 1));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete"), false, () => DeleteEntries(new[] { lane.EditorId }));
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Authoring locked"));
        }
        if (lane != null && _densityClustersByLane.TryGetValue(lane.LaneIndex, out List<PointDensityCluster> hiddenClusters))
        {
            menu.AddSeparator(string.Empty);
            foreach (PointDensityCluster cluster in hiddenClusters)
            {
                foreach (ActionDocumentEntry entry in cluster.Entries.OrderBy(item => item.ItemIndex))
                    AddNavigationItem(menu, "Dense Points", entry);
            }
        }
        menu.ShowAsContext();
    }

    private void AddNavigationItem(GenericMenu menu, string group, ActionDocumentEntry entry)
    {
        if (entry == null)
            return;
        ActionDocumentEntry captured = entry;
        string time = entry.Source is PointGameplayItem
            ? $"Frame {entry.StartFrame}"
            : $"[{entry.StartFrame}, {entry.RawEndFrameExclusive})";
        string label = $"{group}/{entry.DisplayName} | {time} | {entry.AuthoringPath}";
        menu.AddItem(new GUIContent(label),
            entry.HasStableSelection && ActionEditorContext.Shared.IsSelected(entry.EditorId),
            () => NavigateToEntry(captured, true));
    }

    private void StartRenameFromHeader(ActionDocumentEntry lane)
    {
        lane = ResolveCurrentEntry(lane);
        if (lane == null || !_laneHeaderViews.TryGetValue(lane.DisplayKey, out VisualElement row))
            return;
        Label title = row.Q<Label>(className: "action-editor-lane-title");
        BeginInlineRename(lane, row, title);
    }

    private void BeginInlineRename(ActionDocumentEntry lane, VisualElement row, Label title)
    {
        if (!CanAuthor(out string message) || lane?.Source is not GameplayLane gameplayLane || _renameField != null)
        {
            if (!string.IsNullOrEmpty(message)) SetStatusNotice(message);
            return;
        }
        _interaction = InteractionState.InlineRename;
        _renamingLane = lane;
        title.style.display = DisplayStyle.None;
        _renameField = new TextField { value = gameplayLane.Name };
        _renameField.AddToClassList("action-editor-inline-rename");
        row.Insert(row.IndexOf(title), _renameField);
        _renameField.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                CommitInlineRename();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                CancelInlineRename();
                evt.StopPropagation();
            }
        });
        _renameField.RegisterCallback<BlurEvent>(_ => CommitInlineRename());
        _renameField.schedule.Execute(() => { _renameField?.Focus(); _renameField?.SelectAll(); });
    }

    private void CommitInlineRename()
    {
        if (_renameField == null)
            return;
        string value = _renameField.value;
        ActionDocumentEntry lane = _renamingLane;
        FinishInlineRenameVisual();
        bool committed = lane != null && ActionEditorCommands.RenameLane(_document?.Asset, lane.EditorId, value);
        SetStatusNotice(committed ? $"Renamed lane to '{value}'." : "Lane name was unchanged.");
    }

    private void CancelInlineRename()
    {
        if (_renameField == null)
            return;
        FinishInlineRenameVisual();
        SetStatusNotice("Rename cancelled.");
    }

    private void FinishInlineRenameVisual()
    {
        TextField field = _renameField;
        _renameField = null;
        _renamingLane = null;
        _interaction = InteractionState.Idle;
        ActionEditorInteractionGate.Release(this);
        VisualElement parent = field?.parent;
        field?.RemoveFromHierarchy();
        Label title = parent?.Q<Label>(className: "action-editor-lane-title");
        if (title != null) title.style.display = DisplayStyle.Flex;
        FocusTimelineCommands();
    }

    private void SetMuted(ActionDocumentEntry entry, bool muted)
    {
        entry = ResolveCurrentEntry(entry);
        if (!CanAuthor(out string message) || entry == null || !entry.HasStableSelection)
        {
            SetStatusNotice(string.IsNullOrEmpty(message) ? "The selected object is not editable." : message);
            return;
        }
        bool committed = ActionEditorCommands.SetMuted(_document.Asset, entry.EditorId, muted);
        SetStatusNotice(committed ? (muted ? "Muted content." : "Unmuted content.") : "Mute state was unchanged.");
    }

    private void ReorderLane(ActionDocumentEntry lane, int delta)
    {
        lane = ResolveCurrentEntry(lane);
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        bool committed = lane != null && ActionEditorCommands.ReorderLane(_document.Asset, lane.EditorId, delta);
        SetStatusNotice(committed ? "Reordered Gameplay Lane." : "Lane cannot move farther in that direction.");
    }

    private void SetStatusNotice(string message)
    {
        _statusNotice = message;
        RefreshStatus();
    }

    private void RevealPrimarySelection()
    {
        RevealById(ActionEditorContext.Shared.PrimarySelection.EditorId);
    }

    private void RevealById(string editorId)
    {
        if (string.IsNullOrEmpty(editorId))
            return;
        rootVisualElement.schedule.Execute(() =>
        {
            if (!HasActivePointerGesture && _document != null && _document.ById.TryGetValue(editorId, out ActionDocumentEntry entry))
            {
                if (entry.SelectionKind == ActionSelectionKind.GameplayLane)
                    ApplyVerticalScroll(Mathf.Max(0f, RowTop(entry) - 24f), true);
                else
                    FrameEntry(entry);
            }
        });
    }

    private void OnEntryPointerDown(PointerDownEvent evt, ActionDocumentEntry entry, VisualElement view)
    {
        if (HasActivePointerGesture)
        {
            evt.StopImmediatePropagation();
            return;
        }
        FocusTimelineCommands();
        if (entry?.Source is GameplayItem)
        {
            ActionDocumentEntry nearest = ResolveSemanticGameplayHit(entry, evt.position);
            if (nearest != null && _entryViews.TryGetValue(nearest.DisplayKey, out VisualElement nearestView))
            {
                entry = nearest;
                view = nearestView;
            }
        }
        if (evt.button == 1)
        {
            if (entry.HasStableSelection && !ActionEditorContext.Shared.IsSelected(entry.EditorId))
                ActionEditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, false, false);
            ShowEntryMenu(entry);
            evt.StopPropagation();
            return;
        }
        if (evt.button != 0 || evt.altKey)
            return;

        _transientDisplayKey = null;
        _statusNotice = null;
        if (!entry.HasStableSelection)
        {
            _transientDisplayKey = entry.DisplayKey;
            RefreshSelectionPresentation();
            evt.StopPropagation();
            return;
        }

        bool toggle = evt.ctrlKey || evt.commandKey;
        bool additive = evt.shiftKey || toggle;
        bool wasSelected = ActionEditorContext.Shared.IsSelected(entry.EditorId);
        if (!wasSelected || toggle || evt.shiftKey)
            ActionEditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, additive, toggle);

        if (evt.clickCount >= 2)
        {
            ActionDetailsWindow.OpenShared();
            evt.StopPropagation();
            return;
        }

        if (!toggle && ActionEditorContext.Shared.IsSelected(entry.EditorId))
            BeginPendingManipulation(evt, entry, view, wasSelected);
        evt.StopPropagation();
    }

    private void OnEntryPointerMove(PointerMoveEvent evt, ActionDocumentEntry entry, VisualElement view)
    {
        if (_interaction == InteractionState.Idle && entry != null)
        {
            bool selected = entry.HasStableSelection && ActionEditorContext.Shared.IsSelected(entry.EditorId);
            InteractionState state = ResolveManipulationState(entry, view, evt.position, selected);
            view.EnableInClassList("action-editor-resize-cursor", state != InteractionState.Move);
        }
        OnManipulationPointerMove(evt);
    }

    private void BeginPendingManipulation(PointerDownEvent evt, ActionDocumentEntry entry, VisualElement view, bool wasSelected)
    {
        _pendingEditorId = entry.EditorId;
        _pendingAction = _document.Asset;
        _pendingManipulationState = ResolveManipulationState(entry, view, evt.position, wasSelected);
        _pendingCollapseSelection = wasSelected && !evt.shiftKey && !evt.ctrlKey && !evt.commandKey &&
                                    ActionEditorContext.Shared.SelectedIds.Count > 1;
        _interaction = InteractionState.PendingManipulation;
        _capturedPointer = evt.pointerId;
        _captureTarget = view;
        _gestureStart = _contentCanvas.WorldToLocal(evt.position);
        _gestureCurrent = _gestureStart;
        _lastPointerWorld = evt.position;
        view.CapturePointer(evt.pointerId);
    }

    private static InteractionState ResolveManipulationState(
        ActionDocumentEntry entry, VisualElement view, Vector2 worldPosition, bool wasSelected)
    {
        VisualElement rangeBody = view.Q<VisualElement>(className: "action-editor-range-body");
        if (rangeBody == null)
            return InteractionState.Move;
        float width = rangeBody.layout.width;
        float localX = rangeBody.WorldToLocal(worldPosition).x;
        if (width < 16f && !wasSelected)
            return InteractionState.Move;
        const float edgeWidth = 6f;
        float leftDistance = Mathf.Abs(localX);
        float rightDistance = Mathf.Abs(localX - width);
        bool nearLeft = localX >= -edgeWidth && localX <= edgeWidth;
        bool nearRight = localX >= width - edgeWidth && localX <= width + edgeWidth;
        bool chooseLeft = nearLeft && (!nearRight || leftDistance <= rightDistance);
        bool chooseRight = nearRight && !chooseLeft;
        if (entry.Source is AnimationSegment animation && CanTrim(animation))
            return chooseLeft ? InteractionState.TrimLeft : chooseRight ? InteractionState.TrimRight : InteractionState.Move;
        if (entry.Source is RangeGameplayItem range && range.DurationFrames > 0)
            return chooseLeft ? InteractionState.ResizeLeft : chooseRight ? InteractionState.ResizeRight : InteractionState.Move;
        return InteractionState.Move;
    }

    private void ShowEntryMenu(ActionDocumentEntry entry)
    {
        if (HasActivePointerGesture)
            return;
        var menu = new GenericMenu();
        if (entry?.Source == null && _document?.Readiness != ActionEditorReadiness.IdentityBlocked)
        {
            ActionEditorDocument sourceDocument = _document;
            menu.AddItem(new GUIContent("Delete quarantined null entry"), false, () => DeleteNullEntry(sourceDocument, entry));
            menu.ShowAsContext();
            return;
        }
        bool editable = CanAuthor(out _) && entry != null && entry.HasStableSelection;
        if (editable && entry.Source is GameplayItem gameplayItem)
            menu.AddItem(new GUIContent(gameplayItem.Muted ? "Unmute" : "Mute"), false, () => SetMuted(entry, !gameplayItem.Muted));
        else if (entry.Source is GameplayItem)
            menu.AddDisabledItem(new GUIContent("Mute"));
        if (entry.Source is GameplayItem)
            menu.AddSeparator(string.Empty);
        if (editable)
        {
            menu.AddItem(new GUIContent("Copy"), false, CopySelection);
            menu.AddItem(new GUIContent("Duplicate"), false, DuplicateSelection);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete"), false, DeleteCurrentSelection);
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Authoring locked"));
        }
        menu.ShowAsContext();
    }

    private ActionDocumentEntry ResolveSemanticGameplayHit(ActionDocumentEntry fallback, Vector2 worldPosition)
    {
        if (_geometry == null || _contentCanvas == null || fallback == null)
            return fallback;
        Vector2 pointer = _contentCanvas.WorldToLocal(worldPosition);
        List<ActionDocumentEntry> candidates = _document.GameplayItems
            .Where(candidate => candidate.Source is GameplayItem && candidate.LaneIndex == fallback.LaneIndex &&
                                _entryPresentation.TryGetValue(candidate.DisplayKey, out EntryPresentation presentation) &&
                                presentation.Displayed)
            .ToList();
        var visualHits = candidates.Select(candidate =>
            IsInsideVisibleShape(_entryPresentation[candidate.DisplayKey], pointer)).ToList();
        var expandedHits = candidates.Select(candidate =>
            _entryPresentation[candidate.DisplayKey].HitRect.Contains(pointer)).ToList();
        int index = ActionTimelineInteractionMath.ChooseHitIndex(
            visualHits, expandedHits, candidates.Select(EntryLayerRank).ToList(),
            candidates.Select(candidate => candidate.ItemIndex).ToList());
        return index >= 0 ? candidates[index] : fallback;
    }

    private static bool IsInsideVisibleShape(EntryPresentation presentation, Vector2 pointer)
    {
        if (!presentation.IsPoint)
            return presentation.VisualRect.Contains(pointer);
        Vector2 delta = pointer - presentation.PointCenter;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= 7.1f;
    }

    private int EntryLayerRank(ActionDocumentEntry entry)
    {
        if (string.Equals(entry.DisplayKey, _hoverDisplayKey, StringComparison.Ordinal)) return 4;
        string primaryId = ActionEditorContext.Shared.PrimarySelection.EditorId;
        if (entry.HasStableSelection && string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal)) return 3;
        if (entry.HasStableSelection && ActionEditorContext.Shared.IsSelected(entry.EditorId)) return 2;
        return 1;
    }

    private void DeleteNullEntry(ActionEditorDocument sourceDocument, ActionDocumentEntry entry)
    {
        if (!ReferenceEquals(sourceDocument, ActionEditorContext.Shared.Document))
        {
            SetStatusNotice("The quarantine entry changed. Reopen its menu and try again.");
            return;
        }
        entry = ResolveCurrentEntry(entry);
        if (!CanAuthor(out string message) || entry == null || entry.Source != null)
        {
            SetStatusNotice(string.IsNullOrEmpty(message) ? "The quarantine entry is no longer valid." : message);
            return;
        }
        bool committed = ActionEditorCommands.DeleteNullEntry(
            _document.Asset, entry.SelectionKind, entry.LaneIndex, entry.ItemIndex, out message);
        SetStatusNotice(committed ? $"Deleted {entry.AuthoringPath}." : message);
    }

    private bool BeginManipulation(PointerMoveEvent evt)
    {
        string requestedEditorId = _pendingEditorId;
        ActionAsset requestedAction = _pendingAction;
        InteractionState next = _pendingManipulationState;
        VisualElement currentView = _captureTarget;
        ActionDocumentEntry entry = _document?.ContentEntries.FirstOrDefault(candidate =>
            candidate.HasStableSelection && string.Equals(candidate.EditorId, requestedEditorId, StringComparison.Ordinal));
        if (!CanAuthor(out string message) || entry == null || entry.Source == null)
        {
            if (!string.IsNullOrEmpty(message)) SetStatusNotice(message);
            ClearPendingManipulation();
            return false;
        }
        if (!ActionEditorInteractionGate.TryAcquire(this))
        {
            evt.StopImmediatePropagation();
            ClearPendingManipulation();
            return false;
        }

        bool started = false;
        int pointerId = evt.pointerId;
        ReleaseManipulationCapture();
        _interaction = InteractionState.Idle;
        try
        {
            // Property bindings write the asset before their deferred session notification. Resolve the
            // gesture only after those writes have produced the current published Document.
            ActionEditorContext context = ActionEditorContext.Shared;
            context.FlushQueuedBindingChange();
            if (context.CurrentAction != requestedAction)
            {
                SetStatusNotice("The edited Action changed before the Timeline operation could begin.");
                return false;
            }

            _document = context.Document;
            if (!_document.ById.TryGetValue(requestedEditorId, out entry) || entry.Source == null ||
                !_entryViews.TryGetValue(entry.DisplayKey, out currentView) ||
                !CanAuthor(out message))
            {
                SetStatusNotice(string.IsNullOrEmpty(message)
                    ? "The Timeline target changed before the operation could begin."
                    : message);
                return false;
            }

            if (next != InteractionState.Move)
                context.Select(entry.SelectionKind, entry.EditorId, false, false);

            _manipulationEntries.Clear();
            IEnumerable<ActionDocumentEntry> selected = next == InteractionState.Move
                ? _document.ContentEntries.Where(candidate => candidate.HasStableSelection && context.IsSelected(candidate.EditorId))
                : new[] { entry };
            foreach (ActionDocumentEntry selectedEntry in selected)
            {
                int duration = selectedEntry.Source is RangeGameplayItem selectedRange
                    ? selectedRange.DurationFrames
                    : selectedEntry.Source is AnimationSegment selectedAnimation
                        ? selectedAnimation.DerivedDurationFrames
                        : 1;
                _manipulationEntries.Add(new ManipulationEntry
                {
                    Entry = selectedEntry,
                    StartFrame = selectedEntry.StartFrame,
                    DurationFrames = duration,
                    LaneIndex = selectedEntry.LaneIndex,
                });
            }
            if (_manipulationEntries.Count == 0)
                return false;

            List<string> targetIds = _manipulationEntries.Select(item => item.Entry.EditorId).ToList();
            _operationSnapshot = ActionTimelineOperationSnapshot.Capture(
                _document, OperationKind(next), targetIds);
            if (_operationSnapshot == null)
                return false;

            _interaction = next;
            _capturedPointer = pointerId;
            _captureTarget = currentView;
            currentView.CapturePointer(pointerId);
            _manipulationPrimary = entry;
            _gestureCurrent = _contentCanvas.WorldToLocal(evt.position);
            _manipulationPixelsPerFrame = Math.Max(0.001d, _geometry.PixelsPerFrame);
            _previewFrameDelta = 0;
            _previewLaneDelta = 0;
            _operationInput = ActionTimelineOperationInput.Delta(0, 0);
            _operationResult = _operationSnapshot.Evaluate(_operationInput);
            _previewValid = _operationResult.State != ActionTimelineOperationState.Rejected;
            _previewMessage = _operationResult.Message;
            StopPlayback();
            BuildManipulationGhosts();
            started = true;
            ClearPendingStateOnly();
            return true;
        }
        finally
        {
            if (!started)
            {
                ReleaseManipulationCapture();
                _manipulationEntries.Clear();
                _manipulationPrimary = null;
                _operationSnapshot = null;
                _operationResult = null;
                _interaction = InteractionState.Idle;
                ActionEditorInteractionGate.Release(this);
                ClearPendingStateOnly();
            }
        }
    }

    private void OnManipulationPointerMove(PointerMoveEvent evt)
    {
        if (_interaction == InteractionState.PendingManipulation && evt.pointerId == _capturedPointer)
        {
            _lastPointerWorld = evt.position;
            _gestureCurrent = _contentCanvas.WorldToLocal(evt.position);
            if (!ActionTimelineInteractionMath.PassedDragThreshold(_gestureStart, _gestureCurrent))
            {
                evt.StopPropagation();
                return;
            }
            if (!BeginManipulation(evt))
                return;
        }
        if (!IsManipulating || evt.pointerId != _capturedPointer)
            return;
        _lastPointerWorld = evt.position;
        _gestureCurrent = _contentCanvas.WorldToLocal(evt.position);
        UpdateManipulationFromPointer(evt.ctrlKey || evt.commandKey);
        UpdateAutoPanRegistration();
        evt.StopPropagation();
    }

    private void OnManipulationPointerUp(PointerUpEvent evt)
    {
        if (_interaction == InteractionState.PendingManipulation && evt.pointerId == _capturedPointer)
        {
            string editorId = _pendingEditorId;
            bool collapse = _pendingCollapseSelection;
            ReleaseManipulationCapture();
            _interaction = InteractionState.Idle;
            ClearPendingStateOnly();
            if (collapse && _document != null && _document.ById.TryGetValue(editorId, out ActionDocumentEntry clicked))
                ActionEditorContext.Shared.Select(clicked.SelectionKind, clicked.EditorId, false, false);
            evt.StopPropagation();
            return;
        }
        if (!IsManipulating || evt.pointerId != _capturedPointer)
            return;
        ActionTimelineOperationSnapshot snapshot = _operationSnapshot;
        ActionTimelineOperationInput input = _operationInput;
        ActionTimelineOperationState state = _operationResult?.State ?? ActionTimelineOperationState.Rejected;
        string failure = _previewMessage;
        ReleaseManipulationCapture();
        ClearManipulation();
        if (state == ActionTimelineOperationState.Allowed &&
            !ActionEditorCommands.CommitTimelineOperation(snapshot, input, out string message) && !string.IsNullOrEmpty(message))
            ShowNotification(new GUIContent(message));
        else if (state == ActionTimelineOperationState.Rejected && !string.IsNullOrEmpty(failure))
            ShowNotification(new GUIContent(failure));
        evt.StopPropagation();
    }

    private bool IsManipulating => _interaction == InteractionState.Move ||
                                   _interaction == InteractionState.ResizeLeft ||
                                   _interaction == InteractionState.ResizeRight ||
                                   _interaction == InteractionState.TrimLeft ||
                                   _interaction == InteractionState.TrimRight;

    private int CalculateLaneDelta(float pointerY)
    {
        if (_interaction != InteractionState.Move || _manipulationPrimary == null)
            return 0;
        int target = pointerY < ActionEditorTheme.AnimationLaneHeight
            ? -1
            : Mathf.FloorToInt((pointerY - ActionEditorTheme.AnimationLaneHeight) / ActionEditorTheme.GameplayLaneHeight);
        return target - _manipulationPrimary.LaneIndex;
    }

    private void UpdateManipulationFromPointer(bool invertSnap)
    {
        if (!IsManipulating || _operationSnapshot == null)
            return;
        _lastSnapInverted = invertSnap;
        double deltaValue = (_gestureCurrent.x - _gestureStart.x) / _manipulationPixelsPerFrame;
        int rawDelta = deltaValue >= int.MaxValue ? int.MaxValue : deltaValue <= int.MinValue
            ? int.MinValue
            : (int)Math.Round(deltaValue, MidpointRounding.AwayFromZero);
        _previewLaneDelta = CalculateLaneDelta(_gestureCurrent.y);
        string boundaryReason = string.Empty;
        if (_interaction != InteractionState.Move)
            rawDelta = _operationSnapshot.ConstrainPointerDelta(rawDelta, out boundaryReason);

        _activeSnapFrame = null;
        bool snapping = _snapEnabled ^ invertSnap;
        if (snapping)
        {
            List<int> movingEdges = ManipulationEdges();
            List<int> targets = VisibleSnapTargets();
            if (ActionTimelineInteractionMath.TrySnap(rawDelta, movingEdges, targets,
                    _manipulationPixelsPerFrame, IsSnapDeltaAllowed, out ActionTimelineSnapResult snap))
            {
                rawDelta = snap.Delta;
                _activeSnapFrame = snap.TargetFrame;
            }
        }

        if (_interaction != InteractionState.Move)
        {
            rawDelta = _operationSnapshot.ConstrainPointerDelta(rawDelta, out string snappedBoundaryReason);
            if (!string.IsNullOrEmpty(snappedBoundaryReason))
                boundaryReason = snappedBoundaryReason;
        }
        _previewFrameDelta = rawDelta;
        EvaluateManipulation();
        if (!string.IsNullOrEmpty(boundaryReason) && _previewValid)
            _previewMessage = boundaryReason;
        UpdateManipulationGhosts();
        RefreshManipulationGuides();
    }

    private bool IsSnapDeltaAllowed(int delta)
    {
        if (_operationSnapshot == null)
            return false;
        if (_interaction != InteractionState.Move &&
            _operationSnapshot.ConstrainPointerDelta(delta, out _) != delta)
            return false;
        ActionTimelineOperationResult result = _operationSnapshot.Evaluate(
            ActionTimelineOperationInput.Delta(delta, _previewLaneDelta));
        return result.State != ActionTimelineOperationState.Rejected;
    }

    private List<int> ManipulationEdges()
    {
        if (_manipulationEntries.Count == 0)
            return new List<int>();
        if (_interaction == InteractionState.ResizeLeft || _interaction == InteractionState.TrimLeft)
            return new List<int> { _manipulationEntries[0].StartFrame };
        if (_interaction == InteractionState.ResizeRight || _interaction == InteractionState.TrimRight)
            return new List<int> { _manipulationEntries[0].StartFrame + _manipulationEntries[0].DurationFrames };
        int earliest = _manipulationEntries.Min(item => item.StartFrame);
        int latest = _manipulationEntries.Max(item => item.StartFrame + item.DurationFrames);
        return new List<int> { earliest, latest };
    }

    private List<int> VisibleSnapTargets()
    {
        var targets = new HashSet<int> { 0, ActionEditorContext.Shared.CurrentFrame };
        var moving = new HashSet<object>(_manipulationEntries.Select(item => item.Entry.Source));
        double visibleStart = _geometry?.VisibleStartFrame ?? 0d;
        double visibleEnd = _geometry?.VisibleEndFrame ?? double.MaxValue;
        foreach (ActionDocumentEntry entry in _document.ContentEntries)
        {
            if (entry.Source == null || moving.Contains(entry.Source) ||
                entry.IdentityState != ActionEditorIdentityState.Valid ||
                entry.DisplayState != ActionEntryDisplayState.Normal)
                continue;
            AddVisibleSnapFrame(targets, entry.StartFrame, visibleStart, visibleEnd);
            if (!(entry.Source is PointGameplayItem))
                AddVisibleSnapFrame(targets, entry.RawEndFrameExclusive, visibleStart, visibleEnd);
        }
        return targets.OrderBy(frame => frame).ToList();
    }

    private static void AddVisibleSnapFrame(HashSet<int> targets, long frame, double visibleStart, double visibleEnd)
    {
        if (frame >= int.MinValue && frame <= int.MaxValue && frame >= visibleStart && frame <= visibleEnd)
            targets.Add((int)frame);
    }

    private void EvaluateManipulation()
    {
        _operationInput = ActionTimelineOperationInput.Delta(_previewFrameDelta, _previewLaneDelta);
        _operationResult = _operationSnapshot?.Evaluate(_operationInput);
        _previewValid = _operationResult != null && _operationResult.State != ActionTimelineOperationState.Rejected;
        _previewMessage = _operationResult?.Message ?? "Timeline operation cannot be evaluated.";
    }

    private void BuildManipulationGhosts()
    {
        _ghostLayer.Clear();
        foreach (ManipulationEntry item in _manipulationEntries)
        {
            if (_entryViews.TryGetValue(item.Entry.DisplayKey, out VisualElement sourceView))
                sourceView.style.visibility = Visibility.Hidden;
            bool point = item.Entry.Source is PointGameplayItem;
            VisualElement ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            ghost.AddToClassList("action-editor-entry");
            if (point)
            {
                ghost.AddToClassList("action-editor-point-entry");
                ghost.AddToClassList("action-editor-point-ghost");
                var diamond = new VisualElement { pickingMode = PickingMode.Ignore };
                diamond.AddToClassList("action-editor-point-diamond");
                ghost.Add(diamond);
            }
            else
            {
                ghost.AddToClassList("action-editor-range-entry");
                var body = new VisualElement { pickingMode = PickingMode.Ignore };
                body.AddToClassList("action-editor-range-body");
                ghost.Add(body);
            }
            VisualElement contentHost = point ? ghost : ghost.Q<VisualElement>(className: "action-editor-range-body");
            var label = new Label(EntryLabel(item.Entry, false)) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("action-editor-entry-label");
            if (point)
                label.AddToClassList("action-editor-point-label");
            contentHost.Add(label);
            ghost.AddToClassList("action-editor-drag-ghost");
            ghost.AddToClassList(item.Entry.Source is AnimationSegment ? "action-editor-animation-entry" : ItemClass(item.Entry.Source));
            item.Ghost = ghost;
            _ghostLayer.Add(ghost);
        }
        UpdateManipulationGhosts();
    }

    private void UpdateManipulationGhosts()
    {
        foreach (ManipulationEntry item in _manipulationEntries)
        {
            long start = item.StartFrame;
            long duration = Math.Max(1, item.DurationFrames);
            int laneIndex = item.LaneIndex;
            ActionTimelineOperationCandidate candidate = _operationResult?.ForSource(item.Entry.Source);
            if (candidate != null)
            {
                start = candidate.StartFrame;
                duration = Math.Max(1, candidate.DurationFrames);
                laneIndex = candidate.LaneIndex;
            }
            else if (_interaction == InteractionState.Move)
            {
                start += _previewFrameDelta;
                laneIndex += _previewLaneDelta;
            }
            else if (_interaction == InteractionState.ResizeLeft || _interaction == InteractionState.TrimLeft)
            {
                start += _previewFrameDelta;
                duration = Math.Max(1, duration - _previewFrameDelta);
            }
            else if (_interaction == InteractionState.ResizeRight || _interaction == InteractionState.TrimRight)
            {
                duration = Math.Max(1, duration + _previewFrameDelta);
            }

            float left = _geometry.FrameToPixel(Math.Max(0L, start), out _);
            float right = _geometry.FrameToPixel(Math.Max(0L, start + duration), out _);
            bool point = item.Entry.Source is PointGameplayItem;
            float ghostWidth = point
                ? ActionTimelineGeometry.PointHitWidth
                : Mathf.Max(1f, right - left);
            float ghostLeft = point
                ? Mathf.Clamp(left - ghostWidth * 0.5f, 0f, Mathf.Max(0f, _geometry.ContentWidth - ghostWidth))
                : left;
            item.Ghost.style.left = ghostLeft;
            item.Ghost.style.top = _geometry.LaneTop(laneIndex) + 3f;
            item.Ghost.style.width = ghostWidth;
            item.Ghost.style.height = Mathf.Max(18f, (laneIndex < 0 ? ActionEditorTheme.AnimationLaneHeight : ActionEditorTheme.GameplayLaneHeight) - 6f);
            VisualElement rangeBody = item.Ghost.Q<VisualElement>(className: "action-editor-range-body");
            if (rangeBody != null)
            {
                rangeBody.style.left = 0f;
                rangeBody.style.width = ghostWidth;
            }
            if (point)
            {
                float localAnchor = Mathf.Clamp(left - ghostLeft, 0f, ghostWidth);
                VisualElement diamond = item.Ghost.Q<VisualElement>(className: "action-editor-point-diamond");
                if (diamond != null)
                    diamond.style.left = Mathf.Clamp(localAnchor - 5f, 0f, ghostWidth - 10f);
            }
            item.Ghost.EnableInClassList("action-editor-drag-invalid", !_previewValid);
        }
    }

    private void RefreshManipulationGuides()
    {
        if (_snapGuide != null)
        {
            bool showSnap = _activeSnapFrame.HasValue && _geometry != null;
            _snapGuide.style.display = showSnap ? DisplayStyle.Flex : DisplayStyle.None;
            if (showSnap)
                _snapGuide.style.left = _geometry.FrameToPixel(_activeSnapFrame.Value, out _);
        }
        if (_operationLabel == null || _manipulationEntries.Count == 0)
            return;
        ManipulationEntry primary = _manipulationEntries.FirstOrDefault(item =>
            ReferenceEquals(item.Entry.Source, _manipulationPrimary?.Source)) ?? _manipulationEntries[0];
        ActionTimelineOperationCandidate candidate = _operationResult?.ForSource(primary.Entry.Source);
        int start = candidate?.StartFrame ?? primary.StartFrame + _previewFrameDelta;
        int duration = candidate?.DurationFrames ?? Math.Max(1, primary.DurationFrames);
        int end = start + duration;
        _operationLabel.text = _previewValid
            ? _interaction == InteractionState.Move ? $"Start {start}  End {end}" :
              _interaction == InteractionState.ResizeLeft || _interaction == InteractionState.TrimLeft
                  ? $"Start {start}  Duration {duration}"
                  : $"End {end}  Duration {duration}"
            : _previewMessage;
        _operationLabel.EnableInClassList("action-editor-operation-invalid", !_previewValid);
        _operationLabel.style.left = Mathf.Clamp(
            _geometry.FrameToPixel(Math.Max(0, _interaction == InteractionState.ResizeLeft || _interaction == InteractionState.TrimLeft ? start : end), out _) + 8f,
            4f, Mathf.Max(4f, _geometry.ContentWidth - 190f));
        _operationLabel.style.top = _geometry.LaneTop(candidate?.LaneIndex ?? primary.LaneIndex) + 4f;
        _operationLabel.style.display = DisplayStyle.Flex;
    }

    private static bool CanTrim(AnimationSegment segment)
    {
        return segment != null && segment.AnimationAsset != null && segment.AnimationAsset.Clip != null &&
               ActionAuthoringMath.IsFinite(segment.SourceStartTime) && ActionAuthoringMath.IsFinite(segment.SourceEndTime) &&
               ActionAuthoringMath.IsFinite(segment.PlayRate) && segment.PlayRate > 0f &&
               segment.SourceStartTime >= 0f && segment.SourceEndTime > segment.SourceStartTime;
    }


    private void ReleaseManipulationCapture()
    {
        int pointerId = _capturedPointer;
        _capturedPointer = -1;
        if (_captureTarget != null && pointerId >= 0 && _captureTarget.HasPointerCapture(pointerId))
        {
            _releasingPointerCapture = true;
            _captureTarget.ReleasePointer(pointerId);
            _releasingPointerCapture = false;
        }
        _captureTarget = null;
    }

    private void ClearPendingManipulation()
    {
        ReleaseManipulationCapture();
        _interaction = InteractionState.Idle;
        ClearPendingStateOnly();
    }

    private void ClearPendingStateOnly()
    {
        _pendingEditorId = null;
        _pendingAction = null;
        _pendingManipulationState = InteractionState.Idle;
        _pendingCollapseSelection = false;
    }

    private void UpdateAutoPanRegistration()
    {
        if (!IsManipulating || _contentScroll?.contentViewport == null)
        {
            StopAutoPan();
            return;
        }
        Vector2 local = _contentScroll.contentViewport.WorldToLocal(_lastPointerWorld);
        float width = ResolvedSize(_contentScroll.contentViewport, true);
        float height = ResolvedSize(_contentScroll.contentViewport, false);
        bool horizontal = local.x < 0f || local.x > width;
        bool vertical = _interaction == InteractionState.Move && _manipulationPrimary?.LaneIndex >= 0 &&
                        (local.y < 0f || local.y > height);
        if (!horizontal && !vertical)
        {
            StopAutoPan();
            return;
        }
        if (_autoPanRegistered)
            return;
        _autoPanRegistered = true;
        _lastAutoPanTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnAutoPanUpdate;
    }

    private void OnAutoPanUpdate()
    {
        if (!IsManipulating || _contentScroll?.contentViewport == null || _geometry == null)
        {
            StopAutoPan();
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        float deltaTime = (float)Math.Max(0d, Math.Min(0.1d, now - _lastAutoPanTime));
        _lastAutoPanTime = now;
        Vector2 local = _contentScroll.contentViewport.WorldToLocal(_lastPointerWorld);
        float width = ResolvedSize(_contentScroll.contentViewport, true);
        float height = ResolvedSize(_contentScroll.contentViewport, false);
        float outsideX = local.x < 0f ? local.x : local.x > width ? local.x - width : 0f;
        float outsideY = local.y < 0f ? local.y : local.y > height ? local.y - height : 0f;
        float horizontalPixels = ActionTimelineInteractionMath.AutoPanSpeed(outsideX, 600f) * deltaTime;
        float verticalPixels = _interaction == InteractionState.Move && _manipulationPrimary?.LaneIndex >= 0
            ? ActionTimelineInteractionMath.AutoPanSpeed(outsideY, 300f) * deltaTime
            : 0f;
        if (Mathf.Approximately(horizontalPixels, 0f) && Mathf.Approximately(verticalPixels, 0f))
        {
            StopAutoPan();
            return;
        }

        if (!Mathf.Approximately(horizontalPixels, 0f))
        {
            double before = _timeViewport.VisibleStartFrame;
            _timeViewport.PanPixels(horizontalPixels);
            _timeViewport.ExtendNavigationForPan();
            CommitTimeViewport();
            float appliedPixels = (float)((_timeViewport.VisibleStartFrame - before) * _geometry.PixelsPerFrame);
            _gestureStart.x -= appliedPixels;
        }
        if (!Mathf.Approximately(verticalPixels, 0f))
            ApplyVerticalScroll(_contentScroll.scrollOffset.y + verticalPixels, true);
        _gestureCurrent = _contentCanvas.WorldToLocal(_lastPointerWorld);
        UpdateManipulationFromPointer(_lastSnapInverted);
        Repaint();
    }

    private void StopAutoPan()
    {
        if (!_autoPanRegistered)
            return;
        _autoPanRegistered = false;
        EditorApplication.update -= OnAutoPanUpdate;
    }

    private void ClearManipulation()
    {
        StopAutoPan();
        foreach (ManipulationEntry item in _manipulationEntries)
            if (_entryViews.TryGetValue(item.Entry.DisplayKey, out VisualElement sourceView))
                sourceView.style.visibility = Visibility.Visible;
        _ghostLayer?.Clear();
        _manipulationEntries.Clear();
        _manipulationPrimary = null;
        _previewFrameDelta = 0;
        _previewLaneDelta = 0;
        _previewValid = false;
        _previewMessage = string.Empty;
        _activeSnapFrame = null;
        _manipulationPixelsPerFrame = 0d;
        _operationSnapshot = null;
        _operationResult = null;
        _interaction = InteractionState.Idle;
        if (_snapGuide != null)
            _snapGuide.style.display = DisplayStyle.None;
        if (_operationLabel != null)
            _operationLabel.style.display = DisplayStyle.None;
        ActionEditorInteractionGate.Release(this);
        if (_geometry != null)
        {
            RefreshPointDensityPresentation(false);
        }
    }

    private static ActionTimelineOperationKind OperationKind(InteractionState state)
    {
        switch (state)
        {
            case InteractionState.ResizeLeft: return ActionTimelineOperationKind.ResizeLeft;
            case InteractionState.ResizeRight: return ActionTimelineOperationKind.ResizeRight;
            case InteractionState.TrimLeft: return ActionTimelineOperationKind.TrimLeft;
            case InteractionState.TrimRight: return ActionTimelineOperationKind.TrimRight;
            default: return ActionTimelineOperationKind.Move;
        }
    }

    private void CopySelection()
    {
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        bool copied = ActionEditorCommands.Copy(_document.Asset, ActionEditorContext.Shared.SelectedIds, out message);
        SetStatusNotice(copied ? "Copied Timeline content." : message);
    }

    private void PasteClipboard()
    {
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        bool committed = ActionEditorCommands.Paste(_document.Asset, ActionEditorContext.Shared.CurrentFrame, out message);
        SetStatusNotice(committed ? $"Pasted Timeline content at Frame {ActionEditorContext.Shared.CurrentFrame}." : message);
        if (committed) RevealPrimarySelection();
    }

    private void DuplicateSelection()
    {
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        bool committed = ActionEditorCommands.Duplicate(_document.Asset, ActionEditorContext.Shared.SelectedIds, out message);
        SetStatusNotice(committed ? "Duplicated Timeline content." : message);
        if (committed) RevealPrimarySelection();
    }

    private void DeleteCurrentSelection()
    {
        DeleteEntries(ActionEditorContext.Shared.SelectedIds);
    }

    private void DeleteEntries(IReadOnlyList<string> ids)
    {
        if (!CanAuthor(out string message) || ids == null || ids.Count == 0)
        {
            SetStatusNotice(string.IsNullOrEmpty(message) ? "Select content to delete." : message);
            return;
        }
        List<ActionDocumentEntry> entries = ids.Where(id => _document.ById.ContainsKey(id)).Select(id => _document.ById[id]).ToList();
        int containedItems = entries.Where(entry => entry.Source is GameplayLane)
            .Sum(entry => ((GameplayLane)entry.Source).Items.Count);
        if (containedItems > 0 && !EditorUtility.DisplayDialog(
                "Delete Gameplay Lane?",
                $"The selected Lane selection contains {containedItems} item{(containedItems == 1 ? string.Empty : "s")}. Delete the Lane and all of its items?",
                "Delete", "Cancel"))
        {
            SetStatusNotice("Delete cancelled.");
            return;
        }
        bool committed = ActionEditorCommands.DeleteSelection(_document.Asset, ids, true, out message);
        SetStatusNotice(committed ? "Deleted Timeline content." : message);
    }

    private void SelectEntry(PointerDownEvent evt, ActionDocumentEntry entry, bool focusCommands)
    {
        if (HasActivePointerGesture)
        {
            evt.StopImmediatePropagation();
            return;
        }
        if (evt.button != 0)
            return;
        if (focusCommands)
            FocusTimelineCommands();
        _transientDisplayKey = null;
        _statusNotice = null;
        if (entry.HasStableSelection)
        {
            bool toggle = evt.ctrlKey || evt.commandKey;
            bool additive = evt.shiftKey || toggle;
            ActionEditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, additive, toggle);
            if (evt.clickCount >= 2)
                ActionDetailsWindow.OpenShared();
        }
        else
        {
            _transientDisplayKey = entry.DisplayKey;
            RefreshSelectionPresentation();
        }
        evt.StopPropagation();
    }

    private void OnCanvasPointerDown(PointerDownEvent evt)
    {
        if (HasActivePointerGesture)
        {
            evt.StopImmediatePropagation();
            return;
        }
        if (evt.button == 1 && evt.target == _contentCanvas)
        {
            FocusTimelineCommands();
            ShowCanvasCreationMenu(evt.localPosition);
            evt.StopPropagation();
            return;
        }
        if (evt.button == 2 || (evt.button == 0 && evt.altKey))
        {
            FocusTimelineCommands();
            ResetWheelAnchor();
            if (!TryBeginPointerGesture(evt, InteractionState.Pan, _contentCanvas))
                return;
            _gestureStart = evt.position;
            _panStartScroll = _contentScroll.scrollOffset;
            _panStartVisibleFrame = _timeViewport.VisibleStartFrame;
            evt.StopPropagation();
            return;
        }
        if (evt.button != 0 || evt.target != _contentCanvas)
            return;

        FocusTimelineCommands();
        if (!TryBeginPointerGesture(evt, InteractionState.Marquee, _contentCanvas))
            return;
        _marqueeAdditive = evt.shiftKey;
        _gestureStart = evt.localPosition;
        _gestureCurrent = _gestureStart;
        _marquee.style.display = DisplayStyle.Flex;
        UpdateMarqueeVisual();
        evt.StopPropagation();
    }

    private void OnCanvasPointerMove(PointerMoveEvent evt)
    {
        if (evt.pointerId != _capturedPointer)
            return;
        if (_interaction == InteractionState.Pan)
        {
            Vector2 current = new Vector2(evt.position.x, evt.position.y);
            Vector2 delta = current - _gestureStart;
            SetVisibleRange(
                _panStartVisibleFrame - delta.x / Math.Max(0.001f, _geometry.PixelsPerFrame),
                _timeViewport.VisibleSpanFrames,
                true);
            ApplyVerticalScroll(Mathf.Max(0f, _panStartScroll.y - delta.y), true);
            evt.StopPropagation();
            return;
        }
        if (_interaction == InteractionState.Marquee)
        {
            _gestureCurrent = evt.localPosition;
            UpdateMarqueeVisual();
            evt.StopPropagation();
        }
    }

    private void OnCanvasPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != _capturedPointer)
            return;
        if (_contentCanvas.HasPointerCapture(evt.pointerId))
        {
            _releasingPointerCapture = true;
            _contentCanvas.ReleasePointer(evt.pointerId);
            _releasingPointerCapture = false;
        }
        _captureTarget = null;
        _capturedPointer = -1;
        if (_interaction == InteractionState.Pan)
        {
            _interaction = InteractionState.Idle;
            ActionEditorInteractionGate.Release(this);
            evt.StopPropagation();
            return;
        }
        if (_interaction != InteractionState.Marquee)
            return;

        _interaction = InteractionState.Idle;
        ActionEditorInteractionGate.Release(this);
        _marquee.style.display = DisplayStyle.None;
        Rect marqueeRect = Rect.MinMaxRect(
            Mathf.Min(_gestureStart.x, _gestureCurrent.x),
            Mathf.Min(_gestureStart.y, _gestureCurrent.y),
            Mathf.Max(_gestureStart.x, _gestureCurrent.x),
            Mathf.Max(_gestureStart.y, _gestureCurrent.y));
        if (marqueeRect.width < 3f && marqueeRect.height < 3f)
        {
            _statusNotice = null;
        }
        else
        {
            _statusNotice = null;
            List<ActionDocumentEntry> hits = _document.ContentEntries
                .Where(entry => entry.HasStableSelection && marqueeRect.Overlaps(SelectionRect(entry), true))
                .ToList();
            if (hits.Count > 0)
            {
                _transientDisplayKey = null;
                ActionEditorContext.Shared.SetSelection(hits, _marqueeAdditive);
            }
        }
        evt.StopPropagation();
    }

    private void UpdateMarqueeVisual()
    {
        _marquee.style.left = Mathf.Min(_gestureStart.x, _gestureCurrent.x);
        _marquee.style.top = Mathf.Min(_gestureStart.y, _gestureCurrent.y);
        _marquee.style.width = Mathf.Abs(_gestureCurrent.x - _gestureStart.x);
        _marquee.style.height = Mathf.Abs(_gestureCurrent.y - _gestureStart.y);
    }

    private void BeginScrub(PointerDownEvent evt)
    {
        if (evt.button != 0 || !TryBeginPointerGesture(evt, InteractionState.Scrub, _rulerCanvas))
            return;
        FocusTimelineCommands();
        ScrubAt(evt.localPosition.x);
        evt.StopPropagation();
    }

    private void ContinueScrub(PointerMoveEvent evt)
    {
        if (_interaction != InteractionState.Scrub || evt.pointerId != _capturedPointer)
            return;
        ScrubAt(evt.localPosition.x);
        evt.StopPropagation();
    }

    private void EndScrub(PointerUpEvent evt)
    {
        if (_interaction != InteractionState.Scrub || evt.pointerId != _capturedPointer)
            return;
        _interaction = InteractionState.Idle;
        ActionEditorInteractionGate.Release(this);
        if (_rulerCanvas.HasPointerCapture(evt.pointerId))
        {
            _releasingPointerCapture = true;
            _rulerCanvas.ReleasePointer(evt.pointerId);
            _releasingPointerCapture = false;
        }
        _captureTarget = null;
        _capturedPointer = -1;
        evt.StopPropagation();
    }

    private void ScrubAt(float localX)
    {
        ActionEditorContext.Shared.SetFrame(_geometry != null ? _geometry.PixelToFrame(localX) : 0);
    }

    private void OnTimelineWheel(WheelEvent evt)
    {
        if (_interaction != InteractionState.Idle)
        {
            evt.StopImmediatePropagation();
            return;
        }
        FocusTimelineCommands();
        CancelPendingScrollRestore();
        float deltaX = evt.delta.x;
        float deltaY = evt.delta.y;
        if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY))
        {
            ResetWheelAnchor();
            _timeViewport.PanPixels(deltaX * 50d);
            CommitTimeViewport();
            evt.StopImmediatePropagation();
            return;
        }

        if (Mathf.Approximately(deltaY, 0f))
        {
            evt.StopImmediatePropagation();
            return;
        }

        float viewportX = _contentScroll.contentViewport.WorldToLocal(evt.mousePosition).x;
        double now = EditorApplication.timeSinceStartup;
        bool retainAnchor = now - _lastWheelTime <= 0.22d &&
                            !float.IsNaN(_wheelAnchorX) &&
                            Mathf.Abs(viewportX - _wheelAnchorX) <= 1f;
        if (!retainAnchor)
        {
            _wheelAnchorX = viewportX;
            _wheelAnchorFrame = _timeViewport.PixelToFrame(viewportX);
        }
        _lastWheelTime = now;
        double usableWidth = Math.Max(1d, _timeViewport.UsableWidth);
        double ratio = (viewportX - ActionTimelineGeometry.HorizontalPresentationInset) / usableWidth;
        double factor = Math.Max(0.8d, Math.Min(1.25d, 1d + deltaY * 0.02d));
        _timeViewport.ZoomAround(_wheelAnchorFrame, ratio, factor);
        CommitTimeViewport();
        evt.StopImmediatePropagation();
    }

    private void OnHeaderWheel(WheelEvent evt)
    {
        if (_interaction != InteractionState.Idle)
        {
            evt.StopImmediatePropagation();
            return;
        }
        ApplyVerticalScroll(_contentScroll.scrollOffset.y + evt.delta.y * 20f, true);
        evt.StopImmediatePropagation();
    }

    private void ResetWheelAnchor()
    {
        _lastWheelTime = double.NegativeInfinity;
        _wheelAnchorX = float.NaN;
    }

    private void BeginHeaderResize(PointerDownEvent evt)
    {
        if (evt.button != 0 || !TryBeginPointerGesture(evt, InteractionState.HeaderResize, (VisualElement)evt.currentTarget)) return;
        FocusTimelineCommands();
        ResetWheelAnchor();
        _gestureStart = evt.position;
        _panStartScroll = new Vector2(_headerWidth, 0f);
        _headerResizeRootSize = new Vector2(rootVisualElement.resolvedStyle.width, rootVisualElement.resolvedStyle.height);
        evt.StopPropagation();
    }

    private void ContinueHeaderResize(PointerMoveEvent evt)
    {
        var handle = (VisualElement)evt.currentTarget;
        if (evt.pointerId != _capturedPointer || !handle.HasPointerCapture(evt.pointerId)) return;
        _headerWidth = Mathf.Clamp(_panStartScroll.x + evt.position.x - _gestureStart.x, ActionEditorTheme.MinHeaderWidth, ActionEditorTheme.MaxHeaderWidth);
        ApplyHeaderWidth();
        evt.StopPropagation();
    }

    private void EndHeaderResize(PointerUpEvent evt)
    {
        var handle = (VisualElement)evt.currentTarget;
        if (evt.pointerId != _capturedPointer || !handle.HasPointerCapture(evt.pointerId)) return;
        _releasingPointerCapture = true;
        handle.ReleasePointer(evt.pointerId);
        _releasingPointerCapture = false;
        _captureTarget = null;
        _capturedPointer = -1;
        _interaction = InteractionState.Idle;
        ActionEditorInteractionGate.Release(this);
        evt.StopPropagation();
    }

    private void ApplyHeaderWidth()
    {
        if (_corner != null) _corner.style.width = _headerWidth;
        if (_headerViewport != null) _headerViewport.style.width = _headerWidth;
        if (_navigatorCorner != null) _navigatorCorner.style.width = _headerWidth;
        if (_emptyOverlay != null) _emptyOverlay.style.left = _headerWidth;
    }

    private void BeginNavigatorInteraction(PointerDownEvent evt, InteractionState state)
    {
        if (evt.button != 0 || !TryBeginPointerGesture(evt, state, (VisualElement)evt.currentTarget))
            return;
        FocusTimelineCommands();
        ResetWheelAnchor();
        _gestureStart = evt.position;
        _navigatorStartFrame = _timeViewport.VisibleStartFrame;
        _navigatorStartSpan = _timeViewport.VisibleSpanFrames;
        _navigatorStartExtent = _timeViewport.NavigatorDomainEndFrame;
        _navigatorStartTrackWidth = Mathf.Max(1f, ResolvedSize(_timeNavigator, true));
        evt.StopImmediatePropagation();
    }

    private void ContinueNavigatorInteraction(PointerMoveEvent evt)
    {
        if (evt.pointerId != _capturedPointer || _captureTarget == null ||
            !_captureTarget.HasPointerCapture(evt.pointerId))
            return;
        double frameDelta = (evt.position.x - _gestureStart.x) /
                            Math.Max(1d, _navigatorStartTrackWidth) * _navigatorStartExtent;
        double minimumSpan = _timeViewport.MinimumVisibleSpan;
        switch (_interaction)
        {
            case InteractionState.TimeNavigatorPan:
            {
                ActionNavigationRange range = ActionTimelineInteractionMath.Pan(
                    _navigatorStartExtent, _navigatorStartFrame, _navigatorStartSpan, frameDelta);
                _timeViewport.SetRange(range.Start, range.Span);
                CommitTimeViewport();
                break;
            }
            case InteractionState.TimeNavigatorResizeLeft:
            {
                ActionNavigationRange range = ActionTimelineInteractionMath.ResizeLeft(
                    _navigatorStartExtent, _navigatorStartFrame, _navigatorStartSpan, minimumSpan, frameDelta);
                SetVisibleRange(range.Start, range.Span);
                break;
            }
            case InteractionState.TimeNavigatorResizeRight:
            {
                ActionNavigationRange range = ActionTimelineInteractionMath.ResizeRight(
                    _navigatorStartExtent, _navigatorStartFrame, _navigatorStartSpan, minimumSpan, frameDelta);
                SetVisibleRange(range.Start, range.Span);
                break;
            }
        }
        evt.StopImmediatePropagation();
    }

    private void EndNavigatorInteraction(PointerUpEvent evt)
    {
        if (evt.pointerId != _capturedPointer || _captureTarget == null)
            return;
        if (_captureTarget.HasPointerCapture(evt.pointerId))
        {
            _releasingPointerCapture = true;
            _captureTarget.ReleasePointer(evt.pointerId);
            _releasingPointerCapture = false;
        }
        _captureTarget = null;
        _capturedPointer = -1;
        _interaction = InteractionState.Idle;
        ActionEditorInteractionGate.Release(this);
        evt.StopImmediatePropagation();
    }

    private void RefreshTimeNavigator()
    {
        if (_timeNavigator == null || _timeNavigatorThumb == null)
            return;
        float trackWidth = ResolvedSize(_timeNavigator, true);
        if (trackWidth <= 1f)
            return;
        bool navigatorCaptured = _interaction == InteractionState.TimeNavigatorPan ||
                                 _interaction == InteractionState.TimeNavigatorResizeLeft ||
                                 _interaction == InteractionState.TimeNavigatorResizeRight;
        double navigationExtent = Math.Max(1d, navigatorCaptured ? _navigatorStartExtent : _timeViewport.NavigatorDomainEndFrame);
        float semanticLeft = (float)(_timeViewport.VisibleStartFrame / navigationExtent * trackWidth);
        float semanticWidth = (float)(_timeViewport.VisibleSpanFrames / navigationExtent * trackWidth);
        float visualWidth = Mathf.Min(trackWidth, Mathf.Max(18f, semanticWidth));
        float visualLeft = Mathf.Clamp(semanticLeft - (visualWidth - semanticWidth) * 0.5f, 0f, trackWidth - visualWidth);
        _timeNavigatorThumb.style.left = visualLeft;
        _timeNavigatorThumb.style.width = visualWidth;
        string fullRangeHint = _timeViewport.VisibleSpanFrames >= navigationExtent - 0.0001d
            ? "\nFull navigation range is visible. Pan or zoom the main view to explore farther."
            : string.Empty;
        _timeNavigatorThumb.tooltip =
            $"Visible [{_timeViewport.VisibleStartFrame:0.##}, {_timeViewport.VisibleEndFrame:0.##}) / Navigation {navigationExtent:0.##} / Workspace {_timeViewport.WorkspaceEndFrame:0.##}{fullRangeHint}";
    }

    private void SetVisibleRange(double startFrame, double spanFrames, bool extendNavigationForPan = false)
    {
        _timeViewport.SetRange(startFrame, spanFrames);
        if (extendNavigationForPan)
            _timeViewport.ExtendNavigationForPan();
        CommitTimeViewport();
    }

    private void CommitTimeViewport()
    {
        StoreTimeViewportState();
        RefreshGeometry();
    }

    private void StoreTimeViewportState()
    {
        _visibleStartFrame = _timeViewport.VisibleStartFrame;
        _visibleSpanFrames = _timeViewport.VisibleSpanFrames;
        _workspaceEndFrame = _timeViewport.WorkspaceEndFrame;
        _navigationEndFrame = _timeViewport.NavigationEndFrame;
        _hasTimeViewportState = true;
    }

    private float TimelineViewportWidth()
    {
        float width = ResolvedSize(_contentScroll?.contentViewport, true);
        if (float.IsNaN(width) || float.IsInfinity(width) || width < 32f)
            width = Mathf.Max(32f, position.width - _headerWidth - 20f);
        return width;
    }

    private static string CurrentActionGuid(ActionAsset asset)
    {
        if (asset == null)
            return string.Empty;
        string path = AssetDatabase.GetAssetPath(asset);
        string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        return string.IsNullOrEmpty(guid) ? GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString() : guid;
    }

    private void OnVerticalScroll(float value)
    {
        if (_syncingScroll)
            return;
        RememberVerticalScroll(value);
        if (_headerContent != null) _headerContent.style.top = -value;
    }

    private void ApplyVerticalScroll(float requested, bool viewCommand)
    {
        if (_contentScroll == null)
            return;
        if (viewCommand)
            CancelPendingScrollRestore();

        float viewportHeight = ResolvedSize(_contentScroll.contentViewport, false);
        float clamped = _geometry != null
            ? _geometry.ClampVerticalOffset(requested, viewportHeight)
            : 0f;

        _syncingScroll = true;
        _contentScroll.scrollOffset = new Vector2(0f, clamped);
        if (_headerContent != null) _headerContent.style.top = -clamped;
        _syncingScroll = false;
        RememberVerticalScroll(clamped);
        _gridCanvas?.MarkDirtyRepaint();
    }

    private void ClampCurrentVerticalScroll()
    {
        if (_contentScroll == null || _rebuildRestorePending)
            return;
        ApplyVerticalScroll(_contentScroll.scrollOffset.y, false);
    }

    private float CurrentPlayheadX()
    {
        if (_geometry == null)
            return 0f;
        float value = _geometry.FramePositionToPixel(ActionEditorContext.Shared.PreviewPosition, out _);
        return Mathf.Min(Mathf.Max(0f, _geometry.ContentWidth - 1f), value);
    }

    private void RememberVerticalScroll(float value)
    {
        _storedVerticalScroll = value;
        _hasStoredScroll = true;
    }

    private void CancelPendingScrollRestore()
    {
        _rebuildRestorePending = false;
        _restoreGeneration++;
    }

    private void CaptureWindowState()
    {
        if (_contentScroll != null)
            RememberVerticalScroll(_contentScroll.scrollOffset.y);
        StoreTimeViewportState();
    }

    private void DrawRuler()
    {
        if (_document == null || _geometry == null || Event.current.type != EventType.Repaint)
            return;
        float height = ActionEditorTheme.RulerHeight;
        ActionVisibleFrameRange visible = _geometry.VisibleFrames();
        float visibleLeft = 0f;
        float visibleRight = _geometry.ContentWidth;
        EditorGUI.DrawRect(new Rect(visibleLeft, height - 1f, Mathf.Max(1f, visibleRight - visibleLeft), 1f), ActionEditorTheme.LaneDivider);
        GetTickSteps(72f, out int major, out int minor);
        GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = ActionEditorTheme.RulerText },
        };
        long firstTick = ((long)visible.First + minor - 1L) / minor * minor;
        for (long frame = firstTick; frame < visible.LastExclusive; frame += minor)
        {
            float x = _geometry.FrameToPixel(frame, out bool overflow);
            if (overflow)
                break;
            bool isMajor = frame % major == 0;
            float tickHeight = isMajor ? 7f : 4f;
            EditorGUI.DrawRect(
                new Rect(x, height - tickHeight, 1f, tickHeight),
                isMajor ? ActionEditorTheme.RulerTick : ActionEditorTheme.MinorRulerTick);
            if (isMajor)
                GUI.Label(new Rect(x + 4f, 2f, 60f, 18f), frame.ToString(), style);
        }

        float durationX = _geometry.FrameToPixel(_document.DurationFrames, out bool durationOverflow);
        if (!durationOverflow && durationX >= visibleLeft - 1f && durationX <= visibleRight + 1f)
            EditorGUI.DrawRect(new Rect(durationX, 0f, 1f, height), new Color(0.35f, 0.72f, 0.92f, 0.8f));
        float horizonX = _geometry.FrameToPixel(_document.HorizonFrames, out bool horizonOverflow);
        if (!horizonOverflow && horizonX >= visibleLeft - 1f && horizonX <= visibleRight + 1f)
            EditorGUI.DrawRect(new Rect(horizonX, 0f, 1f, height), ActionEditorTheme.HorizonLine);
        if (visible.First == 0)
        {
            float frameZeroX = _geometry.FrameToPixel(0, out _);
            EditorGUI.DrawRect(new Rect(frameZeroX, 0f, 2f, height), ActionEditorTheme.OriginLine);
        }
    }

    private void DrawGrid()
    {
        if (_document == null || _geometry == null || Event.current.type != EventType.Repaint)
            return;
        float height = _geometry.ContentHeight;
        ActionVisibleFrameRange visible = _geometry.VisibleFrames();
        float visibleLeft = 0f;
        float visibleRight = _geometry.ContentWidth;
        GetTickSteps(72f, out int major, out int minor);
        long firstTick = ((long)visible.First + minor - 1L) / minor * minor;
        for (long frame = firstTick; frame < visible.LastExclusive; frame += minor)
        {
            bool isMajor = frame % major == 0;
            Color color = isMajor ? ActionEditorTheme.GridLine : ActionEditorTheme.MinorGridLine;
            float x = _geometry.FrameToPixel(frame, out bool overflow);
            if (overflow)
                break;
            EditorGUI.DrawRect(new Rect(x, 0f, 1f, height), color);
        }
        float horizontalWidth = Mathf.Max(1f, visibleRight - visibleLeft);
        float animationBottom = ActionEditorTheme.AnimationLaneHeight;
        float verticalOffset = _contentScroll?.scrollOffset.y ?? 0f;
        float viewportHeight = ResolvedSize(_contentScroll?.contentViewport, false);
        if (animationBottom >= verticalOffset - 1f && animationBottom <= verticalOffset + viewportHeight + 1f)
            EditorGUI.DrawRect(new Rect(visibleLeft, animationBottom - 1f, horizontalWidth, 1f), ActionEditorTheme.LaneDivider);
        int firstLane = Mathf.Clamp(
            Mathf.FloorToInt((verticalOffset - animationBottom) / ActionEditorTheme.GameplayLaneHeight),
            0,
            _document.Lanes.Count);
        int lastLane = Mathf.Clamp(
            Mathf.CeilToInt((verticalOffset + viewportHeight - animationBottom) / ActionEditorTheme.GameplayLaneHeight) + 1,
            firstLane,
            _document.Lanes.Count);
        for (int lane = firstLane; lane < lastLane; lane++)
        {
            float y = animationBottom + (lane + 1) * ActionEditorTheme.GameplayLaneHeight;
            EditorGUI.DrawRect(new Rect(visibleLeft, y - 1f, Mathf.Max(1f, visibleRight - visibleLeft), 1f), ActionEditorTheme.LaneDivider);
        }
        if (visible.First == 0)
        {
            float frameZeroX = _geometry.FrameToPixel(0, out _);
            EditorGUI.DrawRect(new Rect(frameZeroX, 0f, 2f, height), ActionEditorTheme.OriginLine);
        }
    }

    private void GetTickSteps(float targetPixels, out int major, out int minor)
    {
        double targetFrames = Math.Max(1d, targetPixels / Math.Max(0.000001f, _geometry.PixelsPerFrame));
        double power = Math.Pow(10d, Math.Floor(Math.Log10(targetFrames)));
        double normalized = targetFrames / power;
        double step = normalized <= 1d ? 1d : normalized <= 2d ? 2d : normalized <= 5d ? 5d : 10d;
        double chosen = step * power;
        major = chosen >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)Math.Ceiling(chosen));
        minor = Mathf.Max(1, major / 5);
    }

    private void FitAll()
    {
        if (_document == null || HasActivePointerGesture)
            return;
        CancelPendingScrollRestore();
        ResetWheelAnchor();
        _timeViewport.Fit(_document.HorizonFrames);
        CommitTimeViewport();
    }

    private void FrameSelectionOrAll()
    {
        if (HasActivePointerGesture)
            return;
        List<ActionDocumentEntry> selected = ActionEditorContext.Shared.SelectedIds
            .Where(id => _document.ById.TryGetValue(id, out _))
            .Select(id => _document.ById[id])
            .Where(entry => entry.SelectionKind == ActionSelectionKind.AnimationSegment || entry.SelectionKind == ActionSelectionKind.GameplayItem)
            .ToList();
        if (selected.Count == 0)
        {
            FitAll();
            return;
        }
        double start = Math.Max(0d, selected.Min(entry => (double)entry.StartFrame));
        double end = Math.Max(start, selected.Max(entry => (double)entry.EndFrame));
        CancelPendingScrollRestore();
        ResetWheelAnchor();
        if (end - start <= 1d && selected.All(entry => entry.Source is PointGameplayItem))
        {
            _timeViewport.SetRange(start - _timeViewport.VisibleSpanFrames * 0.5d, _timeViewport.VisibleSpanFrames);
        }
        else
        {
            double semanticSpan = Math.Max(1d, end - start);
            double padding = semanticSpan * 0.1d;
            _timeViewport.SetRange(start - padding, semanticSpan + padding * 2d);
        }
        CommitTimeViewport();
    }

    private void FrameEntry(ActionDocumentEntry entry)
    {
        if (entry == null) return;
        ResetWheelAnchor();
        double start = Math.Max(0d, entry.StartFrame);
        double end = Math.Max(start + 1d, entry.RawEndFrameExclusive);
        if (start < _timeViewport.VisibleStartFrame || end > _timeViewport.VisibleEndFrame)
            SetVisibleRange(start - _timeViewport.VisibleSpanFrames * 0.35d, _timeViewport.VisibleSpanFrames);
        ApplyVerticalScroll(Mathf.Max(0f, RowTop(entry) - 24f), true);
    }

    private float RowTop(ActionDocumentEntry entry) => _geometry?.LaneTop(entry?.LaneIndex ?? -1) ?? 0f;

    private void TogglePlayback()
    {
        if (HasActivePointerGesture)
            return;
        ActionEditorPlayback.Toggle();
    }

    private void RunTimelineNavigation(Action action)
    {
        if (!HasActivePointerGesture)
            action?.Invoke();
    }

    private void StartPlayback()
    {
        ActionEditorPlayback.Play();
    }

    private void StopPlayback()
    {
        ActionEditorPlayback.Stop();
    }

    private void OnPlaybackStateChanged()
    {
        if (_playButtonIcon != null)
            _playButtonIcon.image = EditorGUIUtility.IconContent(
                ActionEditorPlayback.IsPlaying ? "PauseButton" : "Animation.Play").image;
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (IsNativeInputFocus(evt.target as VisualElement))
            return;
        if (HasActivePointerGesture)
        {
            if (evt.keyCode == KeyCode.Escape)
                CancelInteraction();
            evt.StopPropagation();
            return;
        }
        if (evt.ctrlKey || evt.commandKey)
        {
            switch (evt.keyCode)
            {
                case KeyCode.A: SelectAllStableContent(); evt.StopPropagation(); return;
                case KeyCode.C: CopySelection(); evt.StopPropagation(); return;
                case KeyCode.V: PasteClipboard(); evt.StopPropagation(); return;
                case KeyCode.D: DuplicateSelection(); evt.StopPropagation(); return;
            }
        }
        switch (evt.keyCode)
        {
            case KeyCode.Delete:
            case KeyCode.Backspace: DeleteCurrentSelection(); evt.StopPropagation(); break;
            case KeyCode.Space: TogglePlayback(); evt.StopPropagation(); break;
            case KeyCode.F: FrameSelectionOrAll(); evt.StopPropagation(); break;
            case KeyCode.LeftArrow: ActionEditorContext.Shared.SetFrame(ActionEditorContext.Shared.CurrentFrame - 1); evt.StopPropagation(); break;
            case KeyCode.RightArrow: ActionEditorContext.Shared.SetFrame(ActionEditorContext.Shared.CurrentFrame + 1); evt.StopPropagation(); break;
            case KeyCode.Escape:
                if (CancelInteraction()) evt.StopPropagation();
                break;
        }
    }

    private void OnContextChanged(ActionEditorChange change)
    {
        ActionEditorChangeFlags flags = change.Flags;
        if ((flags & ActionEditorChangeFlags.Playback) != 0)
            OnPlaybackStateChanged();
        if (!_uiReady)
            return;
        if (_refreshingViews)
        {
            _pendingContextChanges |= flags;
            return;
        }

        if ((flags & (ActionEditorChangeFlags.Context | ActionEditorChangeFlags.Structure |
                      ActionEditorChangeFlags.Timing)) != 0)
        {
            bool cancelledGesture = _interaction == InteractionState.PendingManipulation || IsManipulating;
            CancelInteraction();
            if (cancelledGesture)
                ShowNotification(new GUIContent("Timeline data changed. The active operation was cancelled."));
            if ((flags & ActionEditorChangeFlags.Context) != 0)
                CancelAnimationPicker();
            StopPlayback();
            RefreshDocumentAndViews();
            return;
        }
        if ((flags & (ActionEditorChangeFlags.Content |
                      ActionEditorChangeFlags.Presentation)) != 0)
            RefreshContentOnly();
        if ((flags & (ActionEditorChangeFlags.Frame | ActionEditorChangeFlags.Preview)) != 0)
            RefreshFramePresentation();
        if ((flags & ActionEditorChangeFlags.Selection) != 0) RefreshSelectionPresentation();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            CancelInteraction();
    }

    private void FlushPendingContextChanges()
    {
        if (!_uiReady || _refreshingViews || _pendingContextChanges == ActionEditorChangeFlags.None)
            return;

        ActionEditorChangeFlags pending = _pendingContextChanges;
        _pendingContextChanges = ActionEditorChangeFlags.None;
        OnContextChanged(new ActionEditorChange(
            ActionEditorContext.Shared.CurrentAction,
            pending,
            ActionEditorContext.Shared.DocumentVersion,
            ActionEditorChangeOrigin.Session));
    }

    private void OnRootGeometryChanged(GeometryChangedEvent evt)
    {
        if (_interaction == InteractionState.HeaderResize &&
            !Approximately(evt.newRect.size, _headerResizeRootSize))
            CancelInteraction();
        rootVisualElement.EnableInClassList("action-editor-compact", evt.newRect.width < 520f);
    }

    private void OnViewportGeometryChanged(GeometryChangedEvent evt)
    {
        Vector2 size = evt.newRect.size;
        if (Approximately(size, _lastViewportSize))
            return;
        _lastViewportSize = size;
        if (_interaction != InteractionState.Idle && _interaction != InteractionState.HeaderResize)
            CancelInteraction();
        int generation = ++_viewportGeometryGeneration;
        rootVisualElement.schedule.Execute(() =>
        {
            if (generation != _viewportGeometryGeneration || _document == null)
                return;
            RefreshGeometry();
        });
    }

    private void OnLostFocus()
    {
        SetHoveredEntry(null);
        CancelInteraction();
    }

    private void SetHoveredEntry(string displayKey)
    {
        if (string.Equals(_hoverDisplayKey, displayKey, StringComparison.Ordinal))
            return;
        if (!string.IsNullOrEmpty(_hoverDisplayKey) && _entryViews.TryGetValue(_hoverDisplayKey, out VisualElement previous))
            previous.EnableInClassList("action-editor-entry-hover", false);
        _hoverDisplayKey = displayKey;
        if (!string.IsNullOrEmpty(_hoverDisplayKey) && _entryViews.TryGetValue(_hoverDisplayKey, out VisualElement current))
            current.EnableInClassList("action-editor-entry-hover", true);
        RefreshPointDensityPresentation(false);
        RefreshEntryLayering();
        FinalizeLayerOrder();
    }

    private void ClearHoveredEntry(string displayKey)
    {
        if (string.Equals(_hoverDisplayKey, displayKey, StringComparison.Ordinal))
            SetHoveredEntry(null);
    }

    private void RefreshEntryLayering()
    {
        if (_document == null || _entryLayer == null)
            return;

        foreach (ActionDocumentEntry entry in _document.ContentEntries)
            if (_entryViews.TryGetValue(entry.DisplayKey, out VisualElement view)) view.BringToFront();

        string primaryId = ActionEditorContext.Shared.PrimarySelection.EditorId;
        foreach (ActionDocumentEntry entry in _document.ContentEntries)
        {
            if (!entry.HasStableSelection || !ActionEditorContext.Shared.IsSelected(entry.EditorId) ||
                string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal))
                continue;
            if (_entryViews.TryGetValue(entry.DisplayKey, out VisualElement selected)) selected.BringToFront();
        }

        ActionDocumentEntry primaryEntry = _document.ContentEntries.FirstOrDefault(entry =>
            entry.HasStableSelection && string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal));
        if (primaryEntry != null && _entryViews.TryGetValue(primaryEntry.DisplayKey, out VisualElement primary))
            primary.BringToFront();
        if (!string.IsNullOrEmpty(_transientDisplayKey) && _entryViews.TryGetValue(_transientDisplayKey, out VisualElement transient))
            transient.BringToFront();
        if (!string.IsNullOrEmpty(_hoverDisplayKey) && _entryViews.TryGetValue(_hoverDisplayKey, out VisualElement hover))
            hover.BringToFront();
    }

    private void SelectAllStableContent()
    {
        if (_document?.Asset == null)
            return;
        _transientDisplayKey = null;
        _statusNotice = null;
        ActionEditorContext.Shared.SetSelection(
            _document.ContentEntries.Where(entry => entry.HasStableSelection),
            false);
    }

    private Rect SelectionRect(ActionDocumentEntry entry)
    {
        if (entry == null || !_entryViews.TryGetValue(entry.DisplayKey, out VisualElement view) || _geometry == null)
            return default;
        if (entry.DisplayState != ActionEntryDisplayState.Normal)
            return view.layout;

        float left = _geometry.FrameToPixel(Math.Max(0L, entry.StartFrame), out bool startOverflow);
        float right = _geometry.FrameToPixel(Math.Max(0L, entry.RawEndFrameExclusive), out bool endOverflow);
        if (startOverflow || endOverflow)
            return view.layout;
        float height = entry.LaneIndex < 0
            ? ActionEditorTheme.AnimationLaneHeight
            : ActionEditorTheme.GameplayLaneHeight;
        return new Rect(left, _geometry.LaneTop(entry.LaneIndex), Mathf.Max(0.5f, right - left), height);
    }

    private void PositionPointDiamond(VisualElement view, ActionDocumentEntry entry, ActionEntryGeometry layout)
    {
        float anchor = _geometry.FrameToPixel(Math.Max(0L, entry.StartFrame), out bool overflow);
        float localAnchor = overflow
            ? layout.Width * 0.5f
            : Mathf.Clamp(anchor - layout.Left, 0f, layout.Width);
        VisualElement diamond = view.Q<VisualElement>(className: "action-editor-point-diamond");
        if (diamond != null)
            diamond.style.left = Mathf.Clamp(localAnchor - 5f, 0f, Mathf.Max(0f, layout.Width - 10f));
    }

    private bool CancelInteraction()
    {
        if (_interaction == InteractionState.InlineRename)
        {
            CancelInlineRename();
            return true;
        }

        bool manipulating = IsManipulating;
        bool navigating = _interaction == InteractionState.TimeNavigatorPan ||
                          _interaction == InteractionState.TimeNavigatorResizeLeft ||
                          _interaction == InteractionState.TimeNavigatorResizeRight;
        bool active = manipulating || navigating || HasActivePointerGesture;
        if (!active)
            return false;

        if (manipulating || _interaction == InteractionState.PendingManipulation)
            ReleaseManipulationCapture();

        int pointerId = _capturedPointer;
        _capturedPointer = -1;
        _releasingPointerCapture = true;
        if (pointerId >= 0 && _rulerCanvas != null && _rulerCanvas.HasPointerCapture(pointerId))
            _rulerCanvas.ReleasePointer(pointerId);
        if (pointerId >= 0 && _contentCanvas != null && _contentCanvas.HasPointerCapture(pointerId))
            _contentCanvas.ReleasePointer(pointerId);
        if (pointerId >= 0 && _headerResizeHandle != null && _headerResizeHandle.HasPointerCapture(pointerId))
            _headerResizeHandle.ReleasePointer(pointerId);
        if (pointerId >= 0 && _captureTarget != null && _captureTarget.HasPointerCapture(pointerId))
            _captureTarget.ReleasePointer(pointerId);
        _releasingPointerCapture = false;
        _captureTarget = null;
        _interaction = InteractionState.Idle;
        ClearPendingStateOnly();
        ActionEditorInteractionGate.Release(this);
        ClearManipulation();
        if (_marquee != null)
            _marquee.style.display = DisplayStyle.None;
        return true;
    }

    private void FocusTimelineCommands()
    {
        if (rootVisualElement != null && rootVisualElement.panel != null)
            rootVisualElement.Focus();
    }

    private static VisualElement CreateContentLayer(string className, PickingMode pickingMode)
    {
        var layer = new VisualElement { pickingMode = pickingMode };
        layer.AddToClassList("action-editor-content-layer");
        layer.AddToClassList(className);
        return layer;
    }

    private static void SetLayerSize(VisualElement layer, float width, float height)
    {
        if (layer == null)
            return;
        layer.style.width = width;
        layer.style.height = height;
    }

    private static Button TransportButton(string iconName, string tooltip, Action clicked)
    {
        return TransportButton(iconName, tooltip, clicked, out _);
    }

    private static Button TransportButton(string iconName, string tooltip, Action clicked, out Image icon)
    {
        var button = new ToolbarButton(clicked) { tooltip = tooltip };
        button.AddToClassList("action-editor-transport-button");
        icon = new Image
        {
            image = EditorGUIUtility.IconContent(iconName).image,
            pickingMode = PickingMode.Ignore,
            scaleMode = ScaleMode.ScaleToFit,
        };
        icon.AddToClassList("action-editor-toolbar-icon");
        button.Add(icon);
        return button;
    }

    private static Button LaneCommandButton(string text, string tooltip, Action clicked)
    {
        var button = new Button(clicked) { tooltip = tooltip, focusable = false };
        button.AddToClassList("action-editor-lane-command");
        string iconName = text == "+" ? "CreateAddNew" :
            text == "×" ? "TreeEditor.Trash" : "_Popup";
        if (text == "⋮")
            button.text = "⋮";
        else
            SetButtonIcon(button, iconName);
        button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        return button;
    }

    private static void SetButtonIcon(Button button, string iconName)
    {
        if (button == null)
            return;
        button.Clear();
        var icon = new Image
        {
            image = EditorGUIUtility.IconContent(iconName).image,
            pickingMode = PickingMode.Ignore,
            scaleMode = ScaleMode.ScaleToFit,
        };
        icon.AddToClassList("action-editor-toolbar-icon");
        button.Add(icon);
    }

    private static string ItemClass(object source)
    {
        if (source is HitBoxItem) return "action-editor-item-hitbox";
        if (source is RootMotionItem) return "action-editor-item-root-motion";
        if (source is SelfRotationItem) return "action-editor-item-self-rotation";
        if (source is VelocityOverrideItem) return "action-editor-item-velocity";
        if (source is MotionPolicyItem) return "action-editor-item-motion-policy";
        if (source is TagItem) return "action-editor-item-tag";
        if (source is ImpulseItem) return "action-editor-item-impulse";
        return "action-editor-item-unknown";
    }

    private static void ApplyPlaceholderClasses(VisualElement view, ActionEntryDisplayState state)
    {
        bool placeholder = state != ActionEntryDisplayState.Normal;
        view.EnableInClassList("action-editor-placeholder", placeholder);
        view.EnableInClassList("action-editor-placeholder-null", (state & ActionEntryDisplayState.NullEntry) != 0);
        view.EnableInClassList("action-editor-placeholder-negative", (state & ActionEntryDisplayState.NegativeStart) != 0);
        view.EnableInClassList("action-editor-placeholder-duration", (state & ActionEntryDisplayState.InvalidDuration) != 0);
        view.EnableInClassList("action-editor-placeholder-source", (state & ActionEntryDisplayState.MissingSource) != 0);
    }

    private static string EntryLabel(ActionDocumentEntry entry, bool geometryOverflow)
    {
        if (entry == null)
            return "Null entry";
        if ((entry.DisplayState & ActionEntryDisplayState.NullEntry) != 0)
        {
            switch (entry.SelectionKind)
            {
                case ActionSelectionKind.AnimationSegment: return $"Null AnimationSegment #{entry.ItemIndex}";
                case ActionSelectionKind.GameplayLane: return $"Null GameplayLane #{entry.LaneIndex}";
                default: return $"Null GameplayItem #{entry.ItemIndex}";
            }
        }

        string value = entry.DisplayName;
        if ((entry.DisplayState & ActionEntryDisplayState.MissingSource) != 0)
            value = "Missing Animation Asset";
        else if ((entry.DisplayState & ActionEntryDisplayState.InvalidDuration) != 0)
            value = $"Invalid duration · {value}";

        if ((entry.DisplayState & ActionEntryDisplayState.NegativeStart) != 0)
            value = $"← {entry.StartFrame} · {value}";
        if (geometryOverflow)
            value = $"→ Frame {entry.StartFrame} · {value}";
        return value;
    }

    private static string EntryTooltip(ActionDocumentEntry entry, bool geometryOverflow)
    {
        ActionEntryDisplayState displayState = entry.DisplayState |
            (geometryOverflow ? ActionEntryDisplayState.GeometryOverflow : ActionEntryDisplayState.Normal);
        return $"{entry.DisplayName}\n{entry.AuthoringPath}\nRaw [{entry.StartFrame}, {entry.RawEndFrameExclusive})\n" +
               $"Display: {DisplayStateText(displayState)}\n{IdentityText(entry.IdentityState)}";
    }

    private static string DisplayStateText(ActionEntryDisplayState state)
    {
        if (state == ActionEntryDisplayState.Normal)
            return "Normal";
        var labels = new List<string>();
        foreach (ActionEntryDisplayState value in Enum.GetValues(typeof(ActionEntryDisplayState)))
        {
            if (value != ActionEntryDisplayState.Normal && (state & value) != 0)
                labels.Add(value.ToString());
        }
        return string.Join(", ", labels);
    }

    private static string IdentityText(ActionEditorIdentityState state) => state == ActionEditorIdentityState.Valid
        ? "Stable Editor ID"
        : $"Identity: {state}";


    private static float ResolvedSize(VisualElement element, bool width)
    {
        if (element == null)
            return 0f;
        float value = width ? element.resolvedStyle.width : element.resolvedStyle.height;
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        if (float.IsNaN(left.x) || float.IsNaN(left.y) || float.IsNaN(right.x) || float.IsNaN(right.y))
            return false;
        return Mathf.Abs(left.x - right.x) < 0.5f && Mathf.Abs(left.y - right.y) < 0.5f;
    }

    private bool IsNativeInputFocus(VisualElement eventTarget)
    {
        VisualElement focused = rootVisualElement?.panel?.focusController?.focusedElement as VisualElement;
        VisualElement current = focused ?? eventTarget;
        while (current != null)
        {
            if (current is TextField || current is IntegerField || current is FloatField ||
                current is ObjectField || current is CurveField || current.ClassListContains("unity-base-field"))
                return true;
            current = current.parent;
        }
        return false;
    }
}
#endif
