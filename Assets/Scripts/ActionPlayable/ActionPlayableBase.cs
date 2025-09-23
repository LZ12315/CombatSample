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

    protected virtual void OnClipFinish(bool isNormal)
    {
        state = Enums.ActionClipState.None;
        isGraphSwitch = false;
    }

    #region 方法继承

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (state == Enums.ActionClipState.Play)
            return;
        state = Enums.ActionClipState.Play;

        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director == null) return;

        actor = director.GetComponent<Actor>();
        if (actor == null) return;

        actor.actionPlayerDirector.RegisterForTimelineEvent(this, OnPlayableGraphSwitch);
        OnClipPlay(playable);
    }

    bool isGraphSwitch = false;
    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (state != Enums.ActionClipState.Play) return;

        //禁止OnClipPause方法在Clip刚开始时被调用
        if (playable.GetTime() <= 0.1f) return;

        if (playable.GetTime() >= playable.GetDuration() - 0.0001f)
        {
            state = Enums.ActionClipState.Finish;
            OnClipFinish(true);
            actor.actionPlayerDirector.UnregisterFromTimelineEvent(this);
        }
        else if (isGraphSwitch)
        {
            state = Enums.ActionClipState.Finish;
            OnClipFinish(false);
            actor.actionPlayerDirector.UnregisterFromTimelineEvent(this);
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

    public void OnPlayableGraphSwitch(PlayableDirector director)
    {
        isGraphSwitch = true;
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