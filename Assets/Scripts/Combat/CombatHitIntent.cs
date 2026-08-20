using System;
using System.Collections.Generic;
using UnityEngine;

internal interface ICombatHitIntentSink
{
    int TickId { get; }
    bool TryEnqueue(in CombatHitIntent intent);
}

internal interface ICombatHitIntentReceipt
{
    void OnResolved(in HitResolveResult result);
    void OnAborted();
}

internal readonly struct CombatHitIntent
{
    public CombatHitIntent(
        int tickId,
        int attackerStableId,
        string clipStableId,
        int targetStableId,
        AttackHitData hitData,
        IDamageable target,
        Collider representativeCollider,
        IReadOnlyList<ImpactEffectConfig> effects,
        ICombatHitIntentReceipt receipt)
    {
        TickId = tickId;
        AttackerStableId = attackerStableId;
        ClipStableId = string.IsNullOrEmpty(clipStableId) ? string.Empty : clipStableId;
        TargetStableId = targetStableId;
        HitData = hitData;
        Target = target;
        RepresentativeCollider = representativeCollider;
        Effects = effects;
        Receipt = receipt;
    }

    public int TickId { get; }
    public int AttackerStableId { get; }
    public string ClipStableId { get; }
    public int TargetStableId { get; }
    public AttackHitData HitData { get; }
    public IDamageable Target { get; }
    public Collider RepresentativeCollider { get; }
    public IReadOnlyList<ImpactEffectConfig> Effects { get; }
    public ICombatHitIntentReceipt Receipt { get; }
}

internal sealed class CombatHitIntentBuffer : ICombatHitIntentSink
{
    private static readonly Comparison<CombatHitIntent> CompareIntents = Compare;
    private readonly List<CombatHitIntent> _intents = new List<CombatHitIntent>(32);
    private bool _isResolving;

    public int TickId { get; private set; }
    public IReadOnlyList<CombatHitIntent> Intents => _intents;
    public int Count => _intents.Count;

    public void Begin(int tickId)
    {
        TickId = tickId;
        _isResolving = false;
        _intents.Clear();
    }

    public bool TryEnqueue(in CombatHitIntent intent)
    {
        if (_isResolving)
            throw new InvalidOperationException("CombatHitIntent cannot be enqueued while the current hit buffer is resolving.");

        if (intent.Target == null)
            return false;

        _intents.Add(intent);
        return true;
    }

    public void Resolve()
    {
        _isResolving = true;
        int resolvedCount = 0;

        try
        {
            _intents.Sort(CompareIntents);
            for (; resolvedCount < _intents.Count; resolvedCount++)
            {
                CombatHitIntent intent = _intents[resolvedCount];
                HitResolveResult result = ResolveOne(intent);
                intent.Receipt?.OnResolved(result);
            }
        }
        catch
        {
            AbortFrom(resolvedCount);
            throw;
        }
        finally
        {
            _isResolving = false;
            _intents.Clear();
        }
    }

    public void Abort()
    {
        AbortFrom(0);
        _isResolving = false;
        _intents.Clear();
    }

    private static HitResolveResult ResolveOne(in CombatHitIntent intent)
    {
        IDamageable target = intent.Target;
        if (target == null || target.IsDead)
            return HitResolveResult.AlreadyDead();

        HitResolveResult result = target.TakeDamage(intent.HitData);
        if (result.ImpactAllowed)
            TriggerImpactEffect(intent);

        return result;
    }

    private void AbortFrom(int startIndex)
    {
        for (int i = Mathf.Max(0, startIndex); i < _intents.Count; i++)
            _intents[i].Receipt?.OnAborted();
    }

    private static int Compare(CombatHitIntent a, CombatHitIntent b)
    {
        int tickCompare = a.TickId.CompareTo(b.TickId);
        if (tickCompare != 0)
            return tickCompare;

        int attackerCompare = a.AttackerStableId.CompareTo(b.AttackerStableId);
        if (attackerCompare != 0)
            return attackerCompare;

        int clipCompare = string.Compare(a.ClipStableId, b.ClipStableId, StringComparison.Ordinal);
        if (clipCompare != 0)
            return clipCompare;

        return a.TargetStableId.CompareTo(b.TargetStableId);
    }

    private static void TriggerImpactEffect(in CombatHitIntent intent)
    {
        if (intent.Effects == null || intent.Effects.Count == 0)
            return;

        ImpactSystem.EnsureExists();
        if (ImpactSystem.Instance == null)
            return;

        AttackHitData hitData = intent.HitData;
        ImpactData impactData = ImpactData.FromAttackHit(hitData);
        impactData.VfxSpawnPoint = hitData.HitPoint;
        impactData.FacingReferenceWorldPosition = HitVfxFacingUtility.ResolveFacingWorldPosition(
            impactData.TargetReceiver != null ? impactData.TargetReceiver.HitFacingTargetOverride : null,
            hitData.Attacker);

        Vector3 attackerReference = HitVfxAnchorUtility.GetDefaultAttackerRayOrigin(hitData.Attacker);
        impactData.PopulateDirectionalReferences(attackerReference);

        ImpactSystem.Instance.ApplyImpact(impactData, intent.Effects);
    }
}

internal sealed class CombatTickTransaction
{
    private readonly CombatHitIntentBuffer _hitIntents = new CombatHitIntentBuffer();
    private bool _isOpen;

    public ICombatHitIntentSink HitIntentSink => _hitIntents;
    public int TickId { get; private set; }

    public void Begin(int tickId)
    {
        TickId = tickId;
        _isOpen = true;
        _hitIntents.Begin(tickId);
    }

    public void ResolveHits()
    {
        if (!_isOpen)
            return;

        _hitIntents.Resolve();
    }

    public void Abort()
    {
        if (!_isOpen)
            return;

        _hitIntents.Abort();
    }

    public void Close()
    {
        _hitIntents.Abort();
        _isOpen = false;
        TickId = 0;
    }
}
