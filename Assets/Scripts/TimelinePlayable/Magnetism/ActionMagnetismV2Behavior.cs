using UnityEngine;
using UnityEngine.Playables;
using CombatSample.Magnetism;

namespace CombatSample.TimelinePlayable.Magnetism
{
    /// <summary>
    /// Timeline 薄层：解析战斗目标，驱动 ActionMagnetismSession（根↔表面间隙带，无武器锚点）。
    /// </summary>
    public class ActionMagnetismV2Behavior : ActionBehaviourBase
    {
        public bool useCombatTarget;
        public Transform customTarget;
        public MagnetismConfig config;

        private Transform _targetTransform;
        private ActionMagnetismSession _session;

        protected override void OnClipStart(Playable playable)
        {
            if (actor == null || config == null) return;

            _targetTransform = useCombatTarget ? actor.combater?.CombatTarget?.transform : customTarget;
            if (_targetTransform == null) return;

            _session = new ActionMagnetismSession(actor, _targetTransform, config);
            _session.Begin();
        }

        protected override void OnClipUpdate(Playable playable, FrameData info)
        {
            _session?.Tick((float)info.deltaTime);
        }

        protected override void OnClipStop(bool isNormal)
        {
            _session?.End();
            _session = null;
            _targetTransform = null;
        }
    }
}
