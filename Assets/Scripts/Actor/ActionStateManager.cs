using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ActionPlayer), typeof(Actor))]
public class ActionStateManager : MonoBehaviour
{
    [Header("组件引用")]
    private ActionPlayer _actionPlayer;
    private Actor _actor;

    [Header("配置")]
    [SerializeField, Tooltip("包含角色所有动作资产的列表")]
    private ActionAssetList _actionList;

    // 缓存列表，用于存放当前帧所有满足条件的动作候选人
    private List<ActionAsset> _validCandidatesCache = new List<ActionAsset>(10);

    private void Awake()
    {
        _actionPlayer = GetComponent<ActionPlayer>();
        _actor = GetComponent<Actor>();
    }

    private void OnEnable()
    {
        // 订阅播放器完成事件，以处理动作自然结束的逻辑
        _actionPlayer.OnActionFinished += HandleActionFinished;
    }

    private void OnDisable()
    {
        _actionPlayer.OnActionFinished -= HandleActionFinished;
    }

    private void Update()
    {
        // 每帧寻找满足准入条件的最佳动作
        ActionAsset nextAction = CheckForTransition();
        
        if (nextAction != null)
        {
            //抢占式独裁：只要拿到入场券，无脑顶替当前动作！
            PlayNewAction(nextAction);
        }
    }

    /// <summary>
    /// 遍历所有可切换动作
    /// </summary>
    private ActionAsset CheckForTransition()
    {
        if (_actionList == null) return null;

        _validCandidatesCache.Clear();

        // 1. 遍历角色拥有的所有动作
        var allActions = _actionList.GetAllAvailableActions();
        
        for (int i = 0; i < allActions.Count; i++)
        {
            ActionAsset action = allActions[i];
            
            if (action != null && action.CheckEntry(_actor))
            {
                _validCandidatesCache.Add(action);
            }
        }

        // 2. 优先级排序
        if (_validCandidatesCache.Count > 0)
        {
            return SelectHighestPriorityAction(_validCandidatesCache);
        }

        return null;
    }

    /// <summary>
    /// 当一个动作自然播放完毕后，决定下一步做什么
    /// </summary>
    private void HandleActionFinished(ActionInstance finishedAction)
    {
        // 1. 检查是否循环
        if (finishedAction.Config.IsLoop)
        {
            PlayNewAction(finishedAction.Config);
            return;
        }
        // 2. 检查是否有强制派生的下一个动作 (比如收刀)
        if (finishedAction.Config.NextAction != null)
        {
            PlayNewAction(finishedAction.Config.NextAction);
            return;
        }
        
        // 3. 动作彻底打完，主动放手
        StopCurrentAction();
    }

    /// <summary>
    /// 核心方法：状态交接仪式
    /// </summary>
    private void PlayNewAction(ActionAsset actionToPlay)
    {
        if (actionToPlay == null) return;

        // 只有在确定要进入这个动作时，才根据配置决定是否清空玩家输入。
        if (actionToPlay.FlushInputOnEnter && _actor.logicInput != null)
        {
            _actor.logicInput.ClearBuffer();
        }

        // 让旧实例退场
        if (_actionPlayer.CurrentAction != null)
        {
            _actionPlayer.CurrentAction.OnExit();
        }

        // 播放器切换
        _actionPlayer.Play(actionToPlay);
        
        // 新实例入场
        if (_actionPlayer.CurrentAction != null)
        {
            _actionPlayer.CurrentAction.OnEnter(_actor);
        }
    }

    /// <summary>
    /// 停止当前动作，交出角色控制权
    /// </summary>
    private void StopCurrentAction()
    {
        if (_actionPlayer.CurrentAction != null)
        {
            _actionPlayer.CurrentAction.OnExit();
            _actionPlayer.Stop(); 
        }
    }

    /// <summary>
    /// 从候选列表中选出优先级最高的动作
    /// </summary>
    private ActionAsset SelectHighestPriorityAction(List<ActionAsset> actions)
    {
        if (actions.Count == 1) return actions[0];

        ActionAsset bestAction = actions[0];
        for (int i = 1; i < actions.Count; i++)
        {
            if ((int)actions[i].Priority > (int)bestAction.Priority)
            {
                bestAction = actions[i];
            }
        }

        return bestAction;
    }
}