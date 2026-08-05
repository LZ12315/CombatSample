#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public sealed class ActionSequenceEditorWindowV2 : EditorWindow
{
    private const string UxmlPath = "Assets/Scripts/ActionSequence/Editor/V2/ActionSequenceEditorWindowV2.uxml";
    private const string UssPath = "Assets/Scripts/ActionSequence/Editor/V2/ActionSequenceEditorWindowV2.uss";
    private const double PollIntervalSeconds = 0.2d;

    [SerializeField]
    private Object _targetObject;

    [SerializeField]
    private float _savedPixelsPerFrame = ActionSequenceEditorState.DefaultPixelsPerFrame;

    [SerializeField]
    private float _savedHorizontalScroll;

    [SerializeField]
    private float _savedVerticalScroll;

    [SerializeField]
    private float _savedHeaderWidth = 220f;

    [SerializeField]
    private ActionSequenceEditorSelection.SelectionKind _savedSelectionKind;

    [SerializeField]
    private string _savedSelectionTrackId;

    [SerializeField]
    private string _savedSelectionClipId;

    [SerializeField]
    private int _savedCurrentFrame;

    private readonly ActionSequenceEditorState state = new ActionSequenceEditorState();
    private ActionSequenceEditorRoot editorRoot;
    private VisualElement serializedTracker;
    private bool upgradeAttemptedForCurrentBinding;
    private bool editorUpdateRegistered;
    private bool suppressCommandChanged;
    private double nextPollTime;

    [MenuItem("Tools/Combat/Action Sequence Editor")]
    public static void Open()
    {
        GetWindow<ActionSequenceEditorWindowV2>("Action Sequence");
    }

    public static void Open(ActionAsset actionAsset)
    {
        var window = GetWindow<ActionSequenceEditorWindowV2>("Action Sequence");
        window.BindTarget(actionAsset, true);
    }

    public static void Open(ActionSequenceAsset sequenceAsset)
    {
        var window = GetWindow<ActionSequenceEditorWindowV2>("Action Sequence");
        window.BindTarget(sequenceAsset, true);
    }

    public void CreateGUI()
    {
        BuildVisualTree();
        editorRoot = new ActionSequenceEditorRoot(rootVisualElement.Q<VisualElement>("asv2-root") ?? rootVisualElement);
        editorRoot.TargetChanged += target => BindTarget(target, true);
        editorRoot.ZoomChanged += OnZoomChanged;
        editorRoot.HorizontalScrollChanged += OnHorizontalScrollChanged;
        editorRoot.VerticalScrollChanged += OnVerticalScrollChanged;
        editorRoot.ViewportChanged += OnViewportChanged;
        editorRoot.FitRequested += OnFitRequested;
        editorRoot.RepairInvalidIdsRequested += OnRepairInvalidIdsRequested;
        editorRoot.RepairCommandRequested += OnRepairCommandRequested;
        editorRoot.ValidationIssueLocateRequested += OnValidationIssueLocateRequested;
        editorRoot.AddTrackRequested += OnAddTrackRequested;
        editorRoot.PlayPauseRequested += OnPlayPauseRequested;
        editorRoot.StopRequested += OnStopRequested;
        editorRoot.PlayheadScrubStarted += OnPlayheadScrubStarted;
        editorRoot.CurrentFrameChanged += OnCurrentFrameChanged;
        editorRoot.SequenceSelected += OnSequenceSelected;
        editorRoot.TrackSelected += OnTrackSelected;
        editorRoot.ClipSelected += OnClipSelected;
        editorRoot.AddClipRequested += OnAddClipRequested;
        editorRoot.TrackMuteChanged += OnTrackMuteChanged;
        editorRoot.TrackLockChanged += OnTrackLockChanged;
        editorRoot.TrackCollapseChanged += OnTrackCollapseChanged;
        editorRoot.TrackContextRequested += OnTrackContextRequested;
        editorRoot.ClipContextRequested += OnClipContextRequested;
        editorRoot.ClipTimingPreviewStarted += OnClipTimingPreviewStarted;
        editorRoot.ClipTimingPreviewChanged += OnClipTimingPreviewChanged;
        editorRoot.ClipTimingPreviewCommitted += OnClipTimingPreviewCommitted;
        editorRoot.ClipTimingPreviewCancelled += OnClipTimingPreviewCancelled;
        editorRoot.ShortcutRequested += OnShortcutRequested;
        BindTarget(_targetObject, true);
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        ActionSequenceEditorCommands.Changed -= OnCommandChanged;
        ActionSequenceEditorCommands.Changed += OnCommandChanged;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        ActionSequenceEditorCommands.Changed -= OnCommandChanged;
        SetEditorUpdateActive(false);
        state.Dispose();
    }

    private void BuildVisualTree()
    {
        rootVisualElement.Clear();

        VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (tree != null)
            tree.CloneTree(rootVisualElement);
        else
            BuildFallbackVisualTree();

        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        if (styleSheet != null)
            rootVisualElement.styleSheets.Add(styleSheet);
    }

    private void BuildFallbackVisualTree()
    {
        var root = new VisualElement { name = "asv2-root" };
        rootVisualElement.Add(root);

        var toolbar = new VisualElement { name = "toolbar-host" };
        toolbar.Add(new ObjectField("Target") { name = "target-field" });
        toolbar.Add(new Label { name = "fps-label" });
        toolbar.Add(new Label { name = "duration-label" });
        toolbar.Add(new Slider { name = "zoom-slider" });
        toolbar.Add(new Button { name = "fit-button", text = "Fit" });
        root.Add(toolbar);

        root.Add(new VisualElement { name = "timeline-host" });

        var status = new VisualElement { name = "status-bar" };
        status.Add(new Label { name = "status-target-label" });
        status.Add(new Label { name = "status-validation-label" });
        status.Add(new Button { name = "issues-button", text = "Issues" });
        status.Add(new Button { name = "repair-ids-button", text = "Repair IDs" });
        root.Add(status);
    }

    private void BindTarget(Object target, bool runAutoUpgrade)
    {
        _targetObject = target;
        upgradeAttemptedForCurrentBinding = false;

        if (runAutoUpgrade)
            TryAutoUpgrade();

        state.SetTarget(_targetObject);
        state.ConfigureView(_savedPixelsPerFrame, _savedHorizontalScroll, _savedVerticalScroll, _savedHeaderWidth);
        state.SetCurrentFrame(_savedCurrentFrame);
        state.RestoreLocalSelection(_savedSelectionKind, _savedSelectionTrackId, _savedSelectionClipId);
        state.PublishSelection();
        editorRoot?.SetTarget(_targetObject);
        editorRoot?.SetStatusMessage(null);
        TrackSerializedTarget();
        RefreshWholeView();
    }

    private void TryAutoUpgrade()
    {
        if (upgradeAttemptedForCurrentBinding || _targetObject == null)
            return;

        upgradeAttemptedForCurrentBinding = true;
        ActionSequenceEditorIdentity.UpgradeMissingIds(_targetObject);
    }

    private void TrackSerializedTarget()
    {
        if (serializedTracker != null)
            serializedTracker.RemoveFromHierarchy();

        serializedTracker = new VisualElement { name = "serialized-object-tracker" };
        serializedTracker.style.display = DisplayStyle.None;
        rootVisualElement.Add(serializedTracker);

        if (state.IsSupported)
            serializedTracker.TrackSerializedObjectValue(state.Document.SerializedObject, _ => RefreshFromSerializedChange());
    }

    private void RefreshFromSerializedChange()
    {
        if (state.Refresh())
            RefreshWholeView();
    }

    private void RefreshWholeView()
    {
        editorRoot?.Refresh(state, BuildValidationPresentation());
    }

    private ActionSequenceValidationPresentation BuildValidationPresentation()
    {
        ActionSequenceEditorValidationResult validation = state.IsSupported
            ? ActionSequenceValidator.Validate(state.Document)
            : new ActionSequenceEditorValidationResult();
        return ActionSequenceValidationPresentation.Create(validation);
    }

    private void RefreshViewportOnly()
    {
        editorRoot?.RefreshViewportOnly(state);
    }

    private void OnZoomChanged(float viewportAnchorX, float pixelsPerFrame)
    {
        state.ZoomAt(viewportAnchorX, pixelsPerFrame);
        SaveViewState();
        RefreshViewportOnly();
    }

    private void OnHorizontalScrollChanged(float value)
    {
        state.SetHorizontalScroll(value);
        SaveViewState();
        RefreshViewportOnly();
    }

    private void OnVerticalScrollChanged(float value)
    {
        state.SetVerticalScroll(value);
        SaveViewState();
    }

    private void OnViewportChanged(float width, float height)
    {
        state.SetViewport(width, height);
        SaveViewState();
        RefreshViewportOnly();
    }

    private void OnFitRequested()
    {
        state.Fit();
        SaveViewState();
        RefreshViewportOnly();
    }

    private void OnPlayPauseRequested()
    {
        if (!state.TogglePlayback(EditorApplication.timeSinceStartup, out string reason))
        {
            editorRoot?.SetStatusMessage(reason);
            RefreshWholeView();
            return;
        }

        SetEditorUpdateActive(state.IsPlaying);
        SaveCurrentFrameState();
        editorRoot?.RefreshPlaybackOnly(state);
    }

    private void OnStopRequested()
    {
        state.StopPlayback(resetFrame: true);
        SetEditorUpdateActive(false);
        SaveCurrentFrameState();
        editorRoot?.RefreshPlaybackOnly(state);
    }

    private void OnPlayheadScrubStarted()
    {
        state.StopPlayback(resetFrame: false);
        SetEditorUpdateActive(false);
        editorRoot?.RefreshPlaybackOnly(state);
    }

    private void OnCurrentFrameChanged(int frame)
    {
        state.SetCurrentFrame(frame);
        SaveCurrentFrameState();
        editorRoot?.RefreshPlaybackOnly(state);
    }

    private void OnRepairInvalidIdsRequested()
    {
        if (_targetObject == null)
            return;

        ExecuteCommand(() => ActionSequenceEditorCommands.RepairInvalidIds(_targetObject));
    }

    private void OnRepairCommandRequested(string commandId)
    {
        if (_targetObject == null || string.IsNullOrEmpty(commandId))
            return;

        switch (commandId)
        {
            case ActionSequenceValidator.RepairInvalidIdsCommandId:
                ExecuteCommand(() => ActionSequenceEditorCommands.RepairInvalidIds(_targetObject));
                break;
            case ActionSequenceValidator.MigrateLegacyClipsCommandId:
                ExecuteCommand(() => ActionSequenceEditorCommands.MigrateLegacyClips(_targetObject));
                break;
            case ActionSequenceValidator.RepairTrackPhaseOrderCommandId:
                ExecuteCommand(() => ActionSequenceEditorCommands.RepairTrackPhaseOrder(_targetObject));
                break;
            default:
                editorRoot?.SetStatusMessage("Unknown repair command: " + commandId);
                break;
        }
    }

    private void OnValidationIssueLocateRequested(ActionSequenceEditorValidationIssue issue)
    {
        if (issue == null || !state.IsSupported)
            return;

        switch (issue.ItemKind)
        {
            case ActionSequenceEditorDocumentItemKind.Track:
                if (!string.IsNullOrEmpty(issue.EditorId))
                    state.SelectTrack(issue.EditorId);
                else if (issue.TrackIndex >= 0 && issue.TrackIndex < state.Document.Tracks.Count)
                    state.SelectTrack(state.Document.Tracks[issue.TrackIndex].EditorId);
                break;
            case ActionSequenceEditorDocumentItemKind.Clip:
                if (!string.IsNullOrEmpty(issue.EditorId))
                    state.SelectClip(issue.EditorId);
                else if (issue.TrackIndex >= 0 && issue.TrackIndex < state.Document.Tracks.Count)
                {
                    ActionSequenceTrackSnapshot track = state.Document.Tracks[issue.TrackIndex];
                    if (issue.ClipIndex >= 0 && issue.ClipIndex < track.Clips.Count)
                        state.SelectClip(track.Clips[issue.ClipIndex].EditorId);
                }
                break;
            default:
                state.SelectSequence();
                break;
        }

        FrameSelection();
        SaveSelectionState();
        RefreshWholeView();
        editorRoot?.SetStatusMessage(issue.Code + ": " + issue.Message);
    }

    private void OnAddTrackRequested(VisualElement anchor)
    {
        if (!state.IsSupported)
            return;

        var menu = new GenericMenu();
        bool hasItems = false;
        foreach (ActionSequenceEditorTrackTypeInfo typeInfo in ActionSequenceEditorTypeRegistry.TrackTypes)
        {
            ActionSequenceEditorTrackTypeInfo captured = typeInfo;
            menu.AddItem(new GUIContent($"{captured.Phase}/{captured.DisplayName}"), false, () =>
            {
                ExecuteCommand(() => ActionSequenceEditorCommands.AddTrack(_targetObject, captured.Type));
            });
            hasItems = true;
        }

        if (!hasItems)
            menu.AddDisabledItem(new GUIContent("No creatable track types"));

        menu.DropDown(anchor != null ? anchor.worldBound : new Rect(position.x, position.y, 1f, 1f));
    }

    private void OnSequenceSelected()
    {
        if (!state.IsSupported)
            return;

        state.SelectSequence();
        SaveSelectionState();
        RefreshWholeView();
    }

    private void OnTrackSelected(ActionSequenceTrackSnapshot track)
    {
        if (track == null)
            return;

        state.SelectTrack(track.EditorId);
        SaveSelectionState();
        RefreshWholeView();
    }

    private void OnClipSelected(ActionSequenceClipSnapshot clip, ActionSequenceTrackSnapshot track)
    {
        if (clip == null || track == null)
            return;

        if (track.Locked)
            state.SelectTrack(track.EditorId);
        else
            state.SelectClip(clip.EditorId);

        SaveSelectionState();
        RefreshWholeView();
    }

    private void OnAddClipRequested(ActionSequenceTrackSnapshot track, int frame, Vector2 position)
    {
        ShowAddClipMenu(track, frame, position);
    }

    private void OnTrackMuteChanged(ActionSequenceTrackSnapshot track, bool muted)
    {
        if (track != null)
            ExecuteCommand(() => ActionSequenceEditorCommands.SetTrackMuted(_targetObject, track.EditorId, muted));
    }

    private void OnTrackLockChanged(ActionSequenceTrackSnapshot track, bool locked)
    {
        if (track != null)
            ExecuteCommand(() => ActionSequenceEditorCommands.SetTrackLocked(_targetObject, track.EditorId, locked));
    }

    private void OnTrackCollapseChanged(ActionSequenceTrackSnapshot track, bool collapsed)
    {
        if (track != null)
            ExecuteCommand(() => ActionSequenceEditorCommands.SetTrackCollapsed(_targetObject, track.EditorId, collapsed));
    }

    private void OnTrackContextRequested(ActionSequenceTrackSnapshot track, int frame, Vector2 position)
    {
        if (track == null)
            return;

        var menu = new GenericMenu();
        if (track.Locked)
        {
            menu.AddItem(new GUIContent("Unlock"), false, () =>
                ExecuteCommand(() => ActionSequenceEditorCommands.SetTrackLocked(_targetObject, track.EditorId, false)));
            menu.ShowAsContext();
            return;
        }

        AddClipMenuItems(menu, track, frame, "Add Clip/");
        menu.AddSeparator(string.Empty);
        AddTrackReorderMenuItems(menu, track);
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("Delete Track"), false, () => DeleteTrackWithConfirmation(track, false));
        menu.ShowAsContext();
    }

    private void OnClipContextRequested(ActionSequenceClipSnapshot clip, ActionSequenceTrackSnapshot track, Vector2 position)
    {
        if (clip == null || track == null)
            return;

        var menu = new GenericMenu();
        if (track.Locked)
            menu.AddDisabledItem(new GUIContent("Delete Clip"));
        else
            menu.AddItem(new GUIContent("Delete Clip"), false, () => ExecuteCommand(() => ActionSequenceEditorCommands.DeleteClip(_targetObject, clip.EditorId)));

        menu.ShowAsContext();
    }

    private void OnClipTimingPreviewStarted(
        ActionSequenceClipSnapshot clip,
        ActionSequenceTrackSnapshot track,
        ActionSequenceDisplayClip displayClip,
        ActionSequenceClipTimingEditMode mode)
    {
        if (clip == null || track == null || track.Locked || clip.MissingType || string.IsNullOrEmpty(clip.EditorId))
            return;

        state.StopPlayback(resetFrame: false);
        SetEditorUpdateActive(false);
        state.SelectClip(clip.EditorId);
        state.BeginInteractionPreview(clip, displayClip, mode);
        SaveSelectionState();
        editorRoot?.RefreshInteractionPreviewOnly(state);
    }

    private void OnClipTimingPreviewChanged(ActionSequenceClipSnapshot clip, ActionSequenceTrackSnapshot track, int startFrame, int endFrame)
    {
        if (!state.InteractionPreview.IsActive || clip == null || !string.Equals(state.InteractionPreview.ClipId, clip.EditorId, StringComparison.Ordinal))
            return;

        state.UpdateInteractionPreview(startFrame, endFrame);
        ApplyPreviewAutoScroll(startFrame, endFrame);
        editorRoot?.SetStatusMessage($"Timing {startFrame}-{endFrame}");
        editorRoot?.RefreshInteractionPreviewOnly(state);
        SaveViewState();
    }

    private void OnClipTimingPreviewCommitted(ActionSequenceClipSnapshot clip, ActionSequenceTrackSnapshot track, int startFrame, int endFrame)
    {
        if (clip == null || !state.InteractionPreview.IsActive)
        {
            CancelInteractionPreview();
            return;
        }

        state.ClearInteractionPreview();
        editorRoot?.RefreshInteractionPreviewOnly(state);
        ExecuteCommand(() => ActionSequenceEditorCommands.SetClipTiming(_targetObject, clip.EditorId, startFrame, endFrame));
    }

    private void OnClipTimingPreviewCancelled()
    {
        CancelInteractionPreview();
    }

    private void CancelInteractionPreview()
    {
        if (!state.InteractionPreview.IsActive)
            return;

        state.ClearInteractionPreview();
        editorRoot?.CancelActiveInteractionGesture();
        editorRoot?.SetStatusMessage("Timing edit cancelled.");
        editorRoot?.RefreshInteractionPreviewOnly(state);
    }

    private void ApplyPreviewAutoScroll(int startFrame, int endFrame)
    {
        const float edgeSize = 24f;
        const float maxStep = 48f;
        float startX = state.Transform.FrameToViewportX(startFrame);
        float endX = state.Transform.FrameToViewportX(endFrame);
        float scroll = state.HorizontalScroll;

        if (endX > state.ViewportWidth - edgeSize)
        {
            float t = Mathf.InverseLerp(state.ViewportWidth - edgeSize, state.ViewportWidth, endX);
            scroll += Mathf.Lerp(4f, maxStep, t);
        }
        else if (startX < edgeSize)
        {
            float t = Mathf.InverseLerp(edgeSize, 0f, startX);
            scroll -= Mathf.Lerp(4f, maxStep, t);
        }

        state.SetHorizontalScroll(scroll);
    }

    private void OnShortcutRequested(ActionSequenceEditorShortcut shortcut)
    {
        switch (shortcut)
        {
            case ActionSequenceEditorShortcut.PlayPause:
                OnPlayPauseRequested();
                break;
            case ActionSequenceEditorShortcut.Stop:
                OnStopRequested();
                break;
            case ActionSequenceEditorShortcut.StepFrameLeft:
                OnCurrentFrameChanged(state.CurrentFrame - 1);
                break;
            case ActionSequenceEditorShortcut.StepFrameRight:
                OnCurrentFrameChanged(state.CurrentFrame + 1);
                break;
            case ActionSequenceEditorShortcut.StepMajorLeft:
                OnCurrentFrameChanged(state.CurrentFrame - state.Transform.ChooseStepForMinimumPixels(72f));
                break;
            case ActionSequenceEditorShortcut.StepMajorRight:
                OnCurrentFrameChanged(state.CurrentFrame + state.Transform.ChooseStepForMinimumPixels(72f));
                break;
            case ActionSequenceEditorShortcut.FrameSelection:
                FrameSelection();
                break;
            case ActionSequenceEditorShortcut.DeleteSelection:
                DeleteSelection();
                break;
            case ActionSequenceEditorShortcut.ToggleLock:
                ToggleSelectedTrackLock();
                break;
            case ActionSequenceEditorShortcut.ToggleMute:
                ToggleSelectedTrackMute();
                break;
            case ActionSequenceEditorShortcut.CancelInteraction:
                CancelInteractionPreview();
                break;
        }
    }

    private void FrameSelection()
    {
        if (!state.IsSupported)
            return;

        if (state.LocalSelection.Kind == ActionSequenceEditorSelection.SelectionKind.Clip
            && state.TryFindDisplayClip(state.LocalSelection.ClipId, out _, out ActionSequenceDisplayClip clip))
        {
            state.FrameRange(clip.SafeStartFrame, clip.SafeEndFrame);
            SaveViewState();
            RefreshViewportOnly();
            return;
        }

        if (state.LocalSelection.Kind == ActionSequenceEditorSelection.SelectionKind.Track
            && state.TryResolveTrackSnapshot(state.LocalSelection.TrackId, out ActionSequenceTrackSnapshot track))
        {
            if (TryGetTrackFrameRange(track.EditorId, out int start, out int end))
            {
                state.FrameRange(start, end);
                SaveViewState();
                RefreshViewportOnly();
                return;
            }
        }

        state.Fit();
        SaveViewState();
        RefreshViewportOnly();
    }

    private bool TryGetTrackFrameRange(string trackId, out int startFrame, out int endFrame)
    {
        startFrame = int.MaxValue;
        endFrame = int.MinValue;
        for (int trackIndex = 0; trackIndex < state.DisplayTracks.Count; trackIndex++)
        {
            ActionSequenceDisplayTrack track = state.DisplayTracks[trackIndex];
            if (!string.Equals(track.Snapshot.EditorId, trackId, StringComparison.Ordinal))
                continue;

            IReadOnlyList<ActionSequenceDisplayClip> clips = track.Clips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                startFrame = Mathf.Min(startFrame, clips[clipIndex].SafeStartFrame);
                endFrame = Mathf.Max(endFrame, clips[clipIndex].SafeEndFrame);
            }

            break;
        }

        if (startFrame == int.MaxValue || endFrame == int.MinValue)
            return false;

        return true;
    }

    private void DeleteSelection()
    {
        switch (state.LocalSelection.Kind)
        {
            case ActionSequenceEditorSelection.SelectionKind.Clip:
                if (state.TryResolveClipSnapshot(state.LocalSelection.ClipId, out _, out ActionSequenceClipSnapshot clip))
                    ExecuteCommand(() => ActionSequenceEditorCommands.DeleteClip(_targetObject, clip.EditorId));
                break;
            case ActionSequenceEditorSelection.SelectionKind.Track:
                if (state.TryResolveTrackSnapshot(state.LocalSelection.TrackId, out ActionSequenceTrackSnapshot track))
                    DeleteTrackWithConfirmation(track, false);
                break;
        }
    }

    private void ToggleSelectedTrackLock()
    {
        if (TryGetSelectedOrOwningTrack(out ActionSequenceTrackSnapshot track))
            ExecuteCommand(() => ActionSequenceEditorCommands.SetTrackLocked(_targetObject, track.EditorId, !track.Locked));
    }

    private void ToggleSelectedTrackMute()
    {
        if (TryGetSelectedOrOwningTrack(out ActionSequenceTrackSnapshot track))
            ExecuteCommand(() => ActionSequenceEditorCommands.SetTrackMuted(_targetObject, track.EditorId, !track.Muted));
    }

    private bool TryGetSelectedOrOwningTrack(out ActionSequenceTrackSnapshot track)
    {
        track = null;
        switch (state.LocalSelection.Kind)
        {
            case ActionSequenceEditorSelection.SelectionKind.Track:
                return state.TryResolveTrackSnapshot(state.LocalSelection.TrackId, out track);
            case ActionSequenceEditorSelection.SelectionKind.Clip:
                return state.TryResolveClipSnapshot(state.LocalSelection.ClipId, out track, out _);
            default:
                return false;
        }
    }

    private void ShowAddClipMenu(ActionSequenceTrackSnapshot track, int frame, Vector2 position)
    {
        if (track == null)
            return;

        var menu = new GenericMenu();
        if (track.Locked)
        {
            menu.AddDisabledItem(new GUIContent("Track is locked"));
        }
        else
        {
            AddClipMenuItems(menu, track, frame, string.Empty);
        }

        menu.ShowAsContext();
    }

    private void AddClipMenuItems(GenericMenu menu, ActionSequenceTrackSnapshot track, int frame, string prefix)
    {
        ActionSequenceTrackDefinition trackDefinition = state.Document.GetTrackProperty(track.TrackIndex)?.managedReferenceValue as ActionSequenceTrackDefinition;
        IReadOnlyList<ActionSequenceEditorClipTypeInfo> clipTypes = ActionSequenceEditorTypeRegistry.GetClipTypesForTrack(trackDefinition);
        if (clipTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent(prefix + "No allowed clip types"));
            return;
        }

        int start = ClampNewClipStartFrame(frame);
        int end = start + 1;
        for (int i = 0; i < clipTypes.Count; i++)
        {
            ActionSequenceEditorClipTypeInfo captured = clipTypes[i];
            menu.AddItem(new GUIContent(prefix + captured.DisplayName), false, () =>
                ExecuteCommand(() => ActionSequenceEditorCommands.AddClip(_targetObject, track.EditorId, captured.Type, start, end)));
        }
    }

    private int ClampNewClipStartFrame(int frame)
    {
        int start = Mathf.Max(0, frame);
        if (!state.IsSupported)
            return start;

        ActionSequenceSnapshot sequence = state.Document.Sequence;
        if (sequence.DurationMode == ActionSequenceDurationMode.FixedFrames)
            start = Mathf.Clamp(start, 0, Mathf.Max(0, sequence.FixedDurationFrames - 1));

        return start;
    }

    private void AddTrackReorderMenuItems(GenericMenu menu, ActionSequenceTrackSnapshot track)
    {
        int localIndex = GetPhaseLocalIndex(track, out int phaseCount);
        if (localIndex > 0)
            menu.AddItem(new GUIContent("Move Up"), false, () =>
                ExecuteCommand(() => ActionSequenceEditorCommands.ReorderTrackWithinPhase(_targetObject, track.EditorId, localIndex - 1)));
        else
            menu.AddDisabledItem(new GUIContent("Move Up"));

        if (localIndex >= 0 && localIndex < phaseCount - 1)
            menu.AddItem(new GUIContent("Move Down"), false, () =>
                ExecuteCommand(() => ActionSequenceEditorCommands.ReorderTrackWithinPhase(_targetObject, track.EditorId, localIndex + 1)));
        else
            menu.AddDisabledItem(new GUIContent("Move Down"));
    }

    private int GetPhaseLocalIndex(ActionSequenceTrackSnapshot track, out int phaseCount)
    {
        int localIndex = -1;
        phaseCount = 0;
        if (!state.IsSupported || track == null)
            return -1;

        for (int i = 0; i < state.Document.Tracks.Count; i++)
        {
            ActionSequenceTrackSnapshot candidate = state.Document.Tracks[i];
            if (candidate.IsNull || candidate.MissingType || candidate.Phase != track.Phase)
                continue;

            if (candidate.EditorId == track.EditorId)
                localIndex = phaseCount;

            phaseCount++;
        }

        return localIndex;
    }

    private void DeleteTrackWithConfirmation(ActionSequenceTrackSnapshot track, bool confirm)
    {
        ActionSequenceEditorCommandResult result = ExecuteCommandForConfirmation(() => ActionSequenceEditorCommands.DeleteTrack(_targetObject, track.EditorId, confirm));
        if (result.Status == ActionSequenceEditorCommandStatus.ConfirmationRequired)
        {
            bool accepted = EditorUtility.DisplayDialog(
                "Delete Track",
                $"Delete '{ActionSequenceViewUtility.GetTrackDisplayName(track)}' and {track.Clips.Count} clip(s)?",
                "Delete",
                "Cancel");
            if (accepted)
                DeleteTrackWithConfirmation(track, true);
            return;
        }

        ExecuteCommand(result);
    }

    private ActionSequenceEditorCommandResult ExecuteCommandForConfirmation(Func<ActionSequenceEditorCommandResult> command)
    {
        suppressCommandChanged = true;
        try
        {
            return command();
        }
        finally
        {
            suppressCommandChanged = false;
        }
    }

    private void ExecuteCommand(ActionSequenceEditorCommandResult result)
    {
        if (result == null)
            return;

        if (result.Status == ActionSequenceEditorCommandStatus.NoChange)
        {
            editorRoot?.SetStatusMessage(result.Message);
            return;
        }

        if (result.Status != ActionSequenceEditorCommandStatus.Success)
        {
            editorRoot?.SetStatusMessage($"{result.Status}: {result.Message}");
            RefreshWholeView();
            return;
        }

        suppressCommandChanged = true;
        state.Refresh();
        suppressCommandChanged = false;
        if (ShouldApplySelection(result))
            state.ApplySelectionSuggestion(result.SelectionSuggestion);
        SaveSelectionState();
        RefreshWholeView();
        editorRoot?.SetStatusMessage(result.Message);
    }

    private void ExecuteCommand(Func<ActionSequenceEditorCommandResult> command)
    {
        if (command == null)
            return;

        suppressCommandChanged = true;
        ActionSequenceEditorCommandResult result;
        try
        {
            result = command();
        }
        finally
        {
            suppressCommandChanged = false;
        }

        ExecuteCommand(result);
    }

    private static bool ShouldApplySelection(ActionSequenceEditorCommandResult result)
    {
        if (result.SelectionSuggestion.Kind == ActionSequenceEditorDocumentItemKind.Track
            || result.SelectionSuggestion.Kind == ActionSequenceEditorDocumentItemKind.Clip)
            return true;

        return result.ChangeSet != null
            && (result.ChangeSet.Flags & ActionSequenceEditorChangeFlags.Structure) != 0
            && (!string.IsNullOrEmpty(result.AffectedTrackId) || !string.IsNullOrEmpty(result.AffectedClipId));
    }

    private void OnUndoRedoPerformed()
    {
        state.ClearInteractionPreview();
        state.StopPlayback(resetFrame: false);
        SetEditorUpdateActive(false);
        state.Refresh();
        SaveSelectionState();
        RefreshWholeView();
    }

    private void OnCommandChanged(Object target, ActionSequenceEditorChangeSet changeSet)
    {
        if (suppressCommandChanged)
            return;

        if (target != _targetObject)
            return;

        if (changeSet != null && (changeSet.Flags & (ActionSequenceEditorChangeFlags.Structure | ActionSequenceEditorChangeFlags.Timing)) != 0)
            state.ClearInteractionPreview();

        state.Refresh();
        SaveSelectionState();
        RefreshWholeView();
    }

    private void OnEditorUpdate()
    {
        bool needsUpdate = false;
        if (state.IsPlaying)
        {
            if (state.AdvancePlayback(EditorApplication.timeSinceStartup))
            {
                SaveCurrentFrameState();
                editorRoot?.RefreshPlaybackOnly(state);
            }

            needsUpdate = state.IsPlaying;
        }

        if (EditorApplication.timeSinceStartup < nextPollTime)
        {
            if (!needsUpdate)
                SetEditorUpdateActive(false);
            return;
        }

        nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSeconds;
        if (state.Refresh())
            RefreshWholeView();

        if (!needsUpdate)
            SetEditorUpdateActive(false);
    }

    private void SetEditorUpdateActive(bool active)
    {
        if (editorUpdateRegistered == active)
            return;

        editorUpdateRegistered = active;
        if (active)
            EditorApplication.update += OnEditorUpdate;
        else
            EditorApplication.update -= OnEditorUpdate;
    }

    private void SaveViewState()
    {
        _savedPixelsPerFrame = state.PixelsPerFrame;
        _savedHorizontalScroll = state.HorizontalScroll;
        _savedVerticalScroll = state.VerticalScroll;
        _savedHeaderWidth = state.HeaderWidth;
    }

    private void SaveSelectionState()
    {
        _savedSelectionKind = state.LocalSelection.Kind;
        _savedSelectionTrackId = state.LocalSelection.TrackId;
        _savedSelectionClipId = state.LocalSelection.ClipId;
    }

    private void SaveCurrentFrameState()
    {
        _savedCurrentFrame = state.CurrentFrame;
    }
}
#endif
