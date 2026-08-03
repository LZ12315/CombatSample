using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

[System.Serializable]
public sealed class ActionSequenceHitBoxClipDefinition : ActionSequenceClipDefinition
{
    public BoneReference boneReference;
    public ActionHitBoxConfig hitboxConfig = new ActionHitBoxConfig();
    public AttackDataConfig dataConfig = new AttackDataConfig();

    [SerializeReference, SubclassSelector]
    public List<ImpactEffectConfig> effects = new List<ImpactEffectConfig>();

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.HitBox;

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private static readonly Collider[] OverlapResults = new Collider[32];

        private readonly ActionSequenceHitBoxClipDefinition _definition;
        private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
        private Transform _resolvedBone;

        public Runtime(ActionSequenceHitBoxClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            _hitTargets.Clear();
            _resolvedBone = context.Actor != null ? _definition.boneReference.Resolve(context.Actor) : null;
        }

        public override void OnTick(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            AttackDataConfig dataConfig = _definition.dataConfig;
            if (actor == null || actor.combater == null || _resolvedBone == null || dataConfig == null)
                return;

            BuildCapsule(out Vector3 pointA, out Vector3 pointB, out float radius);

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                radius,
                OverlapResults,
                dataConfig.targetLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider targetCollider = OverlapResults[i];
                if (targetCollider == null || IsOwnCollider(actor, targetCollider))
                    continue;

                IDamageable damageable = ResolveDamageable(targetCollider);
                if (damageable == null || _hitTargets.Contains(damageable))
                    continue;

                Vector3 hitPoint = targetCollider.ClosestPoint((pointA + pointB) * 0.5f);
                Component damageableComponent = damageable as Component;
                GameObject target = damageableComponent != null ? damageableComponent.gameObject : targetCollider.gameObject;

                AttackHitData hitData = new AttackHitData(
                    dataConfig._baseDamage,
                    actor.combater,
                    target,
                    targetCollider,
                    hitPoint,
                    ResolveHitEventTag(dataConfig));

                HitResolveResult hitResult = damageable.TakeDamage(hitData);
                if (!hitResult.ImpactAllowed)
                    continue;

                _hitTargets.Add(damageable);
                TriggerImpactEffect(hitData);
            }
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            _hitTargets.Clear();
            _resolvedBone = null;
        }

        private void BuildCapsule(out Vector3 pointA, out Vector3 pointB, out float radius)
        {
            ActionHitBoxConfig config = _definition.hitboxConfig ?? new ActionHitBoxConfig();
            radius = Mathf.Max(0.001f, config.radius);

            Vector3 center = _resolvedBone.TransformPoint(config.center);
            Quaternion rotation = _resolvedBone.rotation * config.rotation;
            float halfSegment = Mathf.Max(0f, config.height * 0.5f - radius);
            Vector3 axis = rotation * Vector3.up * halfSegment;

            pointA = center + axis;
            pointB = center - axis;
        }

        private void TriggerImpactEffect(AttackHitData hitData)
        {
            if (_definition.effects == null || _definition.effects.Count == 0)
                return;

            ImpactSystem.EnsureExists();
            if (ImpactSystem.Instance == null)
                return;

            ImpactData impactData = ImpactData.FromAttackHit(hitData);
            impactData.VfxSpawnPoint = hitData.HitPoint;
            impactData.FacingReferenceWorldPosition = HitVfxFacingUtility.ResolveFacingWorldPosition(
                impactData.TargetReceiver != null ? impactData.TargetReceiver.HitFacingTargetOverride : null,
                hitData.Attacker);

            Vector3 attackerReference = HitVfxAnchorUtility.GetDefaultAttackerRayOrigin(hitData.Attacker);
            impactData.PopulateDirectionalReferences(attackerReference);

            ImpactSystem.Instance.ApplyImpact(impactData, _definition.effects);
        }

        private static bool IsOwnCollider(Actor actor, Collider collider)
        {
            return actor != null && collider != null && collider.GetComponentInParent<Actor>() == actor;
        }

        private static IDamageable ResolveDamageable(Collider collider)
        {
            if (collider == null)
                return null;

            Component[] components = collider.GetComponentsInParent<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IDamageable damageable)
                    return damageable;
            }

            return null;
        }

        private static Tag ResolveHitEventTag(AttackDataConfig dataConfig)
        {
            return dataConfig != null && dataConfig.hitEventTag != null ? dataConfig.hitEventTag.GetTag() : null;
        }
    }
}
