using System;
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

    [Test]
    public void AbortBeforeResolve_AbortsAllReceiptsWithoutDamage()
    {
        var targetA = new FakeDamageable("A", 10f);
        var targetB = new FakeDamageable("B", 10f);
        var receiptA = new RecordingReceipt();
        var receiptB = new RecordingReceipt();
        var log = new List<string>();
        var buffer = new CombatHitIntentBuffer();

        buffer.Begin(1);
        Assert.IsTrue(buffer.TryEnqueue(NewIntent(1, 1, "a", 1, targetA, log, 5f, receiptA)));
        Assert.IsTrue(buffer.TryEnqueue(NewIntent(1, 2, "b", 2, targetB, log, 5f, receiptB)));

        buffer.Abort();

        Assert.IsTrue(receiptA.Aborted);
        Assert.IsTrue(receiptB.Aborted);
        Assert.AreEqual(0, buffer.Count);
        Assert.AreEqual(10f, targetA.Health);
        Assert.AreEqual(10f, targetB.Health);
        Assert.AreEqual(0, log.Count);
    }

    [Test]
    public void Resolve_WhenTargetThrowsAfterPartialResolve_KeepsPriorDamageAbortsCurrentAndRemainingAndRethrows()
    {
        var targetA = new FakeDamageable("A", 10f);
        var targetB = new FakeDamageable("B", 10f) { ThrowOnDamage = true };
        var targetC = new FakeDamageable("C", 10f);
        var receiptA = new RecordingReceipt();
        var receiptB = new RecordingReceipt();
        var receiptC = new RecordingReceipt();
        var log = new List<string>();
        var buffer = new CombatHitIntentBuffer();

        buffer.Begin(1);
        buffer.TryEnqueue(NewIntent(1, 1, "a", 1, targetA, log, 5f, receiptA));
        buffer.TryEnqueue(NewIntent(1, 2, "b", 2, targetB, log, 5f, receiptB));
        buffer.TryEnqueue(NewIntent(1, 3, "c", 3, targetC, log, 5f, receiptC));

        Assert.Throws<InvalidOperationException>(() => buffer.Resolve());

        Assert.AreEqual(5f, targetA.Health);
        Assert.AreEqual(10f, targetB.Health);
        Assert.AreEqual(10f, targetC.Health);
        Assert.IsTrue(receiptA.Resolved);
        Assert.IsFalse(receiptA.Aborted);
        Assert.IsFalse(receiptB.Resolved);
        Assert.IsTrue(receiptB.Aborted);
        Assert.IsFalse(receiptC.Resolved);
        Assert.IsTrue(receiptC.Aborted);
        Assert.AreEqual(0, buffer.Count);
    }

    [Test]
    public void Resolve_WhenReceiptEnqueuesDuringResolve_RejectsReentryAbortsRemainingAndRethrows()
    {
        var targetA = new FakeDamageable("A", 10f);
        var targetB = new FakeDamageable("B", 10f);
        var targetC = new FakeDamageable("C", 10f);
        var log = new List<string>();
        var buffer = new CombatHitIntentBuffer();
        var receiptA = new EnqueueOnResolveReceipt(
            buffer,
            NewIntent(1, 9, "z", 9, targetC, log, 1f));
        var receiptB = new RecordingReceipt();

        buffer.Begin(1);
        buffer.TryEnqueue(NewIntent(1, 1, "a", 1, targetA, log, 5f, receiptA));
        buffer.TryEnqueue(NewIntent(1, 2, "b", 2, targetB, log, 5f, receiptB));

        Assert.Throws<InvalidOperationException>(() => buffer.Resolve());

        Assert.AreEqual(5f, targetA.Health);
        Assert.AreEqual(10f, targetB.Health);
        Assert.AreEqual(10f, targetC.Health);
        Assert.IsTrue(receiptA.Resolved);
        Assert.IsTrue(receiptA.Aborted);
        Assert.IsTrue(receiptB.Aborted);
        Assert.AreEqual(0, buffer.Count);
    }

    [Test]
    public void Resolve_MultipleLethalHitsOnlyFirstKillsTarget()
    {
        var target = new FakeDamageable("Target", 10f);
        var receiptA = new RecordingReceipt();
        var receiptB = new RecordingReceipt();
        var log = new List<string>();

        Resolve(
            NewIntent(1, 1, "a", 1, target, log, 10f, receiptA),
            NewIntent(1, 2, "b", 1, target, log, 10f, receiptB));

        Assert.IsTrue(receiptA.Result.TargetKilled);
        Assert.IsTrue(receiptA.Result.DamageApplied);
        Assert.IsFalse(receiptB.Result.TargetKilled);
        Assert.IsFalse(receiptB.Result.DamageApplied);
        CollectionAssert.AreEqual(new[] { "Target" }, log);
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
        ICombatHitIntentReceipt receipt = null)
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
        public bool ThrowOnDamage { get; set; }
        public List<string> Log { get; set; }
        public float Health => _health;
        public bool IsDead => _health <= 0f;

        public HitResolveResult TakeDamage(AttackHitData attackData)
        {
            if (ThrowOnDamage)
                throw new InvalidOperationException($"Injected damage failure for {Name}.");
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
        public bool Resolved { get; private set; }
        public bool Aborted { get; private set; }

        public void OnResolved(in HitResolveResult result)
        {
            Resolved = true;
            Result = result;
        }

        public void OnAborted()
        {
            Aborted = true;
        }
    }

    private sealed class EnqueueOnResolveReceipt : ICombatHitIntentReceipt
    {
        private readonly CombatHitIntentBuffer _buffer;
        private readonly CombatHitIntent _intentToEnqueue;

        public EnqueueOnResolveReceipt(CombatHitIntentBuffer buffer, CombatHitIntent intentToEnqueue)
        {
            _buffer = buffer;
            _intentToEnqueue = intentToEnqueue;
        }

        public bool Resolved { get; private set; }
        public bool Aborted { get; private set; }

        public void OnResolved(in HitResolveResult result)
        {
            Resolved = true;
            _buffer.TryEnqueue(_intentToEnqueue);
        }

        public void OnAborted()
        {
            Aborted = true;
        }
    }
}
