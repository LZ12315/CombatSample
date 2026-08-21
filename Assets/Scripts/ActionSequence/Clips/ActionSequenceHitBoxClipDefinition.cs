using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

[System.Serializable]
public sealed class ActionSequenceHitBoxClipDefinition : ActionSequenceClipDefinition
{
    public BoneReference boneReference;
    public ActionHitBoxConfig hitboxConfig = new ActionHitBoxConfig();
    public AttackDataConfig dataConfig = new AttackDataConfig();

    [SerializeReference, SubclassSelector]
    public List<ImpactEffectConfig> effects = new List<ImpactEffectConfig>();

    public override ActionSequenceTrackKind Kind => ActionSequenceTrackKind.HitBox;

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceHitBoxClipDefinition _definition;
        private HitBoxHandle _handle;
        private bool _reportedMissingRuntime;

        public Runtime(ActionSequenceHitBoxClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            ActorHitBoxRuntime hitBoxes = context.HitBoxes;
            if (hitBoxes == null)
            {
                ReportMissingRuntime(context);
                return;
            }

            _handle = hitBoxes.Activate(
                _definition.Guid,
                _definition.boneReference,
                _definition.hitboxConfig,
                _definition.dataConfig,
                _definition.effects);
        }

        public override void OnExit(ActionSequenceContext context, bool completed)
        {
            ActorHitBoxRuntime hitBoxes = context.HitBoxes;
            if (hitBoxes != null)
                hitBoxes.Deactivate(_handle);

            _handle = default;
        }

        private void ReportMissingRuntime(ActionSequenceContext context)
        {
            if (_reportedMissingRuntime)
                return;

            _reportedMissingRuntime = true;
            Actor actor = context != null ? context.Actor : null;
            Debug.LogWarning(
                "[ActionSequenceHitBox] No ActorHitBoxRuntime is available. Authoritative hit query and damage are skipped.",
                actor);
        }
    }
}
