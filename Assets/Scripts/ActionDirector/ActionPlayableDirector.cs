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
    public TimelineAsset defaultAction;
    TimelineAsset actionPlaying;

    private void Start()
    {
        playableDirector.extrapolationMode = DirectorWrapMode.None;
        playableDirector.stopped += OnDirectorStopped;

        PlayAction(defaultAction);
    }

    public void PlayAction(TimelineAsset action)
    {
        movement.ResetRotation();
        playableDirector.Play(action);
        actionPlaying = action;
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        playableDirector.time = 0;
        playableDirector.Play();
        //PlayAction(defaultAction);
    }

}
