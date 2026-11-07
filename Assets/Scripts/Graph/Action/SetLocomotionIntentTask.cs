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
    [Description("按世界方向或世界点向自身写入 LocomotionIntent，不读取 CombatTarget。")]
    public class SetLocomotionIntentTask : ActionTask<Actor>
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

        [Header("Source")]
        [Tooltip("World Direction：按世界方向移动；World Point：朝世界点移动。")]
        public IntentSource source = IntentSource.WorldDirection;
        [ShowIf("source", 0), Tooltip("仅 World Direction：期望的世界移动方向。")]
        public BBParameter<Vector3> worldDirection;
        [ShowIf("source", 1), Tooltip("仅 World Point：期望到达的世界位置。")]
        public BBParameter<Vector3> worldPoint;

        [Tooltip("Push Once：输出一次意图即成功；Run Until Point Reached：每帧推进直到到达或超时。")]
        public RunMode runMode = RunMode.PushOnce;
        [ShowIf("stopDistanceApplies", 1), Tooltip("仅 Run Until Point Reached + World Point：水平距离进入该值即完成。")]
        public BBParameter<float> stopDistance = 0.5f;
        [Tooltip("移动输入强度，不是世界单位速度。")]
        public BBParameter<float> moveStrength = 1f;
        [Tooltip("移动时朝移动方向；关闭后不写入朝向。")]
        public BBParameter<bool> faceMoveDirection = true;

        [Header("Timeout")]
        [ShowIf("runMode", 1), Tooltip("仅 Run Until Point Reached：启用超时失败。")]
        public BBParameter<bool> useTimeout = false;
        [ShowIf("timeoutApplies", 1), Tooltip("超出该时间仍未完成则失败，使用受 Time Scale 影响的时间。")]
        public BBParameter<float> timeout = 2f;

        // Combined conditions for ShowIf; keep completion-only fields hidden when they cannot apply.
        private bool stopDistanceApplies => runMode == RunMode.RunUntilPointReached && source == IntentSource.WorldPoint;
        private bool timeoutApplies => runMode == RunMode.RunUntilPointReached && useTimeout != null && useTimeout.value;

        private float _startTime;
        private ActorLocomotion _continuousLocomotion;

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

        protected override void OnStop(bool interrupted) => ReleaseContinuousIntent();
        protected override void OnPause() => ReleaseContinuousIntent();
        protected override void OnResume() => Tick();

        private void ReleaseContinuousIntent()
        {
            _continuousLocomotion?.ReleaseContinuousLocomotionIntent();
            _continuousLocomotion = null;
        }

        private void Tick()
        {
            var actorValue = agent;
            if (actorValue?.actorLocomotion == null)
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

            var intent = new LocomotionIntent
            {
                WorldMoveDirection = dir,
                MoveStrength = strength,
                FacingDirection = faceMove ? dir : Vector3.zero,
            };
            if (runMode == RunMode.PushOnce)
                actorValue.actorLocomotion.SetLocomotionIntent(intent);
            else
            {
                if (_continuousLocomotion != actorValue.actorLocomotion)
                    ReleaseContinuousIntent();
                _continuousLocomotion = actorValue.actorLocomotion;
                _continuousLocomotion.SetContinuousLocomotionIntent(intent);
            }
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

        private void PushIdle(Actor actorValue)
        {
            ReleaseContinuousIntent();
            if (actorValue?.actorLocomotion == null)
                return;
            actorValue.actorLocomotion.SetLocomotionIntent(LocomotionIntent.Idle);
        }
    }
}
