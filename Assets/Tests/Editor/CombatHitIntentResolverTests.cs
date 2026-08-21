using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatHitBufferTests
{
    [Test]
    public void Resolve_UsesStableSortKeysInsteadOfAddOrder()
    {
        var log = new List<string>();

        Resolve(
            NewHit(2, "b", 1, new FakeDamageable("2:b:Target", 100f), log, 1f),
            NewHit(1, "z", 1, new FakeDamageable("1:z:Target", 100f), log, 1f),
            NewHit(1, "a", 1, new FakeDamageable("1:a:Target", 100f), log, 1f));

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
    public void Resolve_AllowsMutualKillAndSkipsLaterDeadTarget()
    {
        var actorA = new FakeDamageable("A", 10f);
        var actorB = new FakeDamageable("B", 10f);
        var log = new List<string>();

        Resolve(
            NewHit(3, "c", 2, actorB, log, 10f),
            NewHit(2, "b", 1, actorA, log, 10f),
            NewHit(1, "a", 2, actorB, log, 10f));

        CollectionAssert.AreEqual(new[] { "B", "A" }, log);
        Assert.IsTrue(actorA.IsDead);
        Assert.IsTrue(actorB.IsDead);
    }

    [Test]
    public void Resolve_InvincibleResultDoesNotApplyDamage()
    {
        var target = new FakeDamageable("Target", 10f) { Invincible = true };

        Resolve(NewHit(1, "hit", 2, target, new List<string>(), 5f));

        Assert.AreEqual(10f, target.Health);
        Assert.IsFalse(target.IsDead);
    }

    [Test]
    public void ClearBeforeResolve_DropsAllPendingHitsWithoutDamage()
    {
        var targetA = new FakeDamageable("A", 10f);
        var targetB = new FakeDamageable("B", 10f);
        var log = new List<string>();
        var buffer = new CombatHitBuffer();

        buffer.Begin();
        Assert.IsTrue(buffer.Add(NewHit(1, "a", 1, targetA, log, 5f)));
        Assert.IsTrue(buffer.Add(NewHit(2, "b", 2, targetB, log, 5f)));

        buffer.Clear();

        Assert.AreEqual(0, buffer.Count);
        Assert.AreEqual(10f, targetA.Health);
        Assert.AreEqual(10f, targetB.Health);
        Assert.AreEqual(0, log.Count);
    }

    [Test]
    public void Resolve_WhenTargetThrowsAfterPartialResolve_KeepsPriorDamageClearsRemainingAndRethrows()
    {
        var targetA = new FakeDamageable("A", 10f);
        var targetB = new FakeDamageable("B", 10f) { ThrowOnDamage = true };
        var targetC = new FakeDamageable("C", 10f);
        var log = new List<string>();
        var buffer = new CombatHitBuffer();

        buffer.Begin();
        buffer.Add(NewHit(1, "a", 1, targetA, log, 5f));
        buffer.Add(NewHit(2, "b", 2, targetB, log, 5f));
        buffer.Add(NewHit(3, "c", 3, targetC, log, 5f));

        Assert.Throws<InvalidOperationException>(() => buffer.Resolve());

        Assert.AreEqual(5f, targetA.Health);
        Assert.AreEqual(10f, targetB.Health);
        Assert.AreEqual(10f, targetC.Health);
        Assert.AreEqual(0, buffer.Count);
        CollectionAssert.AreEqual(new[] { "A" }, log);
    }

    [Test]
    public void Resolve_WhenDamageableAddsDuringResolve_RejectsReentryClearsRemainingAndRethrows()
    {
        var targetA = new EnqueueOnDamageDamageable("A", 10f);
        var targetB = new FakeDamageable("B", 10f);
        var targetC = new FakeDamageable("C", 10f);
        var log = new List<string>();
        var buffer = new CombatHitBuffer();
        targetA.Buffer = buffer;
        targetA.HitToAdd = NewHit(9, "z", 9, targetC, log, 1f);

        buffer.Begin();
        buffer.Add(NewHit(1, "a", 1, targetA, log, 5f));
        buffer.Add(NewHit(2, "b", 2, targetB, log, 5f));

        Assert.Throws<InvalidOperationException>(() => buffer.Resolve());

        Assert.AreEqual(5f, targetA.Health);
        Assert.AreEqual(10f, targetB.Health);
        Assert.AreEqual(10f, targetC.Health);
        Assert.AreEqual(0, buffer.Count);
        CollectionAssert.AreEqual(new[] { "A" }, log);
    }

    [Test]
    public void Resolve_MultipleLethalHitsOnlyFirstKillsTarget()
    {
        var target = new FakeDamageable("Target", 10f);
        var log = new List<string>();

        Resolve(
            NewHit(1, "a", 1, target, log, 10f),
            NewHit(2, "b", 1, target, log, 10f));

        Assert.IsTrue(target.IsDead);
        Assert.AreEqual(1, target.KillCount);
        CollectionAssert.AreEqual(new[] { "Target" }, log);
    }

    private static void Resolve(params PendingHit[] hits)
    {
        var buffer = new CombatHitBuffer();
        buffer.Begin();
        for (int i = 0; i < hits.Length; i++)
            buffer.Add(hits[i]);

        buffer.Resolve();
    }

    private static PendingHit NewHit(
        int attackerId,
        string clipId,
        int targetId,
        FakeDamageable target,
        List<string> log,
        float damage)
    {
        target.Log = log;
        return new PendingHit(
            attackerId,
            clipId,
            targetId,
            new AttackHitData(damage, null, null, null, Vector3.zero),
            target,
            null,
            null);
    }

    private class FakeDamageable : IDamageable
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
        public int KillCount { get; private set; }
        public bool IsDead => _health <= 0f;

        public virtual HitResolveResult TakeDamage(AttackHitData attackData)
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
            bool killed = previousHealth > 0f && _health <= 0f;
            if (killed)
                KillCount++;

            return HitResolveResult.Normal(false, killed);
        }
    }

    private sealed class EnqueueOnDamageDamageable : FakeDamageable
    {
        public EnqueueOnDamageDamageable(string name, float health)
            : base(name, health)
        {
        }

        public CombatHitBuffer Buffer { get; set; }
        public PendingHit HitToAdd { get; set; }

        public override HitResolveResult TakeDamage(AttackHitData attackData)
        {
            HitResolveResult result = base.TakeDamage(attackData);
            Buffer.Add(HitToAdd);
            return result;
        }
    }
}
