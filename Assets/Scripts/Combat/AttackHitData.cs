using System;
using DeiveEx.TagTree;
using UnityEngine;

public struct AttackHitData
{
    public float Damage { get; }
    public ActorCombater Attacker { get; }
    public GameObject Target { get; }  // Fixed: target object that was hit
    public Collider TargetCollider { get; } // 本帧命中的具体碰撞体（用于VFX锚点）
    public Vector3 HitPoint { get; }
    public Tag HitEventTag { get; }
    public bool CanBeParried { get; }

    public AttackHitData(float damage, ActorCombater attacker, GameObject target, Collider targetCollider, Vector3 hitPoint, Tag hitEventTag = null)
        : this(damage, attacker, target, targetCollider, hitPoint, hitEventTag, false)
    {
    }

    public AttackHitData(float damage, ActorCombater attacker, GameObject target, Collider targetCollider, Vector3 hitPoint, Tag hitEventTag, bool canBeParried)
    {
        Damage = damage;
        Attacker = attacker;
        Target = target;
        TargetCollider = targetCollider;
        HitPoint = hitPoint;
        HitEventTag = hitEventTag;
        CanBeParried = canBeParried;
    }
}

public enum HitResolveOutcome
{
    None = 0,
    Normal = 1,
    SuperArmor = 2,
    Invincible = 3,
    AlreadyDead = 4,
    Parried = 5,
}

public readonly struct HitResolveResult
{
    public HitResolveOutcome Outcome { get; }
    public bool DamageApplied { get; }
    public bool HitReactionApplied { get; }
    public bool ImpactAllowed { get; }
    public bool TargetKilled { get; }

    private HitResolveResult(HitResolveOutcome outcome, bool damageApplied, bool hitReactionApplied, bool impactAllowed, bool targetKilled)
    {
        Outcome = outcome;
        DamageApplied = damageApplied;
        HitReactionApplied = hitReactionApplied;
        ImpactAllowed = impactAllowed;
        TargetKilled = targetKilled;
    }

    public static HitResolveResult Normal(bool hitReactionApplied, bool targetKilled = false)
    {
        return new HitResolveResult(HitResolveOutcome.Normal, true, hitReactionApplied, true, targetKilled);
    }

    public static HitResolveResult SuperArmor(bool targetKilled = false)
    {
        return new HitResolveResult(HitResolveOutcome.SuperArmor, true, false, true, targetKilled);
    }

    public static HitResolveResult Invincible()
    {
        return new HitResolveResult(HitResolveOutcome.Invincible, false, false, false, false);
    }

    public static HitResolveResult AlreadyDead()
    {
        return new HitResolveResult(HitResolveOutcome.AlreadyDead, false, false, false, false);
    }

    /// <summary>Parries suppress ordinary hit feedback; dedicated feedback can subscribe to ActorCombater.ParrySucceeded.</summary>
    public static HitResolveResult Parried()
    {
        return new HitResolveResult(HitResolveOutcome.Parried, false, false, false, false);
    }
}

[Serializable]
public class AttackDataConfig
{
    public LayerMask targetLayers = 1 << 8;
    public float _baseDamage = 10f;
    [Tooltip("Optional Event.Hit.* tag to route the defender into a hit action via SendEvent.")]
    public TagReference hitEventTag;
    [Tooltip("Allow this hit to be parried during the defender's parry window. Disabled by default for existing attacks.")]
    public bool canBeParried;
}

