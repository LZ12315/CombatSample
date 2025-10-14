using UnityEngine;
using UnityEngine.Playables;

public class ActionTransitionAsset : PlayableAsset
{
    [Header("∆¨∂Œ…Ë÷√")]
    public Enums.MoveType moveType;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionTransitionClip>.Create(graph);
        ActionTransitionClip clip = playable.GetBehaviour();

        clip.moveType = moveType;

        return playable;
    }

}

public class ActionTransitionClip : ActionClipBase
{
    public Enums.MoveType moveType;
}

public static partial class Enums
{
    public enum MoveType
    {
        None,
        Idle,
        StartUp,
        Recovery,
        Effect,
        Charge,
        OverCharge
    }
}