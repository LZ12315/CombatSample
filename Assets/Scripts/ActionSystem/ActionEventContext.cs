using UnityEngine;

/// <summary>
/// Action 开始时携带的上下文快照。
/// 事件触发与“启动即锁定”的轮询动作都可以复用这份数据；
/// Action / Timeline 自己决定读取哪些字段。
/// </summary>
public struct ActionEventContext
{
    /// <summary>事件发起者（如攻击者）</summary>
    public GameObject Instigator;

    /// <summary>事件目标（如受击者，通常就是自己）</summary>
    public GameObject Target;

    /// <summary>世界空间中的关键位置（如命中点）</summary>
    public Vector3 HitPoint;

    /// <summary>通用方向（如击退/击飞方向）</summary>
    public Vector3 Direction;

    /// <summary>通用数值（如力度、伤害等，Action 自己解读）</summary>
    public float Magnitude;

    /// <summary>当包含有效引用、方向或数值时返回 true。</summary>
    public bool IsValid =>
        Instigator != null ||
        Target != null ||
        Direction.sqrMagnitude > 0.001f ||
        Mathf.Abs(Magnitude) > 0.001f;
}
