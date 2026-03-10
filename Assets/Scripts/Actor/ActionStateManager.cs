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
    [Header("组件引用")]
    private ActionPlayer _actionPlayer;
    private Actor _actor;

    [Header("配置")]
    [SerializeField] private ActionAssetList _actionList;

    [Header("状态")]
    [SerializeField] private ControlState _currentControl = ControlState.None;
    public ControlState CurrentControl => _currentControl;
    
    private List<ActionAsset> _validCandidatesCache = new List<ActionAsset>(10);

    private void Awake()
    {
        _actionPlayer = GetComponent<ActionPlayer>();
        _actor = GetComponent<Actor>();
    }

    private void OnEnable() { _actionPlayer.OnActionFinished += HandleActionFinished; }
    private void OnDisable() { _actionPlayer.OnActionFinished -= HandleActionFinished; }

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
                // 无论当前是在播动作还是已经结束，统一执行清理！
                StopCurrentAction();

                _currentControl = ControlState.Locomotion;
                _actor.locomotion.StartLocomotion(); 
            }
        }
    }

    private void PlayNewAction(ActionAsset actionToPlay)
    { 
        // 清理输入缓存的逻辑暂时放在这里
        if (actionToPlay.FlushInputOnEnter && _actor.logicInput != null)
        {
            _actor.logicInput.ClearBuffer();
        }

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
    }

    private void HandleActionFinished(ActionInstance finishedAction)
    {
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
        
        // 播放默认动作（通常是待机）
        PlayNewAction(_actionList.DefaultAction);
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