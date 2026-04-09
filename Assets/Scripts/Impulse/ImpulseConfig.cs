using System;
using UnityEngine;

/// <summary>
/// Direction source mode for impulse.
/// </summary>
public enum ImpulseDirectionMode
{
    /// <summary>Use EventContext.Direction (attacker→victim snapshot at hit moment). Most common knockback mode.</summary>
    FromContext,
    /// <summary>Recalculate Instigator→Self direction every frame. For tracking a moving attacker.</summary>
    FromInstigator,
    /// <summary>Use self transform.forward. For dash / active-skill forward movement.</summary>
    ActorForward,
    /// <summary>Use self -transform.forward. For backward dodge.</summary>
    ActorBackward,
    /// <summary>Use a fixed local direction configured on the clip. For cinematic / fixed-angle launch.</summary>
    Fixed,
}

/// <summary>
/// Impulse configuration — defines horizontal/vertical force, decay curve, gravity scale, etc.
/// Attached to ActionImpulseClip on a Timeline, read by ActionImpulseBehavior at runtime.
/// </summary>
[Serializable]
public class ImpulseConfig
{
    [Tooltip("Direction source mode for this impulse")]
    public ImpulseDirectionMode directionMode = ImpulseDirectionMode.FromContext;

    [Tooltip("Local direction in Fixed mode (relative to actor facing, e.g. (0,0,1)=forward, (0,0,-1)=backward)")]
    public Vector3 fixedLocalDirection = Vector3.forward;

    [Tooltip("Horizontal initial speed (m/s) along the resolved direction")]
    public float horizontalForce = 5f;

    [Tooltip("Horizontal force decay over clip time (X: 0~1 normalized time, Y: force multiplier)")]
    public AnimationCurve horizontalDecay = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Vertical initial speed (m/s), positive = up. Injected once at clip start, then decays by gravity")]
    public float verticalForce = 0f;

    [Tooltip("Gravity scale during this clip (0 = float, 1 = normal, 2 = fast fall)")]
    public float gravityScale = 1f;

    [Tooltip("Lock actor facing during impulse")]
    public bool lockFacing = true;

    [Tooltip("Print debug info to console")]
    public bool debugLog = false;
}
