using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Check Target Angle")]
    [Category("Custom/Combat")]
    public class CheckTargetAngleTask : ConditionTask
    {
        public BBParameter<Actor> actor;
        public BBParameter<Transform> targetOverride;
        [Range(0f, 180f)] public float maxAngle = 60f;

        protected override bool OnCheck()
        {
            var actorValue = actor.value;
            if (!NodeCanvasCombatTargetUtility.TryResolveTarget(actorValue, targetOverride, out Transform target))
                return false;

            Vector3 forward = actorValue.transform.forward;
            Vector3 toTarget = target.position - actorValue.transform.position;
            forward.y = 0f;
            toTarget.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
                return false;

            float angle = Vector3.Angle(forward.normalized, toTarget.normalized);
            return angle <= maxAngle;
        }
    }
}
