using System;
using UnityEngine;

[Serializable]
public enum VerticalVelocityMode
{
    AdditiveExternal = 0, // 默认：写入外部垂直速度，与重力/冲量叠加
    ClampRange = 1,       // 钳制最终垂直速度范围（常用于空中攻击短暂停滞）
    OverrideVertical = 2, // 直接覆盖最终垂直速度（强演出/定身）
}

[Serializable]
public enum VerticalReleaseMode
{
    Transparent = 0,  // Clip 结束后通道状态原样暴露（默认）
    ResetVertical = 1, // Clip 结束时重置垂直通道（冲量+重力归零）
}

/// <summary>
/// VelocityConfig —— ActionVelocityClip 的配置数据。
/// 
/// 语义定位：
///   VelocityClip 是"持续型"位移通道。与 ImpulseClip（瞬时冲量 + 自然衰减）不同，
///   Velocity 在 Clip 期间"每帧持续写入"外部速度，Clip 结束时清零。
///   典型用途：滑行、浮空移动、吹飞减速（曲线驱动）、技能位移、特殊状态移动。
/// 
/// 与 ImpulseConfig 的关系：
///   - 方向部分共用 MotionDirectionMode（避免两套枚举漂移）
///   - Speed 字段语义是"当前帧速度（m/s）"，而非 Impulse 的"初始冲量"
///   - 用曲线 horizontalCurve / verticalCurve 做"按 Clip 进度缩放速度"，天然替代 Impulse 的衰减曲线
/// 
/// 重力规则（重要）：
///   VelocityClip 期间，默认会把 Movement 的 gravityScale 覆盖为 Config 里的 gravityScale
///   （默认 0 = 完全浮空）。这是为了让"持续垂直速度"的语义清晰——Clip 期间完全接管垂直。
///   如果希望 Clip 期间保留重力（verticalSpeed 只是在重力之上叠加），把 gravityScale 设为 1。
///   Clip 结束时 Behavior 会恢复到 1.0（默认重力）。
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

    [Tooltip("水平速度（m/s），沿解析出的方向写入外部水平通道。0 表示不启用水平持续速度。")]
    public float horizontalSpeed = 0f;

    [Tooltip("垂直速度（m/s，正值向上）。写入外部垂直通道。0 表示不启用垂直持续速度。")]
    public float verticalSpeed = 0f;

    [Tooltip("垂直速度写入模式：AdditiveExternal=叠加；ClampRange=钳制最终垂直范围；OverrideVertical=直接覆盖最终垂直速度。")]
    public VerticalVelocityMode verticalMode = VerticalVelocityMode.AdditiveExternal;

    [Tooltip("ClampRange 模式下允许的最低垂直速度（m/s）。负值=缓降。")]
    public float clampMin = -0.5f;

    [Tooltip("ClampRange 模式下允许的最高垂直速度（m/s）。0=禁止上升。")]
    public float clampMax = 0f;

    // ── 曲线缩放（可选，恒定 1 时等价于不启用） ──

    [Tooltip("水平速度随 Clip 进度的缩放（X: 0~1 归一化时间，Y: 速度乘子）。默认恒定 1。用于击飞减速、冲刺加速等手感塑形。")]
    public AnimationCurve horizontalCurve = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("垂直速度随 Clip 进度的缩放（X: 0~1 归一化时间，Y: 速度乘子）。默认恒定 1。")]
    public AnimationCurve verticalCurve = AnimationCurve.Constant(0f, 1f, 1f);

    // ── 重力 ──

    [Tooltip("Clip 期间使用的重力缩放。默认 0 = 完全浮空（由 Clip 接管垂直）。设为 1 则保留正常重力叠加在 verticalSpeed 之上。")]
    public float gravityScale = 0f;

    [Tooltip("Clip 结束时的垂直通道处理。Transparent=原样暴露；ResetVertical=归零后由重力接管。")]
    public VerticalReleaseMode releaseMode = VerticalReleaseMode.Transparent;

    // ── 调试 ──

    [Tooltip("打印调试信息到控制台")]
    public bool debugLog = false;
}
