using System;
using UnityEngine;

/// <summary>
/// Impulse configuration — defines horizontal/vertical initial speed, gravity scale, etc.
/// Attached to ActionImpulseClip on a Timeline, read by ActionImpulseBehavior at runtime.
/// 
/// 语义说明：Impulse 模式一次性注入初始速度（m/s），水平分量由 ActorMovement 的 _horizontalDrag
/// 自然衰减，垂直分量由重力自然衰减。字段名沿用 horizontalForce/verticalForce 是历史原因
/// （重构前按"施力"设计，现已改为"初速度"语义），不改名以避免已有 Timeline 资源里的数值失效。
/// 
/// 重力规则（重要）：
///   ImpulseClip 通常只持续 1~数帧，用 Clip 生命周期去控制"整招期间的重力"并不合理
///   （Clip 结束时重力立刻被恢复，整招大部分时间的重力都失控）。
///   所以 gravityScale 默认 -1 = 不覆盖，整招的重力由 ActionMotionConfig.gravityScale 负责。
///   只有在极少数需要"Clip 级短暂改重力"的场景才填 >= 0 的值。
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

<<<<<<< HEAD
    [Obsolete("优先使用 ActionMotionConfig.gravityScale 或 VelocityClip 的 overrideGravity + gravityScale。")]
=======
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
    [Tooltip("Gravity scale during this clip. -1 = 不覆盖（推荐，整招重力由 ActionMotionConfig 负责）；>=0 时在 Clip 期间强制覆盖（0=浮空，1=正常，2=快速下坠）")]
    public float gravityScale = -1f;

    [Tooltip("Print debug info to console")]
    public bool debugLog = false;
}
