using UnityEngine;

/// <summary>
/// 受击反馈挂点 — 挂在可被攻击的目标上，引用 <see cref="HitFeedbackProfile"/>。
/// 具体播放由 <see cref="ImpactSystem"/> 根据 Profile 的 effects 执行。
/// </summary>
public class HitFeedbackReceiver : MonoBehaviour
{
    [SerializeField] private HitFeedbackProfile profile;

    [Tooltip("可选：受击 VFX 朝向参考点；不填则用本次攻击者的 CharacterController 中心")]
    [SerializeField] private Transform hitFacingTargetOverride;

    public HitFeedbackProfile Profile => profile;
    public Transform HitFacingTargetOverride => hitFacingTargetOverride;

    /// <summary>受击音效用 AudioSource（由 ImpactSystem 使用）。</summary>
    public AudioSource FeedbackAudioSource
    {
        get
        {
            EnsureAudioSource();
            return _audioSource;
        }
    }

    private AudioSource _audioSource;

    private void Awake()
    {
        EnsureAudioSource();
    }

    void EnsureAudioSource()
    {
        if (_audioSource != null) return;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
            _audioSource.playOnAwake = false;
        }
    }
}
