using System;
using System.Collections.Generic;
using UnityEngine;
using DeiveEx.TagTree;

/// <summary>
/// Action 状态管理器：在固定 Tick 收集候选 Action（Neutral / CancelRule / Event / External 请求），
/// 统一裁决播放 Action 或返回 Locomotion；合法 Action 优先。External 和 Event 请求仅登记，在下一次 <see cref="DecideAction"/> 中裁决。
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

    /// <summary>A single external request. Cancellation never stops an Action already being started.</summary>
    public sealed class ExternalActionRequest
    {
        internal readonly ActionAsset Action;
        internal readonly ActionContext Context;
        internal readonly int Order;
        private readonly ActionStateManager _owner;
        private Action<bool> _callback;
        private bool _starting;
        private bool _completed;

        internal ExternalActionRequest(ActionStateManager owner, ActionAsset action, ActionContext context,
            Action<bool> callback, int order)
        {
            _owner = owner;
            Action = action;
            Context = context;
            _callback = callback;
            Order = order;
        }

        internal bool IsPending => !_starting && !_completed;

        /// <summary>
        /// Withdraw this request before the start attempt, reporting false exactly once.
        /// Returns false if it was already resolved or ASM has begun starting it.
        /// Other requests and running Actions are unaffected. Call on the Unity main thread.
        /// </summary>
        public bool Cancel()
        {
            if (!IsPending)
                return false;

            // Do not mutate the deciding snapshot while candidate collection/callbacks iterate it.
            _owner._pendingExternalRequests.Remove(this);
            Complete(false, _owner);
            return true;
        }

        internal bool TryBegin()
        {
            if (!IsPending)
                return false;

            // Begin invokes lifecycle code; cancellation must not revoke a start already in progress.
            _starting = true;
            return true;
        }

        internal void Complete(bool started, UnityEngine.Object logContext)
        {
            if (_completed)
                return;

            _completed = true;
            Action<bool> callback = _callback;
            _callback = null;
            if (callback == null)
                return;

            try
            {
                callback(started);
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
        public readonly ActionEntryMatch EntryMatch;

        public ActionCandidate(
            ActionAsset action,
            ActionContext context,
            ActionCandidateOrigin origin,
            int order,
            ExternalActionRequest externalRequest = null,
            ActionEntryMatch entryMatch = default)
        {
            Action = action;
            Context = context;
            Origin = origin;
            Order = order;
            ExternalRequest = externalRequest;
            EntryMatch = entryMatch;
        }
    }

    private enum ActionDecisionKind
    {
        None = 0,
        PlayAction = 1,
        ReturnToLocomotion = 2,
    }

    private readonly struct ActionDecision
    {
        public readonly ActionDecisionKind Kind;
        public readonly ActionCandidate Candidate;
        public readonly ActionInstance SourceAction;
        public readonly ActionEntryMatch ReturnMatch;

        private ActionDecision(ActionDecisionKind kind, ActionCandidate candidate, ActionInstance sourceAction,
            ActionEntryMatch returnMatch = default)
        {
            Kind = kind;
            Candidate = candidate;
            SourceAction = sourceAction;
            ReturnMatch = returnMatch;
        }

        public static ActionDecision Play(ActionCandidate candidate) =>
            new ActionDecision(ActionDecisionKind.PlayAction, candidate, null);

        public static ActionDecision Return(ActionInstance sourceAction, ActionEntryMatch match) =>
            new ActionDecision(ActionDecisionKind.ReturnToLocomotion, default, sourceAction, match);
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
        RequestCancellableExternalAction(action, context, callback);
    }

    /// <summary>
    /// Same arbitration as RequestExternalAction, with a handle for withdrawing this pending request.
    /// An inactive ASM reports false synchronously and returns an already completed handle.
    /// </summary>
    public ExternalActionRequest RequestCancellableExternalAction(ActionAsset action, ActionContext context,
        Action<bool> callback)
    {
        var request = new ExternalActionRequest(this, action, context, callback, AllocateCandidateOrder());

        if (!isActiveAndEnabled)
            request.Complete(false, this);
        else
            _pendingExternalRequests.Add(request);

        return request;
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
            _validCandidatesCache.Clear();

            ActionDecision returnCandidate = CollectNormalCandidates(currentInst);
            CollectEventCandidatesIntoPool();

            startedCandidate = ExecuteDecision(SelectDecision(returnCandidate));
        }
        catch
        {
            AbortQueuedActionRequests();
            throw;
        }

        CompleteDecidingExternalRequests(startedCandidate);
        ClearDecidingSnapshot();
    }

    private ActionDecision CollectNormalCandidates(ActionInstance currentInst)
    {
        if (currentInst == null)
        {
            CollectNeutralCandidates();
            return default;
        }

        return CollectCancelCandidates(currentInst);
    }

    private void CollectNeutralCandidates()
    {
        CollectActionListNeutralCandidates();
        CollectExternalNeutralCandidates();
    }

    private void CollectActionListNeutralCandidates()
    {
        if (_actionList == null) return;

        IReadOnlyList<ActionListEntry> entries = _actionList.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            ActionListEntry entry = entries[i];
            if (entry == null || !entry.allowFromNeutral || entry.action == null ||
                entry.action.TriggerMode == ActionTriggerMode.Event)
                continue;
            TryAddPollCandidate(entry.action, entry.conditions);
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

    private ActionDecision CollectCancelCandidates(ActionInstance currentInst)
    {
        ActionDecision returnCandidate = CollectCancelRuleCandidates(currentInst);
        CollectExternalCancelCandidates(currentInst);
        return returnCandidate;
    }

    private ActionDecision CollectCancelRuleCandidates(ActionInstance currentInstance)
    {
        var rules = currentInstance.Config.CancelRules;
        if (rules == null || rules.Count == 0) return default;

        int currentFrame = _actionPlayer.CurrentFrame;
        int totalFrames = _actionPlayer.TotalFrames;
        ActionDecision returnCandidate = default;

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule == null) continue;
            if (!rule.window.ContainsFrame(currentFrame, totalFrames)) continue;

            switch (rule.targetKind)
            {
                case CancelTargetKind.Locomotion:
                    if (returnCandidate.Kind == ActionDecisionKind.None && CanReturnToLocomotion(rule) &&
                        ActionCondition.TryMatchAll(rule.conditions, _actor, currentInstance.Context, false,
                            out ActionEntryMatch returnMatch))
                        returnCandidate = ActionDecision.Return(currentInstance, returnMatch);
                    break;

                case CancelTargetKind.SpecificAction:
                    if (rule.MatchesTarget(rule.specificTarget))
                        TryAddPollCandidate(rule.specificTarget, rule.conditions);
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
                            if (rule.MatchesTarget(candidate))
                                TryAddPollCandidate(candidate, rule.conditions);
                        }
                    }
                    break;

                case CancelTargetKind.Any:
                    if (_actionList != null)
                    {
                        var allActions = _actionList.GetAllAvailableActions();
                        for (int j = 0; j < allActions.Count; j++)
                        {
                            if (rule.MatchesTarget(allActions[j]))
                                TryAddPollCandidate(allActions[j], rule.conditions);
                        }
                    }
                    break;
            }
        }

        return returnCandidate;
    }

    private bool CanReturnToLocomotion(CancelRule rule)
    {
        ActorLocomotion locomotion = _actor != null ? _actor.actorLocomotion : null;
        if (locomotion == null || !locomotion.isActiveAndEnabled)
            return false;

        float threshold = rule.locomotionIntentThreshold;
        return LocomotionDataValidation.IsFinite(threshold) && threshold >= 0f && threshold <= 1f
               && locomotion.TryGetControlIntent(out LocomotionIntent intent)
               && LocomotionDataValidation.IsFinite(intent.MoveStrength)
               && intent.MoveStrength > threshold
               && LocomotionAnimationUtility.IsValidPlanarDirection(intent.WorldMoveDirection);
    }

    private void CollectExternalCancelCandidates(ActionInstance currentInst)
    {
        for (int i = 0; i < _decidingExternalRequests.Count; i++)
        {
            var request = _decidingExternalRequests[i];
            var action = request.Action;
            if (!IsValidExternalAction(action))
                continue;

            if (!TryMatchCancel(currentInst, action, request.Context, out ActionEntryMatch routeMatch))
                continue;

            TryAddCandidate(new ActionCandidate(
                action,
                request.Context,
                ActionCandidateOrigin.External,
                request.Order,
                request,
                routeMatch));
        }
    }

    private static bool IsValidExternalAction(ActionAsset action)
    {
        return action != null && action.TriggerMode != ActionTriggerMode.Event;
    }

    private bool TryMatchCancel(ActionInstance currentInst, ActionAsset requestedAction,
        ActionContext context, out ActionEntryMatch match)
    {
        match = default;
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

            if (rule.MatchesTarget(requestedAction) &&
                ActionCondition.TryMatchAll(rule.conditions, _actor, context, false, out match))
                return true;
        }

        return false;
    }

    private void CollectEventCandidatesIntoPool()
    {
        for (int i = 0; i < _decidingEventCandidates.Count; i++)
            TryAddCandidate(_decidingEventCandidates[i]);
    }

    private ActionDecision SelectDecision(ActionDecision returnCandidate)
    {
        // Later entry checks may withdraw an external candidate collected earlier in this snapshot.
        for (int i = _validCandidatesCache.Count - 1; i >= 0; i--)
        {
            var request = _validCandidatesCache[i].ExternalRequest;
            if (request != null && !request.IsPending)
                _validCandidatesCache.RemoveAt(i);
        }

        if (_validCandidatesCache.Count > 0)
            return ActionDecision.Play(SelectHighestPriorityAction(_validCandidatesCache));

        return returnCandidate;
    }

    private ActionCandidate? ExecuteDecision(ActionDecision decision)
    {
        switch (decision.Kind)
        {
            case ActionDecisionKind.PlayAction:
                return TryPlayChosenAction(decision.Candidate);
            case ActionDecisionKind.ReturnToLocomotion:
                // Never stop a replacement Action or collect Neutral candidates again in this tick.
                if (ReferenceEquals(_actionPlayer.CurrentAction, decision.SourceAction) &&
                    decision.ReturnMatch.IsCurrentFor(_actor.PlayerInput))
                {
                    _actionPlayer.StopAction();
                    decision.ReturnMatch.TryConsume(_actor.PlayerInput);
                }
                break;
        }

        return null;
    }

    private ActionCandidate? TryPlayChosenAction(ActionCandidate candidate)
    {
        ActionAsset action = candidate.Action;
        if (action == null)
            return null;

        bool sameAsCurrent = _actionPlayer.CurrentAction != null &&
                             _actionPlayer.CurrentAction.Config == action;
        if (sameAsCurrent && !action.AllowReenterWhilePlaying)
            return null;

        if (!candidate.EntryMatch.IsCurrentFor(_actor.PlayerInput))
            return null;
        if (candidate.ExternalRequest != null && !candidate.ExternalRequest.TryBegin())
            return null;
        if (!PlayNewAction(action, candidate.Context))
            return null;

        // Begin can invoke lifecycle code. A reset/owner change invalidates the
        // receipt rather than consuming input belonging to a different session.
        candidate.EntryMatch.TryConsume(_actor.PlayerInput);
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

    private bool PlayNewAction(ActionAsset actionToPlay, ActionContext startContext)
    {
        if (_actionPlayer.TryBeginAction(actionToPlay, startContext, out string failureReason))
            return true;

        Debug.LogWarning(failureReason, this);
        return false;
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

        ActorLocomotion locomotion = _actor.actorLocomotion;
        if (locomotion == null)
        {
            Debug.LogError(
                $"Action '{action.name}' uses StartContextMode.LocomotionIntent but Actor '{_actor.name}' has no ActorLocomotion.",
                this);
            return false;
        }

        if (!locomotion.TryGetControlIntent(out LocomotionIntent intent))
            return false;

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

    private void TryAddPollCandidate(ActionAsset action, IReadOnlyList<ActionCondition> conditions)
    {
        if (action == null || _actor == null) return;
        if (!TryBuildPollContext(action, out ActionContext context) ||
            !ActionCondition.TryMatchAll(conditions, _actor, context, false, out ActionEntryMatch routeMatch))
            return;

        TryAddCandidate(new ActionCandidate(
            action,
            context,
            ActionCandidateOrigin.Poll,
            AllocateCandidateOrder(),
            entryMatch: routeMatch));
    }

    private void TryAddCandidate(ActionCandidate candidate)
    {
        if (candidate.Action == null || _actor == null) return;
        if (candidate.ExternalRequest != null && !candidate.ExternalRequest.IsPending) return;
        if (ContainsEquivalentCandidate(candidate)) return;
        if (CurrentActionAsset == candidate.Action && !candidate.Action.AllowReenterWhilePlaying) return;
        if (!candidate.Action.TryMatchEntry(_actor, candidate.Context,
                candidate.Origin == ActionCandidateOrigin.Event, out ActionEntryMatch match)) return;
        if (candidate.ExternalRequest != null && !candidate.ExternalRequest.IsPending) return;
        _validCandidatesCache.Add(new ActionCandidate(candidate.Action, candidate.Context,
            candidate.Origin, candidate.Order, candidate.ExternalRequest, candidate.EntryMatch.Combine(match)));
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
