public static class CombatSimulationTiming
{
    public const int FrameRate = 60;
    public const float FixedDeltaTime = 1f / FrameRate;

    public static bool IsGameplayFrameRate(int frameRate)
    {
        return frameRate == FrameRate;
    }

    public static bool IsGameplayFixedDeltaTime(float deltaTime)
    {
        return UnityEngine.Mathf.Abs(deltaTime - FixedDeltaTime) <= 0.000001f;
    }
}
