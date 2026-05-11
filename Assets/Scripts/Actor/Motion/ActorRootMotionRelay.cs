using UnityEngine;

/// <summary>
/// 挂在 Animator 同 GameObject 上，把 Root Motion delta 转发给父级 ActorMotor。
///
/// 所有者：Animator GameObject 的 prefab 层级。
/// 依赖：必须挂在 Animator 所在物体上；父级必须有 ActorMotor 组件。
/// 不要运行时自动添加——应由 prefab Inspector 显式装配。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class ActorRootMotionRelay : MonoBehaviour
{
    [SerializeField] private ActorMotor motor;
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (motor == null)
            motor = GetComponentInParent<ActorMotor>();

        if (motor == null)
        {
            Debug.LogError(
                $"[ActorRootMotionRelay] No ActorMotor assigned or found in parent of '{name}'. "
                + "Root Motion will NOT be applied. Assign the motor in the Inspector or ensure "
                + "this relay is on a child of a GameObject with an ActorMotor component.",
                this);
        }
    }

    private void OnAnimatorMove()
    {
        if (motor != null && _animator != null)
        {
            motor.AddAnimatorRootMotionDelta(
                _animator.deltaPosition,
                _animator.deltaRotation);
        }
    }

    private void OnValidate()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (motor == null)
            motor = GetComponentInParent<ActorMotor>();
        if (motor == null)
        {
            Debug.LogWarning(
                $"[ActorRootMotionRelay] No ActorMotor assigned or found in parent of '{name}'. "
                + "Assign the motor in the Inspector or place this relay on a child of the "
                + "ActorMotor GameObject.",
                this);
        }
    }
}
