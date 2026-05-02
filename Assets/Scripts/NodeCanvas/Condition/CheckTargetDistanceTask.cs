using NodeCanvas.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Check Target Distance")]
    [Category("Custom/Combat")]
    public class CheckTargetDistanceTask : ConditionTask
    {
        public enum DistanceCheckMode
        {
            Range,
            Compare,
        }

        public BBParameter<Actor> actor;
        public BBParameter<Transform> targetOverride;

        public DistanceCheckMode checkMode = DistanceCheckMode.Range;

        public BBParameter<float> minDistance = 0f;
        public BBParameter<float> maxDistance = 3f;

        public CompareMethod compareMethod = CompareMethod.LessOrEqualTo;
        public BBParameter<float> compareDistance = 3f;

        [SliderField(0, 0.1f)]
        public float floatingPoint = 0.05f;

        public BBParameter<bool> horizontalOnly = true;

        protected override bool OnCheck()
        {
            var actorValue = actor?.value;
            if (actorValue == null)
                return false;

            bool horiz = horizontalOnly == null || horizontalOnly.value;

            if (!NodeCanvasCombatTargetUtility.TryComputeDistanceToTarget(actorValue, targetOverride, horiz, out float distance))
                return false;

            if (checkMode == DistanceCheckMode.Range)
            {
                float min = minDistance != null ? minDistance.value : 0f;
                float max = maxDistance != null ? maxDistance.value : 0f;
                return distance >= min && distance <= max;
            }

            float cmp = compareDistance != null ? compareDistance.value : 0f;
            return OperationTools.Compare(distance, cmp, compareMethod, floatingPoint);
        }
    }
}
