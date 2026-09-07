#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Stage 5R fixed-row Action V1 Timeline. Presentation, pointer gestures, and authoring commands
/// are intentionally separated so Ghost feedback and the committed operation share the same rules.
/// </summary>
public sealed class ActionV1TimelineWindow : EditorWindow
{
    private enum InteractionState
    {
        Idle,
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
        internal ActionV1DocumentEntry Entry;
        internal int StartFrame;
        internal int DurationFrames;
        internal int LaneIndex;
        internal string LaneId;
        internal AnimationAsset AnimationAsset;
        internal float SourceStartTime;
        internal float SourceEndTime;
        internal float PlayRate;
        internal VisualElement Ghost;
    }

    private sealed class PointDensityCluster
    {
        internal int LaneIndex;
        internal readonly List<ActionV1DocumentEntry> Entries = new List<ActionV1DocumentEntry>();
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
    private readonly Dictionary<string, ActionV1DocumentEntry> _entriesByDisplayKey = new Dictionary<string, ActionV1DocumentEntry>();
    private readonly Dictionary<string, EntryPresentation> _entryPresentation = new Dictionary<string, EntryPresentation>();
    private readonly Dictionary<string, Button> _pointDensityMarkers = new Dictionary<string, Button>();
    private readonly Dictionary<int, List<PointDensityCluster>> _densityClustersByLane = new Dictionary<int, List<PointDensityCluster>>();
    private readonly Dictionary<int, List<PointDensityCluster>> _hiddenDensityClustersByLane = new Dictionary<int, List<PointDensityCluster>>();
    private readonly Dictionary<int, List<Rect>> _reservedMarkerRectsByLane = new Dictionary<int, List<Rect>>();
    private ActionV1EditorDocument _document;
    private ActionV1TimelineGeometry _geometry;
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
    private VisualElement _overlapLayer;
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
    private Foldout _issuesFoldout;
    private ScrollView _issuesList;
    private Label _frameLabel;
    private Label _statusLabel;
    private Label _readinessPill;
    private Button _playButton;
    [SerializeField] private float _headerWidth = ActionV1EditorTheme.DefaultHeaderWidth;
    [SerializeField] private double _visibleStartFrame;
    [SerializeField] private double _visibleSpanFrames = 60d;
    [SerializeField] private double _workspaceEndFrame = 120d;
    [SerializeField] private double _navigationEndFrame = 60d;
    [SerializeField] private string _viewActionGuid;
    [SerializeField] private float _storedVerticalScroll;
    [SerializeField] private bool _hasStoredScroll;
    [SerializeField] private bool _hasTimeViewportState;
    [SerializeField] private bool _issuesExpanded;
    private readonly ActionV1TimeViewport _timeViewport = new ActionV1TimeViewport();
    private bool _playing;
    private double _lastPlaybackTime;
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
    private bool _scrubbing;
    private bool _panning;
    private bool _marqueeSelecting;
    private bool _marqueeAdditive;
    private bool _resizingHeader;
    private InteractionState _interaction;
    private VisualElement _captureTarget;
    private readonly List<ManipulationEntry> _manipulationEntries = new List<ManipulationEntry>();
    private ActionV1DocumentEntry _manipulationPrimary;
    private ActionV1TimelineOperationSnapshot _operationSnapshot;
    private ActionV1TimelineOperationResult _operationResult;
    private int _previewFrameDelta;
    private int _previewLaneDelta;
    private bool _previewValid;
    private string _previewMessage;
    private TextField _renameField;
    private ActionV1DocumentEntry _renamingLane;
    private int _animationPickerControlId;
    private int _pendingAnimationFrame;
    private string _hoverDisplayKey;
    private string _transientDisplayKey;
    private string _statusNotice;
    private bool _syncingScroll;
    private bool _releasingPointerCapture;
    private bool _rebuildRestorePending;
    private int _restoreGeneration;
    private int _viewportGeometryGeneration;
    private Vector2 _lastViewportSize = new Vector2(float.NaN, float.NaN);

    [MenuItem("Tools/Combat/Action V1/Action Timeline")]
    public static void OpenFromMenu()
    {
        if (Selection.activeObject is ActionAsset action)
            ActionV1EditorContext.Shared.SetAction(action);
        GetWindow<ActionV1TimelineWindow>("Action Timeline").Show();
    }

    internal static void Open(ActionAsset action)
    {
        ActionV1EditorContext.Shared.SetAction(action);
        GetWindow<ActionV1TimelineWindow>("Action Timeline").Show();
    }

    internal static void OpenShared() => GetWindow<ActionV1TimelineWindow>("Action Timeline").Show();

    internal static void LocateFromPreview(ActionV1DocumentEntry entry)
    {
        ActionV1TimelineWindow window = GetWindow<ActionV1TimelineWindow>("Action Timeline");
        window.Show();
        window.rootVisualElement.schedule.Execute(() =>
        {
            if (window._document == null)
                window.RefreshDocumentAndViews();
            window.NavigateToEntry(entry, true);
        });
    }

    private void OnEnable()
    {
        ActionV1EditorContext.Changed += OnContextChanged;
        Undo.undoRedoPerformed += OnExternalDataChanged;
        EditorApplication.projectChanged += OnExternalDataChanged;
    }

    private void OnDisable()
    {
        CaptureWindowState();
        CancelInteraction();
        ActionV1EditorInteractionGate.Release(this);
        CancelAnimationPicker();
        SetHoveredEntry(null);
        StopPlayback();
        ActionV1EditorContext.Changed -= OnContextChanged;
        Undo.undoRedoPerformed -= OnExternalDataChanged;
        EditorApplication.projectChanged -= OnExternalDataChanged;
    }

    public void CreateGUI()
    {
        _headerWidth = Mathf.Clamp(_headerWidth, ActionV1EditorTheme.MinHeaderWidth, ActionV1EditorTheme.MaxHeaderWidth);
        rootVisualElement.Clear();
        ActionV1EditorTheme.Apply(rootVisualElement, "action-v1-timeline-window");
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
        BuildIssuesDrawer();
        BuildStatusBar();
        RefreshDocumentAndViews();
    }

    private void BuildChrome()
    {
        rootVisualElement.Add(ActionV1EditorChrome.ContextBar(
            ActionV1EditorContext.Shared.CurrentAction,
            action => ActionV1EditorContext.Shared.SetAction(action),
            ("Details", ActionV1DetailsWindow.OpenShared),
            ("Preview", ActionV1PreviewWindow.OpenShared)));

        var transport = new VisualElement();
        transport.AddToClassList("action-v1-transport-bar");
        transport.Add(TransportButton("|◀", "First frame", () => RunTimelineNavigation(() => ActionV1EditorContext.Shared.SetFrame(0))));
        transport.Add(TransportButton("◀", "Previous frame", () => RunTimelineNavigation(() => ActionV1EditorContext.Shared.SetFrame(ActionV1EditorContext.Shared.CurrentFrame - 1))));
        _playButton = TransportButton("▶", "Play / Pause", TogglePlayback);
        transport.Add(_playButton);
        transport.Add(TransportButton("▶", "Next frame", () => RunTimelineNavigation(() => ActionV1EditorContext.Shared.SetFrame(ActionV1EditorContext.Shared.CurrentFrame + 1))));
        transport.Add(TransportButton("▶|", "Last frame", () => RunTimelineNavigation(() => ActionV1EditorContext.Shared.SetFrame(Mathf.Max(0, (_document?.DurationFrames ?? 1) - 1)))));

        var loop = new ToolbarToggle { text = "Loop", value = ActionV1EditorContext.Shared.PreviewLoop };
        loop.tooltip = "Loop editor frame playback";
        loop.AddToClassList("action-v1-loop-toggle");
        loop.RegisterValueChangedCallback(evt =>
        {
            if (HasActivePointerGesture)
            {
                loop.SetValueWithoutNotify(ActionV1EditorContext.Shared.PreviewLoop);
                return;
            }
            ActionV1EditorContext.Shared.SetPreviewLoop(evt.newValue);
        });
        transport.Add(loop);

        var separator = new VisualElement();
        separator.AddToClassList("action-v1-toolbar-separator");
        transport.Add(separator);
        transport.Add(TransportButton("Fit", "Fit the authoring horizon", FitAll));
        _frameLabel = new Label("Frame 0");
        _frameLabel.AddToClassList("action-v1-frame-label");
        transport.Add(_frameLabel);
        var transportSpacer = new VisualElement();
        transportSpacer.AddToClassList("action-v1-spacer");
        transport.Add(transportSpacer);
        _readinessPill = ActionV1EditorChrome.Pill("NO ACTION", "action-v1-pill-neutral");
        transport.Add(_readinessPill);
        rootVisualElement.Add(transport);

        _identityBanner = new VisualElement();
        _identityBanner.AddToClassList("action-v1-identity-banner");
        rootVisualElement.Add(_identityBanner);
    }

    private void BuildTimelineShell()
    {
        _timelineHost = new VisualElement();
        _timelineHost.AddToClassList("action-v1-timeline-main");

        var topRow = new VisualElement();
        topRow.AddToClassList("action-v1-timeline-top-row");
        _corner = new VisualElement();
        _corner.AddToClassList("action-v1-timeline-corner");
        var cornerTitle = new Label("LANES");
        cornerTitle.AddToClassList("action-v1-corner-title");
        _corner.Add(cornerTitle);
        _addLaneButton = new Button(AddGameplayLane) { text = "+ Lane", tooltip = "Add a Gameplay Lane" };
        _addLaneButton.AddToClassList("action-v1-lane-command");
        _addLaneButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        _corner.Add(_addLaneButton);
        _headerResizeHandle = new VisualElement();
        _headerResizeHandle.AddToClassList("action-v1-header-resize-handle");
        _headerResizeHandle.RegisterCallback<PointerDownEvent>(BeginHeaderResize);
        _headerResizeHandle.RegisterCallback<PointerMoveEvent>(ContinueHeaderResize);
        _headerResizeHandle.RegisterCallback<PointerUpEvent>(EndHeaderResize);
        _corner.Add(_headerResizeHandle);
        topRow.Add(_corner);

        _rulerViewport = new VisualElement();
        _rulerViewport.AddToClassList("action-v1-ruler-viewport");
        _rulerCanvas = new IMGUIContainer(DrawRuler);
        _rulerCanvas.AddToClassList("action-v1-ruler-canvas");
        _rulerCanvas.RegisterCallback<PointerDownEvent>(BeginScrub);
        _rulerCanvas.RegisterCallback<PointerMoveEvent>(ContinueScrub);
        _rulerCanvas.RegisterCallback<PointerUpEvent>(EndScrub);
        _rulerViewport.RegisterCallback<WheelEvent>(OnTimelineWheel, TrickleDown.TrickleDown);
        _rulerViewport.Add(_rulerCanvas);
        topRow.Add(_rulerViewport);
        _timelineHost.Add(topRow);

        var bodyRow = new VisualElement();
        bodyRow.AddToClassList("action-v1-timeline-body-row");
        _headerViewport = new VisualElement();
        _headerViewport.AddToClassList("action-v1-header-viewport");
        _headerContent = new VisualElement();
        _headerContent.AddToClassList("action-v1-header-content");
        _headerViewport.Add(_headerContent);
        _headerViewport.RegisterCallback<WheelEvent>(OnHeaderWheel, TrickleDown.TrickleDown);
        bodyRow.Add(_headerViewport);

        _contentScroll = new ScrollView(ScrollViewMode.Vertical);
        _contentScroll.AddToClassList("action-v1-content-scroll");
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
        _contentCanvas.AddToClassList("action-v1-content-canvas");
        _contentCanvas.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown, TrickleDown.TrickleDown);
        _contentCanvas.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove);
        _contentCanvas.RegisterCallback<PointerUpEvent>(OnCanvasPointerUp);
        _gridCanvas = new IMGUIContainer(DrawGrid) { pickingMode = PickingMode.Ignore };
        _gridCanvas.AddToClassList("action-v1-grid-canvas");
        _contentCanvas.Add(_gridCanvas);
        _postDurationShade = new VisualElement { pickingMode = PickingMode.Ignore };
        _postDurationShade.AddToClassList("action-v1-post-duration");
        _contentCanvas.Add(_postDurationShade);
        _durationLine = new VisualElement { pickingMode = PickingMode.Ignore };
        _durationLine.AddToClassList("action-v1-duration-line");
        _contentCanvas.Add(_durationLine);
        _horizonLine = new VisualElement { pickingMode = PickingMode.Ignore };
        _horizonLine.AddToClassList("action-v1-horizon-line");
        _contentCanvas.Add(_horizonLine);

        _entryLayer = CreateContentLayer("action-v1-entry-layer", PickingMode.Ignore);
        _contentCanvas.Add(_entryLayer);
        _pointDensityLayer = CreateContentLayer("action-v1-point-density-layer", PickingMode.Ignore);
        _contentCanvas.Add(_pointDensityLayer);
        _overlapLayer = CreateContentLayer("action-v1-overlap-layer", PickingMode.Ignore);
        _contentCanvas.Add(_overlapLayer);
        _ghostLayer = CreateContentLayer("action-v1-ghost-layer", PickingMode.Ignore);
        _contentCanvas.Add(_ghostLayer);
        _guideLayer = CreateContentLayer("action-v1-guide-layer", PickingMode.Ignore);
        _contentCanvas.Add(_guideLayer);

        _playhead = new VisualElement { pickingMode = PickingMode.Ignore };
        _playhead.AddToClassList("action-v1-playhead");
        _guideLayer.Add(_playhead);
        _marquee = new VisualElement { pickingMode = PickingMode.Ignore };
        _marquee.AddToClassList("action-v1-marquee");
        _marquee.style.display = DisplayStyle.None;
        _guideLayer.Add(_marquee);
        _contentScroll.Add(_contentCanvas);
        bodyRow.Add(_contentScroll);
        _timelineHost.Add(bodyRow);

        BuildTimeNavigator();

        _emptyOverlay = ActionV1EditorChrome.EmptyState("Choose an ActionAsset", "Select an ActionAsset above or in the Project window.");
        _emptyOverlay.pickingMode = PickingMode.Ignore;
        _emptyOverlay.style.position = Position.Absolute;
        _emptyOverlay.style.left = 0f;
        _emptyOverlay.style.right = 0f;
        _emptyOverlay.style.top = ActionV1EditorTheme.RulerHeight;
        _emptyOverlay.style.bottom = 0f;
        _timelineHost.Add(_emptyOverlay);
        _emptyTitle = _emptyOverlay.Q<Label>(className: "action-v1-empty-title");
        _emptyBody = _emptyOverlay.Q<Label>(className: "action-v1-empty-body");

        _geometryOverflowSentinel = new Label("DISPLAY LIMIT") { pickingMode = PickingMode.Ignore };
        _geometryOverflowSentinel.AddToClassList("action-v1-geometry-overflow");
        _geometryOverflowSentinel.tooltip = "Timeline coordinates beyond this presentation boundary keep their raw frame values but are pinned here for UI safety.";
        _guideLayer.Add(_geometryOverflowSentinel);

        _rulerPlayhead = new VisualElement { pickingMode = PickingMode.Ignore };
        _rulerPlayhead.AddToClassList("action-v1-ruler-playhead");
        _rulerViewport.Add(_rulerPlayhead);
        rootVisualElement.Add(_timelineHost);
        ApplyHeaderWidth();
    }

    private void BuildTimeNavigator()
    {
        _navigatorRow = new VisualElement();
        _navigatorRow.AddToClassList("action-v1-time-navigator-row");
        _navigatorCorner = new VisualElement();
        _navigatorCorner.AddToClassList("action-v1-time-navigator-corner");
        _navigatorRow.Add(_navigatorCorner);

        _timeNavigator = new VisualElement();
        _timeNavigator.AddToClassList("action-v1-time-navigator");
        _timeNavigator.RegisterCallback<GeometryChangedEvent>(_ => RefreshTimeNavigator());
        _timeNavigatorThumb = new VisualElement();
        _timeNavigatorThumb.AddToClassList("action-v1-time-navigator-thumb");
        _timeNavigatorThumb.RegisterCallback<PointerDownEvent>(evt => BeginNavigatorInteraction(evt, InteractionState.TimeNavigatorPan));
        _timeNavigatorThumb.RegisterCallback<PointerMoveEvent>(ContinueNavigatorInteraction);
        _timeNavigatorThumb.RegisterCallback<PointerUpEvent>(EndNavigatorInteraction);

        _timeNavigatorLeftHandle = new VisualElement();
        _timeNavigatorLeftHandle.AddToClassList("action-v1-time-navigator-handle");
        _timeNavigatorLeftHandle.AddToClassList("action-v1-time-navigator-handle-left");
        _timeNavigatorLeftHandle.RegisterCallback<PointerDownEvent>(evt => BeginNavigatorInteraction(evt, InteractionState.TimeNavigatorResizeLeft));
        _timeNavigatorLeftHandle.RegisterCallback<PointerMoveEvent>(ContinueNavigatorInteraction);
        _timeNavigatorLeftHandle.RegisterCallback<PointerUpEvent>(EndNavigatorInteraction);
        _timeNavigatorThumb.Add(_timeNavigatorLeftHandle);

        _timeNavigatorRightHandle = new VisualElement();
        _timeNavigatorRightHandle.AddToClassList("action-v1-time-navigator-handle");
        _timeNavigatorRightHandle.AddToClassList("action-v1-time-navigator-handle-right");
        _timeNavigatorRightHandle.RegisterCallback<PointerDownEvent>(evt => BeginNavigatorInteraction(evt, InteractionState.TimeNavigatorResizeRight));
        _timeNavigatorRightHandle.RegisterCallback<PointerMoveEvent>(ContinueNavigatorInteraction);
        _timeNavigatorRightHandle.RegisterCallback<PointerUpEvent>(EndNavigatorInteraction);
        _timeNavigatorThumb.Add(_timeNavigatorRightHandle);
        _timeNavigator.Add(_timeNavigatorThumb);
        _navigatorRow.Add(_timeNavigator);
        _timelineHost.Add(_navigatorRow);
    }

    private void BuildIssuesDrawer()
    {
        _issuesFoldout = new Foldout { text = "Issues", value = _issuesExpanded };
        _issuesFoldout.RegisterValueChangedCallback(evt => _issuesExpanded = evt.newValue);
        _issuesFoldout.AddToClassList("action-v1-issues-drawer");
        _issuesList = new ScrollView(ScrollViewMode.Vertical);
        _issuesList.AddToClassList("action-v1-issues-list");
        _issuesFoldout.Add(_issuesList);
        rootVisualElement.Add(_issuesFoldout);
    }

    private void BuildStatusBar()
    {
        var status = new VisualElement();
        status.AddToClassList("action-v1-status-bar");
        _statusLabel = new Label();
        status.Add(_statusLabel);
        var spacer = new VisualElement();
        spacer.AddToClassList("action-v1-spacer");
        status.Add(spacer);
        status.Add(new Label("AUTHORING MODE  ·  60 FPS"));
        rootVisualElement.Add(status);
    }

    private void RefreshDocumentAndViews()
    {
        if (_contentCanvas == null)
            return;

        float verticalScroll = _document == null && _hasStoredScroll
            ? _storedVerticalScroll
            : _contentScroll.scrollOffset.y;
        int restoreGeneration = ++_restoreGeneration;
        _rebuildRestorePending = true;
        _document = ActionV1EditorDocument.Build(ActionV1EditorContext.Shared.CurrentAction);
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
        _geometry = new ActionV1TimelineGeometry(
            _document.DurationFrames,
            _document.HorizonFrames,
            _document.Lanes.Count,
            _timeViewport.VisibleStartFrame,
            _timeViewport.VisibleSpanFrames,
            viewportWidth);
        ActionV1EditorChrome.SyncActionField(rootVisualElement);
        _addLaneButton?.SetEnabled(_document.Asset != null && _document.HasTimeline &&
                                   _document.Readiness != ActionV1EditorReadiness.IdentityBlocked);
        UpdateEmptyState();
        _transientDisplayKey = null;
        _statusNotice = null;
        ActionV1EditorContext.Shared.ValidateSelection(_document);
        _entryViews.Clear();
        _entryPresentation.Clear();
        _laneHeaderViews.Clear();
        _entriesByDisplayKey.Clear();
        _headerContent.Clear();
        ClearCanvasExceptInfrastructure();
        BuildIdentityBanner();
        BuildLaneHeaders();
        BuildEntries();
        BuildOverlapMarkers();
        FinalizeLayerOrder();
        BuildIssues();
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
    /// Details leaf fields commonly change validation and preview inputs without changing
    /// timeline geometry. Rebuilding the complete visual tree here interrupts native
    /// controls in the other window, so keep this path intentionally narrow.
    /// </summary>
    private void RefreshValidationOnly()
    {
        if (_contentCanvas == null)
            return;
        _document = ActionV1EditorDocument.Build(ActionV1EditorContext.Shared.CurrentAction);
        ActionV1EditorContext.Shared.ValidateSelection(_document);
        RefreshEntryValidationPresentation();
        BuildIssues();
        RefreshStatus();
    }

    private void RefreshEntryValidationPresentation()
    {
        foreach (KeyValuePair<string, VisualElement> pair in _entryViews)
        {
            ActionV1DocumentEntry entry = _document.ContentEntries.FirstOrDefault(candidate =>
                string.Equals(candidate.DisplayKey, pair.Key, StringComparison.Ordinal));
            if (entry == null)
                continue;

            VisualElement view = pair.Value;
            view.userData = entry;
            _entriesByDisplayKey[pair.Key] = entry;
            IReadOnlyList<ActionAuthoringValidationIssue> issues = _document.ValidationIndex.ForEntry(entry);
            ActionV1ValidationSeverity? severity = HighestSeverity(issues);
            bool hasIssue = issues.Count > 0 || entry.Source == null || entry.StartFrame < 0 ||
                            entry.RawEndFrameExclusive <= entry.StartFrame;
            view.EnableInClassList("action-v1-entry-has-issue", hasIssue);
            view.RemoveFromClassList(SeverityClass(ActionV1ValidationSeverity.IdentityBlock));
            view.RemoveFromClassList(SeverityClass(ActionV1ValidationSeverity.Error));
            view.RemoveFromClassList(SeverityClass(ActionV1ValidationSeverity.NeedsSetup));
            if (severity.HasValue)
                view.AddToClassList(SeverityClass(severity.Value));

            VisualElement badge = view.Q<VisualElement>(className: "action-v1-issue-badge");
            while (badge != null)
            {
                badge.RemoveFromHierarchy();
                badge = view.Q<VisualElement>(className: "action-v1-issue-badge");
            }
            if (issues.Count > 0 || entry.Source == null ||
                entry.IdentityState != ActionV1EditorIdentityState.Valid)
            {
                VisualElement contentHost = entry.Source is PointGameplayItem
                    ? view
                    : view.Q<VisualElement>(className: "action-v1-range-body") ?? view;
                var newBadge = new Label(severity == ActionV1ValidationSeverity.NeedsSetup ? "?" : "!");
                newBadge.AddToClassList("action-v1-issue-badge");
                if (severity.HasValue)
                    newBadge.AddToClassList(SeverityClass(severity.Value));
                newBadge.tooltip = severity == ActionV1ValidationSeverity.NeedsSetup
                    ? "Needs setup"
                    : "Authoring issue";
                contentHost.Add(newBadge);
            }
            view.tooltip = EntryTooltip(entry, issues, false);
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
            _emptyBody.text = "The asset remains unchanged. See Issues for the validation result.";
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
        _overlapLayer?.Clear();
        _ghostLayer?.Clear();
        _hoverDisplayKey = null;
    }

    private void FinalizeLayerOrder()
    {
        _entryLayer?.BringToFront();
        _pointDensityLayer?.BringToFront();
        _overlapLayer?.BringToFront();
        _ghostLayer?.BringToFront();
        _guideLayer?.BringToFront();
    }

    private void BuildIdentityBanner()
    {
        _identityBanner.Clear();
        bool blocked = _document != null && _document.Readiness == ActionV1EditorReadiness.IdentityBlocked;
        _identityBanner.style.display = blocked ? DisplayStyle.Flex : DisplayStyle.None;
        if (!blocked)
            return;

        ActionV1ValidationIndex index = _document.ValidationIndex;
        int count = index.BlockingIdentityCount;
        var text = new Label(
            $"Identity blocked · {count} affected " +
            $"({index.MissingIdentityCount} missing, {index.MalformedIdentityCount} malformed, {index.DuplicateIdentityCount} duplicate). " +
            "Unsafe entries remain visible but cannot enter stable selection.");
        text.AddToClassList("action-v1-banner-text");
        _identityBanner.Add(text);
        var repair = new Button(RepairIdentity) { text = $"Repair {count} ID{(count == 1 ? string.Empty : "s")}" };
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
        if (ActionV1EditorCommands.RepairEditorIds(asset, out int repaired, out string message))
        {
            _transientDisplayKey = null;
            _statusNotice = message;
            Debug.Log($"[Action V1 Authoring] {message} Asset: '{asset.name}'.", asset);
        }
        else
        {
            _statusNotice = message;
        }
        RefreshStatus();
    }

    private void BuildLaneHeaders()
    {
        int animationOverlap = _document.OverlappingEntryCount(true, -1);
        VisualElement animationHeader = CreateLaneHeader(
            "Animation",
            animationOverlap > 0 ? $"OVERLAP · {animationOverlap}" : "POSE",
            ActionV1EditorTheme.AnimationLaneHeight,
            false,
            null);
        animationHeader.EnableInClassList("action-v1-lane-overlap-error", animationOverlap > 0);
        Button addAnimation = LaneCommandButton("+", "Add Animation Segment at Current Frame", () => BeginAnimationPicker(ActionV1EditorContext.Shared.CurrentFrame));
        addAnimation.SetEnabled(CanAuthor(out _));
        animationHeader.Add(addAnimation);
        animationHeader.Add(LaneCommandButton("...", "Animation content navigation", () => ShowLaneNavigationMenu(-1, true)));
        _headerContent.Add(animationHeader);
        foreach (ActionV1DocumentEntry lane in _document.Lanes)
        {
            int overlapping = _document.OverlappingEntryCount(false, lane.LaneIndex);
            string meta = lane.Source is GameplayLane gameplay
                ? overlapping > 0
                    ? $"{gameplay.Items.Count} items · {overlapping} overlapping"
                    : $"{gameplay.Items.Count} items"
                : "QUARANTINED";
            VisualElement header = CreateLaneHeader(EntryLabel(lane, false), meta, ActionV1EditorTheme.GameplayLaneHeight, lane.Muted, lane);
            header.EnableInClassList("action-v1-lane-overlap-neutral", overlapping > 0);
            if (lane.Source is GameplayLane gameplayLane && lane.HasStableSelection)
            {
                var mute = new Toggle { value = gameplayLane.Muted, tooltip = "Mute Lane" };
                mute.AddToClassList("action-v1-lane-mute");
                mute.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                mute.RegisterValueChangedCallback(evt => SetMuted(lane, evt.newValue));
                mute.SetEnabled(CanAuthor(out _));
                header.Add(mute);
                Button addItem = LaneCommandButton("+", "Add Gameplay Item at Current Frame", () => ShowItemCreationMenu(lane, ActionV1EditorContext.Shared.CurrentFrame));
                Button commands = LaneCommandButton("⋮", "Lane commands", () => ShowLaneMenu(lane));
                addItem.SetEnabled(CanAuthor(out _));
                header.Add(addItem);
                header.Add(commands);
            }
            else if (lane.Source == null && _document.Readiness != ActionV1EditorReadiness.IdentityBlocked)
            {
                header.Add(LaneCommandButton("×", "Delete quarantined null Lane", () => DeleteNullEntry(lane)));
            }
            if (!(lane.Source is GameplayLane && lane.HasStableSelection))
                header.Add(LaneCommandButton("…", "Lane content navigation", () => ShowLaneNavigationMenu(lane.LaneIndex, false)));
            _headerContent.Add(header);
        }
    }

    private VisualElement CreateLaneHeader(string title, string meta, float height, bool muted, ActionV1DocumentEntry entry)
    {
        var row = new VisualElement();
        row.AddToClassList("action-v1-lane-header");
        row.style.height = height;
        row.RegisterCallback<PointerDownEvent>(_ => FocusTimelineCommands());
        if (muted) row.AddToClassList("action-v1-muted");
        if (entry != null && entry.IdentityState != ActionV1EditorIdentityState.Valid)
            row.AddToClassList("action-v1-invalid-identity");
        if (entry != null && entry.DisplayState != ActionV1EntryDisplayState.Normal)
            row.AddToClassList("action-v1-placeholder-header");
        var titleLabel = new Label(title);
        titleLabel.AddToClassList("action-v1-lane-title");
        titleLabel.tooltip = entry == null
            ? title
            : $"{title}\n{entry.AuthoringPath}\nDisplay: {DisplayStateText(entry.DisplayState)}\n{IdentityText(entry.IdentityState)}";
        row.Add(titleLabel);
        var metaLabel = new Label(meta);
        metaLabel.AddToClassList("action-v1-lane-meta");
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
                        ActionV1EditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, false, false);
                        BeginInlineRename(entry, row, titleLabel);
                    }
                    evt.StopPropagation();
                }
            });
            _laneHeaderViews[entry.DisplayKey] = row;
        }
        return row;
    }

    private void BuildEntries()
    {
        foreach (ActionV1DocumentEntry entry in _document.AnimationSegments)
            AddEntry(entry, 0f, ActionV1EditorTheme.AnimationLaneHeight, true);
        foreach (ActionV1DocumentEntry entry in _document.GameplayItems)
        {
            float y = _geometry.LaneTop(entry.LaneIndex);
            AddEntry(entry, y, ActionV1EditorTheme.GameplayLaneHeight, false);
        }
    }

    private void AddEntry(ActionV1DocumentEntry entry, float rowTop, float rowHeight, bool animation)
    {
        bool pointEntry = entry.Source is PointGameplayItem;
        var view = new VisualElement { userData = entry };
        view.AddToClassList("action-v1-entry");
        view.AddToClassList(animation ? "action-v1-animation-entry" : ItemClass(entry.Source));
        if (pointEntry)
            view.AddToClassList("action-v1-point-entry");
        if (entry.Muted)
            view.AddToClassList("action-v1-muted");
        if (entry.IdentityState != ActionV1EditorIdentityState.Valid)
            view.AddToClassList("action-v1-invalid-identity");
        ApplyPlaceholderClasses(view, entry.DisplayState);
        IReadOnlyList<ActionAuthoringValidationIssue> entryIssues = _document.ValidationIndex.ForEntry(entry);
        ActionV1ValidationSeverity? severity = HighestSeverity(entryIssues);
        if (entryIssues.Count > 0 || entry.Source == null || entry.StartFrame < 0 || entry.RawEndFrameExclusive <= entry.StartFrame)
            view.AddToClassList("action-v1-entry-has-issue");
        if (severity.HasValue)
            view.AddToClassList(SeverityClass(severity.Value));

        if (pointEntry)
        {
            var diamond = new VisualElement { pickingMode = PickingMode.Ignore };
            diamond.AddToClassList("action-v1-point-diamond");
            view.Add(diamond);
        }

        VisualElement contentHost = view;
        if (!pointEntry)
        {
            view.AddToClassList("action-v1-range-entry");
            contentHost = new VisualElement { pickingMode = PickingMode.Ignore };
            contentHost.AddToClassList("action-v1-range-body");
            view.Add(contentHost);
        }

        var label = new Label(EntryLabel(entry, false)) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("action-v1-entry-label");
        if (pointEntry)
            label.AddToClassList("action-v1-point-label");
        contentHost.Add(label);
        if (entryIssues.Count > 0 || entry.Source == null || entry.IdentityState != ActionV1EditorIdentityState.Valid)
        {
            var badge = new Label(severity == ActionV1ValidationSeverity.NeedsSetup ? "?" : "!");
            badge.AddToClassList("action-v1-issue-badge");
            if (severity.HasValue)
                badge.AddToClassList(SeverityClass(severity.Value));
            badge.tooltip = severity == ActionV1ValidationSeverity.NeedsSetup ? "Needs setup" : "Authoring issue";
            contentHost.Add(badge);
        }
        view.tooltip = EntryTooltip(entry, entryIssues, false);
        if (entry.Source is RangeGameplayItem || entry.Source is AnimationSegment)
        {
            var leftHandle = new VisualElement { pickingMode = PickingMode.Ignore };
            leftHandle.AddToClassList("action-v1-entry-handle");
            leftHandle.AddToClassList("action-v1-entry-handle-left");
            contentHost.Add(leftHandle);
            var rightHandle = new VisualElement { pickingMode = PickingMode.Ignore };
            rightHandle.AddToClassList("action-v1-entry-handle");
            rightHandle.AddToClassList("action-v1-entry-handle-right");
            contentHost.Add(rightHandle);
        }
        view.RegisterCallback<PointerDownEvent>(evt => OnEntryPointerDown(evt, entry, view));
        view.RegisterCallback<PointerMoveEvent>(OnManipulationPointerMove);
        view.RegisterCallback<PointerUpEvent>(OnManipulationPointerUp);
        view.RegisterCallback<PointerEnterEvent>(_ => SetHoveredEntry(entry.DisplayKey));
        view.RegisterCallback<PointerLeaveEvent>(_ => ClearHoveredEntry(entry.DisplayKey));
        view.style.top = rowTop + 3f;
        view.style.height = Mathf.Max(18f, rowHeight - 6f);
        _entryLayer.Add(view);
        _entryViews[entry.DisplayKey] = view;
        _entriesByDisplayKey[entry.DisplayKey] = entry;
    }

    private void BuildOverlapMarkers()
    {
        foreach (ActionV1OverlapRegion region in _document.OverlapRegions)
        {
            if (region.EndFrameExclusive <= 0)
                continue;

            var marker = new Button(() => ShowOverlapPicker(region))
            {
                text = $"×{region.PeakOverlapCount}",
                focusable = false,
            };
            marker.AddToClassList("action-v1-overlap-marker");
            marker.AddToClassList(region.IsAnimation ? "action-v1-overlap-error" : "action-v1-overlap-neutral");
            marker.tooltip = OverlapTooltip(region);
            marker.style.top = region.IsAnimation
                ? 5f
                : _geometry.LaneTop(region.LaneIndex) + 5f;
            marker.userData = region;
            marker.RegisterCallback<PointerDownEvent>(_ => FocusTimelineCommands());
            _overlapLayer.Add(marker);
        }
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
        foreach (ActionV1DocumentEntry point in _document.GameplayItems.Where(entry => entry.Source is PointGameplayItem))
        {
            if (_entryViews.TryGetValue(point.DisplayKey, out VisualElement pointView))
            {
                pointView.style.display = DisplayStyle.Flex;
                pointView.EnableInClassList("action-v1-point-density-member", false);
            }
            if (_entryPresentation.TryGetValue(point.DisplayKey, out EntryPresentation presentation))
                presentation.Displayed = true;
        }

        string primaryId = ActionV1EditorContext.Shared.PrimarySelection.EditorId;
        ActionV1DocumentEntry primaryPoint = _document.GameplayItems.FirstOrDefault(entry =>
            entry.Source is PointGameplayItem && entry.HasStableSelection &&
            string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal));
        if (primaryPoint != null && _entryPresentation.TryGetValue(primaryPoint.DisplayKey, out EntryPresentation primaryPresentation))
            ReservedRects(primaryPoint.LaneIndex).Add(primaryPresentation.HitRect);

        const float collisionDistance = 26f;
        var activeClusters = new Dictionary<string, PointDensityCluster>();
        if (rebuildGroups)
        foreach (IGrouping<int, ActionV1DocumentEntry> lanePoints in _document.GameplayItems
                     .Where(entry => entry.Source is PointGameplayItem &&
                                     entry.DisplayState == ActionV1EntryDisplayState.Normal)
                     .GroupBy(entry => entry.LaneIndex))
        {
            var positioned = lanePoints
                .Select(entry => new
                {
                    Entry = entry,
                    X = _geometry.FrameToPixel(Math.Max(0L, entry.StartFrame), out bool overflow),
                    Overflow = overflow,
                })
                .Where(item => !item.Overflow && item.X >= -ActionV1TimelineGeometry.PointHitWidth &&
                               item.X <= _geometry.ContentWidth + ActionV1TimelineGeometry.PointHitWidth)
                .OrderBy(item => item.X)
                .ThenBy(item => item.Entry.ItemIndex)
                .ToList();

            foreach (List<int> indices in ActionV1TimelineInteractionMath.BoundedGroups(
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

        string primaryId = ActionV1EditorContext.Shared.PrimarySelection.EditorId;
        ActionV1DocumentEntry primary = cluster.Entries.FirstOrDefault(entry =>
            entry.HasStableSelection && string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal));
        ActionV1DocumentEntry focused = primary;
        if (focused == null)
            focused = cluster.Entries.FirstOrDefault(entry =>
                entry.HasStableSelection && ActionV1EditorContext.Shared.IsSelected(entry.EditorId));
        if (focused == null && !string.IsNullOrEmpty(_transientDisplayKey))
            focused = cluster.Entries.FirstOrDefault(entry => entry.DisplayKey == _transientDisplayKey);
        if (focused == null && !string.IsNullOrEmpty(_hoverDisplayKey))
            focused = cluster.Entries.FirstOrDefault(entry => entry.DisplayKey == _hoverDisplayKey);

        foreach (ActionV1DocumentEntry entry in cluster.Entries)
        {
            if (!_entryViews.TryGetValue(entry.DisplayKey, out VisualElement view))
                continue;
            view.EnableInClassList("action-v1-point-density-member", true);
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
        bool markerHidden = !ActionV1TimelineInteractionMath.CanPlaceMarker(markerRect, reserved);
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
            marker.AddToClassList("action-v1-point-density-cluster");
            marker.clicked += () =>
            {
                if (marker.userData is PointDensityCluster current)
                    ShowPointDensityPicker(current);
            };
            var backDiamond = new VisualElement { pickingMode = PickingMode.Ignore };
            backDiamond.AddToClassList("action-v1-point-density-diamond-back");
            marker.Add(backDiamond);
            var frontDiamond = new VisualElement { pickingMode = PickingMode.Ignore };
            frontDiamond.AddToClassList("action-v1-point-density-diamond-front");
            marker.Add(frontDiamond);
            var count = new Label { pickingMode = PickingMode.Ignore };
            count.AddToClassList("action-v1-point-density-count");
            marker.Add(count);
            marker.RegisterCallback<PointerDownEvent>(_ => FocusTimelineCommands());
            _pointDensityLayer.Add(marker);
            _pointDensityMarkers.Add(cluster.Key, marker);
        }
        marker.userData = cluster;
        marker.Q<Label>(className: "action-v1-point-density-count").text = cluster.Entries.Count > 99 ? "99+" : cluster.Entries.Count.ToString();
        marker.EnableInClassList("action-v1-point-density-focused", focused != null);
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
        foreach (ActionV1DocumentEntry entry in cluster.Entries.OrderBy(item => item.ItemIndex))
        {
            ActionV1DocumentEntry captured = entry;
            string label = $"{entry.DisplayName} · Frame {entry.StartFrame} · {entry.AuthoringPath}";
            menu.AddItem(new GUIContent(label),
                entry.HasStableSelection && ActionV1EditorContext.Shared.IsSelected(entry.EditorId),
                () => NavigateToEntry(captured, true));
        }
        menu.ShowAsContext();
    }

    private void ShowOverlapPicker(ActionV1OverlapRegion region)
    {
        FocusTimelineCommands();
        var menu = new GenericMenu();
        menu.AddDisabledItem(new GUIContent("Navigation only · no priority"));
        menu.AddSeparator(string.Empty);
        foreach (ActionV1DocumentEntry entry in region.Entries)
        {
            string lane = region.IsAnimation ? "Animation" : entry.LaneName;
            string label = $"{entry.DisplayName} · {lane} · [{entry.StartFrame}, {entry.RawEndFrameExclusive}) · {entry.AuthoringPath}";
            if (entry.HasStableSelection)
            {
                ActionV1DocumentEntry captured = entry;
                menu.AddItem(new GUIContent(label), ActionV1EditorContext.Shared.IsSelected(entry.EditorId), () =>
                {
                    NavigateToEntry(captured, true);
                });
            }
            else
            {
                ActionV1DocumentEntry captured = entry;
                menu.AddItem(new GUIContent(label + "  · unstable ID"), false, () =>
                {
                    NavigateToEntry(captured, true);
                });
            }
        }
        menu.ShowAsContext();
    }

    private void BuildIssues()
    {
        _issuesList.Clear();
        int count = _document.Validation?.Issues.Count ?? 0;
        ActionV1ValidationIndex index = _document.ValidationIndex;
        _issuesFoldout.text = count == 0
            ? "Issues · none"
            : $"Issues · {index.BlockingIdentityCount} identity · {index.ErrorCount} error · {index.NeedsSetupCount} setup";
        if (count == 0)
        {
            var clear = new Label("No authoring issues detected.");
            clear.AddToClassList("action-v1-issues-empty");
            _issuesList.Add(clear);
            return;
        }

        foreach (ActionAuthoringValidationIssue issue in _document.Validation.Issues)
        {
            var row = new Button(() => LocateIssue(issue));
            row.AddToClassList("action-v1-issue-row");
            ActionV1ValidationSeverity severity = ActionV1ValidationIndex.SeverityOf(issue.Code);
            row.AddToClassList(SeverityClass(severity));
            string path = string.IsNullOrEmpty(issue.AuthoringPath) ? "Timeline" : issue.AuthoringPath;
            row.text = $"{SeverityLabel(severity)} · {path} · {issue.Code} · {Shorten(issue.Message, 96)}";
            row.tooltip = $"{path}\n{issue.Code}\n{issue.Message}";
            _issuesList.Add(row);
        }
    }

    private void LocateIssue(ActionAuthoringValidationIssue issue)
    {
        if (HasActivePointerGesture)
            return;
        FocusTimelineCommands();
        IReadOnlyList<string> paths = _document.ValidationIndex.PathsFor(issue);
        List<ActionV1DocumentEntry> entries = paths
            .Select(path => _document.EntriesInAuthoringOrder().FirstOrDefault(entry => entry.AuthoringPath == path))
            .Where(entry => entry != null)
            .ToList();
        if (entries.Count == 1)
        {
            LocateEntry(entries[0]);
            return;
        }
        if (entries.Count > 1)
        {
            var menu = new GenericMenu();
            foreach (ActionV1DocumentEntry candidate in entries)
            {
                ActionV1DocumentEntry captured = candidate;
                menu.AddItem(new GUIContent(captured.AuthoringPath), false, () => LocateEntry(captured));
            }
            menu.ShowAsContext();
            return;
        }
        _transientDisplayKey = null;
        _statusNotice = $"Global issue · {issue.Code}";
        RefreshSelectionPresentation();
    }

    private void LocateEntry(ActionV1DocumentEntry entry)
    {
        NavigateToEntry(entry, true);
    }

    private void NavigateToEntry(ActionV1DocumentEntry entry, bool reveal)
    {
        entry = ResolveCurrentEntry(entry);
        if (entry == null || HasActivePointerGesture)
            return;

        FocusTimelineCommands();
        _statusNotice = null;
        if (entry.HasStableSelection)
        {
            _transientDisplayKey = null;
            ActionV1EditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, false, false);
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

    private ActionV1DocumentEntry ResolveCurrentEntry(ActionV1DocumentEntry stale)
    {
        if (stale == null || _document == null ||
            !ReferenceEquals(_document.Asset, ActionV1EditorContext.Shared.CurrentAction))
            return null;
        if (stale.HasStableSelection && _document.ById.TryGetValue(stale.EditorId, out ActionV1DocumentEntry byId) &&
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
        _geometry = new ActionV1TimelineGeometry(
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
        SetLayerSize(_overlapLayer, width, height);
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
            ActionV1DocumentEntry entry = _entriesByDisplayKey[pair.Key];
            ActionV1EntryGeometry layout = _geometry.EntryRect(entry);
            pair.Value.style.left = layout.Left;
            pair.Value.style.width = layout.Width;
            pair.Value.style.top = _geometry.LaneTop(entry.LaneIndex) + 3f;
            VisualElement rangeBody = pair.Value.Q<VisualElement>(className: "action-v1-range-body");
            if (rangeBody != null)
            {
                rangeBody.style.left = layout.VisualLeft;
                rangeBody.style.width = layout.VisualWidth;
            }
            pair.Value.EnableInClassList("action-v1-placeholder-overflow", layout.Overflow);
            hasPinnedOverflow |= layout.Overflow;
            Label label = pair.Value.Q<Label>(className: "action-v1-entry-label");
            if (label != null)
                label.text = EntryLabel(entry, layout.Overflow);
            if (entry.Source is PointGameplayItem)
                PositionPointDiamond(pair.Value, entry, layout);
            pair.Value.tooltip = EntryTooltip(entry, _document.ValidationIndex.ForEntry(entry), layout.Overflow);

            float rowTop = _geometry.LaneTop(entry.LaneIndex);
            float rowHeight = entry.LaneIndex < 0
                ? ActionV1EditorTheme.AnimationLaneHeight
                : ActionV1EditorTheme.GameplayLaneHeight;
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
        hasPinnedOverflow |= RefreshOverlapMarkerPresentation(width);
        _geometryOverflowSentinel.style.display = hasPinnedOverflow ? DisplayStyle.Flex : DisplayStyle.None;
        RefreshEntryLayering();
        FinalizeLayerOrder();
        _rulerCanvas.MarkDirtyRepaint();
        _gridCanvas.MarkDirtyRepaint();
        ClampCurrentVerticalScroll();
        RefreshTimeNavigator();
        RefreshFramePresentation();
    }

    private bool RefreshOverlapMarkerPresentation(float width)
    {
        bool hasPinnedOverflow = false;
        string primaryId = ActionV1EditorContext.Shared.PrimarySelection.EditorId;
        ActionV1DocumentEntry primaryEntry = _document.ContentEntries.FirstOrDefault(entry =>
            entry.HasStableSelection && string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal));
        if (primaryEntry != null && _entryPresentation.TryGetValue(primaryEntry.DisplayKey, out EntryPresentation primaryPresentation) &&
            !primaryPresentation.IsPoint)
            ReservedRects(primaryEntry.LaneIndex).Add(primaryPresentation.HitRect);

        foreach (VisualElement marker in _overlapLayer.Children())
        {
            var region = marker.userData as ActionV1OverlapRegion;
            if (region != null)
            {
                float startX = _geometry.FrameToPixel(Math.Max(0L, region.StartFrame), out bool startOverflow);
                float endX = _geometry.FrameToPixel(Math.Max(0L, region.EndFrameExclusive), out bool endOverflow);
                bool overflow = startOverflow || endOverflow;
                float markerLeft = overflow ? Mathf.Max(0f, width - 34f) : startX + 2f;
                float markerWidth = overflow
                    ? 32f
                    : Mathf.Clamp(Mathf.Max(0f, endX - startX - 4f), 28f, 64f);
                float markerTop = region.IsAnimation ? 5f : _geometry.LaneTop(region.LaneIndex) + 5f;
                var markerRect = new Rect(markerLeft, markerTop, markerWidth, 22f);
                List<Rect> reserved = ReservedRects(region.IsAnimation ? -1 : region.LaneIndex);
                bool hidden = !ActionV1TimelineInteractionMath.CanPlaceMarker(markerRect, reserved);
                marker.style.left = markerLeft;
                marker.style.width = markerWidth;
                marker.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
                if (!hidden)
                    reserved.Add(markerRect);
                marker.EnableInClassList("action-v1-overlap-overflow", overflow);
                hasPinnedOverflow |= overflow;
                marker.tooltip = OverlapTooltip(region) + (overflow ? "\nPinned at the safe geometry boundary." : string.Empty);
            }
        }
        return hasPinnedOverflow;
    }

    private void RefreshFramePresentation()
    {
        int frame = ActionV1EditorContext.Shared.CurrentFrame;
        _frameLabel.text = $"Frame {frame}";
        float x = _geometry != null
            ? Mathf.Min(Mathf.Max(0f, _geometry.ContentWidth - 1f), _geometry.FrameToPixel(frame, out _))
            : 0f;
        if (_playhead != null) _playhead.style.left = x;
        if (_rulerPlayhead != null) _rulerPlayhead.style.left = x;
        foreach (KeyValuePair<string, VisualElement> pair in _entryViews)
        {
            ActionV1DocumentEntry entry = _entriesByDisplayKey[pair.Key];
            pair.Value.EnableInClassList("action-v1-entry-active", frame >= entry.StartFrame && frame < entry.RawEndFrameExclusive);
            pair.Value.EnableInClassList("action-v1-entry-hold", false);
        }
        ActionV1DocumentEntry activeAnimation = _document.AnimationSegments.FirstOrDefault(entry =>
            entry.Source != null && frame >= entry.StartFrame && frame < entry.RawEndFrameExclusive);
        if (activeAnimation == null)
        {
            ActionV1DocumentEntry heldAnimation = _document.AnimationSegments
                .Where(entry => entry.Source != null && entry.RawEndFrameExclusive <= frame)
                .OrderBy(entry => entry.RawEndFrameExclusive)
                .LastOrDefault();
            if (heldAnimation != null && _entryViews.TryGetValue(heldAnimation.DisplayKey, out VisualElement heldView))
                heldView.EnableInClassList("action-v1-entry-hold", true);
        }
        RefreshStatus();
    }

    private void RefreshSelectionPresentation()
    {
        string primaryId = ActionV1EditorContext.Shared.PrimarySelection.EditorId;
        foreach (KeyValuePair<string, VisualElement> pair in _entryViews)
        {
            ActionV1DocumentEntry entry = _entriesByDisplayKey[pair.Key];
            bool selected = entry.HasStableSelection && ActionV1EditorContext.Shared.IsSelected(entry.EditorId);
            bool primary = selected && string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal);
            bool transient = string.Equals(_transientDisplayKey, entry.DisplayKey, StringComparison.Ordinal);
            pair.Value.EnableInClassList("action-v1-entry-selected", selected);
            pair.Value.EnableInClassList("action-v1-entry-primary", primary);
            pair.Value.EnableInClassList("action-v1-entry-transient", transient);
        }
        foreach (KeyValuePair<string, VisualElement> pair in _laneHeaderViews)
        {
            ActionV1DocumentEntry lane = _document.Lanes.FirstOrDefault(entry => entry.DisplayKey == pair.Key);
            bool selected = lane != null && lane.HasStableSelection && ActionV1EditorContext.Shared.IsSelected(lane.EditorId);
            bool transient = string.Equals(_transientDisplayKey, pair.Key, StringComparison.Ordinal);
            pair.Value.EnableInClassList("action-v1-lane-header-selected", selected || transient);
        }
        RefreshPointDensityPresentation(false);
        RefreshOverlapMarkerPresentation(_geometry.ContentWidth);
        RefreshEntryLayering();
        FinalizeLayerOrder();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (_document == null)
            return;
        int issues = _document.Validation?.Issues.Count ?? 0;
        string overflow = _geometryOverflowSentinel != null &&
                          _geometryOverflowSentinel.resolvedStyle.display != DisplayStyle.None
            ? "  ·  display clipped at safe geometry limit"
            : string.Empty;
        string notice = string.IsNullOrEmpty(_statusNotice) ? string.Empty : $"  ·  {_statusNotice}";
        _statusLabel.text = $"Duration {_document.DurationFrames}f  ·  Horizon {_document.HorizonFrames}f  ·  {_document.Lanes.Count} gameplay lanes  ·  {ActionV1EditorContext.Shared.SelectedIds.Count} selected  ·  {issues} issues{overflow}{notice}";
        _readinessPill.text = ActionV1EditorTheme.ReadinessLabel(_document.Readiness);
        _readinessPill.RemoveFromClassList("action-v1-pill-neutral");
        _readinessPill.RemoveFromClassList("action-v1-pill-ready");
        _readinessPill.RemoveFromClassList("action-v1-pill-warning");
        _readinessPill.RemoveFromClassList("action-v1-pill-error");
        _readinessPill.AddToClassList(ReadinessClass(_document.Readiness));
    }

    private bool CanAuthor(out string message)
    {
        if (HasActivePointerGesture)
        {
            message = "Finish or cancel the active Timeline gesture first.";
            return false;
        }
        if (_document?.Asset == null || !_document.HasTimeline)
        {
            message = "Choose an ActionAsset with V1 Timeline data.";
            return false;
        }
        if (_document.Readiness == ActionV1EditorReadiness.IdentityBlocked)
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
        if (evt == null || owner == null || _interaction != InteractionState.Idle ||
            !ActionV1EditorInteractionGate.TryAcquire(this, evt.pointerId))
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
        GameplayLane lane = ActionV1EditorCommands.AddLane(_document.Asset);
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
        bool committed = ActionV1EditorCommands.AddAnimationSegment(_document.Asset, selected, frame, out string message);
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
        if (canvasPosition.y < ActionV1EditorTheme.AnimationLaneHeight)
        {
            var menu = new GenericMenu();
            AddAuthoringMenuItem(menu, "Add Animation Segment…", () => BeginAnimationPicker(frame));
            menu.ShowAsContext();
            return;
        }

        int laneIndex = Mathf.FloorToInt((canvasPosition.y - ActionV1EditorTheme.AnimationLaneHeight) /
                                         ActionV1EditorTheme.GameplayLaneHeight);
        ActionV1DocumentEntry lane = _document?.Lanes.FirstOrDefault(candidate => candidate.LaneIndex == laneIndex);
        if (lane != null)
            ShowItemCreationMenu(lane, frame);
    }

    private void ShowItemCreationMenu(ActionV1DocumentEntry lane, int frame)
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

    private void AddGameplayItem(ActionV1DocumentEntry lane, Type itemType, int frame)
    {
        lane = ResolveCurrentEntry(lane);
        if (!CanAuthor(out string message) || lane == null || !lane.HasStableSelection)
        {
            SetStatusNotice(string.IsNullOrEmpty(message) ? "The target Lane is not editable." : message);
            return;
        }
        GameplayItem item = ActionV1EditorCommands.AddItem(_document.Asset, lane.EditorId, itemType, frame, out message);
        SetStatusNotice(item != null ? $"Added {itemType.Name} at Frame {frame}." : message);
        if (item != null) RevealById(item.EditorId);
    }

    private void ShowLaneMenu(ActionV1DocumentEntry lane)
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
                foreach (ActionV1DocumentEntry entry in cluster.Entries.OrderBy(item => item.ItemIndex))
                    AddNavigationItem(menu, "Dense Points", entry);
            }
        }
        if (lane != null)
            AppendOverlapNavigation(menu, lane.LaneIndex, false);
        menu.ShowAsContext();
    }

    private void ShowLaneNavigationMenu(int laneIndex, bool animation)
    {
        if (HasActivePointerGesture)
            return;
        FocusTimelineCommands();
        var menu = new GenericMenu();
        if (!animation && _densityClustersByLane.TryGetValue(laneIndex, out List<PointDensityCluster> clusters))
        {
            foreach (PointDensityCluster cluster in clusters)
            foreach (ActionV1DocumentEntry entry in cluster.Entries.OrderBy(item => item.ItemIndex))
                AddNavigationItem(menu, "Dense Points", entry);
        }
        AppendOverlapNavigation(menu, laneIndex, animation);
        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("No dense or overlapping content"));
        menu.ShowAsContext();
    }

    private void AppendOverlapNavigation(GenericMenu menu, int laneIndex, bool animation)
    {
        foreach (ActionV1OverlapRegion region in _document.OverlapRegions.Where(region =>
                     region.IsAnimation == animation && (animation || region.LaneIndex == laneIndex)))
        foreach (ActionV1DocumentEntry entry in region.Entries)
            AddNavigationItem(menu, "Time Overlap", entry);
    }

    private void AddNavigationItem(GenericMenu menu, string group, ActionV1DocumentEntry entry)
    {
        if (entry == null)
            return;
        ActionV1DocumentEntry captured = entry;
        string time = entry.Source is PointGameplayItem
            ? $"Frame {entry.StartFrame}"
            : $"[{entry.StartFrame}, {entry.RawEndFrameExclusive})";
        string label = $"{group}/{entry.DisplayName} | {time} | {entry.AuthoringPath}";
        menu.AddItem(new GUIContent(label),
            entry.HasStableSelection && ActionV1EditorContext.Shared.IsSelected(entry.EditorId),
            () => NavigateToEntry(captured, true));
    }

    private void StartRenameFromHeader(ActionV1DocumentEntry lane)
    {
        lane = ResolveCurrentEntry(lane);
        if (lane == null || !_laneHeaderViews.TryGetValue(lane.DisplayKey, out VisualElement row))
            return;
        Label title = row.Q<Label>(className: "action-v1-lane-title");
        BeginInlineRename(lane, row, title);
    }

    private void BeginInlineRename(ActionV1DocumentEntry lane, VisualElement row, Label title)
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
        _renameField.AddToClassList("action-v1-inline-rename");
        row.Insert(0, _renameField);
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
        ActionV1DocumentEntry lane = _renamingLane;
        FinishInlineRenameVisual();
        bool committed = lane != null && ActionV1EditorCommands.RenameLane(_document?.Asset, lane.EditorId, value);
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
        ActionV1EditorInteractionGate.Release(this);
        VisualElement parent = field?.parent;
        field?.RemoveFromHierarchy();
        Label title = parent?.Q<Label>(className: "action-v1-lane-title");
        if (title != null) title.style.display = DisplayStyle.Flex;
        FocusTimelineCommands();
    }

    private void SetMuted(ActionV1DocumentEntry entry, bool muted)
    {
        entry = ResolveCurrentEntry(entry);
        if (!CanAuthor(out string message) || entry == null || !entry.HasStableSelection)
        {
            SetStatusNotice(string.IsNullOrEmpty(message) ? "The selected object is not editable." : message);
            return;
        }
        bool committed = ActionV1EditorCommands.SetMuted(_document.Asset, entry.EditorId, muted);
        SetStatusNotice(committed ? (muted ? "Muted content." : "Unmuted content.") : "Mute state was unchanged.");
    }

    private void ReorderLane(ActionV1DocumentEntry lane, int delta)
    {
        lane = ResolveCurrentEntry(lane);
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        bool committed = lane != null && ActionV1EditorCommands.ReorderLane(_document.Asset, lane.EditorId, delta);
        SetStatusNotice(committed ? "Reordered Gameplay Lane." : "Lane cannot move farther in that direction.");
    }

    private void SetStatusNotice(string message)
    {
        _statusNotice = message;
        RefreshStatus();
    }

    private void RevealPrimarySelection()
    {
        RevealById(ActionV1EditorContext.Shared.PrimarySelection.EditorId);
    }

    private void RevealById(string editorId)
    {
        if (string.IsNullOrEmpty(editorId))
            return;
        rootVisualElement.schedule.Execute(() =>
        {
            if (!HasActivePointerGesture && _document != null && _document.ById.TryGetValue(editorId, out ActionV1DocumentEntry entry))
            {
                if (entry.SelectionKind == ActionV1SelectionKind.GameplayLane)
                    ApplyVerticalScroll(Mathf.Max(0f, RowTop(entry) - 24f), true);
                else
                    FrameEntry(entry);
            }
        });
    }

    private void OnEntryPointerDown(PointerDownEvent evt, ActionV1DocumentEntry entry, VisualElement view)
    {
        if (HasActivePointerGesture)
        {
            evt.StopImmediatePropagation();
            return;
        }
        FocusTimelineCommands();
        if (entry?.Source is GameplayItem)
        {
            ActionV1DocumentEntry nearest = ResolveSemanticGameplayHit(entry, evt.position);
            if (nearest != null && _entryViews.TryGetValue(nearest.DisplayKey, out VisualElement nearestView))
            {
                entry = nearest;
                view = nearestView;
            }
        }
        if (evt.button == 1)
        {
            if (entry.HasStableSelection && !ActionV1EditorContext.Shared.IsSelected(entry.EditorId))
                ActionV1EditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, false, false);
            ShowEntryMenu(entry);
            evt.StopPropagation();
            return;
        }
        if (evt.button != 0)
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
        bool wasSelected = ActionV1EditorContext.Shared.IsSelected(entry.EditorId);
        if (!wasSelected || toggle || evt.shiftKey)
            ActionV1EditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, additive, toggle);

        if (evt.clickCount >= 2)
        {
            ActionV1DetailsWindow.OpenShared();
            evt.StopPropagation();
            return;
        }

        if (!toggle && ActionV1EditorContext.Shared.IsSelected(entry.EditorId))
            BeginManipulation(evt, entry, view);
        evt.StopPropagation();
    }

    private void ShowEntryMenu(ActionV1DocumentEntry entry)
    {
        if (HasActivePointerGesture)
            return;
        var menu = new GenericMenu();
        if (entry?.Source == null && _document?.Readiness != ActionV1EditorReadiness.IdentityBlocked)
        {
            menu.AddItem(new GUIContent("Delete quarantined null entry"), false, () => DeleteNullEntry(entry));
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

    private ActionV1DocumentEntry ResolveSemanticGameplayHit(ActionV1DocumentEntry fallback, Vector2 worldPosition)
    {
        if (_geometry == null || _contentCanvas == null || fallback == null)
            return fallback;
        Vector2 pointer = _contentCanvas.WorldToLocal(worldPosition);
        List<ActionV1DocumentEntry> candidates = _document.GameplayItems
            .Where(candidate => candidate.Source is GameplayItem && candidate.LaneIndex == fallback.LaneIndex &&
                                _entryPresentation.TryGetValue(candidate.DisplayKey, out EntryPresentation presentation) &&
                                presentation.Displayed)
            .ToList();
        var visualHits = candidates.Select(candidate =>
            IsInsideVisibleShape(_entryPresentation[candidate.DisplayKey], pointer)).ToList();
        var expandedHits = candidates.Select(candidate =>
            _entryPresentation[candidate.DisplayKey].HitRect.Contains(pointer)).ToList();
        int index = ActionV1TimelineInteractionMath.ChooseHitIndex(
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

    private int EntryLayerRank(ActionV1DocumentEntry entry)
    {
        if (string.Equals(entry.DisplayKey, _hoverDisplayKey, StringComparison.Ordinal)) return 4;
        string primaryId = ActionV1EditorContext.Shared.PrimarySelection.EditorId;
        if (entry.HasStableSelection && string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal)) return 3;
        if (entry.HasStableSelection && ActionV1EditorContext.Shared.IsSelected(entry.EditorId)) return 2;
        return 1;
    }

    private void DeleteNullEntry(ActionV1DocumentEntry entry)
    {
        entry = ResolveCurrentEntry(entry);
        if (!CanAuthor(out string message) || entry?.Source != null)
        {
            SetStatusNotice(string.IsNullOrEmpty(message) ? "The quarantine entry is no longer valid." : message);
            return;
        }
        bool committed = ActionV1EditorCommands.DeleteNullEntry(
            _document.Asset, entry.SelectionKind, entry.LaneIndex, entry.ItemIndex, out message);
        SetStatusNotice(committed ? $"Deleted {entry.AuthoringPath}." : message);
    }

    private void BeginManipulation(PointerDownEvent evt, ActionV1DocumentEntry entry, VisualElement view)
    {
        if (!CanAuthor(out string message) || entry.Source == null)
        {
            if (!string.IsNullOrEmpty(message)) SetStatusNotice(message);
            return;
        }

        VisualElement rangeBody = view.Q<VisualElement>(className: "action-v1-range-body");
        float visualWidth = rangeBody != null ? rangeBody.layout.width : view.layout.width;
        float localPointerX = rangeBody != null
            ? rangeBody.WorldToLocal(evt.position).x
            : view.WorldToLocal(evt.position).x;
        float edge = Mathf.Min(8f, Mathf.Max(4f, visualWidth * 0.25f));
        bool canResolveEdge = visualWidth >= 16f;
        bool left = canResolveEdge && localPointerX <= edge;
        bool right = canResolveEdge && localPointerX >= visualWidth - edge;
        InteractionState next = InteractionState.Move;
        if (entry.Source is AnimationSegment animation && CanTrim(animation))
            next = left ? InteractionState.TrimLeft : right ? InteractionState.TrimRight : InteractionState.Move;
        else if (entry.Source is RangeGameplayItem range && range.DurationFrames > 0)
            next = left ? InteractionState.ResizeLeft : right ? InteractionState.ResizeRight : InteractionState.Move;

        if (next != InteractionState.Move)
            ActionV1EditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, false, false);

        _manipulationEntries.Clear();
        IEnumerable<ActionV1DocumentEntry> selected = next == InteractionState.Move
            ? _document.ContentEntries.Where(candidate => candidate.HasStableSelection && ActionV1EditorContext.Shared.IsSelected(candidate.EditorId))
            : new[] { entry };
        foreach (ActionV1DocumentEntry selectedEntry in selected)
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
                LaneId = selectedEntry.LaneId,
                AnimationAsset = selectedEntry.Source is AnimationSegment snapshotAnimation ? snapshotAnimation.AnimationAsset : null,
                SourceStartTime = selectedEntry.Source is AnimationSegment sourceAnimation ? sourceAnimation.SourceStartTime : 0f,
                SourceEndTime = selectedEntry.Source is AnimationSegment endAnimation ? endAnimation.SourceEndTime : 0f,
                PlayRate = selectedEntry.Source is AnimationSegment rateAnimation ? rateAnimation.PlayRate : 0f,
            });
        }
        if (_manipulationEntries.Count == 0)
            return;

        List<string> targetIds = _manipulationEntries.Select(item => item.Entry.EditorId).ToList();
        _operationSnapshot = ActionV1TimelineOperationSnapshot.Capture(_document, OperationKind(next), targetIds);
        if (_operationSnapshot == null || !TryBeginPointerGesture(evt, next, view))
        {
            _manipulationEntries.Clear();
            _operationSnapshot = null;
            return;
        }
        _manipulationPrimary = entry;
        _gestureStart = _contentCanvas.WorldToLocal(evt.position);
        _gestureCurrent = _gestureStart;
        _manipulationPixelsPerFrame = Math.Max(0.001d, _geometry.PixelsPerFrame);
        _previewFrameDelta = 0;
        _previewLaneDelta = 0;
        _operationResult = _operationSnapshot.Evaluate(0, 0);
        _previewValid = _operationResult.State != ActionV1TimelineOperationState.Rejected;
        _previewMessage = _operationResult.Message;
        StopPlayback();
        BuildManipulationGhosts();
    }

    private void OnManipulationPointerMove(PointerMoveEvent evt)
    {
        if (!IsManipulating || evt.pointerId != _capturedPointer)
            return;
        _gestureCurrent = _contentCanvas.WorldToLocal(evt.position);
        double deltaValue = (_gestureCurrent.x - _gestureStart.x) / _manipulationPixelsPerFrame;
        _previewFrameDelta = deltaValue >= int.MaxValue ? int.MaxValue : deltaValue <= int.MinValue
            ? int.MinValue
            : (int)Math.Round(deltaValue, MidpointRounding.AwayFromZero);
        _previewLaneDelta = CalculateLaneDelta(_gestureCurrent.y);
        EvaluateManipulation();
        UpdateManipulationGhosts();
        evt.StopPropagation();
    }

    private void OnManipulationPointerUp(PointerUpEvent evt)
    {
        if (!IsManipulating || evt.pointerId != _capturedPointer)
            return;
        ActionV1TimelineOperationSnapshot snapshot = _operationSnapshot;
        ActionV1TimelineOperationResult result = _operationResult;
        ReleaseManipulationCapture();
        ClearManipulation();
        bool committed = ActionV1EditorCommands.CommitTimelineOperation(snapshot, result, out string message);
        SetStatusNotice(committed ? "Timeline timing updated." : string.IsNullOrEmpty(message) ? "No timing change." : message);
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
        int target = pointerY < ActionV1EditorTheme.AnimationLaneHeight
            ? -1
            : Mathf.FloorToInt((pointerY - ActionV1EditorTheme.AnimationLaneHeight) / ActionV1EditorTheme.GameplayLaneHeight);
        return target - _manipulationPrimary.LaneIndex;
    }

    private void EvaluateManipulation()
    {
        _operationResult = _operationSnapshot?.Evaluate(_previewFrameDelta, _previewLaneDelta);
        _previewValid = _operationResult != null && _operationResult.State != ActionV1TimelineOperationState.Rejected;
        _previewMessage = _operationResult?.Message ?? "Timeline operation cannot be evaluated.";
    }

    private void BuildManipulationGhosts()
    {
        _ghostLayer.Clear();
        foreach (ManipulationEntry item in _manipulationEntries)
        {
            if (_entryViews.TryGetValue(item.Entry.DisplayKey, out VisualElement sourceView))
                sourceView.style.visibility = Visibility.Hidden;
            VisualElement ghost;
            if (item.Entry.Source is PointGameplayItem)
            {
                ghost = new VisualElement { pickingMode = PickingMode.Ignore };
                ghost.AddToClassList("action-v1-point-ghost");
                var diamond = new VisualElement { pickingMode = PickingMode.Ignore };
                diamond.AddToClassList("action-v1-point-diamond");
                ghost.Add(diamond);
                var label = new Label(EntryLabel(item.Entry, false)) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("action-v1-point-label");
                ghost.Add(label);
            }
            else
            {
                ghost = new Label(EntryLabel(item.Entry, false)) { pickingMode = PickingMode.Ignore };
            }
            ghost.AddToClassList("action-v1-drag-ghost");
            ghost.AddToClassList(item.Entry.Source is AnimationSegment ? "action-v1-animation-entry" : ItemClass(item.Entry.Source));
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
            ActionV1TimelineOperationCandidate candidate = _operationResult?.ForSource(item.Entry.Source);
            if (candidate != null)
            {
                start = candidate.StartFrame;
                duration = Math.Max(1, candidate.DurationFrames);
                laneIndex = candidate.LaneIndex;
            }

            float left = _geometry.FrameToPixel(Math.Max(0L, start), out _);
            float right = _geometry.FrameToPixel(Math.Max(0L, start + duration), out _);
            bool point = item.Entry.Source is PointGameplayItem;
            float ghostWidth = point
                ? ActionV1TimelineGeometry.PointHitWidth
                : Mathf.Max(1f, right - left);
            float ghostLeft = point
                ? Mathf.Clamp(left - ghostWidth * 0.5f, 0f, Mathf.Max(0f, _geometry.ContentWidth - ghostWidth))
                : left;
            item.Ghost.style.left = ghostLeft;
            item.Ghost.style.top = _geometry.LaneTop(laneIndex) + 3f;
            item.Ghost.style.width = ghostWidth;
            item.Ghost.style.height = Mathf.Max(18f, (laneIndex < 0 ? ActionV1EditorTheme.AnimationLaneHeight : ActionV1EditorTheme.GameplayLaneHeight) - 6f);
            if (point)
            {
                float localAnchor = Mathf.Clamp(left - ghostLeft, 0f, ghostWidth);
                VisualElement diamond = item.Ghost.Q<VisualElement>(className: "action-v1-point-diamond");
                if (diamond != null)
                    diamond.style.left = Mathf.Clamp(localAnchor - 5f, 0f, ghostWidth - 10f);
            }
            item.Ghost.EnableInClassList("action-v1-drag-invalid", !_previewValid);
        }
        SetStatusNotice(_previewValid
            ? $"{_interaction}  ·  ΔFrame {_previewFrameDelta}  ·  ΔLane {_previewLaneDelta}"
            : _previewMessage);
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

    private void ClearManipulation()
    {
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
        _manipulationPixelsPerFrame = 0d;
        _operationSnapshot = null;
        _operationResult = null;
        _interaction = InteractionState.Idle;
        ActionV1EditorInteractionGate.Release(this);
        if (_geometry != null)
        {
            RefreshPointDensityPresentation(false);
            RefreshOverlapMarkerPresentation(_geometry.ContentWidth);
        }
    }

    private static ActionV1TimelineOperationKind OperationKind(InteractionState state)
    {
        switch (state)
        {
            case InteractionState.ResizeLeft: return ActionV1TimelineOperationKind.ResizeLeft;
            case InteractionState.ResizeRight: return ActionV1TimelineOperationKind.ResizeRight;
            case InteractionState.TrimLeft: return ActionV1TimelineOperationKind.TrimLeft;
            case InteractionState.TrimRight: return ActionV1TimelineOperationKind.TrimRight;
            default: return ActionV1TimelineOperationKind.Move;
        }
    }

    private void CopySelection()
    {
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        bool copied = ActionV1EditorCommands.Copy(_document.Asset, ActionV1EditorContext.Shared.SelectedIds, out message);
        SetStatusNotice(copied ? "Copied Timeline content." : message);
    }

    private void PasteClipboard()
    {
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        bool committed = ActionV1EditorCommands.Paste(_document.Asset, ActionV1EditorContext.Shared.CurrentFrame, out message);
        SetStatusNotice(committed ? $"Pasted Timeline content at Frame {ActionV1EditorContext.Shared.CurrentFrame}." : message);
        if (committed) RevealPrimarySelection();
    }

    private void DuplicateSelection()
    {
        if (!CanAuthor(out string message))
        {
            SetStatusNotice(message);
            return;
        }
        bool committed = ActionV1EditorCommands.Duplicate(_document.Asset, ActionV1EditorContext.Shared.SelectedIds, out message);
        SetStatusNotice(committed ? "Duplicated Timeline content." : message);
        if (committed) RevealPrimarySelection();
    }

    private void DeleteCurrentSelection()
    {
        DeleteEntries(ActionV1EditorContext.Shared.SelectedIds);
    }

    private void DeleteEntries(IReadOnlyList<string> ids)
    {
        if (!CanAuthor(out string message) || ids == null || ids.Count == 0)
        {
            SetStatusNotice(string.IsNullOrEmpty(message) ? "Select content to delete." : message);
            return;
        }
        List<ActionV1DocumentEntry> entries = ids.Where(id => _document.ById.ContainsKey(id)).Select(id => _document.ById[id]).ToList();
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
        bool committed = ActionV1EditorCommands.DeleteSelection(_document.Asset, ids, true, out message);
        SetStatusNotice(committed ? "Deleted Timeline content." : message);
    }

    private void SelectEntry(PointerDownEvent evt, ActionV1DocumentEntry entry, bool focusCommands)
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
            ActionV1EditorContext.Shared.Select(entry.SelectionKind, entry.EditorId, additive, toggle);
            if (evt.clickCount >= 2)
                ActionV1DetailsWindow.OpenShared();
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
            _panning = true;
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
        _marqueeSelecting = true;
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
        if (_panning)
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
        if (_marqueeSelecting)
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
        if (_panning)
        {
            _panning = false;
            _interaction = InteractionState.Idle;
            ActionV1EditorInteractionGate.Release(this);
            evt.StopPropagation();
            return;
        }
        if (!_marqueeSelecting)
            return;

        _marqueeSelecting = false;
        _interaction = InteractionState.Idle;
        ActionV1EditorInteractionGate.Release(this);
        _marquee.style.display = DisplayStyle.None;
        Rect marqueeRect = Rect.MinMaxRect(
            Mathf.Min(_gestureStart.x, _gestureCurrent.x),
            Mathf.Min(_gestureStart.y, _gestureCurrent.y),
            Mathf.Max(_gestureStart.x, _gestureCurrent.x),
            Mathf.Max(_gestureStart.y, _gestureCurrent.y));
        if (marqueeRect.width < 3f && marqueeRect.height < 3f)
        {
            _transientDisplayKey = null;
            _statusNotice = null;
            ActionV1EditorContext.Shared.SelectAction();
        }
        else
        {
            _transientDisplayKey = null;
            _statusNotice = null;
            List<ActionV1DocumentEntry> hits = _document.ContentEntries
                .Where(entry => entry.HasStableSelection && marqueeRect.Overlaps(SelectionRect(entry), true))
                .ToList();
            ActionV1EditorContext.Shared.SetSelection(hits, _marqueeAdditive);
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
        _scrubbing = true;
        ScrubAt(evt.localPosition.x);
        evt.StopPropagation();
    }

    private void ContinueScrub(PointerMoveEvent evt)
    {
        if (!_scrubbing || evt.pointerId != _capturedPointer)
            return;
        ScrubAt(evt.localPosition.x);
        evt.StopPropagation();
    }

    private void EndScrub(PointerUpEvent evt)
    {
        if (!_scrubbing || evt.pointerId != _capturedPointer)
            return;
        _scrubbing = false;
        _interaction = InteractionState.Idle;
        ActionV1EditorInteractionGate.Release(this);
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
        ActionV1EditorContext.Shared.SetFrame(_geometry != null ? _geometry.PixelToFrame(localX) : 0);
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
        double ratio = (viewportX - ActionV1TimelineGeometry.HorizontalPresentationInset) / usableWidth;
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
        _resizingHeader = true;
        _gestureStart = evt.position;
        _panStartScroll = new Vector2(_headerWidth, 0f);
        _headerResizeRootSize = new Vector2(rootVisualElement.resolvedStyle.width, rootVisualElement.resolvedStyle.height);
        evt.StopPropagation();
    }

    private void ContinueHeaderResize(PointerMoveEvent evt)
    {
        var handle = (VisualElement)evt.currentTarget;
        if (evt.pointerId != _capturedPointer || !handle.HasPointerCapture(evt.pointerId)) return;
        _headerWidth = Mathf.Clamp(_panStartScroll.x + evt.position.x - _gestureStart.x, ActionV1EditorTheme.MinHeaderWidth, ActionV1EditorTheme.MaxHeaderWidth);
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
        _resizingHeader = false;
        _captureTarget = null;
        _capturedPointer = -1;
        _interaction = InteractionState.Idle;
        ActionV1EditorInteractionGate.Release(this);
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
                ActionV1NavigationRange range = ActionV1TimelineInteractionMath.Pan(
                    _navigatorStartExtent, _navigatorStartFrame, _navigatorStartSpan, frameDelta);
                _timeViewport.SetRange(range.Start, range.Span);
                CommitTimeViewport();
                break;
            }
            case InteractionState.TimeNavigatorResizeLeft:
            {
                ActionV1NavigationRange range = ActionV1TimelineInteractionMath.ResizeLeft(
                    _navigatorStartExtent, _navigatorStartFrame, _navigatorStartSpan, minimumSpan, frameDelta);
                SetVisibleRange(range.Start, range.Span);
                break;
            }
            case InteractionState.TimeNavigatorResizeRight:
            {
                ActionV1NavigationRange range = ActionV1TimelineInteractionMath.ResizeRight(
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
        ActionV1EditorInteractionGate.Release(this);
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
        float value = _geometry.FrameToPixel(ActionV1EditorContext.Shared.CurrentFrame, out _);
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
        if (_issuesFoldout != null)
            _issuesExpanded = _issuesFoldout.value;
    }

    private void DrawRuler()
    {
        if (_document == null || _geometry == null || Event.current.type != EventType.Repaint)
            return;
        float height = ActionV1EditorTheme.RulerHeight;
        ActionV1VisibleFrameRange visible = _geometry.VisibleFrames();
        float visibleLeft = 0f;
        float visibleRight = _geometry.ContentWidth;
        EditorGUI.DrawRect(new Rect(visibleLeft, height - 1f, Mathf.Max(1f, visibleRight - visibleLeft), 1f), new Color(0.32f, 0.34f, 0.38f));
        GetTickSteps(72f, out int major, out int minor);
        GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.67f, 0.7f, 0.75f) },
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
                isMajor ? new Color(0.46f, 0.49f, 0.55f) : new Color(0.36f, 0.39f, 0.44f));
            if (isMajor)
                GUI.Label(new Rect(x + 4f, 2f, 60f, 18f), frame.ToString(), style);
        }

        float durationX = _geometry.FrameToPixel(_document.DurationFrames, out bool durationOverflow);
        if (!durationOverflow && durationX >= visibleLeft - 1f && durationX <= visibleRight + 1f)
            EditorGUI.DrawRect(new Rect(durationX, 0f, 1f, height), new Color(0.35f, 0.72f, 0.92f, 0.8f));
        float horizonX = _geometry.FrameToPixel(_document.HorizonFrames, out bool horizonOverflow);
        if (!horizonOverflow && horizonX >= visibleLeft - 1f && horizonX <= visibleRight + 1f)
            EditorGUI.DrawRect(new Rect(horizonX, 0f, 1f, height), new Color(0.58f, 0.62f, 0.69f, 0.55f));
        if (visible.First == 0)
        {
            float frameZeroX = _geometry.FrameToPixel(0, out _);
            EditorGUI.DrawRect(new Rect(frameZeroX, 0f, 2f, height), new Color(0.7f, 0.73f, 0.78f, 0.8f));
        }
    }

    private void DrawGrid()
    {
        if (_document == null || _geometry == null || Event.current.type != EventType.Repaint)
            return;
        float height = _geometry.ContentHeight;
        ActionV1VisibleFrameRange visible = _geometry.VisibleFrames();
        float visibleLeft = 0f;
        float visibleRight = _geometry.ContentWidth;
        GetTickSteps(72f, out int major, out int minor);
        long firstTick = ((long)visible.First + minor - 1L) / minor * minor;
        for (long frame = firstTick; frame < visible.LastExclusive; frame += minor)
        {
            bool isMajor = frame % major == 0;
            Color color = isMajor ? new Color(0.3f, 0.32f, 0.36f, 0.72f) : new Color(0.27f, 0.28f, 0.31f, 0.36f);
            float x = _geometry.FrameToPixel(frame, out bool overflow);
            if (overflow)
                break;
            EditorGUI.DrawRect(new Rect(x, 0f, 1f, height), color);
        }
        float horizontalWidth = Mathf.Max(1f, visibleRight - visibleLeft);
        float animationBottom = ActionV1EditorTheme.AnimationLaneHeight;
        float verticalOffset = _contentScroll?.scrollOffset.y ?? 0f;
        float viewportHeight = ResolvedSize(_contentScroll?.contentViewport, false);
        if (animationBottom >= verticalOffset - 1f && animationBottom <= verticalOffset + viewportHeight + 1f)
            EditorGUI.DrawRect(new Rect(visibleLeft, animationBottom - 1f, horizontalWidth, 1f), new Color(0.35f, 0.37f, 0.41f));
        int firstLane = Mathf.Clamp(
            Mathf.FloorToInt((verticalOffset - animationBottom) / ActionV1EditorTheme.GameplayLaneHeight),
            0,
            _document.Lanes.Count);
        int lastLane = Mathf.Clamp(
            Mathf.CeilToInt((verticalOffset + viewportHeight - animationBottom) / ActionV1EditorTheme.GameplayLaneHeight) + 1,
            firstLane,
            _document.Lanes.Count);
        for (int lane = firstLane; lane < lastLane; lane++)
        {
            float y = animationBottom + (lane + 1) * ActionV1EditorTheme.GameplayLaneHeight;
            EditorGUI.DrawRect(new Rect(visibleLeft, y - 1f, Mathf.Max(1f, visibleRight - visibleLeft), 1f), new Color(0.27f, 0.29f, 0.32f));
        }
        if (visible.First == 0)
        {
            float frameZeroX = _geometry.FrameToPixel(0, out _);
            EditorGUI.DrawRect(new Rect(frameZeroX, 0f, 2f, height), new Color(0.52f, 0.55f, 0.61f, 0.75f));
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
        List<ActionV1DocumentEntry> selected = ActionV1EditorContext.Shared.SelectedIds
            .Where(id => _document.ById.TryGetValue(id, out _))
            .Select(id => _document.ById[id])
            .Where(entry => entry.SelectionKind == ActionV1SelectionKind.AnimationSegment || entry.SelectionKind == ActionV1SelectionKind.GameplayItem)
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

    private void FrameEntry(ActionV1DocumentEntry entry)
    {
        if (entry == null) return;
        ResetWheelAnchor();
        double start = Math.Max(0d, entry.StartFrame);
        double end = Math.Max(start + 1d, entry.RawEndFrameExclusive);
        if (start < _timeViewport.VisibleStartFrame || end > _timeViewport.VisibleEndFrame)
            SetVisibleRange(start - _timeViewport.VisibleSpanFrames * 0.35d, _timeViewport.VisibleSpanFrames);
        ApplyVerticalScroll(Mathf.Max(0f, RowTop(entry) - 24f), true);
    }

    private float RowTop(ActionV1DocumentEntry entry) => _geometry?.LaneTop(entry?.LaneIndex ?? -1) ?? 0f;

    private void TogglePlayback()
    {
        if (HasActivePointerGesture)
            return;
        if (_playing) StopPlayback(); else StartPlayback();
    }

    private void RunTimelineNavigation(Action action)
    {
        if (!HasActivePointerGesture)
            action?.Invoke();
    }

    private void StartPlayback()
    {
        if (_playing || _document == null || _document.Asset == null) return;
        _playing = true;
        _lastPlaybackTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += PlaybackUpdate;
        _playButton.text = "Ⅱ";
    }

    private void StopPlayback()
    {
        if (!_playing) return;
        _playing = false;
        EditorApplication.update -= PlaybackUpdate;
        if (_playButton != null) _playButton.text = "▶";
    }

    private void PlaybackUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        int delta = Mathf.FloorToInt((float)((now - _lastPlaybackTime) * ActionTimelineData.FrameRate));
        if (delta <= 0) return;
        _lastPlaybackTime += delta / (double)ActionTimelineData.FrameRate;
        int last = Mathf.Max(0, (_document?.DurationFrames ?? 1) - 1);
        long nextValue = (long)ActionV1EditorContext.Shared.CurrentFrame + delta;
        if (nextValue > last)
        {
            if (ActionV1EditorContext.Shared.PreviewLoop)
                nextValue = last > 0 ? nextValue % ((long)last + 1L) : 0L;
            else
            {
                nextValue = last;
                StopPlayback();
            }
        }
        ActionV1EditorContext.Shared.SetFrame((int)Math.Max(0L, Math.Min(int.MaxValue, nextValue)));
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
            case KeyCode.LeftArrow: ActionV1EditorContext.Shared.SetFrame(ActionV1EditorContext.Shared.CurrentFrame - 1); evt.StopPropagation(); break;
            case KeyCode.RightArrow: ActionV1EditorContext.Shared.SetFrame(ActionV1EditorContext.Shared.CurrentFrame + 1); evt.StopPropagation(); break;
            case KeyCode.Escape:
                if (CancelInteraction()) evt.StopPropagation();
                break;
        }
    }

    private void OnContextChanged(ActionV1EditorChangeFlags flags)
    {
        if ((flags & (ActionV1EditorChangeFlags.Context | ActionV1EditorChangeFlags.Structure |
                      ActionV1EditorChangeFlags.Timing | ActionV1EditorChangeFlags.Presentation)) != 0)
        {
            CancelInteraction();
            if ((flags & ActionV1EditorChangeFlags.Context) != 0)
                CancelAnimationPicker();
            StopPlayback();
            RefreshDocumentAndViews();
            return;
        }
        if ((flags & (ActionV1EditorChangeFlags.Content | ActionV1EditorChangeFlags.Validation)) != 0)
        {
            RefreshValidationOnly();
            return;
        }
        if ((flags & ActionV1EditorChangeFlags.Frame) != 0) RefreshFramePresentation();
        if ((flags & ActionV1EditorChangeFlags.Selection) != 0) RefreshSelectionPresentation();
    }

    private void OnExternalDataChanged()
    {
        CancelInteraction();
        rootVisualElement?.schedule.Execute(RefreshDocumentAndViews);
    }

    private void OnRootGeometryChanged(GeometryChangedEvent evt)
    {
        if (_interaction == InteractionState.HeaderResize &&
            !Approximately(evt.newRect.size, _headerResizeRootSize))
            CancelInteraction();
        rootVisualElement.EnableInClassList("action-v1-compact", evt.newRect.width < 520f);
        if (_issuesList != null)
            _issuesList.style.maxHeight = Mathf.Max(80f, evt.newRect.height * 0.3f);
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
            previous.EnableInClassList("action-v1-entry-hover", false);
        _hoverDisplayKey = displayKey;
        if (!string.IsNullOrEmpty(_hoverDisplayKey) && _entryViews.TryGetValue(_hoverDisplayKey, out VisualElement current))
            current.EnableInClassList("action-v1-entry-hover", true);
        RefreshPointDensityPresentation(false);
        if (_geometry != null)
            RefreshOverlapMarkerPresentation(_geometry.ContentWidth);
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

        foreach (ActionV1DocumentEntry entry in _document.ContentEntries)
            if (_entryViews.TryGetValue(entry.DisplayKey, out VisualElement view)) view.BringToFront();

        string primaryId = ActionV1EditorContext.Shared.PrimarySelection.EditorId;
        foreach (ActionV1DocumentEntry entry in _document.ContentEntries)
        {
            if (!entry.HasStableSelection || !ActionV1EditorContext.Shared.IsSelected(entry.EditorId) ||
                string.Equals(entry.EditorId, primaryId, StringComparison.Ordinal))
                continue;
            if (_entryViews.TryGetValue(entry.DisplayKey, out VisualElement selected)) selected.BringToFront();
        }

        ActionV1DocumentEntry primaryEntry = _document.ContentEntries.FirstOrDefault(entry =>
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
        ActionV1EditorContext.Shared.SetSelection(
            _document.ContentEntries.Where(entry => entry.HasStableSelection),
            false);
    }

    private Rect SelectionRect(ActionV1DocumentEntry entry)
    {
        if (entry == null || !_entryViews.TryGetValue(entry.DisplayKey, out VisualElement view) || _geometry == null)
            return default;
        if (entry.DisplayState != ActionV1EntryDisplayState.Normal)
            return view.layout;

        float left = _geometry.FrameToPixel(Math.Max(0L, entry.StartFrame), out bool startOverflow);
        float right = _geometry.FrameToPixel(Math.Max(0L, entry.RawEndFrameExclusive), out bool endOverflow);
        if (startOverflow || endOverflow)
            return view.layout;
        float height = entry.LaneIndex < 0
            ? ActionV1EditorTheme.AnimationLaneHeight
            : ActionV1EditorTheme.GameplayLaneHeight;
        return new Rect(left, _geometry.LaneTop(entry.LaneIndex), Mathf.Max(0.5f, right - left), height);
    }

    private void PositionPointDiamond(VisualElement view, ActionV1DocumentEntry entry, ActionV1EntryGeometry layout)
    {
        float anchor = _geometry.FrameToPixel(Math.Max(0L, entry.StartFrame), out bool overflow);
        float localAnchor = overflow
            ? layout.Width * 0.5f
            : Mathf.Clamp(anchor - layout.Left, 0f, layout.Width);
        VisualElement diamond = view.Q<VisualElement>(className: "action-v1-point-diamond");
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
        bool active = manipulating || navigating || _scrubbing || _panning || _marqueeSelecting || _resizingHeader;
        if (!active)
            return false;

        if (manipulating)
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
        _scrubbing = false;
        _panning = false;
        _marqueeSelecting = false;
        _resizingHeader = false;
        _interaction = InteractionState.Idle;
        ActionV1EditorInteractionGate.Release(this);
        ClearManipulation();
        if (_marquee != null)
            _marquee.style.display = DisplayStyle.None;
        _statusNotice = "Interaction cancelled.";
        RefreshStatus();
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
        layer.AddToClassList("action-v1-content-layer");
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

    private static Button TransportButton(string text, string tooltip, Action clicked)
    {
        var button = new Button(clicked) { text = text, tooltip = tooltip };
        button.AddToClassList("action-v1-transport-button");
        return button;
    }

    private static Button LaneCommandButton(string text, string tooltip, Action clicked)
    {
        var button = new Button(clicked) { text = text, tooltip = tooltip, focusable = false };
        button.AddToClassList("action-v1-lane-command");
        button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        return button;
    }

    private static string OverlapTooltip(ActionV1OverlapRegion region)
    {
        string kind = region.IsAnimation ? "Invalid animation overlap" : "Concurrent gameplay content";
        return $"{kind}\nRaw [{region.StartFrame}, {region.EndFrameExclusive})\n" +
               $"Peak {region.PeakOverlapCount} · {region.Entries.Count} participating entr{(region.Entries.Count == 1 ? "y" : "ies")}\n" +
               "Navigation only · no priority";
    }

    private static string ItemClass(object source)
    {
        if (source is HitBoxItem) return "action-v1-item-hitbox";
        if (source is RootMotionItem) return "action-v1-item-root-motion";
        if (source is SelfRotationItem) return "action-v1-item-self-rotation";
        if (source is VelocityOverrideItem) return "action-v1-item-velocity";
        if (source is MotionPolicyItem) return "action-v1-item-motion-policy";
        if (source is TagItem) return "action-v1-item-tag";
        if (source is ImpulseItem) return "action-v1-item-impulse";
        return "action-v1-item-unknown";
    }

    private static void ApplyPlaceholderClasses(VisualElement view, ActionV1EntryDisplayState state)
    {
        bool placeholder = state != ActionV1EntryDisplayState.Normal;
        view.EnableInClassList("action-v1-placeholder", placeholder);
        view.EnableInClassList("action-v1-placeholder-null", (state & ActionV1EntryDisplayState.NullEntry) != 0);
        view.EnableInClassList("action-v1-placeholder-negative", (state & ActionV1EntryDisplayState.NegativeStart) != 0);
        view.EnableInClassList("action-v1-placeholder-duration", (state & ActionV1EntryDisplayState.InvalidDuration) != 0);
        view.EnableInClassList("action-v1-placeholder-source", (state & ActionV1EntryDisplayState.MissingSource) != 0);
        view.EnableInClassList("action-v1-placeholder-timing", (state & ActionV1EntryDisplayState.InvalidTiming) != 0);
    }

    private static string EntryLabel(ActionV1DocumentEntry entry, bool geometryOverflow)
    {
        if (entry == null)
            return "Null entry";
        if ((entry.DisplayState & ActionV1EntryDisplayState.NullEntry) != 0)
        {
            switch (entry.SelectionKind)
            {
                case ActionV1SelectionKind.AnimationSegment: return $"Null AnimationSegment #{entry.ItemIndex}";
                case ActionV1SelectionKind.GameplayLane: return $"Null GameplayLane #{entry.LaneIndex}";
                default: return $"Null GameplayItem #{entry.ItemIndex}";
            }
        }

        string value = entry.DisplayName;
        if ((entry.DisplayState & ActionV1EntryDisplayState.MissingSource) != 0)
            value = "Missing Animation Asset";
        else if ((entry.DisplayState & ActionV1EntryDisplayState.InvalidDuration) != 0)
            value = $"Invalid duration · {value}";

        if ((entry.DisplayState & ActionV1EntryDisplayState.NegativeStart) != 0)
            value = $"← {entry.StartFrame} · {value}";
        if (geometryOverflow)
            value = $"→ Frame {entry.StartFrame} · {value}";
        return value;
    }

    private static string EntryTooltip(
        ActionV1DocumentEntry entry,
        IReadOnlyList<ActionAuthoringValidationIssue> issues,
        bool geometryOverflow)
    {
        string firstIssue = issues != null && issues.Count > 0
            ? $"\n{issues[0].Code}: {issues[0].Message}"
            : string.Empty;
        ActionV1EntryDisplayState displayState = entry.DisplayState |
            (geometryOverflow ? ActionV1EntryDisplayState.GeometryOverflow : ActionV1EntryDisplayState.Normal);
        return $"{entry.DisplayName}\n{entry.AuthoringPath}\nRaw [{entry.StartFrame}, {entry.RawEndFrameExclusive})\n" +
               $"Display: {DisplayStateText(displayState)}\n{IdentityText(entry.IdentityState)}{firstIssue}";
    }

    private static string DisplayStateText(ActionV1EntryDisplayState state)
    {
        if (state == ActionV1EntryDisplayState.Normal)
            return "Normal";
        var labels = new List<string>();
        foreach (ActionV1EntryDisplayState value in Enum.GetValues(typeof(ActionV1EntryDisplayState)))
        {
            if (value != ActionV1EntryDisplayState.Normal && (state & value) != 0)
                labels.Add(value.ToString());
        }
        return string.Join(", ", labels);
    }

    private static string IdentityText(ActionV1EditorIdentityState state) => state == ActionV1EditorIdentityState.Valid
        ? "Stable Editor ID"
        : $"Identity: {state}";

    private static string ReadinessClass(ActionV1EditorReadiness readiness)
    {
        switch (readiness)
        {
            case ActionV1EditorReadiness.Ready: return "action-v1-pill-ready";
            case ActionV1EditorReadiness.EditableWithIssues: return "action-v1-pill-warning";
            case ActionV1EditorReadiness.IdentityBlocked: return "action-v1-pill-error";
            default: return "action-v1-pill-neutral";
        }
    }

    private static ActionV1ValidationSeverity? HighestSeverity(IReadOnlyList<ActionAuthoringValidationIssue> issues)
    {
        ActionV1ValidationSeverity? result = null;
        for (int i = 0; issues != null && i < issues.Count; i++)
        {
            ActionV1ValidationSeverity current = ActionV1ValidationIndex.SeverityOf(issues[i].Code);
            if (!result.HasValue || current < result.Value)
                result = current;
        }
        return result;
    }

    private static string SeverityClass(ActionV1ValidationSeverity severity)
    {
        switch (severity)
        {
            case ActionV1ValidationSeverity.IdentityBlock: return "action-v1-validation-identity";
            case ActionV1ValidationSeverity.NeedsSetup: return "action-v1-validation-setup";
            default: return "action-v1-validation-error";
        }
    }

    private static string SeverityLabel(ActionV1ValidationSeverity severity)
    {
        switch (severity)
        {
            case ActionV1ValidationSeverity.IdentityBlock: return "IDENTITY";
            case ActionV1ValidationSeverity.NeedsSetup: return "SETUP";
            default: return "ERROR";
        }
    }

    private static string Shorten(string value, int maximum)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maximum)
            return value ?? string.Empty;
        return value.Substring(0, Mathf.Max(1, maximum - 1)).TrimEnd() + "…";
    }

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
