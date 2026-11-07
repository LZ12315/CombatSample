using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Check Target Angle")]
    [Category("Custom/Combat")]
    [Description("比较自身水平朝向与显式配置目标之间的角度。")]
    public class CheckTargetAngleTask : ConditionTask<Actor>
    {
        [Tooltip("角度检查的目标 Actor。为空时条件返回 false，不会回退到 CombatTarget。")]
        public BBParameter<Actor> target;
        [SliderField(0f, 180f), Tooltip("目标位于自身水平朝向该角度范围内时返回 true。")]
        public float maxAngle = 60f;

        protected override bool OnCheck()
        {
            var actorValue = agent;
            var targetValue = target?.value;
            if (actorValue == null || targetValue == null)
                return false;

            Vector3 forward = actorValue.transform.forward;
            Vector3 toTarget = targetValue.transform.position - actorValue.transform.position;
            forward.y = 0f;
            toTarget.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
                return false;

            float angle = Vector3.Angle(forward.normalized, toTarget.normalized);
            return angle <= maxAngle;
        }
    }
}
