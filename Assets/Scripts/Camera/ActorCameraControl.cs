using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using Cinemachine;

public class ActorCameraControl : MonoBehaviour
{
    public CinemachineVirtualCamera virtualCamera;

    [Header("相机设置")]
    public bool invertVertical;
    public float horizontalSpeed = 100f;
    public float verticalSpeed = 30f;
    public float inputDeadzone = 0.1f;
    public float verticalAngleMin = -30f;
    public float verticalAngleMax = 70f;
    public float rotationSmoothing = 8f; // 指数平滑参数

    // 相机旋转状态 //
    private float _currentHorizontalAngle = 0;
    private float _currentVerticalAngle = 0;
    private Quaternion _targetRotation;
    private Quaternion _previousRotation; // 上一帧旋转记录
    private bool _isFirstRotation = true; // 首次旋转标志

    // 输入状态 //
    private Vector2 _rawLook = Vector2.zero;
    private Vector2 _smoothedLookInput = Vector2.zero; // 输入平滑
    public float inputSmoothing = 6f; // 输入平滑参数

    private Enums.PlayerCameraState state;

    private void Start()
    {
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        // 记录初始旋转状态
        _previousRotation = transform.rotation;
        _isFirstRotation = true;

        // 初始化角度
        _currentHorizontalAngle = transform.eulerAngles.y;
        _currentVerticalAngle = NormalizeAngle(transform.eulerAngles.x);
    }

    private float NormalizeAngle(float angle)
    {
        // 将角度规范到-180~180范围
        angle %= 360;
        if (angle > 180) angle -= 360;
        return angle;
    }

    public void HandleCameraRotation(Vector2 rawLook)
    {
        // 平滑输入
        _smoothedLookInput = Vector2.Lerp(
            _smoothedLookInput,
            rawLook,
            inputSmoothing * Time.deltaTime
        );

        // 检查输入是否超过死区阈值
        if (_smoothedLookInput.magnitude < inputDeadzone)
        {
            _isFirstRotation = true; // 重置首次旋转标志
            return;
        }

        // 计算旋转增量
        float horizontalDelta = _smoothedLookInput.x * horizontalSpeed * Time.deltaTime;
        float verticalDelta = _smoothedLookInput.y * verticalSpeed * Time.deltaTime;

        // 反转垂直轴（根据玩家设置）
        if (invertVertical) verticalDelta = -verticalDelta;

        // 更新水平旋转角度（Y轴）
        _currentHorizontalAngle += horizontalDelta;

        // 确保角度在0-360范围内
        if (_currentHorizontalAngle > 360) _currentHorizontalAngle -= 360;
        if (_currentHorizontalAngle < 0) _currentHorizontalAngle += 360;

        // 更新垂直旋转角度（X轴）并限制范围
        _currentVerticalAngle = Mathf.Clamp(
            _currentVerticalAngle - verticalDelta,
            verticalAngleMin,
            verticalAngleMax
        );

        // 创建目标旋转四元数
        _targetRotation = Quaternion.Euler(
            _currentVerticalAngle,
            _currentHorizontalAngle,
            0
        );

        // 应用旋转（特殊处理首次旋转）
        if (_isFirstRotation)
        {
            transform.rotation = _targetRotation;
            _previousRotation = _targetRotation;
            _isFirstRotation = false;
        }
        else
        {
            // 使用改进的平滑方法
            transform.rotation = SmoothRotation(_previousRotation, _targetRotation);
            _previousRotation = transform.rotation;
        }
    }

    // 改进的旋转平滑方法
    private Quaternion SmoothRotation(Quaternion from, Quaternion to)
    {
        // 计算角度差异
        float angle = Quaternion.Angle(from, to);

        // 小角度时直接设置，避免反向插值
        if (angle < 5f) return to;

        // 使用指数平滑
        return Quaternion.Slerp(
            from,
            to,
            1 - Mathf.Exp(-rotationSmoothing * Time.deltaTime)
        );
    }

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

    public void SetCameraState(Enums.PlayerCameraState state)
    {
        this.state = state;

        switch (state) 
        {
            case Enums.PlayerCameraState.Normal:
                virtualCamera.m_Lens.FieldOfView = 50;
                break;
            case Enums.PlayerCameraState.Concentrate:
                virtualCamera.m_Lens.FieldOfView = 80;
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
