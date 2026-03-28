using UnityEngine;

public class CameraTargetStabilizer : MonoBehaviour
{
    public Transform playerTarget;
    public float verticalSmoothTime = 0.3f;
    public float maxVerticalOffset = 0.2f;

    private Vector3 _currentVelocity;
    private float _baseHeight;

    void Start()
    {
        // 初始化虚拟目标位置
        transform.position = playerTarget.position;
        _baseHeight = playerTarget.position.y;
    }

    void LateUpdate()
    {
        // 获取角色当前位置
        Vector3 targetPos = playerTarget.position;

        // 计算目标Y位置（限制在基础高度附近）
        float targetY = Mathf.Clamp(
            targetPos.y,
            _baseHeight - maxVerticalOffset,
            _baseHeight + maxVerticalOffset
        );

        // 平滑过渡Y轴位置
        float newY = Mathf.SmoothDamp(
            transform.position.y,
            targetY,
            ref _currentVelocity.y,
            verticalSmoothTime
        );

        // 更新虚拟目标位置（XZ平面立即跟随）
        transform.position = new Vector3(
            targetPos.x,
            newY,
            targetPos.z
        );
    }
}
