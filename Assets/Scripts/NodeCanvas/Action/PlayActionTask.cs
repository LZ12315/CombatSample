using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Actions
{
    /// <summary>
    /// 通过 <see cref="ActionStateManager.RequestExternalAction"/> 请求播放 Action；
    /// ASM 在本帧统一仲裁后回调，成功表示本帧确实开始播放该 Action。
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

        [Header("Settings")]
        public BBParameter<Actor> actor;
        public BBParameter<ActionAsset> actionToPlay;
        public RequestMode requestMode = RequestMode.EnterOnce;
        [Tooltip("仅 UntilStarted：超时仍未开始播放则失败。")]
        public float timeout = 0.3f;

        private bool _active;
        private bool _waitingForResult;
        private float _startTime;

        protected override void OnExecute()
        {
            _active = true;
            _waitingForResult = false;
            _startTime = Time.time;
            SubmitRequest();
        }

        protected override void OnUpdate()
        {
            if (requestMode == RequestMode.EnterOnce)
                return;

            if (Time.time - _startTime >= timeout)
            {
                EndAction(false);
                return;
            }

            if (!_waitingForResult)
                SubmitRequest();
        }

        protected override void OnStop()
        {
            _active = false;
            _waitingForResult = false;
        }

        private void SubmitRequest()
        {
            var actorValue = actor.value;
            var actionValue = actionToPlay.value;
            if (actorValue == null || actorValue.actionManager == null || actionValue == null)
            {
                EndAction(false);
                return;
            }

            _waitingForResult = true;
            actorValue.actionManager.RequestExternalAction(actionValue, HandleRequestResult);
        }

        private void HandleRequestResult(bool started)
        {
            if (!_active)
                return;

            _waitingForResult = false;

            if (started)
            {
                EndAction(true);
                return;
            }

            if (requestMode == RequestMode.EnterOnce)
                EndAction(false);
        }
    }
}
