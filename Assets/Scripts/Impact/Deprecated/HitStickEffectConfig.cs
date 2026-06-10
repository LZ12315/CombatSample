using System;
using UnityEngine;

[Serializable]
[AddTypeMenu("Hit Stick", 1)]
[Obsolete("请使用 SpeedEffectConfig，通过 affectBothParties 控制双方/单方冻结")]
public class HitStickEffectConfig : ImpactEffectConfig
{
    [Tooltip("Attacker Timeline speed during stick.")]
    [Range(0.05f, 1f)]
    public float speedScale = 0.3f;

    [Tooltip("Stick time (seconds).")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.15f;
}
