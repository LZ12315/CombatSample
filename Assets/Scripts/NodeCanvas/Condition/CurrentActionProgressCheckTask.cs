using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Conditions
{
    /// <summary>
    /// 替代旧版 ActionDataCheckTask：校验当前播放 Action 是否与黑板引用一致，并按进度窗口过滤。
    /// 兼容序列化字段：<c>currentAction</c>、<c>dataChecks</c>、<c>requiredPhase</c>。
    /// </summary>
    [Name("Current Action Progress Check")]
    [Category("Custom/Combat")]
    public class CurrentActionProgressCheckTask : ConditionTask
    {
        public BBParameter<Actor> actor;
        public BBParameter<ActionAsset> currentAction;
        public Enums.ActionDataType dataChecks = Enums.ActionDataType.Progress;

        /// <summary>旧图字段：常用作最小归一化进度阈值（≈ requiredPhase/255）。</summary>
        public int requiredPhase;

        [Range(0f, 1f)] public float minProgress = 0f;
        public float maxProgress = 1f;

        protected override bool OnCheck()
        {
            var act = ResolveActor();
            if (act == null || act.actionPlayer == null)
                return false;

            var inst = act.actionPlayer.CurrentAction;
            if (inst == null)
                return false;

            if (currentAction?.value != null && inst.Config != currentAction.value)
                return false;

            bool needProgress =
                (dataChecks & Enums.ActionDataType.Progress) != 0
                || requiredPhase != 0
                || minProgress > 0f
                || Mathf.Abs(maxProgress - 1f) > 0.0001f;

            // 旧资源 dataChecks 有时序列化为 1（非 Flags Progress）；仍视为需要进度判断。
            if ((int)dataChecks == 1)
                needProgress = true;

            if (!needProgress)
                return true;

            float min = minProgress;
            float max = maxProgress;
            if (requiredPhase != 0 && Mathf.Approximately(minProgress, 0f) && Mathf.Approximately(maxProgress, 1f))
                min = requiredPhase / 255f;

            double t = inst.RuntimeData.normalizedTime;
            return t >= min && t <= max;
        }

        private Actor ResolveActor()
        {
            if (actor != null && actor.value != null)
                return actor.value;
            if (agent is Actor a)
                return a;
            return agent != null ? agent.GetComponentInParent<Actor>() : null;
        }
    }
}
