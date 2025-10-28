using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using Cinemachine;

public class ActorCameraControl : MonoBehaviour
{
    public Actor actor;
    public CinemachineBrain cinemachineBrain;

    [Header("相机配置")]
    // 为三种相机模式分别声明对应的Cinemachine虚拟相机引用
    public CinemachineVirtualCamera normalVirtualCamera;
    public CinemachineFreeLook freeLookVirtualCamera; // FreeLook通常是特殊类型
    public CinemachineVirtualCamera combatVirtualCamera;

    [Header("相机属性")]
    [SerializeField] private Enums.PlayerCameraState currentState;
    public Enums.PlayerCameraState CinemachineState 
    {  get
        {
            return currentState;
        }
        set
        {
            currentState = value;
            SetCameraState(value);
        }
    }

    // 添加一个字典来方便管理虚拟相机和状态的映射关系
    private Dictionary<Enums.PlayerCameraState, ICinemachineCamera> stateToCameraMap;

    void Start()
    {
        InitializeCameraMap();
        // 确保游戏开始时相机状态正确设置
        SetCameraState(currentState);
    }

    void InitializeCameraMap()
    {
        stateToCameraMap = new Dictionary<Enums.PlayerCameraState, ICinemachineCamera>
        {
            { Enums.PlayerCameraState.Normal, normalVirtualCamera },
            { Enums.PlayerCameraState.FreeLook, freeLookVirtualCamera },
            { Enums.PlayerCameraState.Combat, combatVirtualCamera }
        };
    }

    public Vector3 CalculateDirection(Vector2 rawMove)
    {
        // 你原有的方向计算逻辑保持不变
        switch (currentState)
        {
            case Enums.PlayerCameraState.Normal:
                return CalculateNormalDirection(rawMove);
            case Enums.PlayerCameraState.FreeLook:
                return CalculateFreeDirection(rawMove);
            case Enums.PlayerCameraState.Combat:
                return CalculateLockDirection(rawMove);
            default:
                return Vector3.zero;
        }
    }

    Vector3 CalculateNormalDirection(Vector2 rawMove)
    {
        // 没有Cinemachine时返回零向量（角色保持当前朝向）
        if (GetActiveCamera() == null) return Vector3.zero;

        // 无输入时返回零向量（角色保持当前朝向）
        if (rawMove.sqrMagnitude <= 0.01f) return Vector3.zero;

        // 使用当前Cinemachine的方向，仅取水平面（Y 轴置零）
        Transform cameraTransform = GetActiveCamera().VirtualCameraGameObject.transform;
        Vector3 cameraDirect = transform.position - cameraTransform.position;
        cameraDirect.y = 0;
        cameraDirect.Normalize(); // 归一化确保各方向速度一致

        return cameraDirect;
    }

    Vector3 CalculateFreeDirection(Vector2 rawMove)
    {
        if (GetActiveCamera() == null) return Vector3.zero;

        if (rawMove.sqrMagnitude <= 0.01f) return Vector3.zero;

        Transform cameraTransform = GetActiveCamera().VirtualCameraGameObject.transform;
        Vector3 cameraDirect = transform.position - cameraTransform.position;
        cameraDirect.y = 0;
        cameraDirect.Normalize();

        // 获取摄像机右向量（始终是水平的）
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraDirect).normalized;
        // 组合移动方向：前向分量 + 右向分量
        Vector3 moveDirection = (cameraDirect * rawMove.y) + (cameraRight * rawMove.x);

        return moveDirection.normalized;
    }

    Vector3 CalculateLockDirection(Vector2 rawMove)
    {
        Transform targetTrans = actor.combater.CombatTarget.transform;
        if (targetTrans == null) return CalculateFreeDirection(rawMove);

        if (rawMove.sqrMagnitude <= 0.01f) return Vector3.zero;

        Vector3 faceDirect = targetTrans.position - transform.position ;
        faceDirect.y = 0;
        faceDirect.Normalize();

        return faceDirect;
    }

    public void SetCameraState(Enums.PlayerCameraState newState)
    {
        // 如果状态没有变化，则不需要做任何操作
        if (currentState == newState) return;

        // 更新当前状态
        currentState = newState;

        // 重置所有相机的优先级（设置为较低的基准值）
        ResetAllCameraPriorities();

        // 激活新状态对应的相机（设置高优先级）
        ActivateStateCamera(newState);
    }

    private void ResetAllCameraPriorities()
    {
        // 将所有虚拟相机的优先级设置为较低值（如0或10）
        // CinemachineBrain会自动选择优先级最高的相机作为活跃相机
        if (normalVirtualCamera != null)
            normalVirtualCamera.Priority = 10;

        if (freeLookVirtualCamera != null)
            freeLookVirtualCamera.Priority = 10;

        if (combatVirtualCamera != null)
            combatVirtualCamera.Priority = 10;
    }

    private void ActivateStateCamera(Enums.PlayerCameraState state)
    {
        ICinemachineCamera targetCamera;
        if (stateToCameraMap.TryGetValue(state, out targetCamera))
        {
            if (targetCamera is CinemachineVirtualCamera)
            {
                // 对于普通虚拟相机，设置高优先级（如20）
                (targetCamera as CinemachineVirtualCamera).Priority = 20;
            }
            else if (targetCamera is CinemachineFreeLook)
            {
                // 对于FreeLook相机同样设置优先级
                (targetCamera as CinemachineFreeLook).Priority = 20;
            }
        }
        else
        {
            Debug.LogWarning($"未找到状态 {state} 对应的虚拟相机配置！");
        }
    }

    // 通过代码动态切换状态的方法
    public void SwitchToNormalCamera() => SetCameraState(Enums.PlayerCameraState.Normal);
    public void SwitchToFreeLookCamera() => SetCameraState(Enums.PlayerCameraState.FreeLook);
    public void SwitchToCombatCamera() => SetCameraState(Enums.PlayerCameraState.Combat);

    // 获取当前活跃的虚拟相机
    public ICinemachineCamera GetActiveCamera()
    {
        if (stateToCameraMap.ContainsKey(currentState))
            return stateToCameraMap[currentState];
        return null;
    }
}

public partial class Enums
{
    public enum PlayerCameraState
    {
        Normal, FreeLook, Combat
    }
}
