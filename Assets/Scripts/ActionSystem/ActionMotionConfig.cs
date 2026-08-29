using System;
using UnityEngine;

/// <summary>
/// Legacy Timeline whole-action motion settings. Sequence actions ignore this
/// data at runtime; E3-G migration bakes equivalent contributions into clips.
/// </summary>
[Serializable]
public struct ActionMotionConfig
{
    [Tooltip("Legacy Timeline root motion strategy. Sequence actions ignore this field.")]
    public RootMotionApplyMode rootMotionMode;

    [Tooltip("Legacy Timeline locomotion suppression. Sequence actions use MotionPolicy clips instead.")]
    public bool suppressLocomotion;

    [Tooltip("Legacy Timeline start-facing behavior. Sequence actions use FacingSnap clips instead.")]
    public ActionFacingOnStart facingOnStart;

    [Tooltip("Legacy Timeline gravity scale. Sequence actions use MotionPolicy clips instead. Negative means no override.")]
    public float gravityScale;

    [Range(0f, 1f), Tooltip("Legacy Timeline horizontal momentum inheritance.")]
    public float horizontalMomentumInheritance;

    [Range(0f, 1f), Tooltip("Legacy Timeline vertical momentum inheritance.")]
    public float verticalMomentumInheritance;

    public static ActionMotionConfig Default => new()
    {
        rootMotionMode = RootMotionApplyMode.Managed,
        suppressLocomotion = true,
        facingOnStart = ActionFacingOnStart.SnapToInputOrTarget,
        gravityScale = -1f,
        horizontalMomentumInheritance = 0f,
        verticalMomentumInheritance = 0f,
    };
}
