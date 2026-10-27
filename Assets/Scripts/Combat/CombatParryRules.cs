using UnityEngine;

/// <summary>Pure parry eligibility. Direction is measured toward the attacker on the world's XZ plane.</summary>
internal static class CombatParryRules
{
    public static bool CanParry(
        bool attackCanBeParried,
        bool hasParryWindow,
        Vector3 defenderForward,
        Vector3 toAttacker,
        float halfAngle)
    {
        if (!attackCanBeParried || !hasParryWindow
            || !ActionAuthoringMath.IsFinite(defenderForward)
            || !ActionAuthoringMath.IsFinite(toAttacker)
            || !ActionAuthoringMath.IsFinite(halfAngle))
            return false;

        defenderForward.y = 0f;
        toAttacker.y = 0f;
        if (defenderForward.sqrMagnitude <= 0.0001f || toAttacker.sqrMagnitude <= 0.0001f)
            return false;

        float dot = Vector3.Dot(defenderForward.normalized, toAttacker.normalized);
        float threshold = Mathf.Cos(Mathf.Clamp(halfAngle, 0f, 90f) * Mathf.Deg2Rad);
        // Include the cone boundary despite float round-off (notably at 90 degrees).
        return dot >= threshold - 0.000001f;
    }
}
