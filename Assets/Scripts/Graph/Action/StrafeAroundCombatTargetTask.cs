using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>Faces the explicitly configured Actor target while orbiting within a preferred distance band.</summary>
    [Name("Strafe Around Target")]
    [Category("Custom/Combat")]
    [Description("围绕显式配置的 Actor 目标侧向移动，并在距离带外逐步修正距离。")]
    public class StrafeAroundCombatTargetTask : ActionTask<Actor>
    {
        public enum StrafeSide
        {
            Left,
            Right,
            Random,
        }

        [Tooltip("绕行的目标 Actor。为空时节点失败，不会回退到 CombatTarget。")]
        public BBParameter<Actor> target;

        [Tooltip("侧向在一次执行开始时选定；Random 运行中不会改变方向。")]
        public StrafeSide side = StrafeSide.Random;
        [MinValue(0.01f), Tooltip("期望的与目标水平距离，单位为世界单位。")]
        public float preferredDistance = 3f;
        [MinValue(0f), Tooltip("允许的距离偏差；超出后才开始渐进修正。")]
        public float distanceTolerance = 0.5f;
        [SliderField(0f, 1f), Tooltip("移动输入强度，不是世界单位速度。")]
        public float moveStrength = 0.4f;
        [MinValue(0f), Tooltip("持续时长（秒）；到时成功结束，暂停不会延长截止时间。")]
        public float duration = 1.5f;

        private bool _moveRight;
        private float _startTime;
        private ActorLocomotion _continuousLocomotion;

        protected override void OnExecute()
        {
            ReleaseContinuousIntent();
            switch (side)
            {
                case StrafeSide.Left:
                    _moveRight = false;
                    break;
                case StrafeSide.Right:
                    _moveRight = true;
                    break;
                case StrafeSide.Random:
                    _moveRight = UnityEngine.Random.value < 0.5f;
                    break;
                default:
                    EndAction(false);
                    return;
            }

            _startTime = Time.time;
            TickMove();
        }

        protected override void OnUpdate() => TickMove();
        protected override void OnStop(bool interrupted) => ReleaseContinuousIntent();
        protected override void OnPause() => ReleaseContinuousIntent();
        protected override void OnResume() => TickMove();

        private void TickMove()
        {
            var actorValue = agent;
            var targetValue = target?.value;
            var locomotion = actorValue != null ? actorValue.actorLocomotion : null;
            if (actorValue == null || !actorValue.isActiveAndEnabled
                || locomotion == null || !locomotion.isActiveAndEnabled
                || !LocomotionDataValidation.IsFinite(duration) || duration < 0f
                || targetValue == null || !targetValue.gameObject.activeInHierarchy
                || !CombatStrafeMovement.TryBuildIntent(actorValue.transform.position, targetValue.transform.position,
                    _moveRight, preferredDistance, distanceTolerance, moveStrength, out LocomotionIntent intent))
            {
                EndAction(false);
                return;
            }

            if (Time.time - _startTime >= duration)
            {
                EndAction(true);
                return;
            }

            if (_continuousLocomotion != locomotion)
                ReleaseContinuousIntent();
            _continuousLocomotion = locomotion;
            _continuousLocomotion.SetContinuousLocomotionIntent(intent);
        }

        private void ReleaseContinuousIntent()
        {
            if (_continuousLocomotion != null)
                _continuousLocomotion.ReleaseContinuousLocomotionIntent();
            _continuousLocomotion = null;
        }
    }

    /// <summary>Pure horizontal steering: tangent motion with bounded, continuous distance correction.</summary>
    internal static class CombatStrafeMovement
    {
        internal static bool TryBuildIntent(Vector3 position, Vector3 targetPosition, bool moveRight,
            float preferredDistance, float distanceTolerance, float moveStrength, out LocomotionIntent intent)
        {
            intent = LocomotionIntent.Idle;
            if (!LocomotionDataValidation.IsFinite(position) || !LocomotionDataValidation.IsFinite(targetPosition)
                || !LocomotionDataValidation.IsFinite(preferredDistance) || preferredDistance <= 0f
                || !LocomotionDataValidation.IsFinite(distanceTolerance) || distanceTolerance < 0f
                || !LocomotionDataValidation.IsFinite(moveStrength) || moveStrength < 0f || moveStrength > 1f)
                return false;

            Vector3 toTarget = targetPosition - position;
            toTarget.y = 0f;
            float distanceSquared = toTarget.sqrMagnitude;
            // A coincident horizontal position has no stable facing or orbit tangent.
            if (!LocomotionDataValidation.IsFinite(distanceSquared) || distanceSquared < 0.0001f)
                return false;

            float distance = Mathf.Sqrt(distanceSquared);
            Vector3 facing = toTarget / distance;
            Vector3 tangent = Vector3.Cross(Vector3.up, facing) * (moveRight ? 1f : -1f);
            float error = distance - preferredDistance;
            float excess = Mathf.Max(0f, Mathf.Abs(error) - distanceTolerance);
            // Grow smoothly outside the band; at most 45 degrees toward/away from the target.
            // Preferred distance provides the correction scale without another tuning parameter.
            float correction = Mathf.Clamp01(excess / preferredDistance) * (error < 0f ? -1f : 1f);
            Vector3 direction = (tangent + facing * correction).normalized;

            intent = new LocomotionIntent
            {
                WorldMoveDirection = direction,
                MoveStrength = moveStrength,
                FacingDirection = facing,
            };
            return true;
        }
    }
}
