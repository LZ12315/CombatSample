using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>Faces the combat target while orbiting within a preferred distance band.</summary>
    [Name("Strafe Around Combat Target")]
    [Category("Custom/Combat")]
    public class StrafeAroundCombatTargetTask : ActionTask
    {
        public enum StrafeSide
        {
            Left,
            Right,
            Random,
        }

        [Header("References")]
        public BBParameter<Actor> actor;
        public BBParameter<Transform> targetOverride;

        [Header("Strafe")]
        [Tooltip("Side is selected once per execution. Random does not change direction while running.")]
        public StrafeSide side = StrafeSide.Random;
        [Min(0.01f), Tooltip("Preferred horizontal distance from the target, in world units.")]
        public float preferredDistance = 3f;
        [Min(0f), Tooltip("Allowed distance deviation before gradual inward or outward correction begins.")]
        public float distanceTolerance = 0.5f;
        [Range(0f, 1f), Tooltip("Locomotion input strength, not a speed in world units per second.")]
        public float moveStrength = 0.4f;
        [Min(0f), Tooltip("Scaled game-time duration. Completion reports success; pause does not extend the deadline.")]
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
            var actorValue = actor?.value;
            var locomotion = actorValue != null ? actorValue.actorLocomotion : null;
            if (actorValue == null || !actorValue.isActiveAndEnabled
                || locomotion == null || !locomotion.isActiveAndEnabled
                || !LocomotionDataValidation.IsFinite(duration) || duration < 0f
                || !NodeCanvasCombatTargetUtility.TryResolveTarget(actorValue, targetOverride, out Transform target)
                || !target.gameObject.activeInHierarchy
                || !CombatStrafeMovement.TryBuildIntent(actorValue.transform.position, target.position,
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
