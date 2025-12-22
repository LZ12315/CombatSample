using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;

public struct AttackHitData
{
    public float Damage { get; }
    public ActorCombater Attacker { get; }
    public Vector3 HitPoint { get; }

    public AttackHitData(float damage, ActorCombater attacker, Vector3 hitPoint)
    {
        Damage = damage;
        Attacker = attacker;
        HitPoint = hitPoint;
    }
}

[Serializable]
public class AttackDataConfig
{
    public LayerMask targetLayers = 1 << 8;
    public float _baseDamage = 10f;
}

[Serializable]
public class AttackImpactConfig
{
    [Header("攻击反馈")]
    public Enums.HitImpactType impactType;
    public float stopTime = 0.1f;
    public float stickStrength = 0.3f;

    [Header("受击反馈")]
    public GameObject hitPrefab;
    public bool hitRotate;
    public Range rotateRange;

    [Header("其他")]
    public bool screenShake;
    public AudioClip hitSound;
}

