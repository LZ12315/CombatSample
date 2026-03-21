using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
public class ActionMagnetismClip : PlayableAsset
{
    [Header("Target")]
    public bool useCombatTarget = true;
    public Transform customTarget;

    [Header("Config")]
    public MagnetismConfig config = new MagnetismConfig();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionMagnetismBehavior>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.useCombatTarget = useCombatTarget;
        behaviour.customTarget = customTarget;
        behaviour.config = config;
        return playable;
    }
}
