using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

internal readonly struct HitBoxHandle
{
    internal HitBoxHandle(int id)
    {
        Id = id;
    }

    internal int Id { get; }
    public bool IsValid => Id != 0;
}

/// <summary>Per-actor hit queries owned and ticked by ActorSimulationRuntime.</summary>
internal sealed class ActorHitBoxRuntime
{
    private sealed class ActiveHitBox
    {
        public HitBoxHandle Handle;
        public string ClipStableId;
        public Transform Binding;
        public ActionHitBoxConfig HitBoxConfig;
        public AttackDataConfig AttackConfig;
        public IReadOnlyList<ImpactEffectConfig> Effects;
        public readonly HashSet<IDamageable> AttemptedTargets = new HashSet<IDamageable>();
    }

    private readonly struct HitCandidate
    {
        public HitCandidate(
            IDamageable damageable,
            int targetStableId,
            Collider collider,
            GameObject target,
            Vector3 hitPoint,
            float distanceSq,
            int colliderId)
        {
            Damageable = damageable;
            TargetStableId = targetStableId;
            Collider = collider;
            Target = target;
            HitPoint = hitPoint;
            DistanceSq = distanceSq;
            ColliderId = colliderId;
        }

        public IDamageable Damageable { get; }
        public int TargetStableId { get; }
        public Collider Collider { get; }
        public GameObject Target { get; }
        public Vector3 HitPoint { get; }
        public float DistanceSq { get; }
        public int ColliderId { get; }
    }

    private static readonly Collider[] OverlapResults = new Collider[32];
    private readonly Actor _actor;
    private readonly List<ActiveHitBox> _active = new List<ActiveHitBox>(4);
    private readonly Dictionary<IDamageable, HitCandidate> _candidates =
        new Dictionary<IDamageable, HitCandidate>(16);
    private readonly List<HitCandidate> _orderedCandidates = new List<HitCandidate>(16);
    private int _nextHandleId;

    public ActorHitBoxRuntime(Actor actor)
    {
        _actor = actor;
    }

    public HitBoxHandle Activate(
        string clipStableId,
        ActionHitBoxAnchor anchor,
        ActionHitBoxConfig hitBoxConfig,
        AttackDataConfig attackConfig,
        IReadOnlyList<ImpactEffectConfig> effects)
    {
        if (_actor == null || attackConfig == null)
            return default;

        Animator animator = _actor.animancer != null ? _actor.animancer.Animator : null;
        if (!ActionHitBoxAnchorResolver.TryResolve(
                anchor, _actor.transform, animator, out Transform binding, out string failureReason))
        {
            Debug.LogWarning(
                $"[Action HitBox] Item '{clipStableId}' could not resolve anchor '{anchor}': {failureReason} Hit query is skipped.",
                _actor);
            return default;
        }

        return ActivateResolved(clipStableId, binding, hitBoxConfig, attackConfig, effects);
    }

    private HitBoxHandle ActivateResolved(
        string clipStableId,
        Transform binding,
        ActionHitBoxConfig hitBoxConfig,
        AttackDataConfig attackConfig,
        IReadOnlyList<ImpactEffectConfig> effects)
    {
        if (!ActionHitBoxGeometry.TryBuild(binding, hitBoxConfig, out _, out string shapeFailure))
        {
            Debug.LogWarning(
                $"[Action HitBox] Item '{clipStableId}' has an invalid shape: {shapeFailure} Hit query is skipped.",
                _actor);
            return default;
        }

        var active = new ActiveHitBox
        {
            Handle = new HitBoxHandle(++_nextHandleId),
            ClipStableId = string.IsNullOrEmpty(clipStableId) ? string.Empty : clipStableId,
            Binding = binding,
            HitBoxConfig = hitBoxConfig ?? new ActionHitBoxConfig(),
            AttackConfig = attackConfig,
            Effects = effects,
        };

        _active.Add(active);
        return active.Handle;
    }

    public void Deactivate(HitBoxHandle handle)
    {
        if (!handle.IsValid)
            return;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].Handle.Id == handle.Id)
            {
                _active.RemoveAt(i);
                return;
            }
        }
    }

    public void DetectHits(CombatHitBuffer buffer)
    {
        if (buffer == null || _actor == null || _actor.combater == null)
            return;

        for (int i = 0; i < _active.Count; i++)
            DetectHits(_active[i], buffer);
    }

    public void Clear()
    {
        _active.Clear();
        _candidates.Clear();
        _orderedCandidates.Clear();
    }

    private void DetectHits(ActiveHitBox hitBox, CombatHitBuffer buffer)
    {
        if (hitBox == null || hitBox.Binding == null || hitBox.AttackConfig == null)
            return;

        if (!ActionHitBoxGeometry.TryBuild(hitBox.Binding, hitBox.HitBoxConfig, out ActionHitBoxWorldShape shape, out _))
            return;
        Vector3 queryCenter = shape.Center;
        int hitCount = ActionHitBoxGeometry.QueryNonAlloc(
            shape, OverlapResults, hitBox.AttackConfig.targetLayers, QueryTriggerInteraction.Collide);

        _candidates.Clear();
        _orderedCandidates.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider targetCollider = OverlapResults[i];
            if (targetCollider == null || IsOwnCollider(targetCollider))
                continue;

            IDamageable damageable = ResolveDamageable(targetCollider);
            if (damageable == null || damageable.IsDead || hitBox.AttemptedTargets.Contains(damageable))
                continue;

            Vector3 hitPoint = targetCollider.ClosestPoint(queryCenter);
            float distanceSq = (hitPoint - queryCenter).sqrMagnitude;
            int colliderId = targetCollider.GetInstanceID();
            int targetStableId = GetStableTargetId(damageable, targetCollider);
            Component damageableComponent = damageable as Component;
            GameObject target = damageableComponent != null ? damageableComponent.gameObject : targetCollider.gameObject;
            var candidate = new HitCandidate(
                damageable,
                targetStableId,
                targetCollider,
                target,
                hitPoint,
                distanceSq,
                colliderId);

            if (_candidates.TryGetValue(damageable, out HitCandidate existing)
                && !IsBetterRepresentative(distanceSq, colliderId, existing.DistanceSq, existing.ColliderId))
            {
                continue;
            }

            _candidates[damageable] = candidate;
        }

        foreach (KeyValuePair<IDamageable, HitCandidate> pair in _candidates)
            _orderedCandidates.Add(pair.Value);

        _orderedCandidates.Sort((a, b) => a.TargetStableId.CompareTo(b.TargetStableId));

        for (int i = 0; i < _orderedCandidates.Count; i++)
        {
            HitCandidate candidate = _orderedCandidates[i];
            AttackHitData hitData = new AttackHitData(
                hitBox.AttackConfig._baseDamage,
                _actor.combater,
                candidate.Target,
                candidate.Collider,
                candidate.HitPoint,
                ResolveHitEventTag(hitBox.AttackConfig));

            var pendingHit = new PendingHit(
                _actor.GetInstanceID(),
                hitBox.ClipStableId,
                candidate.TargetStableId,
                hitData,
                candidate.Damageable,
                candidate.Collider,
                hitBox.Effects);

            if (buffer.Add(pendingHit))
                hitBox.AttemptedTargets.Add(candidate.Damageable);
        }
    }

    private bool IsOwnCollider(Collider collider)
    {
        return collider != null && collider.GetComponentInParent<Actor>() == _actor;
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
}
