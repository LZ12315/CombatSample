using DeiveEx.TagTree;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Current Action Has SelfTag")]
    [Category("Custom/Combat")]
    [Description("检查自身当前播放动作的 SelfTags 是否包含指定标签。")]
    public class CurrentActionHasSelfTagTask : ConditionTask<Actor>
    {
        [Tooltip("要检查的 SelfTag。为空时条件返回 false。")]
        public TagReference requiredTag;
        [Tooltip("Exact：要求完全一致；Fuzzy：允许子标签匹配父标签。")]
        public ActorTagMatchMode matchMode = ActorTagMatchMode.Exact;

        protected override bool OnCheck()
        {
            var actorValue = agent;
            var current = actorValue != null && actorValue.actionManager != null
                ? actorValue.actionManager.CurrentActionAsset
                : null;

            return NodeCanvasTagUtility.ActionHasSelfTag(current, requiredTag, matchMode);
        }
    }
}
