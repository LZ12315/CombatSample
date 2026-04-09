using System;
using UnityEngine;

/// <summary>
/// 冲量配置数据 — 定义一段 ImpulseClip 的水平/垂直力度、衰减曲线、重力缩放等参数。
/// 放在 Timeline 的 ActionImpulseClip 上，由 ActionImpulseBehavior 在运行时读取。
/// </summary>
[Serializable]
public class ImpulseConfig
{
    [Header("水平冲量")]
    [Tooltip("水平初速度（米/秒），沿 EventContext.Direction 的水平投影方向")]
    public float horizontalForce = 5f;

    [Tooltip("水平力度随 Clip 时间的衰减曲线（X: 0~1 归一化时间，Y: 力度百分比）")]
    public AnimationCurve horizontalDecay = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("垂直冲量")]
    [Tooltip("垂直初速度（米/秒），正值=向上。仅在 Clip 开始时注入一次，之后由重力自然衰减")]
    public float verticalForce = 0f;

    [Header("重力")]
    [Tooltip("此 Clip 期间的重力缩放（0=浮空, 1=正常, 2=加速下落）")]
    public float gravityScale = 1f;

    [Header("朝向")]
    [Tooltip("冲量期间是否锁定朝向")]
    public bool lockFacing = true;

    [Header("调试")]
    public bool debugLog = false;
}
