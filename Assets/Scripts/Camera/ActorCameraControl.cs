using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

/// <summary>
/// 核心相机控制器。
/// 挂载位置：角色根物体下的 CameraPivot 子物体。
/// </summary>

public class ActorCameraControl : MonoBehaviour
{
    #region 1. 组件与配置

    public Actor actor;

    // 【修改】改为私有，代码在 Awake 中自动创建
    private CinemachineTargetGroup _targetGroup;
    public CinemachineTargetGroup TargetGroup => _targetGroup;

    [Header("虚拟相机")]
    public CinemachineFreeLook normalFreeLookCamera; // Free
    public CinemachineVirtualCamera softLockCamera;   // SoftLock
    public CinemachineVirtualCamera hardLockCamera;   // HardLock

    [Header("Pivot 旋转参数")]
    [Tooltip("Pivot 跟随敌人旋转的平滑速度")]
    public float pivotRotationSpeed = 10f;
    [Tooltip("退出锁定回到自由模式时的回正速度")]
    public float freeModeResetSpeed = 5f;

    [Header("智能偏移 (锁定模式)")]
    [Tooltip("固定的偏移角度 (肩部偏置)。Pivot 将保持这个角度，而不是回正到0。")]
    public float fixedOffsetAngle = 15f;
    [Tooltip("偏移角度变化的平滑时间")]
    public float offsetSmoothTime = 0.2f;
    [Tooltip("切换左右肩所需的最小输入阈值")]
    public float shoulderSwitchThreshold = 0.1f;

    // 内部状态
    [SerializeField] private Enums.PlayerCameraState currentState;
    private Dictionary<Enums.PlayerCameraState, ICinemachineCamera> _stateToCameraMap;

    // 平滑计算变量
    private float _currentOffsetAngle;
    private float _offsetVelocity;
    private float _targetShoulderSide = 1f; // 1: 右肩, -1: 左肩 (记忆方向)

    public Enums.PlayerCameraState CinemachineState
    {
        get => currentState;
        set => SetCameraState(value);
    }
    #endregion

    #region 2. 初始化与生命周期

    void Awake()
    {
        // 1. 动态创建 TargetGroup
        GameObject groupObj = new GameObject("Runtime_TargetGroup");
        _targetGroup = groupObj.AddComponent<CinemachineTargetGroup>();
        _targetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupCenter;
        _targetGroup.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;

        // 2. 添加玩家自己 (因为脚本挂在 CameraPivot 上，transform 就是 Pivot)
        _targetGroup.AddMember(transform, 1f, 2f); // Weight 1, Radius 2
    }

    void Start()
    {
        InitializeCameraMap();

        // 3. 自动将动态创建的 Group 赋值给锁定相机的 LookAt
        if (softLockCamera != null) softLockCamera.LookAt = _targetGroup.transform;
        if (hardLockCamera != null) hardLockCamera.LookAt = _targetGroup.transform;

        // 初始对齐
        transform.localRotation = Quaternion.identity;

        // 初始化状态
        SetCameraState(currentState, true);
    }

    void InitializeCameraMap()
    {
        _stateToCameraMap = new Dictionary<Enums.PlayerCameraState, ICinemachineCamera>
            {
                { Enums.PlayerCameraState.Free, normalFreeLookCamera },
                { Enums.PlayerCameraState.SoftLock, softLockCamera },
                { Enums.PlayerCameraState.HardLock, hardLockCamera }
            };
    }

    // 核心逻辑放在 LateUpdate，确保在 LogicInput(Update) 之后执行
    void LateUpdate()
    {
        UpdatePivotBehavior();
    }

    #endregion

    #region 3. 核心逻辑：Pivot 驱动

    /// <summary>
    /// 控制 CameraPivot 的旋转，包含“智能偏移”和“肩部记忆”逻辑
    /// </summary>
    public void UpdatePivotBehavior()
    {
        Transform enemy = actor.combater.CombatTarget?.transform;
        bool isLockMode = currentState != Enums.PlayerCameraState.Free;

        if (isLockMode && enemy != null)
        {
            // ===========================
            // 锁定模式 (Soft / Hard)
            // ===========================

            // A. 计算基础朝向：从 Pivot 指向 敌人 (忽略高度差)
            Vector3 dirToEnemy = enemy.position - transform.position;
            dirToEnemy.y = 0;

            if (dirToEnemy.sqrMagnitude > 0.001f)
            {
                // B. 获取输入并更新肩部记忆
                float inputX = (actor.logicInput != null) ? actor.logicInput.MoveInput.x : 0f;

                // 只有当输入明确超过阈值时，才切换肩部方向
                // 如果停下或只按前后，保持上一次的 _targetShoulderSide
                if (inputX > shoulderSwitchThreshold) _targetShoulderSide = 1f;      // 向右偏移
                else if (inputX < -shoulderSwitchThreshold) _targetShoulderSide = -1f; // 向左偏移

                // C. 计算目标角度 (固定偏移量)
                float targetOffset = _targetShoulderSide * fixedOffsetAngle;

                // D. 平滑插值角度
                _currentOffsetAngle = Mathf.SmoothDamp(_currentOffsetAngle, targetOffset, ref _offsetVelocity, offsetSmoothTime);

                // E. 合成最终旋转：基础朝向 * 偏移旋转
                Quaternion baseRotation = Quaternion.LookRotation(dirToEnemy);
                Quaternion finalRotation = baseRotation * Quaternion.Euler(0, _currentOffsetAngle, 0);

                // F. 应用旋转
                transform.rotation = Quaternion.Slerp(transform.rotation, finalRotation, Time.deltaTime * pivotRotationSpeed);
            }
        }
        else
        {
            // ===========================
            // 自由模式 (Free)
            // ===========================

            // 重置偏移计算变量
            _currentOffsetAngle = 0f;
            _offsetVelocity = 0f;
            // _targetShoulderSide 保持不变，或者重置为 1f (默认右肩)
            _targetShoulderSide = 1f;

            // Pivot 归零：
            // 在自由模式下，Pivot 不需要旋转，它应该保持相对静止（跟随父物体），
            // 或者平滑回正到 Identity，把旋转权交给 CinemachineFreeLook。
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, Time.deltaTime * freeModeResetSpeed);
        }
    }

    #endregion

    #region 4. 状态切换与 TargetGroup

    public void SetCameraState(Enums.PlayerCameraState newState, bool forceUpdate = false)
    {
        if (currentState == newState && !forceUpdate) return;

        Transform enemyTarget = actor.combater.CombatTarget?.transform;

        // 安全检查：无目标强制回退 Free
        if (newState != Enums.PlayerCameraState.Free && enemyTarget == null)
        {
            newState = Enums.PlayerCameraState.Free;
        }

        currentState = newState;

        // --- 更新 TargetGroup 成员 ---
        if (_targetGroup != null)
        {
            // 清理旧敌人 (保留 Index 0 的玩家)
            for (int i = _targetGroup.m_Targets.Length - 1; i > 0; i--)
            {
                _targetGroup.RemoveMember(_targetGroup.m_Targets[i].target);
            }

            // 添加新敌人
            if (currentState != Enums.PlayerCameraState.Free && enemyTarget != null)
            {
                // Weight=1: 玩家和敌人视觉比重 1:1，GroupCenter 会在两人连线中点
                // Radius=2: 给敌人一点缓冲圈
                _targetGroup.AddMember(enemyTarget, 1f, 2f);
            }
        }

        // --- 切换 LookAt ---
        // 锁定模式 LookAt Group，自由模式 LookAt Null (FreeLook 自己处理)
        Transform lookAtTarget = (currentState == Enums.PlayerCameraState.Free) ? null : _targetGroup.transform;
        if (softLockCamera != null) softLockCamera.LookAt = lookAtTarget;
        if (hardLockCamera != null) hardLockCamera.LookAt = lookAtTarget;

        // --- 切换相机优先级 ---
        foreach (var kvp in _stateToCameraMap)
        {
            if (kvp.Value == null) continue;

            var camBase = kvp.Value as CinemachineVirtualCameraBase;
            if (camBase != null)
            {
                camBase.Priority = (kvp.Key == currentState) ? 20 : 10;
            }
        }
    }

    #endregion

    #region 5. 工具方法

    /// <summary>
    /// 计算基于当前主相机视角的移动方向 (用于 LogicInput)
    /// </summary>
    public Vector3 CalculateWorldDirection(Vector2 rawMove)
    {
        if (rawMove.sqrMagnitude <= 0.01f) return Vector3.zero;

        Transform mainCam = Camera.main.transform;
        Vector3 forward = mainCam.forward;
        Vector3 right = mainCam.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        return (forward * rawMove.y) + (right * rawMove.x);
    }

    #endregion
}

public partial class Enums
{
    public enum PlayerCameraState
    {
        Free, SoftLock, HardLock
    }
}
