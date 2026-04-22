using System;
using System.Collections;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;

public struct AttackHitData
{
    public float Damage { get; }
    public ActorCombater Attacker { get; }
    public GameObject Target { get; }  // Fixed: target object that was hit
    public Collider TargetCollider { get; } // 本帧命中的具体碰撞体（用于VFX锚点）
    public Vector3 HitPoint { get; }
    public Tag HitEventTag { get; }

    public AttackHitData(float damage, ActorCombater attacker, GameObject target, Collider targetCollider, Vector3 hitPoint, Tag hitEventTag = null)
    {
        Damage = damage;
        Attacker = attacker;
        Target = target;
        TargetCollider = targetCollider;
        HitPoint = hitPoint;
        HitEventTag = hitEventTag;
    }
}

[Serializable]
public class AttackDataConfig
{
    public LayerMask targetLayers = 1 << 8;
    public float _baseDamage = 10f;
    [Tooltip("Optional Event.Hit.* tag to route the defender into a hit action via SendEvent.")]
    public TagReference hitEventTag;
}

