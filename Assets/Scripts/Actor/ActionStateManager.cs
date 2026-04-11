using System.Collections.Generic;
using UnityEngine;
using DeiveEx.TagTree;

public enum ControlState
{
    None,
    Action,
    Locomotion
}

[RequireComponent(typeof(Actor))]
public class ActionStateManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActionPlayer _actionPlayer;
    [SerializeField] private Actor _actor;

    [Header("Settings")]
    [SerializeField] private ActionAssetList _actionList;

    [Header("Runtime")]
    [SerializeField] private ControlState _currentControl = ControlState.None;
    public ControlState CurrentControl => _currentControl;

    private List<ActionAsset> _validCandidatesCache = new List<ActionAsset>(10);
    private readonly List<ActionAsset> _externalCandidatesThisFrame = new List<ActionAsset>(4);

    // ── 事件触发路径 ──
    private Dictionary<int, List<ActionAsset>> _eventActionMap;
    private readonly List<ActionAsset> _eventCandidatesThisFrame = new List<ActionAsset>(4);
    private ActionEventContext _pendingEventContext;

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

    private void Update()
    {
        ActionAsset chosen = CheckForTransition();

        if (chosen != null)
        {
            ActionEventContext startContext = ResolveStartContext(chosen);

            if (_currentControl == ControlState.Locomotion)
                _actor.locomotion.StopLocomotion();

            _currentControl = ControlState.Action;
            PlayNewAction(chosen, startContext);
        }
        else if (_actor.locomotion.CheckConditions())
        {
            if (_currentControl != ControlState.Locomotion)
            {
                StopCurrentAction();

                _currentControl = ControlState.Locomotion;
                _actor.locomotion.StartLocomotion();
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
        if (_actor == null || _actor.locomotion == null)
            return default;

        var intent = _actor.locomotion.CurrentIntent;
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
        _currentControl = ControlState.Locomotion;
        _actor.locomotion.StartLocomotion();
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

        var currentInstance = _actionPlayer.CurrentAction;
        if (currentInstance != null)
        {
            var branches = currentInstance.Config.Branches;
            for (int i = 0; i < branches.Count; i++)
                TryAddCandidate(branches[i]);
        }

        // 事件候选
        for (int i = 0; i < _eventCandidatesThisFrame.Count; i++)
            TryAddCandidate(_eventCandidatesThisFrame[i]);

        // 外部候选
        for (int i = 0; i < _externalCandidatesThisFrame.Count; i++)
            TryAddCandidate(_externalCandidatesThisFrame[i]);

        if (_validCandidatesCache.Count > 0)
            return SelectHighestPriorityAction(_validCandidatesCache);
        return null;
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
