using System;
using UnityEngine;

namespace CombatSample.Magnetism
{
    public enum MagnetismApproachMode
    {
        InstantMove, // 一开始瞬移到贴近点，然后只做旋转/持续保持（可选）
        SpeedMove,   // 按速度持续逼近贴近点
    }

    public enum MagnetismRotationMode
    {
        None,
        InstantSnap,  // 直接把朝向摆到目标
        AngularSpeed, // 按角速度平滑转向（由 ActorMovement 统一执行）
    }

    public enum MagnetismRotationAxis
    {
        YawOnly, // 只在XZ平面转（战斗常用）
        Full,    // 全轴（不建议，除非你确实需要）
    }

    [Serializable]
    public class MagnetismConfig
    {
        [Header("Distance")]
        [Tooltip("超过此距离不吸附（0=无限制）")]
        public float maxDistance = 0f;

        [Tooltip("到目标点保持的贴身距离（>=0）。当距离<=attachDistance 时认为已贴上")]
        public float attachDistance = 0.5f;

        [Header("Approach")]
        public MagnetismApproachMode approachMode = MagnetismApproachMode.SpeedMove;

        [Tooltip("贴近速度（米/秒）。InstantMove 下仅用于旋转/可选的保持逻辑")]
        public float approachSpeed = 8f;

        [Header("Rotation")]
        public bool rotateToTarget = true;
        public MagnetismRotationMode rotationMode = MagnetismRotationMode.AngularSpeed;
        [Tooltip("旋转角速度（度/秒）。rotationMode=AngularSpeed 时有效；0=不做旋转覆盖")]
        public float rotationAngularSpeed = 360f;
        public MagnetismRotationAxis rotationAxis = MagnetismRotationAxis.YawOnly;

        [Header("Debug")]
        public bool debugLog = false;
    }
}

