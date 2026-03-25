using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// 打击感系统管理器 - 场景单例。
/// 场景中应显式放置并配置；ActionHitBoxBehavior 仅向其发送命中上下文与效果列表。
/// </summary>
public class ImpactSystem : MonoBehaviour
{
    #region Singleton Pattern
    private static ImpactSystem _instance;
    private static bool _hasWarnedMissingInstance;
    public static ImpactSystem Instance => _instance;
    private bool _hasWarnedMissingImpulseSource;

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

    #region 效果管理
    private List<ImpactEffect> activeEffects = new List<ImpactEffect>();

    public void ApplyImpact(ImpactData impactData, IReadOnlyList<ImpactEffectConfig> effects)
    {
        if (impactData == null) return;

        HitStopEffectConfig hitStopConfig = null;
        HitStickEffectConfig hitStickConfig = null;
        ScreenShakeEffectConfig screenShakeConfig = null;
        TargetFeedbackEffectConfig targetFeedbackConfig = null;

        if (effects != null)
        {
            foreach (var effect in effects)
            {
                if (effect == null || !effect.enabled) continue;

                switch (effect)
                {
                    case HitStopEffectConfig stop:
                        if (hitStopConfig == null)
                            hitStopConfig = stop;
                        else
                            Debug.LogWarning("Duplicate HitStopEffectConfig found. Only the first one will be used.", this);
                        break;
                    case HitStickEffectConfig stick:
                        if (hitStickConfig == null)
                            hitStickConfig = stick;
                        else
                            Debug.LogWarning("Duplicate HitStickEffectConfig found. Only the first one will be used.", this);
                        break;
                    case HitConfirmVfxEffectConfig vfx:
                        SpawnHitConfirmVFX(impactData, vfx);
                        break;
                    case ScreenShakeEffectConfig shake:
                        if (screenShakeConfig == null)
                            screenShakeConfig = shake;
                        else
                            Debug.LogWarning("Duplicate ScreenShakeEffectConfig found. Only the first one will be used.", this);
                        break;
                    case TargetFeedbackEffectConfig targetFeedback:
                        if (targetFeedbackConfig == null)
                            targetFeedbackConfig = targetFeedback;
                        else
                            Debug.LogWarning("Duplicate TargetFeedbackEffectConfig found. Only the first one will be used.", this);
                        break;
                }
            }
        }

        if (hitStopConfig != null || hitStickConfig != null)
        {
            var speedEffect = new AttackerSpeedEffect();
            speedEffect.Execute(impactData.Attacker, hitStopConfig, hitStickConfig);
            if (speedEffect.IsActive)
                activeEffects.Add(speedEffect);
        }

        if (screenShakeConfig != null)
            TriggerScreenShake(impactData, screenShakeConfig);

        if (targetFeedbackConfig != null)
            ApplyTargetFeedback(impactData);
    }

    private void SpawnHitConfirmVFX(ImpactData impactData, HitConfirmVfxEffectConfig config)
    {
        if (config == null || config.prefab == null) return;

        Vector3 spawnPos = impactData.VfxSpawnPoint;
        var rotation = VFXRotationResolver.Resolve(
            config.orientation,
            config.rollMode,
            spawnPos,
            impactData.FacingReferenceWorldPosition,
            config.rollPresetDegrees,
            config.rollRandomRange);
        var vfx = Instantiate(config.prefab, spawnPos, rotation);
        vfx.transform.localScale = Vector3.one * config.scale;

        float speed = Mathf.Max(config.simulationSpeed, 0.01f);
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.simulationSpeed = speed;
        }

        Destroy(vfx, config.lifetime / speed);
    }

    private void ApplyTargetFeedback(ImpactData impactData)
    {
        if (impactData.TargetObject == null) return;

        var receiver = impactData.TargetObject.GetComponentInParent<HitFeedbackReceiver>();
        if (receiver == null) return;

        var profile = receiver.Profile;
        if (profile == null) return;

        Vector3 facingRef = HitVfxFacingUtility.ResolveFacingWorldPosition(
            receiver.HitFacingTargetOverride,
            impactData.Attacker);
        Quaternion rot = VFXRotationResolver.Resolve(
            profile.hitVfxOrientation,
            profile.hitVfxRollMode,
            impactData.VfxSpawnPoint,
            facingRef,
            profile.hitVfxRollPresetDegrees,
            profile.hitVfxRollRandomRange);

        receiver.PlayFeedback(impactData.VfxSpawnPoint, rot);
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
