using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

public class ActionHitBoxBehavior : ActionBehaviourBase
{
    public Transform boneTransform;
    public ActionHitBoxConfig hitboxConfig;
    public AttackDataConfig dataConfig;
    public List<ImpactEffectConfig> effects;

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

        // 运行时才挂载 AttackHandler（编辑器预览时只做碰撞体可视化）
        if (!IsEditorPreview && actor != null && actor.combater != null)
        {
            var hitbox = collider.gameObject.AddComponent<AttackHandler>();
            hitbox.Init(actor.combater, dataConfig);

            // Register event
            hitbox.RegisterHitStartEvent(this, OnHitStart);
            hitbox.RegisterHitOverEvent(this, OnHitOver);
        }

        // Add Updater
        var updater = hitboxObject.AddComponent<HitBoxUpdater>();
        updater.Init(this);
    }

    public void UpdateHitbox()
    {
        if (hitboxObject == null || boneTransform == null) return;

        hitboxObject.transform.position = boneTransform.TransformPoint(hitboxConfig.center);
        hitboxObject.transform.rotation = boneTransform.rotation * hitboxConfig.rotation;

        if (collider == null) return;

        collider.height = hitboxConfig.height;
        collider.radius = hitboxConfig.radius;

        collider.direction = 1;
    }

    private void DestroyHitbox()
    {
        if (hitboxObject == null) return;

        // 只有运行时才有 AttackHandler
        var hitbox = hitboxObject.GetComponent<AttackHandler>();
        if (hitbox != null)
        {
            hitbox.UnregisterHitStartEvent(this);
            hitbox.UnregisterHitOverEvent(this);
        }

        var updater = hitboxObject.GetComponent<HitBoxUpdater>();
        if (updater != null)
        {
            if (Application.isPlaying)
                Object.Destroy(updater);
            else
                Object.DestroyImmediate(updater);
        }

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
        TriggerImpactEffect(data);
    }

    private void TriggerImpactEffect(AttackHitData hitData)
    {
        ImpactSystem.EnsureExists();
        if (ImpactSystem.Instance == null) return;

        var impactData = ImpactData.FromAttackHit(hitData);

        Vector3 rayOrigin = HitVfxAnchorUtility.GetDefaultAttackerRayOrigin(hitData.Attacker);

        Vector3 baseSpawnPoint = HitVfxAnchorUtility.ComputeVfxSpawnPoint(
            rayOrigin,
            hitData.Attacker,
            hitData.Target,
            hitData.HitPoint);
        var afterBias = HitVfxAnchorUtility.ApplyCameraLateralBias(
            baseSpawnPoint,
            rayOrigin,
            hitData.Target);
        impactData.VfxSpawnPoint = afterBias;
        HitVfxAnchorDiagnostics.LogFromHitBox(hitData, baseSpawnPoint, afterBias, rayOrigin);

        impactData.FacingReferenceWorldPosition = HitVfxFacingUtility.ResolveFacingWorldPosition(
            impactData.TargetReceiver != null ? impactData.TargetReceiver.HitFacingTargetOverride : null,
            hitData.Attacker);

        impactData.PopulateDirectionalReferences(rayOrigin);

        ImpactSystem.Instance.ApplyImpact(impactData, effects);
    }

    void OnHitOver(AttackHitData data)
    {
    }

    #endregion

    #region HitBox Updater

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
