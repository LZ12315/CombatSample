using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[Serializable]
public class AttackHitBox : MonoBehaviour
{
    private ActionHitBoxClip clip;
    public AttackConfig config;

    public void Init(ActionHitBoxClip clip)
    {
        this.config = clip.attackConfig;
        this.clip = clip;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 检查是否在敌人层
        if (((1 << other.gameObject.layer) & config.targetLayers) != 0)
        {
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                // 创建攻击数据
                AttackHitData hitData = new AttackHitData(
                    damage: config._baseDamage,
                    attacker: clip._hitboxObject.transform.root.gameObject,
                    hitPoint: other.ClosestPoint(clip._hitboxObject.transform.position)
                );

                // 处理伤害
                damageable.TakeDamage(hitData);

                if (config._debugMode)
                {
                    Debug.Log($"[攻击命中] 目标: {other.name}, 伤害: {config._baseDamage}");
                }
            }
        }
    }

}

public struct AttackHitData
{
    public float Damage { get; }
    public GameObject Attacker { get; }
    public Vector3 HitPoint { get; }

    public AttackHitData(float damage, GameObject attacker, Vector3 hitPoint)
    {
        Damage = damage;
        Attacker = attacker;
        HitPoint = hitPoint;
    }

}