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
        private readonly HashSet<IDamageable> _pendingTargets = new HashSet<IDamageable>();
        private readonly Dictionary<IDamageable, HitCandidate> _candidates =
            new Dictionary<IDamageable, HitCandidate>(16);
        private Transform _resolvedBone;
        private bool _reportedMissingSink;

        public Runtime(ActionSequenceHitBoxClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            _hitTargets.Clear();
            _pendingTargets.Clear();
            _candidates.Clear();
            _resolvedBone = context.Actor != null ? _definition.boneReference.Resolve(context.Actor) : null;
        }

        public override void OnTick(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            AttackDataConfig dataConfig = _definition.dataConfig;
            if (actor == null || actor.combater == null || _resolvedBone == null || dataConfig == null)
                return;

            ICombatHitIntentSink sink = context.HitIntentSink;
            if (sink == null)
            {
                if (!_reportedMissingSink)
                {
                    Debug.LogWarning(
                        "[ActionSequenceHitBox] No CombatHitIntent sink is available. Authoritative hit query and damage are skipped.",
                        actor);
                    _reportedMissingSink = true;
                }

                return;
            }

            BuildCapsule(out Vector3 pointA, out Vector3 pointB, out float radius);
            Vector3 queryCenter = (pointA + pointB) * 0.5f;

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                radius,
                OverlapResults,
                dataConfig.targetLayers,
                QueryTriggerInteraction.Collide);

            _candidates.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                Collider targetCollider = OverlapResults[i];
                if (targetCollider == null || IsOwnCollider(actor, targetCollider))
                    continue;

                IDamageable damageable = ResolveDamageable(targetCollider);
                if (damageable == null || _hitTargets.Contains(damageable) || _pendingTargets.Contains(damageable))
                    continue;

                Vector3 hitPoint = targetCollider.ClosestPoint(queryCenter);
                float distanceSq = (hitPoint - queryCenter).sqrMagnitude;
                int colliderId = targetCollider.GetInstanceID();
                if (_candidates.TryGetValue(damageable, out HitCandidate existing)
                    && !IsBetterRepresentative(distanceSq, colliderId, existing.DistanceSq, existing.ColliderId))
                {
                    continue;
                }

                Component damageableComponent = damageable as Component;
                GameObject target = damageableComponent != null ? damageableComponent.gameObject : targetCollider.gameObject;
                _candidates[damageable] = new HitCandidate(targetCollider, target, hitPoint, distanceSq, colliderId);
            }

            foreach (KeyValuePair<IDamageable, HitCandidate> pair in _candidates)
            {
                IDamageable damageable = pair.Key;
                HitCandidate candidate = pair.Value;
                AttackHitData hitData = new AttackHitData(
                    dataConfig._baseDamage,
                    actor.combater,
                    candidate.Target,
                    candidate.Collider,
                    candidate.HitPoint,
                    ResolveHitEventTag(dataConfig));

                var intent = new CombatHitIntent(
                    sink.TickId,
                    actor.GetInstanceID(),
                    _definition.Guid,
                    GetStableTargetId(damageable, candidate.Collider),
                    hitData,
                    damageable,
                    candidate.Collider,
                    _definition.effects,
                    new Receipt(this, damageable));

                if (sink.TryEnqueue(intent))
                    _pendingTargets.Add(damageable);
            }
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            _pendingTargets.Clear();
            _hitTargets.Clear();
            _candidates.Clear();
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

        private void ResolvePending(IDamageable damageable, in HitResolveResult result)
        {
            if (damageable == null)
                return;

            _pendingTargets.Remove(damageable);
            if (result.ImpactAllowed)
                _hitTargets.Add(damageable);
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

        private static bool IsBetterRepresentative(float distanceSq, int colliderId, float existingDistanceSq, int existingColliderId)
        {
            if (!Mathf.Approximately(distanceSq, existingDistanceSq))
                return distanceSq < existingDistanceSq;

            return colliderId < existingColliderId;
        }

        private static int GetStableTargetId(IDamageable damageable, Collider collider)
        {
            if (damageable is Component component)
                return component.GetInstanceID();

            return collider != null ? collider.GetInstanceID() : 0;
        }

        private readonly struct HitCandidate
        {
            public HitCandidate(Collider collider, GameObject target, Vector3 hitPoint, float distanceSq, int colliderId)
            {
                Collider = collider;
                Target = target;
                HitPoint = hitPoint;
                DistanceSq = distanceSq;
                ColliderId = colliderId;
            }

            public Collider Collider { get; }
            public GameObject Target { get; }
            public Vector3 HitPoint { get; }
            public float DistanceSq { get; }
            public int ColliderId { get; }
        }

        private sealed class Receipt : ICombatHitIntentReceipt
        {
            private Runtime _runtime;
            private readonly IDamageable _damageable;

            public Receipt(Runtime runtime, IDamageable damageable)
            {
                _runtime = runtime;
                _damageable = damageable;
            }

            public void OnResolved(in HitResolveResult result)
            {
                Runtime runtime = _runtime;
                _runtime = null;
                runtime?.ResolvePending(_damageable, result);
            }

            public void OnAborted()
            {
                Runtime runtime = _runtime;
                _runtime = null;
                runtime?._pendingTargets.Remove(_damageable);
            }
        }
    }
}
