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
        if (action == null) return;

        playableDirector.Play(action.TimelineAsset);

        actionPlaying = action;
    }

    public void ReplayAction()
    {
        if(!actionPlaying) return;

        playableDirector.time = 0;
        PlayAction(actionPlaying);
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        if (actionPlaying.isLoop)
            ReplayAction();
        else if (actionPlaying.nextAction != null)
            PlayAction(actionPlaying.nextAction);
        else
            PlayAction(defaultAction);
    }

}
