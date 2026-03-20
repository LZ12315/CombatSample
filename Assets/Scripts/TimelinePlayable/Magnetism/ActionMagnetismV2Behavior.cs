using UnityEngine;
using UnityEngine.Playables;
using CombatSample.Magnetism;

namespace CombatSample.TimelinePlayable.Magnetism
{
    /// <summary>
    /// Timeline 执行层（V2）：计算与应用都直接在 Behaviour 内完成，
    /// 避免再为 Magnetism 引入额外的“组件/控制器对象”。
    /// </summary>
    public class ActionMagnetismV2Behavior : ActionBehaviourBase
    {
        public bool useCombatTarget;
        public Transform customTarget;
        public MagnetismConfig config;

        private Transform _targetTransform;
        private bool _didInstantMove;
        private Vector3 _cachedHorizontalDir = Vector3.forward;
        private bool _hasCachedHorizontalDir;

        protected override void OnClipStart(Playable playable)
        {
            if (actor == null) return;

            _targetTransform = useCombatTarget ? actor.combater?.CombatTarget?.transform : customTarget;
            if (_targetTransform == null) return;

            _didInstantMove = false;
            _hasCachedHorizontalDir = false;

            // 清空旋转速度覆盖（如果之前还有残留）
            actor?.movement?.SetRotationSpeedOverride(-1f);
        }

        protected override void OnClipUpdate(Playable playable, FrameData info)
        {
            if (_targetTransform == null || actor == null || config == null) return;

            float deltaTime = (float)info.deltaTime;

            // 1) 计算水平到目标方向
            Vector3 toTarget = _targetTransform.position - actor.transform.position;
            toTarget.y = 0f;
            float horizontalDistance = toTarget.magnitude;

            if (config.maxDistance > 0f && horizontalDistance > config.maxDistance)
            {
                if (config.debugLog)
                    Debug.Log($"[MagnetismV2] Skip: distance={horizontalDistance:F3} > maxDistance={config.maxDistance:F3}");
                return;
            }

            Vector3 dir;
            const float eps = 0.000001f;
            if (horizontalDistance > eps)
            {
                dir = toTarget / horizontalDistance;
                _cachedHorizontalDir = dir;
                _hasCachedHorizontalDir = true;
            }
            else
            {
                if (!_hasCachedHorizontalDir) return;
                dir = _cachedHorizontalDir;
            }

            // 2) 方案一：固定贴身（desiredPos 永远保持离目标 attachDistance）
            float attachDistance = Mathf.Max(config.attachDistance, 0f);
            Vector3 targetPos = _targetTransform.position;
            // 保持 Y 不参与“贴身”判断（你的旋转/贴身都是水平面逻辑）
            targetPos.y = actor.transform.position.y;

            Vector3 desiredPos = targetPos - dir * attachDistance;

            // 如果已经比贴身距离更靠近，就不再把角色“推过目标”
            // （否则 InstantMove 可能瞬移到 desiredPos 的反侧）
            if (horizontalDistance <= attachDistance)
            {
                desiredPos = actor.transform.position;
            }

            // 3) 应用移动（方案由 approachMode 决定）
            switch (config.approachMode)
            {
                case MagnetismApproachMode.InstantMove:
                    if (!_didInstantMove)
                    {
                        ApplyMoveInstant(desiredPos);
                        _didInstantMove = true;
                    }
                    break;

                case MagnetismApproachMode.SpeedMove:
                    ApplyMoveTowards(desiredPos, deltaTime);
                    break;
            }

            // 4) 应用旋转（rotationMode 决定是瞬转还是角速度平滑）
            if (!config.rotateToTarget || config.rotationMode == MagnetismRotationMode.None) return;
            if (actor.movement == null) return;

            Vector3 faceDir = dir;
            if (config.rotationAxis == MagnetismRotationAxis.YawOnly)
                faceDir.y = 0f;

            if (config.rotationMode == MagnetismRotationMode.InstantSnap || config.rotationAngularSpeed <= 0f)
            {
                actor.movement.SetRotationInstant(faceDir);
            }
            else
            {
                actor.movement.SetRotationSpeedOverride(config.rotationAngularSpeed);
                actor.movement.UpdateRotation(faceDir);
            }
        }

        protected override void OnClipStop(bool isNormal)
        {
            _targetTransform = null;

            _didInstantMove = false;
            _hasCachedHorizontalDir = false;

            // 清空旋转速度覆盖
            actor?.movement?.SetRotationSpeedOverride(-1f);
        }

        private void ApplyMoveInstant(Vector3 desiredPos)
        {
            if (actor.characterController != null)
            {
                actor.characterController.enabled = false;
                actor.transform.position = desiredPos;
                actor.characterController.enabled = true;
            }
            else
            {
                actor.transform.position = desiredPos;
            }
        }

        private void ApplyMoveTowards(Vector3 desiredPos, float deltaTime)
        {
            if (deltaTime <= 0f) return;

            Vector3 currentPos = actor.transform.position;
            Vector3 delta = desiredPos - currentPos;
            delta.y = 0f; // 保持水平逼近

            float dist = delta.magnitude;
            if (dist <= 0.0001f) return;

            float step = Mathf.Min(dist, config.approachSpeed * deltaTime);
            Vector3 newPos = currentPos + delta / dist * step;

            if (actor.characterController != null)
            {
                actor.characterController.enabled = false;
                actor.transform.position = newPos;
                actor.characterController.enabled = true;
            }
            else
            {
                actor.transform.position = newPos;
            }
        }
    }
}

