using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>
    /// 读取自身 Combater 当前存储的目标 Actor，写入黑板快照；不持续同步。
    /// 目标缺失或没有 Actor 时清空输出并失败，不触发目标搜索。
    /// </summary>
    [Name("Get Combat Target")]
    [Category("Custom/Combat")]
    [Description("读取一次自身 Combater 当前存储的目标 Actor，写入黑板。失败时清空输出。")]
    public class GetCombatTargetTask : ActionTask<Actor>
    {
        [BlackboardOnly]
        [Tooltip("输出快照的黑板 Actor 变量，未指定时节点失败。")]
        public BBParameter<Actor> saveAs;

        protected override void OnExecute()
        {
            if (saveAs == null || saveAs.isNone)
            {
                EndAction(false);
                return;
            }

            var actorValue = agent;
            var combatTarget = actorValue != null && actorValue.combater != null
                ? actorValue.combater.CombatTarget
                : null;
            var target = combatTarget != null ? combatTarget.GetComponent<Actor>() : null;
            saveAs.value = target;
            EndAction(target != null);
        }
    }
}
