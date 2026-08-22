using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

/// <summary>
/// The single fixed-simulation entry owned by one Actor.
/// Gameplay subsystems remain behind this boundary instead of registering
/// individual clips, conditions, or sessions with the world Driver.
/// </summary>
internal sealed class ActorSimulationRuntime
{
    private readonly Actor _actor;
    private ActionPlayer _actionPlayer;
    private ActionStateManager _actionStateManager;
    private ActionPlayer _tickActionPlayer;
    private bool _playedActionFrameThisTick;
    private bool _tickClosed;
    private readonly ActorHitBoxRuntime _hitBoxes;

    public ActorSimulationRuntime(Actor actor)
    {
        _actor = actor;
        _hitBoxes = new ActorHitBoxRuntime(actor);
    }

    public bool IsActive => _actor != null && _actor.isActiveAndEnabled;
    public int StableId => _actor != null ? _actor.GetInstanceID() : 0;

    public ActorHitBoxRuntime HitBoxes => _hitBoxes;

    public void DecideAction()
    {
        if (!IsActive)
            return;

        ResolveActionStateManager()?.DecideAction();
    }

    public bool PlayActionFrame(float deltaSeconds)
    {
        _tickActionPlayer = null;
        _playedActionFrameThisTick = false;
        _tickClosed = false;

        if (!IsActive)
            return false;

        _tickActionPlayer = ResolveActionPlayer();
        _playedActionFrameThisTick = _tickActionPlayer != null && _tickActionPlayer.PlayActionFrame(deltaSeconds);
        return _playedActionFrameThisTick;
    }

    public void DetectHits(CombatHitBuffer buffer)
    {
        if (!_tickClosed && _playedActionFrameThisTick)
            _hitBoxes.DetectHits(buffer);
    }

    public void FinishActionFrame()
    {
        if (_tickClosed)
            return;

        _tickClosed = true;
        _tickActionPlayer?.FinishActionFrame();
        _tickActionPlayer = null;
        _playedActionFrameThisTick = false;
    }

    public void CancelAction()
    {
        ResolveActionStateManager()?.AbortQueuedActionRequests();

        if (_tickClosed)
        {
            _hitBoxes.Clear();
            _playedActionFrameThisTick = false;
            return;
        }

        _tickClosed = true;
        (_tickActionPlayer ?? ResolveActionPlayer())?.CancelAction();
        _hitBoxes.Clear();
        _tickActionPlayer = null;
        _playedActionFrameThisTick = false;
    }

    private ActionPlayer ResolveActionPlayer()
    {
        if (_actionPlayer == null && _actor != null)
            _actionPlayer = _actor.actionPlayer != null
                ? _actor.actionPlayer
                : _actor.GetComponent<ActionPlayer>();

        return _actionPlayer != null && _actionPlayer.isActiveAndEnabled
            ? _actionPlayer
            : null;
    }

    private ActionStateManager ResolveActionStateManager()
    {
        if (_actionStateManager == null && _actor != null)
            _actionStateManager = _actor.actionManager != null
                ? _actor.actionManager
                : _actor.GetComponent<ActionStateManager>();

        return _actionStateManager != null && _actionStateManager.isActiveAndEnabled
            ? _actionStateManager
            : null;
    }
}

internal readonly struct HitBoxHandle
{
    internal HitBoxHandle(int id)
    {
        Id = id;
    }

    internal int Id { get; }
    public bool IsValid => Id != 0;
}

internal sealed class ActorHitBoxRuntime
{
    private sealed class ActiveHitBox
    {
        public HitBoxHandle Handle;
        public string ClipStableId;
        public Transform Bone;
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
        BoneReference boneReference,
        ActionHitBoxConfig hitBoxConfig,
        AttackDataConfig attackConfig,
        IReadOnlyList<ImpactEffectConfig> effects)
    {
        if (_actor == null || attackConfig == null)
            return default;

        Transform bone = boneReference.Resolve(_actor);
        if (bone == null)
            return default;

        var active = new ActiveHitBox
        {
            Handle = new HitBoxHandle(++_nextHandleId),
            ClipStableId = string.IsNullOrEmpty(clipStableId) ? string.Empty : clipStableId,
            Bone = bone,
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
        if (hitBox == null || hitBox.Bone == null || hitBox.AttackConfig == null)
            return;

        BuildCapsule(hitBox, out Vector3 pointA, out Vector3 pointB, out float radius);
        Vector3 queryCenter = (pointA + pointB) * 0.5f;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            pointA,
            pointB,
            radius,
            OverlapResults,
            hitBox.AttackConfig.targetLayers,
            QueryTriggerInteraction.Collide);

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

    private static void BuildCapsule(ActiveHitBox hitBox, out Vector3 pointA, out Vector3 pointB, out float radius)
    {
        ActionHitBoxConfig config = hitBox.HitBoxConfig ?? new ActionHitBoxConfig();
        radius = Mathf.Max(0.001f, config.radius);

        Vector3 center = hitBox.Bone.TransformPoint(config.center);
        Quaternion rotation = hitBox.Bone.rotation * config.rotation;
        float halfSegment = Mathf.Max(0f, config.height * 0.5f - radius);
        Vector3 axis = rotation * Vector3.up * halfSegment;

        pointA = center + axis;
        pointB = center - axis;
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
