#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class ActionDocumentEntry
{
    internal ActionSelectionKind SelectionKind;
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
    internal ActionEditorIdentityState IdentityState;
    internal ActionEntryDisplayState DisplayState;
    internal object Source;

    internal bool HasStableSelection => IdentityState == ActionEditorIdentityState.Valid && !string.IsNullOrEmpty(EditorId);

    internal string DisplayName
    {
        get
        {
            if (Source == null)
                return SelectionKind == ActionSelectionKind.AnimationSegment ? "Null Animation Segment" :
                    SelectionKind == ActionSelectionKind.GameplayLane ? "Null Gameplay Lane" : "Null Gameplay Item";
            if (Source is AnimationSegment animation)
                return animation.AnimationAsset != null ? animation.AnimationAsset.name : "Missing Animation Asset";
            if (Source is GameplayLane lane)
                return string.IsNullOrWhiteSpace(lane.Name) ? "Unnamed Lane" : lane.Name;
            return Source.GetType().Name.Replace("Item", string.Empty);
        }
    }
}

/// <summary>Read-only render snapshot. The ActionAsset remains the only persisted authority.</summary>
internal sealed class ActionEditorDocument
{
    internal ActionAsset Asset;
    internal int DurationFrames;
    internal int HorizonFrames;
    internal bool HasTimeline;
    internal ActionEditorReadiness Readiness;
    private readonly List<ActionDocumentEntry> _animationSegments = new List<ActionDocumentEntry>();
    private readonly List<ActionDocumentEntry> _lanes = new List<ActionDocumentEntry>();
    private readonly List<ActionDocumentEntry> _gameplayItems = new List<ActionDocumentEntry>();
    private readonly Dictionary<string, ActionDocumentEntry> _byId =
        new Dictionary<string, ActionDocumentEntry>(StringComparer.Ordinal);
    internal IReadOnlyList<ActionDocumentEntry> AnimationSegments => _animationSegments;
    internal IReadOnlyList<ActionDocumentEntry> Lanes => _lanes;
    internal IReadOnlyList<ActionDocumentEntry> GameplayItems => _gameplayItems;
    internal IReadOnlyDictionary<string, ActionDocumentEntry> ById => _byId;

    internal static ActionEditorDocument Build(ActionAsset asset)
    {
        var document = new ActionEditorDocument
        {
            Asset = asset,
            DurationFrames = 1,
            HorizonFrames = 60,
            Readiness = asset == null ? ActionEditorReadiness.NoAction : ActionEditorReadiness.Ready,
        };
        ActionTimelineData timeline = asset != null ? asset.Timeline : null;
        document.HasTimeline = timeline != null;
        if (timeline == null)
        {
            return document;
        }

        document.DurationFrames = Mathf.Max(1, timeline.DurationFrames);
        IReadOnlyList<AnimationSegment> segments = timeline.AnimationSegments;
        for (int i = 0; segments != null && i < segments.Count; i++)
        {
            AnimationSegment segment = segments[i];
            int start = segment != null ? segment.StartFrame : 0;
            int duration = segment != null ? segment.DerivedDurationFrames : 0;
            var entry = new ActionDocumentEntry
            {
                SelectionKind = ActionSelectionKind.AnimationSegment,
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
                IdentityState = segment == null ? ActionEditorIdentityState.NotApplicable : ClassifyIdentity(segment.EditorId),
                Source = segment,
            };
            entry.DisplayState = ClassifyDisplayState(entry);
            document._animationSegments.Add(entry);
        }

        IReadOnlyList<GameplayLane> lanes = timeline.GameplayLanes;
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            GameplayLane lane = lanes[laneIndex];
            var laneEntry = new ActionDocumentEntry
            {
                SelectionKind = ActionSelectionKind.GameplayLane,
                EditorId = lane != null ? lane.EditorId : string.Empty,
                DisplayKey = $"lane:{laneIndex}",
                AuthoringPath = $"GameplayLanes[{laneIndex}]",
                LaneId = lane != null ? lane.EditorId : string.Empty,
                LaneName = lane != null ? lane.Name : "Null Lane",
                LaneIndex = laneIndex,
                ItemIndex = -1,
                Muted = lane != null && lane.Muted,
                IdentityState = lane == null ? ActionEditorIdentityState.NotApplicable : ClassifyIdentity(lane.EditorId),
                Source = lane,
            };
            laneEntry.DisplayState = ClassifyDisplayState(laneEntry);
            document._lanes.Add(laneEntry);

            IReadOnlyList<GameplayItem> items = lane != null ? lane.Items : null;
            for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
            {
                GameplayItem item = items[itemIndex];
                int start = item is PointGameplayItem point ? point.Frame : item is RangeGameplayItem range ? range.StartFrame : 0;
                int duration = item is PointGameplayItem ? 1 : item is RangeGameplayItem ranged ? ranged.DurationFrames : 0;
                var itemEntry = new ActionDocumentEntry
                {
                    SelectionKind = ActionSelectionKind.GameplayItem,
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
                    IdentityState = item == null ? ActionEditorIdentityState.NotApplicable : ClassifyIdentity(item.EditorId),
                    Source = item,
                };
                itemEntry.DisplayState = ClassifyDisplayState(itemEntry);
                document._gameplayItems.Add(itemEntry);
            }
        }

        document.FinalizeIdentity();
        document.HorizonFrames = Mathf.Max(60, SaturatingEnd(document.DurationFrames, 30));
        bool identityBlocked = document.AllEntries().Any(entry =>
            entry.IdentityState == ActionEditorIdentityState.Missing ||
            entry.IdentityState == ActionEditorIdentityState.Malformed ||
            entry.IdentityState == ActionEditorIdentityState.Duplicate);
        document.Readiness = identityBlocked ? ActionEditorReadiness.IdentityBlocked : ActionEditorReadiness.Ready;
        return document;
    }

    internal IEnumerable<ActionDocumentEntry> ContentEntries => AnimationSegments.Concat(GameplayItems);

    private void FinalizeIdentity()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ActionDocumentEntry entry in AllEntries())
        {
            if (entry.IdentityState != ActionEditorIdentityState.Valid)
                continue;
            counts.TryGetValue(entry.EditorId, out int count);
            counts[entry.EditorId] = count + 1;
        }

        foreach (ActionDocumentEntry entry in AllEntries())
        {
            if (entry.IdentityState == ActionEditorIdentityState.Valid && counts[entry.EditorId] > 1)
                entry.IdentityState = ActionEditorIdentityState.Duplicate;
            if (entry.IdentityState == ActionEditorIdentityState.Valid)
                _byId.Add(entry.EditorId, entry);
        }
    }

    private IEnumerable<ActionDocumentEntry> AllEntries() => AnimationSegments.Concat(Lanes).Concat(GameplayItems);

    internal IEnumerable<ActionDocumentEntry> EntriesInAuthoringOrder()
    {
        foreach (ActionDocumentEntry animation in AnimationSegments)
            yield return animation;
        foreach (ActionDocumentEntry lane in Lanes.OrderBy(entry => entry.LaneIndex))
        {
            yield return lane;
            foreach (ActionDocumentEntry item in GameplayItems.Where(entry => entry.LaneIndex == lane.LaneIndex).OrderBy(entry => entry.ItemIndex))
                yield return item;
        }
    }

    private static ActionEditorIdentityState ClassifyIdentity(string editorId)
    {
        if (string.IsNullOrEmpty(editorId))
            return ActionEditorIdentityState.Missing;
        return ActionAuthoringIdentity.IsValidEditorId(editorId)
            ? ActionEditorIdentityState.Valid
            : ActionEditorIdentityState.Malformed;
    }

    private static ActionEntryDisplayState ClassifyDisplayState(ActionDocumentEntry entry)
    {
        if (entry == null)
            return ActionEntryDisplayState.NullEntry;

        ActionEntryDisplayState state = ActionEntryDisplayState.Normal;
        if (entry.Source == null)
            state |= ActionEntryDisplayState.NullEntry;

        bool hasTiming = entry.SelectionKind == ActionSelectionKind.AnimationSegment ||
                         entry.SelectionKind == ActionSelectionKind.GameplayItem;
        if (hasTiming && entry.StartFrame < 0)
            state |= ActionEntryDisplayState.NegativeStart;
        if (hasTiming && entry.RawEndFrameExclusive <= entry.StartFrame)
            state |= ActionEntryDisplayState.InvalidDuration;
        if (entry.Source is AnimationSegment segment &&
            (segment.AnimationAsset == null || segment.AnimationAsset.Clip == null))
            state |= ActionEntryDisplayState.MissingSource;

        return state;
    }

    private static int SaturatingEnd(int start, int duration)
    {
        long end = (long)start + duration;
        return end >= int.MaxValue ? int.MaxValue : (int)end;
    }
}

#endif
