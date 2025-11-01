using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[Serializable]
public class AttackHandler : MonoBehaviour
{
    public ActorCombater combater;
    public AttackDataConfig config;
    private GenericEventManager<AttackHitData> hitEventManager = new GenericEventManager<AttackHitData>();

    public void Init(ActorCombater combater, AttackDataConfig config)
    {
        this.combater = combater;
        this.config = config;
    }

    private void OnDestroy()
    {
        ClearEvents();
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

                // 广播攻击事件
                InvokeHitEvent(hitData);
            }
        }
    }

    public void RegisterForHitEvent(object registrant, Action<AttackHitData> callback)
    {
        hitEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterFromHitEvent(object registrant)
    {
        hitEventManager.Unsubscribe(registrant);
    }

    void InvokeHitEvent(AttackHitData eventData)
    {
        hitEventManager.Publish(eventData);
    }

    void ClearEvents()
    {
        hitEventManager.ClearAllSubscriptions();
    }

}