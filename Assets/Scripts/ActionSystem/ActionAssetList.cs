using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ActionSystem/ActionList")]
public class ActionAssetList : ScriptableObject
{
    [Header("基础配置")]
    [SerializeField, Tooltip("默认的待机动作 (什么条件都不满足时播它)")]
    private ActionAsset _defaultAction;

    [Header("全局动作 (最高优先级)")]
    [SerializeField, Tooltip("存放受击、闪避、死亡等随时可能抢占的动作")]
    private List<ActionAsset> _globalActions = new List<ActionAsset>();

    [Header("常规技能库")]
    [SerializeField, Tooltip("存放玩家当前可用的普攻、主动技能等")]
    private List<ActionAsset> _skillActions = new List<ActionAsset>();

    // 运行时缓存，避免每次 Update 组装列表产生 GC (内存垃圾)
    private List<ActionAsset> _allActionsCache;

    #region 属性封装
    public ActionAsset DefaultAction => _defaultAction;
    #endregion

    /// <summary>
    /// 获取当前角色拥有的所有动作（全局 + 技能），供大脑检票使用
    /// </summary>
    public IReadOnlyList<ActionAsset> GetAllAvailableActions()
    {
        // 只有在第一次调用，或者缓存为空时才进行组装
        if (_allActionsCache == null)
        {
            _allActionsCache = new List<ActionAsset>(_globalActions.Count + _skillActions.Count);
            
            _allActionsCache.AddRange(_globalActions);
            _allActionsCache.AddRange(_skillActions);
        }
        return _allActionsCache;
    }

    private void OnEnable()
    {
        // 保证在编辑器里修改了列表，或者重新 Play 游戏时，缓存能正确刷新
        _allActionsCache = null;
    }
}