using NUnit.Framework;
using UnityEngine;

public sealed class CombatDeathContractTests
{
    private GameObject _root;
    private ActorCombater _combater;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Death contract defender");
        _combater = _root.AddComponent<ActorCombater>();
        // Explicit standard startup: EditMode does not run normal scene Awake.
        _root.SendMessage("Awake");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void NonlethalDamage_DoesNotNotifyDeathOrDisableActor()
    {
        int deaths = 0;
        _combater.Died += (_, __) => deaths++;
        float health = _combater.CurrentHealth;

        HitResolveResult result = _combater.TakeDamage(Hit(health / 2f));

        Assert.AreEqual(0, deaths);
        Assert.IsFalse(result.TargetKilled);
        Assert.IsFalse(_combater.IsDead);
        Assert.IsTrue(_root.activeSelf);
    }

    [Test]
    public void FatalDamage_NotifiesOnceWithHitContextBeforeDisablingActor()
    {
        int deaths = 0;
        bool wasActiveDuringNotification = false;
        bool wasDeadDuringNotification = false;
        ActorCombater notifiedActor = null;
        AttackHitData notifiedHit = default;
        _combater.Died += (actor, hit) =>
        {
            deaths++;
            notifiedActor = actor;
            notifiedHit = hit;
            wasActiveDuringNotification = actor.gameObject.activeSelf;
            wasDeadDuringNotification = actor.IsDead;
        };
        AttackHitData fatalHit = Hit(_combater.MaxHealth * 2f);

        HitResolveResult fatalResult = _combater.TakeDamage(fatalHit);
        HitResolveResult repeatedResult = _combater.TakeDamage(fatalHit);

        Assert.AreEqual(1, deaths);
        Assert.AreSame(_combater, notifiedActor);
        Assert.AreEqual(fatalHit, notifiedHit);
        Assert.IsTrue(wasActiveDuringNotification);
        Assert.IsTrue(wasDeadDuringNotification);
        Assert.AreEqual(0f, _combater.CurrentHealth);
        Assert.IsFalse(_root.activeSelf);
        Assert.IsTrue(fatalResult.TargetKilled);
        Assert.AreEqual(HitResolveOutcome.AlreadyDead, repeatedResult.Outcome);
        Assert.IsFalse(repeatedResult.TargetKilled);
    }

    private AttackHitData Hit(float damage) =>
        new AttackHitData(damage, null, _root, null, new Vector3(1f, 2f, 3f));
}
