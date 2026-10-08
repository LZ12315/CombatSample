using System;
using System.Collections.Generic;
using UnityEngine;

internal readonly struct PendingHit
{
    public PendingHit(
        int attackerStableId,
        string clipStableId,
        int targetStableId,
        AttackHitData hitData,
        IDamageable target,
        Collider representativeCollider,
        IReadOnlyList<ImpactEffectConfig> effects)
    {
        AttackerStableId = attackerStableId;
        ClipStableId = string.IsNullOrEmpty(clipStableId) ? string.Empty : clipStableId;
        TargetStableId = targetStableId;
        HitData = hitData;
        Target = target;
        RepresentativeCollider = representativeCollider;
        Effects = effects;
    }

    public int AttackerStableId { get; }
    public string ClipStableId { get; }
    public int TargetStableId { get; }
    public AttackHitData HitData { get; }
    public IDamageable Target { get; }
    public Collider RepresentativeCollider { get; }
    public IReadOnlyList<ImpactEffectConfig> Effects { get; }
}

internal sealed class CombatHitBuffer
{
    private static readonly Comparison<PendingHit> CompareHits = Compare;
    private readonly List<PendingHit> _hits = new List<PendingHit>(32);
    private bool _isResolving;

    public IReadOnlyList<PendingHit> Hits => _hits;
    public int Count => _hits.Count;

    public void Begin()
    {
        _isResolving = false;
        _hits.Clear();
    }

    public bool Add(in PendingHit hit)
    {
        if (_isResolving)
            throw new InvalidOperationException("PendingHit cannot be added while the current hit buffer is resolving.");

        if (hit.Target == null)
            return false;

        _hits.Add(hit);
        return true;
    }

    public void Resolve()
    {
        _isResolving = true;

        try
        {
            _hits.Sort(CompareHits);
            for (int i = 0; i < _hits.Count; i++)
            {
                PendingHit hit = _hits[i];
                HitResolveResult result = ResolveOne(hit);
                if (result.ImpactAllowed)
                    ImpactSystem.HandleConfirmedHit(hit.HitData, hit.Effects);
            }
        }
        finally
        {
            _isResolving = false;
            _hits.Clear();
        }
    }

    public void Clear()
    {
        _isResolving = false;
        _hits.Clear();
    }

    private static HitResolveResult ResolveOne(in PendingHit hit)
    {
        IDamageable target = hit.Target;
        if (target == null || target.IsDead)
            return HitResolveResult.AlreadyDead();

        return target.TakeDamage(hit.HitData);
    }

    private static int Compare(PendingHit a, PendingHit b)
    {
        int attackerCompare = a.AttackerStableId.CompareTo(b.AttackerStableId);
        if (attackerCompare != 0)
            return attackerCompare;

        int clipCompare = string.Compare(a.ClipStableId, b.ClipStableId, StringComparison.Ordinal);
        if (clipCompare != 0)
            return clipCompare;

        return a.TargetStableId.CompareTo(b.TargetStableId);
    }
}
