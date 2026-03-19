using UnityEngine;
using UnityEngine.Playables;

public class ActionMagnetismBehavior : ActionBehaviourBase
{
    public ActionMagnetismClip.MagnetismMode mode;
    public bool useCombatTarget;
    public Transform customTarget;
    public float maxDistance;
    public float magnetSpeed;
    public bool rotateToTarget;
    public float rotateSpeed;

    private Transform _targetTransform;
    private Vector3 _initialOffset;
    private bool _hasCalculatedOffset;

    protected override void OnClipStart(Playable playable)
    {
        if (actor == null) return;

        // 确定目标
        _targetTransform = GetTargetTransform();
        if (_targetTransform == null) return;

        // 计算初始偏移量 (Instant模式用)
        Vector3 direction = _targetTransform.position - actor.transform.position;
        float distance = direction.magnitude;
        
        // 检查距离限制
        if (maxDistance > 0f && distance > maxDistance) return;

        _initialOffset = direction;
        _hasCalculatedOffset = true;

        // 根据模式执行
        if (mode == ActionMagnetismClip.MagnetismMode.Instant)
        {
            ExecuteInstantMagnetism(playable);
        }
        else // Continuous
        {
            // Continuous 模式会在每帧 ProcessFrame 执行吸附
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (mode != ActionMagnetismClip.MagnetismMode.Continuous) return;
        if (actor == null || _targetTransform == null) return;

        // 持续吸附: 每帧向目标移动
        ExecuteContinuousMagnetism(info.deltaTime);
    }

    protected override void OnClipStop(bool isNormal)
    {
        _targetTransform = null;
        _hasCalculatedOffset = false;
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
        float distance = direction.magnitude;

        // 距离检查
        if (maxDistance > 0f && distance > maxDistance) return;

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

        // 旋转朝向
        if (rotateToTarget)
        {
            RotateToTarget();
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

        // 旋转朝向
        if (rotateToTarget)
        {
            RotateToTarget();
        }
    }

    private void RotateToTarget()
    {
        if (_targetTransform == null) return;

        Vector3 direction = _targetTransform.position - actor.transform.position;
        direction.y = 0;

        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        if (rotateSpeed <= 0f)
        {
            // 瞬转
            actor.movement?.UpdateRotation(direction.normalized);
        }
        else
        {
            // 插值旋转
            Quaternion currentRotation = actor.transform.rotation;
            float angle = Quaternion.Angle(currentRotation, targetRotation);
            if (angle > 0.01f)
            {
                float t = rotateSpeed * Time.deltaTime / angle;
                t = Mathf.Clamp01(t);
                Quaternion newRotation = Quaternion.Slerp(currentRotation, targetRotation, t);
                actor.movement?.UpdateRotation(newRotation * Vector3.forward);
            }
        }
    }
}
