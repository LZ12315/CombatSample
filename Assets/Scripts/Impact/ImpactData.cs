using UnityEngine;

/// <summary>
/// 打击表现层上下文。由 <see cref="AttackHitData"/> 派生并补充 VFX 等字段。
/// </summary>
public class ImpactData
{
    /// <summary>本次命中的战斗层来源；由 <see cref="FromAttackHit"/> 写入。</summary>
    public AttackHitData SourceHit;

    /// <summary>Resolved contact outcome. Original attacker/target roles are retained, including on parries.</summary>
    public HitResolveOutcome Outcome = HitResolveOutcome.Normal;

    public ActorCombater Attacker;
    public GameObject TargetObject;
    public Vector3 HitPoint;
    public float Damage;

    /// <summary>
    /// 兼容性字段，现等同于 HitPoint。保留供旧代码引用，新配置应直接从 SourceHit 解算位置。
    /// </summary>
    public Vector3 VfxSpawnPoint;

    /// <summary>
    /// FaceAttacker 时「看向」的世界坐标（默认攻击者 CC 中心），命中时由 ActionHitBoxBehavior 写入。
    /// </summary>
    public Vector3 FacingReferenceWorldPosition;

    /// <summary>攻击者参考点（与射线起点一致），用于世界空间方向与火花轴向。</summary>
    public Vector3 AttackerWorldPosition;

    /// <summary>目标参考点（Collider 包围盒中心等），用于世界空间方向。</summary>
    public Vector3 TargetWorldPosition;

    /// <summary>攻击者→目标 的单位方向；由参考点推导，供火花等使用。</summary>
    public Vector3 ImpactDirectionWorld;

    /// <summary>缓存的目标受击组件，避免重复 GetComponent。</summary>
    public HitFeedbackReceiver TargetReceiver;

    /// <summary>Selected feedback Profile for this outcome. A parry never falls back to the ordinary hit Profile.</summary>
    public HitFeedbackProfile TargetProfile;

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

    public static ImpactData FromAttackHit(in AttackHitData hit) => FromAttackHit(hit, HitResolveOutcome.Normal);

    public static ImpactData FromAttackHit(in AttackHitData hit, HitResolveOutcome outcome)
    {
        var d = new ImpactData(hit.Attacker, hit.Target, hit.HitPoint, hit.Damage)
        {
            SourceHit = hit,
            Outcome = outcome,
        };

        if (hit.Target != null)
        {
            d.TargetReceiver = hit.Target.GetComponentInParent<HitFeedbackReceiver>();
            d.TargetProfile = d.TargetReceiver != null ? d.TargetReceiver.GetProfile(outcome) : null;
        }

        return d;
    }

    /// <summary>写入攻击者/目标参考点与 <see cref="ImpactDirectionWorld"/>。</summary>
    public void PopulateDirectionalReferences(Vector3 attackerWorldPosition)
    {
        AttackerWorldPosition = attackerWorldPosition;
        TargetWorldPosition = HitVfxAnchorUtility.GetTargetReferenceWorldPosition(TargetObject);
        Vector3 d = TargetWorldPosition - AttackerWorldPosition;
        ImpactDirectionWorld = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
    }
}
