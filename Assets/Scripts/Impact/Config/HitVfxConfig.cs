using System;
using UnityEngine;

/// <summary>VFX 锚点策略：命中接触点 vs 目标碰撞体中心</summary>
public enum VfxAnchorMode
{
    ContactHitPoint,
    HitColliderCenter,
}

/// <summary>VFX 朝向策略：屏幕向 vs 世界向</summary>
public enum VfxOrientationMode
{
    ScreenOriented,
    WorldDirectional,
}

/// <summary>
/// 统一的空间 VFX 配置，取代 ScreenOrientedImpactEffectConfig 与 WorldDirectionalImpactEffectConfig。
/// 通过 orientationMode 选择朝向，通过 anchorMode 选择生成位置。
/// </summary>
[Serializable]
[AddTypeMenu("Hit VFX (Unified)", 6)]
public class HitVfxConfig : ImpactEffectConfig
{
    [Tooltip("VFX prefab")]
    public GameObject prefab;

    [Tooltip("遮挡模式")]
    public HitVfxOcclusionMode occlusionMode = HitVfxOcclusionMode.EnvironmentOnly;

    [Tooltip("整体缩放")]
    [Range(0.1f, 5f)]
    public float scale = 1f;

    [Tooltip("生命周期(秒)")]
    [Range(0.1f, 5f)]
    public float lifetime = 1f;

    [Tooltip("粒子模拟速度倍率")]
    [Range(0.1f, 10f)]
    public float simulationSpeed = 1f;

    [Header("位置")]
    [Tooltip("初始锚点：ContactHitPoint=命中接触点(推荐)，HitColliderCenter=命中碰撞体中心")]
    public VfxAnchorMode anchorMode = VfxAnchorMode.ContactHitPoint;

    [Header("朝向")]
    [Tooltip("VFX朝向：ScreenOriented=屏幕刀光类，WorldDirectional=世界喷溅类")]
    public VfxOrientationMode orientationMode = VfxOrientationMode.ScreenOriented;

    [Header("Screen Oriented 参数")]
    [Tooltip("屏幕角度模式")]
    public ScreenAngleMode screenAngleMode = ScreenAngleMode.Preset;
    [Tooltip("固定角度(度)")]
    public float anglePresetDegrees;
    [Tooltip("随机角度上限(度)")]
    [Range(0f, 360f)]
    public float angleRandomRange = 360f;

    [Header("World Directional 参数")]
    [Tooltip("世界喷射主轴方向")]
    public WorldDirectionMode worldDirectionMode = WorldDirectionMode.FromAttackerToTarget;
    [Tooltip("绕主轴Roll模式")]
    public VFXRollMode rollMode = VFXRollMode.Random;
    [Tooltip("固定Roll角度(度)")]
    public float rollPresetDegrees;
    [Tooltip("随机Roll上限(度)")]
    [Range(0f, 360f)]
    public float rollRandomRange = 360f;
}
