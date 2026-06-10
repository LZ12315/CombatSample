using System;
using UnityEngine;

[Serializable]
[AddTypeMenu("Hit Stop", 0)]
[Obsolete("请使用 SpeedEffectConfig，通过 affectBothParties 控制双方/单方冻结")]
public class HitStopEffectConfig : ImpactEffectConfig
{
    [Tooltip("Hit stop time (seconds).")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.08f;

    [Tooltip("Attacker Timeline speed during hit stop. 0 = freeze.")]
    [Range(0f, 1f)]
    public float timeScale = 0.05f;
}
