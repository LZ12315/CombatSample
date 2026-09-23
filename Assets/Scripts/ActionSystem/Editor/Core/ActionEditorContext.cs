#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[Flags]
internal enum ActionEditorChangeFlags
{
    None = 0,
    Context = 1 << 0,
    Selection = 1 << 1,
    Frame = 1 << 2,
    Structure = 1 << 3,
    Timing = 1 << 4,
    Content = 1 << 5,
    Preview = 1 << 7,
    Presentation = 1 << 8,
    PreviewPosition = 1 << 9,
    PreviewCharacter = 1 << 10,
    PreviewResources = 1 << 11,
    Playback = 1 << 12,
    All = ~0,
}

internal enum ActionEditorChangeOrigin
{
    Session,
    Command,
    Binding,
    UndoRedo,
    Project,
    ObjectChange,
    Playback,
}

internal readonly struct ActionEditorChange
{
    internal ActionEditorChange(ActionAsset action, ActionEditorChangeFlags flags, int documentVersion,
        ActionEditorChangeOrigin origin)
    {
        Action = action;
        Flags = flags;
        DocumentVersion = documentVersion;
        Origin = origin;
    }

    internal ActionAsset Action { get; }
    internal ActionEditorChangeFlags Flags { get; }
    internal int DocumentVersion { get; }
    internal ActionEditorChangeOrigin Origin { get; }
}

internal enum ActionSelectionKind
{
    None,
    Action,
    AnimationSegment,
    GameplayLane,
    GameplayItem,
}

internal enum ActionEditorReadiness
{
    NoAction,
    IdentityBlocked,
    Ready,
}

internal enum ActionEditorIdentityState
{
    NotApplicable,
    Valid,
    Missing,
    Malformed,
    Duplicate,
}

[Flags]
internal enum ActionEntryDisplayState
{
    Normal = 0,
    NullEntry = 1 << 0,
    NegativeStart = 1 << 1,
    InvalidDuration = 1 << 2,
    MissingSource = 1 << 3,
    GeometryOverflow = 1 << 5,
}

[Serializable]
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp-Editor", sourceClassName: "ActionV1SelectionValue")]
internal struct ActionSelectionValue
{
    [SerializeField] private ActionSelectionKind _kind;
    [SerializeField] private string _editorId;

    public ActionSelectionValue(ActionSelectionKind kind, string editorId)
    {
        _kind = kind;
        _editorId = editorId ?? string.Empty;
    }

    public ActionSelectionKind Kind => _kind;
    public string EditorId => _editorId;
}

internal static class ActionPreviewCharacterResolver
{
    internal static GameObject Resolve(ActionAsset action, GameObject overridePrefab)
    {
        if (overridePrefab != null)
            return overridePrefab;
        IReadOnlyList<AnimationSegment> segments = action?.Timeline?.AnimationSegments;
        for (int i = 0; segments != null && i < segments.Count; i++)
        {
            AnimationAsset animation = segments[i]?.AnimationAsset;
            AnimationRigAsset rig = animation != null ? animation.AnimationRigAsset : null;
            if (rig != null && rig.DefaultPreviewPrefab != null)
                return rig.DefaultPreviewPrefab;
        }
        return null;
    }
}

/// <summary>Purpose-specific comparison state used only when Unity reports an external object change.</summary>
internal readonly struct ActionEditorObservedState
{
    private const ActionEditorChangeFlags ObservedChangeMask =
        ActionEditorChangeFlags.Structure | ActionEditorChangeFlags.Timing |
        ActionEditorChangeFlags.Content | ActionEditorChangeFlags.Presentation |
        ActionEditorChangeFlags.PreviewResources;

    private readonly bool _valid;
    private readonly int _structure;
    private readonly int _timing;
    private readonly int _presentation;
    private readonly int _content;
    private readonly int _animationData;
    private readonly int _previewResources;

    private ActionEditorObservedState(int structure, int timing, int presentation, int content,
        int animationData, int previewResources)
    {
        _valid = true;
        _structure = structure;
        _timing = timing;
        _presentation = presentation;
        _content = content;
        _animationData = animationData;
        _previewResources = previewResources;
    }

    internal static ActionEditorObservedState Capture(ActionAsset action, GameObject characterOverride)
    {
        int structure = 17;
        int timing = 17;
        int presentation = 17;
        int content = 17;
        int animationData = 17;
        int previewResources = 17;

        Add(ref structure, action != null);
        Add(ref presentation, action != null ? action.name : string.Empty);
        if (action != null)
        {
            Add(ref content, (int)action.PriorityLayer);
            Add(ref content, action.PriorityValue);
            Add(ref content, action.IsLoop);
            Add(ref content, action.AllowReenterWhilePlaying);
            Add(ref content, (int)action.TriggerMode);
            AddJson(ref content, action.EventTriggerTag);
            Add(ref content, (int)action.StartContextMode);
            AddJsonList(ref content, action.CancelRules);
            AddJsonList(ref content, action.SelfTags);
            AddJsonList(ref content, action.EntryConditions);
            AddJsonList(ref content, action.ExitConditions);
        }

        var animationAssets = new List<AnimationAsset>();
        var knownAnimationAssets = new HashSet<int>();
        ActionTimelineData timeline = action != null ? action.Timeline : null;
        IReadOnlyList<AnimationSegment> segments = timeline?.AnimationSegments;
        Add(ref structure, segments?.Count ?? -1);
        for (int i = 0; segments != null && i < segments.Count; i++)
        {
            AnimationSegment segment = segments[i];
            Add(ref structure, segment != null);
            if (segment == null)
                continue;
            Add(ref structure, segment.EditorId);
            Add(ref timing, segment.StartFrame);
            AddObject(ref timing, segment.AnimationAsset);
            Add(ref timing, segment.SourceStartTime);
            Add(ref timing, segment.SourceEndTime);
            Add(ref timing, segment.PlayRate);
            CollectAnimation(segment.AnimationAsset, animationAssets, knownAnimationAssets);
        }

        IReadOnlyList<GameplayLane> lanes = timeline?.GameplayLanes;
        Add(ref structure, lanes?.Count ?? -1);
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            GameplayLane lane = lanes[laneIndex];
            Add(ref structure, lane != null);
            if (lane == null)
                continue;
            Add(ref structure, lane.EditorId);
            Add(ref presentation, lane.Name);
            Add(ref presentation, lane.Muted);
            Add(ref content, lane.Muted);
            IReadOnlyList<GameplayItem> items = lane.Items;
            Add(ref structure, items?.Count ?? -1);
            for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
            {
                GameplayItem item = items[itemIndex];
                Add(ref structure, item != null);
                if (item == null)
                    continue;
                Add(ref structure, item.EditorId);
                Add(ref structure, item.GetType().FullName);
                Add(ref presentation, item.Muted);
                Add(ref content, item.Muted);
                if (item is PointGameplayItem point)
                    Add(ref timing, point.Frame);
                else if (item is RangeGameplayItem range)
                {
                    Add(ref timing, range.StartFrame);
                    Add(ref timing, range.DurationFrames);
                }
                object config = ConfigOf(item);
                AddJson(ref content, config);
                if (item is RootMotionItem root)
                    CollectAnimation(root.Config?.animationAsset, animationAssets, knownAnimationAssets);
            }
        }

        for (int i = 0; i < animationAssets.Count; i++)
        {
            Add(ref presentation, animationAssets[i].name);
            AddAnimationData(ref animationData, animationAssets[i]);
        }

        GameObject previewPrefab = ActionPreviewCharacterResolver.Resolve(action, characterOverride);
        AddAssetDependency(ref previewResources, previewPrefab);
        return new ActionEditorObservedState(
            structure, timing, presentation, content, animationData, previewResources);
    }

    internal ActionEditorChangeFlags ChangesTo(ActionEditorObservedState current)
    {
        if (!_valid || !current._valid)
            return ActionEditorChangeFlags.Structure | ActionEditorChangeFlags.Timing |
                   ActionEditorChangeFlags.Content | ActionEditorChangeFlags.Presentation |
                   ActionEditorChangeFlags.PreviewResources;

        ActionEditorChangeFlags flags = ActionEditorChangeFlags.None;
        if (_structure != current._structure) flags |= ActionEditorChangeFlags.Structure;
        if (_timing != current._timing) flags |= ActionEditorChangeFlags.Timing;
        if (_presentation != current._presentation) flags |= ActionEditorChangeFlags.Presentation;
        if (_content != current._content || _animationData != current._animationData)
            flags |= ActionEditorChangeFlags.Content;
        if (_previewResources != current._previewResources)
            flags |= ActionEditorChangeFlags.PreviewResources;
        return flags;
    }

    internal static ActionEditorChangeFlags WithoutObservedFlags(ActionEditorChangeFlags flags) =>
        flags & ~ObservedChangeMask;

    private static object ConfigOf(GameplayItem item)
    {
        if (item is ImpulseItem impulse) return impulse.Config;
        if (item is HitBoxItem hitBox) return hitBox.Config;
        if (item is RootMotionItem root) return root.Config;
        if (item is SelfRotationItem rotation) return rotation.Config;
        if (item is VelocityOverrideItem velocity) return velocity.Config;
        if (item is MotionPolicyItem policy) return policy.Config;
        if (item is TagItem tag) return tag.Config;
        return null;
    }

    private static void CollectAnimation(AnimationAsset animation, List<AnimationAsset> values, HashSet<int> known)
    {
        if (animation == null || !known.Add(animation.GetInstanceID()))
            return;
        values.Add(animation);
    }

    private static void AddAnimationData(ref int hash, AnimationAsset animation)
    {
        AddObject(ref hash, animation);
        AnimationClip clip = animation != null ? animation.Clip : null;
        AddAssetDependency(ref hash, clip);
        Add(ref hash, clip != null ? clip.length : 0f);
        AddBakeDependencies(ref hash, animation);
        RootMotionTrajectory trajectory = animation != null ? animation.RootMotionData : null;
        Add(ref hash, trajectory != null);
        if (trajectory == null)
            return;
        AddObject(ref hash, trajectory.SourceClip);
        Add(ref hash, trajectory.SampleRate);
        Add(ref hash, trajectory.Duration);
        Add(ref hash, trajectory.BakerVersion);
        Add(ref hash, trajectory.DependencyHash);
        AddList(ref hash, trajectory.SampleTimes);
        AddList(ref hash, trajectory.CumulativePositions);
        AddList(ref hash, trajectory.CumulativeRotations);
    }

    private static void AddBakeDependencies(ref int hash, AnimationAsset animation)
    {
        AnimationRigAsset rig = animation != null ? animation.AnimationRigAsset : null;
        AddObject(ref hash, rig);
        RootMotionBakeSettings settings = animation != null ? animation.RootMotionBakeSettings : null;
        Add(ref hash, settings != null);
        if (settings == null)
            return;
        AddAssetDependency(ref hash, settings.ReferenceRigPrefab);
        Add(ref hash, settings.SampleRate);
        Add(ref hash, settings.PositionTolerance);
        Add(ref hash, settings.RotationToleranceDegrees);
    }

    private static void AddList(ref int hash, IReadOnlyList<float> values)
    {
        Add(ref hash, values?.Count ?? -1);
        for (int i = 0; values != null && i < values.Count; i++)
            Add(ref hash, values[i]);
    }

    private static void AddList(ref int hash, IReadOnlyList<Vector3> values)
    {
        Add(ref hash, values?.Count ?? -1);
        for (int i = 0; values != null && i < values.Count; i++)
            Add(ref hash, values[i]);
    }

    private static void AddList(ref int hash, IReadOnlyList<Quaternion> values)
    {
        Add(ref hash, values?.Count ?? -1);
        for (int i = 0; values != null && i < values.Count; i++)
            Add(ref hash, values[i]);
    }

    private static void AddJsonList<T>(ref int hash, IReadOnlyList<T> values)
    {
        Add(ref hash, values?.Count ?? -1);
        for (int i = 0; values != null && i < values.Count; i++)
            AddJson(ref hash, values[i]);
    }

    private static void AddJson(ref int hash, object value)
    {
        if (value == null)
        {
            Add(ref hash, 0);
            return;
        }
        Add(ref hash, value.GetType().FullName);
        try
        {
            Add(ref hash, EditorJsonUtility.ToJson(value));
        }
        catch (ArgumentException)
        {
            Add(ref hash, value.ToString());
        }
    }

    private static void AddAssetDependency(ref int hash, Object value)
    {
        AddObject(ref hash, value);
        if (value == null)
            return;
        string path = AssetDatabase.GetAssetPath(value);
        Add(ref hash, path);
        if (!string.IsNullOrEmpty(path))
            Add(ref hash, AssetDatabase.GetAssetDependencyHash(path).ToString());
    }

    private static void AddObject(ref int hash, Object value) =>
        Add(ref hash, value != null ? value.GetInstanceID() : 0);

    private static void Add(ref int hash, bool value) => Add(ref hash, value ? 1 : 0);
    private static void Add(ref int hash, int value) => hash = unchecked(hash * 31 + value);
    private static void Add(ref int hash, float value) => Add(ref hash, value.GetHashCode());
    private static void Add(ref int hash, string value) =>
        Add(ref hash, value != null ? StringComparer.Ordinal.GetHashCode(value) : 0);
    private static void Add(ref int hash, Vector3 value)
    {
        Add(ref hash, value.x);
        Add(ref hash, value.y);
        Add(ref hash, value.z);
    }
    private static void Add(ref int hash, Quaternion value)
    {
        Add(ref hash, value.x);
        Add(ref hash, value.y);
        Add(ref hash, value.z);
        Add(ref hash, value.w);
    }
}

/// <summary>Shared, session-only state for the three Action authoring windows.</summary>
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp-Editor", sourceClassName: "ActionV1EditorContext")]
internal sealed class ActionEditorContext : ScriptableSingleton<ActionEditorContext>
{
    [SerializeField] private ActionAsset _currentAction;
    [SerializeField] private int _currentFrame;
    [SerializeField] private double _previewPosition;
    [SerializeField] private ActionSelectionValue _primarySelection;
    [SerializeField] private List<ActionSelectionValue> _selectedEntries = new List<ActionSelectionValue>();
    [SerializeField] private GameObject _previewCharacter;
    [SerializeField] private bool _previewLoop;

    [NonSerialized] private ActionEditorDocument _document;
    [NonSerialized] private int _documentVersion;
    [NonSerialized] private int _sessionVersion;
    [NonSerialized] private ActionEditorObservedState _observedState;
    [NonSerialized] private bool _bindingChangeQueued;
    [NonSerialized] private int _bindingChangeGeneration;
    [NonSerialized] private ActionAsset _queuedBindingAction;
    [NonSerialized] private int _queuedBindingSessionVersion;
    [NonSerialized] private ActionEditorChangeFlags _queuedBindingFlags;

    internal static event Action<ActionEditorChange> Changed;

    internal static ActionEditorContext Shared
    {
        get
        {
            return instance;
        }
    }
    internal ActionAsset CurrentAction => _currentAction;
    internal ActionEditorDocument Document
    {
        get
        {
            if (_document == null || _document.Asset != _currentAction)
                RebuildDocument();
            return _document;
        }
    }
    internal int DocumentVersion => _documentVersion;
    internal int SessionVersion => _sessionVersion;
    internal int CurrentFrame => _currentFrame;
    internal double PreviewPosition => _previewPosition;
    internal ActionSelectionValue PrimarySelection => _primarySelection;
    private List<ActionSelectionValue> SelectionEntries =>
        _selectedEntries ?? (_selectedEntries = new List<ActionSelectionValue>());

    internal IReadOnlyList<string> SelectedIds => SelectionEntries.Select(value => value.EditorId).ToArray();
    internal IReadOnlyList<ActionSelectionValue> SelectedEntries => SelectionEntries;
    internal GameObject PreviewCharacter => _previewCharacter;
    internal bool PreviewLoop => _previewLoop;

    internal bool SetAction(ActionAsset action)
    {
        if (action == null) action = null; // Normalize destroyed Unity objects to a real null.
        if (ReferenceEquals(_currentAction, action))
            return true;

        CancelQueuedBindingChange();
        _currentAction = action;
        _currentFrame = 0;
        _previewPosition = 0d;
        ActionEditorPlayback.Stop(false);
        _primarySelection = action != null
            ? new ActionSelectionValue(ActionSelectionKind.Action, string.Empty)
            : default;
        SelectionEntries.Clear();
        _sessionVersion++;
        RebuildDocument();
        Publish(ActionEditorChangeFlags.Context | ActionEditorChangeFlags.Selection |
              ActionEditorChangeFlags.Frame |
               ActionEditorChangeFlags.Preview | ActionEditorChangeFlags.PreviewPosition |
               ActionEditorChangeFlags.PreviewResources | ActionEditorChangeFlags.Playback,
            ActionEditorChangeOrigin.Session);
        return true;
    }

    internal void SetFrame(int frame)
    {
        int duration = _currentAction != null && _currentAction.Timeline != null ? _currentAction.Timeline.DurationFrames : 1;
        int last = Mathf.Max(0, duration - 1);
        int clamped = Mathf.Clamp(frame, 0, last);
        if (_currentFrame == clamped && Math.Abs(_previewPosition - clamped) <= 1e-9d)
            return;
        _currentFrame = clamped;
        _previewPosition = clamped;
        Publish(ActionEditorChangeFlags.Frame | ActionEditorChangeFlags.Preview |
                ActionEditorChangeFlags.PreviewPosition, ActionEditorChangeOrigin.Session);
    }

    internal void SetPreviewPosition(double position)
    {
        int duration = _currentAction != null && _currentAction.Timeline != null
            ? _currentAction.Timeline.DurationFrames
            : 1;
        double clamped = Math.Max(0d, Math.Min(duration, double.IsNaN(position) ? 0d : position));
        int frame = Mathf.Clamp((int)Math.Floor(clamped), 0, Mathf.Max(0, duration - 1));
        if (Math.Abs(_previewPosition - clamped) <= 1e-9d && _currentFrame == frame)
            return;
        bool frameChanged = _currentFrame != frame;
        _previewPosition = clamped;
        _currentFrame = frame;
        Publish(ActionEditorChangeFlags.Preview | ActionEditorChangeFlags.PreviewPosition |
                (frameChanged ? ActionEditorChangeFlags.Frame : ActionEditorChangeFlags.None),
            ActionEditorChangeOrigin.Session);
    }

    internal void SelectAction()
    {
        _primarySelection = new ActionSelectionValue(ActionSelectionKind.Action, string.Empty);
        SelectionEntries.Clear();
        Publish(ActionEditorChangeFlags.Selection, ActionEditorChangeOrigin.Session);
    }

    internal void Select(ActionSelectionKind kind, string editorId, bool additive, bool toggle)
    {
        if (kind == ActionSelectionKind.Action || string.IsNullOrEmpty(editorId))
        {
            SelectAction();
            return;
        }

        if (!additive && !toggle)
            SelectionEntries.Clear();

        int existing = SelectionEntries.FindIndex(value => string.Equals(value.EditorId, editorId, StringComparison.Ordinal));
        if (toggle && existing >= 0)
        {
            bool removedPrimary = string.Equals(_primarySelection.EditorId, editorId, StringComparison.Ordinal);
            SelectionEntries.RemoveAt(existing);
            if (SelectionEntries.Count == 0)
                _primarySelection = new ActionSelectionValue(ActionSelectionKind.Action, string.Empty);
            else if (removedPrimary)
                _primarySelection = SelectionEntries[SelectionEntries.Count - 1];
        }
        else
        {
            if (existing < 0)
                SelectionEntries.Add(new ActionSelectionValue(kind, editorId));
            else
                SelectionEntries[existing] = new ActionSelectionValue(kind, editorId);
            _primarySelection = SelectionEntries[existing < 0 ? SelectionEntries.Count - 1 : existing];
        }

        Publish(ActionEditorChangeFlags.Selection, ActionEditorChangeOrigin.Session);
    }

    internal void SetSelection(IEnumerable<ActionDocumentEntry> entries, bool additive)
    {
        List<ActionDocumentEntry> candidates = (entries ?? Enumerable.Empty<ActionDocumentEntry>())
            .Where(entry => entry != null && entry.HasStableSelection)
            .ToList();
        if (additive && candidates.Count == 0)
            return;

        if (!additive)
            SelectionEntries.Clear();

        ActionDocumentEntry last = null;
        foreach (ActionDocumentEntry entry in candidates)
        {
            int existing = SelectionEntries.FindIndex(value => string.Equals(value.EditorId, entry.EditorId, StringComparison.Ordinal));
            var value = new ActionSelectionValue(entry.SelectionKind, entry.EditorId);
            if (existing < 0)
                SelectionEntries.Add(value);
            else
                SelectionEntries[existing] = value;
            last = entry;
        }

        _primarySelection = last != null
            ? new ActionSelectionValue(last.SelectionKind, last.EditorId)
            : new ActionSelectionValue(ActionSelectionKind.Action, string.Empty);
        Publish(ActionEditorChangeFlags.Selection, ActionEditorChangeOrigin.Session);
    }

    internal bool IsSelected(string editorId) => !string.IsNullOrEmpty(editorId) &&
        SelectionEntries.Any(value => string.Equals(value.EditorId, editorId, StringComparison.Ordinal));

    private ActionEditorChangeFlags ReconcileState(ActionEditorDocument document)
    {
        ActionSelectionValue oldPrimary = _primarySelection;
        int oldSelectionCount = SelectionEntries.Count;
        int oldFrame = _currentFrame;
        double oldPosition = _previewPosition;
        if (_currentAction == null)
        {
            SelectionEntries.Clear();
            _primarySelection = default;
            _currentFrame = 0;
            _previewPosition = 0d;
            return ActionEditorChangeFlags.Selection | ActionEditorChangeFlags.Frame |
                   ActionEditorChangeFlags.PreviewPosition;
        }

        SelectionEntries.RemoveAll(value => document == null || !document.ById.ContainsKey(value.EditorId));
        for (int i = 0; i < SelectionEntries.Count; i++)
        {
            ActionDocumentEntry entry = document.ById[SelectionEntries[i].EditorId];
            SelectionEntries[i] = new ActionSelectionValue(entry.SelectionKind, entry.EditorId);
        }
        bool primaryIsSelected = _primarySelection.Kind != ActionSelectionKind.Action &&
                                 SelectionEntries.Any(value => string.Equals(
                                     value.EditorId,
                                     _primarySelection.EditorId,
                                     StringComparison.Ordinal));
        if (SelectionEntries.Count == 0)
        {
            _primarySelection = new ActionSelectionValue(ActionSelectionKind.Action, string.Empty);
        }
        else if (!primaryIsSelected)
        {
            _primarySelection = SelectionEntries[SelectionEntries.Count - 1];
        }
        else
        {
            ActionDocumentEntry primary = document.ById[_primarySelection.EditorId];
            _primarySelection = new ActionSelectionValue(primary.SelectionKind, primary.EditorId);
        }
        int duration = document != null ? document.DurationFrames : 1;
        _previewPosition = Math.Max(0d, Math.Min(duration, double.IsNaN(_previewPosition) ? 0d : _previewPosition));
        _currentFrame = Mathf.Clamp((int)Math.Floor(_previewPosition), 0, Mathf.Max(0, duration - 1));

        ActionEditorChangeFlags flags = ActionEditorChangeFlags.None;
        bool selectionChanged = oldSelectionCount != SelectionEntries.Count ||
                                oldPrimary.Kind != _primarySelection.Kind ||
                                !string.Equals(oldPrimary.EditorId, _primarySelection.EditorId, StringComparison.Ordinal);
        if (selectionChanged) flags |= ActionEditorChangeFlags.Selection;
        if (oldFrame != _currentFrame) flags |= ActionEditorChangeFlags.Frame;
        if (Math.Abs(oldPosition - _previewPosition) > 1e-9d)
            flags |= ActionEditorChangeFlags.PreviewPosition | ActionEditorChangeFlags.Preview;
        return flags;
    }

    internal void SetPreviewCharacter(GameObject value)
    {
        if (_previewCharacter == value)
            return;
        _previewCharacter = value;
        ApplyAssetChange(_currentAction,
                ActionEditorChangeFlags.Preview | ActionEditorChangeFlags.PreviewCharacter |
                ActionEditorChangeFlags.PreviewResources, ActionEditorChangeOrigin.Session);
    }

    internal void SetPreviewLoop(bool value)
    {
        if (_previewLoop == value)
            return;
        _previewLoop = value;
        Publish(ActionEditorChangeFlags.Preview, ActionEditorChangeOrigin.Session);
    }

    internal void ApplyAssetChange(ActionAsset asset, ActionEditorChangeFlags flags,
        ActionEditorChangeOrigin origin = ActionEditorChangeOrigin.Command,
        IReadOnlyList<ActionSelectionValue> replacementSelection = null)
    {
        if (asset != _currentAction)
            return;
        flags |= ConsumeQueuedBindingChange(asset);
        ActionEditorObservedState currentState =
            ActionEditorObservedState.Capture(_currentAction, _previewCharacter);
        ActionEditorChangeFlags detectedFlags = _observedState.ChangesTo(currentState);
        bool actualStateIsAuthoritative = origin == ActionEditorChangeOrigin.Binding ||
                                          origin == ActionEditorChangeOrigin.UndoRedo ||
                                          origin == ActionEditorChangeOrigin.Project ||
                                          origin == ActionEditorChangeOrigin.ObjectChange;
        if (actualStateIsAuthoritative)
        {
            flags = ActionEditorObservedState.WithoutObservedFlags(flags) | detectedFlags;
        }
        else
        {
            // A command can run while a Unity object callback is still pending. Publish both the
            // declared command effect and every real change since the last stable session state.
            flags |= detectedFlags;
        }
        if (flags == ActionEditorChangeFlags.None)
            return;
        bool shouldStopPlayback = origin == ActionEditorChangeOrigin.UndoRedo ||
                                  (flags & (ActionEditorChangeFlags.Structure |
                                            ActionEditorChangeFlags.Timing)) != 0;
        if (shouldStopPlayback && ActionEditorPlayback.IsPlaying)
        {
            ActionEditorPlayback.Stop(false);
            flags |= ActionEditorChangeFlags.Playback;
        }

        bool documentChanged = (flags & (ActionEditorChangeFlags.Structure |
                                         ActionEditorChangeFlags.Timing |
                                         ActionEditorChangeFlags.Content |
                                         ActionEditorChangeFlags.Presentation)) != 0;
        if (documentChanged)
            RebuildDocument(currentState);
        else
            _observedState = currentState;
        if (replacementSelection != null)
        {
            SelectionEntries.Clear();
            for (int i = 0; i < replacementSelection.Count; i++)
                SelectionEntries.Add(replacementSelection[i]);
            _primarySelection = SelectionEntries.Count > 0
                ? SelectionEntries[SelectionEntries.Count - 1]
                : new ActionSelectionValue(ActionSelectionKind.Action, string.Empty);
            flags |= ActionEditorChangeFlags.Selection;
        }
        if (documentChanged || replacementSelection != null)
            flags |= ReconcileState(Document);
        if ((flags & (ActionEditorChangeFlags.Structure | ActionEditorChangeFlags.Timing |
                      ActionEditorChangeFlags.Content)) != 0)
            flags |= ActionEditorChangeFlags.Preview;
        Publish(flags, origin);
    }

    /// <summary>Coalesces the property callbacks produced by one UI Toolkit binding transaction.</summary>
    internal void QueueBindingChange(ActionAsset asset, ActionEditorChangeFlags flags)
    {
        if (asset != _currentAction || flags == ActionEditorChangeFlags.None)
            return;
        _queuedBindingAction = asset;
        _queuedBindingSessionVersion = _sessionVersion;
        _queuedBindingFlags |= flags;
        if (_bindingChangeQueued)
            return;

        _bindingChangeQueued = true;
        int generation = _bindingChangeGeneration;
        EditorApplication.delayCall += () =>
        {
            if (generation != _bindingChangeGeneration)
                return;
            FlushQueuedBindingChange();
        };
    }

    internal void FlushQueuedBindingChange()
    {
        ActionAsset queuedAction = _queuedBindingAction;
        int queuedSessionVersion = _queuedBindingSessionVersion;
        ActionEditorChangeFlags queuedFlags = ConsumeQueuedBindingChange(queuedAction);
        if (queuedFlags == ActionEditorChangeFlags.None)
            return;
        if (queuedAction != _currentAction || queuedSessionVersion != _sessionVersion)
            return;
        ApplyAssetChange(queuedAction, queuedFlags, ActionEditorChangeOrigin.Binding);
    }

    internal void CompleteIdentityRepair(ActionAsset asset)
    {
        if (_currentAction != asset)
            return;

        ApplyAssetChange(asset, ActionEditorChangeFlags.Structure,
            ActionEditorChangeOrigin.Command, Array.Empty<ActionSelectionValue>());
    }

    internal void RefreshExternal(ActionEditorChangeOrigin origin)
    {
        if (_currentAction == null)
        {
            if (!ReferenceEquals(_currentAction, null))
                SetAction(null);
            return;
        }
        ApplyAssetChange(_currentAction, ActionEditorChangeFlags.None, origin);
    }

    internal void NotifyPlaybackChanged() =>
        Publish(ActionEditorChangeFlags.Playback, ActionEditorChangeOrigin.Playback);

    private void CancelQueuedBindingChange()
    {
        _bindingChangeGeneration++;
        _bindingChangeQueued = false;
        _queuedBindingAction = null;
        _queuedBindingFlags = ActionEditorChangeFlags.None;
    }

    private ActionEditorChangeFlags ConsumeQueuedBindingChange(ActionAsset asset)
    {
        if (!_bindingChangeQueued || asset == null || asset != _queuedBindingAction ||
            _queuedBindingSessionVersion != _sessionVersion)
            return ActionEditorChangeFlags.None;
        ActionEditorChangeFlags flags = _queuedBindingFlags;
        CancelQueuedBindingChange();
        return flags;
    }

    private void RebuildDocument()
    {
        RebuildDocument(ActionEditorObservedState.Capture(_currentAction, _previewCharacter));
    }

    private void RebuildDocument(ActionEditorObservedState observedState)
    {
        _document = ActionEditorDocument.Build(_currentAction);
        _documentVersion++;
        _observedState = observedState;
    }

    private void Publish(ActionEditorChangeFlags flags, ActionEditorChangeOrigin origin)
    {
        if (flags == ActionEditorChangeFlags.None) return;
        Changed?.Invoke(new ActionEditorChange(_currentAction, flags, _documentVersion, origin));
    }
}

/// <summary>Funnels Unity-side asset changes into the shared Action editor session.</summary>
[InitializeOnLoad]
internal static class ActionEditorEventAdapter
{
    private static bool _queued;
    private static int _generation;
    private static ActionEditorChangeOrigin _origin;
    private static ActionAsset _queuedAction;
    private static int _queuedSessionVersion;

    static ActionEditorEventAdapter()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.projectChanged += OnProjectChanged;
        ObjectChangeEvents.changesPublished += OnChangesPublished;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
    }

    private static void OnUndoRedo() => Queue(ActionEditorChangeOrigin.UndoRedo);
    private static void OnProjectChanged() => Queue(ActionEditorChangeOrigin.Project);
    private static void OnChangesPublished(ref ObjectChangeEventStream stream) =>
        Queue(ActionEditorChangeOrigin.ObjectChange);

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            ActionEditorPlayback.Stop();
    }

    private static void Queue(ActionEditorChangeOrigin origin)
    {
        if (origin == ActionEditorChangeOrigin.UndoRedo || !_queued)
            _origin = origin;
        _queuedAction = ActionEditorContext.Shared.CurrentAction;
        _queuedSessionVersion = ActionEditorContext.Shared.SessionVersion;
        if (_queued) return;
        _queued = true;
        int generation = _generation;
        EditorApplication.delayCall += () =>
        {
            if (generation != _generation) return;
            _queued = false;
            if (_queuedAction != ActionEditorContext.Shared.CurrentAction ||
                _queuedSessionVersion != ActionEditorContext.Shared.SessionVersion)
                return;
            ActionEditorContext.Shared.RefreshExternal(_origin);
        };
    }

    private static void BeforeAssemblyReload()
    {
        _generation++;
        _queued = false;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.projectChanged -= OnProjectChanged;
        ObjectChangeEvents.changesPublished -= OnChangesPublished;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
    }
}
#endif
