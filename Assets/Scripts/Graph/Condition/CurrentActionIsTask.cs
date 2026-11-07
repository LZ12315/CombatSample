using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Conditions
{
    [Name("Current Action Is")]
    [Category("Custom/Combat")]
    [Description("检查自身 ActionStateManager 当前播放的 Action 是否为指定配置。")]
    public class CurrentActionIsTask : ConditionTask<Actor>
    {
        [Tooltip("要比较的 Action 配置。")]
        public BBParameter<ActionAsset> action;

        protected override bool OnCheck()
        {
            var actorValue = agent;
            if (actorValue == null || actorValue.actionManager == null)
                return false;

            return actorValue.actionManager.CurrentActionAsset == action.value;
        }
    }
}
