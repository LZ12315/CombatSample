using System;
using System.Collections.Generic;
using UnityEngine;
using DeiveEx.TagTree;

/// <summary>
/// Action 状态管理器：每帧收集候选 Action（Neutral / CancelRule / Event / External 请求），
/// 统一优先级仲裁后播放。External 请求仅登记，在本帧 <see cref="LateUpdate"/> 与 ASM 正常流程一起裁决
///（让 NodeCanvas/输入等在 <see cref="Update"/> 先登记，帧末统一仲裁）。
/// 有当前 Action 时 External 必须匹配当前帧打开的 <see cref="CancelRule"/>，不绕过取消规则。
/// </summary>
[RequireComponent(typeof(Actor))]
public class ActionStateManager : MonoBehaviour
{
    private sealed class ExternalActionRequest
    {
        public ActionAsset Action;
        public Action<bool> Callback;
    }

    [Header("References")]
    [SerializeField] private Actor _actor;
    private ActionPlayer _actionPlayer => _actor != null ? _actor.actionPlayer : null;

    [Header("Settings")]
    [SerializeField] private ActionAssetList _actionList;

    private readonly List<ActionAsset> _validCandidatesCache = new List<ActionAsset>(10);
    private readonly List<ExternalActionRequest> _externalRequestsThisFrame = new List<ExternalActionRequest>(4);

    // ── 事件触发路径 ──
    private Dictionary<int, List<ActionAsset>> _eventActionMap;
    private readonly List<ActionAsset> _eventCandidatesThisFrame = new List<ActionAsset>(4);
    private ActionEventContext _pendingEventContext;

    /// <summary>当前正在播放的 Action 配置；无当前动作时为 null。供 NodeCanvas 等查询。</summary>
    public ActionAsset CurrentActionAsset => _actionPlayer != null
        ? _actionPlayer.CurrentAction?.Config
        : null;

    private void Awake()
    {
        BuildEventMap();
    }

    private void OnEnable()
    {
        _actionPlayer.OnActionFinished += HandleActionFinished;
    }

    private void OnDisable()
    {
        _actionPlayer.OnActionFinished -= HandleActionFinished;
    }

    /// <summary>
    /// 登记本帧 External 播放请求（例如 AI / NodeCanvas）。不做即时判定，不播放；
    /// 在 <see cref="LateUpdate"/> 中与 Neutral/Cancel/Event 候选统一仲裁。
    /// </summary>
    public void RequestExternalAction(ActionAsset action, Action<bool> callback)
    {
        _externalRequestsThisFrame.Add(new ExternalActionRequest
        {
            Action = action,
            Callback = callback,
        });
    }

    private void LateUpdate()
    {
        // 自愿退出：停掉之后本帧仍可继续选新 Action
        var currentInst = _actionPlayer.CurrentAction;
        if (currentInst != null && currentInst.Config.CheckExit(_actor))
            _actionPlayer.StopAction();

        currentInst = _actionPlayer.CurrentAction;

        _validCandidatesCache.Clear();

        CollectNormalCandidates(currentInst);
        CollectEventCandidatesIntoPool();

        ActionAsset chosen = _validCandidatesCache.Count > 0
            ? SelectHighestPriorityAction(_validCandidatesCache)
            : null;

        ActionAsset startedAction = TryPlayChosenAction(chosen);

        ResolveExternalRequestResults(startedAction);

        _externalRequestsThisFrame.Clear();
        _eventCandidatesThisFrame.Clear();
        _pendingEventContext = default;
    }

    private void CollectNormalCandidates(ActionInstance currentInst)
    {
        if (currentInst == null)
            CollectNeutralCandidates();
        else
            CollectCancelCandidates(currentInst);
    }

    private void CollectNeutralCandidates()
    {
        CollectActionListNeutralCandidates();
        CollectExternalNeutralCandidates();
    }

    /// <summary>无当前 Action：扫 ActionList，跳过 Event。</summary>
    private void CollectActionListNeutralCandidates()
    {
        if (_actionList == null) return;

        var allActions = _actionList.GetAllAvailableActions();
        for (int i = 0; i < allActions.Count; i++)
        {
            if (allActions[i] != null && allActions[i].TriggerMode == ActionTriggerMode.Event)
                continue;
            TryAddCandidate(allActions[i]);
        }
    }

    /// <summary>无当前 Action：External 只需通过目标自身的 EntryCondition。</summary>
    private void CollectExternalNeutralCandidates()
    {
        for (int i = 0; i < _externalRequestsThisFrame.Count; i++)
        {
            var request = _externalRequestsThisFrame[i];
            var action = request.Action;
            if (!IsValidExternalAction(action))
                continue;

            if (action.CheckEntry(_actor))
                TryAddCandidate(action);
        }
    }

    private void CollectCancelCandidates(ActionInstance currentInst)
    {
        CollectCancelRuleCandidates(currentInst);
        CollectExternalCancelCandidates(currentInst);
    }

    /// <summary>有当前 Action：按 CancelRule 自动展开候选（Specific / AnyWithTag / Any）。</summary>
    private void CollectCancelRuleCandidates(ActionInstance currentInstance)
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
                    if (_actionList == null || rule.targetTag == null) break;
                    {
                        var allActionsForTag = _actionList.GetAllAvailableActions();
                        for (int j = 0; j < allActionsForTag.Count; j++)
                        {
                            var candidate = allActionsForTag[j];
                            if (candidate == null || candidate.TriggerMode == ActionTriggerMode.Event)
                                continue;
                            if (ActionHasSelfTagMatchingRule(candidate, rule.targetTag))
                                TryAddCandidate(candidate);
                        }
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
    /// 有当前 Action：External 指定目标必须先匹配当前帧某条打开的 CancelRule，再通过 EntryCondition。
    /// 不展开候选，只验证“请求的这一招”是否合法。
    /// </summary>
    private void CollectExternalCancelCandidates(ActionInstance currentInst)
    {
        for (int i = 0; i < _externalRequestsThisFrame.Count; i++)
        {
            var request = _externalRequestsThisFrame[i];
            var action = request.Action;
            if (!IsValidExternalAction(action))
                continue;

            if (!CanCancelTo(currentInst, action))
                continue;

            if (action.CheckEntry(_actor))
                TryAddCandidate(action);
        }
    }

    private static bool IsValidExternalAction(ActionAsset action)
    {
        return action != null && action.TriggerMode != ActionTriggerMode.Event;
    }

    /// <summary>
    /// 当前动作在当帧取消窗口内，是否允许取消到 <paramref name="requestedAction"/>。
    /// </summary>
    private bool CanCancelTo(ActionInstance currentInst, ActionAsset requestedAction)
    {
        if (currentInst == null || requestedAction == null)
            return false;

        var rules = currentInst.Config.CancelRules;
        if (rules == null || rules.Count == 0)
            return false;

        int currentFrame = _actionPlayer.CurrentFrame;
        int totalFrames = _actionPlayer.TotalFrames;

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule == null || !rule.window.ContainsFrame(currentFrame, totalFrames))
                continue;

            switch (rule.targetKind)
            {
                case CancelTargetKind.SpecificAction:
                    if (rule.specificTarget == requestedAction)
                        return true;
                    break;

                case CancelTargetKind.AnyWithTag:
                    if (ActionHasSelfTagMatchingRule(requestedAction, rule.targetTag))
                        return true;
                    break;

                case CancelTargetKind.Any:
                    return requestedAction.TriggerMode != ActionTriggerMode.Event;
            }
        }

        return false;
    }

    /// <summary>目标 Action 的 SelfTags 中是否有任意标签匹配规则标签（含层级：子匹配父）。</summary>
    private static bool ActionHasSelfTagMatchingRule(ActionAsset action, TagReference ruleTagRef)
    {
        if (action == null || ruleTagRef == null)
            return false;

        Tag ruleTag = ruleTagRef.GetTag();
        if (ruleTag == null)
            return false;

        var selfTags = action.SelfTags;
        if (selfTags == null) return false;

        for (int i = 0; i < selfTags.Count; i++)
        {
            var selfRef = selfTags[i];
            if (selfRef == null) continue;
            Tag selfTag = selfRef.GetTag();
            if (selfTag == null) continue;
            if (selfTag.Matches(ruleTag))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 将本帧 <see cref="SendEvent"/> 产生的 Event 候选并入仲裁池（不受 CancelRule 限制）。
    /// </summary>
    private void CollectEventCandidatesIntoPool()
    {
        for (int i = 0; i < _eventCandidatesThisFrame.Count; i++)
            TryAddCandidate(_eventCandidatesThisFrame[i]);
    }

    private ActionAsset TryPlayChosenAction(ActionAsset chosen)
    {
        if (chosen == null)
            return null;

        bool sameAsCurrent = _actionPlayer.CurrentAction != null &&
                             _actionPlayer.CurrentAction.Config == chosen;
        if (sameAsCurrent && !chosen.AllowReenterWhilePlaying)
            return null;

        chosen.ClaimEntry(_actor);
        ActionEventContext startContext = ResolveStartContext(chosen);
        PlayNewAction(chosen, startContext);
        return chosen;
    }

    private void ResolveExternalRequestResults(ActionAsset startedAction)
    {
        for (int i = 0; i < _externalRequestsThisFrame.Count; i++)
        {
            var request = _externalRequestsThisFrame[i];
            bool started = request.Action != null
                           && startedAction != null
                           && request.Action == startedAction;
            request.Callback?.Invoke(started);
        }
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
        if (_actor == null || _actor.actorMotor == null)
            return default;

        var intent = _actor.actorMotor.LocomotionIntent;
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

    private void HandleActionFinished(ActionInstance _)
    {
        // Action 正常结束，ActionInstance.OnExit 已恢复 ActorMotor 运动策略。
    }

    #region 事件触发
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

    #region 候选
    private void TryAddCandidate(ActionAsset action)
    {
        if (action == null || _actor == null) return;

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
