using System;
using UnityEngine;

namespace CombatSample.PhysicsInteraction
{
/// <summary>
/// [Obsolete] 此组件基于旧 CharacterController.OnControllerColliderHit。
/// 后续改用 KCC 的 rigidbody interaction 或 hit callback 方案替代。
/// </summary>
[Obsolete("Use KCC rigidbody interaction or hit callback instead.")]
[RequireComponent(typeof(CharacterController))]
public class CharacterControllerRigidbodyPush : MonoBehaviour
{
    [SerializeField, Tooltip("Only push rigidbodies on these layers. Example: Enemy only.")]
    private LayerMask _pushableLayers = ~0;

    [SerializeField, Tooltip("Push = (moveLength/dt) x this. Walk bump should be much smaller than sprint hit.")]
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
        float trySpeed = hit.moveLength / dt;

        if (trySpeed < _minTrySpeedToPush)
            return;

        float impulse = trySpeed * _pushGain + _blockedNudge;
        impulse = Mathf.Min(impulse, _maxImpulsePerHit);

        body.AddForce(pushDir * impulse, ForceMode.VelocityChange);
    }
}
}
