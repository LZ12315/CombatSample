using UnityEngine;

/// <summary>
/// 运动通道运行时状态：冲量、重力累积、以及水平/垂直 Velocity 覆盖（owner 控制）。
/// </summary>
public sealed class MotionChannels
{
    private int _nextOwnerId = 1;

    public Vector3 HorizontalImpulseVelocity;
    public float VerticalImpulseVelocity;
    public float GravityAccumulator;

    private bool _hActive;
    private int _hOwnerId;
    private Vector3 _hValue;
    private string _hDebugName;

    private bool _vActive;
    private int _vOwnerId;
    private float _vValue;
    private string _vDebugName;

    public MotionControlOwner CreateOwner() => new MotionControlOwner(_nextOwnerId++);

    public bool HasHorizontalVelocityControl => _hActive;
    public Vector3 HorizontalProgramVelocity => _hValue;

    public bool HasVerticalVelocityControl => _vActive;
    public float VerticalProgramVelocity => _vValue;

    public MotionControlOwner BeginHorizontalVelocityControl(string debugName)
    {
        var owner = CreateOwner();
        if (_hActive)
            Debug.LogWarning($"[MotionChannels] Horizontal velocity takeover: {_hDebugName} -> {debugName}");
        _hActive = true;
        _hOwnerId = owner.Id;
        _hValue = Vector3.zero;
        _hDebugName = debugName;
        return owner;
    }

    public void SetHorizontalVelocity(MotionControlOwner owner, Vector3 velocity)
    {
        if (!_hActive || owner.Id != _hOwnerId) return;
        velocity.y = 0f;
        _hValue = velocity;
    }

    public void EndHorizontalVelocityControl(MotionControlOwner owner)
    {
        if (!_hActive || owner.Id != _hOwnerId) return;
        _hActive = false;
        _hOwnerId = 0;
        _hValue = Vector3.zero;
        _hDebugName = null;
    }

    /// <summary>开始垂直 Velocity 覆盖；清零重力累积与垂直冲量，避免与隐藏重力积分打架。</summary>
    public MotionControlOwner BeginVerticalVelocityControl(string debugName)
    {
        var owner = CreateOwner();
        if (_vActive)
            Debug.LogWarning($"[MotionChannels] Vertical velocity takeover: {_vDebugName} -> {debugName}");
        _vActive = true;
        _vOwnerId = owner.Id;
        _vValue = 0f;
        _vDebugName = debugName;
        GravityAccumulator = 0f;
        VerticalImpulseVelocity = 0f;
        return owner;
    }

    public void SetVerticalVelocity(MotionControlOwner owner, float verticalSpeed)
    {
        if (!_vActive || owner.Id != _vOwnerId) return;
        _vValue = verticalSpeed;
    }

    public void EndVerticalVelocityControl(MotionControlOwner owner)
    {
        if (!_vActive || owner.Id != _vOwnerId) return;
        _vActive = false;
        _vOwnerId = 0;
        _vValue = 0f;
        _vDebugName = null;
        GravityAccumulator = 0f;
        VerticalImpulseVelocity = 0f;
    }

    public void ApplyHorizontalMomentumInheritance(float factor)
    {
        factor = Mathf.Clamp01(factor);
        HorizontalImpulseVelocity *= factor;
    }

    public void ApplyVerticalMomentumInheritance(float factor)
    {
        factor = Mathf.Clamp01(factor);
        GravityAccumulator *= factor;
        VerticalImpulseVelocity *= factor;
    }
}
