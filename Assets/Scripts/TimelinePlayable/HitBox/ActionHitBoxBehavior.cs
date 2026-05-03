using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

public class ActionHitBoxBehavior : ActionBehaviourBase
{
    public BoneReference boneReference;
    private Transform _resolvedBone;

    public ActionHitBoxConfig hitboxConfig;
    public AttackDataConfig dataConfig;
    public List<ImpactEffectConfig> effects;

    public GameObject hitboxObject;
    public CapsuleCollider collider;

    protected override void OnClipStart(Playable playable)
    {
        _resolvedBone = null;
        if (actor != null && actor.animancer != null)
            _resolvedBone = boneReference.Resolve(actor.animancer.Animator);
        CreateHitbox();
    }

    protected override void OnClipStop(bool isNormal)
    {
        DestroyHitbox();
        _resolvedBone = null;
    }

    #region HitBox Create/Destroy

    private void CreateHitbox()
    {
        if (hitboxObject != null || _resolvedBone == null) return;

        hitboxObject = new GameObject("HitBox");
        hitboxObject.hideFlags = HideFlags.HideInHierarchy;

        // Rigidbody 必须存在，否则 Trigger 事件不会触发（Unity 物理规则）
        var rb = hitboxObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

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
        if (hitboxObject == null || _resolvedBone == null) return;

        hitboxObject.transform.position = _resolvedBone.TransformPoint(hitboxConfig.center);
        hitboxObject.transform.rotation = _resolvedBone.rotation * hitboxConfig.rotation;

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

        // VfxSpawnPoint 仅作为兼容性字段保留。
        // 新的 HitVfxConfig 使用自己的锚点逻辑（从 SourceHit 解算），不依赖此字段。
        // 旧配置（已废弃）如仍在使用，也会受益于这个直接设置。
        impactData.VfxSpawnPoint = hitData.HitPoint;

        impactData.FacingReferenceWorldPosition = HitVfxFacingUtility.ResolveFacingWorldPosition(
            impactData.TargetReceiver != null ? impactData.TargetReceiver.HitFacingTargetOverride : null,
            hitData.Attacker);

        // PopulateDirectionalReferences 仍需要 attacker 参考点，用简单方法获取
        Vector3 attackerRef = HitVfxAnchorUtility.GetDefaultAttackerRayOrigin(hitData.Attacker);
        impactData.PopulateDirectionalReferences(attackerRef);

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
