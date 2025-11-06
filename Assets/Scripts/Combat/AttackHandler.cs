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

    private List<Collider> attackedObjects = new List<Collider>();

    public void Init(ActorCombater combater, AttackDataConfig config)
    {
        this.combater = combater;
        this.config = config;
    }

    private void OnDestroy()
    {
        ClearHitStartEvents();
        ClearHitOverEvents();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 检查是否在目标层
        if (((1 << other.gameObject.layer) & config.targetLayers) != 0)
        {
            // 排除已经处理过的对象
            if(attackedObjects.Contains(other)) return;

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

                // 广播攻击开始事件
                InvokeHitStartEvent(hitData);

                attackedObjects.Add(other);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 检查是否在目标层
        if (((1 << other.gameObject.layer) & config.targetLayers) != 0)
        {
            // 如果没有接触过则不处理
            if (!attackedObjects.Contains(other)) return;

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                // 创建攻击数据
                AttackHitData hitData = new AttackHitData(
                    damage: config._baseDamage,
                    attacker: combater,
                    hitPoint: other.ClosestPoint(gameObject.transform.position)
                );

                // 处理伤害
                if (damageable != null)
                    damageable.TakeDamage(hitData);

                // 广播攻击结束事件
                InvokeHitOverEvent(hitData);

                attackedObjects.Remove(other);
            }
        }
    }

    #region 攻击接触事件

    private GenericEventManager<AttackHitData> hitStartEventManager = new GenericEventManager<AttackHitData>();

    public void RegisterHitStartEvent(object registrant, Action<AttackHitData> callback)
    {
        hitStartEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterHitStartEvent(object registrant)
    {
        hitStartEventManager.Unsubscribe(registrant);
    }

    void InvokeHitStartEvent(AttackHitData eventData)
    {
        hitStartEventManager.Publish(eventData);
    }

    void ClearHitStartEvents()
    {
        hitStartEventManager.ClearAllSubscriptions();
    }

    #endregion

    #region  攻击脱离事件

    private GenericEventManager<AttackHitData> hitOverEventManager = new GenericEventManager<AttackHitData>();

    public void RegisterHitOverEvent(object registrant, Action<AttackHitData> callback)
    {
        hitOverEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterHitOverEvent(object registrant)
    {
        hitOverEventManager.Unsubscribe(registrant);
    }

    void InvokeHitOverEvent(AttackHitData eventData)
    {
        hitOverEventManager.Publish(eventData);
    }

    void ClearHitOverEvents()
    {
        hitOverEventManager.ClearAllSubscriptions();
    }

    #endregion

}