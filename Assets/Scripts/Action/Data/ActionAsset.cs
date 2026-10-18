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

    [Header("Settings")]
    [SerializeField, Tooltip("Loop this action")]
    private bool isLoop = false;

    [SerializeField, Tooltip("若开启：本 Action 已在播放时仍允许被再次选中并从头重播（重新开始并消费本次候选输入）。用于受击等需连续打断自身的 Event 动作；关闭时与 ASM 默认行为一致（同 Action 不重播）。")]
    private bool _allowReenterWhilePlaying = false;

    [SerializeReference, SubclassSelector, Tooltip("All conditions in this list must pass. Then this action can run.")]
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    [SerializeReference, SubclassSelector, Tooltip("自愿退出条件。空列表 = 永不自愿退出（需被其他 Action 抢占或自然播完）。与 EntryConditions 对称但空列表语义相反。")]
    private List<ActionCondition> _exitConditions = new List<ActionCondition>();

    [NonSerialized] private bool _reportedInvalidEntry;
    [NonSerialized] private bool _reportedInvalidExit;

    #region 属性封装
    /// <summary>Inline authoring and runtime source.</summary>
    public ActionTimelineData Timeline => _actionTimeline;

    public Enums.ActionPriority PriorityLayer => _priorityLayer;
    public int PriorityValue => _priorityValue;
    public IReadOnlyList<CancelRule> CancelRules => _cancelRules;
#if UNITY_EDITOR
    internal List<CancelRule> EditorCancelRules => _cancelRules;
#endif
    public IReadOnlyList<TagReference> SelfTags => _selfTags;

    public bool IsLoop { get => isLoop; }

    /// <summary>为 true 时 <see cref="ActionStateManager"/> 可在同一条 Action 仍播放时再次进入（重播 Timeline）。</summary>
    public bool AllowReenterWhilePlaying => _allowReenterWhilePlaying;

    public ActionTriggerMode TriggerMode => _triggerMode;
    public TagReference EventTriggerTag => _eventTriggerTag;
    public ActionStartContextMode StartContextMode => _startContextMode;
    public IReadOnlyList<ActionCondition> EntryConditions => _entryConditions.AsReadOnly();
    public IReadOnlyList<ActionCondition> ExitConditions => _exitConditions.AsReadOnly();

    #endregion

    #region 公共接口

    public ActionInstance CreateActionInstance()
    {
        return new ActionInstance(this);
    }

    /// <summary>Read-only entry check. Poll needs at least one condition; all must pass.</summary>
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
        if (!ValidateConditions(_entryConditions, false, eventTriggered)) return false;
        if (_entryConditions == null || _entryConditions.Count == 0) return eventTriggered;

        List<PlayerInputMatch> inputs = null;
        for (int i = 0; i < _entryConditions.Count; i++)
        {
            ActionCondition condition = _entryConditions[i];
            if (eventTriggered && condition.RequiresPlayerInput) continue;
            if (!condition.TryEvaluate(actor, context, out PlayerInputMatch input)) return false;
            if (!condition.ConsumesInput) continue;
            if (inputs == null) inputs = new List<PlayerInputMatch>();
            inputs.Add(input);
        }
        match = new ActionEntryMatch(inputs);
        return true;
    }

    /// <summary>
    /// All exit state checks must pass. Empty means no voluntary exit.
    /// Consumable input events are entry triggers, not repeatable exit state checks.
    /// </summary>
    public bool CheckExit(Actor actor) => CheckExit(actor, ActionContext.ForSelf(actor));

    public bool CheckExit(Actor actor, ActionContext context)
    {
        if (!ValidateConditions(_exitConditions, true, false)) return false;
        if (_exitConditions == null || _exitConditions.Count == 0) return false;
        for (int i = 0; i < _exitConditions.Count; i++)
            if (!_exitConditions[i].Check(actor, context)) return false;
        return true;
    }
    #endregion

    private bool ValidateConditions(IReadOnlyList<ActionCondition> conditions, bool exit, bool skipInput)
    {
        bool valid = ActionCondition.TryValidateAll(conditions, skipInput, out int invalidIndex, exit);
        bool reported = exit ? _reportedInvalidExit : _reportedInvalidEntry;
        if (!valid && !reported)
            Debug.LogWarning($"[Action] '{name}' {(exit ? "Exit" : "Entry")} condition[{invalidIndex}] is incomplete, unsupported here or invalid. This condition set is rejected.", this);
        if (exit) _reportedInvalidExit = !valid;
        else _reportedInvalidEntry = !valid;
        return valid;
    }

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
        if (_entryConditions == null)
            _entryConditions = new List<ActionCondition>();
        if (_exitConditions == null)
            _exitConditions = new List<ActionCondition>();
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
