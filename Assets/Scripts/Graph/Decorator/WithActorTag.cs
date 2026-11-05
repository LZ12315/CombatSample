using DeiveEx.TagTree;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.BehaviourTrees
{
    /// <summary>Holds one transient Actor tag while the decorated child is running.</summary>
    [Name("With Actor Tag")]
    [Category("Decorators/Custom")]
    [Description("子树执行期间持有自身 Actor 的一个 Transient 标签。完成、失败、中断或停图时释放；暂停释放，恢复后的首次执行重新持有。不选择 Locomotion 或播放动画。")]
    public sealed class WithActorTag : BTDecorator
    {
        [Name("Tag"), Tooltip("执行期间持有的临时标签。未配置、标签未注册或自身没有 Actor 时失败，不执行子树。")]
        public TagReference tagToHold = new TagReference();

        private Actor _scopeActor;
        private TagContainer _heldContainer;
        private Tag _heldTag;

        protected override Status OnExecute(Component agent, IBlackboard blackboard)
        {
            if (decoratedConnection == null)
            {
                EndScope();
                return Status.Optional;
            }

            var actor = agent != null ? agent.GetComponent<Actor>() : null;
            if (actor == null || !actor.isActiveAndEnabled || actor.transientTags == null
                || (_scopeActor != null && _scopeActor != actor)
                || (_heldContainer != null && !ReferenceEquals(_heldContainer, actor.transientTags)))
            {
                EndScope();
                decoratedConnection.Reset();
                return Status.Failure;
            }

            if (_heldTag == null)
            {
                var requestedTag = tagToHold?.GetTag();
                if (requestedTag == null)
                {
                    EndScope();
                    decoratedConnection.Reset();
                    return Status.Failure;
                }

                // Capture the actual acquisition, not mutable Inspector/blackboard state at cleanup time.
                _scopeActor = actor;
                _heldContainer = actor.transientTags;
                _heldTag = requestedTag;
                _heldContainer.AddTag(_heldTag);
            }

            try
            {
                Status result = decoratedConnection.Execute(agent, blackboard);
                if (result != Status.Running)
                    EndScope();
                return result;
            }
            catch
            {
                EndScope();
                throw;
            }
        }

        protected override void OnReset() => EndScope();
        public override void OnGraphStoped() => EndScope();
        public override void OnGraphPaused() => ReleaseTag();

        private void EndScope()
        {
            ReleaseTag();
            _scopeActor = null;
        }

        private void ReleaseTag()
        {
            // Forget before removing: tag-change callbacks may re-enter graph lifecycle code.
            var container = _heldContainer;
            var heldTag = _heldTag;
            _heldContainer = null;
            _heldTag = null;
            if (container != null && heldTag != null)
                container.RemoveTag(heldTag);
        }
    }
}
