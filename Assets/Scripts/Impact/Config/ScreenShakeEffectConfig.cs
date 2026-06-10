using System;
using UnityEngine;

[Serializable]
[AddTypeMenu("Screen Shake", 2)]
public class ScreenShakeEffectConfig : ImpactEffectConfig
{
    [Tooltip("Impulse strength for Cinemachine.")]
    [Range(0f, 2f)]
    public float intensity = 0.3f;
}
