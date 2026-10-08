using NodeCanvas.Framework;
using ParadoxNotion.Design;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Current Action Is")]
    [Category("Custom/Combat")]
    public class CurrentActionIsTask : ConditionTask
    {
        public BBParameter<Actor> actor;
        public BBParameter<ActionAsset> action;

        protected override bool OnCheck()
        {
            var actorValue = actor.value;
            if (actorValue == null || actorValue.actionManager == null)
                return false;

            return actorValue.actionManager.CurrentActionAsset == action.value;
        }
    }
}
