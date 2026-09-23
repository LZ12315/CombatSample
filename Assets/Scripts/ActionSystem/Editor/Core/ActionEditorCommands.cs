#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class ActionEditorCommands
{
    internal static string NewEditorId() => Guid.NewGuid().ToString("N");

    internal static bool RepairEditorIds(ActionAsset asset, out int repaired, out string message)
    {
        repaired = 0;
        message = string.Empty;
        if (asset == null || asset.Timeline == null)
        {
            message = "Choose an ActionAsset with a ActionRuntime Timeline.";
            return false;
        }

        ActionEditorDocument before = ActionEditorDocument.Build(asset);
        if (before.Readiness != ActionEditorReadiness.IdentityBlocked)
        {
            message = "All Action Editor IDs are already valid and unique.";
            return false;
        }

        repaired = ActionAuthoringIdentity.RepairInvalidIds(asset);
        if (repaired <= 0)
        {
            message = "No repairable Action Editor IDs were found.";
            return false;
        }

        ActionEditorContext.Shared.CompleteIdentityRepair(asset);
        message = $"Repaired {repaired} Action Editor ID{(repaired == 1 ? string.Empty : "s")}.";
        return true;
    }

    internal static bool AddAnimationSegment(ActionAsset asset, AnimationAsset animationAsset, int startFrame, out string message)
    {
        if (!CanAddAnimationSegment(asset, animationAsset, startFrame, out message))
            return false;

        var segment = new AnimationSegment();
        segment.EditorSetEditorId(NewEditorId());
        segment.EditorSetData(startFrame, animationAsset, 0f, animationAsset.Clip.length, 1f);
        Record(asset, "Add Action Animation Segment");
        asset.Timeline.EditorAnimationSegments.Add(segment);
        Commit(asset, ActionEditorChangeFlags.Structure | ActionEditorChangeFlags.Timing,
            Selection(ActionSelectionKind.AnimationSegment, segment.EditorId));
        return true;
    }

    internal static bool CanAddAnimationSegment(ActionAsset asset, AnimationAsset animationAsset, int startFrame, out string message)
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
        segment.EditorSetData(startFrame, animationAsset, 0f, animationAsset.Clip.length, 1f);
        if (WouldAnimationOverlap(asset, segment, null))
        {
            message = "The new AnimationSegment would overlap an existing segment.";
            return false;
        }

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
        Commit(asset, ActionEditorChangeFlags.Structure,
            Selection(ActionSelectionKind.GameplayLane, lane.EditorId));
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
        if (item is HitBoxItem newHitBox) newHitBox.EditorInitializeNewDefaults();
        if (item is PointGameplayItem point) point.EditorSetFrame(frame);
        else if (item is RangeGameplayItem range) range.EditorSetTiming(frame, 1);
        else
        {
            message = "Only PointGameplayItem and RangeGameplayItem types are supported.";
            return null;
        }
        if (ActionGameplayLaneOccupancy.WouldOverlap(lane, null, frame, (long)frame + 1L))
        {
            message = "The new GameplayItem would overlap existing content on this Lane.";
            return null;
        }

        Record(asset, $"Add {itemType.Name}");
        lane.EditorItems.Add(item);
        Commit(asset, ActionEditorChangeFlags.Structure | ActionEditorChangeFlags.Timing,
            Selection(ActionSelectionKind.GameplayItem, item.EditorId));
        return item;
    }

    internal static bool RenameLane(ActionAsset asset, string laneId, string name)
    {
        GameplayLane lane = FindLane(asset, laneId);
        if (lane == null || string.Equals(lane.Name, name ?? string.Empty, StringComparison.Ordinal))
            return false;
        Record(asset, "Rename Action Gameplay Lane");
        lane.EditorSetName(name);
        Commit(asset, ActionEditorChangeFlags.Presentation);
        return true;
    }

    internal static bool SetMuted(ActionAsset asset, string editorId, bool muted)
    {
        ActionEditorDocument document = DocumentFor(asset);
        if (document == null || !document.ById.TryGetValue(editorId, out ActionDocumentEntry entry))
            return false;
        if (entry.Source is GameplayLane existingLane && existingLane.Muted == muted) return false;
        if (entry.Source is GameplayItem existingItem && existingItem.Muted == muted) return false;
        if (!(entry.Source is GameplayLane) && !(entry.Source is GameplayItem)) return false;
        Record(asset, "Set Action Content Mute");
        if (entry.Source is GameplayLane lane) lane.EditorSetMuted(muted);
        else if (entry.Source is GameplayItem item) item.EditorSetMuted(muted);
        else return false;
        Commit(asset, ActionEditorChangeFlags.Content | ActionEditorChangeFlags.Presentation);
        return true;
    }

    internal static bool EnsureItemConfiguration(ActionAsset asset, string editorId)
    {
        ActionEditorDocument document = DocumentFor(asset);
        if (document == null || !document.ById.TryGetValue(editorId, out ActionDocumentEntry entry) ||
            !(entry.Source is GameplayItem item))
            return false;
        bool missing = item is ImpulseItem impulse && impulse.Config == null ||
                       item is HitBoxItem hitBox && (hitBox.Config == null || hitBox.Config.hitboxConfig == null ||
                                                     hitBox.Config.dataConfig == null || hitBox.Config.effects == null) ||
                       item is RootMotionItem root && root.Config == null ||
                       item is SelfRotationItem rotation && rotation.Config == null ||
                       item is VelocityOverrideItem velocity && velocity.Config == null ||
                       item is MotionPolicyItem policy && policy.Config == null ||
                       item is TagItem tag && tag.Config == null;
        if (!missing) return false;
        Record(asset, "Create Action Item Configuration");
        switch (item)
        {
            case ImpulseItem value: value.EditorEnsureConfig(); break;
            case HitBoxItem value: value.EditorEnsureConfig(); break;
            case RootMotionItem value: value.EditorEnsureConfig(); break;
            case SelfRotationItem value: value.EditorEnsureConfig(); break;
            case VelocityOverrideItem value: value.EditorEnsureConfig(); break;
            case MotionPolicyItem value: value.EditorEnsureConfig(); break;
            case TagItem value: value.EditorEnsureConfig(); break;
            default: return false;
        }
        Commit(asset, ActionEditorChangeFlags.Content);
        return true;
    }

    internal static bool AddCancelRule(ActionAsset asset)
    {
        if (asset?.EditorCancelRules == null) return false;
        Record(asset, "Add Action Cancel Rule");
        asset.EditorCancelRules.Add(new CancelRule());
        Commit(asset, ActionEditorChangeFlags.Content);
        return true;
    }

    internal static bool RemoveCancelRule(ActionAsset asset, int index)
    {
        if (asset?.EditorCancelRules == null || index < 0 || index >= asset.EditorCancelRules.Count) return false;
        Record(asset, "Remove Action Cancel Rule");
        asset.EditorCancelRules.RemoveAt(index);
        Commit(asset, ActionEditorChangeFlags.Content);
        return true;
    }

    internal static bool MoveCancelRule(ActionAsset asset, int from, int to)
    {
        if (asset?.EditorCancelRules == null || from < 0 || from >= asset.EditorCancelRules.Count ||
            to < 0 || to >= asset.EditorCancelRules.Count || from == to) return false;
        Record(asset, "Reorder Action Cancel Rules");
        CancelRule rule = asset.EditorCancelRules[from];
        asset.EditorCancelRules.RemoveAt(from);
        asset.EditorCancelRules.Insert(to, rule);
        Commit(asset, ActionEditorChangeFlags.Content);
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
        Commit(asset, ActionEditorChangeFlags.Structure);
        return true;
    }

    internal static bool DeleteSelection(ActionAsset asset, IReadOnlyList<string> ids, bool confirmNonEmptyLane, out string message)
    {
        message = string.Empty;
        if (asset == null || asset.Timeline == null || ids == null || ids.Count == 0)
            return false;

        ActionEditorDocument document = DocumentFor(asset);
        List<ActionDocumentEntry> entries = ids.Where(id => document.ById.TryGetValue(id, out _)).Select(id => document.ById[id]).ToList();
        if (entries.Count == 0)
            return false;
        if (!confirmNonEmptyLane && entries.Any(entry => entry.Source is GameplayLane lane && lane.Items.Count > 0))
        {
            message = "A selected GameplayLane contains items.";
            return false;
        }

        Record(asset, "Delete Action Timeline Content");
        foreach (ActionDocumentEntry entry in entries.Where(entry => entry.Source is AnimationSegment))
            asset.Timeline.EditorAnimationSegments.Remove((AnimationSegment)entry.Source);
        foreach (ActionDocumentEntry entry in entries.Where(entry => entry.Source is GameplayItem))
        {
            GameplayLane lane = FindLane(asset, entry.LaneId);
            lane?.EditorItems.Remove((GameplayItem)entry.Source);
        }
        foreach (ActionDocumentEntry entry in entries.Where(entry => entry.Source is GameplayLane))
            asset.Timeline.EditorGameplayLanes.Remove((GameplayLane)entry.Source);

        Commit(asset, ActionEditorChangeFlags.Structure | ActionEditorChangeFlags.Timing,
            Array.Empty<ActionSelectionValue>());
        return true;
    }

    internal static bool DeleteNullEntry(ActionAsset asset, ActionSelectionKind kind, int laneIndex, int itemIndex, out string message)
    {
        message = string.Empty;
        if (asset?.Timeline == null)
            return false;

        if (kind == ActionSelectionKind.AnimationSegment)
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
        else if (kind == ActionSelectionKind.GameplayLane)
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
        else if (kind == ActionSelectionKind.GameplayItem)
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

        Commit(asset, ActionEditorChangeFlags.Structure | ActionEditorChangeFlags.Timing,
            Array.Empty<ActionSelectionValue>());
        return true;
    }

    internal static bool CommitTimelineOperation(ActionTimelineOperationSnapshot snapshot,
        ActionTimelineOperationInput input, out string message)
    {
        message = string.Empty;
        if (snapshot == null || ActionEditorContext.Shared.CurrentAction != snapshot.Asset)
        {
            message = "The edited Action is no longer the current Timeline Action.";
            return false;
        }
        if (!snapshot.MatchesCurrentSource(out message))
            return false;

        ActionTimelineOperationResult result = snapshot.Evaluate(input);
        message = result?.Message ?? string.Empty;
        if (result == null || result.State != ActionTimelineOperationState.Allowed)
            return false;

        foreach (ActionTimelineOperationCandidate candidate in result.Candidates.Where(value => value.Source is AnimationSegment))
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
        string undoLabel = snapshot.Kind == ActionTimelineOperationKind.Move
            ? "Move Action Timeline Content"
            : snapshot.Kind == ActionTimelineOperationKind.ResizeLeft || snapshot.Kind == ActionTimelineOperationKind.ResizeRight
                ? "Resize Action Gameplay Item"
                : snapshot.Kind == ActionTimelineOperationKind.SetPointTiming || snapshot.Kind == ActionTimelineOperationKind.SetRangeTiming
                    ? "Edit Action Gameplay Timing"
                    : snapshot.Kind == ActionTimelineOperationKind.SetAnimationTiming
                        ? "Edit Action Animation Timing"
                        : "Trim Action Animation Segment";
        Record(asset, undoLabel);

        if (changesLane)
        {
            foreach (ActionTimelineOperationCandidate candidate in result.Candidates.Where(candidate => candidate.Source is GameplayItem))
            {
                GameplayItem item = (GameplayItem)candidate.Source;
                foreach (GameplayLane lane in asset.Timeline.EditorGameplayLanes.Where(lane => lane != null))
                    lane.EditorItems.Remove(item);
            }
            foreach (ActionTimelineOperationCandidate candidate in result.Candidates.Where(candidate => candidate.Source is GameplayItem))
                asset.Timeline.EditorGameplayLanes[candidate.LaneIndex].EditorItems.Add((GameplayItem)candidate.Source);
        }

        foreach (ActionTimelineOperationCandidate candidate in result.Candidates)
        {
            if (candidate.Source is AnimationSegment animation)
                animation.EditorSetData(candidate.StartFrame, candidate.AnimationAsset, candidate.SourceStartTime,
                    candidate.SourceEndTime, candidate.PlayRate);
            else if (candidate.Source is PointGameplayItem point)
                point.EditorSetFrame(candidate.StartFrame);
            else if (candidate.Source is RangeGameplayItem range)
                range.EditorSetTiming(candidate.StartFrame, candidate.DurationFrames);
        }
        ActionEditorChangeFlags flags = ActionEditorChangeFlags.Timing;
        if (changesLane) flags |= ActionEditorChangeFlags.Structure;
        if (snapshot.Kind == ActionTimelineOperationKind.TrimLeft || snapshot.Kind == ActionTimelineOperationKind.TrimRight ||
            snapshot.Kind == ActionTimelineOperationKind.SetAnimationTiming)
            flags |= ActionEditorChangeFlags.Content;
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

    internal static bool Copy(ActionAsset asset, IReadOnlyList<string> ids, out string message)
    {
        return ActionEditorClipboard.Copy(asset, ids, out message);
    }

    internal static bool Paste(ActionAsset asset, int anchorFrame, out string message)
    {
        return ActionEditorClipboard.Paste(asset, anchorFrame, duplicate: false, out message);
    }

    internal static bool Duplicate(ActionAsset asset, IReadOnlyList<string> ids, out string message)
    {
        return ActionEditorClipboard.Duplicate(asset, ids, out message);
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

    internal static void Commit(ActionAsset asset, ActionEditorChangeFlags flags,
        IReadOnlyList<ActionSelectionValue> replacementSelection = null)
    {
        EditorUtility.SetDirty(asset);
        ActionEditorContext.Shared.ApplyAssetChange(asset, flags,
            ActionEditorChangeOrigin.Command, replacementSelection);
    }

    internal static ActionEditorDocument DocumentFor(ActionAsset asset) =>
        asset == ActionEditorContext.Shared.CurrentAction
            ? ActionEditorContext.Shared.Document
            : ActionEditorDocument.Build(asset);

    private static IReadOnlyList<ActionSelectionValue> Selection(ActionSelectionKind kind, string editorId) =>
        new[] { new ActionSelectionValue(kind, editorId) };

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

}

internal static class ActionEditorClipboard
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
        ActionEditorDocument document = ActionEditorCommands.DocumentFor(asset);
        if (document == null || ids == null)
            return false;
        var selectedIds = new HashSet<string>(ids.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
        foreach (ActionDocumentEntry source in document.EntriesInAuthoringOrder())
        {
            if (!source.HasStableSelection || !selectedIds.Contains(source.EditorId))
                continue;
            if (source.Source is AnimationSegment segment)
            {
                Entries.Add(new Entry { Segment = ActionEditorCommands.CloneManaged(segment), Start = source.StartFrame, End = source.RawEndFrameExclusive });
            }
            else if (source.Source is GameplayItem item)
            {
                Entries.Add(new Entry
                {
                    Item = ActionEditorCommands.CloneManaged(item), LaneId = source.LaneId, LaneName = source.LaneName,
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
            message = "The Action clipboard is empty.";
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
            GameplayLane lane = ReferenceEquals(asset, _sourceAsset) ? ActionEditorCommands.FindLane(asset, entry.LaneId) : null;
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

        var newGameplayRanges = new List<(GameplayLane lane, long start, long end, bool pasted)>();
        foreach (GameplayLane lane in asset.Timeline.GameplayLanes)
        {
            if (lane == null)
                continue;
            foreach (GameplayItem item in lane.Items)
            {
                if (ActionGameplayLaneOccupancy.TryGetInterval(item, out long start, out long end))
                    newGameplayRanges.Add((lane, start, end, false));
            }
        }
        foreach (Entry entry in Entries.Where(entry => entry.Item != null))
            newGameplayRanges.Add((laneMap[entry], entry.Start + offset, entry.End + offset, true));
        for (int left = 0; left < newGameplayRanges.Count; left++)
        for (int right = left + 1; right < newGameplayRanges.Count; right++)
        {
            var a = newGameplayRanges[left];
            var b = newGameplayRanges[right];
            if (ReferenceEquals(a.lane, b.lane) && (a.pasted || b.pasted)
                                                  && ActionGameplayLaneOccupancy.Overlaps(a.start, a.end, b.start, b.end))
            {
                message = "Paste would overlap GameplayItems on the same Lane.";
                return false;
            }
        }

        var generatedIds = Entries.ToDictionary(entry => entry, _ => ActionEditorCommands.NewEditorId());

        Undo.RegisterCompleteObjectUndo(asset, duplicate ? "Duplicate Action Timeline Content" : "Paste Action Timeline Content");
        var selected = new List<ActionSelectionValue>();
        foreach (Entry entry in Entries)
        {
            if (entry.Segment != null)
            {
                AnimationSegment clone = ActionEditorCommands.CloneManaged(entry.Segment);
                clone.EditorSetEditorId(generatedIds[entry]);
                clone.EditorSetData((int)(entry.Start + offset), clone.AnimationAsset, clone.SourceStartTime, clone.SourceEndTime, clone.PlayRate);
                asset.Timeline.EditorAnimationSegments.Add(clone);
                selected.Add(new ActionSelectionValue(ActionSelectionKind.AnimationSegment, clone.EditorId));
            }
            else
            {
                GameplayItem clone = ActionEditorCommands.CloneManaged(entry.Item);
                clone.EditorSetEditorId(generatedIds[entry]);
                int start = (int)(entry.Start + offset);
                if (clone is PointGameplayItem point) point.EditorSetFrame(start);
                else if (clone is RangeGameplayItem range) range.EditorSetTiming(start, (int)(entry.End - entry.Start));
                laneMap[entry].EditorItems.Add(clone);
                selected.Add(new ActionSelectionValue(ActionSelectionKind.GameplayItem, clone.EditorId));
            }
        }
        ActionEditorCommands.Commit(asset,
            ActionEditorChangeFlags.Structure | ActionEditorChangeFlags.Timing | ActionEditorChangeFlags.Content,
            selected);
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

#endif
