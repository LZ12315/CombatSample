using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ImpactFeedbackRoutingTests
{
    private readonly List<Object> _objects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] != null)
                Object.DestroyImmediate(_objects[i]);
        }
        _objects.Clear();
    }

    [TestCase(HitResolveOutcome.Normal)]
    [TestCase(HitResolveOutcome.SuperArmor)]
    [TestCase(HitResolveOutcome.Parried)]
    public void ResolvedContact_SelectsDefendersProfileForItsOutcome(HitResolveOutcome outcome)
    {
        HitFeedbackReceiver receiver = CreateReceiver(out HitFeedbackProfile normal, out HitFeedbackProfile parry);
        AttackHitData hit = IncomingHit(receiver.gameObject);

        ImpactData data = ImpactData.FromAttackHit(hit, outcome);

        Assert.AreSame(receiver, data.TargetReceiver);
        Assert.AreSame(outcome == HitResolveOutcome.Parried ? parry : normal, data.TargetProfile);
        Assert.AreEqual(outcome, data.Outcome);
        Assert.IsTrue(ImpactSystem.HasConfiguredImpactFeedback(data, null));
    }

    [TestCase(HitResolveOutcome.None)]
    [TestCase(HitResolveOutcome.Invincible)]
    [TestCase(HitResolveOutcome.AlreadyDead)]
    public void IgnoredContact_DoesNotSelectProfilesOrRequireAnImpactManager(HitResolveOutcome outcome)
    {
        HitFeedbackReceiver receiver = CreateReceiver(out _, out _);
        Assert.IsNull(receiver.GetProfile(outcome));
        HitResolveResult result = outcome switch
        {
            HitResolveOutcome.Invincible => HitResolveResult.Invincible(),
            HitResolveOutcome.AlreadyDead => HitResolveResult.AlreadyDead(),
            _ => default,
        };

        ImpactSystem.HandleResolvedHit(IncomingHit(receiver.gameObject), result,
            new ImpactEffectConfig[] { new SpeedEffectConfig() });
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Parry_WithMissingOrDisabledProfileDoesNotFallBackToOrdinaryFeedback(bool disabledProfile)
    {
        HitFeedbackReceiver receiver = CreateReceiver(out HitFeedbackProfile normal, out HitFeedbackProfile parry);
        if (disabledProfile)
            parry.effects[0].enabled = false;
        else
            SetProfile(receiver, "parryProfile", null);
        var attackEffects = new ImpactEffectConfig[] { new SpeedEffectConfig() };
        AttackHitData hit = IncomingHit(receiver.gameObject);

        ImpactData data = ImpactData.FromAttackHit(hit, HitResolveOutcome.Parried);

        Assert.IsTrue(normal.HasConfiguredImpactEffects());
        Assert.AreNotSame(normal, data.TargetProfile);
        Assert.IsFalse(ImpactSystem.HasConfiguredImpactFeedback(data, attackEffects),
            "Neither incoming attack effects nor the ordinary defender Profile may supply parry feedback.");
        ImpactSystem.HandleResolvedHit(hit, HitResolveResult.Parried(), attackEffects);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Parry_RequiresAnEnabledDefenderEffectRatherThanIncomingEffects()
    {
        HitFeedbackReceiver receiver = CreateReceiver(out _, out HitFeedbackProfile parry);
        ImpactData data = ImpactData.FromAttackHit(IncomingHit(receiver.gameObject), HitResolveOutcome.Parried);
        Assert.IsTrue(ImpactSystem.HasConfiguredImpactFeedback(data, null));
        parry.effects.Clear();
        parry.effects.Add(null);
        parry.effects.Add(new SpeedEffectConfig { enabled = false });
        Assert.IsFalse(ImpactSystem.HasConfiguredImpactFeedback(data,
            new ImpactEffectConfig[] { new SpeedEffectConfig() }));
    }

    [Test]
    public void OrdinaryContact_StillRecognizesAttackAndDefenderFeedback()
    {
        HitFeedbackReceiver receiver = CreateReceiver(out HitFeedbackProfile normal, out HitFeedbackProfile parry);
        ImpactData data = ImpactData.FromAttackHit(IncomingHit(receiver.gameObject));
        Assert.AreSame(normal, data.TargetProfile);
        Assert.IsTrue(ImpactSystem.HasConfiguredImpactFeedback(data, null));
        normal.effects.Clear();
        Assert.IsFalse(ImpactSystem.HasConfiguredImpactFeedback(data, null),
            "The parry Profile must not supply feedback to an ordinary hit.");
        Assert.IsTrue(parry.HasConfiguredImpactEffects());
        Assert.IsTrue(ImpactSystem.HasConfiguredImpactFeedback(data,
            new ImpactEffectConfig[] { new SpeedEffectConfig() }));
    }

    [Test]
    public void ParryContext_KeepsOriginalParticipantsDamageAndWorldHitPoint()
    {
        HitFeedbackReceiver receiver = CreateReceiver(out _, out _);
        GameObject attackerObject = CreateObject("Incoming attacker");
        ActorCombater attacker = attackerObject.AddComponent<ActorCombater>();
        Vector3 point = new Vector3(1.25f, 2.5f, -3f);
        var hit = new AttackHitData(30f, attacker, receiver.gameObject, null, point, null, true);

        ImpactData data = ImpactData.FromAttackHit(hit, HitResolveOutcome.Parried);

        Assert.AreSame(attacker, data.Attacker);
        Assert.AreSame(receiver.gameObject, data.TargetObject);
        Assert.AreSame(attacker, data.SourceHit.Attacker);
        Assert.AreSame(receiver.gameObject, data.SourceHit.Target);
        Assert.AreEqual(30f, data.Damage);
        Assert.AreEqual(point, data.SourceHit.HitPoint);
        Assert.AreEqual(point, data.HitPoint);
        Assert.AreEqual(point, data.VfxSpawnPoint);
    }

    [Test]
    public void ColliderChildContact_UsesParentDefendersParryProfile()
    {
        HitFeedbackReceiver receiver = CreateReceiver(out _, out HitFeedbackProfile parry);
        GameObject child = CreateObject("Contact child");
        child.transform.SetParent(receiver.transform);
        ImpactData data = ImpactData.FromAttackHit(IncomingHit(child), HitResolveOutcome.Parried);
        Assert.AreSame(receiver, data.TargetReceiver);
        Assert.AreSame(parry, data.TargetProfile);
    }

    private HitFeedbackReceiver CreateReceiver(out HitFeedbackProfile normal, out HitFeedbackProfile parry)
    {
        HitFeedbackReceiver receiver = CreateObject("Defender").AddComponent<HitFeedbackReceiver>();
        normal = CreateProfile();
        parry = CreateProfile();
        SetProfile(receiver, "profile", normal);
        SetProfile(receiver, "parryProfile", parry);
        return receiver;
    }

    private HitFeedbackProfile CreateProfile()
    {
        HitFeedbackProfile profile = ScriptableObject.CreateInstance<HitFeedbackProfile>();
        _objects.Add(profile);
        profile.effects.Add(new SpeedEffectConfig());
        return profile;
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        return gameObject;
    }

    private static AttackHitData IncomingHit(GameObject target) =>
        new AttackHitData(10f, null, target, null, new Vector3(1f, 2f, 3f), null, true);

    private static void SetProfile(HitFeedbackReceiver receiver, string field, HitFeedbackProfile profile)
    {
        // Configure the same serialized references exposed by the Inspector; no scene or repository assets required.
        using (var serialized = new SerializedObject(receiver))
        {
            serialized.FindProperty(field).objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
