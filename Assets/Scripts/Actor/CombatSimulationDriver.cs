using System;
using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// Owns the explicit fixed-step boundary around KCC and actor-on-actor resolution.
///
/// First-stage contract:
/// KCC interpolation pre-step -> KCC simulation -> matching KCC interpolation
/// post-step -> actor overlap resolution.
///
/// Resolver intentionally remains after the interpolation post-step in this
/// compatibility stage, matching the previous -100/-99 FixedUpdate behavior.
/// The later Sequence PostWorld stage will move that boundary deliberately.
///
/// This is intentionally not a general-purpose callback or phase scheduler.
/// </summary>
[DefaultExecutionOrder(-101)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ActorCollisionResolver))]
public sealed class CombatSimulationDriver : MonoBehaviour
{
    private static CombatSimulationDriver _owner;

    private ActorCollisionResolver _collisionResolver;
    private bool _ownsSimulation;
    private bool _hasPreviousAutoSimulation;
    private bool _previousAutoSimulation;
    private bool _reportedAutoSimulationOverride;

    public bool IsSimulationOwner => _ownsSimulation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _owner = null;
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

        SimulateFixedStep(Time.deltaTime);
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

        KCCSettings settings = KinematicCharacterSystem.Settings;
        if (settings == null)
            throw new InvalidOperationException("KinematicCharacterSystem settings are unavailable.");

        // Driver order is -101 and KCC order is -100. Reasserting the invariant here
        // prevents an external setting change from causing a second automatic tick.
        if (settings.AutoSimulation)
        {
            settings.AutoSimulation = false;
            if (!_reportedAutoSimulationOverride)
            {
                Debug.LogError(
                    "[CombatSimulationDriver] KCC AutoSimulation was re-enabled while the Driver owned simulation. " +
                    "It has been disabled again to prevent a double tick.",
                    this);
                _reportedAutoSimulationOverride = true;
            }
        }

        bool interpolationPrepared = false;
        bool interpolateThisStep = settings.Interpolate;

        try
        {
            if (interpolateThisStep)
            {
                KinematicCharacterSystem.PreSimulationInterpolationUpdate(deltaTime);
                interpolationPrepared = true;
            }

            KinematicCharacterSystem.Simulate(
                deltaTime,
                KinematicCharacterSystem.CharacterMotors,
                KinematicCharacterSystem.PhysicsMovers);
        }
        finally
        {
            if (interpolationPrepared)
                KinematicCharacterSystem.PostSimulationInterpolationUpdate(deltaTime);
        }

        // Keep the first-stage Transform visibility identical to the previous
        // KCC(-100) -> resolver(-99) ordering.
        _collisionResolver.ResolveFixedStep();
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
        _ownsSimulation = false;
    }
}
