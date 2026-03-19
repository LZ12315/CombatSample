using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

public class ActionHitBoxBehavior : ActionBehaviourBase
{
    public Transform boneTransform;
    public ActionHitBoxConfig hitboxConfig;
    public ImpactConfig impactConfig;
    public AttackDataConfig dataConfig;

    public GameObject hitboxObject;
    public CapsuleCollider collider;

    protected override void OnClipStart(Playable playable)
    {
        CreateHitbox();
    }

    protected override void OnClipStop(bool isNormal)
    {
        DestroyHitbox();
    }

    #region HitBox Create/Destroy

    private void CreateHitbox()
    {
        if (hitboxObject != null || boneTransform == null) return;

        hitboxObject = new GameObject("HitBox");
        hitboxObject.hideFlags = HideFlags.HideInHierarchy;

        // Add Collider
        collider = hitboxObject.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;

        // Add AttackHandler
        var hitbox = collider.gameObject.AddComponent<AttackHandler>();
        hitbox.Init(actor.combater, dataConfig);

        // Register event
        hitbox.RegisterHitStartEvent(this, OnHitStart);
        hitbox.RegisterHitOverEvent(this, OnHitOver);

        // Add Updater
        var updater = hitboxObject.AddComponent<HitBoxUpdater>();
        updater.Init(this);
    }

    public void UpdateHitbox()
    {
        if (hitboxObject == null || boneTransform == null) return;

        // Update position
        hitboxObject.transform.position = boneTransform.TransformPoint(hitboxConfig.center);
        hitboxObject.transform.rotation = boneTransform.rotation * hitboxConfig.rotation;

        if (collider == null) return;

        // Update size
        collider.height = hitboxConfig.height;
        collider.radius = hitboxConfig.radius;

        // Y axis
        collider.direction = 1; // 1 = Y
    }

    private void DestroyHitbox()
    {
        if (hitboxObject == null) return;

        // Unregister event
        var hitbox = collider.gameObject.GetComponent<AttackHandler>();
        hitbox.UnregisterHitStartEvent(this);
        hitbox.UnregisterHitOverEvent(this);

        // Remove Updater
        var updater = hitboxObject.GetComponent<HitBoxUpdater>();
        if (updater != null)
        {
            if (Application.isPlaying)
                Object.Destroy(updater);
            else
                Object.DestroyImmediate(updater);
        }

            // Destroy in edit mode
        if (Application.isPlaying)
            Object.Destroy(hitboxObject);
        else
            Object.DestroyImmediate(hitboxObject);

        hitboxObject = null;
        collider = null;
    }

    #endregion

    #region Hit Event

    void OnHitStart(AttackHitData data)
    {
        // Now all effects are handled by ImpactSystem
        TriggerImpactEffect(data);
    }
    
    private void TriggerImpactEffect(AttackHitData hitData)
    {
        try
        {
            // Ensure ImpactSystem exists
            ImpactSystem.EnsureExists();
            
            // Create ImpactData - FIXED: use hitData.Target instead of attacker
            ImpactData impactData = new ImpactData(
                attacker: hitData.Attacker,
                target: hitData.Target?.GetComponent<IDamageable>(),
                hitPoint: hitData.HitPoint,
                damage: hitData.Damage,
                impactForce: 1f,
                config: impactConfig,
                weaponType: WeaponType.Sword,
                attackType: AttackType.Light
            );
            
            // Direct call to ImpactSystem (instead of EventCenter)
            ImpactSystem.Instance.ApplyImpact(impactData);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error: " + e.Message);
        }
    }

    void OnHitOver(AttackHitData data)
    {
        // Now handled by ImpactSystem
    }

    #endregion

    #region HitBox Updater

    // Update HitBox position in edit mode
    [ExecuteInEditMode]
    private class HitBoxUpdater : MonoBehaviour
    {
        private ActionHitBoxBehavior _clip;

        public void Init(ActionHitBoxBehavior clip)
        {
            _clip = clip;
        }

        private void Update()
        {
            UpdateInEditMode();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.update += UpdateInEditMode;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.update -= UpdateInEditMode;
#endif
        }

        private void UpdateInEditMode()
        {
            if (_clip != null && _clip.state == Enums.ActionClipState.Play)
            {
                _clip.UpdateHitbox();
            }
        }
    }

    #endregion

}