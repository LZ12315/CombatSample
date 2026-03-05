using UnityEngine;
using UnityEngine.Timeline;
using System;
using System.Collections.Generic;
using CombatSample.Consts;
using DeiveEx.TagTree; // 引入标签树插件命名空间

[CreateAssetMenu(fileName = "NewAction", menuName = "Action System/Action Asset")]
public class ActionAsset : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("资产")]
    [SerializeField, Tooltip("关联的Timeline")]
    private TimelineAsset _timelineAsset;

    [Header("属性")]
    [SerializeField, Tooltip("动作优先级")]
    private Enums.ActionPriority _priority = Enums.ActionPriority.Normal;

    [SerializeField, Tooltip("动作是否循环播放")]
    private bool isLoop = false;
    
    [Header("标签管理 (Tag Management)")]
    [SerializeField, Tooltip("动作播放期间持续拥有的身份标签 (例如：State.Action.Attack)")]
    private TagReference _selfTag;

    [SerializeField, Tooltip("进入该动作时，需要立刻彻底消耗掉的事件标签 (例如：Event.Hit)")]
    private List<TagReference> _consumeTagsOnEnter = new List<TagReference>();

    [Header("配置")]
    [SerializeField, Tooltip("动作自然结束后的强制派生动作")]
    private ActionAsset nextAction;

    // ? 完美换回你自定义的 ActionCondition
    [SerializeReference, SubclassSelector, Tooltip("必须满足列表里【所有】条件，本动作才会被系统选中")]
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    #region 属性封装
    public TimelineAsset TimelineAsset
    {
        get => _timelineAsset;
        set { if (_timelineAsset != value) _timelineAsset = value; }
    }

    public Enums.ActionPriority Priority { get => _priority; } 
    public bool IsLoop { get => isLoop; }
    public ActionAsset NextAction { get => nextAction; }
    
    // 标签数据
    public TagReference SelfTag => _selfTag;
    public IReadOnlyList<TagReference> ConsumeTagsOnEnter => _consumeTagsOnEnter.AsReadOnly();

    // ? 对应修改为 ActionCondition
    public IReadOnlyList<ActionCondition> EntryConditions => _entryConditions.AsReadOnly();

    #endregion

    #region 公共接口

    public void SetTimelineAsset(TimelineAsset timelineAsset)
    {
        _timelineAsset = timelineAsset;
        MarkDirty();
    }

    public ActionInstance CreateActionInstance()
    {
        return new ActionInstance(this);
    }

    /// <summary>
    /// 检查角色当前状态是否满足准入条件 (无状态极速验证)
    /// </summary>
    public bool CheckEntry(Actor actor)
    {
        if (_entryConditions == null || _entryConditions.Count == 0) 
            return false;

        bool hasNormalConditions = false;
        bool allNormalPassed = true;

        for (int i = 0; i < _entryConditions.Count; i++)
        {
            var cond = _entryConditions[i];
            
            bool isMet = cond.Check(actor); 

            if (cond.overrideAll)
            {
                if (isMet) 
                    return true;
            }
            else
            {
                hasNormalConditions = true;
                if (!isMet)
                    allNormalPassed = false; 
            }
        }

        if (hasNormalConditions)
        {
            return allNormalPassed;
        }

        return false; 
    }
    #endregion

    #region 生命周期和序列化

    private void OnEnable()
    {
        if (_entryConditions == null)
            _entryConditions = new List<ActionCondition>(); // ? 对应修改
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        if (_entryConditions == null)
            _entryConditions = new List<ActionCondition>(); // ? 对应修改
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this); 
#endif
    }
    #endregion
}