using System;
using System.Collections;
using System.Collections.Generic;
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
        // Check layer
        Debug.Log("Hit: " + other.name + " Layer:" + other.gameObject.layer);
        if (((1 << other.gameObject.layer) & config.targetLayers) != 0)
        {
            // Layer OK
            Debug.Log("Hit OK: " + other.name);
            if(attackedObjects.Contains(other)) return;

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                // Create hit data - FIXED: added target parameter
                AttackHitData hitData = new AttackHitData(
                    damage: config._baseDamage,
                    attacker: combating,
                    target: other.gameObject,
                    hitPoint: other.ClosestPoint(gameObject.transform.position)
                );

                // Take damage
                if(damageable != null)
                    damageable.TakeDamage(hitData);

                // Fire event
                Debug.Log("Event fire");
                InvokeHitStartEvent(hitData);

                attackedObjects.Add(other);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Check layer
        if (((1 << other.gameObject.layer) & config.targetLayers) != 0)
        {
            // Has IDamageable
            if (!attackedObjects.Contains(other)) return;

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                // Has IDamageable
                Debug.Log("Has IDamageable: " + other.name);
                AttackHitData hitData = new AttackHitData(
                    damage: config._baseDamage,
                    attacker: combating,
                    target: other.gameObject,
                    hitPoint: other.ClosestPoint(gameObject.transform.position)
                );

                // Take damage
                if (damageable != null)
                    damageable.TakeDamage(hitData);

                // Fire over event
                InvokeHitOverEvent(hitData);

                attackedObjects.Remove(other);
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

}
