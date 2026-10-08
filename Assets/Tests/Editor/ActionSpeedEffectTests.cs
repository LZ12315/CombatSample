#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ActionSpeedEffectTests
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

    [TestCase(0f, 0)]
    [TestCase(0.078f, 5)]
    [TestCase(0.15f, 9)]
    [TestCase(1f / 60f, 1)]
    [TestCase(2f / 60f, 2)]
    [TestCase(0.6f, 36)]
    [TestCase(32f / 60f, 32)]
    [TestCase(0.6000001f, 37)]
    [TestCase(1e-9f, 1)]
    [TestCase(float.Epsilon, 1)]
    public void DurationConversion_UsesCeilingWithoutAddingAnAccidentalBoundaryTick(
        float seconds,
        int expectedTicks)
    {
        Assert.IsTrue(ActionSpeedEffect.TryConvertDurationToTicks(seconds, out int ticks));
        Assert.AreEqual(expectedTicks, ticks);
    }

    [Test]
    public void DurationConversion_RejectsInvalidOrUnrepresentableValues()
    {
        Assert.IsFalse(ActionSpeedEffect.TryConvertDurationToTicks(float.NaN, out _));
        Assert.IsFalse(ActionSpeedEffect.TryConvertDurationToTicks(float.PositiveInfinity, out _));
        Assert.IsFalse(ActionSpeedEffect.TryConvertDurationToTicks(-0.01f, out _));
        Assert.IsFalse(ActionSpeedEffect.TryConvertDurationToTicks(float.MaxValue, out _));
    }

    [Test]
    public void AffectBothParties_AttackerAndDelayedTargetUseIndependentFiveTickDurations()
    {
        CreateParticipant("Attacker", out ActorCombater attacker, out ActionPlayer attackerPlayer);
        GameObject target = CreateParticipant("Target", out _, out ActionPlayer targetPlayer);
        var effect = new ActionSpeedEffect();
        effect.Execute(
            new ImpactData(attacker, target, Vector3.zero, 0f),
            new SpeedEffectConfig
            {
                duration = 0.078f,
                speedScale = 0.2f,
                affectBothParties = true,
            });

        Assert.AreEqual(0.2f, attackerPlayer.ExternalSpeedScale);
        Assert.AreEqual(1f, targetPlayer.ExternalSpeedScale);

        for (int tick = 1; tick <= 4; tick++)
        {
            Assert.IsTrue(effect.AdvanceFixedTick());
            Assert.AreEqual(0.2f, attackerPlayer.ExternalSpeedScale, $"attacker tick {tick}");
            Assert.AreEqual(1f, targetPlayer.ExternalSpeedScale, $"target tick {tick}");
        }

        Assert.IsTrue(effect.AdvanceFixedTick());
        Assert.AreEqual(0.2f, attackerPlayer.ExternalSpeedScale);
        Assert.AreEqual(0.2f, targetPlayer.ExternalSpeedScale);

        Assert.IsTrue(effect.AdvanceFixedTick());
        Assert.AreEqual(1f, attackerPlayer.ExternalSpeedScale);
        Assert.AreEqual(0.2f, targetPlayer.ExternalSpeedScale);

        for (int tick = 7; tick <= 9; tick++)
        {
            Assert.IsTrue(effect.AdvanceFixedTick());
            Assert.AreEqual(0.2f, targetPlayer.ExternalSpeedScale, $"target tick {tick}");
        }

        Assert.IsFalse(effect.AdvanceFixedTick());
        Assert.AreEqual(1f, targetPlayer.ExternalSpeedScale);
    }

    [Test]
    public void OrdinaryUpdate_DoesNotConsumeFixedTickLifetime()
    {
        CreateParticipant("Attacker", out ActorCombater attacker, out ActionPlayer player);
        var effect = new ActionSpeedEffect();
        effect.Execute(
            new ImpactData(attacker, null, Vector3.zero, 0f),
            new SpeedEffectConfig { duration = 1f / 60f, speedScale = 0f });

        for (int i = 0; i < 20; i++)
            Assert.IsTrue(effect.Update());
        Assert.AreEqual(0f, player.ExternalSpeedScale);

        Assert.IsTrue(effect.AdvanceFixedTick());
        Assert.AreEqual(0f, player.ExternalSpeedScale);
        Assert.IsFalse(effect.AdvanceFixedTick());
        Assert.AreEqual(1f, player.ExternalSpeedScale);
    }

    [Test]
    public void OverlappingEffects_ReleaseOnlyTheirOwnMinimumModifier()
    {
        CreateParticipant("Attacker", out ActorCombater attacker, out ActionPlayer player);
        var shortEffect = new ActionSpeedEffect();
        var longEffect = new ActionSpeedEffect();
        var data = new ImpactData(attacker, null, Vector3.zero, 0f);
        shortEffect.Execute(data, new SpeedEffectConfig { duration = 1f / 60f, speedScale = 0.3f });
        longEffect.Execute(data, new SpeedEffectConfig { duration = 3f / 60f, speedScale = 0.1f });

        Assert.AreEqual(0.1f, player.ExternalSpeedScale);
        Assert.IsTrue(shortEffect.AdvanceFixedTick());
        Assert.IsTrue(longEffect.AdvanceFixedTick());
        Assert.AreEqual(0.1f, player.ExternalSpeedScale);

        Assert.IsFalse(shortEffect.AdvanceFixedTick());
        Assert.IsTrue(longEffect.AdvanceFixedTick());
        Assert.AreEqual(0.1f, player.ExternalSpeedScale);

        Assert.IsTrue(longEffect.AdvanceFixedTick());
        Assert.AreEqual(0.1f, player.ExternalSpeedScale);
        Assert.IsFalse(longEffect.AdvanceFixedTick());
        Assert.AreEqual(1f, player.ExternalSpeedScale);
    }

    [Test]
    public void ResetAndNoOpConfiguration_DoNotLeaveSpeedTokens()
    {
        CreateParticipant("Attacker", out ActorCombater attacker, out ActionPlayer player);
        var effect = new ActionSpeedEffect();
        var data = new ImpactData(attacker, null, Vector3.zero, 0f);

        effect.Execute(data, new SpeedEffectConfig { duration = 0.2f, speedScale = 1f });
        Assert.IsFalse(effect.IsActive);
        Assert.AreEqual(1f, player.ExternalSpeedScale);

        effect.Execute(data, new SpeedEffectConfig { duration = 0.2f, speedScale = 0f });
        Assert.AreEqual(0f, player.ExternalSpeedScale);
        effect.Reset();
        effect.Reset();
        Assert.AreEqual(1f, player.ExternalSpeedScale);
    }

    [Test]
    public void InvalidConfiguration_IsRejectedBeforeAnyTokenIsCreated()
    {
        CreateParticipant("Attacker", out ActorCombater attacker, out ActionPlayer player);
        var effect = new ActionSpeedEffect();
        LogAssert.Expect(
            LogType.Warning,
            "[ActionSpeedEffect] Effect was rejected: duration must be finite, non-negative, and representable in combat ticks.");

        effect.Execute(
            new ImpactData(attacker, null, Vector3.zero, 0f),
            new SpeedEffectConfig { duration = float.NaN, speedScale = 0f });

        Assert.IsFalse(effect.IsActive);
        Assert.AreEqual(1f, player.ExternalSpeedScale);
    }

    [Test]
    public void DisabledTargetDuringDelay_IsNotReappliedLater()
    {
        CreateParticipant("Attacker", out ActorCombater attacker, out _);
        GameObject target = CreateParticipant("Target", out _, out ActionPlayer targetPlayer);
        var effect = new ActionSpeedEffect();
        effect.Execute(
            new ImpactData(attacker, target, Vector3.zero, 0f),
            new SpeedEffectConfig { duration = 0.1f, speedScale = 0f, affectBothParties = true });

        targetPlayer.enabled = false;
        for (int i = 0; i < 8 && effect.IsActive; i++)
            effect.AdvanceFixedTick();

        targetPlayer.enabled = true;
        Assert.AreEqual(1f, targetPlayer.ExternalSpeedScale);
        effect.Reset();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void OneTargetComponentDisabledDuringDelay_DoesNotRegainItsPendingEffect(bool disablePlayer)
    {
        GameObject target = CreateParticipant("Target", out _, out ActionPlayer player);
        ActorMotor motor = target.AddComponent<ActorMotor>();
        var effect = new ActionSpeedEffect();
        try
        {
            effect.Execute(new ImpactData(null, target, Vector3.zero, 0f),
                new SpeedEffectConfig { duration = 0.1f, speedScale = 0f, affectBothParties = true });
            Behaviour disabled = disablePlayer ? (Behaviour)player : motor;
            disabled.enabled = false;
            Assert.IsTrue(effect.AdvanceFixedTick());
            disabled.enabled = true;
            for (int tick = 2; tick <= 5; tick++)
                effect.AdvanceFixedTick();

            Assert.AreEqual(disablePlayer ? 1f : 0f, player.ExternalSpeedScale);
            Assert.AreEqual(disablePlayer ? 0f : 1f, motor.MovementTimeScale);
        }
        finally { effect.Reset(); }
        Assert.AreEqual(1f, player.ExternalSpeedScale);
        Assert.AreEqual(1f, motor.MovementTimeScale);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActorDisabledOrDestroyed_CancelsActiveAndPendingSpeedEffects(bool destroyActor)
    {
        GameObject attackerObject = CreateParticipant("Attacker", out ActorCombater attacker, out ActionPlayer player);
        Actor attackerActor = attackerObject.AddComponent<Actor>();
        GameObject target = CreateParticipant("Target", out _, out ActionPlayer targetPlayer);
        Actor targetActor = target.AddComponent<Actor>();
        var effect = new ActionSpeedEffect();
        try
        {
            effect.Execute(new ImpactData(attacker, target, Vector3.zero, 0f),
                new SpeedEffectConfig { duration = 0.1f, speedScale = 0f, affectBothParties = true });
            Assert.AreEqual(0f, player.ExternalSpeedScale);
            if (destroyActor)
            {
                Object.DestroyImmediate(attackerActor);
                Object.DestroyImmediate(targetActor);
            }
            else
            {
                attackerActor.enabled = false;
                targetActor.enabled = false;
            }
            Assert.IsFalse(effect.AdvanceFixedTick());
            Assert.AreEqual(1f, player.ExternalSpeedScale);
            if (!destroyActor)
            {
                attackerActor.enabled = true;
                targetActor.enabled = true;
            }
            for (int i = 0; i < 5; i++)
                Assert.IsFalse(effect.AdvanceFixedTick());
            Assert.AreEqual(1f, targetPlayer.ExternalSpeedScale);
        }
        finally { effect.Reset(); }
    }

    [TestCase(false, false, 1d, 0.3f, 0.3f)]
    [TestCase(true, false, 0.05d, 0.3f, 0.05f)]
    [TestCase(true, false, 0.5d, 0.3f, 0.3f)]
    [TestCase(true, true, 1d, 1f, 0f)]
    public void AnimationDelta_UsesMovementScaleAndActionMinimumExactlyOnce(
        bool hasAction,
        bool paused,
        double playbackSpeed,
        float movementScale,
        float expectedScale)
    {
        float delta = ActorSimulationRuntime.CalculateAnimationDeltaSeconds(
            CombatSimulationTiming.FixedDeltaTime,
            hasAction,
            paused,
            playbackSpeed,
            movementScale);
        Assert.AreEqual(CombatSimulationTiming.FixedDeltaTime * expectedScale, delta, 1e-7f);
    }

    [TestCase(false, false, 2d, 1f)]
    [TestCase(true, false, 2d, 2f)]
    [TestCase(true, true, 2d, 0f)]
    public void AnimationDelta_WithoutMotorUsesOnlyActionSpeed(bool hasAction, bool paused, double speed, float expectedScale)
    {
        Assert.AreEqual(CombatSimulationTiming.FixedDeltaTime * expectedScale,
            ActorSimulationRuntime.CalculateAnimationDeltaSeconds(
                CombatSimulationTiming.FixedDeltaTime, hasAction, paused, speed, null), 1e-7f);
    }

    [Test]
    public void TargetProfileFeedback_IsRecognizedWhenAttackEffectsAreEmpty()
    {
        HitFeedbackProfile profile = ScriptableObject.CreateInstance<HitFeedbackProfile>();
        _objects.Add(profile);
        profile.effects.Add(new SpeedEffectConfig());
        var data = new ImpactData(null, null, Vector3.zero, 0f) { TargetProfile = profile };

        Assert.IsTrue(ImpactSystem.HasConfiguredImpactFeedback(data, null));
        profile.effects.Clear();
        Assert.IsFalse(ImpactSystem.HasConfiguredImpactFeedback(data, null));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AttackFeedback_RequiresAnEnabledEffect(bool enabled)
    {
        var effects = new ImpactEffectConfig[] { null, new SpeedEffectConfig { enabled = enabled } };
        Assert.AreEqual(enabled, ImpactSystem.HasConfiguredImpactFeedback(null, effects));
        Assert.IsFalse(ImpactSystem.HasConfiguredImpactFeedback(null, null));
    }

    [Test]
    public void ConfirmedHit_WithoutFeedbackDoesNotRequireAnImpactManager()
    {
        // No scene objects or clock are needed for a hit with no configured feedback.
        ImpactSystem.HandleConfirmedHit(new AttackHitData(10f, null, null, null, Vector3.zero),
            new ImpactEffectConfig[] { null, new SpeedEffectConfig { enabled = false } });
        UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void NoCombatClock_RejectsTargetProfileSpeedEffectWithoutCreatingTokens()
    {
        CreateParticipant("Attacker", out ActorCombater attacker, out ActionPlayer attackerPlayer);
        GameObject target = CreateParticipant("Target", out _, out ActionPlayer targetPlayer);
        HitFeedbackReceiver receiver = target.AddComponent<HitFeedbackReceiver>();
        HitFeedbackProfile profile = ScriptableObject.CreateInstance<HitFeedbackProfile>();
        _objects.Add(profile);
        profile.effects.Add(new SpeedEffectConfig
        {
            duration = 0.2f,
            speedScale = 0.1f,
            affectBothParties = true,
        });

        var systemObject = new GameObject("ImpactSystem");
        _objects.Add(systemObject);
        systemObject.AddComponent<CinemachineImpulseSource>();
        ImpactSystem system = systemObject.AddComponent<ImpactSystem>();
        var data = new ImpactData(attacker, target, Vector3.zero, 0f)
        {
            TargetReceiver = receiver,
            TargetProfile = profile,
        };

        system.ApplyImpact(data, null);
        Assert.AreEqual(1f, attackerPlayer.ExternalSpeedScale);
        Assert.AreEqual(1f, targetPlayer.ExternalSpeedScale);
        Assert.IsFalse(system.HasActiveEffects());
    }

    private GameObject CreateParticipant(
        string name,
        out ActorCombater combater,
        out ActionPlayer player)
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        player = gameObject.AddComponent<ActionPlayer>();
        combater = gameObject.AddComponent<ActorCombater>();
        return gameObject;
    }
}
#endif
