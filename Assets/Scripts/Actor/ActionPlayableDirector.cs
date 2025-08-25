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
    public ActionTimelineAsset defaultAction;
    ActionTimelineAsset actionPlaying;

    private void Start()
    {
        playableDirector.extrapolationMode = DirectorWrapMode.None;
        playableDirector.stopped += OnDirectorStopped;

        PlayAction(defaultAction);
    }

    public void PlayAction(ActionTimelineAsset action)
    {
        playableDirector.Play(action.TimelineAsset);
        //movement.ResetRotation();
        actionPlaying = action;
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        if (actionPlaying.loop)
            PlayAction(actionPlaying);
        else if(actionPlaying.next != null)
            PlayAction(actionPlaying.next);
        else
            PlayAction(defaultAction);
    }

}
