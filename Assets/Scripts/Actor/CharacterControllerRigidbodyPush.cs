using UnityEngine;

namespace CombatSample.PhysicsInteraction
{
/// <summary>
/// 正式功能：玩家 CharacterController 对「可推动」动态刚体的水平挤开。
/// 挂在与 <see cref="CharacterController"/> 同一物体（如 ActorRoot）。
/// 配置推挤层、与 <see cref="RigidbodyHorizontalDamping"/> 配合用于小怪/木桩分级。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterControllerRigidbodyPush : MonoBehaviour
{
    [SerializeField, Tooltip("仅推动这些 Layer 上的刚体（例如只勾 Enemy）；不勾则推不到")]
    private LayerMask _pushableLayers = ~0;

    [SerializeField, Tooltip("冲量 = (moveLength/dt) × 该系数。走路轻碰应远小于跑步猛撞")]
    private float _pushGain = 0.35f;

    [SerializeField, Tooltip("单次回调最大速度增量 (m/s)，防止数值叠爆")]
    private float _maxImpulsePerHit = 0.85f;

    [SerializeField, Tooltip("低于此「试图靠近速度」视为毛刷，不推（减少刚碰到就滑飞）")]
    private float _minTrySpeedToPush = 0.25f;

    [SerializeField, Tooltip("仅在 trySpeed 超过最小阈值后，可选再加一小点底力（一般保持很小）")]
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
