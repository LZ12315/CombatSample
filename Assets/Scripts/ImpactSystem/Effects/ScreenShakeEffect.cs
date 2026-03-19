using UnityEngine;

/// <summary>
/// 屏幕震动效果（Screen Shake）
/// 击中时震动相机，增强打击感
/// </summary>
public class ScreenShakeEffect : ImpactEffect
{
    private float timer;
    private float duration;
    private float intensity;
    private float frequency;
    private Vector3 originalCameraPosition;
    private Camera mainCamera;
    
    public override bool IsActive => timer > 0;
    
    public override void Execute(ImpactData impactData)
    {
        if (impactData.Config == null)
        {
            Debug.LogWarning("ScreenShakeEffect: ImpactData.Config is null");
            return;
        }
        
        // Get config parameters
        duration = impactData.Config.ShakeDuration * impactData.Config.OverallIntensity;
        intensity = impactData.Config.GetAdjustedShakeIntensity();
        frequency = impactData.Config.ShakeFrequency;
        
        // Get main camera
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("ScreenShakeEffect: No main camera found");
            return;
        }
        
        // Save original position
        originalCameraPosition = mainCamera.transform.localPosition;
        
        timer = duration;
        IsActive = true;
        
        Debug.Log($"ScreenShakeEffect executed: duration={duration}s, intensity={intensity}, frequency={frequency}");
    }
    
    public override bool Update()
    {
        if (!IsActive || mainCamera == null) return false;
        
        // Use unscaled time for consistency during HitStop
        timer -= Time.unscaledDeltaTime;
        
        if (timer <= 0)
        {
            // Restore camera position
            mainCamera.transform.localPosition = originalCameraPosition;
            IsActive = false;
            Debug.Log("ScreenShakeEffect ended");
            return false;
        }
        
        // Calculate shake
        float shakeAmount = intensity * (timer / duration); // Fade out
        float x = Mathf.PerlinNoise(Time.time * frequency, 0) - 0.5f;
        float y = Mathf.PerlinNoise(0, Time.time * frequency) - 0.5f;
        
        mainCamera.transform.localPosition = originalCameraPosition + new Vector3(x, y, 0) * shakeAmount;
        
        return true;
    }
    
    public override void Reset()
    {
        timer = 0;
        duration = 0;
        intensity = 0;
        frequency = 0;
        mainCamera = null;
        IsActive = false;
        
        // Ensure camera is restored
        if (mainCamera != null)
        {
            mainCamera.transform.localPosition = originalCameraPosition;
        }
    }
    
    /// <summary>
    /// Force immediately end the screen shake effect
    /// </summary>
    public void ForceEnd()
    {
        if (IsActive && mainCamera != null)
        {
            mainCamera.transform.localPosition = originalCameraPosition;
            timer = 0;
            IsActive = false;
            Debug.Log("ScreenShakeEffect force ended");
        }
    }
}
