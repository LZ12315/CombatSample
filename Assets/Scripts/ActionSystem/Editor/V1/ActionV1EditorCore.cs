#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[Flags]
internal enum ActionV1EditorChangeFlags
{
    None = 0,
    Context = 1 << 0,
    Selection = 1 << 1,
    Frame = 1 << 2,
    Structure = 1 << 3,
    Timing = 1 << 4,
    Content = 1 << 5,
    Validation = 1 << 6,
    Preview = 1 << 7,
    Presentation = 1 << 8,
    All = ~0,
}

internal enum ActionV1SelectionKind
{
    None,
    Action,
    AnimationSegment,
    GameplayLane,
    GameplayItem,
}

internal enum ActionV1EditorReadiness
{
    NoAction,
    IdentityBlocked,
    EditableWithIssues,
    Ready,
}

internal enum ActionV1EditorIdentityState
{
    NotApplicable,
    Valid,
    Missing,
    Malformed,
    Duplicate,
}

internal enum ActionV1ValidationSeverity
{
    IdentityBlock,
    Error,
    NeedsSetup,
}

[Flags]
internal enum ActionV1EntryDisplayState
{
    Normal = 0,
    NullEntry = 1 << 0,
    NegativeStart = 1 << 1,
    InvalidDuration = 1 << 2,
    MissingSource = 1 << 3,
    InvalidTiming = 1 << 4,
    GeometryOverflow = 1 << 5,
}

[Serializable]
internal struct ActionV1SelectionValue
{
    [SerializeField] private ActionV1SelectionKind _kind;
    [SerializeField] private string _editorId;

    public ActionV1SelectionValue(ActionV1SelectionKind kind, string editorId)
    {
        _kind = kind;
        _editorId = editorId ?? string.Empty;
    }

    public ActionV1SelectionKind Kind => _kind;
    public string EditorId => _editorId;
}

/// <summary>Shared, session-only state for the three Action V1 authoring windows.</summary>
internal sealed class ActionV1EditorContext : ScriptableSingleton<ActionV1EditorContext>
{
    [SerializeField] private ActionAsset _currentAction;
    [SerializeField] private int _currentFrame;
    [SerializeField] private ActionV1SelectionValue _primarySelection;
    [SerializeField] private List<ActionV1SelectionValue> _selectedEntries = new List<ActionV1SelectionValue>();
    [SerializeField] private GameObject _previewCharacter;
    [SerializeField] private Vector3 _previewTarget = new Vector3(0f, 0f, 2f);
    [SerializeField] private Vector3 _previewDirection = Vector3.forward;
    [SerializeField] private bool _previewLoop;

    internal static event Action<ActionV1EditorChangeFlags> Changed;

    internal static ActionV1EditorContext Shared => instance;
    internal ActionAsset CurrentAction => _currentAction;
    internal int CurrentFrame => _currentFrame;
    internal ActionV1SelectionValue PrimarySelection => _primarySelection;
    private List<ActionV1SelectionValue> SelectionEntries =>
        _selectedEntries ?? (_selectedEntries = new List<ActionV1SelectionValue>());

    internal IReadOnlyList<string> SelectedIds => SelectionEntries.Select(value => value.EditorId).ToArray();
    internal GameObject PreviewCharacter => _previewCharacter;
    internal Vector3 PreviewTarget => _previewTarget;
    internal Vector3 PreviewDirection => _previewDirection.sqrMagnitude > 0.000001f ? _previewDirection.normalized : Vector3.forward;
    internal bool PreviewLoop => _previewLoop;

    internal void SetAction(ActionAsset action)
    {
        if (_currentAction == action)
            return;

        _currentAction = action;
        _currentFrame = 0;
        _primarySelection = action != null
            ? new ActionV1SelectionValue(ActionV1SelectionKind.Action, string.Empty)
            : default;
        SelectionEntries.Clear();
        Raise(ActionV1EditorChangeFlags.Context | ActionV1EditorChangeFlags.Selection |
              ActionV1EditorChangeFlags.Frame | ActionV1EditorChangeFlags.Validation |
              ActionV1EditorChangeFlags.Preview);
    }

    internal void SetFrame(int frame)
    {
        int duration = _currentAction != null && _currentAction.Timeline != null ? _currentAction.Timeline.DurationFrames : 1;
        int last = Mathf.Max(0, duration - 1);
        int clamped = Mathf.Clamp(frame, 0, last);
        if (_currentFrame == clamped)
            return;
        _currentFrame = clamped;
        Raise(ActionV1EditorChangeFlags.Frame | ActionV1EditorChangeFlags.Preview);
    }

    internal void SelectAction()
    {
        _primarySelection = new ActionV1SelectionValue(ActionV1SelectionKind.Action, string.Empty);
        SelectionEntries.Clear();
        Raise(ActionV1EditorChangeFlags.Selection);
    }

    internal void Select(ActionV1SelectionKind kind, string editorId, bool additive, bool toggle)
    {
        if (kind == ActionV1SelectionKind.Action || string.IsNullOrEmpty(editorId))
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
                _primarySelection = new ActionV1SelectionValue(ActionV1SelectionKind.Action, string.Empty);
            else if (removedPrimary)
                _primarySelection = SelectionEntries[SelectionEntries.Count - 1];
        }
        else
        {
            if (existing < 0)
                SelectionEntries.Add(new ActionV1SelectionValue(kind, editorId));
            else
                SelectionEntries[existing] = new ActionV1SelectionValue(kind, editorId);
            _primarySelection = SelectionEntries[existing < 0 ? SelectionEntries.Count - 1 : existing];
        }

        Raise(ActionV1EditorChangeFlags.Selection);
    }

    internal void SetSelection(IEnumerable<ActionV1DocumentEntry> entries, bool additive)
    {
        List<ActionV1DocumentEntry> candidates = (entries ?? Enumerable.Empty<ActionV1DocumentEntry>())
            .Where(entry => entry != null && entry.HasStableSelection)
            .ToList();
        if (additive && candidates.Count == 0)
            return;

        if (!additive)
            SelectionEntries.Clear();

        ActionV1DocumentEntry last = null;
        foreach (ActionV1DocumentEntry entry in candidates)
        {
            int existing = SelectionEntries.FindIndex(value => string.Equals(value.EditorId, entry.EditorId, StringComparison.Ordinal));
            var value = new ActionV1SelectionValue(entry.SelectionKind, entry.EditorId);
            if (existing < 0)
                SelectionEntries.Add(value);
            else
                SelectionEntries[existing] = value;
            last = entry;
        }

        _primarySelection = last != null
            ? new ActionV1SelectionValue(last.SelectionKind, last.EditorId)
            : new ActionV1SelectionValue(ActionV1SelectionKind.Action, string.Empty);
        Raise(ActionV1EditorChangeFlags.Selection);
    }

    internal bool IsSelected(string editorId) => !string.IsNullOrEmpty(editorId) &&
        SelectionEntries.Any(value => string.Equals(value.EditorId, editorId, StringComparison.Ordinal));

    internal void ValidateSelection(ActionV1EditorDocument document)
    {
        if (_currentAction == null)
        {
            SelectionEntries.Clear();
            _primarySelection = default;
            return;
        }

        SelectionEntries.RemoveAll(value => document == null || !document.ById.ContainsKey(value.EditorId));
        for (int i = 0; i < SelectionEntries.Count; i++)
        {
            ActionV1DocumentEntry entry = document.ById[SelectionEntries[i].EditorId];
            SelectionEntries[i] = new ActionV1SelectionValue(entry.SelectionKind, entry.EditorId);
        }
        bool primaryIsSelected = _primarySelection.Kind != ActionV1SelectionKind.Action &&
                                 SelectionEntries.Any(value => string.Equals(
                                     value.EditorId,
                                     _primarySelection.EditorId,
                                     StringComparison.Ordinal));
        if (SelectionEntries.Count == 0)
        {
            _primarySelection = new ActionV1SelectionValue(ActionV1SelectionKind.Action, string.Empty);
        }
        else if (!primaryIsSelected)
        {
            _primarySelection = SelectionEntries[SelectionEntries.Count - 1];
        }
        else
        {
            ActionV1DocumentEntry primary = document.ById[_primarySelection.EditorId];
            _primarySelection = new ActionV1SelectionValue(primary.SelectionKind, primary.EditorId);
        }
        SetFrame(_currentFrame);
    }

    internal void SetPreviewCharacter(GameObject value)
    {
        if (_previewCharacter == value)
            return;
        _previewCharacter = value;
        Raise(ActionV1EditorChangeFlags.Preview);
    }

    internal void SetPreviewTarget(Vector3 value)
    {
        if (_previewTarget == value)
            return;
        _previewTarget = value;
        Raise(ActionV1EditorChangeFlags.Preview);
    }

    internal void SetPreviewDirection(Vector3 value)
    {
        if (value.sqrMagnitude <= 0.000001f)
            value = Vector3.forward;
        value.Normalize();
        if (_previewDirection == value)
            return;
        _previewDirection = value;
        Raise(ActionV1EditorChangeFlags.Preview);
    }

    internal void ResetPreviewInputs()
    {
        _previewTarget = new Vector3(0f, 0f, 2f);
        _previewDirection = Vector3.forward;
        Raise(ActionV1EditorChangeFlags.Preview);
    }

    internal void SetPreviewLoop(bool value)
    {
        if (_previewLoop == value)
            return;
        _previewLoop = value;
        Raise(ActionV1EditorChangeFlags.Preview);
    }

    internal void NotifyAssetChanged(ActionV1EditorChangeFlags flags)
    {
        int duration = _currentAction != null && _currentAction.Timeline != null ? _currentAction.Timeline.DurationFrames : 1;
        int clamped = Mathf.Clamp(_currentFrame, 0, Mathf.Max(0, duration - 1));
        if (_currentFrame != clamped)
        {
            _currentFrame = clamped;
            flags |= ActionV1EditorChangeFlags.Frame;
        }
        Raise(flags | ActionV1EditorChangeFlags.Validation | ActionV1EditorChangeFlags.Preview);
    }

    internal void CompleteIdentityRepair(ActionAsset asset)
    {
        if (_currentAction != asset)
            return;

        SelectionEntries.Clear();
        _primarySelection = new ActionV1SelectionValue(ActionV1SelectionKind.Action, string.Empty);
        int duration = asset != null && asset.Timeline != null ? asset.Timeline.DurationFrames : 1;
        _currentFrame = Mathf.Clamp(_currentFrame, 0, Mathf.Max(0, duration - 1));
        Raise(ActionV1EditorChangeFlags.Selection | ActionV1EditorChangeFlags.Structure |
              ActionV1EditorChangeFlags.Validation | ActionV1EditorChangeFlags.Preview);
    }

    private static void Raise(ActionV1EditorChangeFlags flags) => Changed?.Invoke(flags);
}

internal sealed class ActionV1DocumentEntry
{
    internal ActionV1SelectionKind SelectionKind;
    internal string EditorId;
    internal string DisplayKey;
    internal string AuthoringPath;
    internal string LaneId;
    internal string LaneName;
    internal int LaneIndex;
    internal int ItemIndex;
    internal int StartFrame;
    internal int EndFrame;
    internal long RawEndFrameExclusive;
    internal int SafeDisplayStart;
    internal int SafeDisplayDuration;
    internal bool Muted;
    internal ActionV1EditorIdentityState IdentityState;
    internal ActionV1EntryDisplayState DisplayState;
    internal object Source;

    internal bool HasStableSelection => IdentityState == ActionV1EditorIdentityState.Valid && !string.IsNullOrEmpty(EditorId);

    internal string DisplayName
    {
        get
        {
            if (Source == null)
                return SelectionKind == ActionV1SelectionKind.AnimationSegment ? "Null Animation Segment" :
                    SelectionKind == ActionV1SelectionKind.GameplayLane ? "Null Gameplay Lane" : "Null Gameplay Item";
            if (Source is AnimationSegment animation)
                return animation.AnimationAsset != null ? animation.AnimationAsset.name : "Missing Animation Asset";
            if (Source is GameplayLane lane)
                return string.IsNullOrWhiteSpace(lane.Name) ? "Unnamed Lane" : lane.Name;
            return Source.GetType().Name.Replace("Item", string.Empty);
        }
    }
}

internal sealed class ActionV1ValidationIndex
{
    internal readonly List<ActionAuthoringValidationIssue> GlobalIssues = new List<ActionAuthoringValidationIssue>();
    internal readonly List<ActionAuthoringValidationIssue> IdentityIssues = new List<ActionAuthoringValidationIssue>();
    internal readonly Dictionary<string, List<ActionAuthoringValidationIssue>> IssuesByEditorId =
        new Dictionary<string, List<ActionAuthoringValidationIssue>>(StringComparer.Ordinal);
    internal readonly Dictionary<string, List<ActionAuthoringValidationIssue>> IssuesByAuthoringPath =
        new Dictionary<string, List<ActionAuthoringValidationIssue>>(StringComparer.Ordinal);
    internal int BlockingIdentityCount => IdentityIssues.Count;
    internal int ErrorCount { get; private set; }
    internal int NeedsSetupCount { get; private set; }
    internal int MissingIdentityCount { get; private set; }
    internal int MalformedIdentityCount { get; private set; }
    internal int DuplicateIdentityCount { get; private set; }

    internal IReadOnlyList<ActionAuthoringValidationIssue> ForEntry(ActionV1DocumentEntry entry)
    {
        if (entry == null)
            return Array.Empty<ActionAuthoringValidationIssue>();
        if (IssuesByAuthoringPath.TryGetValue(entry.AuthoringPath, out List<ActionAuthoringValidationIssue> pathIssues))
            return pathIssues;
        if (string.IsNullOrEmpty(entry.EditorId))
            return Array.Empty<ActionAuthoringValidationIssue>();
        return IssuesByEditorId.TryGetValue(entry.EditorId, out List<ActionAuthoringValidationIssue> issues)
            ? issues
            : (IReadOnlyList<ActionAuthoringValidationIssue>)Array.Empty<ActionAuthoringValidationIssue>();
    }

    internal IReadOnlyList<string> PathsFor(ActionAuthoringValidationIssue issue) =>
        issue != null && !string.IsNullOrEmpty(issue.AuthoringPath)
            ? new[] { issue.AuthoringPath }
            : (IReadOnlyList<string>)Array.Empty<string>();

    internal static ActionV1ValidationIndex Build(ActionAuthoringValidationResult result)
    {
        var index = new ActionV1ValidationIndex();
        if (result == null)
            return index;

        foreach (ActionAuthoringValidationIssue issue in result.Issues)
        {
            ActionV1ValidationSeverity severity = SeverityOf(issue.Code);
            if (severity == ActionV1ValidationSeverity.IdentityBlock)
            {
                index.IdentityIssues.Add(issue);
                if (issue.Code == ActionAuthoringValidationCode.MissingEditorId) index.MissingIdentityCount++;
                else if (issue.Code == ActionAuthoringValidationCode.MalformedEditorId) index.MalformedIdentityCount++;
                else if (issue.Code == ActionAuthoringValidationCode.DuplicateEditorId) index.DuplicateIdentityCount++;
            }
            else if (severity == ActionV1ValidationSeverity.NeedsSetup)
            {
                index.NeedsSetupCount++;
            }
            else
            {
                index.ErrorCount++;
            }

            if (!string.IsNullOrEmpty(issue.AuthoringPath))
                index.AddPathIssue(issue.AuthoringPath, issue);

            if (string.IsNullOrEmpty(issue.EditorId))
            {
                if (string.IsNullOrEmpty(issue.AuthoringPath))
                    index.GlobalIssues.Add(issue);
                continue;
            }

            if (!index.IssuesByEditorId.TryGetValue(issue.EditorId, out List<ActionAuthoringValidationIssue> list))
            {
                list = new List<ActionAuthoringValidationIssue>();
                index.IssuesByEditorId.Add(issue.EditorId, list);
            }
            list.Add(issue);
        }
        return index;
    }

    private void AddPathIssue(string path, ActionAuthoringValidationIssue issue)
    {
        if (string.IsNullOrEmpty(path) || issue == null)
            return;
        if (!IssuesByAuthoringPath.TryGetValue(path, out List<ActionAuthoringValidationIssue> issues))
        {
            issues = new List<ActionAuthoringValidationIssue>();
            IssuesByAuthoringPath.Add(path, issues);
        }
        if (!issues.Contains(issue)) issues.Add(issue);
    }

    internal static bool IsIdentity(ActionAuthoringValidationCode code) =>
        code == ActionAuthoringValidationCode.MissingEditorId ||
        code == ActionAuthoringValidationCode.MalformedEditorId ||
        code == ActionAuthoringValidationCode.DuplicateEditorId;

    internal static ActionV1ValidationSeverity SeverityOf(ActionAuthoringValidationCode code)
    {
        if (IsIdentity(code))
            return ActionV1ValidationSeverity.IdentityBlock;
        return code == ActionAuthoringValidationCode.InvalidConfig
            ? ActionV1ValidationSeverity.NeedsSetup
            : ActionV1ValidationSeverity.Error;
    }
}

internal sealed class ActionV1OverlapRegion
{
    internal bool IsAnimation;
    internal int LaneIndex;
    internal long StartFrame;
    internal long EndFrameExclusive;
    internal int PeakOverlapCount;
    internal readonly List<ActionV1DocumentEntry> Entries = new List<ActionV1DocumentEntry>();
}

/// <summary>Read-only render snapshot. The ActionAsset remains the only persisted authority.</summary>
internal sealed class ActionV1EditorDocument
{
    private static ActionAsset s_validationAsset;
    private static int s_validationDirtyCount = -1;
    private static int s_validationDependencySignature;
    private static ActionAuthoringValidationResult s_validation;

    internal ActionAsset Asset;
    internal int DurationFrames;
    internal int HorizonFrames;
    internal bool HasTimeline;
    internal ActionV1EditorReadiness Readiness;
    internal ActionAuthoringValidationResult Validation;
    internal ActionV1ValidationIndex ValidationIndex;
    internal readonly List<ActionV1DocumentEntry> AnimationSegments = new List<ActionV1DocumentEntry>();
    internal readonly List<ActionV1DocumentEntry> Lanes = new List<ActionV1DocumentEntry>();
    internal readonly List<ActionV1DocumentEntry> GameplayItems = new List<ActionV1DocumentEntry>();
    internal readonly List<ActionV1OverlapRegion> OverlapRegions = new List<ActionV1OverlapRegion>();
    internal readonly Dictionary<string, ActionV1DocumentEntry> ById = new Dictionary<string, ActionV1DocumentEntry>(StringComparer.Ordinal);

    internal static ActionV1EditorDocument Build(ActionAsset asset, bool validate = true)
    {
        var document = new ActionV1EditorDocument
        {
            Asset = asset,
            DurationFrames = 1,
            HorizonFrames = 60,
            Readiness = asset == null ? ActionV1EditorReadiness.NoAction : ActionV1EditorReadiness.Ready,
        };
        document.Validation = asset != null && validate ? GetSharedValidation(asset) : null;
        document.ValidationIndex = ActionV1ValidationIndex.Build(document.Validation);
        ActionTimelineData timeline = asset != null ? asset.Timeline : null;
        document.HasTimeline = timeline != null;
        if (timeline == null)
        {
            if (asset != null)
                document.Readiness = ActionV1EditorReadiness.EditableWithIssues;
            return document;
        }

        document.DurationFrames = Mathf.Max(1, timeline.DurationFrames);
        IReadOnlyList<AnimationSegment> segments = timeline.AnimationSegments;
        for (int i = 0; segments != null && i < segments.Count; i++)
        {
            AnimationSegment segment = segments[i];
            int start = segment != null ? segment.StartFrame : 0;
            int duration = segment != null ? segment.DerivedDurationFrames : 0;
            var entry = new ActionV1DocumentEntry
            {
                SelectionKind = ActionV1SelectionKind.AnimationSegment,
                EditorId = segment != null ? segment.EditorId : string.Empty,
                DisplayKey = $"animation:{i}",
                AuthoringPath = $"AnimationSegments[{i}]",
                LaneIndex = -1,
                ItemIndex = i,
                StartFrame = start,
                EndFrame = SaturatingEnd(start, Mathf.Max(1, duration)),
                RawEndFrameExclusive = (long)start + duration,
                SafeDisplayStart = Mathf.Max(0, start),
                SafeDisplayDuration = Mathf.Max(1, duration),
                IdentityState = segment == null ? ActionV1EditorIdentityState.NotApplicable : ClassifyIdentity(segment.EditorId),
                Source = segment,
            };
            entry.DisplayState = ClassifyDisplayState(entry, document.ValidationIndex);
            document.AnimationSegments.Add(entry);
        }

        IReadOnlyList<GameplayLane> lanes = timeline.GameplayLanes;
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            GameplayLane lane = lanes[laneIndex];
            var laneEntry = new ActionV1DocumentEntry
            {
                SelectionKind = ActionV1SelectionKind.GameplayLane,
                EditorId = lane != null ? lane.EditorId : string.Empty,
                DisplayKey = $"lane:{laneIndex}",
                AuthoringPath = $"GameplayLanes[{laneIndex}]",
                LaneId = lane != null ? lane.EditorId : string.Empty,
                LaneName = lane != null ? lane.Name : "Null Lane",
                LaneIndex = laneIndex,
                ItemIndex = -1,
                Muted = lane != null && lane.Muted,
                IdentityState = lane == null ? ActionV1EditorIdentityState.NotApplicable : ClassifyIdentity(lane.EditorId),
                Source = lane,
            };
            laneEntry.DisplayState = ClassifyDisplayState(laneEntry, document.ValidationIndex);
            document.Lanes.Add(laneEntry);

            IReadOnlyList<GameplayItem> items = lane != null ? lane.Items : null;
            for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
            {
                GameplayItem item = items[itemIndex];
                int start = item is PointGameplayItem point ? point.Frame : item is RangeGameplayItem range ? range.StartFrame : 0;
                int duration = item is PointGameplayItem ? 1 : item is RangeGameplayItem ranged ? ranged.DurationFrames : 0;
                var itemEntry = new ActionV1DocumentEntry
                {
                    SelectionKind = ActionV1SelectionKind.GameplayItem,
                    EditorId = item != null ? item.EditorId : string.Empty,
                    DisplayKey = $"gameplay:{laneIndex}:{itemIndex}",
                    AuthoringPath = $"GameplayLanes[{laneIndex}].Items[{itemIndex}]",
                    LaneId = lane != null ? lane.EditorId : string.Empty,
                    LaneName = lane != null ? lane.Name : "Null Lane",
                    LaneIndex = laneIndex,
                    ItemIndex = itemIndex,
                    StartFrame = start,
                    EndFrame = SaturatingEnd(start, Mathf.Max(1, duration)),
                    RawEndFrameExclusive = (long)start + duration,
                    SafeDisplayStart = Mathf.Max(0, start),
                    SafeDisplayDuration = Mathf.Max(1, duration),
                    Muted = (lane != null && lane.Muted) || (item != null && item.Muted),
                    IdentityState = item == null ? ActionV1EditorIdentityState.NotApplicable : ClassifyIdentity(item.EditorId),
                    Source = item,
                };
                itemEntry.DisplayState = ClassifyDisplayState(itemEntry, document.ValidationIndex);
                document.GameplayItems.Add(itemEntry);
            }
        }

        document.FinalizeIdentity();
        document.BuildOverlapIndex();
        document.HorizonFrames = Mathf.Max(60, SaturatingEnd(document.DurationFrames, 30));
        bool identityBlocked = document.AllEntries().Any(entry =>
            entry.IdentityState == ActionV1EditorIdentityState.Missing ||
            entry.IdentityState == ActionV1EditorIdentityState.Malformed ||
            entry.IdentityState == ActionV1EditorIdentityState.Duplicate);
        document.Readiness = identityBlocked
            ? ActionV1EditorReadiness.IdentityBlocked
            : validate && document.Validation != null && !document.Validation.IsValid
                ? ActionV1EditorReadiness.EditableWithIssues
                : ActionV1EditorReadiness.Ready;
        return document;
    }

    private static ActionAuthoringValidationResult GetSharedValidation(ActionAsset asset)
    {
        int dirtyCount = EditorUtility.GetDirtyCount(asset);
        int dependencySignature = CalculateValidationDependencySignature(asset);
        if (ReferenceEquals(asset, s_validationAsset) && dirtyCount == s_validationDirtyCount &&
            dependencySignature == s_validationDependencySignature && s_validation != null)
            return s_validation;
        s_validationAsset = asset;
        s_validationDirtyCount = dirtyCount;
        s_validationDependencySignature = dependencySignature;
        s_validation = ActionAuthoringValidator.Validate(asset);
        return s_validation;
    }

    private static int CalculateValidationDependencySignature(ActionAsset asset)
    {
        unchecked
        {
            int hash = 17;
            void Add(AnimationAsset animation)
            {
                hash = hash * 31 + (animation != null ? animation.GetInstanceID() : 0);
                hash = hash * 31 + (animation != null ? EditorUtility.GetDirtyCount(animation) : 0);
                string animationPath = animation != null ? AssetDatabase.GetAssetPath(animation) : string.Empty;
                hash = hash * 31 + (!string.IsNullOrEmpty(animationPath)
                    ? AssetDatabase.GetAssetDependencyHash(animationPath).GetHashCode()
                    : 0);
                AnimationClip clip = animation != null ? animation.Clip : null;
                hash = hash * 31 + (clip != null ? clip.GetInstanceID() : 0);
                hash = hash * 31 + (clip != null ? EditorUtility.GetDirtyCount(clip) : 0);
                string clipPath = clip != null ? AssetDatabase.GetAssetPath(clip) : string.Empty;
                hash = hash * 31 + (!string.IsNullOrEmpty(clipPath)
                    ? AssetDatabase.GetAssetDependencyHash(clipPath).GetHashCode()
                    : 0);
            }

            ActionTimelineData timeline = asset != null ? asset.Timeline : null;
            if (timeline == null)
                return hash;
            foreach (AnimationSegment segment in timeline.AnimationSegments)
                Add(segment != null ? segment.AnimationAsset : null);
            foreach (GameplayLane lane in timeline.GameplayLanes)
            foreach (GameplayItem item in lane?.Items ?? Array.Empty<GameplayItem>())
            {
                if (item is RootMotionItem rootMotion) Add(rootMotion.Config?.animationAsset);
                else if (item is SelfRotationItem rotation) Add(rotation.Config?.animationAsset);
            }
            return hash;
        }
    }

    internal IEnumerable<ActionV1DocumentEntry> ContentEntries => AnimationSegments.Concat(GameplayItems);

    internal int OverlappingEntryCount(bool animation, int laneIndex)
    {
        return OverlapRegions
            .Where(region => region.IsAnimation == animation && region.LaneIndex == laneIndex)
            .SelectMany(region => region.Entries)
            .Select(entry => entry.DisplayKey)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private void FinalizeIdentity()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ActionV1DocumentEntry entry in AllEntries())
        {
            if (entry.IdentityState != ActionV1EditorIdentityState.Valid)
                continue;
            counts.TryGetValue(entry.EditorId, out int count);
            counts[entry.EditorId] = count + 1;
        }

        foreach (ActionV1DocumentEntry entry in AllEntries())
        {
            if (entry.IdentityState == ActionV1EditorIdentityState.Valid && counts[entry.EditorId] > 1)
                entry.IdentityState = ActionV1EditorIdentityState.Duplicate;
            if (entry.IdentityState == ActionV1EditorIdentityState.Valid)
                ById.Add(entry.EditorId, entry);
        }
    }

    private IEnumerable<ActionV1DocumentEntry> AllEntries() => AnimationSegments.Concat(Lanes).Concat(GameplayItems);

    internal IEnumerable<ActionV1DocumentEntry> EntriesInAuthoringOrder()
    {
        foreach (ActionV1DocumentEntry animation in AnimationSegments)
            yield return animation;
        foreach (ActionV1DocumentEntry lane in Lanes.OrderBy(entry => entry.LaneIndex))
        {
            yield return lane;
            foreach (ActionV1DocumentEntry item in GameplayItems.Where(entry => entry.LaneIndex == lane.LaneIndex).OrderBy(entry => entry.ItemIndex))
                yield return item;
        }
    }

    private void BuildOverlapIndex()
    {
        AddOverlapRegions(AnimationSegments.Where(IsRealInterval).ToList(), true, -1);
        foreach (ActionV1DocumentEntry lane in Lanes)
        {
            int laneIndex = lane.LaneIndex;
            AddOverlapRegions(GameplayItems.Where(item => item.LaneIndex == laneIndex && IsRealInterval(item)).ToList(), false, laneIndex);
        }
    }

    private void AddOverlapRegions(List<ActionV1DocumentEntry> entries, bool animation, int laneIndex)
    {
        var boundaries = new SortedSet<long>();
        var starting = new Dictionary<long, List<ActionV1DocumentEntry>>();
        var ending = new Dictionary<long, List<ActionV1DocumentEntry>>();
        foreach (ActionV1DocumentEntry entry in entries)
        {
            long start = entry.StartFrame;
            long end = entry.RawEndFrameExclusive;
            boundaries.Add(start);
            boundaries.Add(end);
            AddOverlapEvent(starting, start, entry);
            AddOverlapEvent(ending, end, entry);
        }

        int firstRegionIndex = OverlapRegions.Count;
        var active = new HashSet<ActionV1DocumentEntry>();
        ActionV1OverlapRegion current = null;
        bool hasPrevious = false;
        long previous = 0L;
        foreach (long boundary in boundaries)
        {
            if (hasPrevious && boundary > previous && active.Count >= 2)
            {
                List<ActionV1DocumentEntry> ordered = active.OrderBy(entry => entry.ItemIndex).ToList();
                if (current != null && current.EndFrameExclusive == previous)
                {
                    current.EndFrameExclusive = boundary;
                    current.PeakOverlapCount = Math.Max(current.PeakOverlapCount, ordered.Count);
                    foreach (ActionV1DocumentEntry entry in ordered)
                        if (!current.Entries.Contains(entry)) current.Entries.Add(entry);
                }
                else
                {
                    current = new ActionV1OverlapRegion
                    {
                        IsAnimation = animation,
                        LaneIndex = laneIndex,
                        StartFrame = previous,
                        EndFrameExclusive = boundary,
                        PeakOverlapCount = ordered.Count,
                    };
                    current.Entries.AddRange(ordered);
                    OverlapRegions.Add(current);
                }
            }
            else if (hasPrevious)
            {
                current = null;
            }

            if (ending.TryGetValue(boundary, out List<ActionV1DocumentEntry> endingHere))
                foreach (ActionV1DocumentEntry entry in endingHere) active.Remove(entry);
            if (starting.TryGetValue(boundary, out List<ActionV1DocumentEntry> startingHere))
                foreach (ActionV1DocumentEntry entry in startingHere) active.Add(entry);
            previous = boundary;
            hasPrevious = true;
        }

        for (int i = firstRegionIndex; i < OverlapRegions.Count; i++)
            OverlapRegions[i].Entries.Sort((left, right) => left.ItemIndex.CompareTo(right.ItemIndex));
    }

    private static void AddOverlapEvent(
        Dictionary<long, List<ActionV1DocumentEntry>> events,
        long frame,
        ActionV1DocumentEntry entry)
    {
        if (!events.TryGetValue(frame, out List<ActionV1DocumentEntry> entries))
        {
            entries = new List<ActionV1DocumentEntry>();
            events.Add(frame, entries);
        }
        entries.Add(entry);
    }

    private static bool IsRealInterval(ActionV1DocumentEntry entry) =>
        entry != null && entry.Source != null && entry.RawEndFrameExclusive > entry.StartFrame;

    private static ActionV1EditorIdentityState ClassifyIdentity(string editorId)
    {
        if (string.IsNullOrEmpty(editorId))
            return ActionV1EditorIdentityState.Missing;
        return ActionAuthoringIdentity.IsValidEditorId(editorId)
            ? ActionV1EditorIdentityState.Valid
            : ActionV1EditorIdentityState.Malformed;
    }

    private static ActionV1EntryDisplayState ClassifyDisplayState(
        ActionV1DocumentEntry entry,
        ActionV1ValidationIndex validationIndex)
    {
        if (entry == null)
            return ActionV1EntryDisplayState.NullEntry;

        ActionV1EntryDisplayState state = ActionV1EntryDisplayState.Normal;
        if (entry.Source == null)
            state |= ActionV1EntryDisplayState.NullEntry;

        bool hasTiming = entry.SelectionKind == ActionV1SelectionKind.AnimationSegment ||
                         entry.SelectionKind == ActionV1SelectionKind.GameplayItem;
        if (hasTiming && entry.StartFrame < 0)
            state |= ActionV1EntryDisplayState.NegativeStart;
        if (hasTiming && entry.RawEndFrameExclusive <= entry.StartFrame)
            state |= ActionV1EntryDisplayState.InvalidDuration;
        if (entry.Source is AnimationSegment segment &&
            (segment.AnimationAsset == null || segment.AnimationAsset.Clip == null))
            state |= ActionV1EntryDisplayState.MissingSource;

        IReadOnlyList<ActionAuthoringValidationIssue> issues = validationIndex?.ForEntry(entry);
        for (int i = 0; issues != null && i < issues.Count; i++)
        {
            ActionAuthoringValidationCode code = issues[i].Code;
            if (code == ActionAuthoringValidationCode.InvalidTiming ||
                code == ActionAuthoringValidationCode.InvalidSourceRange ||
                code == ActionAuthoringValidationCode.InvalidPlayRate)
            {
                state |= ActionV1EntryDisplayState.InvalidTiming;
                break;
            }
        }
        return state;
    }

    private static int SaturatingEnd(int start, int duration)
    {
        long end = (long)start + duration;
        return end >= int.MaxValue ? int.MaxValue : (int)end;
    }
}

internal readonly struct ActionV1VisibleFrameRange
{
    internal ActionV1VisibleFrameRange(int first, int lastExclusive)
    {
        First = first;
        LastExclusive = lastExclusive;
    }

    internal int First { get; }
    internal int LastExclusive { get; }
}

internal readonly struct ActionV1EntryGeometry
{
    internal ActionV1EntryGeometry(float left, float width, bool overflow)
        : this(left, width, 0f, width, overflow)
    {
    }

    internal ActionV1EntryGeometry(float left, float width, float visualLeft, float visualWidth, bool overflow)
    {
        Left = left;
        Width = width;
        VisualLeft = visualLeft;
        VisualWidth = visualWidth;
        Overflow = overflow;
    }

    internal float Left { get; }
    internal float Width { get; }
    internal float VisualLeft { get; }
    internal float VisualWidth { get; }
    internal bool Overflow { get; }
}

/// <summary>
/// Session-only horizontal time state. It owns the visible range; pixel scale is always derived from
/// the current viewport so the timeline never exposes an unpainted horizontal canvas.
/// </summary>
internal sealed class ActionV1TimeViewport
{
    internal const double MaximumPixelsPerFrame = 64d;
    private const double MinimumWorkspaceFrames = 120d;

    internal double VisibleStartFrame { get; private set; }
    internal double VisibleSpanFrames { get; private set; } = 60d;
    internal double VisibleEndFrame => VisibleStartFrame + VisibleSpanFrames;
    internal double WorkspaceEndFrame { get; private set; } = MinimumWorkspaceFrames;
    internal double NavigationEndFrame { get; private set; } = 60d;
    internal double NavigatorDomainEndFrame => Math.Max(NavigationEndFrame, VisibleEndFrame);
    internal float ViewportWidth { get; private set; } = 1f;
    internal double UsableWidth => Math.Max(1d, ViewportWidth - ActionV1TimelineGeometry.HorizontalPresentationInset * 2d);
    internal double PixelsPerFrame => UsableWidth / Math.Max(VisibleSpanFrames, double.Epsilon);
    internal double MinimumVisibleSpan => Math.Max(1d, UsableWidth / MaximumPixelsPerFrame);

    internal void Initialize(int horizonFrames, float viewportWidth, double restoredStart, double restoredSpan,
        double restoredWorkspace, double restoredNavigationEnd, bool restore)
    {
        ViewportWidth = SanitizeWidth(viewportWidth);
        WorkspaceEndFrame = InitialWorkspace(horizonFrames);
        NavigationEndFrame = Math.Max(1d, horizonFrames);
        if (restore && IsFinite(restoredWorkspace))
            WorkspaceEndFrame = Math.Max(WorkspaceEndFrame, restoredWorkspace);
        if (restore && IsFinite(restoredNavigationEnd))
            NavigationEndFrame = Math.Max(NavigationEndFrame, Math.Min(WorkspaceEndFrame, restoredNavigationEnd));

        if (restore && IsFinite(restoredStart) && IsFinite(restoredSpan) && restoredSpan > 0d)
            SetRange(restoredStart, restoredSpan);
        else
            Fit(horizonFrames);
    }

    internal void UpdateWorkspace(int horizonFrames)
    {
        WorkspaceEndFrame = Math.Max(WorkspaceEndFrame, InitialWorkspace(horizonFrames));
        NavigationEndFrame = Math.Max(NavigationEndFrame, Math.Max(1d, horizonFrames));
        SetRange(VisibleStartFrame, VisibleSpanFrames);
    }

    internal void SetViewportWidth(float viewportWidth)
    {
        ViewportWidth = SanitizeWidth(viewportWidth);
        SetRange(VisibleStartFrame, VisibleSpanFrames);
    }

    internal void Fit(double horizonFrames)
    {
        NavigationEndFrame = Math.Max(1d, Math.Min(WorkspaceEndFrame, horizonFrames));
        SetRange(0d, Math.Max(1d, Math.Min(WorkspaceEndFrame, horizonFrames)));
    }

    internal void SetRange(double startFrame, double spanFrames)
    {
        double minimum = MinimumVisibleSpan;
        double span = IsFinite(spanFrames) ? spanFrames : WorkspaceEndFrame;
        VisibleSpanFrames = Math.Max(minimum, Math.Min(WorkspaceEndFrame, span));
        double maximumStart = Math.Max(0d, WorkspaceEndFrame - VisibleSpanFrames);
        double start = IsFinite(startFrame) ? startFrame : 0d;
        VisibleStartFrame = Math.Max(0d, Math.Min(maximumStart, start));
    }

    internal void ZoomAround(double anchorFrame, double anchorRatio, double factor)
    {
        double ratio = Math.Max(0d, Math.Min(1d, IsFinite(anchorRatio) ? anchorRatio : 0.5d));
        double span = VisibleSpanFrames * (IsFinite(factor) ? factor : 1d);
        span = Math.Max(MinimumVisibleSpan, Math.Min(WorkspaceEndFrame, span));
        SetRange(anchorFrame - ratio * span, span);
    }

    internal void PanPixels(double pixels)
    {
        if (!IsFinite(pixels))
            return;
        SetRange(VisibleStartFrame + pixels / Math.Max(PixelsPerFrame, double.Epsilon), VisibleSpanFrames);
        ExtendNavigationForPan();
    }

    internal void ExtendNavigationForPan()
    {
        if (VisibleStartFrame <= 0d || VisibleEndFrame <= NavigationEndFrame)
            return;
        double reserve = Math.Max(30d, VisibleSpanFrames * 0.25d);
        double requested = Math.Max(VisibleEndFrame, NavigationEndFrame + reserve);
        double stepped = Math.Ceiling(requested / 30d) * 30d;
        NavigationEndFrame = Math.Min(WorkspaceEndFrame, Math.Max(NavigationEndFrame, stepped));
    }

    internal double PixelToFrame(double pixel)
    {
        double x = IsFinite(pixel) ? pixel : ActionV1TimelineGeometry.HorizontalPresentationInset;
        return VisibleStartFrame +
               (x - ActionV1TimelineGeometry.HorizontalPresentationInset) / Math.Max(PixelsPerFrame, double.Epsilon);
    }

    private static double InitialWorkspace(int horizonFrames)
    {
        return Math.Max(MinimumWorkspaceFrames, Math.Max(1d, horizonFrames) * 2d);
    }

    private static float SanitizeWidth(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) || value < 1f ? 1f : value;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

/// <summary>Finite presentation geometry for an integer-frame Action timeline.</summary>
internal sealed class ActionV1TimelineGeometry
{
    internal const float MaxPixelsPerFrame = 64f;
    internal const float MaximumLayoutPixels = 4194304f;
    internal const float PointHitWidth = 14f;
    internal const float HorizontalPresentationInset = 8f;
    internal const float InvalidMinimumWidth = 52f;

    internal ActionV1TimelineGeometry(int durationFrames, int horizonFrames, int gameplayLaneCount,
        double visibleStartFrame, double visibleSpanFrames, float viewportWidth)
    {
        DurationFrames = Mathf.Max(1, durationFrames);
        HorizonFrames = Mathf.Max(60, horizonFrames);
        GameplayLaneCount = Mathf.Max(0, gameplayLaneCount);
        VisibleStartFrame = IsFinite(visibleStartFrame) ? Math.Max(0d, visibleStartFrame) : 0d;
        VisibleSpanFrames = IsFinite(visibleSpanFrames) ? Math.Max(1d, visibleSpanFrames) : HorizonFrames;
        ContentWidth = IsFinite(viewportWidth) ? Mathf.Max(1f, viewportWidth) : 1f;
        double usableWidth = Math.Max(1d, ContentWidth - HorizontalPresentationInset * 2d);
        PixelsPerFrame = (float)Math.Min(MaxPixelsPerFrame, usableWidth / VisibleSpanFrames);
        ContentHeight = ClampLayout(
            ActionV1EditorTheme.AnimationLaneHeight + (double)GameplayLaneCount * ActionV1EditorTheme.GameplayLaneHeight,
            1f);
    }

    internal int DurationFrames { get; }
    internal int HorizonFrames { get; }
    internal int GameplayLaneCount { get; }
    internal double VisibleStartFrame { get; }
    internal double VisibleSpanFrames { get; }
    internal double VisibleEndFrame => VisibleStartFrame + VisibleSpanFrames;
    internal float PixelsPerFrame { get; }
    internal float ContentWidth { get; }
    internal float ContentHeight { get; }
    internal bool HasHorizontalOverflow => false;

    internal float FrameToPixel(long frame, out bool overflow)
    {
        return FramePositionToPixel(frame, out overflow);
    }

    internal float FramePositionToPixel(double frame, out bool overflow)
    {
        double raw = HorizontalPresentationInset + (frame - VisibleStartFrame) * PixelsPerFrame;
        overflow = Math.Abs(raw) > MaximumLayoutPixels || double.IsNaN(raw) || double.IsInfinity(raw);
        if (!overflow)
            return (float)raw;
        return frame < VisibleStartFrame ? 0f : ContentWidth;
    }

    internal int PixelToFrame(float pixel)
    {
        if (!IsFinite(pixel))
            return 0;
        double frame = Math.Round(
            VisibleStartFrame + (pixel - HorizontalPresentationInset) / Math.Max(0.000001f, PixelsPerFrame),
            MidpointRounding.AwayFromZero);
        return frame >= int.MaxValue ? int.MaxValue : Mathf.Max(0, (int)frame);
    }

    internal float LaneTop(int laneIndex)
    {
        if (laneIndex < 0)
            return 0f;
        return ClampLayout(
            ActionV1EditorTheme.AnimationLaneHeight + (double)laneIndex * ActionV1EditorTheme.GameplayLaneHeight,
            0f);
    }

    internal ActionV1EntryGeometry EntryRect(ActionV1DocumentEntry entry)
    {
        if (entry == null)
            return new ActionV1EntryGeometry(0f, InvalidMinimumWidth, false);

        float start = FrameToPixel(Math.Max(0, entry.StartFrame), out bool startOverflow);
        if (entry.Source is PointGameplayItem)
        {
            bool pointOverflow = startOverflow;
            float left = pointOverflow
                ? Mathf.Max(0f, ContentWidth - PointHitWidth)
                : start - PointHitWidth * 0.5f;
            return new ActionV1EntryGeometry(left, PointHitWidth, pointOverflow);
        }
        float end = FrameToPixel(Math.Max(0L, entry.RawEndFrameExclusive), out bool endOverflow);
        bool invalid = entry.DisplayState != ActionV1EntryDisplayState.Normal;
        float semanticWidth = Mathf.Max(0f, end - start);
        if (!invalid && !startOverflow && !endOverflow)
        {
            float visualWidth = Mathf.Max(1f, semanticWidth);
            float hitWidth = Mathf.Max(PointHitWidth, visualWidth);
            float hitLeft = start - (hitWidth - visualWidth) * 0.5f;
            return new ActionV1EntryGeometry(hitLeft, hitWidth, start - hitLeft, visualWidth, false);
        }

        float minimum = InvalidMinimumWidth;
        float width = Mathf.Max(minimum, semanticWidth);
        bool overflow = startOverflow || endOverflow || start + width > MaximumLayoutPixels;
        if (overflow)
        {
            width = Mathf.Min(width, InvalidMinimumWidth);
            start = entry.StartFrame < VisibleStartFrame ? 0f : Mathf.Max(0f, ContentWidth - width);
        }
        else
        {
            float clippedStart = Mathf.Max(-minimum, start);
            float clippedEnd = Mathf.Min(ContentWidth + minimum, start + width);
            start = clippedStart;
            width = Mathf.Max(minimum, clippedEnd - clippedStart);
        }
        return new ActionV1EntryGeometry(start, width, overflow);
    }

    internal ActionV1VisibleFrameRange VisibleFrames(int paddingFrames = 2)
    {
        long padding = Mathf.Max(0, paddingFrames);
        long firstValue = (long)SaturatingFloor(VisibleStartFrame) - padding;
        long lastValue = (long)SaturatingCeil(VisibleEndFrame) + padding + 1L;
        int first = (int)Math.Max(0L, Math.Min(int.MaxValue, firstValue));
        int last = (int)Math.Max(first, Math.Min(int.MaxValue, lastValue));
        return new ActionV1VisibleFrameRange(first, last);
    }

    internal float ClampVerticalOffset(float value, float viewportHeight)
    {
        float height = IsFinite(viewportHeight) ? Mathf.Max(0f, viewportHeight) : 0f;
        return Mathf.Clamp(IsFinite(value) ? value : 0f, 0f, Mathf.Max(0f, ContentHeight - height));
    }

    private static float ClampLayout(double value, float minimum)
    {
        if (double.IsNaN(value) || value <= minimum)
            return minimum;
        return value >= MaximumLayoutPixels ? MaximumLayoutPixels : (float)value;
    }

    private static int SaturatingFloor(double value)
    {
        if (value <= 0d || double.IsNaN(value)) return 0;
        if (value >= int.MaxValue) return int.MaxValue;
        return (int)Math.Floor(value);
    }

    private static int SaturatingCeil(double value)
    {
        if (value <= 0d || double.IsNaN(value)) return 0;
        if (value >= int.MaxValue) return int.MaxValue;
        return (int)Math.Ceiling(value);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

internal static class ActionV1EditorTheme
{
    internal const string StylePath = "Assets/Scripts/ActionSystem/Editor/V1/ActionV1EditorStyles.uss";
    internal const float ContextBarHeight = 28f;
    internal const float TransportBarHeight = 30f;
    internal const float StatusBarHeight = 22f;
    internal const float RulerHeight = 24f;
    internal const float AnimationLaneHeight = 36f;
    internal const float GameplayLaneHeight = 32f;
    internal const float DefaultHeaderWidth = 220f;
    internal const float MinHeaderWidth = 180f;
    internal const float MaxHeaderWidth = 320f;

    internal static void Apply(VisualElement root, string windowClass)
    {
        root.AddToClassList("action-v1-root");
        root.AddToClassList(windowClass);
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
        if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            root.styleSheets.Add(styleSheet);
    }

    internal static string ReadinessLabel(ActionV1EditorReadiness readiness)
    {
        switch (readiness)
        {
            case ActionV1EditorReadiness.IdentityBlocked: return "IDENTITY BLOCKED";
            case ActionV1EditorReadiness.EditableWithIssues: return "HAS ISSUES";
            case ActionV1EditorReadiness.Ready: return "READY";
            default: return "NO ACTION";
        }
    }
}

internal static class ActionV1EditorChrome
{
    internal static VisualElement ContextBar(
        ActionAsset action,
        Action<ActionAsset> setAction,
        params (string text, Action clicked)[] navigation)
    {
        var bar = new VisualElement();
        bar.AddToClassList("action-v1-context-bar");
        var field = new ObjectField("Action")
        {
            objectType = typeof(ActionAsset),
            allowSceneObjects = false,
            value = action,
        };
        field.AddToClassList("action-v1-action-field");
        field.RegisterValueChangedCallback(evt => setAction?.Invoke(evt.newValue as ActionAsset));
        bar.Add(field);

        var spacer = new VisualElement();
        spacer.AddToClassList("action-v1-spacer");
        bar.Add(spacer);
        foreach ((string text, Action clicked) item in navigation)
        {
            var button = new Button(item.clicked) { text = item.text };
            button.AddToClassList("action-v1-chrome-button");
            bar.Add(button);
        }
        return bar;
    }

    internal static VisualElement EmptyState(string title, string body)
    {
        var host = new VisualElement();
        host.AddToClassList("action-v1-empty-state");
        var titleLabel = new Label(title);
        titleLabel.AddToClassList("action-v1-empty-title");
        host.Add(titleLabel);
        var bodyLabel = new Label(body);
        bodyLabel.AddToClassList("action-v1-empty-body");
        host.Add(bodyLabel);
        return host;
    }

    internal static Label Pill(string text, string modifier = null)
    {
        var label = new Label(text);
        label.AddToClassList("action-v1-pill");
        if (!string.IsNullOrEmpty(modifier))
            label.AddToClassList(modifier);
        return label;
    }

    internal static void SyncActionField(VisualElement root)
    {
        ObjectField field = root?.Q<ObjectField>(className: "action-v1-action-field");
        if (field != null && field.value != ActionV1EditorContext.Shared.CurrentAction)
            field.SetValueWithoutNotify(ActionV1EditorContext.Shared.CurrentAction);
    }
}

internal enum ActionV1TimelineOperationKind
{
    Move,
    ResizeLeft,
    ResizeRight,
    TrimLeft,
    TrimRight,
    SetPointTiming,
    SetRangeTiming,
    SetAnimationTiming,
}

internal enum ActionV1TimelineOperationState
{
    Allowed,
    Rejected,
    NoChange,
}

internal readonly struct ActionV1NavigationRange
{
    internal readonly double Start;
    internal readonly double Span;

    internal ActionV1NavigationRange(double start, double span)
    {
        Start = start;
        Span = span;
    }
}

/// <summary>Deterministic interaction math shared by production presentation and diagnostic assertions.</summary>
internal static class ActionV1TimelineInteractionMath
{
    internal static ActionV1NavigationRange Pan(double domain, double start, double span, double delta)
    {
        double safeDomain = Math.Max(1d, domain);
        double safeSpan = Math.Max(0d, Math.Min(span, safeDomain));
        return new ActionV1NavigationRange(Math.Max(0d, Math.Min(safeDomain - safeSpan, start + delta)), safeSpan);
    }

    internal static ActionV1NavigationRange ResizeLeft(double domain, double start, double span, double minimumSpan, double delta)
    {
        double safeDomain = Math.Max(1d, domain);
        double minimum = Math.Max(0.0001d, Math.Min(minimumSpan, safeDomain));
        double fixedEnd = Math.Max(minimum, Math.Min(safeDomain, start + span));
        double nextStart = Math.Max(0d, Math.Min(fixedEnd - minimum, start + delta));
        return new ActionV1NavigationRange(nextStart, fixedEnd - nextStart);
    }

    internal static ActionV1NavigationRange ResizeRight(double domain, double start, double span, double minimumSpan, double delta)
    {
        double safeDomain = Math.Max(1d, domain);
        double safeStart = Math.Max(0d, Math.Min(safeDomain, start));
        double minimum = Math.Max(0.0001d, Math.Min(minimumSpan, safeDomain - safeStart));
        double end = Math.Max(safeStart + minimum, Math.Min(safeDomain, start + span + delta));
        return new ActionV1NavigationRange(safeStart, end - safeStart);
    }

    internal static List<List<int>> BoundedGroups(IReadOnlyList<double> sortedPositions, double maximumSpan)
    {
        var groups = new List<List<int>>();
        if (sortedPositions == null || sortedPositions.Count == 0)
            return groups;
        List<int> group = null;
        double first = 0d;
        for (int index = 0; index < sortedPositions.Count; index++)
        {
            double position = sortedPositions[index];
            if (group == null || position - first >= maximumSpan)
            {
                group = new List<int>();
                groups.Add(group);
                first = position;
            }
            group.Add(index);
        }
        return groups;
    }

    internal static bool CanPlaceMarker(Rect candidate, IReadOnlyList<Rect> occupied) =>
        occupied == null || !occupied.Any(rect => RectanglesOverlap(
            candidate.xMin, candidate.yMin, candidate.xMax, candidate.yMax,
            rect.xMin, rect.yMin, rect.xMax, rect.yMax));

    internal static bool RectanglesOverlap(float leftA, float topA, float rightA, float bottomA,
        float leftB, float topB, float rightB, float bottomB) =>
        leftA <= rightB && rightA >= leftB && topA <= bottomB && bottomA >= topB;

    internal static int ChooseHitIndex(IReadOnlyList<bool> visualHits, IReadOnlyList<bool> expandedHits,
        IReadOnlyList<int> layerRanks, IReadOnlyList<int> authoringOrder)
    {
        if (visualHits == null || expandedHits == null || layerRanks == null || authoringOrder == null)
            return -1;
        int count = Math.Min(Math.Min(visualHits.Count, expandedHits.Count),
            Math.Min(layerRanks.Count, authoringOrder.Count));
        bool hasVisual = Enumerable.Range(0, count).Any(index => visualHits[index]);
        int best = -1;
        for (int index = 0; index < count; index++)
        {
            if (hasVisual ? !visualHits[index] : !expandedHits[index])
                continue;
            if (best < 0 || layerRanks[index] > layerRanks[best] ||
                layerRanks[index] == layerRanks[best] && authoringOrder[index] > authoringOrder[best])
                best = index;
        }
        return best;
    }
}

/// <summary>One Editor-session pointer gesture gate shared by Timeline and Details.</summary>
internal static class ActionV1EditorInteractionGate
{
    private static object _owner;
    private static int _pointerId = -1;

    internal static bool IsActive => _owner != null;
    internal static bool IsBlockedFor(object owner) => _owner != null && !ReferenceEquals(_owner, owner);

    internal static bool TryAcquire(object owner, int pointerId)
    {
        if (owner == null || _owner != null)
            return false;
        _owner = owner;
        _pointerId = pointerId;
        return true;
    }

    internal static void Release(object owner)
    {
        if (!ReferenceEquals(_owner, owner))
            return;
        _owner = null;
        _pointerId = -1;
    }
}

internal sealed class ActionV1TimelineOperationCandidate
{
    internal object Source;
    internal int StartFrame;
    internal int DurationFrames;
    internal int LaneIndex;
    internal AnimationAsset AnimationAsset;
    internal AnimationClip Clip;
    internal float ClipLength;
    internal float SourceStartTime;
    internal float SourceEndTime;
    internal float PlayRate;
}

internal sealed class ActionV1TimelineOperationResult
{
    internal ActionV1TimelineOperationState State;
    internal string Message = string.Empty;
    internal readonly List<ActionV1TimelineOperationCandidate> Candidates = new List<ActionV1TimelineOperationCandidate>();

    internal ActionV1TimelineOperationCandidate ForSource(object source) =>
        Candidates.FirstOrDefault(candidate => ReferenceEquals(candidate.Source, source));
}

/// <summary>
/// Frozen source state for one Timeline gesture. Evaluation and commit both consume this object so
/// mouse feedback cannot be generated from different authoring data than the final Undo operation.
/// </summary>
internal sealed class ActionV1TimelineOperationSnapshot
{
    private sealed class Entry
    {
        internal object Source;
        internal string EditorId;
        internal int StartFrame;
        internal int DurationFrames;
        internal int LaneIndex;
        internal bool Muted;
        internal AnimationAsset AnimationAsset;
        internal AnimationClip Clip;
        internal float ClipLength;
        internal float SourceStartTime;
        internal float SourceEndTime;
        internal float PlayRate;
    }

    private readonly List<Entry> _targets = new List<Entry>();
    private readonly List<Entry> _animations = new List<Entry>();
    private readonly List<AnimationSegment> _animationOrder = new List<AnimationSegment>();
    private readonly List<GameplayLane> _lanes = new List<GameplayLane>();
    private readonly List<List<GameplayItem>> _laneItems = new List<List<GameplayItem>>();

    internal ActionAsset Asset { get; private set; }
    internal ActionV1TimelineOperationKind Kind { get; private set; }

    internal static ActionV1TimelineOperationSnapshot Capture(
        ActionV1EditorDocument document, ActionV1TimelineOperationKind kind, IReadOnlyList<string> ids)
    {
        if (document?.Asset?.Timeline == null || ids == null)
            return null;
        var snapshot = new ActionV1TimelineOperationSnapshot
        {
            Asset = document.Asset,
            Kind = kind,
        };
        var requested = new HashSet<string>(ids.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
        snapshot._animationOrder.AddRange(document.Asset.Timeline.AnimationSegments);
        foreach (ActionV1DocumentEntry documentEntry in document.ContentEntries)
        {
            if (documentEntry.Source == null)
                continue;
            Entry entry = CaptureEntry(documentEntry);
            if (documentEntry.Source is AnimationSegment)
                snapshot._animations.Add(entry);
            if (requested.Contains(documentEntry.EditorId))
                snapshot._targets.Add(entry);
        }
        foreach (ActionV1DocumentEntry laneEntry in document.Lanes.OrderBy(entry => entry.LaneIndex))
        {
            GameplayLane lane = laneEntry.Source as GameplayLane;
            snapshot._lanes.Add(lane);
            snapshot._laneItems.Add(lane != null ? lane.Items.ToList() : new List<GameplayItem>());
        }
        return snapshot._targets.Count > 0 ? snapshot : null;
    }

    internal ActionV1TimelineOperationResult Evaluate(int frameDelta, int laneDelta)
    {
        if (_targets.Count == 0)
            return Rejected("The operation target no longer exists.");
        if (frameDelta == 0 && (Kind != ActionV1TimelineOperationKind.Move || laneDelta == 0))
            return new ActionV1TimelineOperationResult { State = ActionV1TimelineOperationState.NoChange };

        switch (Kind)
        {
            case ActionV1TimelineOperationKind.Move:
                return EvaluateMove(frameDelta, laneDelta);
            case ActionV1TimelineOperationKind.ResizeLeft:
            case ActionV1TimelineOperationKind.ResizeRight:
                return EvaluateResize(frameDelta);
            case ActionV1TimelineOperationKind.TrimLeft:
            case ActionV1TimelineOperationKind.TrimRight:
                return EvaluateTrim(frameDelta);
            default:
                return Rejected("Unsupported Timeline operation.");
        }
    }

    /// <summary>Evaluates an absolute Details draft against the same frozen source used by Timeline gestures.</summary>
    internal ActionV1TimelineOperationResult EvaluateAbsolute(int startFrame, int durationFrames,
        AnimationAsset animationAsset = null, float sourceStartTime = 0f, float sourceEndTime = 0f, float playRate = 1f)
    {
        if (_targets.Count != 1)
            return Rejected("Details timing requires exactly one current target.");

        Entry target = _targets[0];
        if (Kind == ActionV1TimelineOperationKind.SetPointTiming)
        {
            if (!(target.Source is PointGameplayItem))
                return Rejected("Point timing requires a PointGameplayItem.");
            if (startFrame < 0)
                return Rejected("Point Frame must be at or after Frame 0.");
            if (startFrame == target.StartFrame)
                return new ActionV1TimelineOperationResult { State = ActionV1TimelineOperationState.NoChange };
            return Allowed(Candidate(target, startFrame, 1, target.LaneIndex));
        }

        if (Kind == ActionV1TimelineOperationKind.SetRangeTiming)
        {
            if (!(target.Source is RangeGameplayItem))
                return Rejected("Range timing requires a RangeGameplayItem.");
            if (startFrame < 0 || durationFrames < 1)
                return Rejected("Range Start must be non-negative and Duration must be at least one Frame.");
            if (startFrame == target.StartFrame && durationFrames == target.DurationFrames)
                return new ActionV1TimelineOperationResult { State = ActionV1TimelineOperationState.NoChange };
            return Allowed(Candidate(target, startFrame, durationFrames, target.LaneIndex));
        }

        if (Kind != ActionV1TimelineOperationKind.SetAnimationTiming || !(target.Source is AnimationSegment))
            return Rejected("Animation timing requires an AnimationSegment.");
        AnimationClip clip = animationAsset != null ? animationAsset.Clip : null;
        float clipLength = clip != null ? clip.length : 0f;
        if (startFrame < 0 || animationAsset == null || clip == null ||
            !ActionAuthoringMath.IsFinite(clipLength) || clipLength <= 0f ||
            !ActionAuthoringMath.IsFinite(sourceStartTime) || !ActionAuthoringMath.IsFinite(sourceEndTime) ||
            !ActionAuthoringMath.IsFinite(playRate) || playRate <= 0f || sourceStartTime < 0f ||
            sourceEndTime <= sourceStartTime || sourceEndTime > clipLength + 0.00001f)
            return Rejected("Animation source must have a valid Clip, increasing in-range Source values, and Play Rate above zero.");
        int derivedDuration = CalculateAnimationDuration(sourceStartTime, sourceEndTime, playRate);
        if (derivedDuration < 1)
            return Rejected("Animation source does not derive a valid Duration.");

        bool unchanged = startFrame == target.StartFrame && animationAsset == target.AnimationAsset &&
                         Mathf.Approximately(sourceStartTime, target.SourceStartTime) &&
                         Mathf.Approximately(sourceEndTime, target.SourceEndTime) &&
                         Mathf.Approximately(playRate, target.PlayRate);
        if (unchanged)
            return new ActionV1TimelineOperationResult { State = ActionV1TimelineOperationState.NoChange };

        ActionV1TimelineOperationCandidate candidate = Candidate(target, startFrame, derivedDuration, target.LaneIndex);
        candidate.AnimationAsset = animationAsset;
        candidate.Clip = clip;
        candidate.ClipLength = clipLength;
        candidate.SourceStartTime = sourceStartTime;
        candidate.SourceEndTime = sourceEndTime;
        candidate.PlayRate = playRate;
        ActionV1TimelineOperationResult result = Allowed(candidate);
        if (ModifiedAnimationsOverlap(result.Candidates))
            return Rejected("Animation timing would overlap another AnimationSegment.");
        return result;
    }

    internal bool MatchesCurrentSource(out string message)
    {
        message = string.Empty;
        ActionTimelineData timeline = Asset != null ? Asset.Timeline : null;
        if (timeline == null || timeline.AnimationSegments.Count != _animationOrder.Count || timeline.GameplayLanes.Count != _lanes.Count)
        {
            message = "Timeline structure changed during the gesture.";
            return false;
        }
        for (int i = 0; i < _animationOrder.Count; i++)
        {
            if (!ReferenceEquals(timeline.AnimationSegments[i], _animationOrder[i]))
            {
                message = "Animation data changed during the gesture.";
                return false;
            }
        }
        foreach (Entry animation in _animations)
        {
            if (!(animation.Source is AnimationSegment current) || !Matches(animation, current))
            {
                message = "Animation data changed during the gesture.";
                return false;
            }
        }
        for (int laneIndex = 0; laneIndex < _lanes.Count; laneIndex++)
        {
            GameplayLane lane = timeline.GameplayLanes[laneIndex];
            if (!ReferenceEquals(lane, _lanes[laneIndex]))
            {
                message = "Gameplay Lane structure changed during the gesture.";
                return false;
            }
            if (lane == null)
                continue;
            if (lane.Items.Count != _laneItems[laneIndex].Count)
            {
                message = "Gameplay Lane structure changed during the gesture.";
                return false;
            }
            for (int itemIndex = 0; itemIndex < lane.Items.Count; itemIndex++)
            {
                if (!ReferenceEquals(lane.Items[itemIndex], _laneItems[laneIndex][itemIndex]))
                {
                    message = "Gameplay Item order changed during the gesture.";
                    return false;
                }
            }
        }
        foreach (Entry target in _targets)
        {
            if (!MatchesTarget(target))
            {
                message = "Timeline target data changed during the gesture.";
                return false;
            }
        }
        return true;
    }

    private ActionV1TimelineOperationResult EvaluateMove(int frameDelta, int laneDelta)
    {
        var result = new ActionV1TimelineOperationResult { State = ActionV1TimelineOperationState.Allowed };
        foreach (Entry target in _targets)
        {
            long start = (long)target.StartFrame + frameDelta;
            if (start < 0L || start > int.MaxValue)
                return Rejected("Move would place content outside the supported Frame range.");
            int laneIndex = target.LaneIndex;
            if (laneDelta != 0)
            {
                if (target.Source is AnimationSegment || laneIndex + laneDelta < 0 || laneIndex + laneDelta >= _lanes.Count ||
                    _lanes[laneIndex + laneDelta] == null)
                    return Rejected("Every moved GameplayItem must have a valid destination Lane.");
                laneIndex += laneDelta;
            }
            result.Candidates.Add(Candidate(target, (int)start, target.DurationFrames, laneIndex));
        }
        if (ModifiedAnimationsOverlap(result.Candidates))
            return Rejected("Every moved AnimationSegment must finish without an overlap.");
        return result;
    }

    private ActionV1TimelineOperationResult EvaluateResize(int frameDelta)
    {
        if (_targets.Count != 1 || !(_targets[0].Source is RangeGameplayItem))
            return Rejected("Range resize requires one RangeGameplayItem.");
        Entry target = _targets[0];
        bool left = Kind == ActionV1TimelineOperationKind.ResizeLeft;
        long start = left ? (long)target.StartFrame + frameDelta : target.StartFrame;
        long duration = left ? (long)target.DurationFrames - frameDelta : (long)target.DurationFrames + frameDelta;
        if (start < 0L || start > int.MaxValue || duration < 1L || duration > int.MaxValue)
            return Rejected("Range must remain at or after Frame 0 and at least one Frame long.");
        var result = new ActionV1TimelineOperationResult { State = ActionV1TimelineOperationState.Allowed };
        result.Candidates.Add(Candidate(target, (int)start, (int)duration, target.LaneIndex));
        return result;
    }

    private ActionV1TimelineOperationResult EvaluateTrim(int frameDelta)
    {
        if (_targets.Count != 1 || !(_targets[0].Source is AnimationSegment))
            return Rejected("Animation trim requires one AnimationSegment.");
        Entry target = _targets[0];
        if (target.AnimationAsset == null || target.Clip == null || !ActionAuthoringMath.IsFinite(target.PlayRate) ||
            target.PlayRate <= 0f || !ActionAuthoringMath.IsFinite(target.SourceStartTime) ||
            !ActionAuthoringMath.IsFinite(target.SourceEndTime))
            return Rejected("AnimationSegment cannot be trimmed until its source data is valid.");
        bool left = Kind == ActionV1TimelineOperationKind.TrimLeft;
        long start = left ? (long)target.StartFrame + frameDelta : target.StartFrame;
        float seconds = frameDelta / (float)ActionTimelineData.FrameRate * target.PlayRate;
        float sourceStart = left ? target.SourceStartTime + seconds : target.SourceStartTime;
        float sourceEnd = left ? target.SourceEndTime : target.SourceEndTime + seconds;
        if (start < 0L || start > int.MaxValue || !ActionAuthoringMath.IsFinite(sourceStart) ||
            !ActionAuthoringMath.IsFinite(sourceEnd) || sourceStart < 0f || sourceEnd <= sourceStart ||
            sourceEnd > target.ClipLength + 0.00001f)
            return Rejected("Trim would create an invalid source range or move before Frame 0.");
        int duration = CalculateAnimationDuration(sourceStart, sourceEnd, target.PlayRate);
        if (duration < 1)
            return Rejected("Trim would create an invalid Animation duration.");
        var result = new ActionV1TimelineOperationResult { State = ActionV1TimelineOperationState.Allowed };
        ActionV1TimelineOperationCandidate candidate = Candidate(target, (int)start, duration, target.LaneIndex);
        candidate.SourceStartTime = sourceStart;
        candidate.SourceEndTime = sourceEnd;
        result.Candidates.Add(candidate);
        if (ModifiedAnimationsOverlap(result.Candidates))
            return Rejected("Trim would overlap another AnimationSegment.");
        return result;
    }

    private bool ModifiedAnimationsOverlap(List<ActionV1TimelineOperationCandidate> candidates)
    {
        var candidateBySource = candidates.Where(candidate => candidate.Source is AnimationSegment)
            .ToDictionary(candidate => candidate.Source, candidate => candidate);
        if (candidateBySource.Count == 0)
            return false;
        for (int left = 0; left < _animations.Count; left++)
        for (int right = left + 1; right < _animations.Count; right++)
        {
            Entry leftEntry = _animations[left];
            Entry rightEntry = _animations[right];
            ActionV1TimelineOperationCandidate leftCandidate = candidateBySource.TryGetValue(leftEntry.Source, out ActionV1TimelineOperationCandidate lc) ? lc : null;
            ActionV1TimelineOperationCandidate rightCandidate = candidateBySource.TryGetValue(rightEntry.Source, out ActionV1TimelineOperationCandidate rc) ? rc : null;
            if (leftCandidate == null && rightCandidate == null)
                continue;
            long leftStart = leftCandidate != null ? leftCandidate.StartFrame : leftEntry.StartFrame;
            long leftEnd = leftStart + (leftCandidate != null ? leftCandidate.DurationFrames : leftEntry.DurationFrames);
            long rightStart = rightCandidate != null ? rightCandidate.StartFrame : rightEntry.StartFrame;
            long rightEnd = rightStart + (rightCandidate != null ? rightCandidate.DurationFrames : rightEntry.DurationFrames);
            if (leftEnd > leftStart && rightEnd > rightStart && leftStart < rightEnd && rightStart < leftEnd)
                return true;
        }
        return false;
    }

    private bool MatchesTarget(Entry target)
    {
        if (target.Source is PointGameplayItem point)
            return point.EditorId == target.EditorId && point.Frame == target.StartFrame;
        if (target.Source is RangeGameplayItem range)
            return range.EditorId == target.EditorId && range.StartFrame == target.StartFrame &&
                   range.DurationFrames == target.DurationFrames;
        return target.Source is AnimationSegment animation && Matches(target, animation);
    }

    private static bool Matches(Entry entry, AnimationSegment animation)
    {
        AnimationClip clip = animation.AnimationAsset != null ? animation.AnimationAsset.Clip : null;
        float length = clip != null ? clip.length : 0f;
        return ReferenceEquals(entry.Source, animation) && animation.EditorId == entry.EditorId &&
               animation.StartFrame == entry.StartFrame && animation.AnimationAsset == entry.AnimationAsset &&
               clip == entry.Clip && Mathf.Approximately(length, entry.ClipLength) &&
               Mathf.Approximately(animation.SourceStartTime, entry.SourceStartTime) &&
               Mathf.Approximately(animation.SourceEndTime, entry.SourceEndTime) &&
               Mathf.Approximately(animation.PlayRate, entry.PlayRate);
    }

    private static Entry CaptureEntry(ActionV1DocumentEntry documentEntry)
    {
        var entry = new Entry
        {
            Source = documentEntry.Source,
            EditorId = documentEntry.EditorId,
            StartFrame = documentEntry.StartFrame,
            DurationFrames = documentEntry.Source is AnimationSegment animationDuration ? animationDuration.DerivedDurationFrames :
                documentEntry.Source is RangeGameplayItem range ? range.DurationFrames : 1,
            LaneIndex = documentEntry.LaneIndex,
            Muted = documentEntry.Source is GameplayItem item && item.Muted,
        };
        if (documentEntry.Source is AnimationSegment animation)
        {
            entry.AnimationAsset = animation.AnimationAsset;
            entry.Clip = animation.AnimationAsset != null ? animation.AnimationAsset.Clip : null;
            entry.ClipLength = entry.Clip != null ? entry.Clip.length : 0f;
            entry.SourceStartTime = animation.SourceStartTime;
            entry.SourceEndTime = animation.SourceEndTime;
            entry.PlayRate = animation.PlayRate;
        }
        return entry;
    }

    private static ActionV1TimelineOperationCandidate Candidate(Entry entry, int start, int duration, int laneIndex)
    {
        return new ActionV1TimelineOperationCandidate
        {
            Source = entry.Source,
            StartFrame = start,
            DurationFrames = duration,
            LaneIndex = laneIndex,
            AnimationAsset = entry.AnimationAsset,
            Clip = entry.Clip,
            ClipLength = entry.ClipLength,
            SourceStartTime = entry.SourceStartTime,
            SourceEndTime = entry.SourceEndTime,
            PlayRate = entry.PlayRate,
        };
    }

    private static ActionV1TimelineOperationResult Allowed(ActionV1TimelineOperationCandidate candidate)
    {
        var result = new ActionV1TimelineOperationResult { State = ActionV1TimelineOperationState.Allowed };
        result.Candidates.Add(candidate);
        return result;
    }

    private static ActionV1TimelineOperationResult Rejected(string message) =>
        new ActionV1TimelineOperationResult { State = ActionV1TimelineOperationState.Rejected, Message = message ?? string.Empty };

    private static int CalculateAnimationDuration(float sourceStart, float sourceEnd, float playRate)
    {
        if (playRate <= 0f)
            return 0;
        double frames = Math.Ceiling((sourceEnd - sourceStart) / playRate * ActionTimelineData.FrameRate);
        return double.IsNaN(frames) || double.IsInfinity(frames) || frames < 1d || frames > int.MaxValue ? 0 : (int)frames;
    }
}

internal static class ActionV1EditorCommands
{
    internal static event Action<ActionAsset, ActionV1EditorChangeFlags> Changed;

    internal static string NewEditorId() => Guid.NewGuid().ToString("N");

    internal static bool RepairEditorIds(ActionAsset asset, out int repaired, out string message)
    {
        repaired = 0;
        message = string.Empty;
        if (asset == null || asset.Timeline == null)
        {
            message = "Choose an ActionAsset with a V1 Timeline.";
            return false;
        }

        ActionAuthoringValidationResult before = ActionAuthoringValidator.Validate(asset);
        if (!before.Issues.Any(issue => ActionV1ValidationIndex.IsIdentity(issue.Code)))
        {
            message = "All V1 Editor IDs are already valid and unique.";
            return false;
        }

        repaired = ActionAuthoringIdentity.RepairInvalidIds(asset);
        if (repaired <= 0)
        {
            message = "No repairable V1 Editor IDs were found.";
            return false;
        }

        ActionV1EditorContext.Shared.CompleteIdentityRepair(asset);
        message = $"Repaired {repaired} V1 Editor ID{(repaired == 1 ? string.Empty : "s")}.";
        return true;
    }

    internal static bool AddAnimationSegment(ActionAsset asset, AnimationAsset animationAsset, int startFrame, out string message)
    {
        message = string.Empty;
        if (asset == null || asset.Timeline == null || animationAsset == null || animationAsset.Clip == null)
        {
            message = "Choose an AnimationAsset with a valid AnimationClip.";
            return false;
        }
        if (startFrame < 0 || !ActionAuthoringMath.IsFinite(animationAsset.Clip.length) || animationAsset.Clip.length <= 0f)
        {
            message = "Animation source or start frame is invalid.";
            return false;
        }

        var segment = new AnimationSegment();
        segment.EditorSetEditorId(NewEditorId());
        segment.EditorSetData(startFrame, animationAsset, 0f, animationAsset.Clip.length, 1f);
        if (WouldAnimationOverlap(asset, segment, null))
        {
            message = "The new AnimationSegment would overlap an existing segment.";
            return false;
        }

        Record(asset, "Add Action Animation Segment");
        asset.Timeline.EditorAnimationSegments.Add(segment);
        Commit(asset, ActionV1EditorChangeFlags.Structure | ActionV1EditorChangeFlags.Timing);
        ActionV1EditorContext.Shared.Select(ActionV1SelectionKind.AnimationSegment, segment.EditorId, false, false);
        return true;
    }

    internal static GameplayLane AddLane(ActionAsset asset)
    {
        if (asset == null || asset.Timeline == null)
            return null;
        Record(asset, "Add Action Gameplay Lane");
        var lane = new GameplayLane();
        lane.EditorSetEditorId(NewEditorId());
        lane.EditorSetName(BuildUniqueLaneName(asset.Timeline.EditorGameplayLanes));
        asset.Timeline.EditorGameplayLanes.Add(lane);
        Commit(asset, ActionV1EditorChangeFlags.Structure);
        ActionV1EditorContext.Shared.Select(ActionV1SelectionKind.GameplayLane, lane.EditorId, false, false);
        return lane;
    }

    internal static GameplayItem AddItem(ActionAsset asset, string laneId, Type itemType, int frame, out string message)
    {
        message = string.Empty;
        if (asset == null || asset.Timeline == null || frame < 0 || itemType == null || itemType.IsAbstract || !typeof(GameplayItem).IsAssignableFrom(itemType))
        {
            message = "GameplayItem creation request is invalid.";
            return null;
        }
        GameplayLane lane = FindLane(asset, laneId);
        if (lane == null)
        {
            message = "The target GameplayLane no longer exists.";
            return null;
        }

        var item = (GameplayItem)Activator.CreateInstance(itemType);
        item.EditorSetEditorId(NewEditorId());
        if (item is PointGameplayItem point) point.EditorSetFrame(frame);
        else if (item is RangeGameplayItem range) range.EditorSetTiming(frame, 1);
        else
        {
            message = "Only PointGameplayItem and RangeGameplayItem types are supported.";
            return null;
        }

        Record(asset, $"Add {itemType.Name}");
        lane.EditorItems.Add(item);
        Commit(asset, ActionV1EditorChangeFlags.Structure | ActionV1EditorChangeFlags.Timing);
        ActionV1EditorContext.Shared.Select(ActionV1SelectionKind.GameplayItem, item.EditorId, false, false);
        return item;
    }

    internal static bool RenameLane(ActionAsset asset, string laneId, string name)
    {
        GameplayLane lane = FindLane(asset, laneId);
        if (lane == null || string.Equals(lane.Name, name ?? string.Empty, StringComparison.Ordinal))
            return false;
        Record(asset, "Rename Action Gameplay Lane");
        lane.EditorSetName(name);
        Commit(asset, ActionV1EditorChangeFlags.Content);
        return true;
    }

    internal static bool SetMuted(ActionAsset asset, string editorId, bool muted)
    {
        ActionV1EditorDocument document = ActionV1EditorDocument.Build(asset, false);
        if (document == null || !document.ById.TryGetValue(editorId, out ActionV1DocumentEntry entry))
            return false;
        if (entry.Source is GameplayLane existingLane && existingLane.Muted == muted) return false;
        if (entry.Source is GameplayItem existingItem && existingItem.Muted == muted) return false;
        if (!(entry.Source is GameplayLane) && !(entry.Source is GameplayItem)) return false;
        Record(asset, "Set Action Content Mute");
        if (entry.Source is GameplayLane lane) lane.EditorSetMuted(muted);
        else if (entry.Source is GameplayItem item) item.EditorSetMuted(muted);
        else return false;
        Commit(asset, ActionV1EditorChangeFlags.Content);
        return true;
    }

    internal static bool ReorderLane(ActionAsset asset, string laneId, int delta)
    {
        if (asset == null || asset.Timeline == null || delta == 0)
            return false;
        List<GameplayLane> lanes = asset.Timeline.EditorGameplayLanes;
        int index = lanes.FindIndex(lane => lane != null && lane.EditorId == laneId);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= lanes.Count)
            return false;
        Record(asset, "Reorder Action Gameplay Lane");
        GameplayLane value = lanes[index];
        lanes.RemoveAt(index);
        lanes.Insert(target, value);
        Commit(asset, ActionV1EditorChangeFlags.Structure);
        return true;
    }

    internal static bool DeleteSelection(ActionAsset asset, IReadOnlyList<string> ids, bool confirmNonEmptyLane, out string message)
    {
        message = string.Empty;
        if (asset == null || asset.Timeline == null || ids == null || ids.Count == 0)
            return false;

        ActionV1EditorDocument document = ActionV1EditorDocument.Build(asset, false);
        List<ActionV1DocumentEntry> entries = ids.Where(id => document.ById.TryGetValue(id, out _)).Select(id => document.ById[id]).ToList();
        if (entries.Count == 0)
            return false;
        if (!confirmNonEmptyLane && entries.Any(entry => entry.Source is GameplayLane lane && lane.Items.Count > 0))
        {
            message = "A selected GameplayLane contains items.";
            return false;
        }

        Record(asset, "Delete Action Timeline Content");
        foreach (ActionV1DocumentEntry entry in entries.Where(entry => entry.Source is AnimationSegment))
            asset.Timeline.EditorAnimationSegments.Remove((AnimationSegment)entry.Source);
        foreach (ActionV1DocumentEntry entry in entries.Where(entry => entry.Source is GameplayItem))
        {
            GameplayLane lane = FindLane(asset, entry.LaneId);
            lane?.EditorItems.Remove((GameplayItem)entry.Source);
        }
        foreach (ActionV1DocumentEntry entry in entries.Where(entry => entry.Source is GameplayLane))
            asset.Timeline.EditorGameplayLanes.Remove((GameplayLane)entry.Source);

        Commit(asset, ActionV1EditorChangeFlags.Structure | ActionV1EditorChangeFlags.Timing);
        ActionV1EditorContext.Shared.SelectAction();
        return true;
    }

    internal static bool DeleteNullEntry(ActionAsset asset, ActionV1SelectionKind kind, int laneIndex, int itemIndex, out string message)
    {
        message = string.Empty;
        if (asset?.Timeline == null)
            return false;

        if (kind == ActionV1SelectionKind.AnimationSegment)
        {
            List<AnimationSegment> segments = asset.Timeline.EditorAnimationSegments;
            if (itemIndex < 0 || itemIndex >= segments.Count || segments[itemIndex] != null)
            {
                message = "The null AnimationSegment path is no longer valid.";
                return false;
            }
            Record(asset, "Delete Null Action Animation Segment");
            segments.RemoveAt(itemIndex);
        }
        else if (kind == ActionV1SelectionKind.GameplayLane)
        {
            List<GameplayLane> lanes = asset.Timeline.EditorGameplayLanes;
            if (laneIndex < 0 || laneIndex >= lanes.Count || lanes[laneIndex] != null)
            {
                message = "The null GameplayLane path is no longer valid.";
                return false;
            }
            Record(asset, "Delete Null Action Gameplay Lane");
            lanes.RemoveAt(laneIndex);
        }
        else if (kind == ActionV1SelectionKind.GameplayItem)
        {
            List<GameplayLane> lanes = asset.Timeline.EditorGameplayLanes;
            if (laneIndex < 0 || laneIndex >= lanes.Count || lanes[laneIndex] == null ||
                itemIndex < 0 || itemIndex >= lanes[laneIndex].EditorItems.Count || lanes[laneIndex].EditorItems[itemIndex] != null)
            {
                message = "The null GameplayItem path is no longer valid.";
                return false;
            }
            Record(asset, "Delete Null Action Gameplay Item");
            lanes[laneIndex].EditorItems.RemoveAt(itemIndex);
        }
        else
        {
            message = "Only null Timeline entries can be deleted by AuthoringPath.";
            return false;
        }

        Commit(asset, ActionV1EditorChangeFlags.Structure | ActionV1EditorChangeFlags.Timing);
        ActionV1EditorContext.Shared.SelectAction();
        return true;
    }

    internal static bool MoveSelection(ActionAsset asset, IReadOnlyList<string> ids, int frameDelta, int laneDelta, out string message)
    {
        ActionV1TimelineOperationSnapshot snapshot = ActionV1TimelineOperationSnapshot.Capture(
            ActionV1EditorDocument.Build(asset, false), ActionV1TimelineOperationKind.Move, ids);
        ActionV1TimelineOperationResult result = snapshot?.Evaluate(frameDelta, laneDelta);
        return CommitTimelineOperation(snapshot, result, out message);
    }

    internal static bool ResizeRange(ActionAsset asset, string itemId, bool left, int deltaFrames, out string message)
    {
        ActionV1TimelineOperationSnapshot snapshot = ActionV1TimelineOperationSnapshot.Capture(
            ActionV1EditorDocument.Build(asset, false),
            left ? ActionV1TimelineOperationKind.ResizeLeft : ActionV1TimelineOperationKind.ResizeRight,
            new[] { itemId });
        ActionV1TimelineOperationResult result = snapshot?.Evaluate(deltaFrames, 0);
        return CommitTimelineOperation(snapshot, result, out message);
    }

    internal static bool TrimAnimation(ActionAsset asset, string segmentId, bool left, int deltaFrames, out string message)
    {
        ActionV1TimelineOperationSnapshot snapshot = ActionV1TimelineOperationSnapshot.Capture(
            ActionV1EditorDocument.Build(asset, false),
            left ? ActionV1TimelineOperationKind.TrimLeft : ActionV1TimelineOperationKind.TrimRight,
            new[] { segmentId });
        ActionV1TimelineOperationResult result = snapshot?.Evaluate(deltaFrames, 0);
        return CommitTimelineOperation(snapshot, result, out message);
    }

    internal static bool CommitTimelineOperation(ActionV1TimelineOperationSnapshot snapshot,
        ActionV1TimelineOperationResult result, out string message)
    {
        message = result?.Message ?? string.Empty;
        if (snapshot == null || result == null || result.State != ActionV1TimelineOperationState.Allowed)
            return false;
        if (!snapshot.MatchesCurrentSource(out message))
            return false;

        foreach (ActionV1TimelineOperationCandidate candidate in result.Candidates.Where(value => value.Source is AnimationSegment))
        {
            AnimationClip currentClip = candidate.AnimationAsset != null ? candidate.AnimationAsset.Clip : null;
            float currentLength = currentClip != null ? currentClip.length : 0f;
            if (currentClip != candidate.Clip || !Mathf.Approximately(currentLength, candidate.ClipLength))
            {
                message = "Animation source changed while editing. Reopen the draft and try again.";
                return false;
            }
        }

        ActionAsset asset = snapshot.Asset;
        bool changesLane = result.Candidates.Any(candidate =>
            candidate.Source is GameplayItem item && FindLaneContaining(asset, item, out int laneIndex) && laneIndex != candidate.LaneIndex);
        string undoLabel = snapshot.Kind == ActionV1TimelineOperationKind.Move
            ? "Move Action Timeline Content"
            : snapshot.Kind == ActionV1TimelineOperationKind.ResizeLeft || snapshot.Kind == ActionV1TimelineOperationKind.ResizeRight
                ? "Resize Action Gameplay Item"
                : snapshot.Kind == ActionV1TimelineOperationKind.SetPointTiming || snapshot.Kind == ActionV1TimelineOperationKind.SetRangeTiming
                    ? "Edit Action Gameplay Timing"
                    : snapshot.Kind == ActionV1TimelineOperationKind.SetAnimationTiming
                        ? "Edit Action Animation Timing"
                        : "Trim Action Animation Segment";
        Record(asset, undoLabel);

        if (changesLane)
        {
            foreach (ActionV1TimelineOperationCandidate candidate in result.Candidates.Where(candidate => candidate.Source is GameplayItem))
            {
                GameplayItem item = (GameplayItem)candidate.Source;
                foreach (GameplayLane lane in asset.Timeline.EditorGameplayLanes.Where(lane => lane != null))
                    lane.EditorItems.Remove(item);
            }
            foreach (ActionV1TimelineOperationCandidate candidate in result.Candidates.Where(candidate => candidate.Source is GameplayItem))
                asset.Timeline.EditorGameplayLanes[candidate.LaneIndex].EditorItems.Add((GameplayItem)candidate.Source);
        }

        foreach (ActionV1TimelineOperationCandidate candidate in result.Candidates)
        {
            if (candidate.Source is AnimationSegment animation)
                animation.EditorSetData(candidate.StartFrame, candidate.AnimationAsset, candidate.SourceStartTime,
                    candidate.SourceEndTime, candidate.PlayRate);
            else if (candidate.Source is PointGameplayItem point)
                point.EditorSetFrame(candidate.StartFrame);
            else if (candidate.Source is RangeGameplayItem range)
                range.EditorSetTiming(candidate.StartFrame, candidate.DurationFrames);
        }
        ActionV1EditorChangeFlags flags = ActionV1EditorChangeFlags.Timing;
        if (changesLane) flags |= ActionV1EditorChangeFlags.Structure;
        if (snapshot.Kind == ActionV1TimelineOperationKind.TrimLeft || snapshot.Kind == ActionV1TimelineOperationKind.TrimRight ||
            snapshot.Kind == ActionV1TimelineOperationKind.SetAnimationTiming)
            flags |= ActionV1EditorChangeFlags.Content;
        Commit(asset, flags);
        message = string.Empty;
        return true;
    }

    private static bool FindLaneContaining(ActionAsset asset, GameplayItem item, out int laneIndex)
    {
        laneIndex = -1;
        if (asset?.Timeline == null || item == null)
            return false;
        for (int index = 0; index < asset.Timeline.EditorGameplayLanes.Count; index++)
        {
            GameplayLane lane = asset.Timeline.EditorGameplayLanes[index];
            if (lane != null && lane.EditorItems.Contains(item))
            {
                laneIndex = index;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Shared non-mutating operation checks used by both Timeline ghosts and the commands that commit them.
    /// A false return with an empty message means the operation is a no-op.
    /// </summary>
    internal static bool CanMoveSelection(ActionAsset asset, IReadOnlyList<string> ids, int frameDelta, int laneDelta,
        out List<ActionV1DocumentEntry> entries, out string message)
    {
        return CanMoveSelection(ActionV1EditorDocument.Build(asset, false), ids, frameDelta, laneDelta, out entries, out message);
    }

    internal static bool CanMoveSelection(ActionV1EditorDocument document, IReadOnlyList<string> ids, int frameDelta, int laneDelta,
        out List<ActionV1DocumentEntry> entries, out string message)
    {
        message = string.Empty;
        entries = new List<ActionV1DocumentEntry>();
        if (document == null || ids == null || ids.Count == 0 || (frameDelta == 0 && laneDelta == 0))
            return false;
        entries = ids.Where(id => document.ById.TryGetValue(id, out ActionV1DocumentEntry entry) &&
                                   (entry.Source is AnimationSegment || entry.Source is GameplayItem))
            .Select(id => document.ById[id]).ToList();
        if (entries.Count == 0)
            return false;
        if (entries.Any(entry => (long)entry.StartFrame + frameDelta < 0 ||
                                 (long)entry.StartFrame + frameDelta > int.MaxValue))
        {
            message = "Move would place content outside the supported Frame range.";
            return false;
        }
        if (laneDelta != 0 && entries.Any(entry => entry.Source is AnimationSegment ||
            entry.LaneIndex + laneDelta < 0 || entry.LaneIndex + laneDelta >= document.Lanes.Count ||
            !(document.Lanes[entry.LaneIndex + laneDelta].Source is GameplayLane)))
        {
            message = "Vertical move is only valid when every selected GameplayItem has a destination Lane.";
            return false;
        }
        if (WouldMovedAnimationsOverlap(document, entries, frameDelta))
        {
            message = "Every moved AnimationSegment must finish without an overlap.";
            return false;
        }
        return true;
    }

    internal static bool CanResizeRange(ActionAsset asset, string itemId, bool left, int deltaFrames,
        out RangeGameplayItem range, out int start, out int duration, out string message)
    {
        return CanResizeRange(ActionV1EditorDocument.Build(asset, false), itemId, left, deltaFrames,
            out range, out start, out duration, out message);
    }

    internal static bool CanResizeRange(ActionV1EditorDocument document, string itemId, bool left, int deltaFrames,
        out RangeGameplayItem range, out int start, out int duration, out string message)
    {
        range = null;
        start = 0;
        duration = 0;
        message = string.Empty;
        if (document == null || !document.ById.TryGetValue(itemId, out ActionV1DocumentEntry entry) ||
            !(entry.Source is RangeGameplayItem candidate))
            return false;
        if (deltaFrames == 0)
            return false;
        long startValue = left ? (long)candidate.StartFrame + deltaFrames : candidate.StartFrame;
        long durationValue = left ? (long)candidate.DurationFrames - deltaFrames : (long)candidate.DurationFrames + deltaFrames;
        if (startValue < 0 || startValue > int.MaxValue || durationValue < 1 || durationValue > int.MaxValue)
        {
            message = "Range must remain at or after Frame 0 and at least one Frame long.";
            return false;
        }
        range = candidate;
        start = (int)startValue;
        duration = (int)durationValue;
        return true;
    }

    internal static bool CanTrimAnimation(ActionAsset asset, string segmentId, bool left, int deltaFrames,
        out AnimationSegment segment, out int start, out float sourceStart, out float sourceEnd, out int duration, out string message)
    {
        return CanTrimAnimation(ActionV1EditorDocument.Build(asset, false), segmentId, left, deltaFrames,
            out segment, out start, out sourceStart, out sourceEnd, out duration, out message);
    }

    internal static bool CanTrimAnimation(ActionV1EditorDocument document, string segmentId, bool left, int deltaFrames,
        out AnimationSegment segment, out int start, out float sourceStart, out float sourceEnd, out int duration, out string message)
    {
        segment = null;
        start = 0;
        sourceStart = 0f;
        sourceEnd = 0f;
        duration = 0;
        message = string.Empty;
        if (document == null || !document.ById.TryGetValue(segmentId, out ActionV1DocumentEntry entry) ||
            !(entry.Source is AnimationSegment candidate) || deltaFrames == 0)
            return false;
        if (!TryCalculateAnimationTrim(candidate, left, deltaFrames, out start, out sourceStart, out sourceEnd, out duration, out message))
            return false;
        if (WouldAnimationOverlap(document.Asset, start, duration, candidate))
        {
            message = "Trim would overlap another AnimationSegment.";
            return false;
        }
        segment = candidate;
        return true;
    }

    internal static bool TryCalculateAnimationTrim(AnimationSegment segment, bool left, int deltaFrames,
        out int start, out float sourceStart, out float sourceEnd, out int duration, out string message)
    {
        start = segment != null ? segment.StartFrame : 0;
        sourceStart = segment != null ? segment.SourceStartTime : 0f;
        sourceEnd = segment != null ? segment.SourceEndTime : 0f;
        duration = 0;
        message = "AnimationSegment cannot be trimmed until its source data is valid.";
        if (segment == null || segment.AnimationAsset == null || segment.AnimationAsset.Clip == null ||
            !ActionAuthoringMath.IsFinite(segment.SourceStartTime) || !ActionAuthoringMath.IsFinite(segment.SourceEndTime) ||
            !ActionAuthoringMath.IsFinite(segment.PlayRate) || segment.PlayRate <= 0f ||
            segment.SourceStartTime < 0f || segment.SourceEndTime <= segment.SourceStartTime)
            return false;
        long startValue = left ? (long)segment.StartFrame + deltaFrames : segment.StartFrame;
        float seconds = deltaFrames / (float)ActionTimelineData.FrameRate * segment.PlayRate;
        sourceStart = left ? segment.SourceStartTime + seconds : segment.SourceStartTime;
        sourceEnd = left ? segment.SourceEndTime : segment.SourceEndTime + seconds;
        if (startValue < 0 || startValue > int.MaxValue || !ActionAuthoringMath.IsFinite(sourceStart) ||
            !ActionAuthoringMath.IsFinite(sourceEnd) || sourceStart < 0f || sourceEnd <= sourceStart ||
            sourceEnd > segment.AnimationAsset.Clip.length + 0.00001f)
        {
            message = "Trim would create an invalid source range or move before Frame 0.";
            return false;
        }
        duration = CalculateAnimationDuration(sourceStart, sourceEnd, segment.PlayRate);
        if (duration < 1)
        {
            message = "Trim would create an invalid Animation duration.";
            return false;
        }
        start = (int)startValue;
        message = string.Empty;
        return true;
    }

    internal static bool SetAnimationData(
        ActionAsset asset, string segmentId, int startFrame, AnimationAsset animationAsset,
        float sourceStart, float sourceEnd, float playRate, out string message)
    {
        message = string.Empty;
        ActionV1EditorDocument document = ActionV1EditorDocument.Build(asset, false);
        if (document == null || !document.ById.TryGetValue(segmentId, out ActionV1DocumentEntry entry) || !(entry.Source is AnimationSegment segment))
            return false;
        float clipLength = animationAsset != null && animationAsset.Clip != null ? animationAsset.Clip.length : 0f;
        if (startFrame < 0 || animationAsset == null || animationAsset.Clip == null ||
            !ActionAuthoringMath.IsFinite(sourceStart) || !ActionAuthoringMath.IsFinite(sourceEnd) ||
            !ActionAuthoringMath.IsFinite(playRate) || sourceStart < 0f || sourceEnd <= sourceStart ||
            sourceEnd > clipLength + 0.00001f || playRate <= 0f)
        {
            message = "AnimationSegment timing, source range, or AnimationAsset is invalid.";
            return false;
        }
        int duration = CalculateAnimationDuration(sourceStart, sourceEnd, playRate);
        if (duration < 1 || WouldAnimationOverlap(asset, startFrame, duration, segment))
        {
            message = "AnimationSegment would overlap another segment.";
            return false;
        }
        if (segment.StartFrame == startFrame && segment.AnimationAsset == animationAsset &&
            Mathf.Approximately(segment.SourceStartTime, sourceStart) && Mathf.Approximately(segment.SourceEndTime, sourceEnd) &&
            Mathf.Approximately(segment.PlayRate, playRate))
            return false;
        Record(asset, "Edit Action Animation Segment");
        segment.EditorSetData(startFrame, animationAsset, sourceStart, sourceEnd, playRate);
        Commit(asset, ActionV1EditorChangeFlags.Timing | ActionV1EditorChangeFlags.Content);
        return true;
    }

    internal static bool SetPointFrame(ActionAsset asset, string itemId, int frame, out string message)
    {
        message = string.Empty;
        ActionV1EditorDocument document = ActionV1EditorDocument.Build(asset, false);
        if (frame < 0 || document == null || !document.ById.TryGetValue(itemId, out ActionV1DocumentEntry entry) || !(entry.Source is PointGameplayItem point))
        {
            message = "Point Frame must be non-negative.";
            return false;
        }
        if (point.Frame == frame)
            return false;
        Record(asset, "Edit Action Point Timing");
        point.EditorSetFrame(frame);
        Commit(asset, ActionV1EditorChangeFlags.Timing);
        return true;
    }

    internal static bool SetRangeTiming(ActionAsset asset, string itemId, int startFrame, int durationFrames, out string message)
    {
        message = string.Empty;
        ActionV1EditorDocument document = ActionV1EditorDocument.Build(asset, false);
        if (startFrame < 0 || durationFrames < 1 || document == null || !document.ById.TryGetValue(itemId, out ActionV1DocumentEntry entry) || !(entry.Source is RangeGameplayItem range))
        {
            message = "Range StartFrame must be non-negative and DurationFrames must be at least one.";
            return false;
        }
        if (range.StartFrame == startFrame && range.DurationFrames == durationFrames)
            return false;
        Record(asset, "Edit Action Range Timing");
        range.EditorSetTiming(startFrame, durationFrames);
        Commit(asset, ActionV1EditorChangeFlags.Timing);
        return true;
    }

    internal static bool Copy(ActionAsset asset, IReadOnlyList<string> ids, out string message)
    {
        return ActionV1EditorClipboard.Copy(asset, ids, out message);
    }

    internal static bool Paste(ActionAsset asset, int anchorFrame, out string message)
    {
        return ActionV1EditorClipboard.Paste(asset, anchorFrame, duplicate: false, out message);
    }

    internal static bool Duplicate(ActionAsset asset, IReadOnlyList<string> ids, out string message)
    {
        return ActionV1EditorClipboard.Duplicate(asset, ids, out message);
    }

    internal static GameplayLane FindLane(ActionAsset asset, string laneId)
    {
        return asset?.Timeline?.EditorGameplayLanes.FirstOrDefault(lane => lane != null && lane.EditorId == laneId);
    }

    internal static T CloneManaged<T>(T source) where T : class
    {
        if (source == null)
            return null;
        var clone = (T)Activator.CreateInstance(source.GetType());
        EditorUtility.CopySerializedManagedFieldsOnly(source, clone);
        return clone;
    }

    internal static void Commit(ActionAsset asset, ActionV1EditorChangeFlags flags)
    {
        EditorUtility.SetDirty(asset);
        Changed?.Invoke(asset, flags);
        ActionV1EditorContext.Shared.NotifyAssetChanged(flags);
    }

    private static void Record(ActionAsset asset, string label) => Undo.RegisterCompleteObjectUndo(asset, label);

    private static string BuildUniqueLaneName(List<GameplayLane> lanes)
    {
        var names = new HashSet<string>(lanes.Where(lane => lane != null).Select(lane => lane.Name), StringComparer.OrdinalIgnoreCase);
        if (!names.Contains("Gameplay"))
            return "Gameplay";
        for (int index = 2; ; index++)
        {
            string candidate = $"Gameplay {index}";
            if (!names.Contains(candidate))
                return candidate;
        }
    }

    private static bool WouldMovedAnimationsOverlap(ActionV1EditorDocument document, List<ActionV1DocumentEntry> movedEntries, int delta)
    {
        var movedIds = new HashSet<string>(movedEntries.Where(entry => entry.Source is AnimationSegment).Select(entry => entry.EditorId));
        var ranges = new List<(string id, long start, long end)>();
        foreach (ActionV1DocumentEntry entry in document.AnimationSegments)
        {
            long appliedDelta = movedIds.Contains(entry.EditorId) ? delta : 0;
            ranges.Add((entry.EditorId, (long)entry.StartFrame + appliedDelta, entry.RawEndFrameExclusive + appliedDelta));
        }
        for (int left = 0; left < ranges.Count; left++)
        for (int right = left + 1; right < ranges.Count; right++)
        {
            bool finalOverlap = ranges[left].start < ranges[right].end && ranges[right].start < ranges[left].end;
            if (finalOverlap && (movedIds.Contains(ranges[left].id) || movedIds.Contains(ranges[right].id)))
                return true;
        }
        return false;
    }

    private static bool WouldAnimationOverlap(ActionAsset asset, AnimationSegment candidate, AnimationSegment ignore)
    {
        return WouldAnimationOverlap(asset, candidate.StartFrame, candidate.DerivedDurationFrames, ignore);
    }

    private static bool WouldAnimationOverlap(ActionAsset asset, int start, int duration, AnimationSegment ignore)
    {
        long end = (long)start + Math.Max(1, duration);
        foreach (AnimationSegment segment in asset.Timeline.AnimationSegments)
        {
            if (segment == null || ReferenceEquals(segment, ignore))
                continue;
            if (start < segment.EndFrameExclusiveLong && segment.StartFrame < end)
                return true;
        }
        return false;
    }

    private static int CalculateAnimationDuration(float sourceStart, float sourceEnd, float playRate)
    {
        if (playRate <= 0f)
            return 0;
        return Mathf.CeilToInt((sourceEnd - sourceStart) / playRate * ActionTimelineData.FrameRate);
    }
}

internal static class ActionV1EditorClipboard
{
    private sealed class Entry
    {
        internal AnimationSegment Segment;
        internal GameplayItem Item;
        internal string LaneId;
        internal string LaneName;
        internal long Start;
        internal long End;
    }

    private static readonly List<Entry> Entries = new List<Entry>();
    private static ActionAsset _sourceAsset;

    internal static bool Copy(ActionAsset asset, IReadOnlyList<string> ids, out string message)
    {
        message = string.Empty;
        Entries.Clear();
        _sourceAsset = asset;
        ActionV1EditorDocument document = ActionV1EditorDocument.Build(asset, false);
        if (document == null || ids == null)
            return false;
        var selectedIds = new HashSet<string>(ids.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
        foreach (ActionV1DocumentEntry source in document.EntriesInAuthoringOrder())
        {
            if (!source.HasStableSelection || !selectedIds.Contains(source.EditorId))
                continue;
            if (source.Source is AnimationSegment segment)
            {
                Entries.Add(new Entry { Segment = ActionV1EditorCommands.CloneManaged(segment), Start = source.StartFrame, End = source.RawEndFrameExclusive });
            }
            else if (source.Source is GameplayItem item)
            {
                Entries.Add(new Entry
                {
                    Item = ActionV1EditorCommands.CloneManaged(item), LaneId = source.LaneId, LaneName = source.LaneName,
                    Start = source.StartFrame, End = source.RawEndFrameExclusive,
                });
            }
        }
        if (Entries.Count == 0)
        {
            message = "Select at least one AnimationSegment or GameplayItem.";
            return false;
        }
        return true;
    }

    internal static bool Paste(ActionAsset asset, int anchorFrame, bool duplicate, out string message)
    {
        message = string.Empty;
        if (asset == null || asset.Timeline == null || Entries.Count == 0)
        {
            message = "The Action V1 clipboard is empty.";
            return false;
        }
        long earliest = Entries.Min(entry => entry.Start);
        long latest = Entries.Max(entry => entry.End);
        if (Entries.Any(entry => entry.End <= entry.Start))
        {
            message = "Clipboard content contains invalid Timing and cannot be pasted.";
            return false;
        }
        long offset = duplicate ? latest - earliest : (long)Mathf.Max(0, anchorFrame) - earliest;
        if (Entries.Any(entry => entry.Start + offset < 0L || entry.Start + offset > int.MaxValue ||
                                 entry.End + offset < 0L || entry.End + offset > (long)int.MaxValue + 1L))
        {
            message = "Paste would place content outside the supported Frame range.";
            return false;
        }

        var laneMap = new Dictionary<Entry, GameplayLane>();
        var unmappedLaneNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (Entry entry in Entries.Where(entry => entry.Item != null))
        {
            GameplayLane lane = ReferenceEquals(asset, _sourceAsset) ? ActionV1EditorCommands.FindLane(asset, entry.LaneId) : null;
            if (lane == null)
            {
                List<GameplayLane> matches = asset.Timeline.EditorGameplayLanes
                    .Where(candidate => candidate != null && string.Equals(candidate.Name, entry.LaneName, StringComparison.Ordinal))
                    .ToList();
                if (matches.Count != 1)
                {
                    unmappedLaneNames.Add(string.IsNullOrEmpty(entry.LaneName) ? "<unnamed>" : entry.LaneName);
                    continue;
                }
                lane = matches[0];
            }
            laneMap[entry] = lane;
        }
        if (unmappedLaneNames.Count > 0)
        {
            message = "Cannot uniquely map GameplayLane(s): " + string.Join(", ", unmappedLaneNames.OrderBy(value => value));
            return false;
        }

        var newAnimationRanges = new List<(long start, long end, bool pasted)>();
        foreach (Entry entry in Entries.Where(entry => entry.Segment != null))
            newAnimationRanges.Add((entry.Start + offset, entry.End + offset, true));
        foreach (AnimationSegment segment in asset.Timeline.AnimationSegments)
            if (segment != null)
                newAnimationRanges.Add((segment.StartFrame, segment.EndFrameExclusiveLong, false));
        newAnimationRanges.Sort((a, b) => a.start.CompareTo(b.start));
        for (int left = 0; left < newAnimationRanges.Count; left++)
        for (int right = left + 1; right < newAnimationRanges.Count; right++)
        {
            if ((newAnimationRanges[left].pasted || newAnimationRanges[right].pasted) &&
                newAnimationRanges[left].start < newAnimationRanges[right].end &&
                newAnimationRanges[right].start < newAnimationRanges[left].end)
            {
                message = "Paste would overlap AnimationSegments.";
                return false;
            }
        }

        var generatedIds = Entries.ToDictionary(entry => entry, _ => ActionV1EditorCommands.NewEditorId());

        Undo.RegisterCompleteObjectUndo(asset, duplicate ? "Duplicate Action Timeline Content" : "Paste Action Timeline Content");
        var selected = new List<ActionV1DocumentEntry>();
        foreach (Entry entry in Entries)
        {
            if (entry.Segment != null)
            {
                AnimationSegment clone = ActionV1EditorCommands.CloneManaged(entry.Segment);
                clone.EditorSetEditorId(generatedIds[entry]);
                clone.EditorSetData((int)(entry.Start + offset), clone.AnimationAsset, clone.SourceStartTime, clone.SourceEndTime, clone.PlayRate);
                asset.Timeline.EditorAnimationSegments.Add(clone);
                selected.Add(new ActionV1DocumentEntry { SelectionKind = ActionV1SelectionKind.AnimationSegment, EditorId = clone.EditorId });
            }
            else
            {
                GameplayItem clone = ActionV1EditorCommands.CloneManaged(entry.Item);
                clone.EditorSetEditorId(generatedIds[entry]);
                int start = (int)(entry.Start + offset);
                if (clone is PointGameplayItem point) point.EditorSetFrame(start);
                else if (clone is RangeGameplayItem range) range.EditorSetTiming(start, (int)(entry.End - entry.Start));
                laneMap[entry].EditorItems.Add(clone);
                selected.Add(new ActionV1DocumentEntry { SelectionKind = ActionV1SelectionKind.GameplayItem, EditorId = clone.EditorId });
            }
        }
        ActionV1EditorCommands.Commit(asset, ActionV1EditorChangeFlags.Structure | ActionV1EditorChangeFlags.Timing | ActionV1EditorChangeFlags.Content);
        ActionV1EditorContext.Shared.SetSelection(selected, false);
        return true;
    }

    internal static bool Duplicate(ActionAsset asset, IReadOnlyList<string> ids, out string message)
    {
        List<Entry> savedEntries = Entries.ToList();
        ActionAsset savedSource = _sourceAsset;
        try
        {
            if (!Copy(asset, ids, out message))
                return false;
            return Paste(asset, 0, duplicate: true, out message);
        }
        finally
        {
            Entries.Clear();
            Entries.AddRange(savedEntries);
            _sourceAsset = savedSource;
        }
    }
}

internal static class ActionV1SerializedLookup
{
    internal static SerializedProperty Find(ActionAsset asset, ActionV1SelectionValue selection, out SerializedObject serializedObject)
    {
        serializedObject = asset != null ? new SerializedObject(asset) : null;
        if (serializedObject == null)
            return null;
        serializedObject.UpdateIfRequiredOrScript();
        SerializedProperty timeline = serializedObject.FindProperty("_actionTimeline");
        if (timeline == null)
            return null;
        if (selection.Kind == ActionV1SelectionKind.AnimationSegment)
            return FindById(timeline.FindPropertyRelative("_animationSegments"), selection.EditorId);
        SerializedProperty lanes = timeline.FindPropertyRelative("_gameplayLanes");
        if (selection.Kind == ActionV1SelectionKind.GameplayLane)
            return FindById(lanes, selection.EditorId);
        if (selection.Kind == ActionV1SelectionKind.GameplayItem)
        {
            for (int laneIndex = 0; lanes != null && laneIndex < lanes.arraySize; laneIndex++)
            {
                SerializedProperty lane = lanes.GetArrayElementAtIndex(laneIndex);
                SerializedProperty item = FindById(lane.FindPropertyRelative("_items"), selection.EditorId);
                if (item != null)
                    return item;
            }
        }
        return null;
    }

    private static SerializedProperty FindById(SerializedProperty list, string id)
    {
        for (int i = 0; list != null && i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            SerializedProperty editorId = element.FindPropertyRelative("_editorId");
            if (editorId != null && editorId.stringValue == id)
                return element;
        }
        return null;
    }
}
#endif
