using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>
    /// 按世界方向或世界目标点写入 <see cref="LocomotionIntent"/>；不读取 CombatTarget。
    /// </summary>
    [Name("Set Locomotion Intent")]
    [Category("Custom/Combat")]
    public class SetLocomotionIntentTask : ActionTask
    {
        public enum IntentSource
        {
            WorldDirection,
            WorldPoint,
        }

        public enum RunMode
        {
            PushOnce,
            RunUntilPointReached,
        }

        [Header("Settings")]
        public BBParameter<Actor> actor;
        public IntentSource source = IntentSource.WorldDirection;
        public BBParameter<Vector3> worldDirection;
        public BBParameter<Vector3> worldPoint;
        public RunMode runMode = RunMode.PushOnce;
        public BBParameter<float> stopDistance = 0.5f;
        public BBParameter<float> moveStrength = 1f;
        public BBParameter<bool> faceMoveDirection = true;
        public BBParameter<bool> useTimeout = false;
        public BBParameter<float> timeout = 2f;

        private float _startTime;

        protected override void OnExecute()
        {
            _startTime = Time.time;
            Tick();

            if (runMode == RunMode.PushOnce && isRunning)
                EndAction(true);
        }

        protected override void OnUpdate()
        {
            if (runMode == RunMode.RunUntilPointReached)
                Tick();
        }

        private void Tick()
        {
            var actorValue = actor?.value;
            if (actorValue?.actorMotor == null)
            {
                EndAction(false);
                return;
            }

            if (runMode == RunMode.RunUntilPointReached &&
                useTimeout != null && useTimeout.value &&
                timeout != null &&
                Time.time - _startTime >= timeout.value)
            {
                PushIdle(actorValue);
                EndAction(false);
                return;
            }

            if (runMode == RunMode.RunUntilPointReached && source == IntentSource.WorldPoint)
            {
                float stop = stopDistance != null ? stopDistance.value : 0.5f;
                if (TryGetHorizontalDistanceToPoint(actorValue, out float dist) && dist <= stop)
                {
                    PushIdle(actorValue);
                    EndAction(true);
                    return;
                }
            }

            Vector3 dir = ComputeMoveDirection(actorValue);
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f)
            {
                PushIdle(actorValue);
                if (runMode == RunMode.PushOnce)
                    EndAction(true);
                else if (runMode == RunMode.RunUntilPointReached && source == IntentSource.WorldDirection)
                    EndAction(false);
                return;
            }

            dir.Normalize();

            float strength = Mathf.Clamp01(moveStrength != null ? moveStrength.value : 1f);
            bool faceMove = faceMoveDirection == null || faceMoveDirection.value;

            actorValue.actorMotor.SetLocomotionIntent(new LocomotionIntent
            {
                WorldMoveDirection = dir,
                MoveStrength = strength,
                FacingDirection = faceMove ? dir : Vector3.zero,
            });
        }

        private bool TryGetHorizontalDistanceToPoint(Actor actorValue, out float distance)
        {
            distance = 0f;
            if (worldPoint == null)
                return false;

            Vector3 delta = worldPoint.value - actorValue.transform.position;
            delta.y = 0f;
            distance = delta.magnitude;
            return true;
        }

        private Vector3 ComputeMoveDirection(Actor actorValue)
        {
            if (source == IntentSource.WorldDirection)
                return worldDirection != null ? worldDirection.value : Vector3.zero;

            if (worldPoint == null)
                return Vector3.zero;

            Vector3 delta = worldPoint.value - actorValue.transform.position;
            return delta;
        }

        private static void PushIdle(Actor actorValue)
        {
            if (actorValue?.actorMotor == null)
                return;
            actorValue.actorMotor.SetLocomotionIntent(LocomotionIntent.Idle);
        }
    }
}
