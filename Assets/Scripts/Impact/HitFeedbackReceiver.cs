using UnityEngine;

/// <summary>
/// 命中反馈配置入口 — 普通受击与弹反成功分别引用 <see cref="HitFeedbackProfile"/>。
/// 具体播放由 <see cref="ImpactSystem"/> 根据 Profile 的 effects 执行。
/// </summary>
public class HitFeedbackReceiver : MonoBehaviour
{
    [SerializeField, Tooltip("Feedback for ordinary incoming hits, including super-armor hits.")]
    private HitFeedbackProfile profile;

    [SerializeField, Tooltip("Feedback when this defender successfully parries. Does not combine with incoming attack or ordinary hit effects. Empty = no parry feedback.")]
    private HitFeedbackProfile parryProfile;

    [Tooltip("Optional: aim hit VFX at this. Empty = use attacker CharacterController center.")]
    [SerializeField] private Transform hitFacingTargetOverride;

    public HitFeedbackProfile Profile => profile;
    public HitFeedbackProfile ParryProfile => parryProfile;
    public Transform HitFacingTargetOverride => hitFacingTargetOverride;

    public HitFeedbackProfile GetProfile(HitResolveOutcome outcome) => outcome switch
    {
        HitResolveOutcome.Normal => profile,
        HitResolveOutcome.SuperArmor => profile,
        HitResolveOutcome.Parried => parryProfile,
        _ => null,
    };

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
