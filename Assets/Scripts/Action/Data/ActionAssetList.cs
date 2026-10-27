using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An available Action and its optional, separately gated Neutral entry.</summary>
[Serializable]
public sealed class ActionListEntry
{
    public ActionAsset action;

    [Tooltip("Allow this Action to start when no Action is playing. Disable for combo-only actions. Event registration and cancellation target discovery are unaffected.")]
    public bool allowFromNeutral = true;

    [SerializeReference, SubclassSelector, Tooltip("Neutral-entry conditions. All must pass, in addition to the target Action's Requirements. Empty means no additional restriction. These conditions do not gate Event, External or cancellation requests.")]
    public List<ActionCondition> conditions = new List<ActionCondition>();
}

[CreateAssetMenu(fileName = "ActionList", menuName = "CombatSample/Action/Action List")]
public class ActionAssetList : ScriptableObject
{
    [SerializeField] private List<ActionListEntry> _entries = new List<ActionListEntry>();
    private readonly List<ActionAsset> _allActionsCache = new List<ActionAsset>();
    private readonly List<ActionAsset> _entryActionsSnapshot = new List<ActionAsset>();

    public IReadOnlyList<ActionListEntry> Entries => _entries;
#if UNITY_EDITOR
    internal List<ActionListEntry> EditorEntries => _entries;
#endif

    /// <summary>
    /// Unique registered Actions in authored order, independent of Neutral-entry conditions.
    /// Tracks reference edits without allocating on unchanged queries.
    /// </summary>
    public IReadOnlyList<ActionAsset> GetAllAvailableActions()
    {
        bool changed = _entryActionsSnapshot.Count != _entries.Count;
        for (int i = 0; !changed && i < _entries.Count; i++)
            changed = _entryActionsSnapshot[i] != _entries[i]?.action;
        if (!changed) return _allActionsCache;

        _entryActionsSnapshot.Clear();
        _allActionsCache.Clear();
        foreach (ActionListEntry entry in _entries)
        {
            ActionAsset action = entry?.action;
            _entryActionsSnapshot.Add(action);
            if (action != null && !_allActionsCache.Contains(action))
                _allActionsCache.Add(action);
        }
        return _allActionsCache;
    }
}
