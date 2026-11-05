using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>
    /// 通过可撤销请求交给 ASM，在下一次固定步骤仲裁后报告是否开始动作。
    /// 勾选 Until Started 时持续重试提交，直到动作开始或超时。
    /// 勾选 Wait For Completion 时，启动后继续等待本次 ActionInstance 正常结束；中断或替换返回失败。
    /// 任务结束或暂停只撤销待决请求，不强停已开始的动作；暂停保留已启动动作的完成结果。
    /// </summary>
    [Name("Play Action Via ASM")]
    [Category("Custom/Combat")]
    [Description("向自身 ASM 提交 Action 播放请求。默认启动即成功；Wait For Completion 等待本次动作正常结束，中断或替换则失败。")]
    public class PlayActionTask : ActionTask<Actor>
    {
        [Tooltip("要交由 ASM 仲裁播放的 Action 配置。")]
        public BBParameter<ActionAsset> actionToPlay;
        [Tooltip("勾选后等待本次动作正常结束才成功；被停止、中断或替换则失败。任务停止不会强停动作，循环动作不会正常结束。")]
        public bool waitForCompletion;
        [Tooltip("不勾选：只提交一次启动请求。勾选：持续重试直到动作开始或超时；不影响启动后的完成等待。")]
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
        private ActionPlayer _startedPlayer;
        private ActionInstance _startedAction;
        private bool? _completionResult;

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

            if (_startedAction != null)
            {
                // The start deadline no longer applies. Events are recorded even while the task is paused.
                if (_completionResult.HasValue)
                    EndAction(_completionResult.Value);
                else if (agent == null || !agent.isActiveAndEnabled || _startedPlayer == null
                         || !_startedPlayer.isActiveAndEnabled || agent.actionPlayer != _startedPlayer
                         || !ReferenceEquals(_startedPlayer.CurrentAction, _startedAction))
                    EndAction(false);
                return;
            }

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

        protected override void OnPause()
        {
            // Withdraw an unjudged request, but never restart an Action that has already begun.
            if (_startedAction == null)
                ReleasePendingRequest();
        }

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
            if (_startedPlayer != null)
            {
                _startedPlayer.OnActionFinished -= HandleActionFinished;
                _startedPlayer.OnActionInterrupted -= HandleActionInterrupted;
            }
            _startedPlayer = null;
            _startedAction = null;
            _completionResult = null;
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
            var player = actorValue.actionPlayer;
            _waitingForResult = true;
            var request = actorValue.actionManager.RequestCancellableExternalAction(
                actionValue,
                ActionContext.ForSelf(actorValue),
                started => HandleRequestResult(executionId, player, actionValue, started));

            // Inactive ASM can complete synchronously, possibly ending this execution before return.
            if (_active && executionId == _executionId && _waitingForResult)
                _request = request;
            else
                request.Cancel();
        }

        private void HandleRequestResult(uint executionId, ActionPlayer player, ActionAsset requestedAction, bool started)
        {
            if (!_active || executionId != _executionId)
                return;

            _request = null;
            _waitingForResult = false;
            _resolvedOnce = true;

            if (started)
            {
                if (!waitForCompletion)
                {
                    EndAction(true);
                    return;
                }

                var instance = player != null ? player.CurrentAction : null;
                if (instance == null || instance.Config != requestedAction)
                {
                    EndAction(false);
                    return;
                }

                _startedPlayer = player;
                _startedAction = instance;
                player.OnActionFinished += HandleActionFinished;
                player.OnActionInterrupted += HandleActionInterrupted;
                return;
            }

            if (!untilStarted)
                EndAction(false);
        }

        private void HandleActionFinished(ActionInstance action)
        {
            if (ReferenceEquals(action, _startedAction))
                _completionResult = true;
        }

        private void HandleActionInterrupted(ActionInstance action)
        {
            if (ReferenceEquals(action, _startedAction))
                _completionResult = false;
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
