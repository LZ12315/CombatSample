using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// 打击感系统管理器 - 场景单例。
/// Clip 与 <see cref="HitFeedbackProfile"/> 共用同一套 <see cref="ImpactEffectConfig"/>，由本类按顺序调度。
/// </summary>
public class ImpactSystem : MonoBehaviour
{
    #region Singleton Pattern
    private static ImpactSystem _instance;
    private static bool _hasWarnedMissingInstance;
    public static ImpactSystem Instance => _instance;
    private bool _hasWarnedMissingImpulseSource;
    private bool _hasWarnedMissingHitVfxLayer;

    [Tooltip("命中震屏：向 Cinemachine 广播 Impulse。留空则在本物体上自动添加")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _hasWarnedMissingInstance = false;
        EnsureImpulseSource();
        Initialize();
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            _hasWarnedMissingInstance = false;
        }
    }

    public static void EnsureExists()
    {
        if (_instance == null)
        {
            if (_hasWarnedMissingInstance) return;
            _hasWarnedMissingInstance = true;
            Debug.LogWarning("ImpactSystem is missing in scene. Please place a preconfigured ImpactSystem/Manager object.");
        }
    }
    #endregion

    enum ImpactEffectSource
    {
        Clip,
        TargetProfile,
    }

    struct GatheredAttackerEffects
    {
        public HitStopEffectConfig HitStop;
        public HitStickEffectConfig HitStick;
        public ScreenShakeEffectConfig ScreenShake;
    }

    #region 效果管理
    private List<ImpactEffect> activeEffects = new List<ImpactEffect>();

    public void ApplyImpact(ImpactData impactData, IReadOnlyList<ImpactEffectConfig> clipEffects)
    {
        if (impactData == null) return;

        var gathered = new GatheredAttackerEffects();

        if (clipEffects != null)
        {
            foreach (var effect in clipEffects)
                HandleEffect(impactData, effect, ImpactEffectSource.Clip, ref gathered);
        }

        var receiver = impactData.TargetReceiver;
        if (receiver == null && impactData.TargetObject != null)
            receiver = impactData.TargetObject.GetComponentInParent<HitFeedbackReceiver>();

        var profile = impactData.TargetProfile ?? receiver?.Profile;
        if (receiver != null && profile != null && profile.HasConfiguredImpactEffects())
        {
            foreach (var effect in profile.effects)
                HandleEffect(impactData, effect, ImpactEffectSource.TargetProfile, ref gathered);
        }

        if (gathered.HitStop != null || gathered.HitStick != null)
        {
            var speedEffect = new AttackerSpeedEffect();
            speedEffect.Execute(impactData.Attacker, gathered.HitStop, gathered.HitStick);
            if (speedEffect.IsActive)
                activeEffects.Add(speedEffect);
        }

        if (gathered.ScreenShake != null)
            TriggerScreenShake(impactData, gathered.ScreenShake);
    }

    void HandleEffect(
        ImpactData impactData,
        ImpactEffectConfig effect,
        ImpactEffectSource source,
        ref GatheredAttackerEffects gathered)
    {
        if (effect == null || !effect.enabled) return;

        switch (effect)
        {
            case HitStopEffectConfig stop:
                if (gathered.HitStop == null)
                    gathered.HitStop = stop;
                else
                    Debug.LogWarning("Duplicate HitStopEffectConfig found. Only the first one will be used.", this);
                break;
            case HitStickEffectConfig stick:
                if (gathered.HitStick == null)
                    gathered.HitStick = stick;
                else
                    Debug.LogWarning("Duplicate HitStickEffectConfig found. Only the first one will be used.", this);
                break;
            case ScreenOrientedImpactEffectConfig screenOriented:
                SpawnScreenOrientedImpactVfx(impactData, screenOriented);
                break;
            case WorldDirectionalImpactEffectConfig worldDirectional:
                SpawnWorldDirectionalImpactVfx(impactData, worldDirectional);
                break;
            case ScreenShakeEffectConfig shake:
                if (gathered.ScreenShake == null)
                    gathered.ScreenShake = shake;
                else
                    Debug.LogWarning("Duplicate ScreenShakeEffectConfig found. Only the first one will be used.", this);
                break;
            case HitSoundEffectConfig sound:
                PlayHitSound(impactData, sound, source);
                break;
        }
    }

    static void PlayHitSound(ImpactData impactData, HitSoundEffectConfig config, ImpactEffectSource source)
    {
        if (config == null || config.clips == null || config.clips.Length == 0) return;

        var clip = config.clips[Random.Range(0, config.clips.Length)];
        if (clip == null) return;

        AudioSource src = null;
        if (source == ImpactEffectSource.TargetProfile)
        {
            var receiver = impactData.TargetReceiver
                           ?? impactData.TargetObject?.GetComponentInParent<HitFeedbackReceiver>();
            src = receiver?.FeedbackAudioSource;
        }

        if (src == null && impactData.Attacker != null)
            src = GetOrAddWorldAudioSource(impactData.Attacker.gameObject);

        if (src == null) return;

        src.pitch = 1f + Random.Range(-config.pitchVariation, config.pitchVariation);
        src.PlayOneShot(clip, config.volume);
    }

    static AudioSource GetOrAddWorldAudioSource(GameObject root)
    {
        if (root == null) return null;
        var src = root.GetComponent<AudioSource>();
        if (src == null)
            src = root.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.playOnAwake = false;
        return src;
    }

    void SpawnScreenOrientedImpactVfx(ImpactData impactData, ScreenOrientedImpactEffectConfig config)
    {
        if (config == null || config.prefab == null) return;

        Vector3 spawnPos = impactData.VfxSpawnPoint;
        var rotation = ScreenOrientedImpactRotationResolver.Resolve(
            config.angleMode,
            config.anglePresetDegrees,
            config.angleRandomRange,
            spawnPos);
        SpawnHitVfxInstance(config.prefab, spawnPos, rotation, config.occlusionMode, config.scale, config.lifetime, config.simulationSpeed);
    }

    void SpawnWorldDirectionalImpactVfx(ImpactData impactData, WorldDirectionalImpactEffectConfig config)
    {
        if (config == null || config.prefab == null) return;

        Vector3 spawnPos = impactData.VfxSpawnPoint;
        var rotation = WorldDirectionalImpactRotationResolver.Resolve(
            config.directionMode,
            config.rollMode,
            config.rollPresetDegrees,
            config.rollRandomRange,
            impactData);
        SpawnHitVfxInstance(config.prefab, spawnPos, rotation, config.occlusionMode, config.scale, config.lifetime, config.simulationSpeed);
    }

    void SpawnHitVfxInstance(
        GameObject prefab,
        Vector3 spawnPos,
        Quaternion rotation,
        HitVfxOcclusionMode occlusionMode,
        float scale,
        float lifetime,
        float simulationSpeed)
    {
        var vfx = Instantiate(prefab, spawnPos, rotation);
        if (occlusionMode == HitVfxOcclusionMode.EnvironmentOnly
            && !HitConfirmVfxRenderUtility.TrySetHitConfirmLayer(vfx)
            && !_hasWarnedMissingHitVfxLayer)
        {
            _hasWarnedMissingHitVfxLayer = true;
            Debug.LogWarning($"Layer '{HitConfirmVfxRenderUtility.HitConfirmVfxLayerName}' is missing. Hit VFX will fall back to normal depth.", this);
        }

        vfx.transform.localScale = Vector3.one * scale;

        float speed = Mathf.Max(simulationSpeed, 0.01f);
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.simulationSpeed = speed;
        }

        Destroy(vfx, lifetime / speed);
    }

    void EnsureImpulseSource()
    {
        if (_impulseSource != null) return;
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        if (_impulseSource == null && !_hasWarnedMissingImpulseSource)
        {
            _hasWarnedMissingImpulseSource = true;
            Debug.LogWarning("ImpactSystem has no CinemachineImpulseSource. ScreenShakeEffectConfig will be ignored until one is assigned.", this);
        }
    }

    void TriggerScreenShake(ImpactData impactData, ScreenShakeEffectConfig config)
    {
        if (config == null) return;

        EnsureImpulseSource();
        if (_impulseSource == null) return;

        float force = config.intensity;
        if (force <= 0f) return;

        Vector3 velocity = Random.onUnitSphere * force;
        _impulseSource.GenerateImpulseAtPositionWithVelocity(impactData.VfxSpawnPoint, velocity);
    }

    #endregion

    #region Unity事件
    private void Initialize() { }

    void Update()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (!activeEffects[i].Update())
            {
                activeEffects[i].Reset();
                activeEffects.RemoveAt(i);
            }
        }
    }

    #endregion

    #region 工具方法
    public void ClearAllEffects()
    {
        foreach (var effect in activeEffects)
        {
            effect.Reset();
        }
        activeEffects.Clear();
    }

    public bool HasActiveEffects()
    {
        return activeEffects.Count > 0;
    }
    #endregion
}
