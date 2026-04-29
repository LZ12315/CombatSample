using System;
using UnityEngine;
using UnityEngine.Serialization;

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
<<<<<<< HEAD
///
/// 语义：VelocityClip 在 Clip 期间持续写入程序速度（owner）；Clip 结束释放。
/// <see cref="overrideGravity"/> 仅当策划明确需要 Clip 期间替换重力倍率时启用。
=======
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
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
/// </summary>
[Serializable]
public class VelocityConfig : ISerializationCallbackReceiver
{
    private const float Eps = 0.0001f;

    // ── 方向 ──

    [Tooltip("速度方向来源（与 ImpulseConfig 共用枚举）")]
    public MotionDirectionMode directionMode = MotionDirectionMode.ActorForward;

    [Tooltip("Fixed 模式下的本地方向（相对角色朝向，如 (0,0,1)=正前，(1,0,0)=正右）")]
    public Vector3 fixedLocalDirection = Vector3.forward;

    // ── 速度大小 ──

<<<<<<< HEAD
    [Tooltip("若为 true，Clip 权威覆盖水平程序速度（含速度为 0）。旧资源未勾选时：Horizontal Speed 非零仍视为启用水平覆盖。")]
    public bool controlHorizontal;

    [Tooltip("若为 true，Clip 权威覆盖垂直程序速度（含速度为 0）。旧资源未勾选时：依 Vertical Speed / Vertical Mode 推断。")]
    public bool controlVertical;

    [FormerlySerializedAs("useClipGravity")]
    [Tooltip("若为 true，Clip 期间通过 ClipGravity owner 覆盖重力倍率为 gravityScale；结束时回到 Action baseline。")]
    public bool overrideGravity;

    [Tooltip("水平速度（m/s），沿解析出的方向。配合 Control Horizontal 或旧资源推断。")]
    public float horizontalSpeed = 0f;

    [Tooltip("垂直速度（m/s，正值向上）。配合 Control Vertical / Vertical Mode。")]
    public float verticalSpeed = 0f;

    [Tooltip("[Legacy] 垂直速度写入模式：新语义优先使用 Control Vertical + 权威覆盖；ClampRange 仍走附加+Clamp 兼容路径。")]
=======
    [Tooltip("水平速度（m/s），沿解析出的方向写入外部水平通道。0 表示不启用水平持续速度。")]
    public float horizontalSpeed = 0f;

    [Tooltip("垂直速度（m/s，正值向上）。写入外部垂直通道。0 表示不启用垂直持续速度。")]
    public float verticalSpeed = 0f;

    [Tooltip("垂直速度写入模式：AdditiveExternal=叠加；ClampRange=钳制最终垂直范围；OverrideVertical=直接覆盖最终垂直速度。")]
>>>>>>> parent of 50a4ffc (基本完成第一步整理)
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

<<<<<<< HEAD
    [Tooltip("Clip 期间的重力倍率（仅在 Override Gravity 勾选时生效）。默认 1 = 与 Action 一致。")]
    public float gravityScale = 1f;
=======
    [Tooltip("Clip 期间使用的重力缩放。默认 0 = 完全浮空（由 Clip 接管垂直）。设为 1 则保留正常重力叠加在 verticalSpeed 之上。")]
    public float gravityScale = 0f;
>>>>>>> parent of 50a4ffc (基本完成第一步整理)

    [Tooltip("Clip 结束时的垂直通道处理。Transparent=原样暴露；ResetVertical=归零后由重力接管。")]
    public VerticalReleaseMode releaseMode = VerticalReleaseMode.Transparent;

    // ── 调试 ──

    [Tooltip("打印调试信息到控制台")]
    public bool debugLog = false;

    [SerializeField] private bool _velocityLegacyMigrationApplied;

    /// <summary>编辑器菜单：对 Timeline 内嵌的 config 重新套用迁移规则并保存。</summary>
    public void ApplyMigrationRulesFromEditor()
    {
        ApplyLegacyMigrationRules();
        _velocityLegacyMigrationApplied = true;
    }

    /// <summary>
    /// 旧 YAML / 缺字段资源的迁移：避免纯水平 Clip 因旧默认 gravityScale=0 误开 Override；
    /// ClampRange 仍默认需要 Override 重力以维持滞空。
    /// </summary>
    private void ApplyLegacyMigrationRules()
    {
        bool legacyClamp = verticalMode == VerticalVelocityMode.ClampRange;
        bool hasVerticalIntent =
            controlVertical ||
            Mathf.Abs(verticalSpeed) > Eps ||
            verticalMode == VerticalVelocityMode.OverrideVertical ||
            legacyClamp;

        // 纯水平（旧资源常见）：不误伤重力
        if (!hasVerticalIntent && Mathf.Abs(horizontalSpeed) > Eps)
        {
            if (!controlHorizontal)
                controlHorizontal = true;
            overrideGravity = false;
            gravityScale = 1f;
            return;
        }

        if (legacyClamp)
        {
            overrideGravity = true;
            return;
        }

        if (verticalMode == VerticalVelocityMode.OverrideVertical || Mathf.Abs(verticalSpeed) > Eps)
        {
            overrideGravity = Mathf.Abs(gravityScale - 1f) > Eps;
            return;
        }

        // AdditiveExternal，无垂直意图：仅当重力倍率偏离 1 才 Override
        if (Mathf.Abs(gravityScale - 1f) > Eps)
            overrideGravity = true;
        else
            overrideGravity = false;
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        if (_velocityLegacyMigrationApplied)
            return;
        _velocityLegacyMigrationApplied = true;
        ApplyLegacyMigrationRules();
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
    }
}
