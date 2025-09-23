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

    protected virtual void OnClipPlay(Playable playable)
    {

    }

    protected virtual void OnClipUpdate(Playable playable)
    {

    }

    protected virtual void OnClipPause()
    {

    }

    protected virtual void OnClipFinish()
    {
        state = Enums.ActionClipState.None;
    }

    #region ·½·¨¼Ì³Ð

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (state == Enums.ActionClipState.Play)
            return;
        state = Enums.ActionClipState.Play;

        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director == null) return;

        actor = director.GetComponent<Actor>();
        if (actor == null) return;

        OnClipPlay(playable);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (state != Enums.ActionClipState.Play) return;

        if (playable.GetTime() >= playable.GetDuration() - 0.01f)
        {
            state = Enums.ActionClipState.Finish;
            OnClipFinish();
        }
        else
        {
            state = Enums.ActionClipState.Pause;
            OnClipPause();
        }
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        OnClipUpdate(playable);
    }

    #endregion

}

public partial class Enums
{
    public enum ActionClipState
    {
        None, Play, Pause, Finish
    }
}