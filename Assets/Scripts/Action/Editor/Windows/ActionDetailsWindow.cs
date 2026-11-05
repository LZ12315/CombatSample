#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MackySoft.SerializeReferenceExtensions.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UIElements;

/// <summary>Primary-selection property editor and visual preview for Action authoring.</summary>
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp-Editor", sourceClassName: "ActionV1DetailsWindow")]
public sealed class ActionDetailsWindow : EditorWindow
{
    private sealed class PropertyWatch
    {
        internal string Path;
        internal int Session;
        internal bool RebuildConfiguration;
        internal ActionEditorChangeFlags ChangeFlags;
    }

    internal sealed class TimingDraft
    {
        internal ActionSelectionKind Kind;
        internal string EditorId;
        internal object Source;
        internal ActionTimelineOperationSnapshot Snapshot;
        internal int StartFrame;
        internal int DurationFrames;
        internal AnimationAsset AnimationAsset;
        internal float SourceStartTime;
        internal float SourceEndTime;
        internal float PlayRate;

        internal bool Targets(ActionAsset action, ActionSelectionValue selection) =>
            Snapshot != null && action != null && ReferenceEquals(Snapshot.Asset, action) &&
            Kind == selection.Kind && EditorId == selection.EditorId;

        internal bool CanRetain(ActionAsset action, ActionSelectionValue selection) =>
            Targets(action, selection) && Snapshot.MatchesCurrentSource(out _);

        internal static TimingDraft Create(ActionEditorDocument document, ActionDocumentEntry entry)
        {
            var animation = entry.Source as AnimationSegment;
            var root = entry.Source as RootMotionItem;
            return new TimingDraft
            {
                Kind = entry.SelectionKind, EditorId = entry.EditorId, Source = entry.Source,
                Snapshot = ActionTimelineOperationSnapshot.Capture(document,
                    root != null ? ActionTimelineOperationKind.SetRootMotionTiming : ActionTimelineOperationKind.SetAnimationTiming,
                    new[] { entry.EditorId }),
                StartFrame = entry.StartFrame,
                DurationFrames = animation != null ? animation.DerivedDurationFrames : root.DurationFrames,
                AnimationAsset = animation != null ? animation.AnimationAsset : root.Config.animationAsset,
                SourceStartTime = animation != null ? animation.SourceStartTime : root.Config.sourceStartTime,
                SourceEndTime = animation != null ? animation.SourceEndTime : root.SourceEndTime,
                PlayRate = animation != null ? animation.PlayRate : root.Config.playRate,
            };
        }

        internal ActionTimelineOperationInput Input => ActionTimelineOperationInput.Absolute(StartFrame, DurationFrames,
            AnimationAsset, SourceStartTime, SourceEndTime, PlayRate);

        internal ActionTimelineOperationResult Evaluate() => Snapshot.Evaluate(Input);

        internal void SetAnimationAsset(AnimationAsset animationAsset)
        {
            if (AnimationAsset == animationAsset) return;
            AnimationAsset = animationAsset;
            SourceStartTime = 0f;
            SourceEndTime = animationAsset != null && animationAsset.Clip != null ? animationAsset.Clip.length : 0f;
        }

        internal bool UseFullClip()
        {
            AnimationClip clip = AnimationAsset != null ? AnimationAsset.Clip : null;
            if (clip == null) return false;
            SourceStartTime = 0f;
            SourceEndTime = clip.length;
            return true;
        }

        internal bool TryApply(out string message)
        {
            ActionEditorContext context = ActionEditorContext.Shared;
            message = string.Empty;
            if (ActionEditorInteractionGate.IsActive)
            {
                message = "Finish the current Timeline interaction before applying changes.";
                return false;
            }
            if (!Targets(context.CurrentAction, context.PrimarySelection))
            {
                message = "The edited source is no longer selected.";
                return false;
            }
            context.FlushQueuedBindingChange();
            if (!Snapshot.MatchesCurrentSource(out message)) return false;
            ActionTimelineOperationResult result = Evaluate();
            if (result.State == ActionTimelineOperationState.NoChange) return true;
            if (result.State == ActionTimelineOperationState.Rejected)
            {
                message = result.Message;
                return false;
            }
            Undo.IncrementCurrentGroup();
            try { return ActionEditorCommands.CommitTimelineOperation(Snapshot, Input, out message); }
            finally { Undo.IncrementCurrentGroup(); }
        }
    }

    [SerializeField] private float _savedScrollY;
    [SerializeField] private ActionPreviewPanel _previewPanel = new ActionPreviewPanel();
    private float _splitRatio;
    private ActionDetailsSplitView _splitView;
    private VisualElement _detailsPane;
    private ActionEditorDocument _document;
    private SerializedObject _serializedAction;
    private ScrollView _scroll;
    private VisualElement _content;
    private Label _draftDerived;
    private Label _draftEnd;
    private HelpBox _draftFeedback;
    private Button _draftApply;
    private Button _draftFullClip;
    private bool _draftEditable;
    private TimingDraft _draft;
    private ActionGameplayTimingEdit _gameplayTiming;
    private LongField _gameplayStart;
    private LongField _gameplayEnd;
    private LongField _gameplayDuration;
    private bool _committingGameplayTiming;
    private bool _buildingPage;
    private bool _restoreScroll;
    private int _bindingSession;
    private VisualElement _configurationHost;
    private GameplayItem _configurationItem;
    private ActionDocumentEntry _configurationEntry;
    private bool _configurationEditable;
    private ActionSelectionValue _displayedPrimary;
    private bool _pendingConfigurationRefresh;
    private string _hitBoxRotationEditorId;
    private Quaternion _hitBoxRotationSource;
    private Vector3 _hitBoxRotationEuler;

    [MenuItem("Tools/CombatSample/Action Details")]
    public static void OpenFromMenu()
    {
        OpenShared();
    }

    internal static void OpenShared()
    {
        ActionEditorContext.Shared.EnsureDefaultAnimationSelection();
        GetWindow<ActionDetailsWindow>("Action Details").Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Action Details");
        minSize = new Vector2(760f, 360f);
        _splitRatio = EditorPrefs.GetFloat(SplitRatioPreferenceKey, 0.4f);
        if (float.IsNaN(_splitRatio) || float.IsInfinity(_splitRatio) || _splitRatio <= 0f || _splitRatio >= 1f)
            _splitRatio = 0.4f;
        if (_previewPanel == null) _previewPanel = new ActionPreviewPanel();
        ActionEditorContext.Shared.EnsureDefaultAnimationSelection();
        ActionEditorContext.Changed += OnContextChanged;
        ActionEditorInteractionGate.Changed += OnInteractionGateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        _bindingSession++;
        SaveScroll();
        _splitView?.FinishResize();
        DiscardDraft();
        ActionEditorContext.Changed -= OnContextChanged;
        ActionEditorInteractionGate.Changed -= OnInteractionGateChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        _previewPanel?.Dispose();
    }

    public void CreateGUI()
    {
        _bindingSession++;
        SaveScroll();
        _splitView?.FinishResize();
        rootVisualElement.Clear();
        ActionEditorTheme.Apply(rootVisualElement, "action-editor-details-window");
        rootVisualElement.Add(ActionEditorChrome.ContextBar(
            ActionEditorContext.Shared.CurrentAction,
            null));

        _splitView = new ActionDetailsSplitView(_splitRatio, SaveSplitRatio);
        _detailsPane = new VisualElement();
        _detailsPane.AddToClassList("action-editor-details-pane");
        _detailsPane.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        _scroll = new ScrollView(ScrollViewMode.Vertical);
        _scroll.AddToClassList("action-editor-details-scroll");
        _scroll.verticalScroller.valueChanged += value => _savedScrollY = value;
        _content = new VisualElement();
        _content.AddToClassList("action-editor-details-center");
        _scroll.Add(_content);
        _detailsPane.Add(_scroll);
        _splitView.Add(_detailsPane);
        if (_previewPanel == null) _previewPanel = new ActionPreviewPanel();
        _splitView.Add(_previewPanel.CreateView());
        rootVisualElement.Add(_splitView);
        OnInteractionGateChanged();
        RefreshDocumentAndPage(true);
    }

    private void RefreshDocumentAndPage(bool restoreScroll, bool preserveDraft = false)
    {
        if (_content == null)
            return;
        if (restoreScroll)
            SaveScroll();
        ActionEditorContext context = ActionEditorContext.Shared;
        bool keepDraft = preserveDraft && _draft != null && _draft.CanRetain(context.CurrentAction, context.PrimarySelection);
        if (!keepDraft) DiscardDraft();
        _bindingSession++;
        _document = ActionEditorContext.Shared.Document;
        _serializedAction = _document.Asset != null ? new SerializedObject(_document.Asset) : null;
        ActionEditorChrome.SyncActionField(rootVisualElement);
        RefreshPage(restoreScroll);
    }

    private void RefreshPage(bool restoreScroll = true)
    {
        if (_content == null)
            return;
        float oldScroll = restoreScroll && _scroll != null
            ? Mathf.Max(0f, _content.childCount == 0 ? _savedScrollY : _scroll.scrollOffset.y)
            : 0f;
        _buildingPage = true;
        _content.Clear();
        _draftDerived = null;
        _draftEnd = null;
        _draftFeedback = null;
        _draftApply = null;
        _draftFullClip = null;
        _gameplayTiming = null;
        _gameplayStart = null;
        _gameplayEnd = null;
        _gameplayDuration = null;
        _configurationHost = null;
        _configurationItem = null;
        _configurationEntry = null;
        ActionEditorContext context = ActionEditorContext.Shared;
        _displayedPrimary = context.PrimarySelection;

        if (_document?.Asset == null)
        {
            _buildingPage = false;
            return;
        }

        bool identityBlocked = _document.Readiness == ActionEditorReadiness.IdentityBlocked;
        if (identityBlocked)
            AddIdentityBlockedCard();

        ActionSelectionValue selection = context.PrimarySelection;
        if (selection.Kind != ActionSelectionKind.None && selection.Kind != ActionSelectionKind.Action &&
            _document.ById.TryGetValue(selection.EditorId, out ActionDocumentEntry entry))
        {
            DrawEntry(entry, !identityBlocked);
        }

        _buildingPage = false;
        if (restoreScroll && _scroll != null)
        {
            _restoreScroll = true;
            _scroll.schedule.Execute(() =>
            {
                if (_restoreScroll && _scroll != null)
                    _scroll.scrollOffset = new Vector2(0f, Mathf.Max(0f, oldScroll));
                _restoreScroll = false;
            });
        }
    }

    private void AddIdentityBlockedCard()
    {
        var section = new VisualElement();
        var repair = new Button(RepairIds) { text = "Repair Editor IDs" };
        repair.SetEnabled(!ActionEditorInteractionGate.IsActive);
        section.Add(repair);
        _content.Add(section);
    }

    private void RepairIds()
    {
        if (!ActionEditorInteractionGate.IsActive)
            ActionEditorCommands.RepairEditorIds(_document.Asset, out _, out _);
    }

    private void DrawEntry(ActionDocumentEntry entry, bool editable)
    {
        if (entry.Source is AnimationSegment)
            AddHeading(entry.DisplayName);
        SerializedProperty entryProperty = ResolveEntryProperty(entry);
        switch (entry.Source)
        {
            case AnimationSegment:
                DrawSourceTiming(entry, editable);
                break;
            case RootMotionItem root:
                VisualElement common = Section(entry.DisplayName);
                AddBoundProperty(common, entryProperty?.FindPropertyRelative("_muted"), "Muted", editable);
                _content.Add(common);
                if (root.Config == null) AddMissingConfig(common, root, editable);
                else
                {
                    AddBoundProperty(common, entryProperty?.FindPropertyRelative("_config")?.FindPropertyRelative("distanceScale"),
                        "Distance Scale", editable);
                    DrawSourceTiming(entry, editable);
                }
                break;
            case GameplayLane lane:
                DrawLane(lane, entryProperty, editable);
                break;
            case GameplayItem item:
                DrawItem(item, entry, entryProperty, editable);
                break;
        }
    }

    private void DrawSourceTiming(ActionDocumentEntry entry, bool editable)
    {
        _draft = CreateSourceDraft(entry);
        var editor = new VisualElement();

        VisualElement timing = Section("Timing");
        var timingRow = new VisualElement();
        timingRow.AddToClassList("action-editor-compact-timing-row");
        IntegerField start = DraftInt(timingRow, "Start", _draft.StartFrame, value => _draft.StartFrame = value, editable);
        start.AddToClassList("action-editor-compact-timing-field");
        Label derived = AddCompactTimingValue(timingRow, "Duration", "—");
        Label end = AddCompactTimingValue(timingRow, "End", "—");
        end.tooltip = "Exclusive end frame";
        timing.Add(timingRow);
        editor.Add(timing);

        VisualElement source = Section(entry.Source is RootMotionItem ? "Root Motion Source" : "Animation Source");
        var sourceRange = new VisualElement();
        sourceRange.AddToClassList("action-editor-compact-timing-row");
        FloatField sourceStart = DraftFloat(sourceRange, "Source Start (s)", _draft.SourceStartTime,
            value => _draft.SourceStartTime = value, editable);
        sourceStart.AddToClassList("action-editor-compact-timing-field");
        sourceStart.AddToClassList("action-editor-source-time-field");
        FloatField sourceEnd = DraftFloat(sourceRange, "Source End (s)", _draft.SourceEndTime,
            value => _draft.SourceEndTime = value, editable);
        sourceEnd.AddToClassList("action-editor-compact-timing-field");
        sourceEnd.AddToClassList("action-editor-source-time-field");
        DraftObject(source, "Animation Asset", _draft.AnimationAsset, typeof(AnimationAsset), value =>
        {
            _draft.SetAnimationAsset(value as AnimationAsset);
            sourceStart.SetValueWithoutNotify(_draft.SourceStartTime);
            sourceEnd.SetValueWithoutNotify(_draft.SourceEndTime);
        }, editable);
        source.Add(sourceRange);
        DraftFloat(source, "Play Rate", _draft.PlayRate, value => _draft.PlayRate = value, editable);
        var fullClip = new Button(() =>
        {
            if (ActionEditorInteractionGate.IsActive || !_draft.UseFullClip()) return;
            sourceStart.SetValueWithoutNotify(_draft.SourceStartTime);
            sourceEnd.SetValueWithoutNotify(_draft.SourceEndTime);
            RefreshDraftFeedback();
        }) { text = "Use Full Clip" };
        AddDraftFooter(source, fullClip, derived, end, editable);
        editor.Add(source);

        RegisterDraftKeys(editor);
        _content.Add(editor);
    }

    private void DrawLane(GameplayLane lane, SerializedProperty entryProperty, bool editable)
    {
        VisualElement section = Section("Gameplay Lane");
        AddBoundProperty(section, entryProperty?.FindPropertyRelative("_name"), "Name", editable);
        AddBoundProperty(section, entryProperty?.FindPropertyRelative("_muted"), "Muted", editable);
        AddRow(section, "Item Count", lane.Items?.Count.ToString() ?? "0");
        _content.Add(section);
    }

    private void DrawItem(GameplayItem item, ActionDocumentEntry entry, SerializedProperty itemProperty, bool editable)
    {
        VisualElement common = Section(entry.DisplayName);
        AddBoundProperty(common, itemProperty?.FindPropertyRelative("_muted"), "Muted", editable);
        if (item is PointGameplayItem || item is RangeGameplayItem)
        {
            _gameplayTiming = new ActionGameplayTimingEdit(_document.Asset, item);
            var timing = new VisualElement();
            timing.AddToClassList("action-editor-compact-timing-row");
            common.Add(timing);
            _gameplayStart = GameplayTimingField(timing, item is PointGameplayItem ? "Frame" : "Start",
                ActionGameplayTimingField.Start, _gameplayTiming.Start, editable);
            if (item is RangeGameplayItem)
            {
                _gameplayDuration = GameplayTimingField(timing, "Duration", ActionGameplayTimingField.Duration,
                    _gameplayTiming.Duration, editable);
                _gameplayEnd = GameplayTimingField(timing, "End", ActionGameplayTimingField.End, _gameplayTiming.End, editable);
                _gameplayEnd.tooltip = "Exclusive end frame";
            }
        }
        _content.Add(common);

        _configurationHost = new VisualElement();
        _configurationItem = item;
        _configurationEntry = entry;
        _configurationEditable = editable;
        _content.Add(_configurationHost);
        RebuildConfigurationSection();
    }

    private LongField GameplayTimingField(VisualElement parent, string label, ActionGameplayTimingField timingField,
        long value, bool editable)
    {
        var field = new LongField(label) { value = value, isDelayed = true };
        field.AddToClassList("action-editor-edit-field");
        field.AddToClassList("action-editor-compact-timing-field");
        field.SetEnabled(editable);
        ActionGameplayTimingEdit edit = _gameplayTiming;
        int session = _bindingSession;
        field.RegisterValueChangedCallback(evt =>
        {
            if (session != _bindingSession || _buildingPage || _gameplayTiming != edit || !edit.IsCurrent) return;
            _committingGameplayTiming = true;
            try { edit.TryCommit(timingField, evt.newValue); }
            finally { _committingGameplayTiming = false; }
            // A rejected edit has not changed the asset: silently restore all linked fields.
            if (_gameplayTiming == edit) SyncGameplayTimingFields();
        });
        parent.Add(field);
        return field;
    }

    private void SyncGameplayTimingFields()
    {
        if (_gameplayTiming == null) return;
        _gameplayStart?.SetValueWithoutNotify(_gameplayTiming.Start);
        _gameplayEnd?.SetValueWithoutNotify(_gameplayTiming.End);
        _gameplayDuration?.SetValueWithoutNotify(_gameplayTiming.Duration);
    }

    private void RebuildConfigurationSection()
    {
        if (_configurationHost == null || _configurationItem == null || _configurationEntry == null)
            return;
        _configurationHost.Clear();
        VisualElement config = Section("Configuration");
        SerializedProperty itemProperty = ResolveEntryProperty(_configurationEntry);
        SerializedProperty configProperty = itemProperty?.FindPropertyRelative("_config");
        if (GetConfig(_configurationItem) == null)
            AddMissingConfig(config, _configurationItem, _configurationEditable);
        else
            DrawTypedConfig(config, _configurationItem, configProperty, _configurationEditable);
        _configurationHost.Add(config);
    }

    private void DrawTypedConfig(VisualElement parent, GameplayItem item, SerializedProperty config, bool editable)
    {
        if (config == null)
            return;
        switch (item)
        {
            case EffectItem effect:
                DrawEffect(parent, effect, config, editable);
                break;
            case ScreenShakeItem screenShake:
                DrawScreenShake(parent, screenShake, config, editable);
                break;
            case ImpulseItem impulse:
                DrawImpulse(parent, impulse, config, editable);
                break;
            case HitBoxItem hitBox:
                DrawHitBox(parent, hitBox, config, editable);
                break;
            case SelfRotationItem:
                DrawSelfRotation(parent, config, editable);
                break;
            case VelocityOverrideItem velocity:
                DrawVelocity(parent, velocity, config, editable);
                break;
            case MoveToTargetItem:
                AddBoundProperty(parent, config.FindPropertyRelative("targetSource"), "Target Source", editable);
                AddBoundProperty(parent, config.FindPropertyRelative("speed"), "Speed (m/s)", editable);
                AddBoundProperty(parent, config.FindPropertyRelative("stopDistance"), "Stop Distance (root to root)", editable);
                AddImGuiProperty(parent, config.FindPropertyRelative("speedCurve"), "Speed Curve", editable);
                parent.Add(new HelpBox("Locks the target on entry and tracks its horizontal position. Stops permanently on arrival. " +
                    "Overrides horizontal RootMotion through the end of this window, including after arrival or target loss. " +
                    "Does not rotate the actor. Runtime only: target movement is not simulated in Preview.", HelpBoxMessageType.Info));
                break;
            case MotionPolicyItem policy:
                DrawMotionPolicy(parent, config, policy, editable);
                break;
            case TagItem:
                AddImGuiProperty(parent, config.FindPropertyRelative("tag"), "Tag", editable);
                AddBoundProperty(parent, config.FindPropertyRelative("targetContainer"), "Target Container", editable);
                break;
            default:
                break;
        }
    }

    private void DrawEffect(VisualElement parent, EffectItem item, SerializedProperty config, bool editable)
    {
        var value = item.Config;
        AddBoundProperty(parent, config.FindPropertyRelative("prefab"), "Prefab", editable);
        AddSubheading(parent, "Position Binding");
        AddBoundProperty(parent, config.FindPropertyRelative("anchor"), "Anchor", editable, true);
        if (value.anchor == ActionEffectAnchor.HumanoidBone)
            AddBoundProperty(parent, config.FindPropertyRelative("bone"), "Humanoid Bone", editable);
        else if (value.anchor == ActionEffectAnchor.TransformPath)
            AddBoundProperty(parent, config.FindPropertyRelative("transformPath"), "Path from Actor Root", editable);
        AddBoundProperty(parent, config.FindPropertyRelative("localPosition"), "Local Position", editable);
        AddBoundProperty(parent, config.FindPropertyRelative("followAnchor"), "Follow Position / Scale", editable);
        AddSubheading(parent, "Orientation");
        AddBoundProperty(parent, config.FindPropertyRelative("orientation"), "Orientation", editable, true);
        AddBoundProperty(parent, config.FindPropertyRelative("orientationUpdate"), "Update Policy", editable);
        if (value.orientation == ActionEffectOrientation.ScreenOriented)
        {
            AddBoundProperty(parent, config.FindPropertyRelative("screenAngleMode"), "Screen Angle Mode", editable, true);
            AddBoundProperty(parent, config.FindPropertyRelative(value.screenAngleMode == ScreenAngleMode.Preset ? "anglePresetDegrees" : "angleRandomRange"),
                value.screenAngleMode == ScreenAngleMode.Preset ? "Screen Angle (degrees)" : "Random Screen Angle [0, max]", editable);
        }
        else if (value.orientation == ActionEffectOrientation.WorldDirectional)
        {
            AddBoundProperty(parent, config.FindPropertyRelative("worldDirection"), "World Direction", editable, true);
            if (value.worldDirection == ActionEffectWorldDirection.Custom)
                AddBoundProperty(parent, config.FindPropertyRelative("customWorldDirection"), "World Vector", editable);
            AddBoundProperty(parent, config.FindPropertyRelative("rollMode"), "Roll Mode", editable, true);
            AddBoundProperty(parent, config.FindPropertyRelative(value.rollMode == VFXRollMode.Preset ? "rollPresetDegrees" : "rollRandomRange"),
                value.rollMode == VFXRollMode.Preset ? "Roll (degrees)" : "Random Roll [0, max]", editable);
        }
        AddBoundProperty(parent, config.FindPropertyRelative("localEulerAngles"), "Rotation Offset (degrees)", editable);
        AddSubheading(parent, "Playback");
        AddBoundProperty(parent, config.FindPropertyRelative("scale"), "Scale Multiplier", editable);
        AddBoundProperty(parent, config.FindPropertyRelative("simulationSpeed"), "Particle Speed Multiplier", editable);
        AddBoundProperty(parent, config.FindPropertyRelative("occlusionMode"), "Occlusion", editable);
        parent.Add(new HelpBox("One instance per interval, strictly destroyed at exit/interruption/Abort; no particle tail. " +
            "Position binding and orientation are independent. Match Position Binding = continuous orientation when following, " +
            "otherwise entry-only. Random angles are sampled once, including continuous facing. " +
            "Screen orientation uses the same emitter-axis convention as Damage VFX. Particle speed is explicit and does not follow Action pause/speed.", HelpBoxMessageType.Info));
        parent.Add(new IMGUIContainer(() => DrawEffectDiagnostics(item)));
    }

    private void DrawEffectDiagnostics(EffectItem item)
    {
        if (!ActionEffectRequest.TryCreate(item.Config, out var request, out string error))
        { EditorGUILayout.HelpBox(error, MessageType.Error); return; }
        GameObject character = ActionPreviewCharacterResolver.Resolve(_document?.Asset, ActionEditorContext.Shared.PreviewCharacter);
        if (character != null && !ActionEffectAnchorResolver.TryResolve(request, character.transform,
            character.GetComponentInChildren<Animator>(true), out _, out error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
        error = ActionPresentationPreview.ValidatePrefab(request.Prefab);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
        bool autoplay = false;
        foreach (var particle in request.Prefab.GetComponentsInChildren<ParticleSystem>(true)) autoplay |= particle.main.playOnAwake;
        if (!autoplay) EditorGUILayout.HelpBox("No autoplay particles. Runtime does not force Play; scripted initialization is runtime-only.", MessageType.Warning);
        if (item.DurationFrames <= 2) DrawShortPresentationWarning();
        EditorGUILayout.HelpBox("Preview is an isolated, seeded ParticleSystem sample at nominal 1x timeline age. " +
            "It uses the preview camera and current emitter pose, not gameplay pause history or world-space emission trails along the whole movement path.", MessageType.Info);
    }

    private void DrawScreenShake(VisualElement parent, ScreenShakeItem item, SerializedProperty config, bool editable)
    {
        if (item.Config.screenShake == null) { AddMissingConfig(parent, item, editable); return; }
        SerializedProperty shake = config.FindPropertyRelative("screenShake");
        AddBoundProperty(parent, shake.FindPropertyRelative("enabled"), "Enabled", editable);
        AddBoundProperty(parent, shake.FindPropertyRelative("intensity"), "Position Amplitude (Unity units)", editable);
        AddBoundProperty(parent, config.FindPropertyRelative("mode"), "Signal Mode", editable, true);
        if (item.Config.mode == ActionScreenShakeMode.Directional)
        {
            AddBoundProperty(parent, config.FindPropertyRelative("direction"), "Camera-space Direction", editable, true);
            if (item.Config.direction == ActionScreenShakeDirection.Custom)
                AddBoundProperty(parent, config.FindPropertyRelative("customDirection"), "Direction Vector (normalized)", editable);
            AddImGuiProperty(parent, config.FindPropertyRelative("waveform"), "Signed Motion", editable);
        }
        else
        {
            AddImGuiProperty(parent, config.FindPropertyRelative("positionX"), "X / Right", editable);
            AddImGuiProperty(parent, config.FindPropertyRelative("positionY"), "Y / Up", editable);
            AddImGuiProperty(parent, config.FindPropertyRelative("positionZ"), "Z / Forward", editable);
        }
        AddBoundProperty(parent, config.FindPropertyRelative("useRotation"), "Rotation Signal", editable, true);
        if (item.Config.useRotation)
        {
            AddBoundProperty(parent, config.FindPropertyRelative("rotationAmplitude"), "Rotation Amplitude (degrees)", editable);
            AddImGuiProperty(parent, config.FindPropertyRelative("pitch"), "Pitch / X", editable);
            AddImGuiProperty(parent, config.FindPropertyRelative("yaw"), "Yaw / Y", editable);
            AddImGuiProperty(parent, config.FindPropertyRelative("roll"), "Roll / Z", editable);
        }
        AddBoundProperty(parent, config.FindPropertyRelative("maxDistance"), "Max Distance", editable);
        parent.Add(new HelpBox("Curves: X = normalized Action progress [0,1], Y = signed motion × the corresponding amplitude. " +
            "No hidden sine, frequency or secondary reaction. Pause holds the current sample, including nonzero offsets. " +
            "Dedicated native Impulse channel 2, per-virtual-camera space before blending; no final-Camera Transform writes. " +
            "Exit cancels only this event with no tail. Preview draws direction/rotation guides but never emits camera shake.", HelpBoxMessageType.Info));
        parent.Add(new IMGUIContainer(() => {
            if (!item.Config.screenShake.enabled) return;
            if (!ActionScreenShakeRequest.TryCreate(item.Config, out var request, out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            else if (request.HasNonZeroEndpoint()) EditorGUILayout.HelpBox("Nonzero signal endpoint: strict entry/exit may pop. Your curves are not modified.", MessageType.Warning);
            if (item.DurationFrames <= 2) DrawShortPresentationWarning();
        }));
    }

    private static void DrawShortPresentationWarning() => EditorGUILayout.HelpBox(
        "Very short interval: it can finish between renders. A one-frame shake samples only t=0; endpoint-zero waves may never become visible. " +
        "No hidden minimum visible frame or automatic tail is added.", MessageType.Warning);

    private void DrawImpulse(VisualElement parent, ImpulseItem item, SerializedProperty config, bool editable)
    {
        if (item.Config.impulse == null)
        {
            AddMissingConfig(parent, item, editable);
            return;
        }
        SerializedProperty impulse = config.FindPropertyRelative("impulse");
        bool horizontal = item.Config.useHorizontalImpulse;
        bool vertical = item.Config.useVerticalBallistic;
        AddBoundProperty(parent, config.FindPropertyRelative("useHorizontalImpulse"), "Horizontal", editable, true);
        if (horizontal)
        {
            AddBoundProperty(parent, impulse.FindPropertyRelative("directionMode"), "Direction", editable, true);
            if (item.Config.impulse.directionMode == ImpulseDirectionMode.LocalHorizontal)
                AddBoundProperty(parent, impulse.FindPropertyRelative("localHorizontalDirection"), "Local Direction", editable);
            AddBoundProperty(parent, impulse.FindPropertyRelative("horizontalForce"), "Horizontal Strength", editable);
        }
        AddBoundProperty(parent, config.FindPropertyRelative("useVerticalBallistic"), "Vertical", editable, true);
        if (vertical)
        {
            AddBoundProperty(parent, impulse.FindPropertyRelative("verticalForce"), "Vertical Strength", editable);
            AddBoundProperty(parent, config.FindPropertyRelative("verticalOperation"), "Vertical Operation", editable);
        }
        if (item.Config.overrideGravityScale)
        {
            var disable = new Button(() => SetSerializedBoolean(config.FindPropertyRelative("overrideGravityScale"), false))
                { text = "Disable Legacy Gravity Override" };
            disable.SetEnabled(editable);
            parent.Add(disable);
        }
    }

    private void DrawHitBox(VisualElement parent, HitBoxItem item, SerializedProperty config, bool editable)
    {
        if (item.Config.hitboxConfig == null || item.Config.dataConfig == null || item.Config.effects == null)
        {
            AddMissingConfig(parent, item, editable);
            return;
        }
        AddSubheading(parent, "Binding");
        AddBoundProperty(parent, config.FindPropertyRelative("anchor"), "Anchor", editable, true);
        AddSubheading(parent, "Shape");
        SerializedProperty shape = config.FindPropertyRelative("hitboxConfig");
        AddBoundProperty(parent, shape.FindPropertyRelative("shape"), "Type", editable, true);
        AddBoundProperty(parent, shape.FindPropertyRelative("center"), "Local Center", editable);
        ActionHitBoxShape shapeType = item.Config.hitboxConfig.shape;
        if (shapeType != ActionHitBoxShape.Sphere)
            AddHitBoxRotation(parent, shape.FindPropertyRelative("rotation"), item.EditorId, editable);
        if (shapeType == ActionHitBoxShape.Box)
            AddBoundProperty(parent, shape.FindPropertyRelative("size"), "Full Size", editable);
        else if (shapeType == ActionHitBoxShape.Sphere)
            AddBoundProperty(parent, shape.FindPropertyRelative("radius"), "Radius", editable);
        else if (shapeType == ActionHitBoxShape.Capsule)
        {
            AddBoundProperty(parent, shape.FindPropertyRelative("height"), "Total Height", editable);
            AddBoundProperty(parent, shape.FindPropertyRelative("radius"), "Radius", editable);
        }
        AddSubheading(parent, "Attack Data");
        AddImGuiProperty(parent, config.FindPropertyRelative("dataConfig"), "Attack Data", editable);
        AddSubheading(parent, "Effects");
        AddEffectsList(parent, config.FindPropertyRelative("effects"), editable);
    }

    private void AddEffectsList(VisualElement parent, SerializedProperty property, bool editable)
    {
        if (property == null)
            return;

        PropertyWatch watch = CreateWatch(property, false, false, false);
        var list = new EffectListGUI((serializedObject, path, type) =>
            ApplyEffectTypeSelection(serializedObject, path, type, watch));
        var container = new IMGUIContainer(() => DrawImGuiProperty(watch, "Effects", editable, list.Draw));
        container.AddToClassList("action-editor-imgui-property");
        parent.Add(container);
    }

    private void ApplyEffectTypeSelection(
        SerializedObject serializedObject, string path, Type type, PropertyWatch watch)
    {
        if (this == null || watch.Session != _bindingSession || _serializedAction != serializedObject ||
            ActionEditorInteractionGate.IsActive)
            return;
        serializedObject.UpdateIfRequiredOrScript();
        SerializedProperty current = serializedObject.FindProperty(path);
        if (current == null)
            return;
        current.SetManagedReference(type);
        current.isExpanded = type != null;
        if (serializedObject.ApplyModifiedProperties())
            OnLeafChanged(watch);
        Repaint();
    }

    private void AddHitBoxRotation(
        VisualElement parent, SerializedProperty property, string editorId, bool editable)
    {
        PropertyWatch watch = CreateWatch(property, false, false, false);
        var container = new IMGUIContainer(() =>
        {
            if (watch.Session != _bindingSession || _serializedAction == null) return;
            _serializedAction.UpdateIfRequiredOrScript();
            SerializedProperty current = _serializedAction.FindProperty(watch.Path);
            if (current == null) return;
            Quaternion source = current.quaternionValue;
            if (!string.Equals(_hitBoxRotationEditorId, editorId, StringComparison.Ordinal)
                || !SameQuaternion(_hitBoxRotationSource, source))
            {
                _hitBoxRotationEditorId = editorId;
                _hitBoxRotationSource = source;
                _hitBoxRotationEuler = source.eulerAngles;
            }
            EditorGUI.BeginDisabledGroup(!editable || ActionEditorInteractionGate.IsActive);
            EditorGUI.BeginChangeCheck();
            Vector3 value = EditorGUILayout.Vector3Field("Local Rotation", _hitBoxRotationEuler);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.EndDisabledGroup();
            if (!changed) return;
            _hitBoxRotationEuler = value;
            current.quaternionValue = Quaternion.Euler(value);
            _hitBoxRotationSource = current.quaternionValue;
            _serializedAction.ApplyModifiedProperties();
            OnLeafChanged(watch);
        });
        container.AddToClassList("action-editor-imgui-property");
        parent.Add(container);
    }

    private static bool SameQuaternion(Quaternion left, Quaternion right) =>
        Mathf.Abs(left.x - right.x) <= 1e-6f && Mathf.Abs(left.y - right.y) <= 1e-6f
        && Mathf.Abs(left.z - right.z) <= 1e-6f && Mathf.Abs(left.w - right.w) <= 1e-6f;

    private void DrawSelfRotation(VisualElement parent, SerializedProperty config, bool editable)
    {
        AddBoundProperty(parent, config.FindPropertyRelative("source"), "Source", editable, true);
        AddBoundProperty(parent, config.FindPropertyRelative("mode"), "Mode", editable, true);
        var source = (ActionSelfRotationSource)config.FindPropertyRelative("source").enumValueIndex;
        var mode = (ActionSelfRotationMode)config.FindPropertyRelative("mode").enumValueIndex;
        if (source == ActionSelfRotationSource.RootMotion)
        {
            AddBoundProperty(parent, config.FindPropertyRelative("animationAsset"), "Animation Asset", editable);
            AddBoundProperty(parent, config.FindPropertyRelative("sourceStartTime"), "Source Start", editable);
            AddBoundProperty(parent, config.FindPropertyRelative("playRate"), "Play Rate", editable);
        }
        else if (source == ActionSelfRotationSource.Target)
            AddBoundProperty(parent, config.FindPropertyRelative("targetSource"), "Target Source", editable);
        else if (source == ActionSelfRotationSource.Direction)
        {
            AddBoundProperty(parent, config.FindPropertyRelative("directionSource"), "Direction Source", editable, true);
            if ((ActionSelfRotationDirectionSource)config.FindPropertyRelative("directionSource").enumValueIndex == ActionSelfRotationDirectionSource.PresetLocal)
                AddBoundProperty(parent, config.FindPropertyRelative("presetLocalDirection"), "Preset Local Direction", editable);
        }
        if (mode == ActionSelfRotationMode.RotateBySpeed)
            AddBoundProperty(parent, config.FindPropertyRelative("angularSpeedDegrees"), "Angular Speed", editable);
    }

    private void DrawVelocity(VisualElement parent, VelocityOverrideItem item, SerializedProperty config, bool editable)
    {
        if (item.Config.velocity == null)
        {
            AddMissingConfig(parent, item, editable);
            return;
        }
        SerializedProperty velocity = config.FindPropertyRelative("velocity");
        bool horizontal = item.Config.velocity.useHorizontalVelocity;
        bool vertical = item.Config.velocity.useVerticalVelocity;
        AddBoundProperty(parent, velocity.FindPropertyRelative("useHorizontalVelocity"), "Horizontal", editable, true);
        if (horizontal)
        {
            AddBoundProperty(parent, velocity.FindPropertyRelative("directionMode"), "Direction", editable, true);
            if (item.Config.velocity.directionMode == MotionDirectionMode.LocalHorizontal)
                AddBoundProperty(parent, velocity.FindPropertyRelative("localHorizontalDirection"), "Local Direction", editable);
            AddBoundProperty(parent, velocity.FindPropertyRelative("horizontalSpeed"), "Horizontal Speed", editable);
            AddImGuiProperty(parent, velocity.FindPropertyRelative("horizontalCurve"), "Horizontal Curve", editable);
        }
        AddBoundProperty(parent, velocity.FindPropertyRelative("useVerticalVelocity"), "Vertical", editable, true);
        if (vertical)
        {
            AddBoundProperty(parent, velocity.FindPropertyRelative("verticalSpeed"), "Vertical Speed", editable);
            AddImGuiProperty(parent, velocity.FindPropertyRelative("verticalCurve"), "Vertical Curve", editable);
        }
    }

    private void DrawMotionPolicy(VisualElement parent, SerializedProperty config, MotionPolicyItem item, bool editable)
    {
        var watch = CreateWatch(config, false, false, false);
        var container = new IMGUIContainer(() => DrawImGuiProperty(watch, string.Empty, editable,
            property => ActionMotionPolicyGUI.DrawWindow(property, _document.Asset, item)));
        container.AddToClassList("action-editor-imgui-property");
        parent.Add(container);
    }

    private TimingDraft CreateSourceDraft(ActionDocumentEntry entry)
    {
        if (_draft != null && _draft.Kind == entry.SelectionKind && _draft.EditorId == entry.EditorId &&
            ReferenceEquals(_draft.Source, entry.Source))
            return _draft;
        return TimingDraft.Create(_document, entry);
    }

    private void AddDraftFooter(VisualElement parent, Button fullClip, Label derived, Label end, bool editable)
    {
        _draftEditable = editable;
        _draftFeedback = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        parent.Add(_draftFeedback);
        VisualElement buttons = MiniButtons();
        buttons.AddToClassList("action-editor-source-buttons");
        _draftFullClip = fullClip;
        fullClip.AddToClassList("action-editor-full-clip-button");
        _draftApply = new Button(ApplyDraft) { text = "Apply", tooltip = "Apply source and timing changes (Enter)" };
        var revert = new Button(RevertDraft) { text = "Revert", tooltip = "Discard unapplied changes (Esc)" };
        revert.SetEnabled(editable);
        buttons.Add(fullClip);
        buttons.Add(_draftApply);
        buttons.Add(revert);
        parent.Add(buttons);
        _draftDerived = derived;
        _draftEnd = end;
        RefreshDraftFeedback();
    }

    private void RefreshDraftFeedback()
    {
        ActionTimelineOperationResult result = EvaluateDraft();
        int duration = result?.State == ActionTimelineOperationState.NoChange ? _draft.DurationFrames :
            _draft != null ? AnimationSegment.CalculateDerivedDurationFrames(_draft.SourceStartTime, _draft.SourceEndTime, _draft.PlayRate) : 0;
        if (_draftDerived != null) _draftDerived.text = duration > 0 ? $"{duration} frames" : "—";
        if (_draftEnd != null && _draft != null)
            _draftEnd.text = duration > 0 ? ((long)_draft.StartFrame + duration).ToString() : "—";
        bool rejected = result?.State == ActionTimelineOperationState.Rejected;
        bool changed = result?.State == ActionTimelineOperationState.Allowed;
        _draftApply?.SetEnabled(_draftEditable && changed);
        _draftFullClip?.SetEnabled(_draftEditable && _draft?.AnimationAsset != null && _draft.AnimationAsset.Clip != null);
        ActionEditorContext context = ActionEditorContext.Shared;
        bool previewing = changed && context.SetSourcePreview(_draft, _draft.Snapshot, _draft.Input);
        if (!previewing) context.ClearSourcePreview(_draft);
        if (_draftFeedback != null)
        {
            _draftFeedback.text = rejected ? result.Message + "\nPreview shows applied data until this is resolved." :
                previewing ? "Previewing unapplied changes — Apply / Enter to save, Revert / Esc to restore." :
                "Unapplied changes — Apply / Enter to save, Revert / Esc to discard. Preview is unavailable in Play Mode.";
            _draftFeedback.messageType = rejected ? HelpBoxMessageType.Error : HelpBoxMessageType.Info;
            _draftFeedback.style.display = rejected || changed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private ActionTimelineOperationResult EvaluateDraft()
    {
        if (_draft?.Snapshot == null)
            return null;
        if (!_draft.Snapshot.MatchesCurrentSource(out string message))
            return new ActionTimelineOperationResult { State = ActionTimelineOperationState.Rejected, Message = message };
        return _draft.Evaluate();
    }

    private void ApplyDraft()
    {
        if (ActionEditorInteractionGate.IsActive)
            return;
        TimingDraft draft = _draft;
        if (draft == null) return;
        if (!draft.TryApply(out string message))
        {
            ActionEditorContext.Shared.ClearSourcePreview(draft);
            if (_draftFeedback != null)
            {
                _draftFeedback.text = message;
                _draftFeedback.messageType = HelpBoxMessageType.Error;
                _draftFeedback.style.display = DisplayStyle.Flex;
            }
            return; // Keep all input intact on a rejected Apply.
        }
        if (ReferenceEquals(_draft, draft)) RefreshDocumentAndPage(true);
    }

    private void RevertDraft()
    {
        if (_document?.Asset == null)
            return;
        RefreshDocumentAndPage(true);
    }

    private IntegerField DraftInt(VisualElement parent, string label, int value, Action<int> changed, bool editable)
    {
        var field = new IntegerField(label) { value = value };
        field.AddToClassList("action-editor-edit-field");
        field.SetEnabled(editable);
        field.RegisterValueChangedCallback(evt => { changed(evt.newValue); RefreshDraftFeedback(); });
        parent.Add(field);
        return field;
    }

    private FloatField DraftFloat(VisualElement parent, string label, float value, Action<float> changed, bool editable)
    {
        var field = new FloatField(label) { value = value };
        field.AddToClassList("action-editor-edit-field");
        field.SetEnabled(editable);
        field.RegisterValueChangedCallback(evt => { changed(evt.newValue); RefreshDraftFeedback(); });
        parent.Add(field);
        return field;
    }

    private ObjectField DraftObject(VisualElement parent, string label, UnityEngine.Object value, Type objectType,
        Action<UnityEngine.Object> changed, bool editable)
    {
        var field = new ObjectField(label) { objectType = objectType, value = value, allowSceneObjects = false };
        field.AddToClassList("action-editor-edit-field");
        field.SetEnabled(editable);
        field.RegisterValueChangedCallback(evt => { changed(evt.newValue); RefreshDraftFeedback(); });
        parent.Add(field);
        return field;
    }

    private void RegisterDraftKeys(VisualElement section)
    {
        section.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                ApplyDraft();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                RevertDraft();
                evt.StopPropagation();
            }
        }, TrickleDown.TrickleDown);
    }

    private void AddMissingConfig(VisualElement parent, GameplayItem item, bool editable)
    {
        var create = new Button(() => CreateMissingConfig(item)) { text = "Create Default Configuration" };
        create.SetEnabled(editable && !ActionEditorInteractionGate.IsActive);
        parent.Add(create);
    }

    private void CreateMissingConfig(GameplayItem item)
    {
        if (item == null || _document?.Asset == null || ActionEditorInteractionGate.IsActive)
            return;
        ActionEditorCommands.EnsureItemConfiguration(_document.Asset, item.EditorId);
    }

    private object GetConfig(GameplayItem item)
    {
        return item switch
        {
            EffectItem value => value.Config,
            ScreenShakeItem value => value.Config,
            ImpulseItem value => value.Config,
            HitBoxItem value => value.Config,
            RootMotionItem value => value.Config,
            SelfRotationItem value => value.Config,
            VelocityOverrideItem value => value.Config,
            MoveToTargetItem value => value.Config,
            MotionPolicyItem value => value.Config,
            TagItem value => value.Config,
            _ => null,
        };
    }

    private SerializedProperty ResolveEntryProperty(ActionDocumentEntry entry)
    {
        if (_serializedAction == null || entry == null)
            return null;
        SerializedProperty timeline = _serializedAction.FindProperty("_actionTimeline");
        if (entry.SelectionKind == ActionSelectionKind.AnimationSegment)
            return timeline?.FindPropertyRelative("_animationSegments")?.GetArrayElementAtIndex(entry.ItemIndex);
        SerializedProperty lanes = timeline?.FindPropertyRelative("_gameplayLanes");
        if (lanes == null || entry.LaneIndex < 0 || entry.LaneIndex >= lanes.arraySize)
            return null;
        SerializedProperty lane = lanes.GetArrayElementAtIndex(entry.LaneIndex);
        if (entry.SelectionKind == ActionSelectionKind.GameplayLane)
            return lane;
        SerializedProperty items = lane.FindPropertyRelative("_items");
        return items != null && entry.ItemIndex >= 0 && entry.ItemIndex < items.arraySize
            ? items.GetArrayElementAtIndex(entry.ItemIndex)
            : null;
    }

    private PropertyField AddBoundProperty(VisualElement parent, string propertyPath, string label, bool editable,
        bool rebuildConditional = false, bool rebuildTrigger = false, bool rebuildCancellation = false)
    {
        return AddBoundProperty(parent, _serializedAction?.FindProperty(propertyPath), label, editable,
            rebuildConditional, rebuildTrigger, rebuildCancellation);
    }

    private PropertyField AddBoundProperty(VisualElement parent, SerializedProperty property, string label, bool editable,
        bool rebuildConditional = false, bool rebuildTrigger = false, bool rebuildCancellation = false)
    {
        if (property == null)
            return null;
        var field = string.IsNullOrEmpty(label) ? new PropertyField(property) : new PropertyField(property, label);
        field.AddToClassList("action-editor-edit-field");
        if (property.propertyType == SerializedPropertyType.Boolean)
            field.AddToClassList("action-editor-bool-field");
        field.SetEnabled(editable);
        parent.Add(field);
        PropertyWatch watch = CreateWatch(property, rebuildConditional, rebuildTrigger, rebuildCancellation);
        field.TrackPropertyValue(property, _ => OnLeafChanged(watch));
        field.BindProperty(property);
        return field;
    }

    private IMGUIContainer AddImGuiProperty(VisualElement parent, string propertyPath, string label, bool editable,
        bool rebuildConfiguration = false, bool rebuildTrigger = false, bool rebuildCancellation = false)
    {
        return AddImGuiProperty(parent, _serializedAction?.FindProperty(propertyPath), label, editable,
            rebuildConfiguration, rebuildTrigger, rebuildCancellation);
    }

    private IMGUIContainer AddImGuiProperty(VisualElement parent, SerializedProperty property, string label, bool editable,
        bool rebuildConfiguration = false, bool rebuildTrigger = false, bool rebuildCancellation = false)
    {
        if (property == null)
            return null;
        PropertyWatch watch = CreateWatch(property, rebuildConfiguration, rebuildTrigger, rebuildCancellation);
        var container = new IMGUIContainer(() => DrawImGuiProperty(watch, label, editable));
        container.AddToClassList("action-editor-imgui-property");
        parent.Add(container);
        return container;
    }

    private PropertyWatch CreateWatch(SerializedProperty property, bool rebuildConfiguration, bool rebuildTrigger,
        bool rebuildCancellation)
    {
        bool name = property.propertyPath.EndsWith("._name", StringComparison.Ordinal);
        bool muted = property.propertyPath.EndsWith("._muted", StringComparison.Ordinal);
        return new PropertyWatch
        {
            Path = property.propertyPath,
            Session = _bindingSession,
            RebuildConfiguration = rebuildConfiguration,
            ChangeFlags = name
                ? ActionEditorChangeFlags.Presentation
                : muted
                    ? ActionEditorChangeFlags.Content | ActionEditorChangeFlags.Presentation
                    : ActionEditorChangeFlags.Content,
        };
    }

    private void DrawImGuiProperty(PropertyWatch watch, string label, bool editable,
        Action<SerializedProperty> drawProperty = null)
    {
        if (watch == null || watch.Session != _bindingSession || _serializedAction == null)
            return;
        _serializedAction.UpdateIfRequiredOrScript();
        SerializedProperty property = _serializedAction.FindProperty(watch.Path);
        if (property == null)
            return;
        EditorGUI.BeginDisabledGroup(!editable || ActionEditorInteractionGate.IsActive);
        EditorGUI.BeginChangeCheck();
        if (drawProperty == null)
            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        else
            drawProperty(property);
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUI.EndDisabledGroup();
        // Foldout/view changes are not content edits and must not dirty the Action.
        if (changed && _serializedAction.ApplyModifiedProperties())
            OnLeafChanged(watch);
    }

    private void OnLeafChanged(PropertyWatch watch)
    {
        if (watch == null || watch.Session != _bindingSession || _buildingPage || _document?.Asset == null)
            return;
        _pendingConfigurationRefresh |= watch.RebuildConfiguration;
        ActionEditorContext.Shared.QueueBindingChange(_document.Asset, watch.ChangeFlags);
    }

    private void FlushPendingBindingSectionRefresh()
    {
        bool configuration = _pendingConfigurationRefresh;
        ClearPendingBindingSectionRefresh();
        if (!configuration)
            return;
        int session = _bindingSession;
        rootVisualElement.schedule.Execute(() =>
        {
            if (session != _bindingSession)
                return;
            if (configuration) RebuildConfigurationSection();
            RefreshDocumentOnly();
        });
    }

    private void ClearPendingBindingSectionRefresh()
    {
        _pendingConfigurationRefresh = false;
    }

    private void RefreshDocumentOnly()
    {
        _document = ActionEditorContext.Shared.Document;
        _serializedAction?.UpdateIfRequiredOrScript();
    }

    private void SetSerializedBoolean(SerializedProperty property, bool value)
    {
        if (property == null || ActionEditorInteractionGate.IsActive)
            return;
        property.boolValue = value;
        property.serializedObject.ApplyModifiedProperties();
        OnLeafChanged(CreateWatch(property, true, false, false));
    }

    private void AddHeading(string title)
    {
        var titleLabel = new Label(title);
        titleLabel.AddToClassList("action-editor-details-heading");
        _content.Add(titleLabel);
    }

    private static VisualElement Section(string title)
    {
        var section = new VisualElement();
        section.AddToClassList("action-editor-section");
        AddSubheading(section, title).AddToClassList("action-editor-card-title");
        return section;
    }

    private static Label AddSubheading(VisualElement parent, string title)
    {
        var heading = new Label(title);
        heading.AddToClassList("action-editor-section-title");
        parent.Add(heading);
        return heading;
    }

    private static Label AddRow(VisualElement parent, string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("action-editor-readonly-row");
        var key = new Label(label);
        key.AddToClassList("action-editor-readonly-label");
        row.Add(key);
        var result = new Label(string.IsNullOrEmpty(value) ? "—" : value);
        result.AddToClassList("action-editor-readonly-value");
        row.Add(result);
        parent.Add(row);
        return result;
    }


    private static Label AddCompactTimingValue(VisualElement parent, string label, string value)
    {
        var item = new VisualElement();
        item.AddToClassList("action-editor-compact-timing-value");
        var title = new Label(label);
        title.AddToClassList("action-editor-compact-timing-label");
        item.Add(title);
        var result = new Label(value);
        result.AddToClassList("action-editor-compact-timing-result");
        item.Add(result);
        parent.Add(item);
        return result;
    }

    private static VisualElement MiniButtons()
    {
        var row = new VisualElement();
        row.AddToClassList("action-editor-mini-buttons");
        return row;
    }

    private static void AddMiniButton(VisualElement parent, string text, Action clicked, bool enabled)
    {
        var button = new Button(clicked) { text = text };
        button.SetEnabled(enabled);
        parent.Add(button);
    }

    private void OnContextChanged(ActionEditorChange change)
    {
        _previewPanel?.OnContextChanged(change);
        ActionEditorChangeFlags flags = change.Flags;
        if (_committingGameplayTiming && change.Origin == ActionEditorChangeOrigin.Command &&
            (flags & ActionEditorChangeFlags.Timing) != 0 &&
            _gameplayTiming != null && _gameplayTiming.IsCurrent &&
            (flags & (ActionEditorChangeFlags.Context | ActionEditorChangeFlags.Structure |
                      ActionEditorChangeFlags.Selection)) == 0)
        {
            // Keep the control tree (and Tab/focus order) alive for this field's own commit.
            RefreshDocumentOnly();
            _configurationEntry = _document.ById[_displayedPrimary.EditorId];
            SyncGameplayTimingFields();
            FlushPendingBindingSectionRefresh();
            return;
        }
        if ((flags & (ActionEditorChangeFlags.Context | ActionEditorChangeFlags.Structure |
                      ActionEditorChangeFlags.Timing)) != 0)
        {
            ClearPendingBindingSectionRefresh();
            RefreshDocumentAndPage(true, (flags & ActionEditorChangeFlags.Context) == 0);
            return;
        }

        if ((flags & ActionEditorChangeFlags.Content) != 0)
        {
            // A native binding has already changed the visible control. Keep that control tree alive so
            // focus and drafts survive; OnLeafChanged refreshes the affected conditional section after
            // Unity has finished dispatching the binding callback.
            if (change.Origin == ActionEditorChangeOrigin.Binding &&
                (_draft == null || _draft.CanRetain(ActionEditorContext.Shared.CurrentAction,
                    ActionEditorContext.Shared.PrimarySelection)))
            {
                RefreshDocumentOnly();
                if (_draft != null) RefreshDraftFeedback();
            }
            else
            {
                ClearPendingBindingSectionRefresh();
                RefreshDocumentAndPage(true, true);
            }
        }
        else if ((flags & ActionEditorChangeFlags.Presentation) != 0)
            RefreshDocumentOnly();
        if ((flags & ActionEditorChangeFlags.Selection) != 0 &&
            !SameSelection(ActionEditorContext.Shared.PrimarySelection, _displayedPrimary))
            RefreshDocumentAndPage(true);
        FlushPendingBindingSectionRefresh();
    }

    private void OnInteractionGateChanged() => _content?.SetEnabled(!ActionEditorInteractionGate.IsActive);

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        bool narrow = evt.newRect.width < 520f;
        _detailsPane.EnableInClassList("action-editor-details-narrow", narrow);
    }

    private static string SplitRatioPreferenceKey => "CombatSample.ActionDetails.SplitRatio." + Application.dataPath;

    private void SaveSplitRatio(float ratio)
    {
        _splitRatio = ratio;
        EditorPrefs.SetFloat(SplitRatioPreferenceKey, ratio);
    }

    private void OnLostFocus() => _previewPanel?.CancelCameraInteraction();

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        _previewPanel?.OnPlayModeStateChanged(state);
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            ActionEditorContext.Shared.ClearSourcePreview(_draft);
            if (_draft != null) RefreshDraftFeedback();
        }
        else if (state == PlayModeStateChange.EnteredEditMode && _draft != null)
            RefreshDraftFeedback();
    }

    private void SaveScroll()
    {
        if (_scroll != null)
            _savedScrollY = _scroll.scrollOffset.y;
    }

    private void DiscardDraft()
    {
        TimingDraft previous = _draft;
        _draft = null;
        if (previous != null) ActionEditorContext.Shared.ClearSourcePreview(previous);
    }

    private static bool SameSelection(ActionSelectionValue left, ActionSelectionValue right) =>
        left.Kind == right.Kind && string.Equals(left.EditorId, right.EditorId, StringComparison.Ordinal);
}

/// <summary>Keeps the user's preferred proportion separate from temporary minimum-width constraints.</summary>
internal sealed class ActionDetailsSplitView : TwoPaneSplitView
{
    private const float MinDetailsWidth = 300f;
    private const float MinPreviewWidth = 320f;
    private readonly Action<float> _saveRatio;
    private readonly VisualElement _dragHandle;
    private IVisualElementScheduledItem _layoutUpdate;
    private float _ratio;
    private int _dragPointer = -1;
    private float _dragStartWidth;
    private bool _hostResizedDuringDrag;

    internal ActionDetailsSplitView(float ratio, Action<float> saveRatio)
        : base(0, MinDetailsWidth, TwoPaneSplitViewOrientation.Horizontal)
    {
        _ratio = ratio;
        _saveRatio = saveRatio;
        AddToClassList("action-editor-details-split");
        // Observe the native splitter's gesture; its manipulator continues to own dragging and capture.
        _dragHandle = this.Q<VisualElement>("unity-dragline-anchor");
        RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
        RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut, TrickleDown.TrickleDown);
        RegisterCallback<GeometryChangedEvent>(OnSizeChanged);
        RegisterCallback<DetachFromPanelEvent>(_ => FinishResize());
    }

    private float DetailsWidth => fixedPane == null ? MinDetailsWidth : fixedPane.style.width.value.value;

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || !(evt.target is VisualElement target) ||
            (target != _dragHandle && !_dragHandle.Contains(target)))
            return;
        _dragPointer = evt.pointerId;
        _dragStartWidth = DetailsWidth;
        _hostResizedDuringDrag = false;
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId == _dragPointer && evt.button == 0)
            FinishResize();
    }

    private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        if (evt.pointerId == _dragPointer && evt.target == _dragHandle)
            FinishResize();
    }

    internal void FinishResize()
    {
        if (_dragPointer < 0)
            return;
        _dragPointer = -1;
        if (!_hostResizedDuringDrag && layout.width > 0f && !Mathf.Approximately(DetailsWidth, _dragStartWidth))
        {
            _ratio = Mathf.Clamp01(DetailsWidth / layout.width);
            _saveRatio(_ratio);
        }
        QueueLayoutUpdate();
    }

    private void OnSizeChanged(GeometryChangedEvent evt)
    {
        if (Mathf.Approximately(evt.newRect.width, evt.oldRect.width))
            return;
        // Resizing the host is not a new user preference, even when the native splitter clamps a pane.
        if (_dragPointer >= 0) _hostResizedDuringDrag = true;
        QueueLayoutUpdate();
    }

    private void QueueLayoutUpdate()
    {
        _layoutUpdate?.Pause();
        // Run after the native split view has processed its own geometry callbacks.
        _layoutUpdate = schedule.Execute(ApplyRatio);
    }

    private void ApplyRatio()
    {
        if (_dragPointer >= 0 || fixedPane == null || layout.width <= 0f)
            return;
        fixedPane.style.minWidth = MinDetailsWidth;
        flexedPane.style.minWidth = MinPreviewWidth;
        float width = Mathf.Clamp(layout.width * _ratio, MinDetailsWidth,
            Mathf.Max(MinDetailsWidth, layout.width - MinPreviewWidth));
        fixedPaneInitialDimension = width;
        fixedPane.style.width = width;
        _dragHandle.style.left = width;
    }
}
#endif
