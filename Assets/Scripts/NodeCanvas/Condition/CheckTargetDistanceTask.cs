using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Check Target Distance")]
    [Category("Custom/Combat")]
    public class CheckTargetDistanceTask : ConditionTask
    {
        public BBParameter<Actor> actor;
        public BBParameter<Transform> targetOverride;
        public float minDistance = 0f;
        public float maxDistance = 3f;
        public bool horizontalOnly = true;

        protected override bool OnCheck()
        {
            var actorValue = actor.value;
            if (!NodeCanvasCombatTargetUtility.TryResolveTarget(actorValue, targetOverride, out Transform target))
                return false;

            Vector3 delta = target.position - actorValue.transform.position;
            if (horizontalOnly)
                delta.y = 0f;

            float distance = delta.magnitude;
            return distance >= minDistance && distance <= maxDistance;
        }
    }
}
