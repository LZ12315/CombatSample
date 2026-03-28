using System.Collections.Generic;
using UnityEngine;

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
        ActionAsset nextAction = CheckForTransition();

        if (nextAction != null)
        {
            // === 状态 A：Action夺权 ===
            if (_currentControl == ControlState.Locomotion)
            {
                _actor.locomotion.StopLocomotion();
            }

            _currentControl = ControlState.Action;
            PlayNewAction(nextAction);
        }
        else if (_actor.locomotion.CheckConditions())
        {
            // === 状态 B：locomotion接管（自由移动 or 移动打断） ===
            if (_currentControl != ControlState.Locomotion)
            {
                StopCurrentAction();

                _currentControl = ControlState.Locomotion;
                _actor.locomotion.StartLocomotion();
            }
        }
    }

    private void PlayNewAction(ActionAsset actionToPlay)
    {
        // 在播新动作前关掉旧动作
        StopCurrentAction();

        _actionPlayer.Play(actionToPlay);

        if (_actionPlayer.CurrentAction != null)
        {
            _actionPlayer.CurrentAction.OnEnter(_actor);
        }
    }

    private void StopCurrentAction()
    {
        if (_actionPlayer.CurrentAction != null)
        {
            _actionPlayer.CurrentAction.OnExit();
            _actionPlayer.Stop();
        }

        // 注意：不再在这里清理输入缓冲和标签
        // - 输入缓冲由 _bufferValidTime 自动过期（见 ActorLogicInput）
        // - 标签由 Timeline 的 OnClipStop 回调自动清理（见 ActionTagBehaviour）
        // - 清理逻辑由 ActionInstance 的 OnEnter/OnExit 执行（见 ActionAsset.cleanupOnEnter/cleanupOnExit）
    }

    private void HandleActionFinished(ActionInstance finishedAction)
    {
        // 动作结束时的清理由 ActionInstance.OnExit() 处理
        // 详见 ActionAsset.cleanupOnExit 配置

        // 只有自然结束的，才走你的循环、派生、或者回 Locomotion 逻辑
        if (finishedAction.Config.IsLoop)
        {
            PlayNewAction(finishedAction.Config);
            return;
        }

        if (finishedAction.Config.NextAction != null)
        {
            PlayNewAction(finishedAction.Config.NextAction);
            return;
        }

        // locomotion接管
        StopCurrentAction();
        _currentControl = ControlState.Locomotion;
        _actor.locomotion.StartLocomotion();
    }

    #region Action切换
    private ActionAsset CheckForTransition()
    {
        if (_actionList == null) return null;

        _validCandidatesCache.Clear();
        var allActions = _actionList.GetAllAvailableActions();

        for (int i = 0; i < allActions.Count; i++)
        {
            ActionAsset action = allActions[i];
            if (action != null && action.CheckEntry(_actor))
            {
                _validCandidatesCache.Add(action);
            }
        }

        if (_validCandidatesCache.Count > 0)
        {
            return SelectHighestPriorityAction(_validCandidatesCache);
        }
        return null;
    }

    private ActionAsset SelectHighestPriorityAction(List<ActionAsset> actions)
    {
        if (actions.Count == 1) return actions[0];
        ActionAsset bestAction = actions[0];
        for (int i = 1; i < actions.Count; i++)
        {
            if ((int)actions[i].Priority > (int)bestAction.Priority) bestAction = actions[i];
        }
        return bestAction;
    }
    #endregion
}
