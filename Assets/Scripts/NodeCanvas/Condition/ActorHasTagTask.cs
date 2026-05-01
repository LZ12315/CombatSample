using DeiveEx.TagTree;
using NodeCanvas.Framework;
using ParadoxNotion.Design;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Actor Has Tag")]
    [Category("Custom/Combat")]
    public class ActorHasTagTask : ConditionTask
    {
        public BBParameter<Actor> actor;
        public TagReference requiredTag;
        public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;
        public ActorTagMatchMode matchMode = ActorTagMatchMode.Exact;

        protected override bool OnCheck()
        {
            var actorValue = actor.value;
            if (actorValue == null || requiredTag == null)
                return false;

            Tag tag = requiredTag.GetTag();
            return tag != null && actorValue.HasTag(tag, targetContainer, matchMode);
        }
    }
}
