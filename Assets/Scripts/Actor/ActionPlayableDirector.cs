using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using CombatSample.Consts;

public class ActionPlayableDirector : MonoBehaviour
{
    public PlayableDirector playableDirector;
    public ActorMovement movement;
    public ActorActionSetting actionSetting;
    ActionTimelineAsset actionPlaying;

    private void Awake()
    {
        playableDirector.extrapolationMode = DirectorWrapMode.None;
        playableDirector.stopped += OnDirectorStopped;
    }

    private void Start()
    {
        PlayAction(actionSetting.idle);
    }

    public void PlayAction(ActionTimelineAsset action)
    {
        if (action == null || playableDirector == null) return;

        //停止当前正在播放的任何Timeline
        //playableDirector.Stop(); // 这会立即停止播放并重置所有内部状态

        EventCenter.Instance.EventTrigger<PlayableDirector>(Parameters.ActionTransitionEvent, playableDirector);
        playableDirector.playableAsset = action.TimelineAsset;

        //将播放时间显式重置为0
        playableDirector.time = 0.0;

        playableDirector.Play();

        actionPlaying = action;
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        if(actionPlaying == null) return;

        if (actionPlaying.isLoop)
        {
            playableDirector.time = 0;
            PlayAction(actionPlaying);
        }
        else if (actionPlaying.nextAction != null)
            PlayAction(actionPlaying.nextAction);
        else
        {
            if (actionPlaying.actionType == Enums.ActorActionType.Normal)
                PlayAction(actionSetting.idle);
            else
                PlayAction(actionSetting.fight_Idle);
        }
    }

}
