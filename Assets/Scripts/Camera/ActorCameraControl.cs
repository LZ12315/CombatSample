using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using Cinemachine;

public class ActorCameraControl : MonoBehaviour
{
    //public CinemachineVirtualCamera personalFollowCamera;
    public CinemachineBrain cinemachineBrain;

    private ICinemachineCamera activeVirtualCamera;
    private Enums.PlayerCameraState state;

    //public void HandleCameraRotation(Vector2 rawLook)
    //{


        //[Header("相机设置")]
        //public bool invertVertical;
        //public float horizontalSpeed = 100f;
        //public float verticalSpeed = 30f;
        //public float inputDeadzone = 0.1f;
        //public float verticalAngleMin = -30f;
        //public float verticalAngleMax = 70f;
        //public float rotationSmoothing = 8f; // 指数平滑参数

        //// 相机旋转状态 //
        //private float _currentHorizontalAngle = 0;
        //private float _currentVerticalAngle = 0;
        //private Quaternion _targetRotation;
        //private Quaternion _previousRotation; // 上一帧旋转记录
        //private bool _isFirstRotation = true; // 首次旋转标志

        //// 输入状态 //
        //private Vector2 _rawLook = Vector2.zero;
        //private Vector2 _smoothedLookInput = Vector2.zero; // 输入平滑
        //public float inputSmoothing = 6f; // 输入平滑参数


    //    // 平滑输入
    //    _smoothedLookInput = Vector2.Lerp(
    //        _smoothedLookInput,
    //        rawLook,
    //        inputSmoothing * Time.deltaTime
    //    );

    //    // 检查输入是否超过死区阈值
    //    if (_smoothedLookInput.magnitude < inputDeadzone)
    //    {
    //        _isFirstRotation = true; // 重置首次旋转标志
    //        return;
    //    }

    //    // 计算旋转增量
    //    float horizontalDelta = _smoothedLookInput.x * horizontalSpeed * Time.deltaTime;
    //    float verticalDelta = _smoothedLookInput.y * verticalSpeed * Time.deltaTime;

    //    // 反转垂直轴（根据玩家设置）
    //    if (invertVertical) verticalDelta = -verticalDelta;

    //    // 更新水平旋转角度（Y轴）
    //    _currentHorizontalAngle += horizontalDelta;

    //    // 确保角度在0-360范围内
    //    if (_currentHorizontalAngle > 360) _currentHorizontalAngle -= 360;
    //    if (_currentHorizontalAngle < 0) _currentHorizontalAngle += 360;

    //    // 更新垂直旋转角度（X轴）并限制范围
    //    _currentVerticalAngle = Mathf.Clamp(
    //        _currentVerticalAngle - verticalDelta,
    //        verticalAngleMin,
    //        verticalAngleMax
    //    );

    //    // 创建目标旋转四元数
    //    _targetRotation = Quaternion.Euler(
    //        _currentVerticalAngle,
    //        _currentHorizontalAngle,
    //        0
    //    );

    //    // 应用旋转（特殊处理首次旋转）
    //    if (_isFirstRotation)
    //    {
    //        transform.rotation = _targetRotation;
    //        _previousRotation = _targetRotation;
    //        _isFirstRotation = false;
    //    }
    //    else
    //    {
    //        // 使用改进的平滑方法
    //        transform.rotation = SmoothRotation(_previousRotation, _targetRotation);
    //        _previousRotation = transform.rotation;
    //    }
    //}

    // 改进的旋转平滑方法

    public Vector3 CalculateMovementDirection(Vector2 rawMove)
    {
        if (transform == null) return Vector3.zero;

        // 使用相机枢轴点的方向
        Vector3 cameraForward = transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 cameraRight = transform.right;
        cameraRight.y = 0;
        cameraRight.Normalize();

        // 将输入方向转换为世界空间方向
        return (cameraForward * rawMove.y) + (cameraRight * rawMove.x);
    }

    public Vector3 CalculateFaceDirection(Vector2 rawMove)
    {
        activeVirtualCamera = cinemachineBrain?.ActiveVirtualCamera;
        if (activeVirtualCamera == null) return Vector3.zero;

        // 检查输入是否有效（即 rawMove 不为零向量）
        if (rawMove.sqrMagnitude > 0.01f)
        {
            // 使用相机枢轴点的方向，仅取水平面（Y 轴置零）
            Transform cameraTransform = activeVirtualCamera.VirtualCameraGameObject.transform;
            Vector3 cameraDirect = transform.position - cameraTransform.position;
            cameraDirect.y = 0;
            cameraDirect.Normalize();
            return cameraDirect;
        }

        // 无输入时返回零向量（角色保持当前朝向）
        return Vector3.zero;
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
