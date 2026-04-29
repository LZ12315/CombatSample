using UnityEngine;

namespace CombatSample.PhysicsInteraction
{
/// <summary>
/// 正式功能：玩家 CharacterController 对「可推动」动态刚体的水平挤开。
/// 挂在与 <see cref="CharacterController"/> 同一物体（如 ActorRoot）。
/// 配置推挤层、与 <see cref="RigidbodyHorizontalDamping"/> 配合用于小怪/木桩分级。
/// KCC 阶段该组件处于停用状态：当角色主运动不再由 CharacterController.Move 驱动时，本组件不会产生推挤回调。
/// 后续若需要推刚体，请迁移到 KCC 的 rigidbody interaction 路径。
/// </summary>
public class CharacterControllerRigidbodyPush : MonoBehaviour
{
    [SerializeField, Tooltip("Only push rigidbodies on these layers. Example: Enemy only.")]
    private LayerMask _pushableLayers = ~0;

    [SerializeField, Tooltip("Push = (moveLength/dt) × this. Walk bump should be much smaller than sprint hit.")]
    private float _pushGain = 0.35f;

    [SerializeField, Tooltip("Max speed add per callback (m/s). Stops runaway values.")]
    private float _maxImpulsePerHit = 0.85f;

    [SerializeField, Tooltip("If approach speed is below this, skip push (less bump slide).")]
    private float _minTrySpeedToPush = 0.25f;

    [SerializeField, Tooltip("Optional tiny extra push after speed is over the min. Keep small.")]
    private float _blockedNudge = 0.08f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (((1 << hit.gameObject.layer) & _pushableLayers) == 0)
            return;

        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic)
            return;

        if (hit.moveDirection.y < -0.3f)
            return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (pushDir.sqrMagnitude < 1e-6f)
            return;

        pushDir.Normalize();

        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        // 本帧 Move 里「朝该碰撞体方向」想推进多少（米）→ 换算成等效速度
        float trySpeed = hit.moveLength / dt;

        if (trySpeed < _minTrySpeedToPush)
            return;

        float impulse = trySpeed * _pushGain + _blockedNudge;
        impulse = Mathf.Min(impulse, _maxImpulsePerHit);

        body.AddForce(pushDir * impulse, ForceMode.VelocityChange);
    }
}
}
