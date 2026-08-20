using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ActionSequenceHitBoxIntentIntegrationTests
{
    private GameObject _attackerOwner;
    private GameObject _targetOwner;
    private ActionSequenceAsset _asset;

    [TearDown]
    public void TearDown()
    {
        if (_attackerOwner != null)
            Object.DestroyImmediate(_attackerOwner);
        if (_targetOwner != null)
            Object.DestroyImmediate(_targetOwner);
        if (_asset != null)
            Object.DestroyImmediate(_asset);
    }

    [Test]
    public void HitBox_PostWorldEnqueuesIntentAndResolveAppliesDamage()
    {
        Actor attacker = CreateActor("HitIntent Attacker", Vector3.zero);
        ActorCombater target = CreateDamageableTarget("HitIntent Target", Vector3.forward * 0.25f);
        ActionSequenceRuntime runtime = CreateRuntime(attacker);
        var context = new ActionSequenceContext
        {
            Actor = attacker,
            Context = ActionContext.ForSelf(attacker),
        };
        var buffer = new CombatHitIntentBuffer();

        Assert.IsTrue(runtime.BeginFrame(context, CombatSimulationTiming.FixedDeltaTime, 1f));
        runtime.ExecutePreWorld();
        Physics.SyncTransforms();

        buffer.Begin(1);
        runtime.ExecutePostWorld(buffer);

        Assert.AreEqual(1, buffer.Count);
        Assert.AreEqual(target.MaxHealth, target.CurrentHealth);

        buffer.Resolve();
        runtime.EndFrame();

        Assert.AreEqual(target.MaxHealth - 10f, target.CurrentHealth);
    }

    [Test]
    public void HitBox_WithoutSinkSkipsAuthoritativeDamage()
    {
        Actor attacker = CreateActor("Preview Attacker", Vector3.zero);
        ActorCombater target = CreateDamageableTarget("Preview Target", Vector3.forward * 0.25f);
        ActionSequenceRuntime runtime = CreateRuntime(attacker);
        var context = new ActionSequenceContext
        {
            Actor = attacker,
            Context = ActionContext.ForSelf(attacker),
        };

        Assert.IsTrue(runtime.BeginFrame(context, CombatSimulationTiming.FixedDeltaTime, 1f));
        runtime.ExecutePreWorld();
        Physics.SyncTransforms();
        LogAssert.Expect(
            LogType.Warning,
            "[ActionSequenceHitBox] No CombatHitIntent sink is available. Authoritative hit query and damage are skipped.");

        runtime.ExecutePostWorld();
        runtime.EndFrame();

        Assert.AreEqual(target.MaxHealth, target.CurrentHealth);
    }

    private ActionSequenceRuntime CreateRuntime(Actor attacker)
    {
        _asset = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _asset.EditorSetTiming(CombatSimulationTiming.FrameRate, 1);
        _asset.EditorTracks.Clear();

        var clip = new ActionSequenceHitBoxClipDefinition
        {
            startFrame = 0,
            endFrame = 1,
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

        var track = new ActionSequenceHitBoxTrack();
        track.EditorClips.Add(clip);
        _asset.EditorTracks.Add(track);
        return new ActionSequenceRuntime(_asset);
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
}
