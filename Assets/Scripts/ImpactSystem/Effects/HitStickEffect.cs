using UnityEngine;

/// <summary>
/// 动作黏滞效果（HitStick）
/// 击中时减慢攻击动作的播放速度，产生"黏着感"
/// </summary>
public class HitStickEffect : ImpactEffect
{
    private float timer;
    private float originalSpeed;
    private float stickStrength;
    private float duration;
    private ActionPlayer actionPlayer;  // 攻击者的动作播放器
    
    public override bool IsActive => timer > 0 && actionPlayer != null;
    
    public override void Execute(ImpactData impactData)
    {
        if (impactData.Config == null)
        {
            Debug.LogWarning("HitStickEffect: ImpactData.Config 为空");
            return;
        }
        
        // 获取攻击者的ActionPlayer组件
        if (impactData.Attacker == null)
        {
            Debug.LogWarning("HitStickEffect: 攻击者为空");
            return;
        }
        
        actionPlayer = impactData.Attacker.GetComponent<ActionPlayer>();
        if (actionPlayer == null)
        {
            // 尝试从攻击者的子对象或父对象查找
            actionPlayer = impactData.Attacker.GetComponentInChildren<ActionPlayer>();
            if (actionPlayer == null)
            {
                Debug.LogWarning("HitStickEffect: 未找到ActionPlayer组件");
                return;
            }
        }
        
        // 获取配置参数
        stickStrength = impactData.Config.StickStrength;
        duration = impactData.Config.StickDuration;
        
        // 保存原始速度
        originalSpeed = 1f; // ActionPlayer的默认速度（假设为1）
        
        // 应用黏滞速度
        actionPlayer.SetSpeed(stickStrength);
        timer = duration;
        
        IsActive = true;
        
        Debug.Log($"HitStickEffect 执行: 持续时间={duration}s, 速度缩放={stickStrength}");
    }
    
    public override bool Update()
    {
        if (!IsActive) return false;
        
        timer -= Time.deltaTime;
        
        if (timer <= 0)
        {
            // 恢复原始速度
            if (actionPlayer != null)
            {
                actionPlayer.SetSpeed(1f);
            }
            IsActive = false;
            Debug.Log($"HitStickEffect 结束: 恢复速度=1");
            return false;
        }
        
        return true;
    }
    
    public override void Reset()
    {
        timer = 0;
        originalSpeed = 1f;
        stickStrength = 1f;
        duration = 0;
        IsActive = false;
        
        // 确保ActionPlayer速度被恢复（安全措施）
        if (actionPlayer != null)
        {
            actionPlayer.SetSpeed(1f);
            actionPlayer = null;
        }
    }
    
    /// <summary>
    /// 强制立即结束动作黏滞效果
    /// </summary>
    public void ForceEnd()
    {
        if (IsActive && actionPlayer != null)
        {
            actionPlayer.SetSpeed(1f);
            timer = 0;
            IsActive = false;
            Debug.Log($"HitStickEffect 被强制结束");
        }
    }
}