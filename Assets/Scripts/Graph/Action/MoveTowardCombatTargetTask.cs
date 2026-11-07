using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>
    /// 向显式配置的 Actor 目标写入 <see cref="LocomotionIntent"/>，不读取 CombatTarget。
    /// PushOnce：执行一次即成功；MoveUntilDistance：每帧推进直到进入停止距离。
    /// </summary>
    [Name("Move Toward Target")]
    [Category("Custom/Combat")]
    [Description("朝显式配置的 Actor 目标输出移动意图，不读取 CombatTarget。")]
    public class MoveTowardCombatTargetTask : ActionTask<Actor>
    {
        public enum MoveMode
        {
            PushOnce,
            MoveUntilDistance,
        }

        [Tooltip("移动目标 Actor。为空时节点失败，不会回退到 CombatTarget。")]
        public BBParameter<Actor> target;

        [Tooltip("Push Once：执行一次即成功；Move Until Distance：持续推进直到进入停止距离。")]
        public MoveMode moveMode = MoveMode.PushOnce;
        [ShowIf("moveMode", 1), MinValue(0f)]
        [Tooltip("仅 Move Until Distance：水平距离进入该值后停下并成功。")]
        public float stopDistance = 2.5f;
        [SliderField(0f, 1f), Tooltip("移动输入强度，不是世界单位速度。")]
        public float moveStrength = 1f;
        [Tooltip("移动时朝向目标；关闭后只输出移动方向。")]
        public bool faceTarget = true;

        [Header("Timeout")]
        [ShowIf("moveMode", 1)]
        [Tooltip("仅 Move Until Distance：启用未到达时的超时失败。")]
        public bool useTimeout;
        [ShowIf("timeoutApplies", 1), MinValue(0f)]
        [Tooltip("超出该时间仍未到达则失败，使用受 Time Scale 影响的时间。")]
        public float timeout = 2f;

        // Combined condition for ShowIf; keeps Timeout fields hidden when they cannot apply.
        private bool timeoutApplies => useTimeout && moveMode == MoveMode.MoveUntilDistance;

        private float _startTime;
        private ActorLocomotion _continuousLocomotion;

        protected override void OnExecute()
        {
            _startTime = Time.time;
            TickMove();

            if (moveMode == MoveMode.PushOnce && isRunning)
                EndAction(true);
        }

        protected override void OnUpdate()
        {
            if (moveMode == MoveMode.MoveUntilDistance)
                TickMove();
        }

        protected override void OnStop(bool interrupted) => ReleaseContinuousIntent();
        protected override void OnPause() => ReleaseContinuousIntent();
        protected override void OnResume() => TickMove();

        private void ReleaseContinuousIntent()
        {
            _continuousLocomotion?.ReleaseContinuousLocomotionIntent();
            _continuousLocomotion = null;
        }

        private void TickMove()
        {
            var actorValue = agent;
            if (actorValue?.actorLocomotion == null)
            {
                EndAction(false);
                return;
            }

            var targetValue = target?.value;
            if (targetValue == null)
            {
                EndAction(false);
                return;
            }

            Transform targetTransform = targetValue.transform;
            if (useTimeout && Time.time - _startTime >= timeout)
            {
                PushIdle(actorValue, targetTransform);
                EndAction(false);
                return;
            }

            Vector3 toTarget = targetTransform.position - actorValue.transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (moveMode == MoveMode.MoveUntilDistance && distance <= stopDistance)
            {
                PushIdle(actorValue, targetTransform);
                EndAction(true);
                return;
            }

            if (toTarget.sqrMagnitude < 0.0001f)
            {
                PushIdle(actorValue, targetTransform);
                if (moveMode == MoveMode.PushOnce)
                    EndAction(true);
                return;
            }

            Vector3 dir = toTarget.normalized;
            var intent = new LocomotionIntent
            {
                WorldMoveDirection = dir,
                MoveStrength = Mathf.Clamp01(moveStrength),
                FacingDirection = faceTarget ? dir : Vector3.zero,
            };
            if (moveMode == MoveMode.PushOnce)
                actorValue.actorLocomotion.SetLocomotionIntent(intent);
            else
            {
                if (_continuousLocomotion != actorValue.actorLocomotion)
                    ReleaseContinuousIntent();
                _continuousLocomotion = actorValue.actorLocomotion;
                _continuousLocomotion.SetContinuousLocomotionIntent(intent);
            }
        }

        private void PushIdle(Actor actorValue, Transform target)
        {
            ReleaseContinuousIntent();
            if (actorValue?.actorLocomotion == null)
                return;

            Vector3 face = Vector3.zero;
            if (faceTarget && target != null)
            {
                face = target.position - actorValue.transform.position;
                face.y = 0f;
                if (face.sqrMagnitude > 0.0001f)
                    face.Normalize();
            }

            actorValue.actorLocomotion.SetLocomotionIntent(new LocomotionIntent
            {
                WorldMoveDirection = Vector3.zero,
                MoveStrength = 0f,
                FacingDirection = face,
            });
        }
    }
}
