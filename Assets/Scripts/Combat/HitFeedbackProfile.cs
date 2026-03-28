using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 受击反馈配置 — 挂在不同类型目标上，决定被打时的表现。
/// 在 <see cref="effects"/> 中配置与 Clip 相同的 <see cref="ImpactEffectConfig"/>（常用 Hit Sound / Hit VFX）。
/// </summary>
[CreateAssetMenu(fileName = "NewHitFeedbackProfile", menuName = "Combat/Hit Feedback Profile")]
public class HitFeedbackProfile : ScriptableObject
{
    [Tooltip("On each hit, runs enabled items in order after clip effects. Leave Receiver empty to skip hit feedback.")]
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
