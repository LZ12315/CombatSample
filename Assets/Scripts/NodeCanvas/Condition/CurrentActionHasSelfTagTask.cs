using DeiveEx.TagTree;
using NodeCanvas.Framework;
using ParadoxNotion.Design;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Current Action Has SelfTag")]
    [Category("Custom/Combat")]
    public class CurrentActionHasSelfTagTask : ConditionTask
    {
        public BBParameter<Actor> actor;
        public TagReference requiredTag;
        public ActorTagMatchMode matchMode = ActorTagMatchMode.Exact;

        protected override bool OnCheck()
        {
            var current = actor.value != null && actor.value.actionManager != null
                ? actor.value.actionManager.CurrentActionAsset
                : null;

            return NodeCanvasTagUtility.ActionHasSelfTag(current, requiredTag, matchMode);
        }
    }
}
