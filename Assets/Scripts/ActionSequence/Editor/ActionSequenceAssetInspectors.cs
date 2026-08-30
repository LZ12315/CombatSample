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
        DrawActionV1Timeline(root);
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
        parent.Add(ReadOnly("Kind", track.Kind.ToString()));
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
        parent.Add(ReadOnly("Kind", clip.Kind.ToString()));
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

        Button openButton = new Button(OpenActionSequenceEditor) { text = "Open Action Sequence Editor" };
        foldout.Add(openButton);

        ActionSequenceSnapshot sequence = document.Sequence;
        foldout.Add(ReadOnly("Frame Rate", $"{CombatSimulationTiming.FrameRate} FPS (Project Fixed)"));

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
        DrawActionV1Timeline(parent);
    }

    private void DrawActionV1Timeline(VisualElement parent)
    {
        if (!(target is ActionAsset))
            return;

        SerializedProperty timeline = serializedObject.FindProperty("_actionTimeline");
        if (timeline == null)
            return;

        var foldout = new Foldout { text = "Action V1 Timeline", value = true };
        foldout.Add(new HelpBox(
            "Stage 1 authoring data. It is serialized and validated here, but current Legacy/Sequence playback does not read it yet.",
            HelpBoxMessageType.Info));
        foldout.Add(new PropertyField(timeline.Copy()));
        parent.Add(foldout);
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
        if (clipProperty.managedReferenceValue is ActionSequenceSelfRotationClipDefinition)
        {
            DrawSelfRotationClipConfigFields(parent, clipProperty);
            parent.RegisterCallback<SerializedPropertyChangeEvent>(_ => QueueContentChanged(trackId, clipId));
            return;
        }

        if (clipProperty.managedReferenceValue is ActionSequenceVelocityOverrideClipDefinition)
        {
            DrawVelocityOverrideClipConfigFields(parent, clipProperty);
            parent.RegisterCallback<SerializedPropertyChangeEvent>(_ => QueueContentChanged(trackId, clipId));
            return;
        }

        if (clipProperty.managedReferenceValue is ActionSequenceImpulseClipDefinition)
        {
            DrawImpulseClipConfigFields(parent, clipProperty);
            parent.RegisterCallback<SerializedPropertyChangeEvent>(_ => QueueContentChanged(trackId, clipId));
            return;
        }

        if (clipProperty.managedReferenceValue is ActionSequenceMotionPolicyClipDefinition)
        {
            DrawMotionPolicyClipConfigFields(parent, clipProperty);
            parent.RegisterCallback<SerializedPropertyChangeEvent>(_ => QueueContentChanged(trackId, clipId));
            return;
        }

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

    private void DrawSelfRotationClipConfigFields(VisualElement parent, SerializedProperty clipProperty)
    {
        AddManagedReferenceProperty(parent, clipProperty, "displayName");

        SerializedProperty source = clipProperty.FindPropertyRelative("source");
        SerializedProperty mode = clipProperty.FindPropertyRelative("mode");
        SerializedProperty directionSource = clipProperty.FindPropertyRelative("directionSource");

        AddManagedReferenceProperty(parent, clipProperty, "source");
        AddManagedReferenceProperty(parent, clipProperty, "mode");

        VisualElement rootRotationAnimationKey = AddManagedReferenceProperty(parent, clipProperty, "animationKey", "Animation Key");
        VisualElement rootRotationStartOffset = AddManagedReferenceProperty(parent, clipProperty, "startOffsetSeconds", "Source Start Time (s)");
        VisualElement rootRotationPlaybackSpeed = AddManagedReferenceProperty(parent, clipProperty, "playbackSpeed", "Playback Speed");
        VisualElement targetSource = AddManagedReferenceProperty(parent, clipProperty, "targetSource", "Target Source");
        VisualElement directionSourceField = AddManagedReferenceProperty(parent, clipProperty, "directionSource", "Direction Source");
        VisualElement presetLocalDirection = AddManagedReferenceProperty(parent, clipProperty, "presetLocalDirection", "Preset Local Direction");
        VisualElement angularSpeed = AddManagedReferenceProperty(parent, clipProperty, "angularSpeedDegrees", "Angular Speed");

        void RefreshVisibility()
        {
            SelfRotationSource sourceValue = source != null
                ? (SelfRotationSource)source.enumValueIndex
                : SelfRotationSource.RootRotation;
            SelfRotationMode modeValue = mode != null
                ? (SelfRotationMode)mode.enumValueIndex
                : SelfRotationMode.Snap;
            SelfRotationDirectionSource directionSourceValue = directionSource != null
                ? (SelfRotationDirectionSource)directionSource.enumValueIndex
                : SelfRotationDirectionSource.PresetLocal;

            bool showRootRotation = sourceValue == SelfRotationSource.RootRotation;
            bool showTarget = sourceValue == SelfRotationSource.Target;
            bool showDirection = sourceValue == SelfRotationSource.Direction;
            bool showPreset = showDirection && directionSourceValue == SelfRotationDirectionSource.PresetLocal;

            SetVisible(rootRotationAnimationKey, showRootRotation);
            SetVisible(rootRotationStartOffset, showRootRotation);
            SetVisible(rootRotationPlaybackSpeed, showRootRotation);
            SetVisible(targetSource, showTarget);
            SetVisible(directionSourceField, showDirection);
            SetVisible(presetLocalDirection, showPreset);
            SetVisible(angularSpeed, modeValue == SelfRotationMode.RotateBySpeed);
        }

        RefreshVisibility();
        if (source != null)
            parent.TrackPropertyValue(source, _ => RefreshVisibility());
        if (mode != null)
            parent.TrackPropertyValue(mode, _ => RefreshVisibility());
        if (directionSource != null)
            parent.TrackPropertyValue(directionSource, _ => RefreshVisibility());
    }

    private void DrawVelocityOverrideClipConfigFields(VisualElement parent, SerializedProperty clipProperty)
    {
        AddManagedReferenceProperty(parent, clipProperty, "displayName");

        SerializedProperty config = clipProperty.FindPropertyRelative("config");
        if (config == null)
            return;

        SerializedProperty useHorizontal = config.FindPropertyRelative("useHorizontalVelocity");
        SerializedProperty directionMode = config.FindPropertyRelative("directionMode");
        SerializedProperty useVertical = config.FindPropertyRelative("useVerticalVelocity");

        VisualElement horizontalToggle = AddManagedReferenceProperty(parent, config, "useHorizontalVelocity", "Use Horizontal Velocity");
        VisualElement directionModeField = AddManagedReferenceProperty(parent, config, "directionMode", "Direction Source");
        VisualElement localDirection = AddManagedReferenceProperty(parent, config, "localHorizontalDirection", "Preset Local Direction");
        VisualElement horizontalSpeed = AddManagedReferenceProperty(parent, config, "horizontalSpeed", "Horizontal Speed");
        VisualElement horizontalCurve = AddManagedReferenceProperty(parent, config, "horizontalCurve", "Horizontal Curve");
        VisualElement verticalToggle = AddManagedReferenceProperty(parent, config, "useVerticalVelocity", "Use Vertical Velocity");
        VisualElement verticalSpeed = AddManagedReferenceProperty(parent, config, "verticalSpeed", "Vertical Speed");
        VisualElement verticalCurve = AddManagedReferenceProperty(parent, config, "verticalCurve", "Vertical Curve");
        VisualElement debugLog = AddManagedReferenceProperty(parent, config, "debugLog", "Debug Log");

        void RefreshVisibility()
        {
            bool showHorizontal = useHorizontal != null && useHorizontal.boolValue;
            bool showVertical = useVertical != null && useVertical.boolValue;
            MotionDirectionMode directionValue = directionMode != null
                ? (MotionDirectionMode)directionMode.intValue
                : MotionDirectionMode.LocalHorizontal;

            SetVisible(horizontalToggle, true);
            SetVisible(directionModeField, showHorizontal);
            SetVisible(localDirection, showHorizontal && directionValue == MotionDirectionMode.LocalHorizontal);
            SetVisible(horizontalSpeed, showHorizontal);
            SetVisible(horizontalCurve, showHorizontal);
            SetVisible(verticalToggle, true);
            SetVisible(verticalSpeed, showVertical);
            SetVisible(verticalCurve, showVertical);
            SetVisible(debugLog, true);
        }

        RefreshVisibility();
        if (useHorizontal != null)
            parent.TrackPropertyValue(useHorizontal, _ => RefreshVisibility());
        if (directionMode != null)
            parent.TrackPropertyValue(directionMode, _ => RefreshVisibility());
        if (useVertical != null)
            parent.TrackPropertyValue(useVertical, _ => RefreshVisibility());
    }

    private void DrawImpulseClipConfigFields(VisualElement parent, SerializedProperty clipProperty)
    {
        AddManagedReferenceProperty(parent, clipProperty, "displayName");
        AddManagedReferenceProperty(parent, clipProperty, "useHorizontalImpulse", "Use Horizontal Impulse");
        AddManagedReferenceProperty(parent, clipProperty, "useVerticalBallistic", "Use Vertical Ballistic");
        VisualElement verticalOperation = AddManagedReferenceProperty(parent, clipProperty, "verticalOperation", "Vertical Operation");
        AddManagedReferenceProperty(parent, clipProperty, "overrideGravityScale", "Override Gravity Scale");

        SerializedProperty useHorizontal = clipProperty.FindPropertyRelative("useHorizontalImpulse");
        SerializedProperty useVertical = clipProperty.FindPropertyRelative("useVerticalBallistic");
        SerializedProperty overrideGravity = clipProperty.FindPropertyRelative("overrideGravityScale");
        SerializedProperty config = clipProperty.FindPropertyRelative("config");
        if (config == null)
            return;

        SerializedProperty directionMode = config.FindPropertyRelative("directionMode");

        VisualElement directionModeField = AddManagedReferenceProperty(parent, config, "directionMode", "Direction Source");
        VisualElement localDirection = AddManagedReferenceProperty(parent, config, "localHorizontalDirection", "Preset Local Direction");
        VisualElement horizontalForce = AddManagedReferenceProperty(parent, config, "horizontalForce", "Horizontal Force");
        VisualElement verticalForce = AddManagedReferenceProperty(parent, config, "verticalForce", "Vertical Force");
        VisualElement gravityScale = AddManagedReferenceProperty(parent, clipProperty, "gravityScale", "Gravity Scale");
        VisualElement debugLog = AddManagedReferenceProperty(parent, config, "debugLog", "Debug Log");

        void RefreshVisibility()
        {
            bool showHorizontal = useHorizontal != null && useHorizontal.boolValue;
            bool showVertical = useVertical != null && useVertical.boolValue;
            bool showGravity = overrideGravity != null && overrideGravity.boolValue;
            ImpulseDirectionMode directionValue = directionMode != null
                ? (ImpulseDirectionMode)directionMode.intValue
                : ImpulseDirectionMode.FromContext;

            SetVisible(directionModeField, showHorizontal);
            SetVisible(localDirection, showHorizontal && directionValue == ImpulseDirectionMode.LocalHorizontal);
            SetVisible(horizontalForce, showHorizontal);
            SetVisible(verticalOperation, showVertical);
            SetVisible(verticalForce, showVertical);
            SetVisible(gravityScale, showGravity);
            SetVisible(debugLog, true);
        }

        RefreshVisibility();
        if (useHorizontal != null)
            parent.TrackPropertyValue(useHorizontal, _ => RefreshVisibility());
        if (useVertical != null)
            parent.TrackPropertyValue(useVertical, _ => RefreshVisibility());
        if (overrideGravity != null)
            parent.TrackPropertyValue(overrideGravity, _ => RefreshVisibility());
        if (directionMode != null)
            parent.TrackPropertyValue(directionMode, _ => RefreshVisibility());
    }

    private void DrawMotionPolicyClipConfigFields(VisualElement parent, SerializedProperty clipProperty)
    {
        AddManagedReferenceProperty(parent, clipProperty, "displayName");

        SerializedProperty useLocomotion = clipProperty.FindPropertyRelative("useLocomotionScale");
        SerializedProperty useAirLocomotion = clipProperty.FindPropertyRelative("useAirLocomotionScale");
        SerializedProperty useGravity = clipProperty.FindPropertyRelative("useGravityScale");

        VisualElement locomotionToggle = AddManagedReferenceProperty(parent, clipProperty, "useLocomotionScale", "Use Locomotion Scale");
        VisualElement locomotionScale = AddManagedReferenceProperty(parent, clipProperty, "locomotionScale", "Locomotion Scale");
        VisualElement airToggle = AddManagedReferenceProperty(parent, clipProperty, "useAirLocomotionScale", "Use Air Locomotion Scale");
        VisualElement airScale = AddManagedReferenceProperty(parent, clipProperty, "airLocomotionScale", "Air Locomotion Scale");
        VisualElement gravityToggle = AddManagedReferenceProperty(parent, clipProperty, "useGravityScale", "Use Gravity Scale");
        VisualElement gravityScale = AddManagedReferenceProperty(parent, clipProperty, "gravityScale", "Gravity Scale");

        void RefreshVisibility()
        {
            SetVisible(locomotionToggle, true);
            SetVisible(locomotionScale, useLocomotion != null && useLocomotion.boolValue);
            SetVisible(airToggle, true);
            SetVisible(airScale, useAirLocomotion != null && useAirLocomotion.boolValue);
            SetVisible(gravityToggle, true);
            SetVisible(gravityScale, useGravity != null && useGravity.boolValue);
        }

        RefreshVisibility();
        if (useLocomotion != null)
            parent.TrackPropertyValue(useLocomotion, _ => RefreshVisibility());
        if (useAirLocomotion != null)
            parent.TrackPropertyValue(useAirLocomotion, _ => RefreshVisibility());
        if (useGravity != null)
            parent.TrackPropertyValue(useGravity, _ => RefreshVisibility());
    }

    private static VisualElement AddManagedReferenceProperty(
        VisualElement parent,
        SerializedProperty rootProperty,
        string propertyName,
        string label = null)
    {
        SerializedProperty property = rootProperty.FindPropertyRelative(propertyName);
        if (property == null)
            return null;

        var field = string.IsNullOrEmpty(label)
            ? new PropertyField(property.Copy())
            : new PropertyField(property.Copy(), label);
        parent.Add(field);
        return field;
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

    private void OpenActionSequenceEditor()
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
