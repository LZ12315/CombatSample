using System;
using UnityEngine;
using UnityEngine.Playables;

[Obsolete("配合旧 ActionMagnetismClip 使用；请改用 ActionMagnetismV2Behavior。")]
public class ActionMagnetismBehavior : ActionBehaviourBase
{
    public ActionMagnetismClip.MagnetismMode mode;
    public bool useCombatTarget;
    public Transform customTarget;
    public float maxDistance;
    public float magnetSpeed;
    public bool rotateToTarget;
    public float rotateSpeed;
    public bool debugLog;

    private Transform _targetTransform;
    private bool _hasCalculatedOffset;

    // 用于 Instant 模式下“瞬移到目标点导致 direction 变 0”的兜底
    private Vector3 _lastHorizontalDirection = Vector3.forward;
    private bool _hasLastHorizontalDirection;

    private bool _loggedWhyFailedOnce;

    protected override void OnClipStart(Playable playable)
    {
        if (actor == null) return;

        // 确定目标
        _targetTransform = GetTargetTransform();
        if (_targetTransform == null) return;

        // 计算初始偏移量（只在水平面上做距离/朝向判断）
        Vector3 toTarget = _targetTransform.position - actor.transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        
        // 检查距离限制
        if (maxDistance > 0f && distance > maxDistance)
        {
            if (debugLog && !_loggedWhyFailedOnce)
            {
                Debug.Log(
                    $"[Magnetism][{mode}] clip distance too far, distance={distance:F3}, maxDistance={maxDistance:F3}, actor={(actor ? actor.name : "null")}, movement={(actor?.movement ? actor.movement.name : "null")}"
                );
                _loggedWhyFailedOnce = true;
            }
            return;
        }

        _hasCalculatedOffset = true;
        _loggedWhyFailedOnce = false;

        if (toTarget.sqrMagnitude > 0.000001f)
        {
            _lastHorizontalDirection = toTarget.normalized;
            _hasLastHorizontalDirection = true;
        }
        else
        {
            _hasLastHorizontalDirection = false;
        }

        if (debugLog)
        {
            Debug.Log(
                $"[Magnetism][OnClipStart] mode={mode}, rotateToTarget={rotateToTarget}, rotateSpeed={rotateSpeed}, actor={(actor ? actor.name : "null")}, movement={(actor?.movement ? actor.movement.name : "null")}, movementEnabled={(actor?.movement ? actor.movement.isActiveAndEnabled : false)}, target={( _targetTransform ? _targetTransform.name : "null")}, initialDistance={distance:F3}"
            );
        }

        // 旋转速度覆盖：让 ActorMovement 按 Magnetism 的 rotateSpeed 平滑转向
        if (rotateToTarget && actor.movement != null)
        {
            if (rotateSpeed > 0f)
                actor.movement.SetRotationSpeedOverride(rotateSpeed);
            else
                actor.movement.SetRotationSpeedOverride(-1f);
        }

        // Instant：先算/先转，再瞬移位移，避免“瞬移后 direction=0 导致不再旋转”的问题
        if (rotateToTarget && _hasLastHorizontalDirection)
            RotateToTarget(); // 用上面记录的 last direction

        if (mode == ActionMagnetismClip.MagnetismMode.Instant)
            ExecuteInstantMagnetism(playable);
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (actor == null || _targetTransform == null) return;

        // Continuous：每帧移动
        if (mode == ActionMagnetismClip.MagnetismMode.Continuous)
            ExecuteContinuousMagnetism(info.deltaTime);

        // Instant/Continuous：都持续更新朝向（Instant 用 cached direction 兜底）
        if (rotateToTarget)
            RotateToTarget();
    }

    protected override void OnClipStop(bool isNormal)
    {
        _targetTransform = null;
        _hasCalculatedOffset = false;
        _hasLastHorizontalDirection = false;

        // 清理旋转速度覆盖，避免影响后续移动系统
        if (actor?.movement != null)
            actor.movement.SetRotationSpeedOverride(-1f);
    }

    private Transform GetTargetTransform()
    {
        if (useCombatTarget)
        {
            return actor.combater?.CombatTarget?.transform;
        }
        return customTarget;
    }

    private void ExecuteInstantMagnetism(Playable playable)
    {
        if (!_hasCalculatedOffset || _targetTransform == null) return;

        Vector3 targetPos = _targetTransform.position;
        Vector3 currentPos = actor.transform.position;

        // 计算最终位置
        Vector3 direction = targetPos - currentPos;
        direction.y = 0f;
        float distance = direction.magnitude;

        // 距离检查
        if (maxDistance > 0f && distance > maxDistance) return;
        if (direction.sqrMagnitude < 0.000001f) return;

        // 移动距离 = min(距离, 吸附速度)
        float moveDistance = Mathf.Min(distance, magnetSpeed);
        Vector3 newPosition = currentPos + direction.normalized * moveDistance;

        // 应用移动 (通过 CharacterController)
        if (actor.characterController != null)
        {
            actor.characterController.enabled = false;
            actor.transform.position = newPosition;
            actor.characterController.enabled = true;
        }
        else
        {
            actor.transform.position = newPosition;
        }
    }

    private void ExecuteContinuousMagnetism(float deltaTime)
    {
        if (_targetTransform == null) return;

        Vector3 currentPos = actor.transform.position;
        Vector3 targetPos = _targetTransform.position;
        Vector3 direction = targetPos - currentPos;
        direction.y = 0; // 保持水平

        float distance = direction.magnitude;

        // 距离检查
        if (maxDistance > 0f && distance > maxDistance) return;

        if (distance > 0.01f)
        {
            // 每帧移动
            float moveDistance = magnetSpeed * deltaTime;
            Vector3 newPosition = currentPos + direction.normalized * Mathf.Min(distance, moveDistance);

            // 应用移动
            if (actor.characterController != null)
            {
                actor.characterController.enabled = false;
                actor.transform.position = newPosition;
                actor.characterController.enabled = true;
            }
            else
            {
                actor.transform.position = newPosition;
            }
        }

        // 旋转朝向由 OnClipUpdate 统一调用
    }

    private void RotateToTarget()
    {
        if (_targetTransform == null || actor == null) return;

        Vector3 direction = _targetTransform.position - actor.transform.position;
        direction.y = 0f;

        // direction 可能在 Instant 瞬移后变 0：用 lastHorizontalDirection 兜底继续转向
        if (direction.sqrMagnitude > 0.000001f)
        {
            _lastHorizontalDirection = direction.normalized;
            _hasLastHorizontalDirection = true;
        }
        else
        {
            if (!_hasLastHorizontalDirection)
            {
                if (debugLog && !_loggedWhyFailedOnce)
                {
                    Debug.Log(
                        $"[Magnetism][RotateToTarget] direction too small and no cached direction, actor={actor.name}, target={_targetTransform.name}"
                    );
                    _loggedWhyFailedOnce = true;
                }
                return;
            }
            direction = _lastHorizontalDirection;
        }

        // 交给 ActorMovement：由它在 Update() 里统一做“平滑旋转”
        if (actor.movement != null)
        {
            if (rotateSpeed <= 0f)
            {
                actor.movement.SetRotationInstant(direction);
            }
            else
            {
                actor.movement.SetRotationSpeedOverride(rotateSpeed);
                actor.movement.UpdateRotation(direction);
            }
        }
        else
        {
            // fallback：如果 movement 不存在则直接旋转 transform（避免磁力完全失效）
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            if (rotateSpeed <= 0f)
                actor.transform.rotation = targetRotation;
            else
                actor.transform.rotation = Quaternion.RotateTowards(actor.transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        }
    }
}
