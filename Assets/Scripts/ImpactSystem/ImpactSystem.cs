using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 打击感系统管理器 - 场景单例
/// 负责接收打击事件并执行对应的打击效果
/// </summary>
public class ImpactSystem : MonoBehaviour
{
    #region Singleton Pattern
    private static ImpactSystem _instance;
    public static ImpactSystem Instance => _instance;
    
    [Tooltip("如果场景中不存在ImpactSystem，是否自动创建")]
    private static bool autoCreate = true;
    
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        Initialize();
    }
    
    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
    
    /// <summary>
    /// 确保场景中存在ImpactSystem实例
    /// </summary>
    public static void EnsureExists()
    {
        if (_instance == null && autoCreate)
        {
            GameObject go = new GameObject("ImpactSystem");
            _instance = go.AddComponent<ImpactSystem>();
        }
    }
    #endregion
    
    #region 效果管理
    private List<ImpactEffect> activeEffects = new List<ImpactEffect>();
    
    /// <summary>
    /// 执行打击效果
    /// </summary>
    public void ApplyImpact(ImpactData impactData)
    {
        Debug.Log($"[ImpactSystem] 接收到打击数据: {impactData?.ToString() ?? "null"}");
        
        if (impactData == null || impactData.Config == null)
        {
            Debug.LogWarning("ImpactSystem: 无效的ImpactData");
            return;
        }
        
        Debug.Log($"[ImpactSystem] 配置: HitStop={impactData.Config.EnableHitStop}, HitStick={impactData.Config.EnableHitStick}, ScreenShake={impactData.Config.EnableScreenShake}");
        
        // 执行所有启用的效果（直接创建新实例，无需对象池）
        ExecuteEffectIfEnabled<HitStopEffect>(impactData, impactData.Config.EnableHitStop);
        ExecuteEffectIfEnabled<HitStickEffect>(impactData, impactData.Config.EnableHitStick);
        ExecuteEffectIfEnabled<ScreenShakeEffect>(impactData, impactData.Config.EnableScreenShake);
        
        Debug.Log("[ImpactSystem] 效果执行完毕");
    }
    
    private void ExecuteEffectIfEnabled<T>(ImpactData impactData, bool enabled) where T : ImpactEffect, new()
    {
        if (!enabled) return;
        
        // 直接创建新实例（效果对象很轻量，不需要对象池）
        T effect = new T();
        effect.Execute(impactData);
        activeEffects.Add(effect);
    }
    #endregion
    
    #region Unity事件
    private void Initialize()
    {
        // Direct call mode - no EventCenter listener needed
        Debug.Log("ImpactSystem 初始化完成 (Direct Call Mode)");
    }
    
    void Update()
    {
        // 更新所有活跃效果
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (!activeEffects[i].Update())
            {
                // 效果结束，重置并移除（无需回收到对象池）
                activeEffects[i].Reset();
                activeEffects.RemoveAt(i);
            }
        }
    }
    
    void OnEnable()
    {
        EnsureExists();
    }
    #endregion
    
    #region 工具方法
    /// <summary>
    /// 清除所有活跃效果（例如在游戏暂停时）
    /// </summary>
    public void ClearAllEffects()
    {
        foreach (var effect in activeEffects)
        {
            effect.Reset();
        }
        activeEffects.Clear();
    }
    
    /// <summary>
    /// 检查是否有效果正在运行
    /// </summary>
    public bool HasActiveEffects()
    {
        return activeEffects.Count > 0;
    }
    #endregion
}