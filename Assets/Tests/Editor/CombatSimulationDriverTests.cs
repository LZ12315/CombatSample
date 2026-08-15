using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using KinematicCharacterController;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CombatSimulationDriverStructureTests
{
    private const string ManagerPrefabPath = "Assets/Prefabs/Function/Manager.prefab";

    [Test]
    public void ManagerPrefab_HasOneEnabledDriverAndResolver()
    {
        GameObject manager = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);

        Assert.IsNotNull(manager, $"Could not load {ManagerPrefabPath}.");

        CombatSimulationDriver[] drivers = manager.GetComponents<CombatSimulationDriver>();
        ActorCollisionResolver[] resolvers = manager.GetComponents<ActorCollisionResolver>();

        Assert.AreEqual(1, drivers.Length);
        Assert.IsTrue(drivers[0].enabled);
        Assert.AreEqual(1, resolvers.Length);
    }

    [Test]
    public void Resolver_HasOneExplicitEntryPointAndNoUnityFixedUpdate()
    {
        const BindingFlags instanceMethods =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        MethodInfo fixedUpdate = typeof(ActorCollisionResolver).GetMethod("FixedUpdate", instanceMethods);
        MethodInfo explicitStep = typeof(ActorCollisionResolver).GetMethod(
            "ResolveFixedStep",
            instanceMethods);

        Assert.IsNull(fixedUpdate, "Resolver must not keep a second Unity FixedUpdate entry point.");
        Assert.IsNotNull(explicitStep);
        Assert.IsTrue(explicitStep.IsAssembly);
        Assert.AreEqual(typeof(void), explicitStep.ReturnType);
        Assert.AreEqual(0, explicitStep.GetParameters().Length);
    }
}

public sealed class CombatSimulationDriverPlayModeTests
{
    private const float CombatFixedDeltaTime = 1f / 60f;

    private GameObject _driverOwner;
    private GameObject _duplicateOwner;
    private GameObject _probeOwner;
    private GameObject _actorOwnerA;
    private GameObject _actorOwnerB;
    private GameObject _sequenceActorOwner;
    private ActionAsset _sequenceAction;
    private CombatSimulationDriver _driver;
    private bool _savedInterpolate;
    private bool _hasSavedInterpolate;
    private float _savedTimeScale;
    private bool _hasSavedTimeScale;

    [UnityTest]
    public IEnumerator RuntimeFixedUpdate_DisablesAutoSimulationTicksOnceAndPairsInterpolation()
    {
        yield return new EnterPlayMode();

        _savedTimeScale = Time.timeScale;
        _hasSavedTimeScale = true;
        Time.timeScale = 1f;

        KinematicCharacterSystem.EnsureCreation();
        yield return ReplaceLoadedScenesWithEmptyTestScene();

        Assert.AreEqual(0, KinematicCharacterSystem.CharacterMotors.Count);
        Assert.AreEqual(0, KinematicCharacterSystem.PhysicsMovers.Count);
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(CombatFixedDeltaTime).Within(0.000001f));

        // Establish a deterministic pre-acquisition value for the isolated test Driver.
        KinematicCharacterSystem.Settings.AutoSimulation = true;
        _savedInterpolate = KinematicCharacterSystem.Settings.Interpolate;
        _hasSavedInterpolate = true;

        _driverOwner = new GameObject("CombatSimulationDriver Test Owner");
        _driverOwner.AddComponent<ActorCollisionResolver>();
        _driver = _driverOwner.AddComponent<CombatSimulationDriver>();

        Assert.IsTrue(_driver.IsSimulationOwner);
        Assert.IsFalse(KinematicCharacterSystem.Settings.AutoSimulation);

        LogAssert.Expect(
            LogType.Error,
            "[CombatSimulationDriver] Only one active Driver is allowed. " +
            "Existing owner: 'CombatSimulationDriver Test Owner'.");

        _duplicateOwner = new GameObject("CombatSimulationDriver Duplicate");
        _duplicateOwner.AddComponent<ActorCollisionResolver>();
        CombatSimulationDriver duplicate = _duplicateOwner.AddComponent<CombatSimulationDriver>();

        Assert.IsFalse(duplicate.IsSimulationOwner);
        Assert.IsFalse(duplicate.enabled);
        Assert.IsFalse(KinematicCharacterSystem.Settings.AutoSimulation);

        // A rejected Driver must never restore the setting owned by the real Driver.
        Object.DestroyImmediate(_duplicateOwner);
        _duplicateOwner = null;
        Assert.IsFalse(KinematicCharacterSystem.Settings.AutoSimulation);

        // The owner restores the value it captured, then reacquires cleanly.
        _driver.enabled = false;
        Assert.IsTrue(KinematicCharacterSystem.Settings.AutoSimulation);
        _driver.enabled = true;
        Assert.IsTrue(_driver.IsSimulationOwner);
        Assert.IsFalse(KinematicCharacterSystem.Settings.AutoSimulation);

        var controller = new ProbeController { RequestedVelocity = Vector3.right };
        const float farFromScene = 10000f;
        Vector3 startPosition = new Vector3(farFromScene, farFromScene, farFromScene);

        _probeOwner = new GameObject("CombatSimulationDriver KCC Probe");
        _probeOwner.transform.position = startPosition;
        KinematicCharacterMotor motor = _probeOwner.AddComponent<KinematicCharacterMotor>();
        motor.CharacterController = controller;
        motor.SetMovementCollisionsSolvingActivation(false);
        motor.SetPosition(startPosition);
        motor.InitialTickPosition = startPosition + Vector3.up * 123f;

        CombatSimulationFixedObservationProbe observation =
            _probeOwner.AddComponent<CombatSimulationFixedObservationProbe>();
        observation.Motor = motor;

        KinematicCharacterSystem.Settings.Interpolate = true;
        yield return WaitForObservation(observation, 0);

        Assert.AreEqual(observation.Count, controller.BeforeCount);
        Assert.AreEqual(controller.BeforeCount, controller.UpdateRotationCount);
        Assert.AreEqual(controller.BeforeCount, controller.UpdateVelocityCount);
        Assert.AreEqual(controller.BeforeCount, controller.PostGroundingCount);
        Assert.AreEqual(controller.BeforeCount, controller.AfterCount);
        AssertFixedTimesAreUnique(controller.FixedTimes);
        AssertFixedDeltaTimes(controller.DeltaTimes);

        Assert.Greater(motor.TransientPosition.x, startPosition.x);
        Assert.That(Vector3.Distance(observation.InitialTickPosition, startPosition), Is.LessThan(0.0001f));
        Assert.That(
            Vector3.Distance(observation.TransformPosition, observation.InitialTickPosition),
            Is.LessThan(0.0001f));
        Assert.That(
            Vector3.Distance(observation.TransformPosition, observation.TransientPosition),
            Is.GreaterThan(0.0001f));

        int observationsBeforeNonInterpolatedStep = observation.Count;
        KinematicCharacterSystem.Settings.Interpolate = false;
        yield return WaitForObservation(observation, observationsBeforeNonInterpolatedStep);

        Assert.AreEqual(observation.Count, controller.BeforeCount);
        AssertFixedTimesAreUnique(controller.FixedTimes);
        AssertFixedDeltaTimes(controller.DeltaTimes);
        Assert.That(
            Vector3.Distance(observation.TransformPosition, observation.TransientPosition),
            Is.LessThan(0.0001f));

        yield return VerifySequenceCrossesWorldMotionBarrier(controller, motor, observation);
        yield return VerifyResolverRunsAfterKcc();
        yield return VerifyWorldFaultStopsFurtherSimulation(controller);

        Object.DestroyImmediate(_probeOwner);
        _probeOwner = null;

        CleanupRuntimeObjects();
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlaying)
        {
            CleanupRuntimeObjects();
            yield return new ExitPlayMode();
        }
    }

    private static IEnumerator ReplaceLoadedScenesWithEmptyTestScene()
    {
        Scene testScene = SceneManager.CreateScene("CombatSimulationDriver Test Scene");
        SceneManager.SetActiveScene(testScene);

        var scenesToUnload = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene != testScene && scene.isLoaded)
                scenesToUnload.Add(scene);
        }

        for (int i = 0; i < scenesToUnload.Count; i++)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scenesToUnload[i]);
            while (unload != null && !unload.isDone)
                yield return null;
        }
    }

    private static IEnumerator WaitForObservation(
        CombatSimulationFixedObservationProbe observation,
        int previousCount)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (observation != null &&
               observation.Count <= previousCount &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.IsNotNull(observation);
        Assert.Greater(observation.Count, previousCount, "Timed out waiting for a fixed simulation step.");
    }

    private static void AssertFixedTimesAreUnique(IReadOnlyList<float> fixedTimes)
    {
        for (int i = 1; i < fixedTimes.Count; i++)
        {
            Assert.Greater(
                fixedTimes[i],
                fixedTimes[i - 1],
                "KCC was simulated more than once during the same Unity fixed tick.");
        }
    }

    private static void AssertFixedDeltaTimes(IReadOnlyList<float> deltaTimes)
    {
        for (int i = 0; i < deltaTimes.Count; i++)
            Assert.That(deltaTimes[i], Is.EqualTo(Time.fixedDeltaTime).Within(0.000001f));
    }

    private IEnumerator VerifyResolverRunsAfterKcc()
    {
        const float farFromScene = 10000f;
        Vector3 startA = new Vector3(farFromScene, farFromScene, farFromScene);
        Vector3 startB = startA + Vector3.right * 1.1f;

        ActorMotor actorA = CreateActorMotor("Resolver Order Actor A", startA, out _actorOwnerA);
        ActorMotor actorB = CreateActorMotor("Resolver Order Actor B", startB, out _actorOwnerB);

        actorA.SetGravityScale(0f);
        actorB.SetGravityScale(0f);
        actorA.AddHorizontalImpulse(Vector3.right * 10f);

        float initialDistance = HorizontalDistance(actorA, actorB);
        float combinedRadius = actorA.Capsule.radius + actorB.Capsule.radius;
        Assert.Greater(initialDistance, combinedRadius);

        KinematicCharacterSystem.Settings.Interpolate = false;
        float deadline = Time.realtimeSinceStartup + 5f;
        while (actorA.RequestedVelocity.x <= 0f && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.Greater(actorA.Motor.TransientPosition.x, startA.x);
        Assert.Greater(actorA.RequestedVelocity.x, 0f, "Timed out waiting for ActorMotor's KCC step.");
        Assert.GreaterOrEqual(HorizontalDistance(actorA, actorB), combinedRadius - 0.001f);
    }

    private IEnumerator VerifySequenceCrossesWorldMotionBarrier(
        ProbeController controller,
        KinematicCharacterMotor motor,
        CombatSimulationFixedObservationProbe observation)
    {
        var phaseEvents = new List<string>();
        Collider probeCollider = motor.GetComponent<Collider>();
        Assert.IsNotNull(probeCollider);

        _sequenceActorOwner = new GameObject("CombatSimulationDriver Sequence Actor");
        _sequenceActorOwner.AddComponent<Actor>();
        ActionPlayer player = _sequenceActorOwner.AddComponent<ActionPlayer>();

        _sequenceAction = ScriptableObject.CreateInstance<ActionAsset>();
        _sequenceAction.SetPlaybackBackend(ActionPlaybackBackend.Sequence);
        _sequenceAction.SequenceData.EditorSetTiming(60, 1);
        _sequenceAction.SequenceData.EditorTracks.Clear();
        _sequenceAction.SequenceData.EditorTracks.Add(new DriverPhaseProbeTrack(
            ActionSequenceClipPhase.State,
            new DriverPhaseProbeClip(
                ActionSequenceClipPhase.State,
                phaseEvents,
                "pre")));
        _sequenceAction.SequenceData.EditorTracks.Add(new DriverPhaseProbeTrack(
            ActionSequenceClipPhase.HitBox,
            new DriverPhaseProbeClip(
                ActionSequenceClipPhase.HitBox,
                phaseEvents,
                "post",
                motor,
                probeCollider)));

        controller.RequestedVelocity = Vector3.right * 120f;
        controller.PhaseEvents = phaseEvents;
        KinematicCharacterSystem.Settings.Interpolate = true;
        int previousObservationCount = observation.Count;

        player.BeginAction(_sequenceAction, ActionContext.None);
        Assert.AreEqual(0, phaseEvents.Count, "Frame 0 must wait for the fixed simulation tick.");

        yield return WaitForObservation(observation, previousObservationCount);
        AssertSequencePhaseOrder(phaseEvents);
        Assert.IsNull(player.CurrentAction, "A one-frame Sequence must finish in the same EndFrame.");
        Assert.That(
            Vector3.Distance(observation.TransformPosition, observation.InitialTickPosition),
            Is.LessThan(0.0001f),
            "Interpolation Post must run after Sequence PostWorld and restore the rendered Transform.");

        phaseEvents.Clear();
        KinematicCharacterSystem.Settings.Interpolate = false;
        previousObservationCount = observation.Count;
        player.BeginAction(_sequenceAction, ActionContext.None);

        yield return WaitForObservation(observation, previousObservationCount);
        AssertSequencePhaseOrder(phaseEvents);
        Assert.That(
            Vector3.Distance(observation.TransformPosition, observation.TransientPosition),
            Is.LessThan(0.0001f),
            "Disabling interpolation must not change the Sequence phase order.");

        controller.PhaseEvents = null;
        controller.RequestedVelocity = Vector3.right;
    }

    private IEnumerator VerifyWorldFaultStopsFurtherSimulation(ProbeController controller)
    {
        LogAssert.Expect(
            LogType.Exception,
            new Regex("InvalidOperationException: Injected world simulation fault"));

        controller.ThrowOnBefore = true;
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!_driver.IsSimulationFaulted && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.IsTrue(_driver.IsSimulationFaulted, "Timed out waiting for the Driver fault state.");
        Assert.IsTrue(_driver.IsSimulationOwner);
        Assert.IsFalse(KinematicCharacterSystem.Settings.AutoSimulation);

        int beforeCountAtFault = controller.BeforeCount;
        yield return null;
        yield return null;
        Assert.AreEqual(
            beforeCountAtFault,
            controller.BeforeCount,
            "A faulted Driver must not continue partially trustworthy world simulation.");
    }

    private static void AssertSequencePhaseOrder(List<string> phaseEvents)
    {
        int preIndex = phaseEvents.IndexOf("pre");
        int kccIndex = phaseEvents.IndexOf("kcc");
        int postIndex = phaseEvents.IndexOf("post-synced");
        int exitIndex = phaseEvents.IndexOf("exit");

        Assert.GreaterOrEqual(preIndex, 0);
        Assert.Greater(kccIndex, preIndex);
        Assert.Greater(postIndex, kccIndex);
        Assert.Greater(exitIndex, postIndex);
    }

    private static ActorMotor CreateActorMotor(string name, Vector3 position, out GameObject owner)
    {
        owner = new GameObject(name);
        owner.transform.position = position;

        ActorMotor actorMotor = owner.AddComponent<ActorMotor>();
        actorMotor.Motor.SetGroundSolvingActivation(false);
        actorMotor.Motor.SetMovementCollisionsSolvingActivation(false);
        actorMotor.Motor.SetPosition(position);
        return actorMotor;
    }

    private static float HorizontalDistance(ActorMotor a, ActorMotor b)
    {
        Vector3 aPosition = a.Motor.TransientPosition + a.Capsule.center;
        Vector3 bPosition = b.Motor.TransientPosition + b.Capsule.center;
        aPosition.y = 0f;
        bPosition.y = 0f;
        return Vector3.Distance(aPosition, bPosition);
    }

    private void CleanupRuntimeObjects()
    {
        if (_probeOwner != null)
        {
            Object.DestroyImmediate(_probeOwner);
            _probeOwner = null;
        }

        if (_actorOwnerA != null)
        {
            Object.DestroyImmediate(_actorOwnerA);
            _actorOwnerA = null;
        }

        if (_actorOwnerB != null)
        {
            Object.DestroyImmediate(_actorOwnerB);
            _actorOwnerB = null;
        }

        if (_sequenceActorOwner != null)
        {
            Object.DestroyImmediate(_sequenceActorOwner);
            _sequenceActorOwner = null;
        }

        if (_sequenceAction != null)
        {
            Object.DestroyImmediate(_sequenceAction);
            _sequenceAction = null;
        }

        if (_duplicateOwner != null)
        {
            Object.DestroyImmediate(_duplicateOwner);
            _duplicateOwner = null;
        }

        if (_driverOwner != null)
        {
            Object.DestroyImmediate(_driverOwner);
            _driverOwner = null;
            _driver = null;
        }

        if (_hasSavedInterpolate && KinematicCharacterSystem.Settings != null)
            KinematicCharacterSystem.Settings.Interpolate = _savedInterpolate;

        _hasSavedInterpolate = false;

        if (_hasSavedTimeScale)
        {
            Time.timeScale = _savedTimeScale;
            _hasSavedTimeScale = false;
        }
    }

    private sealed class ProbeController : ICharacterController
    {
        public readonly List<float> FixedTimes = new List<float>();
        public readonly List<float> DeltaTimes = new List<float>();

        public Vector3 RequestedVelocity;
        public List<string> PhaseEvents;
        public bool ThrowOnBefore;
        public int BeforeCount;
        public int UpdateRotationCount;
        public int UpdateVelocityCount;
        public int PostGroundingCount;
        public int AfterCount;

        public void BeforeCharacterUpdate(float deltaTime)
        {
            BeforeCount++;
            FixedTimes.Add(Time.fixedTime);
            DeltaTimes.Add(deltaTime);

            if (ThrowOnBefore)
                throw new System.InvalidOperationException("Injected world simulation fault.");
        }

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            UpdateRotationCount++;
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            UpdateVelocityCount++;
            currentVelocity = RequestedVelocity;
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            PostGroundingCount++;
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            AfterCount++;
            PhaseEvents?.Add("kcc");
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            return false;
        }

        public void OnGroundHit(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnMovementHit(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        public void ProcessHitStabilityReport(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            Vector3 atCharacterPosition,
            Quaternion atCharacterRotation,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }
    }

    private sealed class DriverPhaseProbeTrack : ActionSequenceTrackDefinition
    {
        private static readonly System.Type[] ClipTypes = { typeof(DriverPhaseProbeClip) };
        private readonly ActionSequenceClipPhase _phase;

        public DriverPhaseProbeTrack(
            ActionSequenceClipPhase phase,
            params ActionSequenceClipDefinition[] clips)
        {
            _phase = phase;
            for (int i = 0; i < clips.Length; i++)
                AddClip(clips[i]);
        }

        public override ActionSequenceClipPhase Phase => _phase;
        public override System.Type[] AllowedClipTypes => ClipTypes;
    }

    private sealed class DriverPhaseProbeClip : ActionSequenceClipDefinition
    {
        private readonly ActionSequenceClipPhase _phase;
        private readonly List<string> _events;
        private readonly string _eventName;
        private readonly KinematicCharacterMotor _motor;
        private readonly Collider _expectedCollider;

        public DriverPhaseProbeClip(
            ActionSequenceClipPhase phase,
            List<string> events,
            string eventName,
            KinematicCharacterMotor motor = null,
            Collider expectedCollider = null)
        {
            _phase = phase;
            _events = events;
            _eventName = eventName;
            _motor = motor;
            _expectedCollider = expectedCollider;
            startFrame = 0;
            endFrame = 1;
        }

        public override ActionSequenceClipPhase Phase => _phase;

        public override ActionSequenceClipRuntime CreateRuntime()
        {
            return new Runtime(_events, _eventName, _motor, _expectedCollider);
        }

        private sealed class Runtime : ActionSequenceClipRuntime
        {
            private readonly List<string> _events;
            private readonly string _eventName;
            private readonly KinematicCharacterMotor _motor;
            private readonly Collider _expectedCollider;
            private readonly Collider[] _overlapResults = new Collider[8];

            public Runtime(
                List<string> events,
                string eventName,
                KinematicCharacterMotor motor,
                Collider expectedCollider)
            {
                _events = events;
                _eventName = eventName;
                _motor = motor;
                _expectedCollider = expectedCollider;
            }

            public override void OnTick(ActionSequenceContext context)
            {
                if (_motor == null || _expectedCollider == null)
                {
                    _events.Add(_eventName);
                    return;
                }

                int count = Physics.OverlapSphereNonAlloc(
                    _motor.TransientPosition,
                    0.1f,
                    _overlapResults,
                    ~0,
                    QueryTriggerInteraction.Collide);
                bool foundExpectedCollider = false;
                for (int i = 0; i < count; i++)
                {
                    if (_overlapResults[i] == _expectedCollider)
                    {
                        foundExpectedCollider = true;
                        break;
                    }
                }

                _events.Add(foundExpectedCollider ? "post-synced" : "post-unsynced");
            }

            public override void OnExit(ActionSequenceContext context, bool completed)
            {
                _events.Add("exit");
            }
        }
    }
}

[DefaultExecutionOrder(0)]
public sealed class CombatSimulationFixedObservationProbe : MonoBehaviour
{
    public KinematicCharacterMotor Motor;
    public int Count;
    public Vector3 TransformPosition;
    public Vector3 InitialTickPosition;
    public Vector3 TransientPosition;

    private void FixedUpdate()
    {
        if (Motor == null)
            return;

        Count++;
        TransformPosition = Motor.Transform.position;
        InitialTickPosition = Motor.InitialTickPosition;
        TransientPosition = Motor.TransientPosition;
    }
}
