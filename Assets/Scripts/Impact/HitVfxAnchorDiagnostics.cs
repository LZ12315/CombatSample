using UnityEngine;

/// <summary>
/// 命中与 VFX 参考点是否一致，可在 <see cref="HitVfxAnchorDiagnosticToggler"/> 上开日志验证。
/// </summary>
public static class HitVfxAnchorDiagnostics
{
    /// <summary>由 <see cref="HitVfxAnchorDiagnosticToggler"/> 在运行时写入。</summary>
    public static bool LogEnabled { get; set; }

    /// <summary>在 <see cref="AttackHandler"/> 中调用：比较「本帧命中的 other」与「在 Target 子树上 GetComponentInChildren 的第一个 Collider」是否同一实例、中心高度差。</summary>
    public static void LogFromAttackHandler(Collider other)
    {
        if (!LogEnabled || other == null) return;

        var first = other.gameObject.GetComponentInChildren<Collider>();
        bool same = first == other;
        float dy = 0f;
        if (first != null)
            dy = other.bounds.center.y - first.bounds.center.y;

        Debug.Log(
            $"[HitVfxDiag:命中体] 本次 other==首颗子树Collider? {same} | " +
            $"other=[{other.name} {other.GetType().Name} centerY={other.bounds.center.y:F3}] | " +
            $"first=[{first?.name} {first?.GetType().Name} centerY={first?.bounds.center.y:F3}] | " +
            $"d(centerY)={dy:F3} (应≈0; 非0 则 VFX 用错参考体) Frame={Time.frameCount}",
            other);
    }

    /// <summary>在 <see cref="ActionHitBoxBehavior.TriggerImpactEffect"/> 中调用：看命中点与最终生成点高度差。</summary>
    public static void LogFromHitBox(in AttackHitData hit, Vector3 baseSpawn, Vector3 afterCameraBias, Vector3 rayOrigin)
    {
        if (!LogEnabled) return;

        var col = hit.Target != null
            ? hit.Target.GetComponentInChildren<Collider>()
            : null;

        Debug.Log(
            $"[HitVfxDiag:生成点] target={hit.Target?.name} " +
            $"| HitPointY={hit.HitPoint.y:F3} " +
            $"| baseSpawnY={baseSpawn.y:F3} (射线表面点) " +
            $"| afterBiasY={afterCameraBias.y:F3} (最终VfxSpawnPoint) " +
            $"| 首子树Collider名={col?.name} 其centerY={col?.bounds.center.y:F3} " +
            $"| rayOrigY={rayOrigin.y:F3} " +
            $"| LateralBiasY≈{afterCameraBias.y - baseSpawn.y:F3} Frame={Time.frameCount}",
            hit.Target);
    }
}
