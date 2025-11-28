using UnityEngine;
using UnityEngine.Timeline;
using CombatSample.Consts;
using System.Collections.Generic;
using System;
using System.Linq;

[Serializable]
public struct ActionData
{
    [SerializeField, Range(0, 1)] private double _normalizedTime;
    [SerializeField] private Enums.ActionPhase _phase;

    public double normalizedTime
    {
        get => _normalizedTime;
        set => _normalizedTime = Math.Clamp(value, 0, 1);
    }

    public Enums.ActionPhase phase
    {
        get => _phase;
        set
        {
            if (!Enum.IsDefined(typeof(Enums.ActionPhase), value))
                throw new ArgumentException($"Invalid ActionPhase: {value}");
            _phase = value;
        }
    }

    public bool IsInPhase(Enums.ActionPhase phaseToCheck)
    {
        return (_phase & phaseToCheck) != 0;
    }

    public static readonly ActionData Default = new ActionData
    {
        _normalizedTime = 0,
        _phase = Enums.ActionPhase.Neutral
    };
}

// 属性类增强验证和扩展性
[Serializable]
public class ActionAttribute
{
    [SerializeField, Min(0)] private Enums.ActionPriority _priority = Enums.ActionPriority.Normal;
    [SerializeField, Min(0)] private int _weight = 0;

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

}

public class ActionAsset : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("资产")]
    [SerializeField, Tooltip("关联的Timeline")]
    private TimelineAsset _timelineAsset;

    [Header("数据")]
    [SerializeField, Tooltip("动作运行时状态数据")]
    private ActionData _actionData = new ActionData();

    [Header("属性")]
    [SerializeField, Tooltip("动作优先级")]
    [Min(0)] private Enums.ActionPriority _priority = Enums.ActionPriority.Normal;
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
            {
                _timelineAsset = value;
                //OnTimelineAssetChanged?.Invoke(this);
                //MarkDirty();
            }
        }
    }

    public ActionData ActionData
    {
        get => _actionData;
        set
        {
            if (!_actionData.Equals(value))
            {
                _actionData = value;
                //OnActionDataUpdated?.Invoke(_actionData);
                //MarkDirty();
            }
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

    public void SetTimelineAsset(TimelineAsset timelineAsset)
    {
        _timelineAsset = timelineAsset;
        MarkDirty();
    }

    public void UpdateActionData(double newNormalizedTime, Enums.ActionPhase newPhase)
    {
        var newData = new ActionData
        {
            normalizedTime = newNormalizedTime,
            phase = newPhase
        };

        ActionData = newData;
    }

    public void UpdateActionData(ActionData newData)
    {
        UpdateActionData(newData.normalizedTime, newData.phase);
    }

    public void ResetData()
    {
        UpdateActionData(0, Enums.ActionPhase.Neutral);
    }

    #region 生命周期和序列化

    private void OnEnable()
    {
        // 确保初始化
        if (_transitions == null)
            _transitions = new List<ActionTransition>(); // 防止对空列表操作时出错
    }

    public void OnBeforeSerialize()
    {
        // 序列化前验证数据
        _actionData.normalizedTime = Math.Clamp(_actionData.normalizedTime, 0, 1);
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

public static partial class Enums
{
    [System.Flags]
    public enum ActionPhase
    {
        None = 0,
        Neutral = 2,
        Startup = 4,
        Charging = 8,
        FullPower = 16,
        OverCharge = 32,
        Effect = 64,
        Recovery = 128
    }

}