using UnityEngine;
using UnityEngine.Timeline;
using System;
using System.Collections.Generic;
using CombatSample.Consts;
using DeiveEx.TagTree; // 引入标签树插件命名空间

public class ActionAsset : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("Core")]
    [SerializeField, Tooltip("Main Timeline asset")]
    private TimelineAsset _timelineAsset;

    [Header("Properties")]
    [SerializeField, Tooltip("Action priority")]
    private Enums.ActionPriority _priority = Enums.ActionPriority.Normal;
    
    [Header("Tags")]
    [SerializeField, Tooltip("Tag on you while this action plays. Example: State.Action.Attack")]
    private TagReference _selfTag;

    [Header("Settings")]

    [SerializeField, Tooltip("Loop this action")]
    private bool isLoop = false;

    [SerializeField, Tooltip("Next action after this one ends")]
    private ActionAsset nextAction;

    [SerializeReference, SubclassSelector, Tooltip("All conditions in this list must pass. Then this action can run.")]
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    [Header("Cleanup")]
    [Tooltip("Clear targets when action starts")]
    public Enums.CleanupTarget cleanupOnEnter = Enums.CleanupTarget.None;

    [Tooltip("Clear targets when action ends")]
    public Enums.CleanupTarget cleanupOnExit = Enums.CleanupTarget.None;

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
            _entryConditions = new List<ActionCondition>(); // 反序列化后保证非 null
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        if (_entryConditions == null)
            _entryConditions = new List<ActionCondition>(); // 反序列化后保证非 null
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this); 
#endif
    }
    #endregion
}