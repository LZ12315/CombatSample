using System;
using UnityEngine;

[Serializable]
public abstract class ImpactEffectConfig
{
    [Tooltip("关闭后保留此效果块，但本次命中不执行。")]
    public bool enabled = true;
}

[Serializable]
[AddTypeMenu("Impact/Hit Stop", 0)]
public class HitStopEffectConfig : ImpactEffectConfig
{
    [Tooltip("命中停顿时长（秒）。")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.08f;

    [Tooltip("命中停顿期间攻击者 Timeline 播放速度。0 = 定格。")]
    [Range(0f, 1f)]
    public float timeScale = 0.05f;
}

[Serializable]
[AddTypeMenu("Impact/Hit Stick", 1)]
public class HitStickEffectConfig : ImpactEffectConfig
{
    [Tooltip("动作黏滞期间攻击者 Timeline 播放速度。")]
    [Range(0.05f, 1f)]
    public float speedScale = 0.3f;

    [Tooltip("动作黏滞持续时间（秒）。")]
    [Range(0.01f, 0.5f)]
    public float duration = 0.15f;
}

[Serializable]
[AddTypeMenu("Impact/Hit Confirm VFX", 2)]
public class HitConfirmVfxEffectConfig : ImpactEffectConfig
{
    [Tooltip("命中确认特效预制体；留空则不生成。")]
    public GameObject prefab;

    [Tooltip("特效主朝向：面向出手者，或朝上。")]
    public VFXOrientationMode orientation = VFXOrientationMode.FaceAttacker;

    [Tooltip("绕主朝向轴随机或预设旋转。")]
    public VFXRollMode rollMode = VFXRollMode.Random;

    [Tooltip("RollMode=Preset 时使用的固定角度（度）。")]
    public float rollPresetDegrees;

    [Tooltip("RollMode=Random 时的随机角范围上界（度）。")]
    [Range(0f, 360f)]
    public float rollRandomRange = 360f;

    [Tooltip("特效整体缩放。")]
    [Range(0.1f, 5f)]
    public float scale = 1f;

    [Tooltip("特效生存时长（秒）。")]
    [Range(0.1f, 5f)]
    public float lifetime = 1f;

    [Tooltip("粒子仿真速度倍率。")]
    [Range(0.1f, 10f)]
    public float simulationSpeed = 1f;
}

[Serializable]
[AddTypeMenu("Impact/Screen Shake", 3)]
public class ScreenShakeEffectConfig : ImpactEffectConfig
{
    [Tooltip("传给 Cinemachine Impulse 的强度。")]
    [Range(0f, 2f)]
    public float intensity = 0.3f;
}

[Serializable]
[AddTypeMenu("Impact/Target Feedback", 4)]
public class TargetFeedbackEffectConfig : ImpactEffectConfig
{
}
