#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Primary-selection property editor for Action V1 authoring.</summary>
public sealed class ActionV1DetailsWindow : EditorWindow
{
    private sealed class PropertyWatch
    {
        internal string Path;
        internal int Fingerprint;
        internal int Session;
        internal bool RebuildConfiguration;
        internal bool RebuildTrigger;
        internal bool RebuildCancellation;
        internal bool RefreshTimelinePresentation;
    }

    private sealed class TimingDraft
    {
        internal ActionV1SelectionKind Kind;
        internal string EditorId;
        internal object Source;
        internal ActionV1TimelineOperationSnapshot Snapshot;
        internal int StartFrame;
        internal int DurationFrames;
        internal AnimationAsset AnimationAsset;
        internal float SourceStartTime;
        internal float SourceEndTime;
        internal float PlayRate;
    }

    [SerializeField] private float _savedScrollY;
    [SerializeField] private bool _advancedOpen;
    [SerializeField] private bool _cancelRulesOpen = true;
    [SerializeField] private bool _selfTagsOpen = true;
    [SerializeField] private bool _conditionsOpen = true;

    private ActionV1EditorDocument _document;
    private SerializedObject _serializedAction;
    private Label _selectionSummary;
    private ScrollView _scroll;
    private VisualElement _content;
    private Label _frameValue;
    private Label _pageStatus;
    private Label _draftDerived;
    private Label _draftEnd;
    private Label _draftStatus;
    private VisualElement _selectionIssuesHost;
    private Label _validationReadiness;
    private Label _validationIssueCount;
    private Label _validationErrorCount;
    private Label _validationSetupCount;
    private TimingDraft _draft;
    private bool _buildingPage;
    private bool _localNotification;
    private bool _restoreScroll;
    private int _bindingSession;
    private int _lastObservedDirtyCount = -1;
    private VisualElement _configurationHost;
    private GameplayItem _configurationItem;
    private ActionV1DocumentEntry _configurationEntry;
    private bool _configurationEditable;
    private VisualElement _triggerHost;
    private bool _actionEditable;
    private VisualElement _cancellationHost;
    private ActionV1SelectionValue _displayedPrimary;

    [MenuItem("Tools/Combat/Action V1/Action Details")]
    public static void OpenFromMenu()
    {
        if (Selection.activeObject is ActionAsset action)
            ActionV1EditorContext.Shared.SetAction(action);
        OpenShared();
    }

    internal static void OpenShared() => GetWindow<ActionV1DetailsWindow>("Action Details").Show();

    private void OnEnable()
    {
        ActionV1EditorContext.Changed += OnContextChanged;
        Undo.undoRedoPerformed += OnExternalDataChanged;
        EditorApplication.projectChanged += OnExternalDataChanged;
    }

    private void OnDisable()
    {
        SaveScroll();
        DiscardDraft();
        ActionV1EditorContext.Changed -= OnContextChanged;
        Undo.undoRedoPerformed -= OnExternalDataChanged;
        EditorApplication.projectChanged -= OnExternalDataChanged;
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        ActionV1EditorTheme.Apply(rootVisualElement, "action-v1-details-window");
        rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        rootVisualElement.Add(ActionV1EditorChrome.ContextBar(
            ActionV1EditorContext.Shared.CurrentAction,
            action => ActionV1EditorContext.Shared.SetAction(action),
            ("Timeline", ActionV1TimelineWindow.OpenShared),
            ("Preview", ActionV1PreviewWindow.OpenShared)));

        _selectionSummary = new Label();
        _selectionSummary.AddToClassList("action-v1-selection-summary");
        rootVisualElement.Add(_selectionSummary);

        _scroll = new ScrollView(ScrollViewMode.Vertical);
        _scroll.AddToClassList("action-v1-details-scroll");
        _scroll.verticalScroller.valueChanged += value => _savedScrollY = value;
        _content = new VisualElement();
        _content.AddToClassList("action-v1-details-center");
        _scroll.Add(_content);
        rootVisualElement.Add(_scroll);
        RefreshDocumentAndPage(true);
    }

    private void RefreshDocumentAndPage(bool restoreScroll)
    {
        if (_content == null)
            return;
        if (restoreScroll)
            SaveScroll();
        DiscardDraft();
        _bindingSession++;
        _document = ActionV1EditorDocument.Build(ActionV1EditorContext.Shared.CurrentAction);
        _serializedAction = _document.Asset != null ? new SerializedObject(_document.Asset) : null;
        _lastObservedDirtyCount = _document.Asset != null ? EditorUtility.GetDirtyCount(_document.Asset) : -1;
        ActionV1EditorChrome.SyncActionField(rootVisualElement);
        ActionV1EditorContext.Shared.ValidateSelection(_document);
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
        _frameValue = null;
        _pageStatus = null;
        _draftDerived = null;
        _draftEnd = null;
        _draftStatus = null;
        _selectionIssuesHost = null;
        _validationReadiness = null;
        _validationIssueCount = null;
        _validationErrorCount = null;
        _validationSetupCount = null;
        _configurationHost = null;
        _configurationItem = null;
        _configurationEntry = null;
        _triggerHost = null;
        _cancellationHost = null;
        ActionV1EditorContext context = ActionV1EditorContext.Shared;
        _displayedPrimary = context.PrimarySelection;
        int selectedCount = context.SelectedIds.Count;
        _selectionSummary.text = $"Primary Selection  ·  {PrimaryName(context.PrimarySelection)}  ·  {selectedCount} content selected";

        if (_document?.Asset == null)
        {
            _content.Add(ActionV1EditorChrome.EmptyState("Choose an ActionAsset",
                "Select an ActionAsset here or in Action Timeline to edit its V1 authoring data."));
            _buildingPage = false;
            return;
        }

        bool identityBlocked = _document.Readiness == ActionV1EditorReadiness.IdentityBlocked;
        if (identityBlocked)
            AddIdentityBlockedCard();

        ActionV1SelectionValue selection = context.PrimarySelection;
        if (selection.Kind == ActionV1SelectionKind.None || selection.Kind == ActionV1SelectionKind.Action)
            DrawAction(_document.Asset, !identityBlocked);
        else if (!_document.ById.TryGetValue(selection.EditorId, out ActionV1DocumentEntry entry))
            _content.Add(ActionV1EditorChrome.EmptyState("Selection is unavailable",
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
        VisualElement section = Section("Identity blocked");
        ActionV1ValidationIndex index = _document.ValidationIndex;
        AddParagraph(section,
            $"{index.BlockingIdentityCount} identity issue(s): {index.MissingIdentityCount} missing, " +
            $"{index.MalformedIdentityCount} malformed, {index.DuplicateIdentityCount} duplicate. " +
            "Fields remain read-only until IDs are explicitly repaired.", "action-v1-local-issue");
        var repair = new Button(RepairIds) { text = "Repair Missing / Invalid / Duplicate Editor IDs" };
        repair.SetEnabled(!ActionV1EditorInteractionGate.IsActive);
        section.Add(repair);
        _content.Add(section);
    }

    private void RepairIds()
    {
        if (ActionV1EditorInteractionGate.IsActive)
        {
            SetPageStatus("Finish the active Timeline gesture before repairing IDs.", true);
            return;
        }
        if (ActionV1EditorCommands.RepairEditorIds(_document.Asset, out int repaired, out string message))
            SetPageStatus($"Repaired {repaired} Editor ID(s).", false);
        else
            SetPageStatus(message, true);
    }

    private void DrawAction(ActionAsset asset, bool editable)
    {
        _actionEditable = editable;
        AddHeading(asset.name, "Action V1 overview · direct property editing");
        VisualElement summary = Section("Timeline Summary");
        AddRow(summary, "Frame Rate", $"{ActionTimelineData.FrameRate} FPS");
        AddRow(summary, "Duration", $"{_document.DurationFrames} frames");
        _frameValue = AddRow(summary, "Current Frame", ActionV1EditorContext.Shared.CurrentFrame.ToString());
        AddRow(summary, "Animation Segments", _document.AnimationSegments.Count.ToString());
        AddRow(summary, "Gameplay Lanes", _document.Lanes.Count.ToString());
        AddRow(summary, "Gameplay Items", _document.GameplayItems.Count.ToString());
        _content.Add(summary);

        VisualElement priority = Section("Priority & Reentry");
        AddBoundProperty(priority, "_priorityLayer", "Priority Layer", editable);
        AddBoundProperty(priority, "_priorityValue", "Priority Value", editable);
        AddBoundProperty(priority, "_allowReenterWhilePlaying", "Allow Reentry While Playing", editable);
        _content.Add(priority);

        _triggerHost = new VisualElement();
        _content.Add(_triggerHost);
        RebuildTriggerSection();

        Foldout cancel = FoldoutSection("Cancellation", _cancelRulesOpen, value => _cancelRulesOpen = value);
        _cancellationHost = new VisualElement();
        cancel.Add(_cancellationHost);
        DrawCancelRules(_cancellationHost, editable);
        _content.Add(cancel);

        Foldout tags = FoldoutSection("Self Tags", _selfTagsOpen, value => _selfTagsOpen = value);
        AddImGuiProperty(tags, "_selfTags", "Self Tags", editable);
        _content.Add(tags);

        Foldout conditions = FoldoutSection("Entry Conditions", _conditionsOpen, value => _conditionsOpen = value);
        AddImGuiProperty(conditions, "_entryConditions", "Entry Conditions", editable);
        _content.Add(conditions);
        DrawValidationSummary();
    }

    private void RebuildTriggerSection()
    {
        if (_triggerHost == null || _serializedAction == null)
            return;
        _triggerHost.Clear();
        VisualElement trigger = Section("Trigger & Start Context");
        AddBoundProperty(trigger, "_triggerMode", "Trigger Mode", _actionEditable, rebuildTrigger: true);
        SerializedProperty triggerMode = _serializedAction.FindProperty("_triggerMode");
        if (triggerMode != null && triggerMode.enumValueIndex == (int)ActionTriggerMode.Event)
            AddImGuiProperty(trigger, "_eventTriggerTag", "Event Trigger Tag", _actionEditable);
        AddBoundProperty(trigger, "_startContextMode", "Start Context", _actionEditable);
        _triggerHost.Add(trigger);
    }

    private void RebuildCancellationSection()
    {
        if (_cancellationHost == null)
            return;
        _cancellationHost.Clear();
        DrawCancelRules(_cancellationHost, _actionEditable);
    }

    private void DrawCancelRules(VisualElement parent, bool editable)
    {
        SerializedProperty rules = _serializedAction?.FindProperty("_cancelRules");
        if (rules == null)
            return;
        for (int index = 0; index < rules.arraySize; index++)
        {
            int ruleIndex = index;
            SerializedProperty rule = rules.GetArrayElementAtIndex(index);
            var row = new Foldout { text = $"Rule {index + 1}", value = true };
            row.AddToClassList("action-v1-nested-card");
            SerializedProperty kind = rule.FindPropertyRelative("targetKind");
            AddBoundProperty(row, kind, "Target", editable, rebuildCancellation: true);
            CancelTargetKind value = kind != null && kind.enumValueIndex >= 0
                ? (CancelTargetKind)kind.enumValueIndex
                : CancelTargetKind.SpecificAction;
            if (value == CancelTargetKind.SpecificAction)
                AddBoundProperty(row, rule.FindPropertyRelative("specificTarget"), "Action", editable);
            else if (value == CancelTargetKind.AnyWithTag)
                AddImGuiProperty(row, rule.FindPropertyRelative("targetTag"), "Tag", editable);
            AddImGuiProperty(row, rule.FindPropertyRelative("window"), "Window", editable);
            VisualElement buttons = MiniButtons();
            AddMiniButton(buttons, "Move Up", () => MoveCancelRule(ruleIndex, ruleIndex - 1), editable && ruleIndex > 0);
            AddMiniButton(buttons, "Move Down", () => MoveCancelRule(ruleIndex, ruleIndex + 1), editable && ruleIndex + 1 < rules.arraySize);
            AddMiniButton(buttons, "Remove", () => RemoveCancelRule(ruleIndex), editable);
            row.Add(buttons);
            parent.Add(row);
        }
        var add = new Button(AddCancelRule) { text = "Add Cancel Rule" };
        add.SetEnabled(editable && !ActionV1EditorInteractionGate.IsActive);
        parent.Add(add);
    }

    private void DrawEntry(ActionV1DocumentEntry entry, bool editable)
    {
        AddHeading(entry.DisplayName, $"{entry.SelectionKind} · {entry.AuthoringPath}");
        _selectionIssuesHost = new VisualElement();
        _content.Add(_selectionIssuesHost);
        RefreshSelectionIssues(entry);

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
        DrawAdvanced(entry);
    }

    private void DrawAnimation(AnimationSegment segment, ActionV1DocumentEntry entry, bool editable)
    {
        _draft = CreateDraft(entry, ActionV1TimelineOperationKind.SetAnimationTiming);
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

    private void DrawItem(GameplayItem item, ActionV1DocumentEntry entry, SerializedProperty itemProperty, bool editable)
    {
        VisualElement common = Section("Gameplay Item");
        AddBoundProperty(common, itemProperty?.FindPropertyRelative("_muted"), "Muted", editable);
        AddRow(common, "Lane", string.IsNullOrWhiteSpace(entry.LaneName) ? $"Lane {entry.LaneIndex}" : entry.LaneName);
        _content.Add(common);

        if (item is PointGameplayItem point)
        {
            _draft = CreateDraft(entry, ActionV1TimelineOperationKind.SetPointTiming);
            VisualElement timing = Section("Timing");
            DraftInt(timing, "Frame", _draft.StartFrame, value => _draft.StartFrame = value, editable);
            AddDraftFooter(timing, null, null, editable);
            RegisterDraftKeys(timing);
            _content.Add(timing);
        }
        else if (item is RangeGameplayItem range)
        {
            _draft = CreateDraft(entry, ActionV1TimelineOperationKind.SetRangeTiming);
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
            AddParagraph(parent, "The serialized Config path could not be resolved.", "action-v1-local-issue");
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
                AddParagraph(parent, "Unsupported GameplayItem type.", "action-v1-local-issue");
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
            AddParagraph(parent, "Legacy gravity override is invalid in Action V1.", "action-v1-local-issue");
            var disable = new Button(() => SetSerializedBoolean(config.FindPropertyRelative("overrideGravityScale"), false))
                { text = "Disable Legacy Gravity Override" };
            disable.SetEnabled(editable);
            parent.Add(disable);
        }
        Foldout advanced = AdvancedFoldout();
        AddBoundProperty(advanced, impulse.FindPropertyRelative("debugLog"), "Debug Log", editable);
        parent.Add(advanced);
    }

    private void DrawHitBox(VisualElement parent, HitBoxItem item, SerializedProperty config, bool editable)
    {
        if (item.Config.hitboxConfig == null || item.Config.dataConfig == null || item.Config.effects == null)
        {
            AddMissingConfig(parent, item, editable, "One or more required HitBox settings are missing.");
            return;
        }
        AddSubheading(parent, "Bone");
        AddImGuiProperty(parent, config.FindPropertyRelative("boneReference"), "Binding", editable);
        AddSubheading(parent, "Shape");
        AddImGuiProperty(parent, config.FindPropertyRelative("hitboxConfig"), "Shape", editable);
        AddSubheading(parent, "Attack Data");
        AddImGuiProperty(parent, config.FindPropertyRelative("dataConfig"), "Attack Data", editable);
        AddSubheading(parent, "Effects");
        AddImGuiProperty(parent, config.FindPropertyRelative("effects"), "Effects", editable);
    }

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
        Foldout advanced = AdvancedFoldout();
        AddBoundProperty(advanced, velocity.FindPropertyRelative("debugLog"), "Debug Log", editable);
        parent.Add(advanced);
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

    private TimingDraft CreateDraft(ActionV1DocumentEntry entry, ActionV1TimelineOperationKind kind)
    {
        var draft = new TimingDraft
        {
            Kind = entry.SelectionKind,
            EditorId = entry.EditorId,
            Source = entry.Source,
            Snapshot = ActionV1TimelineOperationSnapshot.Capture(_document, kind, new[] { entry.EditorId }),
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
        status.AddToClassList("action-v1-draft-status");
        parent.Add(status);
        VisualElement buttons = MiniButtons();
        Button apply = new Button(() => ApplyDraft(status)) { text = "Apply" };
        Button revert = new Button(() => RevertDraft()) { text = "Revert" };
        apply.SetEnabled(editable && !ActionV1EditorInteractionGate.IsActive);
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
        ActionV1TimelineOperationResult result = EvaluateDraft();
        ActionV1TimelineOperationCandidate candidate = result?.Candidates.Count > 0 ? result.Candidates[0] : null;
        int duration = candidate != null ? candidate.DurationFrames : _draft?.DurationFrames ?? 0;
        if (_draftDerived != null)
            _draftDerived.text = result != null && result.State != ActionV1TimelineOperationState.Rejected && duration > 0
                ? $"{duration} frames"
                : "Invalid";
        if (_draftEnd != null && _draft != null)
            _draftEnd.text = result != null && result.State != ActionV1TimelineOperationState.Rejected && duration > 0
                ? ((long)_draft.StartFrame + duration).ToString()
                : "Invalid";
        if (_draftStatus != null)
        {
            _draftStatus.text = result == null ? "Draft cannot be evaluated." :
                result.State == ActionV1TimelineOperationState.Allowed ? "Unapplied timing changes." :
                result.State == ActionV1TimelineOperationState.NoChange ? "No unapplied timing changes." : result.Message;
            _draftStatus.EnableInClassList("action-v1-draft-error", result == null || result.State == ActionV1TimelineOperationState.Rejected);
        }
    }

    private ActionV1TimelineOperationResult EvaluateDraft()
    {
        if (_draft?.Snapshot == null)
            return null;
        return _draft.Kind == ActionV1SelectionKind.AnimationSegment
            ? _draft.Snapshot.EvaluateAbsolute(_draft.StartFrame, _draft.DurationFrames, _draft.AnimationAsset,
                _draft.SourceStartTime, _draft.SourceEndTime, _draft.PlayRate)
            : _draft.Snapshot.EvaluateAbsolute(_draft.StartFrame, _draft.DurationFrames);
    }

    private void ApplyDraft(Label status)
    {
        if (ActionV1EditorInteractionGate.IsActive)
        {
            status.text = "Finish the active Timeline gesture before applying Details.";
            status.AddToClassList("action-v1-draft-error");
            return;
        }
        ActionV1TimelineOperationResult result = EvaluateDraft();
        if (result == null || result.State == ActionV1TimelineOperationState.Rejected)
        {
            status.text = result?.Message ?? "Draft cannot be evaluated.";
            status.AddToClassList("action-v1-draft-error");
            return;
        }
        if (result.State == ActionV1TimelineOperationState.NoChange)
        {
            status.text = "No timing change.";
            return;
        }
        if (!ActionV1EditorCommands.CommitTimelineOperation(_draft.Snapshot, result, out string message))
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
        field.AddToClassList("action-v1-edit-field");
        field.SetEnabled(editable);
        field.RegisterValueChangedCallback(evt => { changed(evt.newValue); RefreshDraftFeedback(); });
        parent.Add(field);
        return field;
    }

    private FloatField DraftFloat(VisualElement parent, string label, float value, Action<float> changed, bool editable)
    {
        var field = new FloatField(label) { value = value };
        field.AddToClassList("action-v1-edit-field");
        field.SetEnabled(editable);
        field.RegisterValueChangedCallback(evt => { changed(evt.newValue); RefreshDraftFeedback(); });
        parent.Add(field);
        return field;
    }

    private ObjectField DraftObject(VisualElement parent, string label, UnityEngine.Object value, Type objectType,
        Action<UnityEngine.Object> changed, bool editable)
    {
        var field = new ObjectField(label) { objectType = objectType, value = value, allowSceneObjects = false };
        field.AddToClassList("action-v1-edit-field");
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
                Label status = section.Q<Label>(className: "action-v1-draft-status");
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
        AddParagraph(parent, text, "action-v1-local-issue");
        var create = new Button(() => CreateMissingConfig(item)) { text = "Create Default Configuration" };
        create.SetEnabled(editable && !ActionV1EditorInteractionGate.IsActive);
        parent.Add(create);
    }

    private void CreateMissingConfig(GameplayItem item)
    {
        if (item == null || _document?.Asset == null || ActionV1EditorInteractionGate.IsActive)
            return;
        Undo.RecordObject(_document.Asset, "Create Action Item Configuration");
        FieldInfo field = item.GetType().GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
        object config = field?.GetValue(item);
        if (config == null && field != null)
        {
            config = Activator.CreateInstance(field.FieldType);
            field.SetValue(item, config);
        }
        if (config is ImpulseItemConfig impulse && impulse.impulse == null) impulse.impulse = new ImpulseConfig();
        if (config is HitBoxItemConfig hitBox)
        {
            if (hitBox.hitboxConfig == null) hitBox.hitboxConfig = new ActionHitBoxConfig();
            if (hitBox.dataConfig == null) hitBox.dataConfig = new AttackDataConfig();
            if (hitBox.effects == null) hitBox.effects = new List<ImpactEffectConfig>();
        }
        if (config is VelocityOverrideItemConfig velocity && velocity.velocity == null) velocity.velocity = new VelocityConfig();
        ActionV1EditorCommands.Commit(_document.Asset, ActionV1EditorChangeFlags.Content);
    }

    private object GetConfig(GameplayItem item)
    {
        return item?.GetType().GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(item);
    }

    private void DrawAdvanced(ActionV1DocumentEntry entry)
    {
        Foldout advanced = AdvancedFoldout();
        AddRow(advanced, "Editor ID", entry.EditorId);
        AddRow(advanced, "Authoring Path", entry.AuthoringPath);
        AddRow(advanced, "Identity", entry.IdentityState.ToString());
        _content.Add(advanced);
    }

    private Foldout AdvancedFoldout()
    {
        var foldout = new Foldout { text = "Advanced", value = _advancedOpen };
        foldout.AddToClassList("action-v1-advanced");
        foldout.RegisterValueChangedCallback(evt =>
        {
            if (ReferenceEquals(evt.target, foldout))
                _advancedOpen = evt.newValue;
        });
        return foldout;
    }

    private void DrawValidationSummary()
    {
        VisualElement validation = Section("Validation Summary");
        _validationReadiness = AddRow(validation, "Readiness", ActionV1EditorTheme.ReadinessLabel(_document.Readiness));
        _validationIssueCount = AddRow(validation, "Issues", (_document.Validation?.Issues.Count ?? 0).ToString());
        _validationErrorCount = AddRow(validation, "Errors", _document.ValidationIndex.ErrorCount.ToString());
        _validationSetupCount = AddRow(validation, "Needs Setup", _document.ValidationIndex.NeedsSetupCount.ToString());
        _content.Add(validation);
    }

    private void RefreshSelectionIssues(ActionV1DocumentEntry entry)
    {
        if (_selectionIssuesHost == null)
            return;
        _selectionIssuesHost.Clear();
        IReadOnlyList<ActionAuthoringValidationIssue> issues = entry != null
            ? _document.ValidationIndex.ForEntry(entry)
            : Array.Empty<ActionAuthoringValidationIssue>();
        if (issues.Count == 0)
            return;
        VisualElement topIssues = Section($"Selection Issues · {issues.Count}");
        AddIssueRows(topIssues, issues);
        _selectionIssuesHost.Add(topIssues);
    }

    private void AddIssueRows(VisualElement parent, IReadOnlyList<ActionAuthoringValidationIssue> issues)
    {
        foreach (ActionAuthoringValidationIssue issue in issues)
        {
            bool setup = ActionV1ValidationIndex.SeverityOf(issue.Code) == ActionV1ValidationSeverity.NeedsSetup;
            string severity = setup ? "Needs setup" : ActionV1ValidationIndex.IsIdentity(issue.Code) ? "Identity" : "Error";
            AddParagraph(parent, $"{severity} · {issue.Code} · {issue.Message}",
                setup ? "action-v1-local-setup" : "action-v1-local-issue");
        }
    }

    private SerializedProperty ResolveEntryProperty(ActionV1DocumentEntry entry)
    {
        if (_serializedAction == null || entry == null)
            return null;
        SerializedProperty timeline = _serializedAction.FindProperty("_actionTimeline");
        if (entry.SelectionKind == ActionV1SelectionKind.AnimationSegment)
            return timeline?.FindPropertyRelative("_animationSegments")?.GetArrayElementAtIndex(entry.ItemIndex);
        SerializedProperty lanes = timeline?.FindPropertyRelative("_gameplayLanes");
        if (lanes == null || entry.LaneIndex < 0 || entry.LaneIndex >= lanes.arraySize)
            return null;
        SerializedProperty lane = lanes.GetArrayElementAtIndex(entry.LaneIndex);
        if (entry.SelectionKind == ActionV1SelectionKind.GameplayLane)
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
            AddParagraph(parent, $"Serialized field '{label}' is unavailable.", "action-v1-local-issue");
            return null;
        }
        var field = string.IsNullOrEmpty(label) ? new PropertyField(property) : new PropertyField(property, label);
        field.AddToClassList("action-v1-edit-field");
        field.SetEnabled(editable);
        parent.Add(field);
        PropertyWatch watch = CreateWatch(property, rebuildConditional, rebuildTrigger, rebuildCancellation);
        field.TrackPropertyValue(property, _ => QueuePropertyCheck(watch));
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
            AddParagraph(parent, $"Serialized field '{label}' is unavailable.", "action-v1-local-issue");
            return null;
        }
        PropertyWatch watch = CreateWatch(property, rebuildConfiguration, rebuildTrigger, rebuildCancellation);
        var container = new IMGUIContainer(() => DrawImGuiProperty(watch, label, editable));
        container.AddToClassList("action-v1-imgui-property");
        parent.Add(container);
        container.schedule.Execute(() => QueuePropertyCheck(watch));
        return container;
    }

    private PropertyWatch CreateWatch(SerializedProperty property, bool rebuildConfiguration, bool rebuildTrigger,
        bool rebuildCancellation)
    {
        return new PropertyWatch
        {
            Path = property.propertyPath,
            Fingerprint = PropertyFingerprint(property),
            Session = _bindingSession,
            RebuildConfiguration = rebuildConfiguration,
            RebuildTrigger = rebuildTrigger,
            RebuildCancellation = rebuildCancellation,
            RefreshTimelinePresentation = property.propertyPath.EndsWith("._muted", StringComparison.Ordinal) ||
                                          property.propertyPath.EndsWith("._name", StringComparison.Ordinal),
        };
    }

    private void DrawImGuiProperty(PropertyWatch watch, string label, bool editable)
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
        EditorGUI.BeginDisabledGroup(!editable || ActionV1EditorInteractionGate.IsActive);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUI.EndDisabledGroup();
        if (changed)
            _serializedAction.ApplyModifiedProperties();
        CheckPropertyWatch(watch);
    }

    private void QueuePropertyCheck(PropertyWatch watch)
    {
        if (watch == null || watch.Session != _bindingSession || _buildingPage || rootVisualElement == null)
            return;
        int session = _bindingSession;
        rootVisualElement.schedule.Execute(() =>
        {
            if (session == _bindingSession)
                CheckPropertyWatch(watch);
        });
    }

    private void CheckPropertyWatch(PropertyWatch watch)
    {
        if (watch == null || watch.Session != _bindingSession || _serializedAction == null || _buildingPage)
            return;
        _serializedAction.UpdateIfRequiredOrScript();
        SerializedProperty property = _serializedAction.FindProperty(watch.Path);
        if (property == null)
            return;
        int fingerprint = PropertyFingerprint(property);
        if (fingerprint == watch.Fingerprint)
            return;
        watch.Fingerprint = fingerprint;
        OnLeafChanged(watch);
    }

    private void OnLeafChanged(PropertyWatch watch)
    {
        if (ActionV1EditorInteractionGate.IsActive)
        {
            SetPageStatus("A Timeline gesture is active; finish it before editing Details.", true);
            return;
        }
        _lastObservedDirtyCount = _document?.Asset != null ? EditorUtility.GetDirtyCount(_document.Asset) : -1;
        _localNotification = true;
        ActionV1EditorChangeFlags flags = ActionV1EditorChangeFlags.Content;
        if (watch.RefreshTimelinePresentation)
            flags |= ActionV1EditorChangeFlags.Presentation;
        ActionV1EditorContext.Shared.NotifyAssetChanged(flags);
        _localNotification = false;
        rootVisualElement.schedule.Execute(() =>
        {
            if (watch.Session != _bindingSession)
                return;
            if (watch.RebuildTrigger)
                RebuildTriggerSection();
            else if (watch.RebuildCancellation)
                RebuildCancellationSection();
            else if (watch.RebuildConfiguration)
                RebuildConfigurationSection();
            RefreshDocumentOnly();
        });
    }

    private static int PropertyFingerprint(SerializedProperty source)
    {
        if (source == null)
            return 0;
        unchecked
        {
            int hash = 17;
            SerializedProperty property = source.Copy();
            SerializedProperty end = property.GetEndProperty();
            MixProperty(ref hash, property);
            while (property.NextVisible(true) && !SerializedProperty.EqualContents(property, end))
            {
                MixProperty(ref hash, property);
            }
            return hash;
        }
    }

    private static void MixProperty(ref int hash, SerializedProperty property)
    {
        unchecked
        {
            hash = hash * 31 + (property.propertyPath?.GetHashCode() ?? 0);
            hash = hash * 31 + (int)property.propertyType;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: hash = hash * 31 + property.longValue.GetHashCode(); break;
                case SerializedPropertyType.Boolean: hash = hash * 31 + property.boolValue.GetHashCode(); break;
                case SerializedPropertyType.Float: hash = hash * 31 + property.doubleValue.GetHashCode(); break;
                case SerializedPropertyType.String: hash = hash * 31 + (property.stringValue?.GetHashCode() ?? 0); break;
                case SerializedPropertyType.Color: hash = hash * 31 + property.colorValue.GetHashCode(); break;
                case SerializedPropertyType.ObjectReference:
                    hash = hash * 31 + (property.objectReferenceValue != null ? property.objectReferenceValue.GetInstanceID() : 0); break;
                case SerializedPropertyType.LayerMask: hash = hash * 31 + property.intValue; break;
                case SerializedPropertyType.Enum: hash = hash * 31 + property.enumValueIndex; break;
                case SerializedPropertyType.Vector2: hash = hash * 31 + property.vector2Value.GetHashCode(); break;
                case SerializedPropertyType.Vector3: hash = hash * 31 + property.vector3Value.GetHashCode(); break;
                case SerializedPropertyType.Vector4: hash = hash * 31 + property.vector4Value.GetHashCode(); break;
                case SerializedPropertyType.Rect: hash = hash * 31 + property.rectValue.GetHashCode(); break;
                case SerializedPropertyType.Bounds: hash = hash * 31 + property.boundsValue.GetHashCode(); break;
                case SerializedPropertyType.Quaternion: hash = hash * 31 + property.quaternionValue.GetHashCode(); break;
                case SerializedPropertyType.ManagedReference:
                    hash = hash * 31 + property.managedReferenceId.GetHashCode();
                    hash = hash * 31 + (property.managedReferenceFullTypename?.GetHashCode() ?? 0);
                    break;
                case SerializedPropertyType.AnimationCurve:
                    AnimationCurve curve = property.animationCurveValue;
                    if (curve == null)
                        break;
                    hash = hash * 31 + curve.length;
                    hash = hash * 31 + (int)curve.preWrapMode;
                    hash = hash * 31 + (int)curve.postWrapMode;
                    Keyframe[] keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        Keyframe key = keys[i];
                        hash = hash * 31 + key.time.GetHashCode();
                        hash = hash * 31 + key.value.GetHashCode();
                        hash = hash * 31 + key.inTangent.GetHashCode();
                        hash = hash * 31 + key.outTangent.GetHashCode();
                        hash = hash * 31 + key.inWeight.GetHashCode();
                        hash = hash * 31 + key.outWeight.GetHashCode();
                        hash = hash * 31 + (int)key.weightedMode;
                    }
                    break;
            }
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
                hash = hash * 31 + property.arraySize;
        }
    }

    private void RefreshDocumentOnly()
    {
        _document = ActionV1EditorDocument.Build(ActionV1EditorContext.Shared.CurrentAction);
        _serializedAction?.UpdateIfRequiredOrScript();
        ActionV1EditorContext.Shared.ValidateSelection(_document);
        ActionV1SelectionValue selection = ActionV1EditorContext.Shared.PrimarySelection;
        if (_selectionIssuesHost != null && !string.IsNullOrEmpty(selection.EditorId) &&
            _document.ById.TryGetValue(selection.EditorId, out ActionV1DocumentEntry entry))
            RefreshSelectionIssues(entry);
        if (_validationReadiness != null) _validationReadiness.text = ActionV1EditorTheme.ReadinessLabel(_document.Readiness);
        if (_validationIssueCount != null) _validationIssueCount.text = (_document.Validation?.Issues.Count ?? 0).ToString();
        if (_validationErrorCount != null) _validationErrorCount.text = _document.ValidationIndex.ErrorCount.ToString();
        if (_validationSetupCount != null) _validationSetupCount.text = _document.ValidationIndex.NeedsSetupCount.ToString();
    }

    private void SetSerializedBoolean(SerializedProperty property, bool value)
    {
        if (property == null || ActionV1EditorInteractionGate.IsActive)
            return;
        property.boolValue = value;
        property.serializedObject.ApplyModifiedProperties();
        OnLeafChanged(CreateWatch(property, true, false, false));
    }

    private List<CancelRule> GetCancelRules()
    {
        return _document?.Asset == null
            ? null
            : typeof(ActionAsset).GetField("_cancelRules", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(_document.Asset) as List<CancelRule>;
    }

    private void AddCancelRule()
    {
        List<CancelRule> rules = GetCancelRules();
        if (!CanWriteStructure() || rules == null) return;
        Undo.RegisterCompleteObjectUndo(_document.Asset, "Add Action Cancel Rule");
        rules.Add(new CancelRule());
        ActionV1EditorCommands.Commit(_document.Asset, ActionV1EditorChangeFlags.Content);
    }

    private void RemoveCancelRule(int index)
    {
        List<CancelRule> rules = GetCancelRules();
        if (!CanWriteStructure() || rules == null || index < 0 || index >= rules.Count) return;
        Undo.RegisterCompleteObjectUndo(_document.Asset, "Remove Action Cancel Rule");
        rules.RemoveAt(index);
        ActionV1EditorCommands.Commit(_document.Asset, ActionV1EditorChangeFlags.Content);
    }

    private void MoveCancelRule(int from, int to)
    {
        List<CancelRule> rules = GetCancelRules();
        if (!CanWriteStructure() || rules == null || from < 0 || from >= rules.Count || to < 0 || to >= rules.Count) return;
        Undo.RegisterCompleteObjectUndo(_document.Asset, "Reorder Action Cancel Rules");
        CancelRule rule = rules[from];
        rules.RemoveAt(from);
        rules.Insert(to, rule);
        ActionV1EditorCommands.Commit(_document.Asset, ActionV1EditorChangeFlags.Content);
    }

    private bool CanWriteStructure() => _document?.Asset != null &&
        _document.Readiness != ActionV1EditorReadiness.IdentityBlocked && !ActionV1EditorInteractionGate.IsActive;

    private void AddHeading(string title, string subtitle)
    {
        var titleLabel = new Label(title);
        titleLabel.AddToClassList("action-v1-details-heading");
        _content.Add(titleLabel);
        var subtitleLabel = new Label(subtitle);
        subtitleLabel.AddToClassList("action-v1-details-subtitle");
        _content.Add(subtitleLabel);
    }

    private static VisualElement Section(string title)
    {
        var section = new VisualElement();
        section.AddToClassList("action-v1-section");
        AddSubheading(section, title);
        return section;
    }

    private static Foldout FoldoutSection(string title, bool value, Action<bool> changed)
    {
        var foldout = new Foldout { text = title, value = value };
        foldout.AddToClassList("action-v1-section");
        foldout.RegisterValueChangedCallback(evt =>
        {
            if (ReferenceEquals(evt.target, foldout))
                changed(evt.newValue);
        });
        return foldout;
    }

    private static void AddSubheading(VisualElement parent, string title)
    {
        var heading = new Label(title);
        heading.AddToClassList("action-v1-section-title");
        parent.Add(heading);
    }

    private static Label AddRow(VisualElement parent, string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("action-v1-readonly-row");
        var key = new Label(label);
        key.AddToClassList("action-v1-readonly-label");
        row.Add(key);
        var result = new Label(string.IsNullOrEmpty(value) ? "—" : value) { name = label == "Candidate End Exclusive" ? "draft-end" : null };
        result.AddToClassList("action-v1-readonly-value");
        row.Add(result);
        parent.Add(row);
        return result;
    }

    private static void AddParagraph(VisualElement parent, string text, string className = "action-v1-details-subtitle")
    {
        var label = new Label(text);
        label.AddToClassList(className);
        parent.Add(label);
    }

    private static VisualElement MiniButtons()
    {
        var row = new VisualElement();
        row.AddToClassList("action-v1-mini-buttons");
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
            _pageStatus.AddToClassList("action-v1-page-status");
            _content.Insert(0, _pageStatus);
        }
        _pageStatus.text = message ?? string.Empty;
        _pageStatus.EnableInClassList("action-v1-draft-error", error);
    }

    private void OnContextChanged(ActionV1EditorChangeFlags flags)
    {
        if (_localNotification)
            return;
        if ((flags & (ActionV1EditorChangeFlags.Context | ActionV1EditorChangeFlags.Structure |
                      ActionV1EditorChangeFlags.Timing | ActionV1EditorChangeFlags.Content |
                      ActionV1EditorChangeFlags.Validation)) != 0)
        {
            RefreshDocumentAndPage(true);
            return;
        }
        if ((flags & ActionV1EditorChangeFlags.Selection) != 0)
        {
            ActionV1SelectionValue primary = ActionV1EditorContext.Shared.PrimarySelection;
            if (!SameSelection(primary, _displayedPrimary))
                RefreshDocumentAndPage(true);
            else if (_selectionSummary != null)
                _selectionSummary.text = $"Primary Selection  ·  {PrimaryName(primary)}  ·  " +
                                         $"{ActionV1EditorContext.Shared.SelectedIds.Count} content selected";
        }
        if ((flags & ActionV1EditorChangeFlags.Frame) != 0 && _frameValue != null)
            _frameValue.text = ActionV1EditorContext.Shared.CurrentFrame.ToString();
    }

    private void OnExternalDataChanged()
    {
        ActionAsset asset = ActionV1EditorContext.Shared.CurrentAction;
        int dirtyCount = asset != null ? EditorUtility.GetDirtyCount(asset) : -1;
        if (dirtyCount == _lastObservedDirtyCount)
            return;
        _lastObservedDirtyCount = dirtyCount;
        DiscardDraft();
        rootVisualElement?.schedule.Execute(() => RefreshDocumentAndPage(true));
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        bool narrow = evt.newRect.width < 520f;
        rootVisualElement.EnableInClassList("action-v1-details-narrow", narrow);
        rootVisualElement.EnableInClassList("action-v1-compact", narrow);
    }

    private void SaveScroll()
    {
        if (_scroll != null)
            _savedScrollY = _scroll.scrollOffset.y;
    }

    private void DiscardDraft() => _draft = null;

    private static string PrimaryName(ActionV1SelectionValue selection) =>
        selection.Kind == ActionV1SelectionKind.None ? "Action" : selection.Kind.ToString();
    private static bool SameSelection(ActionV1SelectionValue left, ActionV1SelectionValue right) =>
        left.Kind == right.Kind && string.Equals(left.EditorId, right.EditorId, StringComparison.Ordinal);
    private static string ObjectName(UnityEngine.Object value) => value != null ? value.name : "None";
}
#endif
