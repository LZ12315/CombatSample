using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using CombatSample.Consts;

public class ActorCameraControl : MonoBehaviour
{
    public Actor actor;
    public CinemachineBrain cinemachineBrain;

    [Header("相机配置")]
    // 1. 普通探索相机 (FreeLook)
    public CinemachineFreeLook normalFreeLookCamera;

    // 2. 软锁定相机 (VirtualCamera - 视角锁定敌人，但角色可自由转身)
    public CinemachineVirtualCamera softLockCamera;

    // 3. 硬锁定相机 (VirtualCamera - 视角锁定敌人，角色强制面朝敌人)
    public CinemachineVirtualCamera hardLockCamera;

    [Header("相机属性")]
    [SerializeField] private Enums.PlayerCameraState currentState;

    // 缓存接口
    private Dictionary<Enums.PlayerCameraState, ICinemachineCamera> stateToCameraMap;

    public Enums.PlayerCameraState CinemachineState
    {
        get => currentState;
        set => SetCameraState(value);
    }

    void Start()
    {
        InitializeCameraMap();
        SetCameraState(currentState, true);
    }

    void InitializeCameraMap()
    {
        // 映射枚举到具体的相机实例
        stateToCameraMap = new Dictionary<Enums.PlayerCameraState, ICinemachineCamera>
        {
            { Enums.PlayerCameraState.Free, normalFreeLookCamera },
            { Enums.PlayerCameraState.SoftLock, softLockCamera }, // 这里对应你的“锁定自由相机”
            { Enums.PlayerCameraState.HardLock, hardLockCamera }    // 这里对应你的“RB键硬锁定”
        };
    }

    public void SetCameraState(Enums.PlayerCameraState newState, bool forceUpdate = false)
    {
        if (currentState == newState && !forceUpdate) return;

        // 安全检查：如果没有目标，强制回退到普通视角
        //if ((newState == Enums.PlayerCameraState.SoftLock || newState == Enums.PlayerCameraState.HardLock)
        //    && actor.combater.CombatTarget == null)
        //{
        //    newState = Enums.PlayerCameraState.Free;
        //}

        currentState = newState;

        // 设置优先级：激活的设为20，其他的设为10
        foreach (var kvp in stateToCameraMap)
        {
            if (kvp.Value == null) continue;

            // 处理不同类型的相机基类
            var camBase = kvp.Value as CinemachineVirtualCameraBase;
            if (camBase != null)
            {
                camBase.Priority = (kvp.Key == newState) ? 20 : 10;
            }
        }
    }

    /// <summary>
    /// 计算基于相机的移动方向
    /// </summary>
    public Vector3 CalculateDirection(Vector2 rawMove)
    {
        if (rawMove.sqrMagnitude <= 0.01f) return Vector3.zero;

        // 获取当前主摄像机（Brain）的Transform，这样比去查各个虚拟相机更准确且通用
        Transform cameraTransform = Camera.main.transform;

        // 计算前方和右方（展平Y轴）
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        // 无论哪种相机模式，移动输入总是基于当前屏幕视角的
        // 向推摇杆=向屏幕里跑，向右推=向屏幕右跑
        Vector3 moveDirection = (cameraForward * rawMove.y) + (cameraRight * rawMove.x);

        return moveDirection.normalized;
    }
}

public partial class Enums
{
    public enum PlayerCameraState
    {
        Free, SoftLock, HardLock
    }
}
