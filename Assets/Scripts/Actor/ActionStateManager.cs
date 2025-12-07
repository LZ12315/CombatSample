using System.Collections.Generic;
using System.Linq; // 需要这个来使用OrderBy
using UnityEngine;

[RequireComponent(typeof(ActorActionDirector), typeof(Actor))]
public class ActionStateManager : MonoBehaviour
{
    [Header("组件引用")]
    private ActorActionDirector _actionPlayer;
    private Actor _actor;

    [Header("配置")]
    [SerializeField, Tooltip("包含角色所有动作和全局转换的资产列表")]
    private ActionAssetList _actionList;

    private List<ActionAsset> _possibleTransitionsCache = new List<ActionAsset>();
    private List<ActionTransition> _anyTransitionsClones = new List<ActionTransition>();

    private void Awake()
    {
        _actionPlayer = GetComponent<ActorActionDirector>();
        _actor = GetComponent<Actor>();
    }

    private void OnEnable()
    {
        // 订阅播放器完成事件，以处理动作自然结束的逻辑
        _actionPlayer.OnActionFinished += HandleActionFinished;

        // 为AnyTransitions创建并启用运行时副本
        InitializeAnyTransitions();

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
        // 取消订阅，防止内存泄漏
        _actionPlayer.OnActionFinished -= HandleActionFinished;

        // 清理AnyTransitions的运行时副本
        CleanupAnyTransitions();
    }

    private void Update()
    {
        // 每帧检查所有可能的转换条件
        ActionAsset nextAction = CheckForTransition();
        if (nextAction != null)
        {
            PlayNewAction(nextAction);
        }
    }

    /// <summary>
    /// 检查是否有更高优先级的转换可以触发
    /// </summary>
    private ActionAsset CheckForTransition()
    {
        if (_actionPlayer.CurrentAction == null) return null;

        // 【优化】每次检查前清空缓存列表，而不是创建新列表
        _possibleTransitionsCache.Clear();

        // 1. 检查当前动作的私有转换
        _actionPlayer.CurrentAction.CheckTransitions(_possibleTransitionsCache);

        // 2. 检查全局的AnyTransitions
        foreach (var transition in _anyTransitionsClones)
        {
            if (transition.Check())
            {
                _possibleTransitionsCache.Add(transition.TargetAction);
            }
        }

        // 3. 如果有可转换的动作，进行排序并返回最优选
        if (_possibleTransitionsCache.Count > 0)
        {
            return SelectHighestPriorityAction(_possibleTransitionsCache);
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
        // 2. 检查是否有默认的下一个动作
        if (finishedAction.Config.NextAction != null)
        {
            PlayNewAction(finishedAction.Config.NextAction);
            return;
        }
        // 3. 返回默认的待机动作
        if (_actionList != null && _actionList.DefaultAction != null)
        {
            PlayNewAction(_actionList.DefaultAction);
        }
    }

    /// <summary>
    /// 核心方法：播放一个新的动作，并正确处理新旧实例的生命周期
    /// </summary>
    private void PlayNewAction(ActionAsset actionToPlay)
    {
        // 防止对同一个动作（非循环）的无效重复请求
        if (_actionPlayer.CurrentAction != null && _actionPlayer.CurrentAction.Config == actionToPlay && !actionToPlay.IsLoop)
        {
            return;
        }

        // 【优化】让旧实例自己处理退出逻辑
        if (_actionPlayer.CurrentAction != null)
        {
            _actionPlayer.CurrentAction.OnExit();
        }

        // 命令播放器播放新动作，这会创建一个新的ActionInstance
        _actionPlayer.Play(actionToPlay);

        // 【优化】让新实例自己处理进入逻辑
        if (_actionPlayer.CurrentAction != null)
        {
            _actionPlayer.CurrentAction.OnEnter(_actor);
        }
    }

    /// <summary>
    /// 从候选动作列表中，根据优先级选出最优的动作
    /// </summary>
    private ActionAsset SelectHighestPriorityAction(List<ActionAsset> actions)
    {
        if (actions == null || actions.Count == 0) return null;
        if (actions.Count == 1) return actions[0];

        // 按优先级(enum值越大优先级越高)降序
        return actions.OrderByDescending(a => (int)a.priority)
                      .FirstOrDefault();
    }

    #region AnyTransitions管理

    private void InitializeAnyTransitions()
    {
        if (_actionList == null) return;

        foreach (var transition in _actionList.AnyTransitions)
        {
            if (transition != null)
            {
                var clone = transition.Clone();
                clone.Enable(_actor);
                _anyTransitionsClones.Add(clone);
            }
        }
    }

    private void CleanupAnyTransitions()
    {
        foreach (var clone in _anyTransitionsClones)
        {
            clone.Disable();
        }
        _anyTransitionsClones.Clear();
    }

    #endregion
}