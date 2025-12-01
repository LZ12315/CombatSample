using UnityEngine;
using UnityEngine.Timeline;
using System;
using System.Collections.Generic;
using CombatSample.Consts;

public class ActionAsset : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("资产")]
    [SerializeField, Tooltip("关联的Timeline")]
    private TimelineAsset _timelineAsset;

    [Header("属性")]
    [SerializeField, Tooltip("动作优先级")]
    private Enums.ActionPriority _priority = Enums.ActionPriority.Normal;
    [SerializeField, Tooltip("同一优先级下的重要性")]
    [Min(0)] private int _weight = 0;

    [Header("配置")]
    [SerializeField, Tooltip("动作状态转换配置")]
    private List<ActionTransition> _transitions = new List<ActionTransition>();

    #region 属性封装
    public TimelineAsset TimelineAsset
    {
        get => _timelineAsset;
        set
        {
            if (_timelineAsset != value)
                _timelineAsset = value;
        }
    }

    public Enums.ActionPriority priority
    {
        get => _priority;
        set
        {
            if (!Enum.IsDefined(typeof(Enums.ActionPriority), value))
                throw new ArgumentException($"Invalid ActionPriority: {value}");
            _priority = value;
        }
    }

    public int weight
    {
        get => _weight;
        set => _weight = Math.Max(0, value);
    }

    public IReadOnlyList<ActionTransition> Transitions => _transitions.AsReadOnly();
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
    #endregion

    #region 生命周期和序列化

    private void OnEnable()
    {
        // 确保初始化
        if (_transitions == null)
            _transitions = new List<ActionTransition>(); // 防止对空列表操作时出错
    }

    public void OnBeforeSerialize()
    {

    }

    public void OnAfterDeserialize()
    {
        // 反序列化后修复数据
        if (_transitions == null)
            _transitions = new List<ActionTransition>();
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this); // 只有在编辑器环境下才需要标记
#endif
    }
    #endregion

}