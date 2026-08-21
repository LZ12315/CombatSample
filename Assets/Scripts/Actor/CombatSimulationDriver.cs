using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// Owns the explicit fixed-step boundary around KCC and actor-on-actor resolution.
///
/// Fixed simulation contract:
/// KCC interpolation pre-step -> Play Action Frames -> KCC simulation -> actor
/// overlap resolution -> physics transform sync -> Detect Hits -> Resolve Hits -> Finish Action Frames
/// -> matching KCC interpolation post-step.
///
/// This is intentionally not a general-purpose callback or phase scheduler.
/// </summary>
[DefaultExecutionOrder(-101)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ActorCollisionResolver))]
public sealed class CombatSimulationDriver : MonoBehaviour
{
    private static CombatSimulationDriver _owner;
    private static readonly List<ActorSimulationRuntime> RegisteredActors =
        new List<ActorSimulationRuntime>(32);

    private ActorCollisionResolver _collisionResolver;
    private readonly List<ActorSimulationRuntime> _tickActors =
        new List<ActorSimulationRuntime>(32);
    private bool _ownsSimulation;
    private bool _simulationFaulted;
    private bool _hasPreviousAutoSimulation;
    private bool _previousAutoSimulation;
    private bool _reportedAutoSimulationOverride;
    private readonly CombatHitBuffer _hitBuffer = new CombatHitBuffer();

    public bool IsSimulationOwner => _ownsSimulation;
    public bool IsSimulationFaulted => _simulationFaulted;

    internal static void RegisterActor(ActorSimulationRuntime runtime)
    {
        if (runtime != null && !RegisteredActors.Contains(runtime))
            RegisteredActors.Add(runtime);
    }

    internal static void UnregisterActor(ActorSimulationRuntime runtime)
    {
        if (runtime != null)
            RegisteredActors.Remove(runtime);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _owner = null;
        RegisteredActors.Clear();
    }

    private void Awake()
    {
        CacheDependencies();

        if (isActiveAndEnabled)
            AcquireSimulationOwnership();
    }

    private void OnEnable()
    {
        CacheDependencies();
        AcquireSimulationOwnership();
    }

    private void OnDisable()
    {
        ReleaseSimulationOwnership();
    }

    private void OnDestroy()
    {
        ReleaseSimulationOwnership();
    }

    private void FixedUpdate()
    {
        if (!_ownsSimulation)
            return;

        EnsureAutoSimulationDisabled();
        if (_simulationFaulted)
            return;

        SimulateFixedStep(Time.fixedDeltaTime);
    }

    /// <summary>
    /// Runs exactly one fixed world-motion step. FixedUpdate is intentionally the
    /// only entry point so gameplay code cannot accidentally simulate KCC twice.
    /// </summary>
    private void SimulateFixedStep(float deltaTime)
    {
        if (!_ownsSimulation)
            throw new InvalidOperationException("Only the active CombatSimulationDriver can simulate KCC.");

        if (_collisionResolver == null)
            throw new InvalidOperationException("CombatSimulationDriver requires ActorCollisionResolver on the same GameObject.");

        if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Simulation delta time must be finite and positive.");
        if (!CombatSimulationTiming.IsGameplayFixedDeltaTime(deltaTime))
            throw new InvalidOperationException($"CombatSimulationDriver requires Time.fixedDeltaTime to be {CombatSimulationTiming.FixedDeltaTime:R} ({CombatSimulationTiming.FrameRate} Hz), but it was {deltaTime:R}.");

        KCCSettings settings = KinematicCharacterSystem.Settings;
        if (settings == null)
            throw new InvalidOperationException("KinematicCharacterSystem settings are unavailable.");

        EnsureAutoSimulationDisabled();
        CaptureActorSnapshot();

        bool interpolationPrepared = false;
        bool interpolateThisStep = settings.Interpolate;
        Exception simulationException = null;

        try
        {
            _hitBuffer.Begin();

            if (interpolateThisStep)
            {
                KinematicCharacterSystem.PreSimulationInterpolationUpdate(deltaTime);
                interpolationPrepared = true;
            }

            for (int i = 0; i < _tickActors.Count; i++)
                _tickActors[i].PlayActionFrame(deltaTime);

            KinematicCharacterSystem.Simulate(
                deltaTime,
                KinematicCharacterSystem.CharacterMotors,
                KinematicCharacterSystem.PhysicsMovers);

            _collisionResolver.ResolveFixedStep();
            Physics.SyncTransforms();

            for (int i = 0; i < _tickActors.Count; i++)
                _tickActors[i].DetectHits(_hitBuffer);

            _hitBuffer.Resolve();

            for (int i = 0; i < _tickActors.Count; i++)
                _tickActors[i].FinishActionFrame();
        }
        catch (Exception exception)
        {
            simulationException = exception;
            AbortActorTicks();
        }
        finally
        {
            if (interpolationPrepared)
            {
                try
                {
                    KinematicCharacterSystem.PostSimulationInterpolationUpdate(deltaTime);
                }
                catch (Exception exception)
                {
                    if (simulationException == null)
                    {
                        simulationException = exception;
                        AbortActorTicks();
                    }
                    else
                    {
                        Debug.LogException(exception, this);
                    }
                }
            }

            _tickActors.Clear();
            _hitBuffer.Clear();
        }

        if (simulationException != null)
        {
            _simulationFaulted = true;
            Debug.LogException(simulationException, this);
        }
    }

    private void CaptureActorSnapshot()
    {
        _tickActors.Clear();
        for (int i = RegisteredActors.Count - 1; i >= 0; i--)
        {
            ActorSimulationRuntime runtime = RegisteredActors[i];
            if (runtime == null || runtime.StableId == 0)
            {
                RegisteredActors.RemoveAt(i);
                continue;
            }

            if (runtime.IsActive)
                _tickActors.Add(runtime);
        }

        _tickActors.Sort((a, b) => a.StableId.CompareTo(b.StableId));
    }

    private void AbortActorTicks()
    {
        for (int i = 0; i < _tickActors.Count; i++)
        {
            try
            {
                _tickActors[i].CancelAction();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void EnsureAutoSimulationDisabled()
    {
        KCCSettings settings = KinematicCharacterSystem.Settings;
        if (settings == null || !settings.AutoSimulation)
            return;

        settings.AutoSimulation = false;
        if (_reportedAutoSimulationOverride)
            return;

        Debug.LogError(
            "[CombatSimulationDriver] KCC AutoSimulation was re-enabled while the Driver owned simulation. " +
            "It has been disabled again to prevent a double tick.",
            this);
        _reportedAutoSimulationOverride = true;
    }

    private void CacheDependencies()
    {
        if (_collisionResolver == null)
            _collisionResolver = GetComponent<ActorCollisionResolver>();

        KinematicCharacterSystem.EnsureCreation();
    }

    private void AcquireSimulationOwnership()
    {
        if (!isActiveAndEnabled || _ownsSimulation)
            return;

        if (_collisionResolver == null)
        {
            Debug.LogError(
                "[CombatSimulationDriver] ActorCollisionResolver must be on the same GameObject.",
                this);
            enabled = false;
            return;
        }

        if (_owner != null && _owner != this)
        {
            Debug.LogError(
                $"[CombatSimulationDriver] Only one active Driver is allowed. Existing owner: '{_owner.name}'.",
                this);
            enabled = false;
            return;
        }

        KCCSettings settings = KinematicCharacterSystem.Settings;
        if (settings == null)
        {
            Debug.LogError("[CombatSimulationDriver] KCC settings are unavailable.", this);
            enabled = false;
            return;
        }

        _owner = this;
        _previousAutoSimulation = settings.AutoSimulation;
        _hasPreviousAutoSimulation = true;
        settings.AutoSimulation = false;
        _reportedAutoSimulationOverride = false;
        _simulationFaulted = false;
        _ownsSimulation = true;
    }

    private void ReleaseSimulationOwnership()
    {
        if (!_ownsSimulation)
            return;

        if (_owner == this)
            _owner = null;

        if (_hasPreviousAutoSimulation && KinematicCharacterSystem.Settings != null)
            KinematicCharacterSystem.Settings.AutoSimulation = _previousAutoSimulation;

        _hasPreviousAutoSimulation = false;
        _simulationFaulted = false;
        _ownsSimulation = false;
    }
}
