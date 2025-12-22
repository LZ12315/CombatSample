using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class ActionDataClip : PlayableAsset
{
    [Header("Phase…Ë÷√")]
    public Enums.ActionPhase actionPhase = Enums.ActionPhase.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionDataBehavior>.Create(graph);
        ActionDataBehavior clip = playable.GetBehaviour();

        clip.actionPhase = actionPhase;

        return playable;
    }
}
