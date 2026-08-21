using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ActionSequenceHitBoxIntentIntegrationTests
{
    private GameObject _attackerOwner;
    private GameObject _targetOwner;
    private readonly List<GameObject> _extraOwners = new List<GameObject>();
    private ActionSequenceAsset _asset;
    private ActorHitBoxRuntime _hitBoxes;

    [TearDown]
    public void TearDown()
    {
        if (_attackerOwner != null)
            Object.DestroyImmediate(_attackerOwner);
        if (_targetOwner != null)
            Object.DestroyImmediate(_targetOwner);
        for (int i = 0; i < _extraOwners.Count; i++)
        {
            if (_extraOwners[i] != null)
                Object.DestroyImmediate(_extraOwners[i]);
        }

        _extraOwners.Clear();
        if (_asset != null)
            Object.DestroyImmediate(_asset);
    }

    [Test]
    public void HitBox_DetectHitsAddsPendingHitAndResolveAppliesDamage()
    {
        Actor attacker = CreateActor("HitIntent Attacker", Vector3.zero);
        ActorCombater target = CreateDamageableTarget("HitIntent Target", Vector3.forward * 0.25f);
        ActionSequenceRuntime runtime = CreateRuntime(attacker);
        var context = new ActionSequenceContext
        {
            Actor = attacker,
            Context = ActionContext.ForSelf(attacker),
            HitBoxes = CreateHitBoxes(attacker),
        };
        var buffer = new CombatHitBuffer();

        Assert.IsTrue(runtime.PlayFrame(context, CombatSimulationTiming.FixedDeltaTime, 1f));
        Physics.SyncTransforms();

        buffer.Begin();
        context.HitBoxes.DetectHits(buffer);

        Assert.AreEqual(1, buffer.Count);
        Assert.AreEqual(target.MaxHealth, target.CurrentHealth);

        buffer.Resolve();
        runtime.FinishFrame();

        Assert.AreEqual(target.MaxHealth - 10f, target.CurrentHealth);
    }

    [Test]
    public void HitBox_WithoutRuntimeSkipsAuthoritativeDamage()
    {
        Actor attacker = CreateActor("Preview Attacker", Vector3.zero);
        ActorCombater target = CreateDamageableTarget("Preview Target", Vector3.forward * 0.25f);
        ActionSequenceRuntime runtime = CreateRuntime(attacker);
        var context = new ActionSequenceContext
        {
            Actor = attacker,
            Context = ActionContext.ForSelf(attacker),
        };

        Assert.IsTrue(runtime.PlayFrame(context, CombatSimulationTiming.FixedDeltaTime, 1f));
        Physics.SyncTransforms();
        LogAssert.Expect(
            LogType.Warning,
            "[ActionSequenceHitBox] No ActorHitBoxRuntime is available. Authoritative hit query and damage are skipped.");

        runtime.FinishFrame();

        Assert.AreEqual(target.MaxHealth, target.CurrentHealth);
    }

    [Test]
    public void HitBox_MultipleCollidersOnOneDamageableEnqueuesOneIntent()
    {
        Actor attacker = CreateActor("MultiCollider Attacker", Vector3.zero);
        TestDamageable target = CreateTestDamageableTarget(
            "MultiCollider Target",
            Vector3.forward * 0.25f,
            Vector3.left * 0.05f,
            Vector3.right * 0.05f);
        ActionSequenceRuntime runtime = CreateRuntime(attacker);
        ActionSequenceContext context = CreateContext(attacker);
        var buffer = new CombatHitBuffer();

        DetectHits(runtime, context, buffer, 1);

        Assert.AreEqual(1, buffer.Count);
        buffer.Resolve();
        runtime.FinishFrame();

        Assert.AreEqual(1, target.DamageCount);
        Assert.AreEqual(90f, target.Health);
    }

    [Test]
    public void HitBox_RepresentativeColliderUsesNearestThenLowerInstanceId()
    {
        Actor attacker = CreateActor("Representative Attacker", Vector3.zero);
        TestDamageable nearestTarget = CreateTestDamageableTarget(
            "Nearest Target",
            Vector3.forward * 0.25f,
            Vector3.zero,
            Vector3.right * 0.45f);
        ActionSequenceRuntime nearestRuntime = CreateRuntime(attacker);
        ActionSequenceContext nearestContext = CreateContext(attacker);
        var nearestBuffer = new CombatHitBuffer();

        DetectHits(nearestRuntime, nearestContext, nearestBuffer, 1);

        Assert.AreEqual(1, nearestBuffer.Count);
        Assert.AreSame(nearestTarget.Colliders[0], nearestBuffer.Hits[0].RepresentativeCollider);

        nearestRuntime.Cancel(nearestContext);
        Object.DestroyImmediate(_targetOwner);
        _targetOwner = null;
        Object.DestroyImmediate(_asset);
        _asset = null;

        TestDamageable tieTarget = CreateTestDamageableTarget(
            "Tie Target",
            Vector3.forward * 0.25f,
            Vector3.zero,
            Vector3.zero);
        ActionSequenceRuntime tieRuntime = CreateRuntime(attacker);
        ActionSequenceContext tieContext = CreateContext(attacker);
        var tieBuffer = new CombatHitBuffer();
        Collider expected = tieTarget.Colliders[0].GetInstanceID() < tieTarget.Colliders[1].GetInstanceID()
            ? tieTarget.Colliders[0]
            : tieTarget.Colliders[1];

        DetectHits(tieRuntime, tieContext, tieBuffer, 2);

        Assert.AreEqual(1, tieBuffer.Count);
        Assert.AreSame(expected, tieBuffer.Hits[0].RepresentativeCollider);
    }

    [Test]
    public void HitBox_RejectedImpactStillCountsAsAttemptForThisWindow()
    {
        Actor attacker = CreateActor("Retry Attacker", Vector3.zero);
        TestDamageable target = CreateTestDamageableTarget(
            "Retry Target",
            Vector3.forward * 0.25f,
            Vector3.zero);
        target.Invincible = true;
        ActionSequenceRuntime runtime = CreateRuntime(attacker, durationFrames: 2, hitBoxEndFrame: 2);
        ActionSequenceContext context = CreateContext(attacker);
        var buffer = new CombatHitBuffer();

        DetectHits(runtime, context, buffer, 1);
        Assert.AreEqual(1, buffer.Count);
        buffer.Resolve();
        runtime.FinishFrame();
        Assert.AreEqual(0, target.DamageCount);

        target.Invincible = false;
        DetectHits(runtime, context, buffer, 2);
        Assert.AreEqual(0, buffer.Count);
        buffer.Resolve();
        runtime.FinishFrame();

        Assert.AreEqual(0, target.DamageCount);
        Assert.AreEqual(100f, target.Health);
    }

    [Test]
    public void HitBox_IntentSurvivesProducerExitBeforeResolve()
    {
        Actor attacker = CreateActor("Producer Exit Attacker", Vector3.zero);
        TestDamageable target = CreateTestDamageableTarget(
            "Producer Exit Target",
            Vector3.forward * 0.25f,
            Vector3.zero);
        ActionSequenceRuntime runtime = CreateRuntime(attacker);
        ActionSequenceContext context = CreateContext(attacker);
        var buffer = new CombatHitBuffer();

        DetectHits(runtime, context, buffer, 1);
        Assert.AreEqual(1, buffer.Count);

        runtime.Cancel(context);
        buffer.Resolve();

        Assert.AreEqual(1, target.DamageCount);
        Assert.AreEqual(90f, target.Health);
    }

    [Test]
    public void HitBox_DifferentClipsCanEachHitSameTarget()
    {
        Actor attacker = CreateActor("Two Clip Attacker", Vector3.zero);
        TestDamageable target = CreateTestDamageableTarget(
            "Two Clip Target",
            Vector3.forward * 0.25f,
            Vector3.zero);
        ActionSequenceRuntime runtime = CreateRuntime(attacker, duplicateHitBoxClips: true);
        ActionSequenceContext context = CreateContext(attacker);
        var buffer = new CombatHitBuffer();

        DetectHits(runtime, context, buffer, 1);

        Assert.AreEqual(2, buffer.Count);
        buffer.Resolve();
        runtime.FinishFrame();
        Assert.AreEqual(2, target.DamageCount);
        Assert.AreEqual(80f, target.Health);
    }

    [Test]
    public void HitBox_FinalFrameResolvesBeforeExitAndComplete()
    {
        Actor attacker = CreateActor("Final Frame Attacker", Vector3.zero);
        TestDamageable target = CreateTestDamageableTarget(
            "Final Frame Target",
            Vector3.forward * 0.25f,
            Vector3.zero);
        ActionSequenceRuntime runtime = CreateRuntime(attacker);
        ActionSequenceContext context = CreateContext(attacker);
        var buffer = new CombatHitBuffer();

        DetectHits(runtime, context, buffer, 1);

        Assert.IsTrue(runtime.IsPlaying);
        Assert.IsFalse(runtime.IsComplete);
        buffer.Resolve();
        Assert.AreEqual(1, target.DamageCount);
        Assert.IsTrue(runtime.IsPlaying);
        Assert.IsFalse(runtime.IsComplete);

        runtime.FinishFrame();
        Assert.IsFalse(runtime.IsPlaying);
        Assert.IsTrue(runtime.IsComplete);
    }

    private ActionSequenceRuntime CreateRuntime(
        Actor attacker,
        int durationFrames = 1,
        int hitBoxEndFrame = 1,
        bool duplicateHitBoxClips = false)
    {
        _asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _asset.EditorSetTiming(CombatSimulationTiming.FrameRate, durationFrames);
        _asset.EditorTracks.Clear();

        var track = new ActionSequenceHitBoxTrack();
        track.EditorClips.Add(CreateHitBoxClip("hit-a", 0, hitBoxEndFrame));
        if (duplicateHitBoxClips)
            track.EditorClips.Add(CreateHitBoxClip("hit-b", 0, hitBoxEndFrame));

        _asset.EditorTracks.Add(track);
        return new ActionSequenceRuntime(_asset);
    }

    private static ActionSequenceHitBoxClipDefinition CreateHitBoxClip(string editorId, int startFrame, int endFrame)
    {
        var clip = new ActionSequenceHitBoxClipDefinition
        {
            startFrame = startFrame,
            endFrame = endFrame,
            boneReference = new BoneReference
            {
                mode = BoneReference.Mode.ActorPath,
                bonePath = string.Empty,
            },
            hitboxConfig = new ActionHitBoxConfig
            {
                center = Vector3.forward * 0.25f,
                radius = 0.75f,
                height = 0.5f,
                rotation = Quaternion.identity,
            },
            dataConfig = new AttackDataConfig
            {
                _baseDamage = 10f,
                targetLayers = 1 << 8,
            },
        };

        clip.EditorSetEditorId(editorId);
        return clip;
    }

    private Actor CreateActor(string name, Vector3 position)
    {
        _attackerOwner = new GameObject(name);
        _attackerOwner.transform.position = position;
        var actor = _attackerOwner.AddComponent<Actor>();
        var combater = _attackerOwner.AddComponent<ActorCombater>();
        InvokePrivate(combater, "Awake");
        actor.combater = combater;
        return actor;
    }

    private ActorCombater CreateDamageableTarget(string name, Vector3 position)
    {
        _targetOwner = new GameObject(name);
        _targetOwner.layer = 8;
        _targetOwner.transform.position = position;
        _targetOwner.AddComponent<BoxCollider>();
        var combater = _targetOwner.AddComponent<ActorCombater>();
        SetPrivateField(combater, "_debugLog", false);
        InvokePrivate(combater, "Awake");
        return combater;
    }

    private TestDamageable CreateTestDamageableTarget(string name, Vector3 position, params Vector3[] colliderLocalPositions)
    {
        _targetOwner = new GameObject(name);
        _targetOwner.transform.position = position;
        var damageable = _targetOwner.AddComponent<TestDamageable>();

        if (colliderLocalPositions == null || colliderLocalPositions.Length == 0)
            colliderLocalPositions = new[] { Vector3.zero };

        for (int i = 0; i < colliderLocalPositions.Length; i++)
        {
            var child = new GameObject($"{name} Collider {i}");
            child.layer = 8;
            child.transform.SetParent(_targetOwner.transform, false);
            child.transform.localPosition = colliderLocalPositions[i];
            var collider = child.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.1f;
            damageable.Colliders.Add(collider);
            _extraOwners.Add(child);
        }

        return damageable;
    }

    private static ActionSequenceContext CreateContext(Actor attacker)
    {
        return new ActionSequenceContext
        {
            Actor = attacker,
            Context = ActionContext.ForSelf(attacker),
            HitBoxes = new ActorHitBoxRuntime(attacker),
        };
    }

    private ActorHitBoxRuntime CreateHitBoxes(Actor attacker)
    {
        _hitBoxes = new ActorHitBoxRuntime(attacker);
        return _hitBoxes;
    }

    private static void DetectHits(
        ActionSequenceRuntime runtime,
        ActionSequenceContext context,
        CombatHitBuffer buffer,
        int tickId)
    {
        Assert.IsTrue(runtime.PlayFrame(context, CombatSimulationTiming.FixedDeltaTime, 1f));
        Physics.SyncTransforms();
        buffer.Begin();
        context.HitBoxes.DetectHits(buffer);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private sealed class TestDamageable : MonoBehaviour, IDamageable
    {
        public readonly List<Collider> Colliders = new List<Collider>();
        public bool Invincible;
        public float Health = 100f;
        public int DamageCount;
        public bool IsDead => Health <= 0f;

        public HitResolveResult TakeDamage(AttackHitData attackData)
        {
            if (IsDead)
                return HitResolveResult.AlreadyDead();
            if (Invincible)
                return HitResolveResult.Invincible();

            DamageCount++;
            float previousHealth = Health;
            Health = Mathf.Max(0f, Health - attackData.Damage);
            return HitResolveResult.Normal(false, previousHealth > 0f && Health <= 0f);
        }
    }
}
