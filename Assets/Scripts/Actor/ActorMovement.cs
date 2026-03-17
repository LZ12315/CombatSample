using UnityEngine;

public class ActorMovement : MonoBehaviour
{
    public Actor actor;
    public Animator animator;
    [SerializeField] private float rotateSpeed = 600f; // 稍微调快一点旋转速度

    // === 【新增核心】移动模式枚举 ===
    public enum MovementMode
    {
        RootMotion,      // 完全由动画驱动
        CodeDriven,      // 完全由代码驱动 (根据速度矢量)
        MotionWarping    // 混合模式 (预留给未来的攻击吸附、技能冲刺)
    }

    // === 【新增核心】当前帧的移动参数 ===
    private MovementMode _currentMode = MovementMode.RootMotion; // 默认依然是RootMotion
    private Vector3 _codeDrivenVelocity = Vector3.zero;          // 代码提供的速度
    private Vector3 _cachedRootMotionDelta = Vector3.zero;       // 缓存的动画位移

    private Quaternion targetRotation = Quaternion.identity;
    private Vector3 gravityVelocity = Vector3.zero;

    // 你之前的死区逻辑参数
    private float _yPositionDeadZone = 0.5f;
    private float _accumulatedYDelta;

    private void Start()
    {
        targetRotation = transform.rotation;
    }

    #region === 外部系统调用接口 (供 Locomotion 或 Action 随时调用) ===

    /// <summary>
    /// 设置当前的位移模式（通常在切换状态或Timeline轨道中调用）
    /// </summary>
    public void SetMovementMode(MovementMode mode)
    {
        _currentMode = mode;
    }

    /// <summary>
    /// 提供代码驱动的速度（当模式为 CodeDriven 时有效）
    /// </summary>
    public void SetCodeVelocity(Vector3 velocity)
    {
        _codeDrivenVelocity = velocity;
    }

    /// <summary>
    /// 只负责更新旋转目标
    /// </summary>
    public void UpdateRotation(Vector3 faceDirection)
    {
        if (faceDirection.sqrMagnitude < 0.01f) return;

        // 计算目标旋转
        targetRotation = Quaternion.LookRotation(faceDirection, Vector3.up);
    }
    
    internal void ResetRotation()
    {
        targetRotation = Quaternion.identity;
        transform.localRotation = Quaternion.identity;
    }

    #endregion

    #region === 核心生命周期 (彻底分离了“计算”与“执行”) ===

    // 1. 抓取动画位移 (绝不在这里直接调用 CharacterController.Move！)
    private void OnAnimatorMove()
    {
        if (actor.characterController == null) return;

        // 把处理过死区的动画位移【缓存】起来，留给 Update 统一处理
        _cachedRootMotionDelta = ProcessRootMotionDeadZone(animator.deltaPosition);
    }

    // 2. 统一计算与最终执行
    private void Update()
    {
        // --- 平滑旋转 ---
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );

        // --- 计算水平最终位移 ---
        Vector3 finalMovement = Vector3.zero;

        // 大管线根据当前模式，决定采用谁的位移数据
        switch (_currentMode)
        {
            case MovementMode.RootMotion:
                finalMovement = _cachedRootMotionDelta;
                break;

            case MovementMode.CodeDriven:
                finalMovement = _codeDrivenVelocity * Time.deltaTime;
                break;
                
            case MovementMode.MotionWarping:
                // 未来接入吸附逻辑时在此处理
                break;
        }

        // --- 处理重力 ---
        PerformGravity();
        finalMovement += gravityVelocity * Time.deltaTime;

        // --- 最终一击：全局只在此处调用一次 Move ---
        if (actor.characterController != null)
        {
            actor.characterController.Move(finalMovement);
        }

        // 清理当前帧缓存，防止异常残留
        _cachedRootMotionDelta = Vector3.zero; 
    }

    #endregion

    #region === 内部辅助计算 ===

    // 将你原本的 Y 轴死区处理逻辑提取为独立方法，保持代码清爽
    private Vector3 ProcessRootMotionDeadZone(Vector3 rawDelta)
    {
        float currentYDelta = rawDelta.y;
        if (Mathf.Abs(currentYDelta) < _yPositionDeadZone)
        {
            _accumulatedYDelta += currentYDelta;
            if (Mathf.Abs(_accumulatedYDelta) >= _yPositionDeadZone)
            {
                rawDelta.y = _accumulatedYDelta;
                _accumulatedYDelta = 0;
            }
            else
            {
                rawDelta.y = 0;
            }
        }
        else
        {
            _accumulatedYDelta = 0;
        }
        return rawDelta;
    }

    private void PerformGravity()
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
    }

    #endregion
}