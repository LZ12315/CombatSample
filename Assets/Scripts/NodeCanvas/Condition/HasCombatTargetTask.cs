using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Has Combat Target")]
    [Category("Custom/Combat")]
    public class HasCombatTargetTask : ConditionTask
    {
        public BBParameter<Actor> actor;
        public BBParameter<Transform> targetOverride;

        protected override bool OnCheck()
        {
            return NodeCanvasCombatTargetUtility.TryResolveTarget(actor.value, targetOverride, out _);
        }
    }
}
