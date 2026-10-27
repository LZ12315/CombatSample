using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>
    /// 通过可撤销请求交给 ASM，在下一次固定步骤仲裁后报告是否开始动作。
    /// 勾选 Until Started 时持续重试提交，直到动作开始或超时。
    /// 任务结束或暂停只撤销待决请求，不强停已开始的动作；旧执行轮次的回调无效。
    /// </summary>
    [Name("Play Action Via ASM")]
    [Category("Custom/Combat")]
    [Description("向自身 ActionStateManager 提交 Action 播放请求，并按动作是否真正开始返回成功或失败。")]
    public class PlayActionTask : ActionTask<Actor>
    {
        [Tooltip("要交由 ASM 仲裁播放的 Action 配置。")]
        public BBParameter<ActionAsset> actionToPlay;
        [Tooltip("不勾选：只提交一次请求，在下一次 ASM 仲裁后按是否真正开始返回结果。勾选：持续重试提交，直到动作开始或超时。")]
        public bool untilStarted;
        [ShowIf("untilStarted", 1), MinValue(0f)]
        [Tooltip("仅 Until Started：等待动作开始的最长时间（秒，受 Time Scale 影响）。0 表示只等待下一次仲裁，不重试。")]
        public float timeout = 0.3f;

        private bool _active;
        private bool _waitingForResult;
        private bool _resolvedOnce;
        private float _startTime;
        private uint _executionId;
        private ActionStateManager.ExternalActionRequest _request;

        protected override void OnExecute()
        {
            ReleasePendingRequest();
            _active = true;
            _startTime = Time.time;
            SubmitRequest();
        }

        protected override void OnUpdate()
        {
            if (!_active)
                return;

            if (untilStarted && HasExceededDeadline())
            {
                EndAction(false);
                return;
            }

            // Also submits a fresh request after resuming from a pause.
            if (!_waitingForResult)
                SubmitRequest();
        }

        protected override void OnStop() => ReleasePendingRequest();

        protected override void OnPause() => ReleasePendingRequest();

        protected override void OnResume()
        {
            // Preserve the original Time.time timeout; resuming does not extend its deadline.
            _active = true;
        }

        private void ReleasePendingRequest()
        {
            // Invalidate callbacks before Cancel, which may report false synchronously.
            _active = false;
            _waitingForResult = false;
            // A withdrawn request was never judged, so it must not consume the single-attempt allowance.
            _resolvedOnce = false;
            unchecked { _executionId++; }
            var request = _request;
            _request = null;
            request?.Cancel();
        }

        private void SubmitRequest()
        {
            var actorValue = agent;
            var actionValue = actionToPlay.value;
            if (actorValue == null || actorValue.actionManager == null || actionValue == null)
            {
                EndAction(false);
                return;
            }

            uint executionId = _executionId;
            _waitingForResult = true;
            var request = actorValue.actionManager.RequestCancellableExternalAction(
                actionValue,
                ActionContext.ForSelf(actorValue),
                started => HandleRequestResult(executionId, started));

            // Inactive ASM can complete synchronously, possibly ending this execution before return.
            if (_active && executionId == _executionId && _waitingForResult)
                _request = request;
            else
                request.Cancel();
        }

        private void HandleRequestResult(uint executionId, bool started)
        {
            if (!_active || executionId != _executionId)
                return;

            _request = null;
            _waitingForResult = false;
            _resolvedOnce = true;

            if (started)
            {
                EndAction(true);
                return;
            }

            if (!untilStarted)
                EndAction(false);
        }

        // Timeout 0 means one arbitration chance: the deadline is judged only after a judged request.
        private bool HasExceededDeadline()
        {
            float wait = float.IsNaN(timeout) || timeout < 0f ? 0f : timeout;
            if (wait > 0f)
                return Time.time - _startTime >= wait;

            return _resolvedOnce;
        }
    }
}
