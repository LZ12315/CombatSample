using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using CombatSample.Consts;

public abstract class ActionTrackBase : TrackAsset{ }

public abstract class ActionClipBase : PlayableBehaviour
{
    protected Actor actor = null;
    protected Enums.ActionClipState state = Enums.ActionClipState.None;
    public Enums.ActionClipState State => state;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (!playable.IsValid() || !playable.GetGraph().IsValid())
            return;

        if (state == Enums.ActionClipState.Play) return;
        state = Enums.ActionClipState.Play;

        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        actor = director.GetComponent<Actor>();
        if (actor == null) return;

        OnClipPlay();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (!playable.IsValid() || !playable.GetGraph().IsValid())
            return;

        if (state != Enums.ActionClipState.Play) return;
        state = Enums.ActionClipState.Pause;

        OnClipPause();
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        OnClipFrame(playable);

        if(playable.GetTime() >= playable.GetDuration() - 0.0001f)
        {
            state = Enums.ActionClipState.Finish;
            OnClipFinish();
        }
    }

    protected virtual void OnClipPlay()
    {
    }

    protected virtual void OnClipPause()
    {
    }

    protected virtual void OnClipFrame(Playable playable)
    {
    }

    protected virtual void OnClipFinish()
    {
    }

}

public partial class Enums
{
    public enum ActionClipState
    {
        None, Play, Pause, Finish
    }
}
