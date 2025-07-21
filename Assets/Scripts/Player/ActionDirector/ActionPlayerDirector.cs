using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class ActionPlayerDirector : StateMachine<PlayableDirector>
{
    protected override void Start()
    {
        base.Start();

        _owner.extrapolationMode = DirectorWrapMode.None;
        _owner.stopped += OnDirectorStopped;
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        throw new NotImplementedException();
    }

}
