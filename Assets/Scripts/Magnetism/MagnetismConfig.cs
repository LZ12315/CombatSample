using System;
using UnityEngine;

namespace CombatSample.Magnetism
{
    public enum MagnetismApproachMode
    {
        /// <summary>首帧将根节点一次修正到理想表面间隙（水平分量）。</summary>
        InstantMove,
        /// <summary>按 pull/push 速度持续把间隙维持在理想值±死区内。</summary>
        SpeedMove,
    }

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
    /// Magnetism V2：根节点采样点 ↔ 敌人 Capsule 表面的间隙带（双向修正）。
    /// 已移除：attachDistance、武器锚点、武器表面单向 clearance（见 plans/攻击吸附_根节点到表面_方案与迁移.md）。
    /// </summary>
    [Serializable]
    public class MagnetismConfig
    {
        [Header("Distance")]
        [Tooltip("玩家根与敌人 Transform 的水平距离超过此值则整段不吸附（0=无限制）")]
        public float maxDistance = 0f;

        [Header("Surface gap (actor root ↔ enemy capsule shell)")]
        [Tooltip("根节点世界坐标到敌人胶囊壳的最短间隙目标值（米），主调参")]
        public float idealSurfaceGap = 0.35f;

        [Tooltip("理想值±死区内不做位移修正")]
        public float gapDeadZone = 0.05f;

        [Header("Approach")]
        public MagnetismApproachMode approachMode = MagnetismApproachMode.SpeedMove;

        [Tooltip("间隙偏大（离表面过远）时沿靠近敌人方向移动根的速度（米/秒）")]
        public float pullSpeed = 8f;

        [Tooltip("间隙偏小（过近或穿入壳内）时沿离开表面法线移动根的速度（米/秒）")]
        public float pushSpeed = 8f;

        [Header("Rotation")]
        public bool rotateToTarget = true;
        public MagnetismRotationMode rotationMode = MagnetismRotationMode.AngularSpeed;
        [Tooltip("rotationMode=AngularSpeed 时有效；0=不做旋转覆盖")]
        public float rotationAngularSpeed = 360f;
        public MagnetismRotationAxis rotationAxis = MagnetismRotationAxis.YawOnly;

        [Header("Debug")]
        public bool debugLog = false;
    }
}
