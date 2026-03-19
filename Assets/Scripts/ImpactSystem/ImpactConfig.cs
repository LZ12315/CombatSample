using System;
using UnityEngine;

/// <summary>
/// 打击感配置
/// 定义一次打击所产生的各种效果的参数
/// </summary>
[Serializable]
public class ImpactConfig
{
    #region 时间效果配置
    [Header("时间效果")]
    
    [Tooltip("是否启用时间停顿（HitStop）")]
    public bool EnableHitStop = true;
    
    [Tooltip("时间停顿持续时间（秒）")]
    [Range(0.01f, 0.5f)]
    public float HitStopDuration = 0.08f;
    
    [Tooltip("时间停顿期间的时间缩放（0=完全停止，0.05=极慢，1=正常）")]
    [Range(0f, 1f)]
    public float HitStopTimeScale = 0.05f;
    
    [Tooltip("是否启用动作黏滞（HitStick）")]
    public bool EnableHitStick = false;
    
    [Tooltip("动作黏滞强度（播放速度缩放）")]
    [Range(0.05f, 1f)]
    public float StickStrength = 0.3f;
    
    [Tooltip("动作黏滞持续时间（秒）")]
    [Range(0.01f, 0.5f)]
    public float StickDuration = 0.15f;
    #endregion
    
    #region 屏幕震动配置
    [Header("屏幕震动")]
    
    [Tooltip("是否启用屏幕震动")]
    public bool EnableScreenShake = false;
    
    [Tooltip("震动强度")]
    [Range(0f, 2f)]
    public float ShakeIntensity = 0.3f;
    
    [Tooltip("震动频率")]
    [Range(1f, 50f)]
    public float ShakeFrequency = 20f;
    
    [Tooltip("震动持续时间（秒）")]
    [Range(0.01f, 1f)]
    public float ShakeDuration = 0.2f;
    #endregion

    #region 高级配置
    [Header("高级配置")]
    
    [Tooltip("打击感整体强度乘数（影响所有效果）")]
    [Range(0.1f, 3f)]
    public float OverallIntensity = 1.0f;
    
    [Tooltip("是否对攻击者和受击者应用不同的效果")]
    public bool DifferentiateAttackerTarget = false;
    
    [Tooltip("攻击者效果强度乘数")]
    [Range(0f, 2f)]
    public float AttackerIntensity = 1.0f;
    
    [Tooltip("受击者效果强度乘数")]
    [Range(0f, 2f)]
    public float TargetIntensity = 1.0f;
    #endregion
    
    #region 工具方法
    /// <summary>
    /// 获取调整后的时间停顿持续时间（考虑整体强度）
    /// </summary>
    public float GetAdjustedHitStopDuration()
    {
        return HitStopDuration * OverallIntensity;
    }
    
    /// <summary>
    /// 获取调整后的震动强度（考虑整体强度）
    /// </summary>
    public float GetAdjustedShakeIntensity()
    {
        return ShakeIntensity * OverallIntensity;
    }
    #endregion
}