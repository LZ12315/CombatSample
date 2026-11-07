using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatDeathContractTests
{
    private GameObject _root;
    private ActorCombater _combater;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Death contract defender");
        _combater = _root.AddComponent<ActorCombater>();
        // Fixture-only initialization: EditMode does not run normal scene Awake, and
        // SendMessage("Awake") hits Unity's ShouldRunBehaviour assertion outside Play Mode.
        // Invoke only this component's startup; assertions below exercise its public contracts.
        typeof(ActorCombater).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_combater, null);
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

    [TestCase(false)]
    [TestCase(true)]
    public void HealthObserverFailure_DoesNotBlockOtherObserversOrHitResolution(bool lethal)
    {
        int healthNotifications = 0;
        int deaths = 0;
        float notifiedHealth = -1f;
        _combater.HealthChanged += (_, __, ___) => throw new System.InvalidOperationException("Expected health observer failure");
        _combater.HealthChanged += (_, health, __) => { healthNotifications++; notifiedHealth = health; };
        _combater.Died += (_, __) => deaths++;
        LogAssert.Expect(LogType.Exception, new Regex("Expected health observer failure"));

        HitResolveResult result = _combater.TakeDamage(Hit(_combater.MaxHealth * (lethal ? 2f : 0.5f)));

        Assert.AreEqual(1, healthNotifications);
        Assert.AreEqual(_combater.CurrentHealth, notifiedHealth);
        Assert.AreEqual(lethal ? 1 : 0, deaths);
        Assert.AreEqual(lethal, result.TargetKilled);
        Assert.IsTrue(result.ImpactAllowed);
        Assert.AreEqual(!lethal, _root.activeSelf);
    }

    [Test]
    public void DeathObserverFailure_DoesNotBlockOtherObserversOrActorDisable()
    {
        int deaths = 0;
        bool activeDuringNotification = false;
        _combater.Died += (_, __) => throw new System.InvalidOperationException("Expected death observer failure");
        _combater.Died += (actor, _) => { deaths++; activeDuringNotification = actor.gameObject.activeSelf; };
        LogAssert.Expect(LogType.Exception, new Regex("Expected death observer failure"));

        HitResolveResult result = _combater.TakeDamage(Hit(_combater.MaxHealth * 2f));
        _combater.TakeDamage(Hit(1f));

        Assert.AreEqual(1, deaths);
        Assert.IsTrue(activeDuringNotification);
        Assert.IsTrue(result.TargetKilled);
        Assert.IsFalse(_root.activeSelf);
    }

    [Test]
    public void HealthObserverNestedFatalHit_OnlyTheFatalHitOwnsTheDeathNotification()
    {
        int deaths = 0;
        AttackHitData fatalHit = Hit(_combater.MaxHealth * 2f);
        AttackHitData deathHit = default;
        HitResolveResult nestedResult = default;
        _combater.HealthChanged += (_, health, __) =>
        {
            if (health > 0f)
                nestedResult = _combater.TakeDamage(fatalHit);
        };
        _combater.Died += (_, hit) => { deaths++; deathHit = hit; };

        HitResolveResult originalResult = _combater.TakeDamage(Hit(1f));

        Assert.AreEqual(1, deaths);
        Assert.AreEqual(fatalHit, deathHit);
        Assert.IsTrue(nestedResult.TargetKilled);
        Assert.IsFalse(originalResult.TargetKilled);
        Assert.IsFalse(_root.activeSelf);
    }

    private AttackHitData Hit(float damage) =>
        new AttackHitData(damage, null, _root, null, new Vector3(1f, 2f, 3f));
}
