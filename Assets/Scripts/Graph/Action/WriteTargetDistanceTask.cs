using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>
    /// 将 Actor 到战斗目标（黑板覆盖优先）的距离写入黑板 float，供后续条件复用。
    /// </summary>
    [Name("Write Target Distance")]
    [Category("Custom/Combat")]
    public class WriteTargetDistanceTask : ActionTask
    {
        public BBParameter<Actor> actor;
        public BBParameter<Transform> targetOverride;

        [BlackboardOnly]
        public BBParameter<float> targetDistance;

        public BBParameter<bool> horizontalOnly = true;

        protected override void OnExecute()
        {
            var actorValue = actor?.value;
            if (actorValue == null || targetDistance == null)
            {
                EndAction(false);
                return;
            }

            bool horiz = horizontalOnly == null || horizontalOnly.value;

            if (!NodeCanvasCombatTargetUtility.TryComputeDistanceToTarget(actorValue, targetOverride, horiz, out float distance))
            {
                EndAction(false);
                return;
            }

            targetDistance.value = distance;
            EndAction(true);
        }
    }
}
