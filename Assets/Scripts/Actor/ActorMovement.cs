using UnityEngine;

public class ActorMovement : MonoBehaviour
{
    public Actor actor;
    public Animator animator;
    [SerializeField] private float rotateSpeed = 600f; // 稍微调快一点旋转速度

    private Quaternion targetRotation = Quaternion.identity;
    private Vector3 gravityVelocity = Vector3.zero;

    // 你之前的死区逻辑参数
    private float _yPositionDeadZone = 0.5f;
    private float _accumulatedYDelta;

    private void Start()
    {
        targetRotation = transform.rotation;
    }

    /// <summary>
    /// 只负责更新旋转，位移完全交给RootMotion
    /// </summary>
    /// <param name="faceDirection">角色应该面朝的方向</param>
    public void UpdateRotation(Vector3 faceDirection)
    {
        if (faceDirection.sqrMagnitude < 0.01f) return;

        // 计算目标旋转
        targetRotation = Quaternion.LookRotation(faceDirection, Vector3.up);
    }

    private void Update()
    {
        // 平滑旋转
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );

        // 单独处理重力，因为RootMotion通常处理不好Y轴重力
        PerformGravity();
    }

    private void OnAnimatorMove()
    {
        if (actor.characterController == null) return;

        // 1. 获取动画原本想让角色移动的向量 (Root Motion)
        Vector3 deltaPos = animator.deltaPosition;

        // 你原本的 Y 轴死区处理逻辑 (保留)
        float currentYDelta = deltaPos.y;
        if (Mathf.Abs(currentYDelta) < _yPositionDeadZone)
        {
            _accumulatedYDelta += currentYDelta;
            if (Mathf.Abs(_accumulatedYDelta) >= _yPositionDeadZone)
            {
                deltaPos.y = _accumulatedYDelta;
                _accumulatedYDelta = 0;
            }
            else
            {
                deltaPos.y = 0;
            }
        }
        else
        {
            _accumulatedYDelta = 0;
        }

        // 2. 将动画位移应用给 CharacterController
        // 注意：这里不应用 deltaRotation，因为旋转已经在 Update 中由代码接管了
        // 这样可以避免动画里的旋转和代码计算的旋转打架，手感更顺滑
        actor.characterController.Move(deltaPos);
    }

    void PerformGravity()
    {
        if (actor.characterController.isGrounded)
        {
            // 给一个微小的向下力，确保 isGrounded 检测稳定
            gravityVelocity = Vector3.down * 2f;
        }
        else
        {
            gravityVelocity += Physics.gravity * Time.deltaTime;
        }

        // 应用重力位移
        actor.characterController.Move(gravityVelocity * Time.deltaTime);
    }

    internal void ResetRotation()
    {
        targetRotation = Quaternion.identity;
        transform.localRotation = Quaternion.identity;
    }
}