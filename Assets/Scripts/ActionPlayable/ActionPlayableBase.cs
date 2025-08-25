using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public abstract class ActionTrackBase : TrackAsset{ }

public abstract class ActionClipBase : PlayableBehaviour
{
    protected Actor actor = null;
    protected bool isPlaying = false;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (isPlaying) return;
        isPlaying = true;

        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        actor = director.GetComponent<Actor>();
        if (actor == null) return;

        OnClipPlay();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (!isPlaying) return;
        isPlaying = false;

        OnClipPause();
    }

    protected virtual void OnClipPlay()
    {
    }

    protected virtual void OnClipPause()
    {
    }

}
