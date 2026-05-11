using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>
    /// 向战斗目标写入 <see cref="LocomotionIntent"/>。
    /// PushOnce：执行一次即成功；MoveUntilDistance：每帧推进直到进入停止距离。
    /// </summary>
    [Name("Move Toward Combat Target")]
    [Category("Custom/Combat")]
    public class MoveTowardCombatTargetTask : ActionTask
    {
        public enum MoveMode
        {
            PushOnce,
            MoveUntilDistance,
        }

        [Header("Settings")]
        public BBParameter<Actor> actor;
        public BBParameter<Transform> targetOverride;
        public MoveMode moveMode = MoveMode.PushOnce;
        public float stopDistance = 2.5f;
        [Range(0f, 1f)] public float moveStrength = 1f;
        public bool faceTarget = true;
        public bool useTimeout;
        public float timeout = 2f;

        private float _startTime;

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

        private void TickMove()
        {
            var actorValue = actor.value;
            if (!NodeCanvasCombatTargetUtility.TryResolveTarget(actorValue, targetOverride, out Transform target))
            {
                EndAction(false);
                return;
            }

            if (useTimeout && Time.time - _startTime >= timeout)
            {
                PushIdle(actorValue, target);
                EndAction(false);
                return;
            }

            Vector3 toTarget = target.position - actorValue.transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (moveMode == MoveMode.MoveUntilDistance && distance <= stopDistance)
            {
                PushIdle(actorValue, target);
                EndAction(true);
                return;
            }

            if (toTarget.sqrMagnitude < 0.0001f)
            {
                PushIdle(actorValue, target);
                if (moveMode == MoveMode.PushOnce)
                    EndAction(true);
                return;
            }

            Vector3 dir = toTarget.normalized;
            actorValue.movement.SetLocomotionIntent(new LocomotionIntent
            {
                WorldMoveDirection = dir,
                MoveStrength = Mathf.Clamp01(moveStrength),
                FacingDirection = faceTarget ? dir : Vector3.zero,
            });
        }

        private void PushIdle(Actor actorValue, Transform target)
        {
            if (actorValue?.movement == null)
                return;

            Vector3 face = Vector3.zero;
            if (faceTarget && target != null)
            {
                face = target.position - actorValue.transform.position;
                face.y = 0f;
                if (face.sqrMagnitude > 0.0001f)
                    face.Normalize();
            }

            actorValue.movement.SetLocomotionIntent(new LocomotionIntent
            {
                WorldMoveDirection = Vector3.zero,
                MoveStrength = 0f,
                FacingDirection = face,
            });
        }
    }
}
