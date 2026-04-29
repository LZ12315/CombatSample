using System;
using UnityEngine;

/// <summary>
/// VelocityConfig —— ActionVelocityClip 的配置数据。
/// 
/// 语义定位：
///   VelocityClip 是"持续型"位移控制。与 ImpulseClip（瞬时冲量 + 自然衰减）不同，
///   Velocity 在 Clip 期间覆盖自己声明控制的轴，Clip 结束时释放 owner。
///   典型用途：滑行、浮空移动、吹飞减速（曲线驱动）、技能位移、特殊状态移动。
/// 
/// 与 ImpulseConfig 的关系：
///   - 方向部分共用 MotionDirectionMode（避免两套枚举漂移）
///   - Speed 字段语义是"当前帧速度（m/s）"，而非 Impulse 的"初始冲量"
///   - 用曲线 horizontalCurve / verticalCurve 做"按 Clip 进度缩放速度"，天然替代 Impulse 的衰减曲线
/// 
/// 使用规则：
///   - 勾选 useHorizontalVelocity 时，Clip 期间水平速度由 horizontalSpeed/Curve 覆盖。
///   - 勾选 useVerticalVelocity 时，Clip 期间垂直速度由 verticalSpeed/Curve 覆盖。
///   - 旧资源若已有非 0 速度，也会被 Behavior 视为启用对应轴，以避免迁移时动作完全失效。
/// </summary>
[Serializable]
public class VelocityConfig
{
    // ── 方向 ──

    [Tooltip("速度方向来源（与 ImpulseConfig 共用枚举）")]
    public MotionDirectionMode directionMode = MotionDirectionMode.ActorForward;

    [Tooltip("Fixed 模式下的本地方向（相对角色朝向，如 (0,0,1)=正前，(1,0,0)=正右）")]
    public Vector3 fixedLocalDirection = Vector3.forward;

    // ── 速度大小 ──

    [Tooltip("是否接管水平速度轴。开启后，即使速度为 0 也会覆盖水平程序速度。旧资源中 horizontalSpeed 非 0 时也会自动接管。")]
    public bool useHorizontalVelocity = false;

    [Tooltip("水平速度（m/s），沿解析出的方向覆盖水平程序速度。")]
    public float horizontalSpeed = 0f;

    [Tooltip("是否接管垂直速度轴。开启后，即使速度为 0 也会覆盖垂直程序速度。旧资源中 verticalSpeed 非 0 时也会自动接管。")]
    public bool useVerticalVelocity = false;

    [Tooltip("垂直速度（m/s，正值向上）。开启垂直轴后覆盖最终垂直程序速度。")]
    public float verticalSpeed = 0f;

    // ── 曲线缩放（可选，恒定 1 时等价于不启用） ──

    [Tooltip("水平速度随 Clip 进度的缩放（X: 0~1 归一化时间，Y: 速度乘子）。默认恒定 1。用于击飞减速、冲刺加速等手感塑形。")]
    public AnimationCurve horizontalCurve = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("垂直速度随 Clip 进度的缩放（X: 0~1 归一化时间，Y: 速度乘子）。默认恒定 1。")]
    public AnimationCurve verticalCurve = AnimationCurve.Constant(0f, 1f, 1f);

    // ── 调试 ──

    [Tooltip("打印调试信息到控制台")]
    public bool debugLog = false;
}
