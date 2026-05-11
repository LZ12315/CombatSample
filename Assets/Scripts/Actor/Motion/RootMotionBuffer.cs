using UnityEngine;

/// <summary>
/// 缓存 Animator 输出的原始 RootMotion 位移/旋转。
/// 是否把这些 RootMotion 应用到 KCC，由 ActorMotionRuntime 的 RootMotionApplyMode 决定。
///
/// 使用 snapshot 模式防止 stale delta：BeginMotorTick() 对当前累积值做快照并清空缓冲区，
/// 之后的 KCC tick 只消费快照。即使 EndMotorTick 因异常被跳过，残留数据也不会泄漏到下一帧。
/// </summary>
public sealed class RootMotionBuffer
{
    private Vector3 _pendingPosition;
    private Quaternion _pendingRotation = Quaternion.identity;

    private Vector3 _tickPosition;
    private Quaternion _tickRotation = Quaternion.identity;

    public void AddAnimatorDelta(
        Vector3 deltaPosition,
        Quaternion deltaRotation)
    {
        _pendingPosition += deltaPosition;
        _pendingRotation = deltaRotation * _pendingRotation;
    }

    public Vector3 PendingPosition => _tickPosition;
    public Quaternion PendingRotation => _tickRotation;

    /// <summary>
    /// 在 KCC tick 开始时调用：快照当前累积值供本帧消费，清空缓冲区继续接收下一帧的 root motion。
    /// </summary>
    public void BeginMotorTick()
    {
        _tickPosition = _pendingPosition;
        _tickRotation = _pendingRotation;
        _pendingPosition = Vector3.zero;
        _pendingRotation = Quaternion.identity;
    }
}
