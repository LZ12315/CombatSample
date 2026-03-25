/// <summary>
/// 命中 VFX 正面朝向（生成点仍由射线交点决定）。
/// </summary>
public enum VFXOrientationMode
{
    /// <summary> 从生成点指向出手者（攻击者）参考点：默认其 CharacterController 中心，可 ExposedReference 覆盖 </summary>
    FaceAttacker,
    /// <summary> 正面朝世界向上 </summary>
    WorldUp,
}

/// <summary>
/// 在朝向确定后，绕该朝向的 forward 轴旋转。
/// </summary>
public enum VFXRollMode
{
    Random,
    Preset,
}
