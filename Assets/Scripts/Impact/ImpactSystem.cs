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

    [Tooltip("Camera shake: send Impulse to Cinemachine. Empty = add on this object.")]
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
        public SpeedEffectConfig SpeedEffect;
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

        // 处理新的 SpeedEffectConfig（推荐）
        if (gathered.SpeedEffect != null)
        {
            var speedEffect = new ActionSpeedEffect();
            speedEffect.Execute(impactData, gathered.SpeedEffect);
            if (speedEffect.IsActive)
                activeEffects.Add(speedEffect);
        }
        // 兼容旧配置：如果配置了旧版 HitStop/HitStick 但没有 SpeedEffect
        else if (gathered.HitStop != null || gathered.HitStick != null)
        {
            var speedEffect = new ActionSpeedEffect();
            // 合并旧配置：取最慢速度，最长时长，仅攻击者（旧行为）
            float duration = Mathf.Max(
                gathered.HitStop?.duration ?? 0f,
                gathered.HitStick?.duration ?? 0f);
            float speed = 1f;
            if (gathered.HitStop != null && gathered.HitStick != null)
                speed = Mathf.Min(gathered.HitStop.timeScale, gathered.HitStick.speedScale);
            else if (gathered.HitStop != null)
                speed = gathered.HitStop.timeScale;
            else if (gathered.HitStick != null)
                speed = gathered.HitStick.speedScale;

            var legacyConfig = new SpeedEffectConfig
            {
                enabled = true,
                duration = duration,
                speedScale = speed,
                affectBothParties = false // 旧配置默认仅攻击者
            };
            speedEffect.Execute(impactData, legacyConfig);
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
            case SpeedEffectConfig speed:
                if (gathered.SpeedEffect == null)
                    gathered.SpeedEffect = speed;
                else
                    Debug.LogWarning("Duplicate SpeedEffectConfig found. Only the first one will be used.", this);
                break;
            case ScreenOrientedImpactEffectConfig screenOriented:
                SpawnScreenOrientedImpactVfx(impactData, screenOriented);
                break;
            case WorldDirectionalImpactEffectConfig worldDirectional:
                SpawnWorldDirectionalImpactVfx(impactData, worldDirectional);
                break;
            case HitVfxConfig hitVfx:
                SpawnHitVfx(impactData, hitVfx);
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

    /// <summary>
    /// 统一的 HitVfxConfig 生成入口。先解算锚点位置，再按朝向模式解算旋转。
    /// </summary>
    void SpawnHitVfx(ImpactData impactData, HitVfxConfig config)
    {
        if (config == null || config.prefab == null) return;

        // 1. 解算锚点位置
        Vector3 spawnPos = ResolveAnchorPosition(impactData, config.anchorMode);

        // 2. 解算朝向
        Quaternion rotation = config.orientationMode switch
        {
            VfxOrientationMode.ScreenOriented => ScreenOrientedImpactRotationResolver.Resolve(
                config.screenAngleMode,
                config.anglePresetDegrees,
                config.angleRandomRange,
                spawnPos),
            VfxOrientationMode.WorldDirectional => WorldDirectionalImpactRotationResolver.Resolve(
                config.worldDirectionMode,
                config.rollMode,
                config.rollPresetDegrees,
                config.rollRandomRange,
                impactData),
            _ => Quaternion.identity
        };

        // 3. 生成
        SpawnHitVfxInstance(config.prefab, spawnPos, rotation, config.occlusionMode, config.scale, config.lifetime, config.simulationSpeed);
    }

    /// <summary>
    /// 根据锚点模式解算 VFX 生成位置。
    /// </summary>
    Vector3 ResolveAnchorPosition(ImpactData impactData, VfxAnchorMode anchorMode)
    {
        var sourceHit = impactData.SourceHit;

        return anchorMode switch
        {
            VfxAnchorMode.ContactHitPoint => sourceHit.HitPoint,
            VfxAnchorMode.HitColliderCenter => sourceHit.TargetCollider != null
                ? sourceHit.TargetCollider.bounds.center
                : sourceHit.HitPoint, // fallback
            _ => sourceHit.HitPoint
        };
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

        float speed = Mathf.Max(simulationSpeed, 0.01f);
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            // Prefabs often use Scaling Mode = Local, which ignores parent scale.
            // Clip "scale" is applied on the root; switch to Hierarchy so it takes effect.
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpeed = speed;
        }

        vfx.transform.localScale = Vector3.one * scale;

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
