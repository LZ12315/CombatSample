#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[CustomEditor(typeof(ActionAsset))]
public sealed class ActionAssetSequenceInspector : Editor
{
    private ActionSequenceInspectorV2Builder builder;

    public override VisualElement CreateInspectorGUI()
    {
        builder?.Dispose();
        builder = new ActionSequenceInspectorV2Builder(this, target);
        return builder.Build();
    }

    private void OnDisable()
    {
        builder?.Dispose();
        builder = null;
    }
}

[CustomEditor(typeof(ActionSequenceAsset))]
public sealed class ActionSequenceAssetInspector : Editor
{
    private ActionSequenceInspectorV2Builder builder;

    public override VisualElement CreateInspectorGUI()
    {
        builder?.Dispose();
        builder = new ActionSequenceInspectorV2Builder(this, target);
        return builder.Build();
    }

    private void OnDisable()
    {
        builder?.Dispose();
        builder = null;
    }
}

internal sealed class ActionSequenceInspectorV2Builder : IDisposable
{
    private readonly Editor editor;
    private readonly Object target;
    private readonly SerializedObject serializedObject;
    private readonly VisualElement root = new VisualElement();
    private bool contentNotificationQueued;
    private bool suppressContentNotifications;

    public ActionSequenceInspectorV2Builder(Editor editor, Object target)
    {
        this.editor = editor;
        this.target = target;
        serializedObject = editor.serializedObject;
    }

    public VisualElement Build()
    {
        root.AddToClassList("asv2-inspector-root");
        ActionSequenceEditorSelection.Changed += OnSelectionChanged;
        ActionSequenceEditorCommands.Changed += OnCommandChanged;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        Rebuild();
        return root;
    }

    public void Dispose()
    {
        ActionSequenceEditorSelection.Changed -= OnSelectionChanged;
        ActionSequenceEditorCommands.Changed -= OnCommandChanged;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }

    private void Rebuild()
    {
        if (root.panel == null && root.childCount > 0)
            return;

        serializedObject.UpdateIfRequiredOrScript();
        root.Clear();

        if (target is ActionAsset actionAsset && !actionAsset.UsesSequence)
        {
            DrawLegacyActionAssetInspector(root);
            BindRoot();
            return;
        }

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        if (!document.IsSupported)
        {
            root.Add(new HelpBox("Unsupported ActionSequence target.", HelpBoxMessageType.Info));
            return;
        }

        DrawSelectedContext(root, document);
        DrawSequenceSection(root, document);
        DrawActionAssetSettings(root);
        DrawDebugRawData(root, document);
        BindRoot();
    }

    private void DrawSelectedContext(VisualElement parent, ActionSequenceSerializedDocument document)
    {
        ActionSequenceEditorSelectionValue selection = ActionSequenceEditorSelection.Value;
        if (selection.Target != target)
        {
            DrawSequenceSelection(parent, document);
            return;
        }

        if (selection.Kind == ActionSequenceEditorSelection.SelectionKind.Track
            && document.ResolveTrack(selection.TrackId, out int trackIndex) == ActionSequenceEditorResolveStatus.Found)
        {
            DrawTrackSelection(parent, document, trackIndex);
            return;
        }

        if (selection.Kind == ActionSequenceEditorSelection.SelectionKind.Clip
            && document.ResolveClip(selection.ClipId, out int clipTrackIndex, out int clipIndex) == ActionSequenceEditorResolveStatus.Found)
        {
            ActionSequenceTrackSnapshot track = document.Tracks[clipTrackIndex];
            if (track.Locked)
                DrawTrackSelection(parent, document, clipTrackIndex);
            else
                DrawClipSelection(parent, document, clipTrackIndex, clipIndex);
            return;
        }

        DrawSequenceSelection(parent, document);
    }

    private void DrawSequenceSelection(VisualElement parent, ActionSequenceSerializedDocument document)
    {
        parent.Add(Header("Sequence"));
        parent.Add(ReadOnly("Target", target.name));
        parent.Add(ReadOnly("Tracks", document.Tracks.Count.ToString()));
        parent.Add(ReadOnly("Legacy Clips", document.LegacyClips.Count.ToString()));
    }

    private void DrawTrackSelection(VisualElement parent, ActionSequenceSerializedDocument document, int trackIndex)
    {
        ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
        parent.Add(Header("Selected Track"));
        parent.Add(ReadOnly("Type", ActionSequenceViewUtility.GetTrackTypeDisplayName(track)));
        parent.Add(ReadOnly("Phase", track.Phase.ToString()));
        parent.Add(ReadOnly("Editor ID", track.EditorId));
        parent.Add(ReadOnly("Owning Sequence", target.name));

        if (track.IsNull || track.MissingType)
        {
            parent.Add(new HelpBox("This track cannot be edited because its managed-reference type is missing or null.", HelpBoxMessageType.Warning));
            return;
        }

        var nameField = new TextField("Display Name") { isDelayed = true };
        nameField.SetValueWithoutNotify(track.DisplayName);
        nameField.SetEnabled(!track.Locked);
        nameField.RegisterValueChangedCallback(evt =>
            Execute(ActionSequenceEditorCommands.RenameTrack(target, track.EditorId, evt.newValue)));
        parent.Add(nameField);

        parent.Add(CommandToggle("Muted", track.Muted, !track.Locked, value =>
            ActionSequenceEditorCommands.SetTrackMuted(target, track.EditorId, value)));
        parent.Add(CommandToggle("Locked", track.Locked, true, value =>
            ActionSequenceEditorCommands.SetTrackLocked(target, track.EditorId, value)));
        parent.Add(CommandToggle("Collapsed", track.Collapsed, !track.Locked, value =>
            ActionSequenceEditorCommands.SetTrackCollapsed(target, track.EditorId, value)));

        if (track.Locked)
            parent.Add(new HelpBox("This track is locked. Unlock it before editing the track or its clips.", HelpBoxMessageType.Info));
    }

    private void DrawClipSelection(VisualElement parent, ActionSequenceSerializedDocument document, int trackIndex, int clipIndex)
    {
        ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
        ActionSequenceClipSnapshot clip = track.Clips[clipIndex];
        SerializedProperty clipProperty = GetClipProperty(serializedObject, trackIndex, clipIndex);

        parent.Add(Header("Selected Clip"));
        parent.Add(ReadOnly("Type", ActionSequenceViewUtility.GetClipDisplayName(clip)));
        parent.Add(ReadOnly("Phase", clip.Phase.ToString()));
        parent.Add(ReadOnly("Editor ID", clip.EditorId));
        parent.Add(ReadOnly("Track", ActionSequenceViewUtility.GetTrackDisplayName(track)));

        if (clip.IsNull || clip.MissingType || clipProperty == null)
        {
            parent.Add(new HelpBox("This clip cannot be edited because its managed-reference type is missing or null.", HelpBoxMessageType.Warning));
            return;
        }

        var startField = new IntegerField("Start Frame");
        startField.SetValueWithoutNotify(clip.StartFrame);
        startField.SetEnabled(!track.Locked);
        startField.RegisterValueChangedCallback(evt =>
            Execute(ActionSequenceEditorCommands.SetClipTiming(target, clip.EditorId, evt.newValue, Mathf.Max(evt.newValue + 1, clip.EndFrame))));
        parent.Add(startField);

        var endField = new IntegerField("End Frame");
        endField.SetValueWithoutNotify(clip.EndFrame);
        endField.SetEnabled(!track.Locked);
        endField.RegisterValueChangedCallback(evt =>
            Execute(ActionSequenceEditorCommands.SetClipTiming(target, clip.EditorId, Mathf.Min(clip.StartFrame, evt.newValue - 1), evt.newValue)));
        parent.Add(endField);

        var configFoldout = new Foldout { text = "Config", value = true };
        configFoldout.SetEnabled(!track.Locked);
        DrawClipConfigFields(configFoldout, clipProperty, track.EditorId, clip.EditorId);
        parent.Add(configFoldout);

        if (track.Locked)
            parent.Add(new HelpBox("The owning track is locked. Unlock it before editing this clip.", HelpBoxMessageType.Info));
    }

    private void DrawSequenceSection(VisualElement parent, ActionSequenceSerializedDocument document)
    {
        var foldout = new Foldout { text = "Sequence", value = true };

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        Button prototypeButton = new Button(OpenPrototype) { text = "Open Prototype" };
        Button v2Button = new Button(OpenV2) { text = "Open V2 Preview" };
        buttons.Add(prototypeButton);
        buttons.Add(v2Button);
        foldout.Add(buttons);

        ActionSequenceSnapshot sequence = document.Sequence;
        var frameRate = new IntegerField("Frame Rate");
        frameRate.SetValueWithoutNotify(sequence.FrameRate);
        frameRate.RegisterValueChangedCallback(evt => Execute(ActionSequenceEditorCommands.SetFrameRate(target, evt.newValue)));
        foldout.Add(frameRate);

        var durationMode = new EnumField("Duration Mode", sequence.DurationMode);
        durationMode.RegisterValueChangedCallback(evt =>
            Execute(ActionSequenceEditorCommands.SetDurationMode(target, (ActionSequenceDurationMode)(object)evt.newValue)));
        foldout.Add(durationMode);

        var durationFrames = new IntegerField(sequence.DurationMode == ActionSequenceDurationMode.FixedFrames ? "Duration Frames" : "Minimum View Frames");
        durationFrames.SetValueWithoutNotify(sequence.FixedDurationFrames);
        durationFrames.SetEnabled(sequence.DurationMode == ActionSequenceDurationMode.FixedFrames);
        durationFrames.RegisterValueChangedCallback(evt =>
            Execute(ActionSequenceEditorCommands.SetFixedDurationFrames(target, evt.newValue)));
        foldout.Add(durationFrames);

        if (sequence.DurationMode == ActionSequenceDurationMode.AutoFromClips)
            foldout.Add(ReadOnly("Auto Duration Frames", CalculateAutoDuration(document).ToString()));

        DrawValidationSummary(foldout);
        parent.Add(foldout);
    }

    private void DrawActionAssetSettings(VisualElement parent)
    {
        if (!(target is ActionAsset))
            return;

        var actionAsset = (ActionAsset)target;
        var foldout = new Foldout { text = "ActionAsset Settings", value = false };
        if (actionAsset.UsesSequence)
        {
            AddProperty(foldout, "_timelineAsset");
            AddProperty(foldout, "_playbackBackend");
        }

        AddProperty(foldout, "_priorityLayer");
        AddProperty(foldout, "_priorityValue");
        AddProperty(foldout, "_cancelRules");
        AddProperty(foldout, "_selfTags");
        AddProperty(foldout, "_triggerMode");
        AddProperty(foldout, "_eventTriggerTag");
        AddProperty(foldout, "_startContextMode");
        AddProperty(foldout, "_motionConfig");
        AddProperty(foldout, "isLoop");
        AddProperty(foldout, "_allowReenterWhilePlaying");
        AddProperty(foldout, "_entryConditions");
        AddProperty(foldout, "_exitConditions");
        parent.Add(foldout);
    }

    private void DrawLegacyActionAssetInspector(VisualElement parent)
    {
        parent.Add(Header("Core"));
        AddProperty(parent, "_timelineAsset");
        AddProperty(parent, "_playbackBackend");
        DrawActionAssetSettings(parent);
    }

    private void DrawDebugRawData(VisualElement parent, ActionSequenceSerializedDocument document)
    {
        var foldout = new Foldout { text = "Debug Raw Sequence Data", value = false };

        foldout.Add(new Label("Tracks"));
        for (int i = 0; i < document.Tracks.Count; i++)
        {
            ActionSequenceTrackSnapshot track = document.Tracks[i];
            foldout.Add(new Label($"{i}: {ActionSequenceViewUtility.GetTrackDisplayName(track)} ({ActionSequenceViewUtility.GetTrackTypeDisplayName(track)})"));
            for (int clipIndex = 0; clipIndex < track.Clips.Count; clipIndex++)
            {
                ActionSequenceClipSnapshot clip = track.Clips[clipIndex];
                foldout.Add(new Label($"  Clip {clipIndex}: {ActionSequenceViewUtility.GetClipDisplayName(clip)} [{clip.StartFrame}, {clip.EndFrame})"));
            }
        }

        if (document.LegacyClips.Count > 0)
        {
            foldout.Add(new Label("Legacy Clips"));
            for (int i = 0; i < document.LegacyClips.Count; i++)
            {
                ActionSequenceClipSnapshot clip = document.LegacyClips[i];
                foldout.Add(new Label($"{i}: {ActionSequenceViewUtility.GetClipDisplayName(clip)} [{clip.StartFrame}, {clip.EndFrame})"));
            }
        }

        parent.Add(foldout);
    }

    private void DrawClipConfigFields(VisualElement parent, SerializedProperty clipProperty, string trackId, string clipId)
    {
        SerializedProperty iterator = clipProperty.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            if (ShouldSkipClipConfigProperty(iterator.name))
                continue;

            SerializedProperty copy = iterator.Copy();
            var field = new PropertyField(copy);
            parent.Add(field);
        }

        parent.RegisterCallback<SerializedPropertyChangeEvent>(_ => QueueContentChanged(trackId, clipId));
    }

    private static SerializedProperty GetClipProperty(SerializedObject serializedObject, int trackIndex, int clipIndex)
    {
        SerializedProperty tracks = ActionSequenceEditorSelection.GetTracksProperty(serializedObject);
        if (tracks == null || trackIndex < 0 || trackIndex >= tracks.arraySize)
            return null;

        SerializedProperty clips = tracks.GetArrayElementAtIndex(trackIndex)?.FindPropertyRelative("clips");
        if (clips == null || clipIndex < 0 || clipIndex >= clips.arraySize)
            return null;

        return clips.GetArrayElementAtIndex(clipIndex);
    }

    private void DrawValidationSummary(VisualElement parent)
    {
        ActionSequenceEditorValidationResult validation = ActionSequenceValidator.Validate(target);
        ActionSequenceEditorIdentityValidationResult identity = ActionSequenceEditorIdentity.Validate(target);
        int identityIssues = identity != null ? identity.Issues.Count : 0;
        int validationIssues = validation != null ? validation.Issues.Count : 0;
        if (identityIssues == 0 && validationIssues == 0)
        {
            parent.Add(ReadOnly("Validation", "Valid"));
            return;
        }

        parent.Add(new HelpBox($"{identityIssues} identity issue(s), {validationIssues} validation issue(s).", validation != null && validation.HasErrors ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
    }

    private Toggle CommandToggle(string label, bool value, bool enabled, Func<bool, ActionSequenceEditorCommandResult> command)
    {
        var toggle = new Toggle(label);
        toggle.SetValueWithoutNotify(value);
        toggle.SetEnabled(enabled);
        toggle.RegisterValueChangedCallback(evt => Execute(command(evt.newValue)));
        return toggle;
    }

    private void AddProperty(VisualElement parent, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            parent.Add(new PropertyField(property));
    }

    private void Execute(ActionSequenceEditorCommandResult result)
    {
        if (result == null || result.Status == ActionSequenceEditorCommandStatus.NoChange)
            return;

        if (result.Status != ActionSequenceEditorCommandStatus.Success)
        {
            root.Add(new HelpBox($"{result.Status}: {result.Message}", HelpBoxMessageType.Warning));
            ScheduleRebuild();
            return;
        }

        if (ShouldApplySelection(result))
            ActionSequenceEditorSelection.Select(target, result.SelectionSuggestion);

        ScheduleRebuild();
    }

    private void QueueContentChanged(string trackId, string clipId)
    {
        if (suppressContentNotifications)
            return;

        if (contentNotificationQueued)
            return;

        contentNotificationQueued = true;
        root.schedule.Execute(() =>
        {
            contentNotificationQueued = false;
            var changeSet = new ActionSequenceEditorChangeSet(ActionSequenceEditorChangeFlags.Content | ActionSequenceEditorChangeFlags.Validation)
                .AddTrack(trackId)
                .AddClip(clipId);
            ActionSequenceEditorCommands.NotifyExternalContentChanged(target, changeSet);
        });
    }

    private void OpenPrototype()
    {
        if (target is ActionAsset actionAsset)
            ActionSequenceEditorWindow.Open(actionAsset);
        else if (target is ActionSequenceAsset sequenceAsset)
            ActionSequenceEditorWindow.Open(sequenceAsset);
    }

    private void OpenV2()
    {
        if (target is ActionAsset actionAsset)
            ActionSequenceEditorWindowV2.Open(actionAsset);
        else if (target is ActionSequenceAsset sequenceAsset)
            ActionSequenceEditorWindowV2.Open(sequenceAsset);
    }

    private int CalculateAutoDuration(ActionSequenceSerializedDocument document)
    {
        int maxEnd = 1;
        for (int trackIndex = 0; trackIndex < document.Tracks.Count; trackIndex++)
        {
            ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
            for (int clipIndex = 0; clipIndex < track.Clips.Count; clipIndex++)
                maxEnd = Mathf.Max(maxEnd, Mathf.Max(track.Clips[clipIndex].StartFrame + 1, track.Clips[clipIndex].EndFrame));
        }

        for (int i = 0; i < document.LegacyClips.Count; i++)
            maxEnd = Mathf.Max(maxEnd, Mathf.Max(document.LegacyClips[i].StartFrame + 1, document.LegacyClips[i].EndFrame));

        return maxEnd;
    }

    private void OnSelectionChanged(ActionSequenceEditorSelectionValue value)
    {
        if (value.Target == target || target == Selection.activeObject)
            ScheduleRebuild();
    }

    private void OnCommandChanged(Object changedTarget, ActionSequenceEditorChangeSet changeSet)
    {
        if (changedTarget != target)
            return;

        if (changeSet == null || (changeSet.Flags & (ActionSequenceEditorChangeFlags.Structure | ActionSequenceEditorChangeFlags.Timing)) != 0)
            ScheduleRebuild();
    }

    private void OnUndoRedoPerformed()
    {
        ScheduleRebuild();
    }

    private void ScheduleRebuild()
    {
        if (root.panel == null)
            return;

        root.schedule.Execute(Rebuild);
    }

    private void BindRoot()
    {
        suppressContentNotifications = true;
        root.Bind(serializedObject);
        root.schedule.Execute(() => suppressContentNotifications = false);
    }

    private static VisualElement Header(string text)
    {
        var label = new Label(text);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = 6f;
        label.style.marginBottom = 3f;
        return label;
    }

    private static VisualElement ReadOnly(string label, string value)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        var name = new Label(label);
        name.style.minWidth = 110f;
        name.style.color = new Color(0.65f, 0.65f, 0.65f);
        var text = new Label(value ?? string.Empty);
        text.style.flexGrow = 1f;
        row.Add(name);
        row.Add(text);
        return row;
    }

    private static bool ShouldSkipClipConfigProperty(string propertyName)
    {
        return propertyName == "editorId"
            || propertyName == "startFrame"
            || propertyName == "endFrame";
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
}
#endif
