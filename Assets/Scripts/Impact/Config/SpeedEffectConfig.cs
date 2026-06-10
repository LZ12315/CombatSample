using System;
using UnityEngine;

[Serializable]
[AddTypeMenu("Speed Effect", 2)]
public class SpeedEffectConfig : ImpactEffectConfig
{
    [Header("Timing")]
    [Tooltip("持续时间（秒）。HitStop 通常 0.08，HitStick 通常 0.15")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.08f;

    [Tooltip("时间缩放。0=完全冻结，0.05=几乎暂停（HitStop），0.3=慢动作（HitStick），1=正常")]
    [Range(0f, 1f)]
    public float speedScale = 0.05f;

    [Header("Target")]
    [Tooltip("是否同时冻结受击者。true=双方冻结（推荐用于HitStop），false=仅攻击者")]
    public bool affectBothParties = false;
}
