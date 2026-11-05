using NodeCanvas.Framework;
using ParadoxNotion.Design;

namespace NodeCanvas.Tasks.Actions
{
    [Name("Enter Soft Lock")]
    [Category("Custom/Combat")]
    [Description("让自身 Actor 的 Combater 进入软锁，沿用当前目标或自动选敌。已软锁时保持；没有有效目标或已硬锁时失败。不模拟按键，节点结束不解锁。")]
    public sealed class EnterSoftLockTask : ActionTask<Actor>
    {
        protected override void OnExecute()
        {
            Actor actorValue = agent;
            ActorCombater combater = actorValue != null ? actorValue.combater : null;
            if (actorValue == null || !actorValue.isActiveAndEnabled
                || combater == null || !combater.isActiveAndEnabled)
            {
                EndAction(false);
                return;
            }

            EndAction(combater.TryEnterSoftLock());
        }
    }
}
