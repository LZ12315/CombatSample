using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 命中反馈配置 — 由 Receiver 的普通受击或弹反成功入口选择。
/// 在 <see cref="effects"/> 中配置与 Clip 相同的 <see cref="ImpactEffectConfig"/>（常用 Hit Sound / Hit VFX）。
/// </summary>
[CreateAssetMenu(fileName = "NewHitFeedbackProfile", menuName = "CombatSample/Feedback/Hit Feedback")]
public class HitFeedbackProfile : ScriptableObject
{
    [Tooltip("Runs enabled items in order. Ordinary hits run after attack effects; successful parries run only the parry Profile. Assign this asset on the defender's HitFeedbackReceiver.")]
    [SerializeReference, SubclassSelector]
    public List<ImpactEffectConfig> effects = new List<ImpactEffectConfig>();

    /// <summary>是否存在至少一项启用的 effect。</summary>
    public bool HasConfiguredImpactEffects()
    {
        if (effects == null || effects.Count == 0) return false;
        foreach (var e in effects)
        {
            if (e != null && e.enabled) return true;
        }

        return false;
    }
}
