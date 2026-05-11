using System;
using UnityEngine;

public enum MagnetismRotationMode
{
    None,
    InstantSnap,
    AngularSpeed,
}

public enum MagnetismRotationAxis
{
    YawOnly,
    Full,
}

/// <summary>
/// Timeline Magnetism Clip：片段内朝向战斗目标（+ 水平距离门控）；站位/推挤见 PhysicsInteraction。
/// </summary>
[Serializable]
public class MagnetismConfig
{
    [Header("Gate")]
    [Tooltip("If player–enemy flat distance is over this, clip does nothing. 0 = no limit.")]
    public float maxDistance = 0f;

    [Header("Rotation")]
    public bool rotateToTarget = true;
    public MagnetismRotationMode rotationMode = MagnetismRotationMode.AngularSpeed;
    [Tooltip("Used when rotation mode is AngularSpeed. 0 = no rotate override")]
    public float rotationAngularSpeed = 360f;
    public MagnetismRotationAxis rotationAxis = MagnetismRotationAxis.YawOnly;

    [Header("Debug")]
    public bool debugLog = false;
}
