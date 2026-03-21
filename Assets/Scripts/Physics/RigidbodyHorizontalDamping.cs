using UnityEngine;

namespace CombatSample.PhysicsInteraction
{
/// <summary>
/// 正式功能：可推动体受击/受挤后的水平滑停（不改竖直速度）。
/// 与 <see cref="CharacterControllerRigidbodyPush"/> 配对；真木桩用 Kinematic，不挂本组件。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RigidbodyHorizontalDamping : MonoBehaviour
{
    [SerializeField, Tooltip("越大停得越快，约 3～10；可与 Rigidbody.Drag 二选一或小幅叠加")]
    private float _horizontalDamping = 6f;

    [SerializeField, Tooltip("水平速度低于此 (m/s) 时直接归零，避免无限微滑")]
    private float _stopSpeedThreshold = 0.05f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (_rb == null || _rb.isKinematic)
            return;

        Vector3 v = _rb.velocity;
        float vy = v.y;
        Vector3 h = new Vector3(v.x, 0f, v.z);
        float mag = h.magnitude;
        if (mag < _stopSpeedThreshold)
        {
            if (mag > 0f)
                _rb.velocity = new Vector3(0f, vy, 0f);
            return;
        }

        // 近似指数衰减：每 Fixed 帧乘 (1 - k)，k 随 damping 增大
        float k = Mathf.Clamp01(_horizontalDamping * Time.fixedDeltaTime);
        h *= 1f - k;

        _rb.velocity = new Vector3(h.x, vy, h.z);
    }
}
}
