#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Prototype editor: kept available while ActionSequence Editor V2 is built.
// Do not grow this window beyond compatibility fixes and baseline data support.
public sealed class ActionSequenceEditorWindow : EditorWindow
{
    private const float TrackHeight = 32f;
    private const float RulerHeight = 24f;
    private const float TrackHeaderWidth = 230f;
    private const float HorizontalScrollbarHeight = 16f;
    private const float MinPixelsPerFrame = 4f;
    private const float MaxPixelsPerFrame = 48f;
    private const int DefaultClipDurationFrames = 10;
    private const int AutoMinimumTimelineViewFrames = 60;

    private UnityEngine.Object _targetObject;
    private ActionSequenceData _sequenceData;
    private int _currentFrame;
    private float _pixelsPerFrame = 12f;
    private float _timelineScrollX;
    private bool _isPlaying;
    private double _lastPlaybackEditorTime;

    private ObjectField _targetField;
    private ToolbarMenu _addTrackMenu;
    private ToolbarButton _playPauseButton;
    private IntegerField _frameRateField;
    private EnumField _durationModeField;
    private IntegerField _durationFramesField;
    private IntegerField _currentFrameField;
    private Slider _zoomSlider;
    private Label _autoDurationLabel;
    private VisualElement _rulerContent;
    private ScrollView _trackScrollView;
    private Scroller _horizontalScroller;
    private bool _suppressHorizontalScrollerCallback;
    private ClipEditState _clipEdit;

    private enum ClipEditMode
    {
        None,
        Move,
        ResizeStart,
        ResizeEnd,
    }

    private sealed class ClipEditState
    {
        public ClipEditMode Mode;
        public int TrackIndex;
        public int ClipIndex;
        public int OriginalStartFrame;
        public int OriginalEndFrame;
        public int PointerStartFrame;
        public int Duration;
        public float ContentWidth;
        public bool UndoRecorded;
        public VisualElement Element;
        public Label Label;
        public int PointerId;

        public bool IsActive => Mode != ClipEditMode.None;
    }

    [MenuItem("Tools/Combat/Action Sequence Editor")]
    public static void Open()
    {
        GetWindow<ActionSequenceEditorWindow>("Action Sequence");
    }

    public static void Open(ActionAsset actionAsset)
    {
        var window = GetWindow<ActionSequenceEditorWindow>("Action Sequence");
        window.SetTarget(actionAsset);
    }

    public static void Open(ActionSequenceAsset sequenceAsset)
    {
        var window = GetWindow<ActionSequenceEditorWindow>("Action Sequence");
        window.SetTarget(sequenceAsset);
    }

    public static void RepaintAllOpenWindows()
    {
        ActionSequenceEditorWindow[] windows = Resources.FindObjectsOfTypeAll<ActionSequenceEditorWindow>();
        for (int i = 0; i < windows.Length; i++)
            windows[i].Refresh();
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        rootVisualElement.style.flexDirection = FlexDirection.Column;
        rootVisualElement.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);

        BuildToolbar();
        BuildTimelineShell();
        Refresh();
    }

    private void OnEnable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void BuildToolbar()
    {
        var toolbar = new Toolbar();
        rootVisualElement.Add(toolbar);

        _targetField = new ObjectField { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
        _targetField.style.minWidth = 220f;
        _targetField.RegisterValueChangedCallback(evt => SetTarget(evt.newValue));
        toolbar.Add(_targetField);

        _addTrackMenu = new ToolbarMenu { text = "Add Track" };
        BuildAddTrackMenu();
        toolbar.Add(_addTrackMenu);

        _playPauseButton = new ToolbarButton(TogglePlayback) { text = "Play" };
        toolbar.Add(_playPauseButton);

        toolbar.Add(new ToolbarButton(() =>
        {
            _isPlaying = false;
            _currentFrame = 0;
            UpdatePlaybackButton();
            RefreshTimelineOnly();
        }) { text = "Stop" });

        _frameRateField = new IntegerField("FPS");
        _frameRateField.style.width = 78f;
        _frameRateField.RegisterValueChangedCallback(evt =>
        {
            SetIntProperty("frameRate", Mathf.Max(1, evt.newValue), "Edit Action Sequence Frame Rate");
        });
        toolbar.Add(_frameRateField);

        _durationModeField = new EnumField("Mode", ActionSequenceDurationMode.FixedFrames);
        _durationModeField.style.width = 160f;
        _durationModeField.RegisterValueChangedCallback(evt =>
        {
            SetDurationMode((ActionSequenceDurationMode)evt.newValue);
        });
        toolbar.Add(_durationModeField);

        _durationFramesField = new IntegerField("Frames");
        _durationFramesField.style.width = 100f;
        _durationFramesField.RegisterValueChangedCallback(evt =>
        {
            SetIntProperty("durationFrames", Mathf.Max(1, evt.newValue), "Edit Action Sequence Duration");
        });
        toolbar.Add(_durationFramesField);

        _autoDurationLabel = new Label();
        _autoDurationLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        _autoDurationLabel.style.minWidth = 70f;
        toolbar.Add(_autoDurationLabel);

        _currentFrameField = new IntegerField("Frame");
        _currentFrameField.style.width = 92f;
        _currentFrameField.RegisterValueChangedCallback(evt =>
        {
            _currentFrame = Mathf.Clamp(evt.newValue, 0, Mathf.Max(0, GetEditorTimelineDuration() - 1));
            RefreshTimelineOnly();
        });
        toolbar.Add(_currentFrameField);

        _zoomSlider = new Slider("Zoom", MinPixelsPerFrame, MaxPixelsPerFrame);
        _zoomSlider.style.width = 150f;
        _zoomSlider.value = _pixelsPerFrame;
        _zoomSlider.RegisterValueChangedCallback(evt =>
        {
            _pixelsPerFrame = Mathf.Clamp(evt.newValue, MinPixelsPerFrame, MaxPixelsPerFrame);
            RefreshTimelineOnly();
        });
        toolbar.Add(_zoomSlider);

        toolbar.Add(new ToolbarButton(FitTimelineToWindow) { text = "Fit" });
    }

    private void BuildTimelineShell()
    {
        var rulerRow = new VisualElement { name = "ruler-row" };
        rulerRow.style.flexDirection = FlexDirection.Row;
        rulerRow.style.height = RulerHeight;
        rootVisualElement.Add(rulerRow);

        var rulerHeader = new VisualElement();
        rulerHeader.style.width = TrackHeaderWidth;
        rulerHeader.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
        rulerRow.Add(rulerHeader);

        var rulerViewport = new VisualElement();
        rulerViewport.style.flexGrow = 1f;
        rulerViewport.style.overflow = Overflow.Hidden;
        rulerViewport.RegisterCallback<WheelEvent>(OnTimelineWheel);
        rulerRow.Add(rulerViewport);

        _rulerContent = new VisualElement();
        _rulerContent.style.position = Position.Relative;
        _rulerContent.style.height = RulerHeight;
        rulerViewport.Add(_rulerContent);

        _trackScrollView = new ScrollView(ScrollViewMode.Vertical);
        _trackScrollView.style.flexGrow = 1f;
        _trackScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        _trackScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        rootVisualElement.Add(_trackScrollView);

        _horizontalScroller = new Scroller(0f, 0f, value =>
        {
            if (_suppressHorizontalScrollerCallback)
                return;

            _timelineScrollX = value;
            RefreshTimelineOnly();
        }, SliderDirection.Horizontal);
        _horizontalScroller.style.height = HorizontalScrollbarHeight;
        rootVisualElement.Add(_horizontalScroller);
    }

    private void Refresh()
    {
        RefreshToolbar();
        RefreshTimelineOnly();
    }

    private void RefreshToolbar()
    {
        if (_targetField != null && _targetField.value != _targetObject)
            _targetField.SetValueWithoutNotify(_targetObject);

        bool hasSequence = _sequenceData != null;
        SetToolbarEnabled(hasSequence);
        UpdatePlaybackButton();

        if (!hasSequence)
            return;

        _frameRateField.SetValueWithoutNotify(_sequenceData.FrameRate);
        _durationModeField.SetValueWithoutNotify(_sequenceData.DurationMode);
        _durationFramesField.SetValueWithoutNotify(_sequenceData.FixedDurationFrames);
        _durationFramesField.SetEnabled(_sequenceData.DurationMode == ActionSequenceDurationMode.FixedFrames);
        _autoDurationLabel.text = _sequenceData.DurationMode == ActionSequenceDurationMode.AutoFromClips
            ? $"Auto {_sequenceData.CalculateAutoDurationFrames()}"
            : string.Empty;

        _currentFrame = Mathf.Clamp(_currentFrame, 0, Mathf.Max(0, GetEditorTimelineDuration() - 1));
        _currentFrameField.SetValueWithoutNotify(_currentFrame);
        _zoomSlider.SetValueWithoutNotify(_pixelsPerFrame);
    }

    private void SetToolbarEnabled(bool enabled)
    {
        _addTrackMenu?.SetEnabled(enabled);
        _playPauseButton?.SetEnabled(enabled);
        _frameRateField?.SetEnabled(enabled);
        _durationModeField?.SetEnabled(enabled);
        _durationFramesField?.SetEnabled(enabled);
        _currentFrameField?.SetEnabled(enabled);
        _zoomSlider?.SetEnabled(enabled);
    }

    private void RefreshTimelineOnly()
    {
        if (_rulerContent == null || _trackScrollView == null)
            return;

        _rulerContent.Clear();
        _trackScrollView.Clear();

        if (_sequenceData == null)
        {
            _trackScrollView.Add(new HelpBox("Select a Sequence ActionAsset or ActionSequenceAsset.", HelpBoxMessageType.Info));
            return;
        }

        _sequenceData.Normalize();
        int duration = GetEditorTimelineDuration();
        float viewportWidth = Mathf.Max(1f, position.width - TrackHeaderWidth - 20f);
        float contentWidth = GetTimelineContentWidth(duration, viewportWidth);
        ClampTimelineScroll(contentWidth, viewportWidth);

        _rulerContent.style.width = contentWidth;
        _rulerContent.style.left = -_timelineScrollX;
        DrawRuler(duration, contentWidth);

        IReadOnlyList<ActionSequenceTrackDefinition> tracks = _sequenceData.Tracks;
        for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            _trackScrollView.Add(BuildTrackRow(trackIndex, tracks[trackIndex], duration, contentWidth));

        ConfigureHorizontalScroller(contentWidth, viewportWidth);
        RefreshToolbar();
    }

    private VisualElement BuildTrackRow(int trackIndex, ActionSequenceTrackDefinition track, int duration, float contentWidth)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.height = TrackHeight;
        row.style.flexShrink = 0f;

        var header = BuildTrackHeader(trackIndex, track);
        row.Add(header);

        var laneViewport = new VisualElement();
        laneViewport.style.flexGrow = 1f;
        laneViewport.style.height = TrackHeight;
        laneViewport.style.overflow = Overflow.Hidden;
        laneViewport.style.backgroundColor = track != null && track.muted ? new Color(0.08f, 0.08f, 0.08f) : new Color(0.12f, 0.12f, 0.12f);
        laneViewport.RegisterCallback<WheelEvent>(OnTimelineWheel);
        laneViewport.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            if (track == null || track.locked)
                return;

            int startFrame = XToFrame(evt.localMousePosition.x, duration, contentWidth);
            BuildAddClipMenu(evt.menu, trackIndex, track, startFrame);
        }));
        row.Add(laneViewport);

        var laneContent = new VisualElement();
        laneContent.style.position = Position.Relative;
        laneContent.style.width = contentWidth;
        laneContent.style.height = TrackHeight;
        laneContent.style.left = -_timelineScrollX;
        laneViewport.Add(laneContent);

        DrawLaneGrid(laneContent, duration, contentWidth);

        if (track != null && !track.collapsed)
        {
            IReadOnlyList<ActionSequenceClipDefinition> clips = track.Clips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                AddClipElement(laneContent, laneViewport, trackIndex, clipIndex, track, clips[clipIndex], duration, contentWidth);
        }

        return row;
    }

    private VisualElement BuildTrackHeader(int trackIndex, ActionSequenceTrackDefinition track)
    {
        var header = new VisualElement();
        header.style.width = TrackHeaderWidth;
        header.style.height = TrackHeight;
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.backgroundColor = ActionSequenceEditorSelection.IsTrackSelected(_targetObject, trackIndex)
            ? new Color(0.25f, 0.25f, 0.25f)
            : new Color(0.18f, 0.18f, 0.18f);

        if (track == null)
            return header;

        var collapse = new Toggle();
        collapse.style.width = 18f;
        collapse.value = !track.collapsed;
        collapse.SetEnabled(!track.locked);
        collapse.RegisterValueChangedCallback(evt => MutateTarget("Toggle Track Collapse", () => track.collapsed = !evt.newValue));
        header.Add(collapse);

        var name = new TextField();
        name.style.width = 88f;
        name.value = track.GetDisplayName();
        name.SetEnabled(!track.locked);
        name.RegisterValueChangedCallback(evt => MutateTarget("Rename Action Sequence Track", () => track.displayName = evt.newValue));
        header.Add(name);

        var mute = new Toggle("M");
        mute.style.width = 36f;
        mute.value = track.muted;
        mute.SetEnabled(!track.locked);
        mute.RegisterValueChangedCallback(evt => MutateTarget("Toggle Track Mute", () => track.muted = evt.newValue));
        header.Add(mute);

        var locked = new Toggle("L");
        locked.style.width = 36f;
        locked.value = track.locked;
        locked.RegisterValueChangedCallback(evt => MutateTarget("Toggle Track Lock", () => track.locked = evt.newValue));
        header.Add(locked);

        var add = new Button(() => ShowCreateClipMenu(trackIndex, track, _currentFrame)) { text = "+" };
        add.style.width = 24f;
        add.SetEnabled(!track.locked);
        header.Add(add);

        var delete = new Button(() => RemoveTrack(trackIndex)) { text = "x" };
        delete.style.width = 24f;
        delete.SetEnabled(!track.locked);
        header.Add(delete);

        header.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;

            SelectTrack(trackIndex);
            evt.StopPropagation();
        });

        return header;
    }

    private void AddClipElement(VisualElement laneContent, VisualElement laneViewport, int trackIndex, int clipIndex, ActionSequenceTrackDefinition track, ActionSequenceClipDefinition clip, int duration, float contentWidth)
    {
        if (clip == null)
            return;

        float xMin = FrameToContentX(clip.StartFrame, duration, contentWidth);
        float xMax = FrameToContentX(clip.EndFrame, duration, contentWidth);
        var element = new VisualElement();
        element.style.position = Position.Absolute;
        element.style.left = xMin;
        element.style.top = 5f;
        element.style.width = Mathf.Max(6f, xMax - xMin);
        element.style.height = TrackHeight - 10f;
        element.style.backgroundColor = GetPhaseColor(track.Phase, ActionSequenceEditorSelection.IsClipSelected(_targetObject, trackIndex, clipIndex));
        element.style.borderTopLeftRadius = 3f;
        element.style.borderTopRightRadius = 3f;
        element.style.borderBottomLeftRadius = 3f;
        element.style.borderBottomRightRadius = 3f;

        var label = new Label(clip.GetDisplayName());
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.color = Color.white;
        label.style.marginLeft = 5f;
        element.Add(label);

        element.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;

            SelectClip(trackIndex, clipIndex, false);
            if (!track.locked)
                BeginClipEdit(evt, laneViewport, element, label, trackIndex, clipIndex, clip, duration, contentWidth);
            else
                RefreshTimelineOnly();

            evt.StopPropagation();
        });

        element.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_clipEdit == null || !_clipEdit.IsActive || _clipEdit.Element != element || !element.HasPointerCapture(evt.pointerId))
                return;

            int pointerFrame = XToFrame(evt.position.x - laneViewport.worldBound.x, _clipEdit.Duration, _clipEdit.ContentWidth);
            ApplyClipEdit(pointerFrame);
            evt.StopPropagation();
        });

        element.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (_clipEdit == null || !_clipEdit.IsActive || _clipEdit.Element != element)
                return;

            if (element.HasPointerCapture(evt.pointerId))
                element.ReleasePointer(evt.pointerId);

            _clipEdit = null;
            Refresh();
            evt.StopPropagation();
        });

        element.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            evt.menu.AppendAction("Select", _ => SelectClip(trackIndex, clipIndex));
            if (track.locked)
                evt.menu.AppendAction("Delete", null, _ => DropdownMenuAction.Status.Disabled);
            else
                evt.menu.AppendAction("Delete", _ => RemoveClip(trackIndex, clipIndex));
        }));

        laneContent.Add(element);
    }

    private void DrawRuler(int duration, float contentWidth)
    {
        int minorStep = GetMinorRulerStep();
        int majorStep = GetMajorRulerStep();

        for (int frame = 0; frame <= duration; frame += minorStep)
        {
            float x = FrameToContentX(frame, duration, contentWidth);
            bool major = frame % majorStep == 0;

            var tick = new VisualElement();
            tick.style.position = Position.Absolute;
            tick.style.left = x;
            tick.style.top = major ? 0f : RulerHeight * 0.58f;
            tick.style.width = 1f;
            tick.style.height = major ? RulerHeight : RulerHeight * 0.42f;
            tick.style.backgroundColor = major ? new Color(0.42f, 0.42f, 0.42f) : new Color(0.28f, 0.28f, 0.28f);
            _rulerContent.Add(tick);

            if (!major)
                continue;

            var label = new Label(frame.ToString());
            label.style.position = Position.Absolute;
            label.style.left = x + 3f;
            label.style.top = 2f;
            label.style.width = 52f;
            label.style.height = RulerHeight - 2f;
            label.style.fontSize = 10f;
            label.style.color = new Color(0.75f, 0.75f, 0.75f);
            _rulerContent.Add(label);
        }

        float playheadX = FrameToContentX(_currentFrame, duration, contentWidth);
        var playhead = new VisualElement();
        playhead.style.position = Position.Absolute;
        playhead.style.left = playheadX;
        playhead.style.top = 0f;
        playhead.style.width = 2f;
        playhead.style.height = RulerHeight;
        playhead.style.backgroundColor = new Color(1f, 0.62f, 0.15f);
        _rulerContent.Add(playhead);
    }

    private void DrawLaneGrid(VisualElement laneContent, int duration, float contentWidth)
    {
        int minorStep = GetMinorRulerStep();
        int majorStep = GetMajorRulerStep();

        for (int frame = 0; frame <= duration; frame += minorStep)
        {
            float x = FrameToContentX(frame, duration, contentWidth);
            bool major = frame % majorStep == 0;
            var line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.left = x;
            line.style.top = 0f;
            line.style.width = 1f;
            line.style.height = TrackHeight;
            line.style.backgroundColor = major ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.16f, 0.16f, 0.16f);
            laneContent.Add(line);
        }
    }

    private void BeginClipEdit(PointerDownEvent evt, VisualElement laneViewport, VisualElement element, Label label, int trackIndex, int clipIndex, ActionSequenceClipDefinition clip, int duration, float contentWidth)
    {
        float elementWidth = Mathf.Max(1f, element.resolvedStyle.width);
        ClipEditMode mode = ClipEditMode.Move;
        if (evt.localPosition.x <= 6f)
            mode = ClipEditMode.ResizeStart;
        else if (evt.localPosition.x >= elementWidth - 6f)
            mode = ClipEditMode.ResizeEnd;

        _clipEdit = new ClipEditState
        {
            Mode = mode,
            TrackIndex = trackIndex,
            ClipIndex = clipIndex,
            OriginalStartFrame = clip.StartFrame,
            OriginalEndFrame = clip.EndFrame,
            PointerStartFrame = XToFrame(evt.position.x - laneViewport.worldBound.x, duration, contentWidth),
            Duration = duration,
            ContentWidth = contentWidth,
            UndoRecorded = false,
            Element = element,
            Label = label,
            PointerId = evt.pointerId,
        };

        element.CapturePointer(evt.pointerId);
    }

    private void ApplyClipEdit(int pointerFrame)
    {
        if (_clipEdit == null || !_clipEdit.IsActive)
            return;

        SerializedObject serializedObject = new SerializedObject(_targetObject);
        serializedObject.Update();

        SerializedProperty tracks = ActionSequenceEditorSelection.GetTracksProperty(serializedObject);
        SerializedProperty trackProperty = tracks != null && _clipEdit.TrackIndex >= 0 && _clipEdit.TrackIndex < tracks.arraySize
            ? tracks.GetArrayElementAtIndex(_clipEdit.TrackIndex)
            : null;
        SerializedProperty clips = trackProperty?.FindPropertyRelative("clips");
        SerializedProperty clipProperty = clips != null && _clipEdit.ClipIndex >= 0 && _clipEdit.ClipIndex < clips.arraySize
            ? clips.GetArrayElementAtIndex(_clipEdit.ClipIndex)
            : null;
        if (clipProperty == null)
            return;

        ActionSequenceTrackDefinition track = trackProperty.managedReferenceValue as ActionSequenceTrackDefinition;
        if (track != null && track.locked)
            return;

        SerializedProperty startFrame = clipProperty.FindPropertyRelative("startFrame");
        SerializedProperty endFrame = clipProperty.FindPropertyRelative("endFrame");
        if (startFrame == null || endFrame == null)
            return;

        int delta = pointerFrame - _clipEdit.PointerStartFrame;
        int originalLength = Mathf.Max(1, _clipEdit.OriginalEndFrame - _clipEdit.OriginalStartFrame);
        int nextStart = _clipEdit.OriginalStartFrame;
        int nextEnd = _clipEdit.OriginalEndFrame;

        switch (_clipEdit.Mode)
        {
            case ClipEditMode.Move:
                nextStart = Mathf.Clamp(_clipEdit.OriginalStartFrame + delta, 0, Mathf.Max(0, _clipEdit.Duration - originalLength));
                nextEnd = Mathf.Min(_clipEdit.Duration, nextStart + originalLength);
                break;
            case ClipEditMode.ResizeStart:
                nextStart = Mathf.Clamp(_clipEdit.OriginalStartFrame + delta, 0, _clipEdit.OriginalEndFrame - 1);
                nextEnd = _clipEdit.OriginalEndFrame;
                break;
            case ClipEditMode.ResizeEnd:
                nextStart = _clipEdit.OriginalStartFrame;
                nextEnd = Mathf.Clamp(_clipEdit.OriginalEndFrame + delta, _clipEdit.OriginalStartFrame + 1, _clipEdit.Duration);
                break;
        }

        if (startFrame.intValue == nextStart && endFrame.intValue == nextEnd)
            return;

        if (!_clipEdit.UndoRecorded)
        {
            Undo.RecordObject(_targetObject, "Edit Action Sequence Clip Timing");
            _clipEdit.UndoRecorded = true;
        }

        startFrame.intValue = nextStart;
        endFrame.intValue = nextEnd;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(_targetObject);

        UpdateEditingClipElement(nextStart, nextEnd);
    }

    private void UpdateEditingClipElement(int startFrame, int endFrame)
    {
        if (_clipEdit == null || _clipEdit.Element == null || _clipEdit.Label == null)
            return;

        float xMin = FrameToContentX(startFrame, _clipEdit.Duration, _clipEdit.ContentWidth);
        float xMax = FrameToContentX(endFrame, _clipEdit.Duration, _clipEdit.ContentWidth);
        _clipEdit.Element.style.left = xMin;
        _clipEdit.Element.style.width = Mathf.Max(6f, xMax - xMin);
        _clipEdit.Label.text = $"{startFrame} - {endFrame}";
    }

    private void BuildAddTrackMenu()
    {
        if (_addTrackMenu == null)
            return;

        List<Type> types = ActionSequenceEditorSelection.GetTrackTypes();
        for (int i = 0; i < types.Count; i++)
        {
            Type type = types[i];
            _addTrackMenu.menu.AppendAction(ActionSequenceEditorSelection.GetTrackTypeDisplayName(type), _ => CreateTrack(type));
        }
    }

    private void BuildAddClipMenu(DropdownMenu menu, int trackIndex, ActionSequenceTrackDefinition track, int startFrame)
    {
        List<Type> clipTypes = ActionSequenceEditorSelection.GetClipTypesForTrack(track);
        if (clipTypes.Count == 0)
        {
            menu.AppendAction("No clip types", null, _ => DropdownMenuAction.Status.Disabled);
            return;
        }

        for (int i = 0; i < clipTypes.Count; i++)
        {
            Type type = clipTypes[i];
            menu.AppendAction(ActionSequenceEditorSelection.GetClipTypeDisplayName(type), _ => CreateClip(trackIndex, type, startFrame));
        }
    }

    private void ShowCreateClipMenu(int trackIndex, ActionSequenceTrackDefinition track, int startFrame)
    {
        var menu = new GenericMenu();
        List<Type> clipTypes = ActionSequenceEditorSelection.GetClipTypesForTrack(track);
        if (clipTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No clip types"));
        }
        else
        {
            for (int i = 0; i < clipTypes.Count; i++)
            {
                Type type = clipTypes[i];
                menu.AddItem(new GUIContent(ActionSequenceEditorSelection.GetClipTypeDisplayName(type)), false, () => CreateClip(trackIndex, type, startFrame));
            }
        }

        menu.ShowAsContext();
    }

    private void SetTarget(UnityEngine.Object target)
    {
        if (target is ActionAsset actionAsset)
        {
            _targetObject = actionAsset;
            _sequenceData = actionAsset.UsesSequence ? actionAsset.SequenceData : null;
            if (_sequenceData == null)
                _isPlaying = false;
            ActionSequenceEditorSelection.ClearIfTargetNot(_targetObject);
            Refresh();
            return;
        }

        if (target is ActionSequenceAsset sequenceAsset)
        {
            _targetObject = sequenceAsset;
            _sequenceData = sequenceAsset.Data;
            ActionSequenceEditorSelection.ClearIfTargetNot(_targetObject);
            Refresh();
            return;
        }

        _targetObject = null;
        _sequenceData = null;
        _isPlaying = false;
        ActionSequenceEditorSelection.Clear();
        Refresh();
    }

    private void TogglePlayback()
    {
        if (_sequenceData == null)
            return;

        _isPlaying = !_isPlaying;
        _lastPlaybackEditorTime = EditorApplication.timeSinceStartup;
        UpdatePlaybackButton();
    }

    private void UpdatePlaybackButton()
    {
        if (_playPauseButton != null)
            _playPauseButton.text = _isPlaying ? "Pause" : "Play";
    }

    private void OnEditorUpdate()
    {
        if (!_isPlaying || _sequenceData == null)
            return;

        double now = EditorApplication.timeSinceStartup;
        double elapsed = now - _lastPlaybackEditorTime;
        int frameRate = Mathf.Max(1, _sequenceData.FrameRate);
        int frameDelta = Mathf.FloorToInt((float)(elapsed * frameRate));
        if (frameDelta <= 0)
            return;

        _lastPlaybackEditorTime += frameDelta / (double)frameRate;

        int duration = Mathf.Max(1, GetEditorTimelineDuration());
        _currentFrame += frameDelta;
        if (_currentFrame >= duration)
            _currentFrame %= duration;

        RefreshTimelineOnly();
    }

    private void SelectTrack(int trackIndex)
    {
        SerializedObject serializedObject = new SerializedObject(_targetObject);
        SerializedProperty tracks = ActionSequenceEditorSelection.GetTracksProperty(serializedObject);
        if (tracks == null || trackIndex < 0 || trackIndex >= tracks.arraySize)
            return;

        ActionSequenceEditorSelection.SetTrack(_targetObject, trackIndex, tracks.GetArrayElementAtIndex(trackIndex).propertyPath);
        Selection.activeObject = _targetObject;
        RefreshTimelineOnly();
    }

    private void SelectClip(int trackIndex, int clipIndex, bool refresh = true)
    {
        SerializedObject serializedObject = new SerializedObject(_targetObject);
        SerializedProperty tracks = ActionSequenceEditorSelection.GetTracksProperty(serializedObject);
        SerializedProperty track = tracks != null && trackIndex >= 0 && trackIndex < tracks.arraySize ? tracks.GetArrayElementAtIndex(trackIndex) : null;
        SerializedProperty clips = track?.FindPropertyRelative("clips");
        if (clips == null || clipIndex < 0 || clipIndex >= clips.arraySize)
            return;

        ActionSequenceEditorSelection.SetClip(_targetObject, trackIndex, clipIndex, track.propertyPath, clips.GetArrayElementAtIndex(clipIndex).propertyPath);
        Selection.activeObject = _targetObject;
        if (refresh)
            RefreshTimelineOnly();
    }

    private void CreateTrack(Type trackType)
    {
        if (_targetObject == null || !ActionSequenceEditorSelection.IsCreatableTrackType(trackType))
            return;

        SerializedObject serializedObject = new SerializedObject(_targetObject);
        serializedObject.Update();
        SerializedProperty tracks = ActionSequenceEditorSelection.GetTracksProperty(serializedObject);
        if (tracks == null)
            return;

        Undo.RecordObject(_targetObject, "Create Action Sequence Track");
        var track = (ActionSequenceTrackDefinition)Activator.CreateInstance(trackType);
        track.NormalizeFrames(GetEditorTimelineDuration());
        ActionSequenceEditorIdentity.AssignNewIdToCreatedItem(_targetObject, track);

        int index = tracks.arraySize;
        tracks.InsertArrayElementAtIndex(index);
        tracks.GetArrayElementAtIndex(index).managedReferenceValue = track;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(_targetObject);
        SelectTrack(index);
        Refresh();
    }

    private void RemoveTrack(int trackIndex)
    {
        SerializedObject serializedObject = new SerializedObject(_targetObject);
        serializedObject.Update();
        SerializedProperty tracks = ActionSequenceEditorSelection.GetTracksProperty(serializedObject);
        if (tracks == null || trackIndex < 0 || trackIndex >= tracks.arraySize)
            return;

        ActionSequenceTrackDefinition track = tracks.GetArrayElementAtIndex(trackIndex).managedReferenceValue as ActionSequenceTrackDefinition;
        if (track != null && track.locked)
            return;

        Undo.RecordObject(_targetObject, "Remove Action Sequence Track");
        tracks.DeleteArrayElementAtIndex(trackIndex);
        serializedObject.ApplyModifiedProperties();
        ActionSequenceEditorSelection.ClearIfTarget(_targetObject);
        EditorUtility.SetDirty(_targetObject);
        Refresh();
    }

    private void CreateClip(int trackIndex, Type clipType, int startFrame)
    {
        SerializedObject serializedObject = new SerializedObject(_targetObject);
        serializedObject.Update();
        SerializedProperty tracks = ActionSequenceEditorSelection.GetTracksProperty(serializedObject);
        SerializedProperty trackProperty = tracks != null && trackIndex >= 0 && trackIndex < tracks.arraySize ? tracks.GetArrayElementAtIndex(trackIndex) : null;
        SerializedProperty clips = trackProperty?.FindPropertyRelative("clips");
        if (clips == null)
            return;

        ActionSequenceTrackDefinition track = trackProperty.managedReferenceValue as ActionSequenceTrackDefinition;
        if (track == null || track.locked)
            return;

        int duration = GetEditorTimelineDuration();
        startFrame = Mathf.Clamp(startFrame, 0, duration - 1);
        var clip = (ActionSequenceClipDefinition)Activator.CreateInstance(clipType);
        clip.startFrame = startFrame;
        clip.endFrame = Mathf.Clamp(startFrame + DefaultClipDurationFrames, startFrame + 1, duration);
        clip.NormalizeFrames(duration);
        ActionSequenceEditorIdentity.AssignNewIdToCreatedItem(_targetObject, clip);

        Undo.RecordObject(_targetObject, "Create Action Sequence Clip");
        int clipIndex = clips.arraySize;
        clips.InsertArrayElementAtIndex(clipIndex);
        clips.GetArrayElementAtIndex(clipIndex).managedReferenceValue = clip;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(_targetObject);
        SelectClip(trackIndex, clipIndex);
        Refresh();
    }

    private void RemoveClip(int trackIndex, int clipIndex)
    {
        SerializedObject serializedObject = new SerializedObject(_targetObject);
        serializedObject.Update();
        SerializedProperty tracks = ActionSequenceEditorSelection.GetTracksProperty(serializedObject);
        SerializedProperty trackProperty = tracks != null && trackIndex >= 0 && trackIndex < tracks.arraySize ? tracks.GetArrayElementAtIndex(trackIndex) : null;
        SerializedProperty clips = trackProperty?.FindPropertyRelative("clips");
        if (clips == null || clipIndex < 0 || clipIndex >= clips.arraySize)
            return;

        ActionSequenceTrackDefinition track = trackProperty.managedReferenceValue as ActionSequenceTrackDefinition;
        if (track != null && track.locked)
            return;

        Undo.RecordObject(_targetObject, "Remove Action Sequence Clip");
        clips.DeleteArrayElementAtIndex(clipIndex);
        serializedObject.ApplyModifiedProperties();
        ActionSequenceEditorSelection.ClearIfTarget(_targetObject);
        EditorUtility.SetDirty(_targetObject);
        Refresh();
    }

    private void MutateTarget(string undoName, Action mutation)
    {
        if (_targetObject == null || mutation == null)
            return;

        Undo.RecordObject(_targetObject, undoName);
        mutation();
        EditorUtility.SetDirty(_targetObject);
        Refresh();
        ActionSequenceEditorWindow.RepaintAllOpenWindows();
    }

    private void SetIntProperty(string propertyName, int value, string undoName)
    {
        SerializedProperty property = GetSequenceDataProperty(propertyName, out SerializedObject serializedObject);
        if (property == null)
            return;

        Undo.RecordObject(_targetObject, undoName);
        property.intValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(_targetObject);
        Refresh();
    }

    private void SetDurationMode(ActionSequenceDurationMode durationMode)
    {
        SerializedProperty property = GetSequenceDataProperty("durationMode", out SerializedObject serializedObject);
        if (property == null)
            return;

        Undo.RecordObject(_targetObject, "Change Action Sequence Duration Mode");
        property.enumValueIndex = (int)durationMode;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(_targetObject);
        Refresh();
    }

    private SerializedProperty GetSequenceDataProperty(string propertyName, out SerializedObject serializedObject)
    {
        serializedObject = null;
        if (_targetObject == null)
            return null;

        serializedObject = new SerializedObject(_targetObject);
        serializedObject.Update();
        return ActionSequenceEditorSelection.GetSequenceDataProperty(serializedObject)?.FindPropertyRelative(propertyName);
    }

    private int GetEditorTimelineDuration()
    {
        if (_sequenceData == null)
            return 1;

        return _sequenceData.DurationMode == ActionSequenceDurationMode.AutoFromClips
            ? Mathf.Max(AutoMinimumTimelineViewFrames, _sequenceData.CalculateAutoDurationFrames())
            : _sequenceData.FixedDurationFrames;
    }

    private void FitTimelineToWindow()
    {
        if (_sequenceData == null)
            return;

        float viewportWidth = Mathf.Max(1f, position.width - TrackHeaderWidth - 20f);
        int duration = GetEditorTimelineDuration();
        _pixelsPerFrame = Mathf.Clamp(viewportWidth / Mathf.Max(1, duration), MinPixelsPerFrame, MaxPixelsPerFrame);
        _timelineScrollX = 0f;
        RefreshTimelineOnly();
    }

    private void ConfigureHorizontalScroller(float contentWidth, float viewportWidth)
    {
        if (_horizontalScroller == null)
            return;

        float max = Mathf.Max(0f, contentWidth - viewportWidth);
        _horizontalScroller.lowValue = 0f;
        _horizontalScroller.highValue = max;
        _suppressHorizontalScrollerCallback = true;
        _horizontalScroller.value = Mathf.Clamp(_timelineScrollX, 0f, max);
        _suppressHorizontalScrollerCallback = false;
        _horizontalScroller.SetEnabled(max > 0f);
    }

    private void OnTimelineWheel(WheelEvent evt)
    {
        if (!evt.ctrlKey && !evt.commandKey)
            return;

        int duration = GetEditorTimelineDuration();
        float viewportWidth = Mathf.Max(1f, position.width - TrackHeaderWidth - 20f);
        float oldContentWidth = GetTimelineContentWidth(duration, viewportWidth);
        float mouseX = Mathf.Clamp(evt.localMousePosition.x, 0f, viewportWidth);
        float normalized = Mathf.Clamp01((mouseX + _timelineScrollX) / Mathf.Max(1f, oldContentWidth));

        float factor = evt.delta.y > 0f ? 0.9f : 1.1f;
        _pixelsPerFrame = Mathf.Clamp(_pixelsPerFrame * factor, MinPixelsPerFrame, MaxPixelsPerFrame);

        float newContentWidth = GetTimelineContentWidth(duration, viewportWidth);
        _timelineScrollX = normalized * newContentWidth - mouseX;
        ClampTimelineScroll(newContentWidth, viewportWidth);
        _zoomSlider?.SetValueWithoutNotify(_pixelsPerFrame);

        evt.StopPropagation();
        RefreshTimelineOnly();
    }

    private float GetTimelineContentWidth(int duration, float viewportWidth)
    {
        return Mathf.Max(viewportWidth, Mathf.Max(1, duration) * _pixelsPerFrame);
    }

    private void ClampTimelineScroll(float contentWidth, float viewportWidth)
    {
        _timelineScrollX = Mathf.Clamp(_timelineScrollX, 0f, Mathf.Max(0f, contentWidth - viewportWidth));
    }

    private int GetMinorRulerStep()
    {
        if (_pixelsPerFrame >= 8f)
            return 1;
        if (_pixelsPerFrame >= 4f)
            return 5;

        return 10;
    }

    private int GetMajorRulerStep()
    {
        const float minLabelSpacing = 72f;
        int step = Mathf.Max(1, Mathf.CeilToInt(minLabelSpacing / Mathf.Max(1f, _pixelsPerFrame)));

        if (step <= 5)
            return step;
        if (step <= 10)
            return 10;
        if (step <= 15)
            return 15;
        if (step <= 30)
            return 30;

        return Mathf.CeilToInt(step / 30f) * 30;
    }

    private float FrameToContentX(int frame, int duration, float contentWidth)
    {
        return Mathf.Clamp01(frame / (float)Mathf.Max(1, duration)) * contentWidth;
    }

    private int XToFrame(float x, int duration, float contentWidth)
    {
        return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01((x + _timelineScrollX) / Mathf.Max(1f, contentWidth)) * duration), 0, Mathf.Max(0, duration - 1));
    }

    private static Color GetPhaseColor(ActionSequenceClipPhase phase, bool selected)
    {
        Color color = phase switch
        {
            ActionSequenceClipPhase.State => new Color(0.36f, 0.49f, 0.76f),
            ActionSequenceClipPhase.Animation => new Color(0.42f, 0.65f, 0.38f),
            ActionSequenceClipPhase.Motion => new Color(0.77f, 0.52f, 0.28f),
            ActionSequenceClipPhase.HitBox => new Color(0.76f, 0.34f, 0.34f),
            ActionSequenceClipPhase.Cleanup => new Color(0.52f, 0.42f, 0.68f),
            _ => new Color(0.45f, 0.45f, 0.45f),
        };

        return selected ? Color.Lerp(color, Color.white, 0.25f) : color;
    }
}
#endif
