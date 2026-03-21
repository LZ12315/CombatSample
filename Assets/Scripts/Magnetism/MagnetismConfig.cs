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
    [Tooltip("玩家根与敌人 Transform 的水平距离超过此值则本 Clip 不生效（不旋转，0=无限制）")]
    public float maxDistance = 0f;

    [Header("Rotation")]
    public bool rotateToTarget = true;
    public MagnetismRotationMode rotationMode = MagnetismRotationMode.AngularSpeed;
    [Tooltip("rotationMode=AngularSpeed 时有效；0=不做旋转覆盖")]
    public float rotationAngularSpeed = 360f;
    public MagnetismRotationAxis rotationAxis = MagnetismRotationAxis.YawOnly;

    [Header("Debug")]
    public bool debugLog = false;
}
