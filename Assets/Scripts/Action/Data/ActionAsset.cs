using UnityEngine;
using UnityEngine.Serialization;
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
    [Header("Action Authoring")]
    [SerializeField, Tooltip("Inline fixed-frame timeline data used by ActionRuntime.")]
    private ActionTimelineData _actionTimeline = new ActionTimelineData();

    [Header("Priority")]
    [FormerlySerializedAs("_priority")]
    [SerializeField, Tooltip("Coarse tier: pick the highest layer first.")]
    private Enums.ActionPriority _priorityLayer = Enums.ActionPriority.Normal;

    [SerializeField, Tooltip("Within the same layer, higher wins.")]
    private int _priorityValue;

    [SerializeField, Tooltip("当此 Action 正在播放时，允许在帧窗口内取消到的目标。替代旧的 Branches 列表。")]
    private List<CancelRule> _cancelRules = new List<CancelRule>();

    [SerializeField, Tooltip("此 Action 播放期间写入 Actor Transient TagContainer 的 Tag。CancelRule.AnyWithTag 会据此与规则 Tag 做 Tag.Matches（含层级）匹配。")]
    private List<TagReference> _selfTags = new List<TagReference>();

    [Header("Trigger")]
    [SerializeField, Tooltip("Poll = 每帧轮询条件；Event = 仅由 SendEvent 触发")]
    private ActionTriggerMode _triggerMode = ActionTriggerMode.Poll;

    [SerializeField, Tooltip("仅 Event 模式生效，匹配 SendEvent 传入的 Tag")]
    private TagReference _eventTriggerTag;

    [SerializeField, Tooltip("Action 开始时如何采样上下文快照。Event 模式通常直接使用 SendEvent 传入的数据。")]
    private ActionStartContextMode _startContextMode = ActionStartContextMode.None;

    [SerializeField] private ActionMotionPolicy _motionPolicy = new ActionMotionPolicy();
    public ActionMotionPolicy MotionPolicy => _motionPolicy;

    [Header("Settings")]
    [SerializeField, Tooltip("Loop this action")]
    private bool isLoop = false;

    [SerializeField, Tooltip("若开启：本 Action 已在播放时仍允许被再次选中并从头重播（重新开始并消费本次候选输入）。用于受击等需连续打断自身的 Event 动作；关闭时与 ASM 默认行为一致（同 Action 不重播）。")]
    private bool _allowReenterWhilePlaying = false;

    // Keep the serialized name so existing conditions and managed-reference IDs survive the rename.
    [SerializeReference, SubclassSelector, Tooltip("Requirements checked for every entry. All must pass; empty means no additional restriction. Event requests retain their existing player-input exemption.")]
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    [NonSerialized] private bool _reportedInvalidEntry;

    #region 属性封装
    /// <summary>Inline authoring and runtime source.</summary>
    public ActionTimelineData Timeline => _actionTimeline;

    public Enums.ActionPriority PriorityLayer => _priorityLayer;
    public int PriorityValue => _priorityValue;
    public IReadOnlyList<CancelRule> CancelRules => _cancelRules;
#if UNITY_EDITOR
    internal List<CancelRule> EditorCancelRules => _cancelRules;
    internal List<ActionCondition> EditorRequirements => _entryConditions;
#endif
    public IReadOnlyList<TagReference> SelfTags => _selfTags;

    public bool IsLoop { get => isLoop; }

    /// <summary>为 true 时 <see cref="ActionStateManager"/> 可在同一条 Action 仍播放时再次进入（重播 Timeline）。</summary>
    public bool AllowReenterWhilePlaying => _allowReenterWhilePlaying;

    public ActionTriggerMode TriggerMode => _triggerMode;
    public TagReference EventTriggerTag => _eventTriggerTag;
    public ActionStartContextMode StartContextMode => _startContextMode;
    public IReadOnlyList<ActionCondition> Requirements => _entryConditions;

    #endregion

    #region 公共接口

    public ActionInstance CreateActionInstance()
    {
        return new ActionInstance(this);
    }

    /// <summary>Read-only requirement check. Empty means no additional restriction.</summary>
    public bool CheckEntry(Actor actor) => CheckEntry(actor, ActionContext.ForSelf(actor));

    public bool CheckEntry(Actor actor, ActionContext context) =>
        TryMatchEntry(actor, context, false, out _);

    /// <summary>Event submissions supply their trigger; only player-input checks are skipped.</summary>
    public bool CheckEntryForEvent(Actor actor) => CheckEntryForEvent(actor, ActionContext.ForSelf(actor));

    public bool CheckEntryForEvent(Actor actor, ActionContext context) =>
        TryMatchEntry(actor, context, true, out _);

    internal bool TryMatchEntry(Actor actor, ActionContext context, bool eventTriggered, out ActionEntryMatch match)
    {
        match = default;
        bool valid = ActionCondition.TryValidateAll(_entryConditions, eventTriggered, out int invalidIndex);
        if (!valid && !_reportedInvalidEntry)
            Debug.LogWarning($"[Action] '{name}' requirement[{invalidIndex}] is incomplete or invalid. This condition set is rejected.", this);
        _reportedInvalidEntry = !valid;
        return valid && ActionCondition.TryMatchAll(_entryConditions, actor, context, eventTriggered, out match);
    }
    #endregion

    #region 生命周期和序列化

    private void OnEnable()
    {
        EnsureLists();
        EnsureActionTimeline();
    }

    private void OnValidate()
    {
        EnsureLists();
        EnsureActionTimeline();
    }

    public void OnBeforeSerialize()
    {
        EnsureActionTimeline();
    }

    public void OnAfterDeserialize()
    {
        EnsureLists();
        EnsureActionTimeline();
    }

    private void EnsureLists()
    {
        _motionPolicy ??= new ActionMotionPolicy();
        if (_entryConditions == null)
            _entryConditions = new List<ActionCondition>();
        if (_cancelRules == null)
            _cancelRules = new List<CancelRule>();
        if (_selfTags == null)
            _selfTags = new List<TagReference>();
    }

    private void EnsureActionTimeline()
    {
        if (_actionTimeline == null)
            _actionTimeline = new ActionTimelineData();
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
    #endregion
}
