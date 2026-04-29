using System;
using UnityEngine;

/// <summary>
/// Impulse configuration — defines horizontal/vertical initial speed.
/// Attached to ActionImpulseClip on a Timeline, read by ActionImpulseBehavior at runtime.
/// 
/// 语义说明：Impulse 模式一次性注入初始速度（m/s），水平分量由 ActorMovement 的 _horizontalDrag
/// 自然衰减，垂直分量由重力自然衰减。字段名沿用 horizontalForce/verticalForce 是历史原因
/// （重构前按"施力"设计，现已改为"初速度"语义），不改名以避免已有 Timeline 资源里的数值失效。
/// 持续重力/浮空由 ActionMotionConfig 或 VelocityClip 表达。
/// </summary>
[Serializable]
public class ImpulseConfig
{
    [Tooltip("Direction source mode for this impulse")]
    public MotionDirectionMode directionMode = MotionDirectionMode.FromContext;

    [Tooltip("Local direction in Fixed mode (relative to actor facing, e.g. (0,0,1)=forward, (0,0,-1)=backward)")]
    public Vector3 fixedLocalDirection = Vector3.forward;

    [Tooltip("Horizontal initial speed (m/s) along the resolved direction. 一次性注入，由 Movement 的 drag 衰减")]
    public float horizontalForce = 5f;

    [Tooltip("Vertical initial speed (m/s), positive = up. 一次性注入，由重力衰减")]
    public float verticalForce = 0f;

    [Tooltip("Print debug info to console")]
    public bool debugLog = false;
}
