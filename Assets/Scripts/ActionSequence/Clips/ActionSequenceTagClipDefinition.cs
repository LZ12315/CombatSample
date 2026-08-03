using DeiveEx.TagTree;
using UnityEngine;

[System.Serializable]
public sealed class ActionSequenceTagClipDefinition : ActionSequenceClipDefinition
{
    public TagReference tag;
    public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.State;

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceTagClipDefinition _definition;
        private Tag _activeTag;

        public Runtime(ActionSequenceTagClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            if (context.Actor == null || _definition.tag == null)
                return;

            _activeTag = _definition.tag.GetTag();
            if (_activeTag != null)
                context.Actor.AddTag(_activeTag, _definition.targetContainer);
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            if (context.Actor != null && _activeTag != null)
                context.Actor.RemoveTag(_activeTag, _definition.targetContainer);

            _activeTag = null;
        }
    }
}
