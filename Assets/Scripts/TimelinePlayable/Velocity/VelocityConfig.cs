using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Velocity clips own selected movement axes while the clip is active.
/// Horizontal direction stays planar; vertical velocity is configured directly.
/// </summary>
[Serializable]
public class VelocityConfig
{
    [Tooltip("Horizontal direction source for this velocity.")]
    public MotionDirectionMode directionMode = MotionDirectionMode.LocalHorizontal;

    [FormerlySerializedAs("fixedLocalDirection")]
    [Tooltip("Actor-local horizontal direction. X = right, Z = forward. Y is ignored.")]
    public Vector3 localHorizontalDirection = Vector3.forward;

    [Tooltip("Take ownership of horizontal velocity. When enabled, even zero speed overrides locomotion.")]
    public bool useHorizontalVelocity = false;

    [Tooltip("Horizontal velocity (m/s) along the resolved direction.")]
    public float horizontalSpeed = 0f;

    [Tooltip("Take ownership of vertical velocity. When enabled, even zero speed overrides gravity and vertical impulse.")]
    public bool useVerticalVelocity = false;

    [Tooltip("Vertical velocity (m/s), positive = up.")]
    public float verticalSpeed = 0f;

    [Tooltip("Horizontal speed multiplier over normalized clip time.")]
    public AnimationCurve horizontalCurve = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("Vertical speed multiplier over normalized clip time.")]
    public AnimationCurve verticalCurve = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("Print debug info to console.")]
    public bool debugLog = false;
}
