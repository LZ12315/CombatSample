using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Impulse clips inject initial velocity once when the clip starts.
/// Horizontal velocity decays through ActorMotor drag; vertical velocity is
/// resolved through the motor's gravity and vertical impulse rules.
/// </summary>
[Serializable]
public class ImpulseConfig
{
    [Tooltip("Horizontal direction source for this impulse.")]
    public MotionDirectionMode directionMode = MotionDirectionMode.FromContext;

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
