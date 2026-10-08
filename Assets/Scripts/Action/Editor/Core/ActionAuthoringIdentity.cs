#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

public readonly struct ActionAuthoringIdentityTarget
{
    private readonly Action<string> _set;
    public string Id { get; }
    public string Label { get; }
    public string AuthoringPath { get; }

    public ActionAuthoringIdentityTarget(string id, string label, Action<string> set)
        : this(id, label, string.Empty, set)
    {
    }

    public ActionAuthoringIdentityTarget(string id, string label, string authoringPath, Action<string> set)
    {
        Id = id;
        Label = label;
        AuthoringPath = authoringPath;
        _set = set;
    }

    public void Set(string value) => _set(value);
}

public static class ActionAuthoringIdentity
{
    public static bool IsValidEditorId(string editorId)
    {
        return editorId != null
            && editorId.Length == 32
            && Guid.TryParseExact(editorId, "N", out _);
    }

    public static int RepairInvalidIds(ActionAsset actionAsset)
    {
        if (actionAsset == null || actionAsset.Timeline == null)
            return 0;

        List<ActionAuthoringIdentityTarget> targets = Collect(actionAsset.Timeline);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var repair = new List<ActionAuthoringIdentityTarget>();
        for (int i = 0; i < targets.Count; i++)
        {
            ActionAuthoringIdentityTarget target = targets[i];
            if (!IsValidEditorId(target.Id) || !used.Add(target.Id))
                repair.Add(target);
        }
        if (repair.Count == 0)
            return 0;

        var replacements = new string[repair.Count];
        for (int i = 0; i < repair.Count; i++)
        {
            string id;
            do { id = Guid.NewGuid().ToString("N"); }
            while (!used.Add(id));
            replacements[i] = id;
        }

        Undo.RecordObject(actionAsset, "Repair Action Editor IDs");
        for (int i = 0; i < repair.Count; i++)
            repair[i].Set(replacements[i]);
        EditorUtility.SetDirty(actionAsset);
        return repair.Count;
    }

    internal static List<ActionAuthoringIdentityTarget> Collect(ActionTimelineData timeline)
    {
        var targets = new List<ActionAuthoringIdentityTarget>();
        if (timeline == null)
            return targets;

        IReadOnlyList<AnimationSegment> segments = timeline.AnimationSegments;
        for (int i = 0; segments != null && i < segments.Count; i++)
        {
            AnimationSegment segment = segments[i];
            if (segment != null)
                targets.Add(new ActionAuthoringIdentityTarget(segment.EditorId, $"AnimationSegment {i}", $"AnimationSegments[{i}]", segment.EditorSetEditorId));
        }

        IReadOnlyList<GameplayLane> lanes = timeline.GameplayLanes;
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            GameplayLane lane = lanes[laneIndex];
            if (lane == null)
                continue;
            string lanePath = $"GameplayLanes[{laneIndex}]";
            targets.Add(new ActionAuthoringIdentityTarget(lane.EditorId, $"GameplayLane {laneIndex}", lanePath, lane.EditorSetEditorId));
            IReadOnlyList<GameplayItem> items = lane.Items;
            for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
            {
                GameplayItem item = items[itemIndex];
                if (item != null)
                    targets.Add(new ActionAuthoringIdentityTarget(item.EditorId, $"GameplayLane {laneIndex} Item {itemIndex}", $"{lanePath}.Items[{itemIndex}]", item.EditorSetEditorId));
            }
        }
        return targets;
    }
}
#endif
