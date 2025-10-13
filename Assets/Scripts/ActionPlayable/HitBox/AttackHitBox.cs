using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[Serializable]
public class AttackHitBox : MonoBehaviour
{
    public ActorCombater combater;
    public AttackConfig config;

    public void Init(ActorCombater combater, AttackConfig config)
    {
        this.combater = combater;
        this.config = config;
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
                    attacker: combater,
                    hitPoint: other.ClosestPoint(gameObject.transform.position)
                );

                // 处理伤害
                if(damageable != null)
                    damageable.TakeDamage(hitData);

                if (config._debugMode)
                {
                    Debug.Log($"[攻击命中] 目标: {other.name}, 位置: {hitData.HitPoint}, 伤害: {hitData.Damage}");
                }
            }
        }
    }

}

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