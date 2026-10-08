using System;
using UnityEngine;

/// <summary>
/// 通用的速度阈值条件检查，用于 ActionAsset 的 EntryCondition。
/// 数据源 = ActorMotor.CurrentVelocity（KCC 解算后的真实位移速度，
/// 最贴近"此刻真实速度"）。
///
/// 用法：选轴 → 选比较方式 → 填阈值。
///
/// 典型配置：
///   - Apex（顶点附近）：         axis=VerticalAbs,         comparison=LessOrEqual,    threshold=1
///   - Fall（正在下落）：         axis=VerticalSigned,      comparison=LessOrEqual,    threshold=0
///   - Rise（正在上升）：         axis=VerticalSigned,      comparison=GreaterOrEqual, threshold=0
///   - Sprint（水平速度≥6）：     axis=HorizontalMagnitude, comparison=GreaterOrEqual, threshold=6
///   - 高速下砸：                 axis=VerticalSigned,      comparison=LessOrEqual,    threshold=-15
/// </summary>
[Serializable]
public class VelocityCondition : ActionCondition
{
    [Tooltip("取哪个分量参与比较：\n" +
             "VerticalSigned      = vy（带符号，正上负下）\n" +
             "VerticalAbs         = |vy|（垂直速度大小，用于 Apex 顶点判定）\n" +
             "HorizontalMagnitude = √(vx²+vz²)（水平速度大小）")]
    public VelocityAxis axis = VelocityAxis.VerticalSigned;

    [Tooltip("比较方式：\n" +
             "GreaterOrEqual = value >= threshold\n" +
             "LessOrEqual    = value <= threshold\n" +
             "InsideRange    = min <= value <= max\n" +
             "OutsideRange   = value < min 或 value > max")]
    public Comparison comparison = Comparison.LessOrEqual;

    [Tooltip("单值比较阈值（米/秒）。GreaterOrEqual / LessOrEqual 模式下生效。")]
    public float threshold = 0f;

    [Tooltip("下界（米/秒）。InsideRange / OutsideRange 模式下生效。")]
    public float min = 0f;

    [Tooltip("上界（米/秒）。InsideRange / OutsideRange 模式下生效。")]
    public float max = 0f;

    protected override bool OnCheck(Actor actor)
    {
        if (actor == null || actor.actorMotor == null)
            return false;

        Vector3 v = actor.actorMotor.CurrentVelocity;

        float value;
        switch (axis)
        {
            case VelocityAxis.VerticalSigned:      value = v.y; break;
            case VelocityAxis.VerticalAbs:         value = Mathf.Abs(v.y); break;
            case VelocityAxis.HorizontalMagnitude: value = new Vector2(v.x, v.z).magnitude; break;
            default:                               value = 0f; break;
        }

        switch (comparison)
        {
            case Comparison.GreaterOrEqual: return value >= threshold;
            case Comparison.LessOrEqual:    return value <= threshold;
            case Comparison.InsideRange:    return value >= min && value <= max;
            case Comparison.OutsideRange:   return value < min || value > max;
            default:                        return false;
        }
    }

    public enum VelocityAxis
    {
        VerticalSigned      = 0, // vy（带符号）
        VerticalAbs         = 1, // |vy|
        HorizontalMagnitude = 2, // √(vx²+vz²)
    }

    public enum Comparison
    {
        GreaterOrEqual = 0, // value >= threshold
        LessOrEqual    = 1, // value <= threshold
        InsideRange    = 2, // min <= value <= max
        OutsideRange   = 3, // value < min || value > max
    }
}
