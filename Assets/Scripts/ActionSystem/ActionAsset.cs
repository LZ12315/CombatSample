using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Timeline;
using System;
using System.Collections.Generic;
using CombatSample.Consts;
using DeiveEx.TagTree;

public enum ActionTriggerMode
{
    Poll  = 0,  // 轮询触发（攻击、翻滚、跳跃...）
    Event = 1,  // 事件触发（受击、击飞、弹反...）
}

public enum ActionStartContextMode
{
    None = 0,
    LocomotionIntent = 1,
}

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

    [Header("Trigger")]
    [SerializeField, Tooltip("Poll = 每帧轮询条件；Event = 仅由 SendEvent 触发")]
    private ActionTriggerMode _triggerMode = ActionTriggerMode.Poll;

    [SerializeField, Tooltip("仅 Event 模式生效，匹配 SendEvent 传入的 Tag")]
    private TagReference _eventTriggerTag;

    [SerializeField, Tooltip("Action 开始时如何采样上下文快照。Event 模式通常直接使用 SendEvent 传入的数据。")]
    private ActionStartContextMode _startContextMode = ActionStartContextMode.None;

    [Header("Motion")]
    [SerializeField, Tooltip("整招级运动策略：RootMotion 模式、压制 Locomotion、起手朝向、重力倍率")]
    private ActionMotionConfig _motionConfig = ActionMotionConfig.Default;

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

    public ActionTriggerMode TriggerMode => _triggerMode;
    public TagReference EventTriggerTag => _eventTriggerTag;
    public ActionStartContextMode StartContextMode => _startContextMode;
    public ActionMotionConfig MotionConfig => _motionConfig;

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

    /// <summary>
    /// 事件路径专用的条件检查：跳过输入相关条件（InputStateCondition / InputSequenceCondition），
    /// 其余条件正常检查。用于 SendEvent 触发时的候选筛选。
    /// </summary>
    public bool CheckEntryForEvent(Actor actor)
    {
        if (_entryConditions == null || _entryConditions.Count == 0)
            return true; // 事件 Action 无条件时默认通过（与 Poll 不同）

        bool hasNormalConditions = false;
        bool allNormalPassed = true;

        for (int i = 0; i < _entryConditions.Count; i++)
        {
            var cond = _entryConditions[i];

            // 跳过输入相关条件
            if (cond is InputStateCondition || cond is InputSequenceCondition)
                continue;

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
            return allNormalPassed;

        // 所有条件都被跳过了（全是输入条件），视为通过
        return true;
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
