using UnityEngine;

namespace CombatSample.Magnetism
{
    /// <summary>
    /// 单段 Timeline Magnetism Clip：根节点 ↔ 敌人胶囊表面间隙带（双向）+ 旋转。
    /// 非 Mono，由 ActionMagnetismV2Behavior 创建/驱动。
    /// </summary>
    public sealed class ActionMagnetismSession
    {
        private readonly Actor _actor;
        private readonly Transform _targetTransform;
        private readonly MagnetismConfig _config;
        private readonly CapsuleCollider _enemyCapsule;

        private bool _didInstantMove;
        private Vector3 _cachedHorizontalDir = Vector3.forward;
        private bool _hasCachedHorizontalDir;

        public ActionMagnetismSession(Actor actor, Transform combatTarget, MagnetismConfig config)
        {
            _actor = actor;
            _targetTransform = combatTarget;
            _config = config;

            if (combatTarget != null)
            {
                _enemyCapsule = combatTarget.GetComponent<CapsuleCollider>()
                                ?? combatTarget.GetComponentInChildren<CapsuleCollider>(true);
            }
        }

        public void Begin()
        {
            _didInstantMove = false;
            _hasCachedHorizontalDir = false;
            _actor?.movement?.SetRotationSpeedOverride(-1f);
        }

        public void Tick(float deltaTime)
        {
            if (_targetTransform == null || _actor == null || _config == null) return;

            if (_enemyCapsule == null)
            {
                if (_config.debugLog)
                    Debug.Log("[MagnetismV2] Skip: no CapsuleCollider on combat target.");
                return;
            }

            Vector3 toTarget = _targetTransform.position - _actor.transform.position;
            toTarget.y = 0f;
            float horizontalDistance = toTarget.magnitude;

            if (_config.maxDistance > 0f && horizontalDistance > _config.maxDistance)
            {
                if (_config.debugLog)
                    Debug.Log(
                        $"[MagnetismV2] Skip: horizontalDistance={horizontalDistance:F3} > maxDistance={_config.maxDistance:F3}");
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

            Vector3 root = _actor.transform.position;

            if (!MagnetismCapsuleGeometry.TryGetClearanceInfo(
                    root, _enemyCapsule, out _, out Vector3 nWorld, out float gap))
                return;

            Vector3 nH = nWorld;
            nH.y = 0f;
            if (nH.sqrMagnitude > 1e-8f)
                nH.Normalize();
            else
                nH = Vector3.zero;

            float ideal = _config.idealSurfaceGap;
            float dead = Mathf.Max(_config.gapDeadZone, 0f);
            float low = ideal - dead;
            float high = ideal + dead;

            switch (_config.approachMode)
            {
                case MagnetismApproachMode.InstantMove:
                    if (!_didInstantMove)
                    {
                        float err = gap - ideal;
                        if (Mathf.Abs(err) > dead)
                        {
                            Vector3 deltaH;
                            if (nH.sqrMagnitude > 1e-8f)
                                deltaH = -nH * err;
                            else
                                deltaH = dir * err;

                            ApplyMoveHorizontalDelta(deltaH);
                        }

                        _didInstantMove = true;
                    }

                    break;

                case MagnetismApproachMode.SpeedMove:
                {
                    Vector3 geomDelta = MagnetismCapsuleGeometry.ComputeRootDeltaForGapBand(
                        root,
                        _enemyCapsule,
                        ideal,
                        dead,
                        _config.pullSpeed,
                        _config.pushSpeed,
                        deltaTime,
                        horizontalOnly: true,
                        maxStepMagnitude: 0f);

                    if (geomDelta.sqrMagnitude < 1e-12f && (gap > high || gap < low))
                    {
                        if (gap > high)
                        {
                            float step = Mathf.Min(gap - high, _config.pullSpeed * deltaTime);
                            geomDelta = dir * step;
                        }
                        else
                        {
                            float step = Mathf.Min(low - gap, _config.pushSpeed * deltaTime);
                            geomDelta = -dir * step;
                        }
                    }

                    if (geomDelta.sqrMagnitude > 1e-12f)
                        ApplyMoveHorizontalDelta(geomDelta);
                    break;
                }
            }

            if (!_config.rotateToTarget || _config.rotationMode == MagnetismRotationMode.None) return;
            if (_actor.movement == null) return;

            Vector3 faceDir = dir;
            if (_config.rotationAxis == MagnetismRotationAxis.YawOnly)
                faceDir.y = 0f;

            if (_config.rotationMode == MagnetismRotationMode.InstantSnap || _config.rotationAngularSpeed <= 0f)
                _actor.movement.SetRotationInstant(faceDir);
            else
            {
                _actor.movement.SetRotationSpeedOverride(_config.rotationAngularSpeed);
                _actor.movement.UpdateRotation(faceDir);
            }
        }

        public void End()
        {
            _didInstantMove = false;
            _hasCachedHorizontalDir = false;
            _actor?.movement?.SetRotationSpeedOverride(-1f);
        }

        private void ApplyMoveHorizontalDelta(Vector3 deltaHorizontal)
        {
            deltaHorizontal.y = 0f;
            if (deltaHorizontal.sqrMagnitude < 1e-12f) return;

            Vector3 newPos = _actor.transform.position + deltaHorizontal;

            if (_actor.characterController != null)
            {
                _actor.characterController.enabled = false;
                _actor.transform.position = newPos;
                _actor.characterController.enabled = true;
            }
            else
                _actor.transform.position = newPos;
        }
    }
}
