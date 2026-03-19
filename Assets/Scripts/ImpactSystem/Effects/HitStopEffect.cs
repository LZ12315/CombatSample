using UnityEngine;

/// <summary>
/// 时间停顿效果（HitStop）
/// 击中时暂停或减慢游戏时间，增强打击感
/// </summary>
public class HitStopEffect : ImpactEffect
{
    private float timer;
    private float originalTimeScale;
    private float targetTimeScale;
    private float duration;
    
    public override bool IsActive => timer > 0;
    
    public override void Execute(ImpactData impactData)
    {
        if (impactData.Config == null)
        {
            Debug.LogWarning("HitStopEffect: ImpactData.Config 为空");
            return;
        }
        
        // 获取配置参数
        duration = impactData.Config.GetAdjustedHitStopDuration();
        targetTimeScale = impactData.Config.HitStopTimeScale;
        
        // 保存原始时间缩放
        originalTimeScale = Time.timeScale;
        
        // 应用时间缩放
        Time.timeScale = targetTimeScale;
        timer = duration;
        
        IsActive = true;
        
        Debug.Log($"HitStopEffect 执行: 持续时间={duration}s, 时间缩放={targetTimeScale}");
    }
    
    public override bool Update()
    {
        if (!IsActive) return false;
        
        // 使用未缩放的时间进行计时
        timer -= Time.unscaledDeltaTime;
        
        if (timer <= 0)
        {
            // 恢复原始时间缩放
            Time.timeScale = originalTimeScale;
            IsActive = false;
            Debug.Log($"HitStopEffect 结束: 恢复时间缩放={originalTimeScale}");
            return false;
        }
        
        return true;
    }
    
    public override void Reset()
    {
        timer = 0;
        originalTimeScale = 1f;
        targetTimeScale = 1f;
        duration = 0;
        IsActive = false;
        
        // 确保时间缩放被恢复（安全措施）
        if (Mathf.Abs(Time.timeScale - 1f) > 0.01f)
        {
            Time.timeScale = 1f;
            Debug.LogWarning("HitStopEffect: 重置时发现异常时间缩放，已恢复为1");
        }
    }
    
    /// <summary>
    /// 强制立即结束时间停顿效果
    /// </summary>
    public void ForceEnd()
    {
        if (IsActive)
        {
            Time.timeScale = originalTimeScale;
            timer = 0;
            IsActive = false;
            Debug.Log($"HitStopEffect 被强制结束");
        }
    }
}