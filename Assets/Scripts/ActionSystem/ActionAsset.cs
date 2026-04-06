using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Timeline;
using System;
using System.Collections.Generic;
using CombatSample.Consts;
using DeiveEx.TagTree;

public class ActionAsset : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("Core")]
    [SerializeField, Tooltip("Main Timeline asset")]
    private TimelineAsset _timelineAsset;

    [Header("Priority")]
    [FormerlySerializedAs("_priority")]
    [SerializeField, Tooltip("Coarse tier: pick the highest layer first.")]
    private Enums.ActionPriority _priorityLayer = Enums.ActionPriority.Normal;

    [SerializeField, Tooltip("Within the same layer, higher wins.")]
    private int _priorityValue;

    [Header("Branches")]
    [SerializeField, Tooltip("Derived actions polled only while this action is playing. Not global entrypoints.")]
    private List<ActionAsset> _branches = new List<ActionAsset>();

    [Header("Tags")]
    [SerializeField, Tooltip("Tags applied while this action plays (per container).")]
    private List<ActionRuntimeTagBinding> _selfTags = new List<ActionRuntimeTagBinding>();

    [SerializeField, HideInInspector, FormerlySerializedAs("_selfTag")]
    private TagReference _legacySelfTag;

    [Header("Settings")]
    [SerializeField, Tooltip("Loop this action")]
    private bool isLoop = false;

    [SerializeReference, SubclassSelector, Tooltip("All conditions in this list must pass. Then this action can run.")]
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    #region 属性封装
    public TimelineAsset TimelineAsset
    {
        get => _timelineAsset;
        set { if (_timelineAsset != value) _timelineAsset = value; }
    }

    public Enums.ActionPriority PriorityLayer => _priorityLayer;
    public int PriorityValue => _priorityValue;
    public IReadOnlyList<ActionAsset> Branches => _branches;
    public IReadOnlyList<ActionRuntimeTagBinding> SelfTags => _selfTags;

    public bool IsLoop { get => isLoop; }

    public IReadOnlyList<ActionCondition> EntryConditions => _entryConditions.AsReadOnly();

    #endregion

    #region 公共接口

    public void SetTimelineAsset(TimelineAsset timelineAsset)
    {
        _timelineAsset = timelineAsset;
        MarkDirty();
    }

    public ActionInstance CreateActionInstance()
    {
        return new ActionInstance(this);
    }

    /// <summary>
    /// 检查角色当前状态是否满足准入条件 (无状态极速验证)
    /// </summary>
    public bool CheckEntry(Actor actor)
    {
        if (_entryConditions == null || _entryConditions.Count == 0)
            return false;

        bool hasNormalConditions = false;
        bool allNormalPassed = true;

        for (int i = 0; i < _entryConditions.Count; i++)
        {
            var cond = _entryConditions[i];

            bool isMet = cond.Check(actor);

            if (cond.overrideAll)
            {
                if (isMet)
                    return true;
            }
            else
            {
                hasNormalConditions = true;
                if (!isMet)
                    allNormalPassed = false;
            }
        }

        if (hasNormalConditions)
        {
            return allNormalPassed;
        }

        return false;
    }
    #endregion

    #region 生命周期和序列化

    private void OnEnable()
    {
        EnsureLists();
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        EnsureLists();
        MigrateLegacySelfTag();
    }

    private void EnsureLists()
    {
        if (_entryConditions == null)
            _entryConditions = new List<ActionCondition>();
        if (_branches == null)
            _branches = new List<ActionAsset>();
        if (_selfTags == null)
            _selfTags = new List<ActionRuntimeTagBinding>();
    }

    private void MigrateLegacySelfTag()
    {
        if (_legacySelfTag == null)
            return;

        Tag legacy = _legacySelfTag.GetTag();
        if (legacy == null)
            return;

        foreach (var b in _selfTags)
        {
            if (b?.tag == null) continue;
            Tag t = b.tag.GetTag();
            if (t != null && t.Id == legacy.Id)
                return;
        }

        _selfTags.Add(new ActionRuntimeTagBinding
        {
            tag = _legacySelfTag,
            targetContainer = ActorTagContainerType.Transient
        });
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
    #endregion
}
