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
    }

    [SerializeField] private float _savedScrollY;
    [SerializeField] private ActionPreviewPanel _previewPanel = new ActionPreviewPanel();
    private float _splitRatio;
    private ActionDetailsSplitView _splitView;
    private VisualElement _detailsPane;
    private ActionEditorDocument _document;
    private SerializedObject _serializedAction;
    private Label _selectionSummary;
    private ScrollView _scroll;
    private VisualElement _content;
    private Label _pageStatus;
    private Label _draftDerived;
    private Label _draftEnd;
    private Label _draftStatus;
    private TimingDraft _draft;
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

    internal static void OpenShared() => GetWindow<ActionDetailsWindow>("Action Details").Show();

    private static void SelectCurrentActionAsset()
    {
        ActionAsset action = ActionEditorContext.Shared.CurrentAction;
        if (action == null)
            return;
        Selection.activeObject = action;
        EditorGUIUtility.PingObject(action);
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Action Details");
        minSize = new Vector2(760f, 360f);
        _splitRatio = EditorPrefs.GetFloat(SplitRatioPreferenceKey, 0.4f);
        if (float.IsNaN(_splitRatio) || float.IsInfinity(_splitRatio) || _splitRatio <= 0f || _splitRatio >= 1f)
            _splitRatio = 0.4f;
        if (_previewPanel == null) _previewPanel = new ActionPreviewPanel();
        ActionEditorContext.Changed += OnContextChanged;
        ActionEditorInteractionGate.Changed += OnInteractionGateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
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
        SaveScroll();
        _splitView?.FinishResize();
        rootVisualElement.Clear();
        ActionEditorTheme.Apply(rootVisualElement, "action-editor-details-window");
        rootVisualElement.Add(ActionEditorChrome.ContextBar(
            ActionEditorContext.Shared.CurrentAction,
            null,
            ("Select Asset", SelectCurrentActionAsset),
            ("Timeline", ActionTimelineWindow.OpenShared)));

        _splitView = new ActionDetailsSplitView(_splitRatio, SaveSplitRatio);
        _detailsPane = new VisualElement();
        _detailsPane.AddToClassList("action-editor-details-pane");
        _detailsPane.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        _selectionSummary = new Label();
        _selectionSummary.AddToClassList("action-editor-selection-summary");
        _detailsPane.Add(_selectionSummary);

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
        bool sameTarget = _draft != null && _draft.Targets(context.CurrentAction, context.PrimarySelection);
        bool keepDraft = preserveDraft && _draft != null && _draft.CanRetain(context.CurrentAction, context.PrimarySelection);
        bool draftExpired = preserveDraft && sameTarget && !keepDraft &&
                            EvaluateDraft()?.State != ActionTimelineOperationState.NoChange;
        if (!keepDraft) DiscardDraft();
        _bindingSession++;
        _document = ActionEditorContext.Shared.Document;
        _serializedAction = _document.Asset != null ? new SerializedObject(_document.Asset) : null;
        ActionEditorChrome.SyncActionField(rootVisualElement);
        RefreshPage(restoreScroll);
        if (draftExpired)
            SetPageStatus("Unapplied timing changes were discarded because the source data changed.", true);
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
        _pageStatus = null;
        _draftDerived = null;
        _draftEnd = null;
        _draftStatus = null;
        _configurationHost = null;
        _configurationItem = null;
        _configurationEntry = null;
        ActionEditorContext context = ActionEditorContext.Shared;
        _displayedPrimary = context.PrimarySelection;
        _selectionSummary.text = SelectionSummary(context.PrimarySelection, context.SelectedIds.Count);

        if (_document?.Asset == null)
        {
            _content.Add(ActionEditorChrome.EmptyState("Choose an ActionAsset",
                "Choose an ActionRuntime-backed ActionAsset in Action Timeline to edit it."));
            _buildingPage = false;
            return;
        }

        bool identityBlocked = _document.Readiness == ActionEditorReadiness.IdentityBlocked;
        if (identityBlocked)
            AddIdentityBlockedCard();

        ActionSelectionValue selection = context.PrimarySelection;
        if (selection.Kind == ActionSelectionKind.None || selection.Kind == ActionSelectionKind.Action)
            _content.Add(ActionEditorChrome.EmptyState("Select a Timeline item",
                "Select an animation segment, Gameplay lane, or Gameplay item to edit it here."));
        else if (!_document.ById.TryGetValue(selection.EditorId, out ActionDocumentEntry entry))
            _content.Add(ActionEditorChrome.EmptyState("Selection is unavailable",
                "This object no longer has a unique valid EditorId. Locate it by AuthoringPath and repair identity first."));
        else
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
        section.Add(new HelpBox(
            "Some Timeline items do not have a safe editor identity. Fields remain read-only until Editor IDs are repaired.",
            HelpBoxMessageType.Error));
        var repair = new Button(RepairIds) { text = "Repair Editor IDs" };
        repair.SetEnabled(!ActionEditorInteractionGate.IsActive);
        section.Add(repair);
        _content.Add(section);
    }

    private void RepairIds()
    {
        if (ActionEditorInteractionGate.IsActive)
        {
            SetPageStatus("Finish the active Timeline gesture before repairing IDs.", true);
            return;
        }
        if (ActionEditorCommands.RepairEditorIds(_document.Asset, out int repaired, out string message))
            SetPageStatus($"Repaired {repaired} Editor ID(s).", false);
        else
            SetPageStatus(message, true);
    }

    private void DrawEntry(ActionDocumentEntry entry, bool editable)
    {
        AddHeading(entry.DisplayName, $"{entry.SelectionKind} · {entry.AuthoringPath}");
        SerializedProperty entryProperty = ResolveEntryProperty(entry);
        switch (entry.Source)
        {
            case AnimationSegment segment:
                DrawAnimation(segment, entry, editable);
                break;
            case GameplayLane lane:
                DrawLane(lane, entryProperty, editable);
                break;
            case GameplayItem item:
                DrawItem(item, entry, entryProperty, editable);
                break;
        }
    }

    private void DrawAnimation(AnimationSegment segment, ActionDocumentEntry entry, bool editable)
    {
        _draft = CreateDraft(entry, ActionTimelineOperationKind.SetAnimationTiming);
        VisualElement section = Section("Timing & Animation Source");
        IntegerField start = DraftInt(section, "Start Frame", _draft.StartFrame, value => _draft.StartFrame = value, editable);
        ObjectField asset = DraftObject(section, "Animation Asset", _draft.AnimationAsset, typeof(AnimationAsset),
            value => _draft.AnimationAsset = value as AnimationAsset, editable);
        FloatField sourceStart = DraftFloat(section, "Source Start", _draft.SourceStartTime, value => _draft.SourceStartTime = value, editable);
        FloatField sourceEnd = DraftFloat(section, "Source End", _draft.SourceEndTime, value => _draft.SourceEndTime = value, editable);
        FloatField playRate = DraftFloat(section, "Play Rate", _draft.PlayRate, value => _draft.PlayRate = value, editable);
        AddRow(section, "Clip", ObjectName(_draft.AnimationAsset != null ? _draft.AnimationAsset.Clip : null));
        Label derived = AddRow(section, "Candidate Duration", "—");
        Label end = AddRow(section, "Candidate End Exclusive", "—");
        var fullClip = new Button(() =>
        {
            AnimationClip clip = _draft.AnimationAsset != null ? _draft.AnimationAsset.Clip : null;
            if (clip == null)
                return;
            _draft.SourceStartTime = 0f;
            _draft.SourceEndTime = clip.length;
            sourceStart.SetValueWithoutNotify(0f);
            sourceEnd.SetValueWithoutNotify(clip.length);
            RefreshDraftFeedback();
        }) { text = "Use Full Clip" };
        fullClip.SetEnabled(editable);
        section.Add(fullClip);
        AddDraftFooter(section, derived, end, editable);
        RegisterDraftKeys(section);
        _content.Add(section);
    }

    private void DrawLane(GameplayLane lane, SerializedProperty entryProperty, bool editable)
    {
        VisualElement section = Section("Gameplay Lane");
        AddBoundProperty(section, entryProperty?.FindPropertyRelative("_name"), "Name", editable);
        AddBoundProperty(section, entryProperty?.FindPropertyRelative("_muted"), "Muted", editable);
        AddRow(section, "Item Count", lane.Items?.Count.ToString() ?? "0");
        AddParagraph(section, "Lanes are untyped organization rows. Gameplay overlap is legal and carries no priority.");
        _content.Add(section);
    }

    private void DrawItem(GameplayItem item, ActionDocumentEntry entry, SerializedProperty itemProperty, bool editable)
    {
        VisualElement common = Section("Gameplay Item");
        AddBoundProperty(common, itemProperty?.FindPropertyRelative("_muted"), "Muted", editable);
        AddRow(common, "Lane", string.IsNullOrWhiteSpace(entry.LaneName) ? $"Lane {entry.LaneIndex}" : entry.LaneName);
        _content.Add(common);

        if (item is PointGameplayItem point)
        {
            _draft = CreateDraft(entry, ActionTimelineOperationKind.SetPointTiming);
            VisualElement timing = Section("Timing");
            DraftInt(timing, "Frame", _draft.StartFrame, value => _draft.StartFrame = value, editable);
            AddDraftFooter(timing, null, null, editable);
            RegisterDraftKeys(timing);
            _content.Add(timing);
        }
        else if (item is RangeGameplayItem range)
        {
            _draft = CreateDraft(entry, ActionTimelineOperationKind.SetRangeTiming);
            VisualElement timing = Section("Timing");
            DraftInt(timing, "Start Frame", _draft.StartFrame, value => _draft.StartFrame = value, editable);
            DraftInt(timing, "Duration Frames", _draft.DurationFrames, value => _draft.DurationFrames = value, editable);
            Label end = AddRow(timing, "Candidate End Exclusive", "—");
            AddDraftFooter(timing, null, end, editable);
            RegisterDraftKeys(timing);
            _content.Add(timing);
        }

        _configurationHost = new VisualElement();
        _configurationItem = item;
        _configurationEntry = entry;
        _configurationEditable = editable;
        _content.Add(_configurationHost);
        RebuildConfigurationSection();
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
            AddMissingConfig(config, _configurationItem, _configurationEditable, "The concrete Config is missing.");
        else
            DrawTypedConfig(config, _configurationItem, configProperty, _configurationEditable);
        _configurationHost.Add(config);
    }

    private void DrawTypedConfig(VisualElement parent, GameplayItem item, SerializedProperty config, bool editable)
    {
        if (config == null)
        {
            AddParagraph(parent, "The serialized Config path could not be resolved.", "action-editor-local-issue");
            return;
        }
        switch (item)
        {
            case ImpulseItem impulse:
                DrawImpulse(parent, impulse, config, editable);
                break;
            case HitBoxItem hitBox:
                DrawHitBox(parent, hitBox, config, editable);
                break;
            case RootMotionItem:
                AddBoundProperty(parent, config.FindPropertyRelative("animationAsset"), "Animation Asset", editable);
                AddBoundProperty(parent, config.FindPropertyRelative("sourceStartTime"), "Source Start", editable);
                AddBoundProperty(parent, config.FindPropertyRelative("playRate"), "Play Rate", editable);
                break;
            case SelfRotationItem:
                DrawSelfRotation(parent, config, editable);
                break;
            case VelocityOverrideItem velocity:
                DrawVelocity(parent, velocity, config, editable);
                break;
            case MotionPolicyItem:
                DrawMotionPolicy(parent, config, editable);
                break;
            case TagItem:
                AddImGuiProperty(parent, config.FindPropertyRelative("tag"), "Tag", editable);
                AddBoundProperty(parent, config.FindPropertyRelative("targetContainer"), "Target Container", editable);
                break;
            default:
                AddParagraph(parent, "Unsupported GameplayItem type.", "action-editor-local-issue");
                break;
        }
    }

    private void DrawImpulse(VisualElement parent, ImpulseItem item, SerializedProperty config, bool editable)
    {
        if (item.Config.impulse == null)
        {
            AddMissingConfig(parent, item, editable, "Impulse settings are missing.");
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
            AddParagraph(parent, "Legacy gravity override is invalid in ActionRuntime.", "action-editor-local-issue");
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
            AddMissingConfig(parent, item, editable, "One or more required HitBox settings are missing.");
            return;
        }
        AddSubheading(parent, "Binding");
        AddBoundProperty(parent, config.FindPropertyRelative("anchor"), "Anchor", editable, true);
        DrawHitBoxAnchorDiagnostic(parent, item.Config.anchor);
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
            AddParagraph(parent, "Total Height includes both hemispheres. Values below the diameter resolve to a sphere.");
        }
        AddParagraph(parent,
            "Center is local to the binding and follows its scale. Shape dimensions are world units and do not scale with the character.");
        AddSubheading(parent, "Attack Data");
        AddImGuiProperty(parent, config.FindPropertyRelative("dataConfig"), "Attack Data", editable);
        AddSubheading(parent, "Effects");
        AddEffectsList(parent, config.FindPropertyRelative("effects"), editable);
    }

    private void AddEffectsList(VisualElement parent, SerializedProperty property, bool editable)
    {
        if (property == null)
        {
            AddParagraph(parent, "Serialized field 'Effects' is unavailable.", "action-editor-local-issue");
            return;
        }

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

    private void DrawHitBoxAnchorDiagnostic(VisualElement parent, ActionHitBoxAnchor anchor)
    {
        if (anchor == ActionHitBoxAnchor.ActorRoot)
            return;

        GameObject source = ActionPreviewCharacterResolver.Resolve(
            _document?.Asset, ActionEditorContext.Shared.PreviewCharacter);
        Animator[] animators = source != null ? source.GetComponentsInChildren<Animator>(true) : Array.Empty<Animator>();
        if (animators.Length != 1)
        {
            AddParagraph(parent, source == null
                ? $"Anchor '{anchor}' cannot be verified because this Action has no Preview Character."
                : $"Anchor '{anchor}' requires exactly one Preview Animator; found {animators.Length}.",
                "action-editor-local-issue");
            return;
        }

        if (!ActionHitBoxAnchorResolver.TryResolve(
                anchor, source.transform, animators[0], out _, out string failureReason))
            AddParagraph(parent, failureReason, "action-editor-local-issue");
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
            AddMissingConfig(parent, item, editable, "Velocity settings are missing.");
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

    private void DrawMotionPolicy(VisualElement parent, SerializedProperty config, bool editable)
    {
        DrawPolicy(parent, config, "useLocomotionScale", "locomotionScale", "Locomotion", editable);
        DrawPolicy(parent, config, "useAirLocomotionScale", "airLocomotionScale", "Air Locomotion", editable);
        DrawPolicy(parent, config, "useGravityScale", "gravityScale", "Gravity", editable);
    }

    private void DrawPolicy(VisualElement parent, SerializedProperty config, string toggleName, string scaleName,
        string label, bool editable)
    {
        SerializedProperty toggle = config.FindPropertyRelative(toggleName);
        AddBoundProperty(parent, toggle, label, editable, true);
        if (toggle != null && toggle.boolValue)
            AddBoundProperty(parent, config.FindPropertyRelative(scaleName), $"{label} Scale", editable);
    }

    private TimingDraft CreateDraft(ActionDocumentEntry entry, ActionTimelineOperationKind kind)
    {
        if (_draft != null && _draft.Kind == entry.SelectionKind && _draft.EditorId == entry.EditorId &&
            ReferenceEquals(_draft.Source, entry.Source))
            return _draft;
        var draft = new TimingDraft
        {
            Kind = entry.SelectionKind,
            EditorId = entry.EditorId,
            Source = entry.Source,
            Snapshot = ActionTimelineOperationSnapshot.Capture(_document, kind, new[] { entry.EditorId }),
            StartFrame = entry.StartFrame,
            DurationFrames = entry.Source is RangeGameplayItem range ? range.DurationFrames : 1,
        };
        if (entry.Source is AnimationSegment animation)
        {
            draft.AnimationAsset = animation.AnimationAsset;
            draft.SourceStartTime = animation.SourceStartTime;
            draft.SourceEndTime = animation.SourceEndTime;
            draft.PlayRate = animation.PlayRate;
            draft.DurationFrames = animation.DerivedDurationFrames;
        }
        return draft;
    }

    private void AddDraftFooter(VisualElement parent, Label derived, Label end, bool editable)
    {
        Label status = new Label("No unapplied timing changes.");
        status.AddToClassList("action-editor-draft-status");
        parent.Add(status);
        VisualElement buttons = MiniButtons();
        Button apply = new Button(() => ApplyDraft(status)) { text = "Apply" };
        Button revert = new Button(() => RevertDraft()) { text = "Revert" };
        apply.SetEnabled(editable && !ActionEditorInteractionGate.IsActive);
        revert.SetEnabled(editable);
        buttons.Add(apply);
        buttons.Add(revert);
        parent.Add(buttons);
        _draftDerived = derived;
        _draftEnd = end;
        _draftStatus = status;
        RefreshDraftFeedback();
    }

    private void RefreshDraftFeedback()
    {
        ActionTimelineOperationResult result = EvaluateDraft();
        ActionTimelineOperationCandidate candidate = result?.Candidates.Count > 0 ? result.Candidates[0] : null;
        int duration = candidate != null ? candidate.DurationFrames : _draft?.DurationFrames ?? 0;
        if (_draftDerived != null)
            _draftDerived.text = result != null && result.State != ActionTimelineOperationState.Rejected && duration > 0
                ? $"{duration} frames"
                : "Invalid";
        if (_draftEnd != null && _draft != null)
            _draftEnd.text = result != null && result.State != ActionTimelineOperationState.Rejected && duration > 0
                ? ((long)_draft.StartFrame + duration).ToString()
                : "Invalid";
        if (_draftStatus != null)
        {
            _draftStatus.style.display = DisplayStyle.Flex;
            _draftStatus.text = result == null ? "Draft cannot be evaluated." :
                result.State == ActionTimelineOperationState.Allowed ? "Unapplied timing changes." :
                result.State == ActionTimelineOperationState.NoChange ? "No unapplied timing changes." : result.Message;
            _draftStatus.EnableInClassList("action-editor-draft-error", result == null || result.State == ActionTimelineOperationState.Rejected);
        }
    }

    private ActionTimelineOperationResult EvaluateDraft()
    {
        if (_draft?.Snapshot == null)
            return null;
        return _draft.Snapshot.Evaluate(CreateDraftInput());
    }

    private ActionTimelineOperationInput CreateDraftInput() =>
        _draft.Kind == ActionSelectionKind.AnimationSegment
            ? ActionTimelineOperationInput.Absolute(_draft.StartFrame, _draft.DurationFrames, _draft.AnimationAsset,
                _draft.SourceStartTime, _draft.SourceEndTime, _draft.PlayRate)
            : ActionTimelineOperationInput.Absolute(_draft.StartFrame, _draft.DurationFrames);

    private void ApplyDraft(Label status)
    {
        if (ActionEditorInteractionGate.IsActive)
        {
            status.text = "Finish the active Timeline gesture before applying Details.";
            status.AddToClassList("action-editor-draft-error");
            return;
        }
        ActionTimelineOperationResult result = EvaluateDraft();
        if (result == null || result.State == ActionTimelineOperationState.Rejected)
        {
            status.text = result?.Message ?? "Draft cannot be evaluated.";
            status.AddToClassList("action-editor-draft-error");
            return;
        }
        if (result.State == ActionTimelineOperationState.NoChange)
        {
            status.text = "No timing change.";
            return;
        }
        ActionTimelineOperationSnapshot snapshot = _draft.Snapshot;
        ActionTimelineOperationInput input = CreateDraftInput();
        DiscardDraft(); // The following notification represents this Apply, not an external invalidation.
        if (!ActionEditorCommands.CommitTimelineOperation(snapshot, input, out string message))
        {
            string failure = string.IsNullOrEmpty(message) ? "Timing could not be applied." : message;
            RefreshDocumentAndPage(true);
            SetPageStatus(failure, true);
        }
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
                Label status = section.Q<Label>(className: "action-editor-draft-status");
                ApplyDraft(status);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                RevertDraft();
                evt.StopPropagation();
            }
        }, TrickleDown.TrickleDown);
    }

    private void AddMissingConfig(VisualElement parent, GameplayItem item, bool editable, string text)
    {
        AddParagraph(parent, text, "action-editor-local-issue");
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
            ImpulseItem value => value.Config,
            HitBoxItem value => value.Config,
            RootMotionItem value => value.Config,
            SelfRotationItem value => value.Config,
            VelocityOverrideItem value => value.Config,
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
        {
            AddParagraph(parent, $"Serialized field '{label}' is unavailable.", "action-editor-local-issue");
            return null;
        }
        var field = string.IsNullOrEmpty(label) ? new PropertyField(property) : new PropertyField(property, label);
        field.AddToClassList("action-editor-edit-field");
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
        {
            AddParagraph(parent, $"Serialized field '{label}' is unavailable.", "action-editor-local-issue");
            return null;
        }
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
        {
            EditorGUILayout.HelpBox($"Serialized field '{label}' is unavailable.", MessageType.Warning);
            return;
        }
        EditorGUI.BeginDisabledGroup(!editable || ActionEditorInteractionGate.IsActive);
        EditorGUI.BeginChangeCheck();
        if (drawProperty == null)
            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        else
            drawProperty(property);
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUI.EndDisabledGroup();
        if (changed)
            _serializedAction.ApplyModifiedProperties();
        if (changed)
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

    private void AddHeading(string title, string subtitle)
    {
        var titleLabel = new Label(title);
        titleLabel.AddToClassList("action-editor-details-heading");
        _content.Add(titleLabel);
        var subtitleLabel = new Label(subtitle);
        subtitleLabel.AddToClassList("action-editor-details-subtitle");
        _content.Add(subtitleLabel);
    }

    private static VisualElement Section(string title)
    {
        var section = new VisualElement();
        section.AddToClassList("action-editor-section");
        AddSubheading(section, title);
        return section;
    }

    private static void AddSubheading(VisualElement parent, string title)
    {
        var heading = new Label(title);
        heading.AddToClassList("action-editor-section-title");
        parent.Add(heading);
    }

    private static string SelectionSummary(ActionSelectionValue selection, int selectedCount)
    {
        return $"Primary Selection  ·  {PrimaryName(selection)}  ·  {selectedCount} content selected";
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

    private static void AddParagraph(VisualElement parent, string text, string className = "action-editor-details-subtitle")
    {
        var label = new Label(text);
        label.AddToClassList(className);
        parent.Add(label);
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

    private void SetPageStatus(string message, bool error)
    {
        if (_pageStatus == null)
        {
            _pageStatus = new Label();
            _pageStatus.AddToClassList("action-editor-page-status");
            _content.Insert(0, _pageStatus);
        }
        _pageStatus.text = message ?? string.Empty;
        _pageStatus.EnableInClassList("action-editor-draft-error", error);
    }

    private void OnContextChanged(ActionEditorChange change)
    {
        _previewPanel?.OnContextChanged(change);
        ActionEditorChangeFlags flags = change.Flags;
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
                RefreshDocumentOnly();
            else
            {
                ClearPendingBindingSectionRefresh();
                RefreshDocumentAndPage(true, true);
            }
        }
        else if ((flags & ActionEditorChangeFlags.Presentation) != 0)
        {
            RefreshDocumentOnly();
            if (_selectionSummary != null)
            {
                ActionSelectionValue primary = ActionEditorContext.Shared.PrimarySelection;
                _selectionSummary.text = SelectionSummary(primary, ActionEditorContext.Shared.SelectedIds.Count);
            }
        }
        if ((flags & ActionEditorChangeFlags.Selection) != 0)
        {
            ActionSelectionValue primary = ActionEditorContext.Shared.PrimarySelection;
            if (!SameSelection(primary, _displayedPrimary))
                RefreshDocumentAndPage(true);
            else if (_selectionSummary != null)
                _selectionSummary.text = SelectionSummary(primary, ActionEditorContext.Shared.SelectedIds.Count);
        }
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

    private void OnPlayModeStateChanged(PlayModeStateChange state) => _previewPanel?.OnPlayModeStateChanged(state);

    private void SaveScroll()
    {
        if (_scroll != null)
            _savedScrollY = _scroll.scrollOffset.y;
    }

    private void DiscardDraft() => _draft = null;

    private static string PrimaryName(ActionSelectionValue selection) =>
        selection.Kind == ActionSelectionKind.None ? "Action" : selection.Kind.ToString();
    private static bool SameSelection(ActionSelectionValue left, ActionSelectionValue right) =>
        left.Kind == right.Kind && string.Equals(left.EditorId, right.EditorId, StringComparison.Ordinal);
    private static string ObjectName(UnityEngine.Object value) => value != null ? value.name : "None";
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
