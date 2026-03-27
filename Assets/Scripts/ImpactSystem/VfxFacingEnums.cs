/// <summary>
/// 在朝向确定后，绕该朝向的 forward 轴旋转。
/// </summary>
public enum VFXRollMode
{
    Random,
    Preset,
}

/// <summary>刀痕：在屏幕平面内绕相机视轴（camera.forward）的旋转角。</summary>
public enum ScreenAngleMode
{
    Preset,
    Random,
}

/// <summary>主喷射轴在世界空间中的语义。</summary>
public enum WorldDirectionMode
{
    FromAttackerToTarget,
    FromTargetToAttacker,
    WorldUp,
    CameraForward,
}
