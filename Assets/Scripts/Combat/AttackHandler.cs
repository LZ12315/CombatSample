using System;
using System.Collections;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEditor;
using UnityEngine;

[Serializable]
public class AttackHandler : MonoBehaviour
{
    public ActorCombater combating;
    public AttackDataConfig config;

    private List<Collider> attackedObjects = new List<Collider>();

    public void Init(ActorCombater combating, AttackDataConfig config)
    {
        this.combating = combating;
        this.config = config;
    }

    private void OnDestroy()
    {
        ClearHitStartEvents();
        ClearHitOverEvents();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (combating == null || config == null) return;
        if (((1 << other.gameObject.layer) & config.targetLayers) != 0)
        {
            if(attackedObjects.Contains(other)) return;

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                AttackHitData hitData = new AttackHitData(
                    damage: config._baseDamage,
                    attacker: combating,
                    target: other.gameObject,
                    targetCollider: other,
                    hitPoint: other.ClosestPoint(gameObject.transform.position),
                    hitEventTag: ResolveHitEventTag()
                );

                if(damageable != null)
                    damageable.TakeDamage(hitData);

                InvokeHitStartEvent(hitData);

                attackedObjects.Add(other);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & config.targetLayers) != 0)
        {
            if (!attackedObjects.Contains(other)) return;

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                AttackHitData hitData = new AttackHitData(
                    damage: config._baseDamage,
                    attacker: combating,
                    target: other.gameObject,
                    targetCollider: other,
                    hitPoint: other.ClosestPoint(gameObject.transform.position),
                    hitEventTag: ResolveHitEventTag()
                );

                InvokeHitOverEvent(hitData);
                // 不从 attackedObjects 移除：骨骼回摆会导致 OnTriggerEnter 再次命中同一目标（双重判定）。
                // 列表随 HitBox 销毁清空；多段攻击靠多个 Clip / 多个 AttackHandler 隔离。
            }
        }
    }

    #region Hit Start Event

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

    #region Hit Over Event

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

    private Tag ResolveHitEventTag()
    {
        return config != null && config.hitEventTag != null ? config.hitEventTag.GetTag() : null;
    }
}
