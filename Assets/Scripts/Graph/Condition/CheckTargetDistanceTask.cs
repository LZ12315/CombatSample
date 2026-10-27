using NodeCanvas.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Check Target Distance")]
    [Category("Custom/Combat")]
    [Description("检查自身到显式配置目标 Actor 的距离。")]
    public class CheckTargetDistanceTask : ConditionTask<Actor>
    {
        public enum DistanceCheckMode
        {
            Range,
            Compare,
        }

        [Tooltip("距离检查的目标 Actor。为空时条件返回 false，不会回退到 CombatTarget。")]
        public BBParameter<Actor> target;

        [Tooltip("Range：判断距离是否在区间内；Compare：与单个距离做比较运算。")]
        public DistanceCheckMode checkMode = DistanceCheckMode.Range;

        [ShowIf("checkMode", 0)]
        [Tooltip("仅 Range：允许的最小距离（包含）。")]
        public BBParameter<float> minDistance = 0f;
        [ShowIf("checkMode", 0)]
        [Tooltip("仅 Range：允许的最大距离（包含）。")]
        public BBParameter<float> maxDistance = 3f;

        [ShowIf("checkMode", 1)]
        [Tooltip("仅 Compare：距离与比较距离之间的比较运算。")]
        public CompareMethod compareMethod = CompareMethod.LessOrEqualTo;
        [ShowIf("checkMode", 1)]
        [Tooltip("仅 Compare：参与比较的距离。")]
        public BBParameter<float> compareDistance = 3f;
        [ShowIf("checkMode", 1), SliderField(0f, 0.1f)]
        [Tooltip("仅 Compare：比较时的浮点容差。")]
        public float floatingPoint = 0.05f;

        [Tooltip("仅比较水平面距离；关闭后包含高度差。")]
        public BBParameter<bool> horizontalOnly = true;

        protected override bool OnCheck()
        {
            var actorValue = agent;
            var targetValue = target?.value;
            if (actorValue == null || targetValue == null)
                return false;

            Vector3 delta = targetValue.transform.position - actorValue.transform.position;
            if (horizontalOnly == null || horizontalOnly.value)
                delta.y = 0f;
            float distance = delta.magnitude;

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
