using UnityEngine;

/// <summary>
/// 事件触发 Action 时携带的上下文数据。
/// 类似 UE 的 FGameplayEventData —— 通用信封，Action 自己决定取什么。
/// 轮询触发的 Action 不需要此结构，直接从 Actor 身上读取信息即可。
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

    /// <summary>当 Instigator 不为 null 或 Direction 有有效值时返回 true</summary>
    public bool IsValid => Instigator != null || Direction.sqrMagnitude > 0.001f;
}
