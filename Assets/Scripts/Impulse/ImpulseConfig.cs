using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum ImpulseDirectionMode
{
    /// <summary>Use ActionEventContext.Direction captured when the action starts.</summary>
    FromContext = 0,

    /// <summary>Use an actor-local horizontal vector. X = right, Z = forward.</summary>
    LocalHorizontal = 4,

    /// <summary>Use the 3D direction from this actor to its current CombatTarget.</summary>
    ToCombatTarget3D = 5,
}

/// <summary>
/// Impulse clips inject initial velocity once when the clip starts.
/// Horizontal velocity decays through ActorMotor drag; vertical velocity is
/// resolved through the motor's gravity and vertical impulse rules.
/// </summary>
[Serializable]
public class ImpulseConfig
{
    [Tooltip("Direction source for this impulse.")]
    public ImpulseDirectionMode directionMode = ImpulseDirectionMode.FromContext;

    [FormerlySerializedAs("fixedLocalDirection")]
    [Tooltip("Actor-local horizontal direction. X = right, Z = forward. Y is ignored.")]
    public Vector3 localHorizontalDirection = Vector3.forward;

    [Tooltip("Horizontal initial speed (m/s) along the resolved direction.")]
    public float horizontalForce = 5f;

    [Tooltip("Vertical initial speed (m/s), positive = up.")]
    public float verticalForce = 0f;

    [Tooltip("Print debug info to console.")]
    public bool debugLog = false;
}
