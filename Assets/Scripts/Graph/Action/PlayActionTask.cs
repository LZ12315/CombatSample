using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>
    /// 通过可撤销请求交给 ASM，在下一次固定步骤仲裁后报告是否开始动作。
    /// 任务结束或暂停只撤销待决请求，不强停已开始的动作；旧执行轮次的回调无效。
    /// </summary>
    [Name("Play Action Via ASM")]
    [Category("Custom/Combat")]
    public class PlayActionTask : ActionTask
    {
        public enum RequestMode
        {
            EnterOnce,
            UntilStarted,
        }

        public enum ResultMode
        {
            ByActionStarted,
            AlwaysTrue,
            AlwaysFalse,
        }

        [Header("Settings")]
        public BBParameter<Actor> actor;
        public BBParameter<ActionAsset> actionToPlay;
        public RequestMode requestMode = RequestMode.EnterOnce;
        public ResultMode resultMode = ResultMode.ByActionStarted;
        [Tooltip("UntilStarted only: fail if the action has not started within this many scaled-time seconds.")]
        public float timeout = 0.3f;

        private bool _active;
        private bool _waitingForResult;
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

            if (requestMode != RequestMode.EnterOnce && Time.time - _startTime >= timeout)
            {
                EndWithResult(false);
                return;
            }

            // Also submits a fresh EnterOnce request after resuming from a pause.
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
            unchecked { _executionId++; }
            var request = _request;
            _request = null;
            request?.Cancel();
        }

        private void SubmitRequest()
        {
            var actorValue = actor.value;
            var actionValue = actionToPlay.value;
            if (actorValue == null || actorValue.actionManager == null || actionValue == null)
            {
                EndWithResult(false);
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

            if (started)
            {
                EndWithResult(true);
                return;
            }

            if (requestMode == RequestMode.EnterOnce)
                EndWithResult(false);
        }

        private void EndWithResult(bool actionStarted)
        {
            switch (resultMode)
            {
                case ResultMode.AlwaysTrue:
                    EndAction(true);
                    break;
                case ResultMode.AlwaysFalse:
                    EndAction(false);
                    break;
                default:
                    EndAction(actionStarted);
                    break;
            }
        }
    }
}
