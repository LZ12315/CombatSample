using UnityEngine;
using UnityEngine.Playables;
using CombatSample.Magnetism;

namespace CombatSample.TimelinePlayable.Magnetism
{
    [System.Serializable]
    public class ActionMagnetismV2Clip : PlayableAsset
    {
        [Header("Target")]
        public bool useCombatTarget = true;
        public Transform customTarget;

        [Header("Config")]
        public MagnetismConfig config = new MagnetismConfig();

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = UnityEngine.Playables.ScriptPlayable<ActionMagnetismV2Behavior>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.useCombatTarget = useCombatTarget;
            behaviour.customTarget = customTarget;
            behaviour.config = config;
            return playable;
        }
    }
}

