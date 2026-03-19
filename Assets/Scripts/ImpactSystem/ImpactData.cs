using UnityEngine;

/// <summary>
/// 武器类型（用于差异化打击感）
/// </summary>
public enum WeaponType
{
    Sword = 0,      // 剑
    Fist = 1,       // 拳
    Hammer = 2,     // 锤
    Spear = 3,      // 枪
    Magic = 4,      // 魔法
    Other = 99      // 其他
}

/// <summary>
/// 攻击类型（用于差异化打击感）
/// </summary>
public enum AttackType
{
    Light = 0,      // 轻攻击
    Heavy = 1,      // 重攻击
    Special = 2,    // 特殊技
    Combo = 3,      // 连击
    Finisher = 4,   // 终结技
    Other = 99      // 其他
}

/// <summary>
/// 打击数据包
/// 包含一次打击的所有相关信息
/// </summary>
public class ImpactData
{
    public ActorCombater Attacker;      // 攻击者
    public IDamageable Target;          // 受击者（IDamageable接口）
    public Vector3 HitPoint;           // 击中点（世界坐标）
    public float Damage;               // 伤害值
    public float ImpactForce;          // 打击力度（用于物理反馈）
    public ImpactConfig Config;        // 打击配置
    
    // 用于差异化打击感
    public WeaponType WeaponType;
    public AttackType AttackType;
    
    /// <summary>
    /// 创建打击数据
    /// </summary>
    public ImpactData(
        ActorCombater attacker,
        IDamageable target,
        Vector3 hitPoint,
        float damage,
        float impactForce,
        ImpactConfig config,
        WeaponType weaponType = WeaponType.Sword,
        AttackType attackType = AttackType.Light)
    {
        Attacker = attacker;
        Target = target;
        HitPoint = hitPoint;
        Damage = damage;
        ImpactForce = impactForce;
        Config = config;
        WeaponType = weaponType;
        AttackType = attackType;
    }
    
    /// <summary>
    /// 检查数据是否有效
    /// </summary>
    public bool IsValid()
    {
        return Attacker != null && Config != null;
    }
    
    /// <summary>
    /// 获取简短的描述信息（用于调试）
    /// </summary>
    public override string ToString()
    {
        return $"ImpactData: {WeaponType} {AttackType}, Damage={Damage}, Force={ImpactForce}";
    }
}