using UnityEngine;

/// <summary>
/// Animator 所在对象上的 RootMotion 转发器。
/// OnAnimatorMove 由 Animator 同对象脚本接收，再把原始 delta 交给 ActorMotor 权威入口。
/// </summary>
public sealed class ActorRootMotionRelay : MonoBehaviour
{
    [SerializeField] private Actor actor;
    [SerializeField] private Animator animator;

    private ActorMotor _motor;

    private void Awake()
    {
        actor = actor != null ? actor : GetComponentInParent<Actor>();
        animator = animator != null ? animator : GetComponent<Animator>();
        _motor = actor != null ? actor.actorMotor : GetComponentInParent<ActorMotor>();
    }

    private void OnAnimatorMove()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            return;

        if (_motor == null)
            _motor = actor != null ? actor.actorMotor : GetComponentInParent<ActorMotor>();
        if (_motor == null)
            return;

        _motor.AddAnimatorRootMotionDelta(
            animator.deltaPosition,
            animator.deltaRotation);
    }
}
