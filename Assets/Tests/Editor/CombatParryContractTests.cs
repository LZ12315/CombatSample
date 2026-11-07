using NUnit.Framework;
using UnityEngine;

public sealed class CombatParryContractTests
{
    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public void Parry_RequiresBothAttackPermissionAndActiveWindow(bool allowed, bool window, bool expected)
    {
        Assert.AreEqual(expected,
            CombatParryRules.CanParry(allowed, window, Vector3.forward, Vector3.forward, 60f));
    }

    [TestCase(0f, 1f, true)]
    [TestCase(1f, 1f, true)]
    [TestCase(-1f, 1f, true)]
    [TestCase(1f, 0f, false)]
    [TestCase(0f, -1f, false)]
    public void Parry_UsesDefendersFrontalCone(float x, float z, bool expected)
    {
        Assert.AreEqual(expected,
            CombatParryRules.CanParry(true, true, Vector3.forward, new Vector3(x, 0f, z), 60f));
    }

    [Test]
    public void Parry_ConeRotatesWithDefenderAndIgnoresHeightAndDistance()
    {
        Assert.IsTrue(CombatParryRules.CanParry(
            true, true, new Vector3(2f, 5f, 0f), new Vector3(10f, -100f, 0f), 60f));
        Assert.IsFalse(CombatParryRules.CanParry(
            true, true, Vector3.right, Vector3.forward, 60f));
    }

    [TestCase(-1f)]
    [TestCase(1f)]
    public void Parry_IncludesConeBoundary(float side)
    {
        Assert.IsTrue(CombatParryRules.CanParry(
            true, true, Vector3.forward, new Vector3(side * Mathf.Sqrt(3f), 0f, 1f), 60f));
        Assert.IsFalse(CombatParryRules.CanParry(
            true, true, Vector3.forward, new Vector3(side * 1.8f, 0f, 1f), 60f));
    }

    [Test]
    public void Parry_ZeroAndMaximumHalfAnglesRemainFrontal()
    {
        Assert.IsTrue(CombatParryRules.CanParry(true, true, Vector3.forward, Vector3.forward, 0f));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.forward, Vector3.right, 0f));
        Assert.IsTrue(CombatParryRules.CanParry(true, true, Vector3.forward, Vector3.right, 90f));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.forward, Vector3.back, 90f));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.forward, Vector3.back, 180f));
    }

    [Test]
    public void Parry_RejectsMissingPlanarDirectionAndNonFiniteInputs()
    {
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.zero, Vector3.forward, 60f));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.forward, Vector3.zero, 60f));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.up, Vector3.forward, 60f));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.forward, Vector3.up, 60f));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.forward, Vector3.forward, float.NaN));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.forward, Vector3.forward, float.PositiveInfinity));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, new Vector3(float.NaN, 0f, 1f), Vector3.forward, 60f));
        Assert.IsFalse(CombatParryRules.CanParry(true, true, Vector3.forward, new Vector3(0f, 0f, float.PositiveInfinity), 60f));
    }

    [Test]
    public void Attack_ParryPermissionIsOptInAndCarriedByHitData()
    {
        Assert.IsFalse(new AttackDataConfig().canBeParried);
        var existingCall = new AttackHitData(10f, null, null, null, Vector3.zero);
        Assert.IsFalse(existingCall.CanBeParried);
        var parryable = new AttackHitData(10f, null, null, null, Vector3.zero, hitEventTag: null, canBeParried: true);
        Assert.IsTrue(parryable.CanBeParried);
        Assert.AreEqual(10f, parryable.Damage);
    }

    [Test]
    public void ParriedResult_DistinguishesParryWithoutDamageReactionOrOrdinaryFeedback()
    {
        HitResolveResult result = HitResolveResult.Parried();
        Assert.AreEqual(HitResolveOutcome.Parried, result.Outcome);
        Assert.IsFalse(result.DamageApplied);
        Assert.IsFalse(result.HitReactionApplied);
        Assert.IsFalse(result.ImpactAllowed);
        Assert.IsFalse(result.TargetKilled);
        Assert.AreNotEqual(HitResolveResult.Invincible().Outcome, result.Outcome);
        Assert.AreNotEqual(HitResolveResult.AlreadyDead().Outcome, result.Outcome);
    }

    [Test]
    public void ExistingHitResults_KeepDamageReactionFeedbackAndDeathContracts()
    {
        HitResolveResult normal = HitResolveResult.Normal(true, true);
        Assert.AreEqual(HitResolveOutcome.Normal, normal.Outcome);
        Assert.IsTrue(normal.DamageApplied);
        Assert.IsTrue(normal.HitReactionApplied);
        Assert.IsTrue(normal.ImpactAllowed);
        Assert.IsTrue(normal.TargetKilled);

        HitResolveResult armor = HitResolveResult.SuperArmor();
        Assert.AreEqual(HitResolveOutcome.SuperArmor, armor.Outcome);
        Assert.IsTrue(armor.DamageApplied);
        Assert.IsFalse(armor.HitReactionApplied);
        Assert.IsTrue(armor.ImpactAllowed);
        Assert.IsFalse(armor.TargetKilled);

        foreach (HitResolveResult ignored in new[] { HitResolveResult.Invincible(), HitResolveResult.AlreadyDead() })
        {
            Assert.IsFalse(ignored.DamageApplied);
            Assert.IsFalse(ignored.HitReactionApplied);
            Assert.IsFalse(ignored.ImpactAllowed);
            Assert.IsFalse(ignored.TargetKilled);
        }
    }
}
