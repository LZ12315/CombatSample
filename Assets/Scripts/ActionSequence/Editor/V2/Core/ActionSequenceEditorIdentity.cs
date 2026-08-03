#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public enum ActionSequenceEditorIdentityTargetStatus
{
    Supported,
    Unsupported,
}

public enum ActionSequenceEditorIdentityIssueType
{
    Missing,
    Malformed,
    Duplicate,
}

public enum ActionSequenceEditorIdentityItemKind
{
    Track,
    Clip,
    LegacyClip,
}

public sealed class ActionSequenceEditorIdentityIssue
{
    public ActionSequenceEditorIdentityIssue(
        ActionSequenceEditorIdentityIssueType issueType,
        ActionSequenceEditorIdentityItemKind itemKind,
        int trackIndex,
        int clipIndex,
        int legacyClipIndex,
        string currentId)
    {
        IssueType = issueType;
        ItemKind = itemKind;
        TrackIndex = trackIndex;
        ClipIndex = clipIndex;
        LegacyClipIndex = legacyClipIndex;
        CurrentId = currentId;
    }

    public ActionSequenceEditorIdentityIssueType IssueType { get; }
    public ActionSequenceEditorIdentityItemKind ItemKind { get; }
    public int TrackIndex { get; }
    public int ClipIndex { get; }
    public int LegacyClipIndex { get; }
    public string CurrentId { get; }

    public string GetLocationLabel()
    {
        return ItemKind switch
        {
            ActionSequenceEditorIdentityItemKind.Track => $"Track {TrackIndex}",
            ActionSequenceEditorIdentityItemKind.Clip => $"Track {TrackIndex}, Clip {ClipIndex}",
            ActionSequenceEditorIdentityItemKind.LegacyClip => $"Legacy Clip {LegacyClipIndex}",
            _ => ItemKind.ToString(),
        };
    }
}

public sealed class ActionSequenceEditorIdentityValidationResult
{
    private readonly List<ActionSequenceEditorIdentityIssue> _issues = new List<ActionSequenceEditorIdentityIssue>();

    public ActionSequenceEditorIdentityValidationResult(ActionSequenceEditorIdentityTargetStatus status)
    {
        Status = status;
    }

    public ActionSequenceEditorIdentityTargetStatus Status { get; }
    public IReadOnlyList<ActionSequenceEditorIdentityIssue> Issues => _issues;
    public bool IsSupported => Status == ActionSequenceEditorIdentityTargetStatus.Supported;
    public bool HasIssues => _issues.Count > 0;
    public bool HasRepairableInvalidIds => MalformedCount > 0 || DuplicateCount > 0;
    public int MissingCount { get; private set; }
    public int MalformedCount { get; private set; }
    public int DuplicateCount { get; private set; }

    internal void Add(ActionSequenceEditorIdentityIssue issue)
    {
        _issues.Add(issue);
        switch (issue.IssueType)
        {
            case ActionSequenceEditorIdentityIssueType.Missing:
                MissingCount++;
                break;
            case ActionSequenceEditorIdentityIssueType.Malformed:
                MalformedCount++;
                break;
            case ActionSequenceEditorIdentityIssueType.Duplicate:
                DuplicateCount++;
                break;
        }
    }
}

public static class ActionSequenceEditorIdentity
{
    public const string UpgradeUndoName = "Upgrade ActionSequence Editor Identity";
    public const string RepairUndoName = "Repair ActionSequence Editor Identity";

    public static int UpgradeMissingIds(Object target)
    {
        if (!TryGetSequenceData(target, out ActionSequenceData data))
            return 0;

        var missing = new List<ItemReference>();
        HashSet<string> usedIds = CollectExistingIds(data);
        Traverse(data, item =>
        {
            if (string.IsNullOrEmpty(item.EditorId))
                missing.Add(item);
        });

        if (missing.Count == 0)
            return 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UpgradeUndoName);
        Undo.RecordObject(target, UpgradeUndoName);

        for (int i = 0; i < missing.Count; i++)
            missing[i].SetEditorId(GenerateUniqueId(usedIds));

        EditorUtility.SetDirty(target);
        Undo.CollapseUndoOperations(undoGroup);
        return missing.Count;
    }

    public static int UpgradeMissingIdsWithoutUndo(Object target)
    {
        if (!TryGetSequenceData(target, out ActionSequenceData data))
            return 0;

        var missing = new List<ItemReference>();
        HashSet<string> usedIds = CollectExistingIds(data);
        Traverse(data, item =>
        {
            if (string.IsNullOrEmpty(item.EditorId))
                missing.Add(item);
        });

        for (int i = 0; i < missing.Count; i++)
            missing[i].SetEditorId(GenerateUniqueId(usedIds));

        return missing.Count;
    }

    public static ActionSequenceEditorIdentityValidationResult Validate(Object target)
    {
        if (!TryGetSequenceData(target, out ActionSequenceData data))
            return new ActionSequenceEditorIdentityValidationResult(ActionSequenceEditorIdentityTargetStatus.Unsupported);

        var result = new ActionSequenceEditorIdentityValidationResult(ActionSequenceEditorIdentityTargetStatus.Supported);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        Traverse(data, item =>
        {
            string id = item.EditorId;
            if (string.IsNullOrEmpty(id))
            {
                result.Add(item.CreateIssue(ActionSequenceEditorIdentityIssueType.Missing));
                return;
            }

            if (!IsValidEditorId(id))
            {
                result.Add(item.CreateIssue(ActionSequenceEditorIdentityIssueType.Malformed));
                return;
            }

            if (!seen.Add(id))
                result.Add(item.CreateIssue(ActionSequenceEditorIdentityIssueType.Duplicate));
        });

        return result;
    }

    public static int RepairInvalidIds(Object target)
    {
        if (!TryGetSequenceData(target, out ActionSequenceData data))
            return 0;

        var repair = new List<ItemReference>();
        var usedValidIds = new HashSet<string>(StringComparer.Ordinal);

        Traverse(data, item =>
        {
            string id = item.EditorId;
            if (string.IsNullOrEmpty(id) || !IsValidEditorId(id))
            {
                repair.Add(item);
                return;
            }

            if (!usedValidIds.Add(id))
                repair.Add(item);
        });

        if (repair.Count == 0)
            return 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(RepairUndoName);
        Undo.RecordObject(target, RepairUndoName);

        for (int i = 0; i < repair.Count; i++)
            repair[i].SetEditorId(GenerateUniqueId(usedValidIds));

        EditorUtility.SetDirty(target);
        Undo.CollapseUndoOperations(undoGroup);
        return repair.Count;
    }

    public static bool AssignNewIdToCreatedItem(Object target, object item)
    {
        if (item == null || !TryGetSequenceData(target, out ActionSequenceData data))
            return false;

        HashSet<string> usedIds = CollectExistingIds(data);
        string id = GenerateUniqueId(usedIds);

        if (item is ActionSequenceTrackDefinition track)
        {
            track.EditorSetEditorId(id);
            return true;
        }

        if (item is ActionSequenceClipDefinition clip)
        {
            clip.EditorSetEditorId(id);
            return true;
        }

        return false;
    }

    public static bool IsValidEditorId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length != 32)
            return false;

        return Guid.TryParseExact(id, "N", out Guid parsed) && parsed.ToString("N") == id;
    }

    private static bool TryGetSequenceData(Object target, out ActionSequenceData data)
    {
        data = null;
        if (target is ActionAsset actionAsset)
        {
            if (!actionAsset.UsesSequence)
                return false;

            data = actionAsset.SequenceData;
            return data != null;
        }

        if (target is ActionSequenceAsset sequenceAsset)
        {
            data = sequenceAsset.Data;
            return data != null;
        }

        return false;
    }

    private static HashSet<string> CollectExistingIds(ActionSequenceData data)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Traverse(data, item =>
        {
            if (!string.IsNullOrEmpty(item.EditorId))
                ids.Add(item.EditorId);
        });
        return ids;
    }

    private static string GenerateUniqueId(HashSet<string> usedIds)
    {
        string id;
        do
        {
            id = Guid.NewGuid().ToString("N");
        }
        while (!usedIds.Add(id));

        return id;
    }

    private static void Traverse(ActionSequenceData data, Action<ItemReference> visitor)
    {
        if (data == null || visitor == null)
            return;

        List<ActionSequenceTrackDefinition> tracks = data.EditorTracks;
        if (tracks != null)
        {
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                ActionSequenceTrackDefinition track = tracks[trackIndex];
                if (track == null)
                    continue;

                visitor(ItemReference.ForTrack(track, trackIndex));

                List<ActionSequenceClipDefinition> clips = track.EditorClips;
                if (clips == null)
                    continue;

                for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                {
                    ActionSequenceClipDefinition clip = clips[clipIndex];
                    if (clip != null)
                        visitor(ItemReference.ForClip(clip, trackIndex, clipIndex));
                }
            }
        }

        List<ActionSequenceClipDefinition> legacyClips = data.EditorClips;
        if (legacyClips == null)
            return;

        for (int legacyClipIndex = 0; legacyClipIndex < legacyClips.Count; legacyClipIndex++)
        {
            ActionSequenceClipDefinition clip = legacyClips[legacyClipIndex];
            if (clip != null)
                visitor(ItemReference.ForLegacyClip(clip, legacyClipIndex));
        }
    }

    private readonly struct ItemReference
    {
        private readonly ActionSequenceTrackDefinition _track;
        private readonly ActionSequenceClipDefinition _clip;

        private ItemReference(
            ActionSequenceEditorIdentityItemKind itemKind,
            ActionSequenceTrackDefinition track,
            ActionSequenceClipDefinition clip,
            int trackIndex,
            int clipIndex,
            int legacyClipIndex)
        {
            ItemKind = itemKind;
            _track = track;
            _clip = clip;
            TrackIndex = trackIndex;
            ClipIndex = clipIndex;
            LegacyClipIndex = legacyClipIndex;
        }

        public ActionSequenceEditorIdentityItemKind ItemKind { get; }
        public int TrackIndex { get; }
        public int ClipIndex { get; }
        public int LegacyClipIndex { get; }
        public string EditorId => _track != null ? _track.EditorId : _clip.EditorId;

        public static ItemReference ForTrack(ActionSequenceTrackDefinition track, int trackIndex)
        {
            return new ItemReference(ActionSequenceEditorIdentityItemKind.Track, track, null, trackIndex, -1, -1);
        }

        public static ItemReference ForClip(ActionSequenceClipDefinition clip, int trackIndex, int clipIndex)
        {
            return new ItemReference(ActionSequenceEditorIdentityItemKind.Clip, null, clip, trackIndex, clipIndex, -1);
        }

        public static ItemReference ForLegacyClip(ActionSequenceClipDefinition clip, int legacyClipIndex)
        {
            return new ItemReference(ActionSequenceEditorIdentityItemKind.LegacyClip, null, clip, -1, -1, legacyClipIndex);
        }

        public void SetEditorId(string id)
        {
            if (_track != null)
            {
                _track.EditorSetEditorId(id);
                return;
            }

            _clip.EditorSetEditorId(id);
        }

        public ActionSequenceEditorIdentityIssue CreateIssue(ActionSequenceEditorIdentityIssueType issueType)
        {
            return new ActionSequenceEditorIdentityIssue(issueType, ItemKind, TrackIndex, ClipIndex, LegacyClipIndex, EditorId);
        }
    }
}
#endif
