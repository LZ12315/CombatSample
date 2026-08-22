using System;
using System.Collections.Generic;
using UnityEngine;
using DeiveEx.TagTree;

/// <summary>
/// Action 状态管理器：在固定 Tick 收集候选 Action（Neutral / CancelRule / Event / External 请求），
/// 统一优先级仲裁后播放。External 和 Event 请求仅登记，在下一次 <see cref="DecideAction"/> 中裁决。
/// 有当前 Action 时 External 必须匹配当前帧打开的 <see cref="CancelRule"/>，不绕过取消规则。
/// </summary>
[RequireComponent(typeof(Actor))]
public class ActionStateManager : MonoBehaviour
{
    private enum ActionCandidateOrigin
    {
        Poll = 0,
        Event = 1,
        External = 2,
    }

    private sealed class ExternalActionRequest
    {
        public ActionAsset Action;
        public ActionContext Context;
        public Action<bool> Callback;
        public int Order;
        private bool _completed;

        public void Complete(bool started, UnityEngine.Object logContext)
        {
            if (_completed)
                return;

            _completed = true;
            if (Callback == null)
                return;

            try
            {
                Callback(started);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, logContext);
            }
        }
    }

    private readonly struct ActionCandidate
    {
        public readonly ActionAsset Action;
        public readonly ActionContext Context;
        public readonly ActionCandidateOrigin Origin;
        public readonly int Order;
        public readonly ExternalActionRequest ExternalRequest;

        public ActionCandidate(
            ActionAsset action,
            ActionContext context,
            ActionCandidateOrigin origin,
            int order,
            ExternalActionRequest externalRequest = null)
        {
            Action = action;
            Context = context;
            Origin = origin;
            Order = order;
            ExternalRequest = externalRequest;
        }
    }

    [Header("References")]
    [SerializeField] private Actor _actor;
    private ActionPlayer _actionPlayer => _actor != null ? _actor.actionPlayer : null;

    [Header("Settings")]
    [SerializeField] private ActionAssetList _actionList;

    private readonly List<ActionCandidate> _validCandidatesCache = new List<ActionCandidate>(10);
    private readonly List<ExternalActionRequest> _pendingExternalRequests = new List<ExternalActionRequest>(4);
    private readonly List<ExternalActionRequest> _decidingExternalRequests = new List<ExternalActionRequest>(4);

    private Dictionary<int, List<ActionAsset>> _eventActionMap;
    private readonly List<ActionCandidate> _pendingEventCandidates = new List<ActionCandidate>(4);
    private readonly List<ActionCandidate> _decidingEventCandidates = new List<ActionCandidate>(4);
    private int _nextCandidateOrder;

    /// <summary>当前正在播放的 Action 配置；无当前动作时为 null。供 NodeCanvas 等查询。</summary>
    public ActionAsset CurrentActionAsset => _actionPlayer != null
        ? _actionPlayer.CurrentAction?.Config
        : null;

    private void Awake()
    {
        ResolveActor();
        BuildEventMap();
    }

    private void OnEnable()
    {
        ResolveActor();
        if (_actionPlayer != null)
            _actionPlayer.OnActionFinished += HandleActionFinished;
    }

    private void OnDisable()
    {
        ResolveActor();
        if (_actionPlayer != null)
            _actionPlayer.OnActionFinished -= HandleActionFinished;

        AbortQueuedActionRequests();
    }

    /// <summary>
    /// 登记 External 播放请求（例如 AI / NodeCanvas）。不做即时判定，不播放；
    /// 在下一次 <see cref="DecideAction"/> 中与 Neutral/Cancel/Event 候选统一仲裁。
    /// </summary>
    public void RequestExternalAction(ActionAsset action, ActionContext context, Action<bool> callback)
    {
        var request = new ExternalActionRequest
        {
            Action = action,
            Context = context,
            Callback = callback,
            Order = AllocateCandidateOrder(),
        };

        if (!isActiveAndEnabled)
        {
            request.Complete(false, this);
            return;
        }

        _pendingExternalRequests.Add(request);
    }

    internal void DecideAction()
    {
        if (_actionPlayer == null)
        {
            AbortQueuedActionRequests();
            return;
        }

        PrepareDecidingSnapshot();

        var currentInst = _actionPlayer.CurrentAction;
        ActionCandidate? startedCandidate = null;

        try
        {
            if (currentInst != null && currentInst.Config.CheckExit(_actor, currentInst.Context))
                _actionPlayer.StopAction();

            currentInst = _actionPlayer.CurrentAction;

            _validCandidatesCache.Clear();

            CollectNormalCandidates(currentInst);
            CollectEventCandidatesIntoPool();

            ActionCandidate? chosen = _validCandidatesCache.Count > 0
                ? SelectHighestPriorityAction(_validCandidatesCache)
                : null;

            startedCandidate = TryPlayChosenAction(chosen);
        }
        catch
        {
            AbortQueuedActionRequests();
            throw;
        }

        CompleteDecidingExternalRequests(startedCandidate);
        ClearDecidingSnapshot();
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

    private void CollectActionListNeutralCandidates()
    {
        if (_actionList == null) return;

        var allActions = _actionList.GetAllAvailableActions();
        for (int i = 0; i < allActions.Count; i++)
        {
            var action = allActions[i];
            if (action != null && action.TriggerMode == ActionTriggerMode.Event)
                continue;
            TryAddPollCandidate(action);
        }
    }

    private void CollectExternalNeutralCandidates()
    {
        for (int i = 0; i < _decidingExternalRequests.Count; i++)
        {
            var request = _decidingExternalRequests[i];
            var action = request.Action;
            if (!IsValidExternalAction(action))
                continue;

            TryAddCandidate(new ActionCandidate(
                action,
                request.Context,
                ActionCandidateOrigin.External,
                request.Order,
                request));
        }
    }

    private void CollectCancelCandidates(ActionInstance currentInst)
    {
        CollectCancelRuleCandidates(currentInst);
        CollectExternalCancelCandidates(currentInst);
    }

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
                    TryAddPollCandidate(rule.specificTarget);
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
                                TryAddPollCandidate(candidate);
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
                            TryAddPollCandidate(allActions[j]);
                        }
                    }
                    break;
            }
        }
    }

    private void CollectExternalCancelCandidates(ActionInstance currentInst)
    {
        for (int i = 0; i < _decidingExternalRequests.Count; i++)
        {
            var request = _decidingExternalRequests[i];
            var action = request.Action;
            if (!IsValidExternalAction(action))
                continue;

            if (!CanCancelTo(currentInst, action))
                continue;

            TryAddCandidate(new ActionCandidate(
                action,
                request.Context,
                ActionCandidateOrigin.External,
                request.Order,
                request));
        }
    }

    private static bool IsValidExternalAction(ActionAsset action)
    {
        return action != null && action.TriggerMode != ActionTriggerMode.Event;
    }

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

    private void CollectEventCandidatesIntoPool()
    {
        for (int i = 0; i < _decidingEventCandidates.Count; i++)
            TryAddCandidate(_decidingEventCandidates[i]);
    }

    private ActionCandidate? TryPlayChosenAction(ActionCandidate? chosen)
    {
        if (!chosen.HasValue)
            return null;

        ActionCandidate candidate = chosen.Value;
        ActionAsset action = candidate.Action;
        if (action == null)
            return null;

        bool sameAsCurrent = _actionPlayer.CurrentAction != null &&
                             _actionPlayer.CurrentAction.Config == action;
        if (sameAsCurrent && !action.AllowReenterWhilePlaying)
            return null;

        action.ClaimEntry(_actor);
        PlayNewAction(action, candidate.Context);
        return candidate;
    }

    private void CompleteDecidingExternalRequests(ActionCandidate? startedCandidate)
    {
        for (int i = 0; i < _decidingExternalRequests.Count; i++)
        {
            var request = _decidingExternalRequests[i];
            bool started = startedCandidate.HasValue
                           && ReferenceEquals(startedCandidate.Value.ExternalRequest, request);
            request.Complete(started, this);
        }
    }

    private void PlayNewAction(ActionAsset actionToPlay, ActionContext startContext)
    {
        _actionPlayer.BeginAction(actionToPlay, startContext);
    }

    private bool TryBuildPollContext(ActionAsset action, out ActionContext context)
    {
        context = ActionContext.ForSelf(_actor);
        if (action == null)
            return false;

        switch (action.StartContextMode)
        {
            case ActionStartContextMode.None:
                return true;

            case ActionStartContextMode.LocomotionIntent:
                return TryBuildContextFromLocomotionIntent(action, out context);

            default:
                return true;
        }
    }

    private bool TryBuildContextFromLocomotionIntent(ActionAsset action, out ActionContext context)
    {
        context = ActionContext.ForSelf(_actor);
        if (_actor == null)
            return false;

        ActorMotor motor = _actor.actorMotor;
        if (motor == null)
        {
            Debug.LogError(
                $"Action '{action.name}' uses StartContextMode.LocomotionIntent but Actor '{_actor.name}' has no ActorMotor.",
                this);
            return false;
        }

        if (!motor.HasPendingLocomotionIntent)
            return false;

        LocomotionIntent intent = motor.PendingLocomotionIntent;
        context = context.WithMagnitude(Mathf.Clamp01(intent.MoveStrength));

        Vector3 direction = intent.WorldMoveDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            context = context.WithDirection(direction);

        return true;
    }

    private void HandleActionFinished(ActionInstance _)
    {
        // Action 正常结束，ActionInstance.OnExit 已恢复 ActorMotor 运动策略。
    }

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
    public void SendEvent(Tag eventTag, ActionContext context)
    {
        if (!isActiveAndEnabled)
            return;

        if (eventTag == null) return;
        if (_eventActionMap == null || !_eventActionMap.TryGetValue(eventTag.Id, out var actions))
            return;

        int order = AllocateCandidateOrder();
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var candidate = new ActionCandidate(
                action,
                context,
                ActionCandidateOrigin.Event,
                order);
            _pendingEventCandidates.Add(candidate);
        }
    }

    internal void AbortQueuedActionRequests()
    {
        CompleteExternalRequests(_pendingExternalRequests, false);
        CompleteExternalRequests(_decidingExternalRequests, false);
        _pendingExternalRequests.Clear();
        _decidingExternalRequests.Clear();
        _pendingEventCandidates.Clear();
        _decidingEventCandidates.Clear();
        _validCandidatesCache.Clear();
    }

    private void TryAddPollCandidate(ActionAsset action)
    {
        if (action == null || _actor == null) return;
        if (!TryBuildPollContext(action, out ActionContext context))
            return;

        TryAddCandidate(new ActionCandidate(
            action,
            context,
            ActionCandidateOrigin.Poll,
            AllocateCandidateOrder()));
    }

    private void TryAddCandidate(ActionCandidate candidate)
    {
        if (candidate.Action == null || _actor == null) return;
        if (!CandidatePassesEntry(candidate)) return;
        if (ContainsEquivalentCandidate(candidate)) return;
        _validCandidatesCache.Add(candidate);
    }

    private bool CandidatePassesEntry(ActionCandidate candidate)
    {
        ActionAsset action = candidate.Action;
        if (action == null)
            return false;

        if (!action.CheckContextRequirements(candidate.Context, out string warning))
        {
            Debug.LogError(warning, this);
            return false;
        }

        return candidate.Origin == ActionCandidateOrigin.Event
            ? action.CheckEntryForEvent(_actor, candidate.Context)
            : action.CheckEntry(_actor, candidate.Context);
    }

    private bool ContainsEquivalentCandidate(ActionCandidate candidate)
    {
        for (int i = 0; i < _validCandidatesCache.Count; i++)
        {
            ActionCandidate existing = _validCandidatesCache[i];
            if (existing.Action != candidate.Action)
                continue;

            if (existing.ExternalRequest != null || candidate.ExternalRequest != null)
                return ReferenceEquals(existing.ExternalRequest, candidate.ExternalRequest);

            if (existing.Origin == ActionCandidateOrigin.Poll &&
                candidate.Origin == ActionCandidateOrigin.Poll)
                return true;
        }

        return false;
    }

    private ActionCandidate SelectHighestPriorityAction(List<ActionCandidate> actions)
    {
        if (actions == null || actions.Count == 0) return default;
        if (actions.Count == 1) return actions[0];

        int bestLayerInt = (int)actions[0].Action.PriorityLayer;
        for (int i = 1; i < actions.Count; i++)
        {
            int layer = (int)actions[i].Action.PriorityLayer;
            if (layer > bestLayerInt)
                bestLayerInt = layer;
        }

        ActionCandidate best = default;
        bool hasBest = false;
        int bestValue = int.MinValue;
        int bestOrder = int.MaxValue;
        for (int i = 0; i < actions.Count; i++)
        {
            var candidate = actions[i];
            var action = candidate.Action;
            if ((int)action.PriorityLayer != bestLayerInt) continue;

            bool betterPriority = action.PriorityValue > bestValue;
            bool samePriorityEarlier = action.PriorityValue == bestValue && candidate.Order < bestOrder;
            if (!hasBest || betterPriority || samePriorityEarlier)
            {
                best = candidate;
                hasBest = true;
                bestValue = action.PriorityValue;
                bestOrder = candidate.Order;
            }
        }

        return hasBest ? best : actions[0];
    }

    private int AllocateCandidateOrder()
    {
        return _nextCandidateOrder++;
    }

    private void PrepareDecidingSnapshot()
    {
        _decidingExternalRequests.Clear();
        _decidingEventCandidates.Clear();

        if (_pendingExternalRequests.Count > 0)
        {
            _decidingExternalRequests.AddRange(_pendingExternalRequests);
            _pendingExternalRequests.Clear();
        }

        if (_pendingEventCandidates.Count > 0)
        {
            _decidingEventCandidates.AddRange(_pendingEventCandidates);
            _pendingEventCandidates.Clear();
        }
    }

    private void ClearDecidingSnapshot()
    {
        _decidingExternalRequests.Clear();
        _decidingEventCandidates.Clear();
        _validCandidatesCache.Clear();
    }

    private void CompleteExternalRequests(List<ExternalActionRequest> requests, bool started)
    {
        for (int i = 0; i < requests.Count; i++)
            requests[i]?.Complete(started, this);
    }

    private void ResolveActor()
    {
        if (_actor == null)
            _actor = GetComponent<Actor>();
    }
}
