using System;
using UnityEngine;

[Serializable]
public enum HitVfxOcclusionMode
{
    DefaultDepth = 0,
    EnvironmentOnly = 1,
}

[Serializable]
public abstract class ImpactEffectConfig
{
    [Tooltip("Off = skip this effect for this hit only.")]
    public bool enabled = true;
}
