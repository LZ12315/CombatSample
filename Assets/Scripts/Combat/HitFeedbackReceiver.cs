using UnityEngine;

/// <summary>
/// 受击反馈执行器 — 挂在可被攻击的目标上。
/// 由 ImpactSystem 在命中时调用，根据 HitFeedbackProfile 播放音效和生成粒子。
/// </summary>
public class HitFeedbackReceiver : MonoBehaviour
{
    [SerializeField] private HitFeedbackProfile profile;

    [Tooltip("可选：受击 VFX 朝向参考点；不填则用本次攻击者的 CharacterController 中心")]
    [SerializeField] private Transform hitFacingTargetOverride;

    public HitFeedbackProfile Profile => profile;
    public Transform HitFacingTargetOverride => hitFacingTargetOverride;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
            _audioSource.playOnAwake = false;
        }
    }

    /// <summary>
    /// 播放一次受击反馈（音效 + VFX）。旋转由 ImpactSystem 根据 Profile 预先解析。
    /// </summary>
    public void PlayFeedback(Vector3 worldPosition, Quaternion vfxRotation)
    {
        if (profile == null) return;

        PlayHitSound();
        SpawnHitVFX(worldPosition, vfxRotation);
    }

    private void PlayHitSound()
    {
        var clip = profile.GetRandomHitSound();
        if (clip == null) return;

        _audioSource.pitch = 1f + Random.Range(-profile.pitchVariation, profile.pitchVariation);
        _audioSource.PlayOneShot(clip, profile.volume);
    }

    private void SpawnHitVFX(Vector3 worldPosition, Quaternion vfxRotation)
    {
        if (profile.hitVFXPrefab == null) return;

        var vfx = Instantiate(profile.hitVFXPrefab, worldPosition, vfxRotation);
        vfx.transform.localScale = Vector3.one * profile.vfxScale;
        Destroy(vfx, profile.vfxLifetime);
    }
}
