using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatHitIntentResolverTests
{
    [Test]
    public void Resolve_UsesStableSortKeysInsteadOfEnqueueOrder()
    {
        var log = new List<string>();

        Resolve(
            NewIntent(1, 2, "b", 1, new FakeDamageable("2:b:Target", 100f), log, 1f),
            NewIntent(1, 1, "z", 1, new FakeDamageable("1:z:Target", 100f), log, 1f),
            NewIntent(1, 1, "a", 1, new FakeDamageable("1:a:Target", 100f), log, 1f));

        CollectionAssert.AreEqual(
            new[]
            {
                "1:a:Target",
                "1:z:Target",
                "2:b:Target",
            },
            log);
    }

    [Test]
    public void Resolve_AllowsSameTickMutualKillAndSkipsLaterDeadTarget()
    {
        var actorA = new FakeDamageable("A", 10f);
        var actorB = new FakeDamageable("B", 10f);
        var log = new List<string>();
        var skippedReceipt = new RecordingReceipt();

        Resolve(
            NewIntent(1, 3, "c", 2, actorB, log, 10f, skippedReceipt),
            NewIntent(1, 2, "b", 1, actorA, log, 10f),
            NewIntent(1, 1, "a", 2, actorB, log, 10f));

        CollectionAssert.AreEqual(
            new[]
            {
                "B",
                "A",
            },
            log);
        Assert.IsTrue(actorA.IsDead);
        Assert.IsTrue(actorB.IsDead);
        Assert.IsFalse(skippedReceipt.Result.ImpactAllowed);
        Assert.IsFalse(skippedReceipt.Result.DamageApplied);
    }

    [Test]
    public void Resolve_InvincibleResultDoesNotCommitImpact()
    {
        var target = new FakeDamageable("Target", 10f) { Invincible = true };
        var receipt = new RecordingReceipt();

        Resolve(NewIntent(1, 1, "hit", 2, target, new List<string>(), 5f, receipt));

        Assert.IsFalse(target.IsDead);
        Assert.IsFalse(receipt.Result.ImpactAllowed);
        Assert.IsFalse(receipt.Result.DamageApplied);
    }

    private static void Resolve(params CombatHitIntent[] intents)
    {
        var buffer = new CombatHitIntentBuffer();
        buffer.Begin(1);
        for (int i = 0; i < intents.Length; i++)
            buffer.TryEnqueue(intents[i]);

        buffer.Resolve();
    }

    private static CombatHitIntent NewIntent(
        int tickId,
        int attackerId,
        string clipId,
        int targetId,
        FakeDamageable target,
        List<string> log,
        float damage,
        RecordingReceipt receipt = null)
    {
        receipt ??= new RecordingReceipt();
        target.Log = log;
        return new CombatHitIntent(
            tickId,
            attackerId,
            clipId,
            targetId,
            new AttackHitData(damage, null, null, null, Vector3.zero),
            target,
            null,
            null,
            receipt);
    }

    private sealed class FakeDamageable : IDamageable
    {
        private float _health;

        public FakeDamageable(string name, float health)
        {
            Name = name;
            _health = health;
        }

        public string Name { get; }
        public bool Invincible { get; set; }
        public List<string> Log { get; set; }
        public bool IsDead => _health <= 0f;

        public HitResolveResult TakeDamage(AttackHitData attackData)
        {
            if (IsDead)
                return HitResolveResult.AlreadyDead();
            if (Invincible)
                return HitResolveResult.Invincible();

            Log?.Add(Name);
            float previousHealth = _health;
            _health = Mathf.Max(0f, _health - attackData.Damage);
            return HitResolveResult.Normal(false, previousHealth > 0f && _health <= 0f);
        }
    }

    private sealed class RecordingReceipt : ICombatHitIntentReceipt
    {
        public HitResolveResult Result { get; private set; }
        public bool Aborted { get; private set; }

        public void OnResolved(in HitResolveResult result)
        {
            Result = result;
        }

        public void OnAborted()
        {
            Aborted = true;
        }
    }
}
