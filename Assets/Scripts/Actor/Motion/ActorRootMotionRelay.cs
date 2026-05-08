using UnityEngine;

/// <summary>
/// Animator 所在对象上的 RootMotion 转发器。
/// 收到 OnAnimatorMove 后把 delta 交给父级 ActorMotor。
///
/// 参照 Animancer RedirectRootMotion 的风格：
/// - RequireComponent 保证与 Animator 同物体
/// - 零手动配置，Awake / OnValidate 自动拉引用
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class ActorRootMotionRelay : MonoBehaviour
{
    private Animator _animator;
    private ActorMotor _motor;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _motor = GetComponentInParent<ActorMotor>();
    }

    private void OnAnimatorMove()
    {
        if (_motor != null)
        {
            _motor.AddAnimatorRootMotionDelta(
                _animator.deltaPosition,
                _animator.deltaRotation);
        }
    }

    private void OnValidate()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_motor == null)
            _motor = GetComponentInParent<ActorMotor>();
    }
}
