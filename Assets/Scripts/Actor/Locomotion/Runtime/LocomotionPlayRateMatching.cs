using UnityEngine;

public static class LocomotionPlayRateMatching
{
    public const float MinimumRate = 0.5f;
    public const float MaximumRate = 1.5f;
    public const float SmoothSeconds = 0.1f;
    public const float MinimumReferenceSpeed = 0.1f;
    public static float Update(float previous, float actualSpeed, float referenceSpeed, float dt)
    {
        if (!LocomotionDataValidation.IsFinite(dt) || dt <= 0f) return previous;
        if (!LocomotionDataValidation.IsFinite(referenceSpeed) || referenceSpeed < MinimumReferenceSpeed
            || !LocomotionDataValidation.IsFinite(actualSpeed) || actualSpeed < 0f) return 1f;
        float target = Mathf.Clamp(actualSpeed / referenceSpeed, MinimumRate, MaximumRate);
        return Mathf.Lerp(previous, target, 1f - Mathf.Exp(-dt / SmoothSeconds));
    }
}
