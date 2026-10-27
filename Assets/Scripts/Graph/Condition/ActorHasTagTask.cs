using DeiveEx.TagTree;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Actor Has Tag")]
    [Category("Custom/Combat")]
    [Description("检查自身或指定 Actor 是否包含标签。")]
    public class ActorHasTagTask : ConditionTask<Actor>
    {
        [Tooltip("勾选后检查自身；关闭后检查指定的目标 Actor。")]
        public bool checkSelf = true;
        [ShowIf("checkSelf", 0)]
        [Tooltip("要检查的 Actor。为空时条件返回 false。")]
        public BBParameter<Actor> target;
        [Header("Tag")]
        [Tooltip("要检查的标签。为空时条件返回 false。")]
        public TagReference requiredTag;
        [Tooltip("读取持久标签还是临时标签容器。")]
        public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;
        [Tooltip("Exact：要求容器中直接存在该标签；Fuzzy：允许子标签匹配父标签。")]
        public ActorTagMatchMode matchMode = ActorTagMatchMode.Exact;

        protected override bool OnCheck()
        {
            var actorValue = checkSelf ? agent : target?.value;
            if (actorValue == null || requiredTag == null)
                return false;

            Tag tag = requiredTag.GetTag();
            return tag != null && actorValue.HasTag(tag, targetContainer, matchMode);
        }
    }
}
