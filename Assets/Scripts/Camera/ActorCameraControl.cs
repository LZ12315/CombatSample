using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using Cinemachine;

public class ActorCameraControl : MonoBehaviour
{
    public CinemachineBrain cinemachineBrain;

    private ICinemachineCamera activeVirtualCamera;
    [SerializeField] private Enums.PlayerCameraState state;
    [SerializeField] private Transform targetTrans;

    public Vector3 CalculateDirection(Vector2 rawMove)
    {
        switch (state)
        {
            case Enums.PlayerCameraState.Normal:
                return CalculateFaceDirection(rawMove);
            case Enums.PlayerCameraState.Concentrate:
                return CalculateMovementDirection(rawMove);
            case Enums.PlayerCameraState.Combat:
                return CalculateEnemyDirection(rawMove);
            default:
                return Vector3.zero;
        }
    }

    Vector3 CalculateFaceDirection(Vector2 rawMove)
    {
        activeVirtualCamera = cinemachineBrain?.ActiveVirtualCamera;
        if (activeVirtualCamera == null) return Vector3.zero;

        // 无输入时返回零向量（角色保持当前朝向）
        if (rawMove.sqrMagnitude > 0.01f)
            return Vector3.zero;

        // 使用当前Cinemachine的方向，仅取水平面（Y 轴置零）
        Transform cameraTransform = activeVirtualCamera.VirtualCameraGameObject.transform;
        Vector3 cameraDirect = transform.position - cameraTransform.position;
        cameraDirect.y = 0;
        cameraDirect.Normalize();
        return cameraDirect;
    }

    Vector3 CalculateMovementDirection(Vector2 rawMove)
    {
        activeVirtualCamera = cinemachineBrain?.ActiveVirtualCamera;
        if (activeVirtualCamera == null) return Vector3.zero;

        Transform cameraTransform = activeVirtualCamera.VirtualCameraGameObject.transform;
        Vector3 cameraDirect = transform.position - cameraTransform.position;
        cameraDirect.y = 0;
        cameraDirect.Normalize();

        // 获取摄像机右向量（始终是水平的）
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraDirect).normalized;
        // 组合移动方向：前向分量 + 右向分量
        Vector3 moveDirection = (cameraDirect * rawMove.y) + (cameraRight * rawMove.x);

        return moveDirection.normalized; // 归一化确保各方向速度一致
    }

    Vector3 CalculateEnemyDirection(Vector2 rawMove)
    {
        if (targetTrans == null) return Vector3.zero;

        Vector3 faceDirect = targetTrans.position - transform.position ;
        faceDirect.y = 0;
        faceDirect.Normalize();

        return faceDirect;
    }

    public void SetCameraState(Enums.PlayerCameraState state)
    {
        this.state = state;

        switch (state) 
        {
            case Enums.PlayerCameraState.Normal:
                break;
            case Enums.PlayerCameraState.Concentrate:
                break;
        }
    }

}

public partial class Enums
{
    public enum PlayerCameraState
    {
        Normal, Concentrate, Combat
    }
}
