using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Timeline;
using System;
using System.Collections.Generic;
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

    [Header("Cancel")]
    [SerializeField, Tooltip("当此 Action 正在播放时，允许在帧窗口内取消到的目标。替代旧的 Branches 列表。")]
    private List<CancelRule> _cancelRules = new List<CancelRule>();

    [Header("Tags")]
    [SerializeField, Tooltip("此 Action 播放期间写入 Actor Transient TagContainer 的 Tag。CancelRule.AnyWithTag 会据此与规则 Tag 做 Tag.Matches（含层级）匹配。")]
    private List<TagReference> _selfTags = new List<TagReference>();

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

    [SerializeField, Tooltip("若开启：本 Action 已在播放时仍允许被再次选中并从头重播（ClaimEntry + BeginAction）。用于受击等需连续打断自身的 Event 动作；关闭时与 ASM 默认行为一致（同 Action 不重播）。")]
    private bool _allowReenterWhilePlaying = false;

    [SerializeReference, SubclassSelector, Tooltip("All conditions in this list must pass. Then this action can run.")]
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    [SerializeReference, SubclassSelector, Tooltip("自愿退出条件。空列表 = 永不自愿退出（需被其他 Action 抢占或自然播完）。与 EntryConditions 对称但空列表语义相反。")]
    private List<ActionCondition> _exitConditions = new List<ActionCondition>();

    #region 属性封装
    public TimelineAsset TimelineAsset
    {
        get => _timelineAsset;
        set { if (_timelineAsset != value) _timelineAsset = value; }
    }

    public Enums.ActionPriority PriorityLayer => _priorityLayer;
    public int PriorityValue => _priorityValue;
    public IReadOnlyList<CancelRule> CancelRules => _cancelRules;
    public IReadOnlyList<TagReference> SelfTags => _selfTags;

    public bool IsLoop { get => isLoop; }

    /// <summary>为 true 时 <see cref="ActionStateManager"/> 可在同一条 Action 仍播放时再次进入（重播 Timeline）。</summary>
    public bool AllowReenterWhilePlaying => _allowReenterWhilePlaying;

    public ActionTriggerMode TriggerMode => _triggerMode;
    public TagReference EventTriggerTag => _eventTriggerTag;
    public ActionStartContextMode StartContextMode => _startContextMode;
    public ActionMotionConfig MotionConfig => _motionConfig;

    public IReadOnlyList<ActionCondition> EntryConditions => _entryConditions.AsReadOnly();
    public IReadOnlyList<ActionCondition> ExitConditions => _exitConditions.AsReadOnly();

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

    /// <summary>
    /// 检查当前 Action 是否满足自愿退出条件（与 <see cref="CheckEntry"/> 对称语义，但空列表返回值相反）。
    /// <para>规则：</para>
    /// <list type="number">
    ///   <item><b>空列表 → 返回 false</b>：永不自愿退出，只能被其他 Action 抢占或自然播完（与 CheckEntry 空列表返回 false 在形式上相同，但语义含义不同——那边是"拒绝进入"，这边是"拒绝离开"）。</item>
    ///   <item>任一 <c>overrideAll = true</c> 的条件满足 → 立即返回 true（强制退出）。</item>
    ///   <item>否则所有 normal 条件全部通过 → 返回 true（正常退出）。</item>
    ///   <item>只有 overrideAll 条件但都未命中 → 返回 false。</item>
    /// </list>
    /// </summary>
    public bool CheckExit(Actor actor)
    {
        if (_exitConditions == null || _exitConditions.Count == 0)
            return false;

        bool hasNormalConditions = false;
        bool allNormalPassed = true;

        for (int i = 0; i < _exitConditions.Count; i++)
        {
            var cond = _exitConditions[i];
            if (cond == null) continue;

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

        return false;
    }

    /// <summary>
    /// 胜选回调：当 ActionStateManager 选中此 Action 并决定真正进入时调用。
    /// 遍历所有 EntryCondition 触发 OnClaim，让带有"消费型"语义的条件（例如 InputSequenceCondition）
    /// 把本次命中的输入从 buffer 中标记为已消费，避免同一组输入驱动下一个 Action（如一段跳→二段跳）。
    /// </summary>
    public void ClaimEntry(Actor actor)
    {
        if (actor == null || _entryConditions == null) return;

        for (int i = 0; i < _entryConditions.Count; i++)
        {
            var cond = _entryConditions[i];
            if (cond == null) continue;
            cond.OnClaim(actor);
        }
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
        if (_exitConditions == null)
            _exitConditions = new List<ActionCondition>();
        if (_cancelRules == null)
            _cancelRules = new List<CancelRule>();
        if (_selfTags == null)
            _selfTags = new List<TagReference>();
    }

    private void MigrateLegacySelfTag()
    {
        if (_legacySelfTag == null)
            return;

        Tag legacy = _legacySelfTag.GetTag();
        if (legacy == null)
            return;

        foreach (var tagRef in _selfTags)
        {
            if (tagRef == null) continue;
            Tag t = tagRef.GetTag();
            if (t != null && t.Id == legacy.Id)
                return;
        }

        _selfTags.Add(_legacySelfTag);
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
    #endregion
}
