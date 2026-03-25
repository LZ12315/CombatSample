using UnityEngine;

/// <summary>
/// 受击反馈配置 — 挂在不同类型目标上，决定被打时的音画表现。
/// 同一把武器打不同敌人，音效和粒子不同。
/// </summary>
[CreateAssetMenu(fileName = "NewHitFeedbackProfile", menuName = "Combat/Hit Feedback Profile")]
public class HitFeedbackProfile : ScriptableObject
{
    [Header("Hit Sound")]
    [Tooltip("随机选取一条播放；留空则不播音效")]
    public AudioClip[] hitSounds;

    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Tooltip("每次播放在 1 ± 此值 范围内随机 pitch，避免重复感")]
    [Range(0f, 0.3f)]
    public float pitchVariation = 0.1f;

    [Header("Hit VFX")]
    [Tooltip("击中时生成的粒子预制体；留空则不生成")]
    public GameObject hitVFXPrefab;

    [Range(0.1f, 5f)]
    public float vfxScale = 1f;

    [Tooltip("粒子存活时长，到期自动销毁")]
    [Range(0.5f, 5f)]
    public float vfxLifetime = 2f;

    [Tooltip(
        "FaceAttacker — 从生成点指向出手者（默认攻击者 CharacterController 中心；可在 HitFeedbackReceiver 上覆盖 Transform）\n" +
        "WorldUp     — 正面朝上"
    )]
    public VFXOrientationMode hitVfxOrientation = VFXOrientationMode.FaceAttacker;

    [Tooltip("Random — 绕朝向轴随机 [0, hitVfxRollRandomRange)；Preset — 固定 hitVfxRollPresetDegrees")]
    public VFXRollMode hitVfxRollMode = VFXRollMode.Random;

    [Tooltip("RollMode=Preset 时绕朝向轴的角度（度）")]
    public float hitVfxRollPresetDegrees;

    [Tooltip("RollMode=Random 时随机角上界（度），默认 360；0 表示不随机")]
    [Range(0f, 360f)]
    public float hitVfxRollRandomRange = 360f;

    public AudioClip GetRandomHitSound()
    {
        if (hitSounds == null || hitSounds.Length == 0) return null;
        return hitSounds[Random.Range(0, hitSounds.Length)];
    }
}
