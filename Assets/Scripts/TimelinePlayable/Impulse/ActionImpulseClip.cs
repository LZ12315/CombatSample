using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Timeline Clip asset for impulse-driven displacement (knockback / launch / bounce).
/// Holds an ImpulseConfig; the custom editor draws its fields flat (no nested foldout).
/// </summary>
[System.Serializable]
public class ActionImpulseClip : PlayableAsset
{
    public ImpulseConfig config = new ImpulseConfig();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionImpulseBehavior>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.config = config;
        return playable;
    }
}
