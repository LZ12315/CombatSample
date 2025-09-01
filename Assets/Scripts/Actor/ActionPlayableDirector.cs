using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ActionPlayableDirector : MonoBehaviour
{
    public PlayableDirector playableDirector;
    public ActorMovement movement;
    public ActorActionSetting actionSetting;
    ActionTimelineAsset actionPlaying;

    private void Start()
    {
        playableDirector.extrapolationMode = DirectorWrapMode.None;
        playableDirector.stopped += OnDirectorStopped;

        PlayAction(actionSetting.idle);
    }

    public void PlayAction(ActionTimelineAsset action)
    {
        if (action == null) return;

        playableDirector.Play(action.TimelineAsset);
        //movement.ResetRotation();
        actionPlaying = action;
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        if (actionPlaying.isLoop)
            PlayAction(actionPlaying);
        else if(actionPlaying.nextAction != null)
            PlayAction(actionPlaying.nextAction);
        else
        {
            switch (actionPlaying.actionType)
            {
                case Enums.ActorActionType.Normal:
                    PlayAction(actionSetting.idle);
                    break;
                case Enums.ActorActionType.Combat:
                    PlayAction(actionSetting.fight_Idle); 
                    break;
            }
        }
    }

}
