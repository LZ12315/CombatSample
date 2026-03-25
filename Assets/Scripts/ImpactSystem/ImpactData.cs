using UnityEngine;

/// <summary>
/// 打击数据包
/// 包含一次打击的所有相关信息
/// </summary>
public class ImpactData
{
    public ActorCombater Attacker;
    public GameObject TargetObject;
    public Vector3 HitPoint;
    public float Damage;

    /// <summary>
    /// VFX 专用生成点（攻击者→目标 Collider 中心射线与目标表面的交点等），由命中流程写入。
    /// </summary>
    public Vector3 VfxSpawnPoint;

    /// <summary>
    /// FaceAttacker 时「看向」的世界坐标（默认攻击者 CC 中心），命中时由 ActionHitBoxBehavior 写入。
    /// </summary>
    public Vector3 FacingReferenceWorldPosition;

    public ImpactData(
        ActorCombater attacker,
        GameObject targetObject,
        Vector3 hitPoint,
        float damage)
    {
        Attacker = attacker;
        TargetObject = targetObject;
        HitPoint = hitPoint;
        Damage = damage;
        VfxSpawnPoint = hitPoint;
    }
}