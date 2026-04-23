using System.Collections.Generic;
using UnityEngine;
using DeiveEx.TagTree;

[RequireComponent(typeof(Actor))]
public class ActionStateManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActionPlayer _actionPlayer;
    [SerializeField] private Actor _actor;

    [Header("Settings")]
    [SerializeField] private ActionAssetList _actionList;

    private List<ActionAsset> _validCandidatesCache = new List<ActionAsset>(10);
    private readonly List<ActionAsset> _externalCandidatesThisFrame = new List<ActionAsset>(4);

    // ── 事件触发路径 ──
    private Dictionary<int, List<ActionAsset>> _eventActionMap;
    private readonly List<ActionAsset> _eventCandidatesThisFrame = new List<ActionAsset>(4);
    private ActionEventContext _pendingEventContext;

    // ── CancelRule.AnyWithTag 反向索引 ──
    /// <summary>
    /// SelfTag 反向索引：Tag.Id → 所有 SelfTags 中包含该 Tag 的 ActionAsset 列表。
    /// <para>用途：<see cref="CancelRule"/> 的 <c>AnyWithTag</c> 取消规则在运行时通过该索引 O(1) 查表，
    /// 避免每次取消都扫全表（<c>_actionList</c> 可能有几十个 Action）。</para>
    /// <para>来源：扫描 <c>_actionList</c> 的所有 Action，把它们的 <c>SelfTags</c>
    /// 按 Tag.Id 建反向映射。</para>
    /// <para>构建时机：<see cref="Awake"/> 一次性构建，之后不再变更（_actionList 是编辑期配置）。</para>
    /// </summary>
    private Dictionary<int, List<ActionAsset>> _selfTagIndex;

    private void Awake()
    {
        BuildEventMap();
        BuildSelfTagIndex();
    }

    private void OnEnable()
    {
        _actionPlayer.OnActionFinished += HandleActionFinished;
    }

    private void OnDisable()
    {
        _actionPlayer.OnActionFinished -= HandleActionFinished;
    }

    private void Update()
    {
        // 每帧开头先做自愿退出检查：若当前动作的 ExitConditions 判定为 true，则立即 StopAction。
        // 停掉之后，本帧仍可继续 CheckForTransition 选出新 Action（例如 Locomotion 接管），无需等下一帧。
        var currentInst = _actionPlayer.CurrentAction;
        if (currentInst != null && currentInst.Config.CheckExit(_actor))
        {
            _actionPlayer.StopAction();
        }

        ActionAsset chosen = CheckForTransition();

        if (chosen != null)
        {
            // 如果选中的 Action 与当前正在播放的是同一个（Loop 场景），跳过重复播放
            if (_actionPlayer.CurrentAction != null && _actionPlayer.CurrentAction.Config == chosen)
            {
                // 同一个 Action 正在播放，不重复启动；也不消费输入（本次判定视为沿用而非新进入）
            }
            else
            {
                // 胜选：先让条件消费本次命中的输入（例如 InputSequenceCondition 标记 buffer 为 IsConsumed），
                // 避免同一条输入驱动下一帧/下一个 Action 再次触发（典型：一段跳→二段跳自动连触）。
                chosen.ClaimEntry(_actor);

                ActionEventContext startContext = ResolveStartContext(chosen);
                PlayNewAction(chosen, startContext);
            }
        }

        _externalCandidatesThisFrame.Clear();
        _eventCandidatesThisFrame.Clear();
        _pendingEventContext = default;
    }

    public void RegisterExternalCandidate(ActionAsset action)
    {
        if (action == null) return;
        _externalCandidatesThisFrame.Add(action);
    }

    private void PlayNewAction(ActionAsset actionToPlay, ActionEventContext startContext)
    {
        _actionPlayer.BeginAction(actionToPlay, startContext);
    }

    private ActionEventContext ResolveStartContext(ActionAsset actionToPlay)
    {
        if (actionToPlay == null)
            return default;

        if (actionToPlay.TriggerMode == ActionTriggerMode.Event)
            return _pendingEventContext;

        return actionToPlay.StartContextMode switch
        {
            ActionStartContextMode.LocomotionIntent => BuildContextFromLocomotionIntent(),
            _ => default
        };
    }

    private ActionEventContext BuildContextFromLocomotionIntent()
    {
        if (_actor == null || _actor.movement == null)
            return default;

        var intent = _actor.movement.LocomotionIntent;
        var direction = intent.WorldMoveDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = Vector3.zero;

        return new ActionEventContext
        {
            Direction = direction,
            Magnitude = Mathf.Clamp01(intent.MoveStrength),
        };
    }

    private void StopCurrentAction()
    {
        _actionPlayer.StopAction();
    }

    private void HandleActionFinished(ActionInstance _)
    {
        // Action 正常结束，ActionInstance.OnExit 已恢复 Movement 状态。
        // Locomotion ActionAsset 的条件会在下一帧通过 → ASM 选中它 → 播放。
    }

    #region 事件触发
    /// <summary>
    /// 初始化时构建事件映射表：将 TriggerMode == Event 的 Action 按 EventTriggerTag 分组。
    /// </summary>
    private void BuildEventMap()
    {
        _eventActionMap = new Dictionary<int, List<ActionAsset>>();
        if (_actionList == null) return;

        var allActions = _actionList.GetAllAvailableActions();
        for (int i = 0; i < allActions.Count; i++)
        {
            var action = allActions[i];
            if (action == null || action.TriggerMode != ActionTriggerMode.Event)
                continue;

            if (action.EventTriggerTag == null) continue;
            var tag = action.EventTriggerTag.GetTag();
            if (tag == null) continue;

            if (!_eventActionMap.TryGetValue(tag.Id, out var list))
            {
                list = new List<ActionAsset>(2);
                _eventActionMap[tag.Id] = list;
            }
            list.Add(action);
        }
    }

    /// <summary>
    /// 外部调用入口：发送事件，将匹配的 Action 加入本帧事件候选列表。
    /// </summary>
    public void SendEvent(Tag eventTag, ActionEventContext context = default)
    {
        if (eventTag == null) return;
        if (_eventActionMap == null || !_eventActionMap.TryGetValue(eventTag.Id, out var actions))
            return;

        _pendingEventContext = context;
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action.CheckEntryForEvent(_actor))
            {
                if (!_eventCandidatesThisFrame.Contains(action))
                    _eventCandidatesThisFrame.Add(action);
            }
        }
    }
    #endregion

    #region Action切换
    private ActionAsset CheckForTransition()
    {
        _validCandidatesCache.Clear();

        // 按「有无当前动作」分叉收集基础候选：
        //   - 无当前动作：扫全表（Locomotion/Idle/起手类等需要从 Neutral 态进入的都在这里被选中）
        //   - 有当前动作：只从当前动作的 CancelRules 派生（按帧窗口 + 目标/Tag 过滤）
        var currentInstance = _actionPlayer.CurrentAction;
        if (currentInstance == null)
        {
            if (_actionList != null)
            {
                var allActions = _actionList.GetAllAvailableActions();
                for (int i = 0; i < allActions.Count; i++)
                {
                    // 跳过事件模式的 Action，它们只通过 SendEvent 进入候选
                    if (allActions[i] != null && allActions[i].TriggerMode == ActionTriggerMode.Event)
                        continue;
                    TryAddCandidate(allActions[i]);
                }
            }
        }
        else
        {
            CollectCancelCandidates(currentInstance);
        }

        // 事件候选：两种分叉下都参与（它们是独立的插入通道，不受 CancelRules 约束）
        for (int i = 0; i < _eventCandidatesThisFrame.Count; i++)
            TryAddCandidate(_eventCandidatesThisFrame[i]);

        // 外部候选：同上
        for (int i = 0; i < _externalCandidatesThisFrame.Count; i++)
            TryAddCandidate(_externalCandidatesThisFrame[i]);

        if (_validCandidatesCache.Count > 0)
            return SelectHighestPriorityAction(_validCandidatesCache);
        return null;
    }

    /// <summary>
    /// 从当前正在播放的动作的 <c>CancelRules</c> 中派生本帧候选。
    /// <para>流程：遍历规则 → 检查 <c>window.ContainsFrame(CurrentFrame, TotalFrames)</c> → 按 targetKind 分发：</para>
    /// <list type="bullet">
    ///   <item><b>SpecificAction</b>：直接把 <c>specificTarget</c> 送入 <see cref="TryAddCandidate"/>。</item>
    ///   <item><b>AnyWithTag</b>：用 <c>_selfTagIndex</c> 查表得到所有 SelfTags 含此 Tag 的 Action，逐一送入候选。</item>
    ///   <item><b>Any</b>：把全表（跳过 Event 模式）送入候选。</item>
    /// </list>
    /// <para>注意：是否最终进入仍由目标 Action 的 <c>CheckEntry</c>（EntryConditions）决定，CancelRule 仅负责"递交候选"。</para>
    /// </summary>
    private void CollectCancelCandidates(ActionInstance currentInstance)
    {
        var rules = currentInstance.Config.CancelRules;
        if (rules == null || rules.Count == 0) return;

        int currentFrame = _actionPlayer.CurrentFrame;
        int totalFrames = _actionPlayer.TotalFrames;

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule == null) continue;
            if (!rule.window.ContainsFrame(currentFrame, totalFrames)) continue;

            switch (rule.targetKind)
            {
                case CancelTargetKind.SpecificAction:
                    TryAddCandidate(rule.specificTarget);
                    break;

                case CancelTargetKind.AnyWithTag:
                    if (rule.targetTag == null) break;
                    var tag = rule.targetTag.GetTag();
                    if (tag == null) break;
                    if (_selfTagIndex != null && _selfTagIndex.TryGetValue(tag.Id, out var actions))
                    {
                        for (int j = 0; j < actions.Count; j++)
                            TryAddCandidate(actions[j]);
                    }
                    break;

                case CancelTargetKind.Any:
                    if (_actionList != null)
                    {
                        var allActions = _actionList.GetAllAvailableActions();
                        for (int j = 0; j < allActions.Count; j++)
                        {
                            if (allActions[j] != null && allActions[j].TriggerMode == ActionTriggerMode.Event)
                                continue;
                            TryAddCandidate(allActions[j]);
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// 构建 <see cref="_selfTagIndex"/>：Tag.Id → SelfTags 中包含该 Tag 的 ActionAsset 列表。
    /// 在 <see cref="Awake"/> 一次性调用。
    /// </summary>
    private void BuildSelfTagIndex()
    {
        _selfTagIndex = new Dictionary<int, List<ActionAsset>>();
        if (_actionList == null) return;

        var allActions = _actionList.GetAllAvailableActions();
        for (int i = 0; i < allActions.Count; i++)
        {
            var action = allActions[i];
            if (action == null) continue;

            var selfTags = action.SelfTags;
            if (selfTags == null || selfTags.Count == 0) continue;

            for (int k = 0; k < selfTags.Count; k++)
            {
                var tagRef = selfTags[k];
                if (tagRef == null) continue;
                var tag = tagRef.GetTag();
                if (tag == null) continue;

                if (!_selfTagIndex.TryGetValue(tag.Id, out var list))
                {
                    list = new List<ActionAsset>(4);
                    _selfTagIndex[tag.Id] = list;
                }
                if (!list.Contains(action))
                    list.Add(action);
            }
        }
    }

    private void TryAddCandidate(ActionAsset action)
    {
        if (action == null || _actor == null) return;

        // 事件模式的 Action 使用事件专用条件检查（跳过输入条件）
        bool passed = action.TriggerMode == ActionTriggerMode.Event
            ? action.CheckEntryForEvent(_actor)
            : action.CheckEntry(_actor);

        if (!passed) return;
        if (!_validCandidatesCache.Contains(action))
            _validCandidatesCache.Add(action);
    }

    private ActionAsset SelectHighestPriorityAction(List<ActionAsset> actions)
    {
        if (actions == null || actions.Count == 0) return null;
        if (actions.Count == 1) return actions[0];

        int bestLayerInt = (int)actions[0].PriorityLayer;
        for (int i = 1; i < actions.Count; i++)
        {
            int layer = (int)actions[i].PriorityLayer;
            if (layer > bestLayerInt)
                bestLayerInt = layer;
        }

        ActionAsset best = null;
        int bestValue = int.MinValue;
        for (int i = 0; i < actions.Count; i++)
        {
            var a = actions[i];
            if ((int)a.PriorityLayer != bestLayerInt) continue;
            if (best == null || a.PriorityValue > bestValue)
            {
                best = a;
                bestValue = a.PriorityValue;
            }
        }

        return best ?? actions[0];
    }
    #endregion
}
