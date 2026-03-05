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

        // 游戏开始时，播放默认动作
        if (_actionList != null && _actionList.DefaultAction != null)
        {
            PlayNewAction(_actionList.DefaultAction);
        }
        else
        {
            Debug.LogError("ActionList或其DefaultAction未在Inspector中设置！", this);
        }
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
            ActionAsset bestNextAction = SelectHighestPriorityAction(_validCandidatesCache);

            // 防抖保护：如果选出来的动作就是正在播的非循环动作，直接忽略
            if (_actionPlayer.CurrentAction != null && 
                _actionPlayer.CurrentAction.Config == bestNextAction && 
                !bestNextAction.IsLoop)
            {
                return null;
            }

            // 【架构精髓】：为什么这里不需要比较 bestNextAction 和 CurrentAction 的优先级？
            // 因为如果是普通攻击想打断当前攻击，它的 CheckEntry 根本不会过（因为没有 Cancelable 标签）！
            // 如果 CheckEntry 过了，说明它在逻辑上绝对是被允许播放的。
            // 优先级仅仅用于解决“多个动作在同一帧同时满足条件”的竞争！
            return bestNextAction;
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
        // 3. 动作彻底打完，返回默认的待机动作
        if (_actionList != null && _actionList.DefaultAction != null)
        {
            PlayNewAction(_actionList.DefaultAction);
        }
    }

    /// <summary>
    /// 核心方法：状态交接仪式
    /// </summary>
    private void PlayNewAction(ActionAsset actionToPlay)
    {
        if (actionToPlay == null) return;

        // 让旧实例优雅退场 (回收它的专属Tag)？？？？？？？？？？？？
        if (_actionPlayer.CurrentAction != null)
        {
            _actionPlayer.CurrentAction.OnExit();
        }

        // 播放器切换
        _actionPlayer.Play(actionToPlay);
        if (_actionPlayer.CurrentAction != null)
        {
            _actionPlayer.CurrentAction.OnEnter(_actor);
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