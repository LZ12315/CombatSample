using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using CombatSample.Consts;

public abstract class ActionTrackBase : TrackAsset{ }

public abstract class ActionBehaviourBase : PlayableBehaviour
{
    protected Actor actor = null;
    protected ActionAsset actionAsset;
    protected Enums.ActionClipState state = Enums.ActionClipState.None;

    protected virtual void OnClipPlay(Playable playable) { }

    protected virtual void OnClipUpdate(Playable playable) { }

    protected virtual void OnClipPause() { }

    protected virtual void OnClipFinish(bool isNormal) { }

    protected virtual void CleanUp()
    {
        state = Enums.ActionClipState.None;
        actor.actionPlayerDirector.UnregisterFromTimelineEvent(this);
    }

    #region ·½·¨¼Ì³Ð

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (state == Enums.ActionClipState.Play) return;
        state = Enums.ActionClipState.Play;

        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director == null) return;

        actor = director.GetComponent<Actor>();
        if (actor == null) return;

        actionAsset = actor.actionPlayerDirector.ActionPlaying;

        actor.actionPlayerDirector.RegisterForTimelineEvent(this, OnGraphSwitch);
        OnClipPlay(playable);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (state != Enums.ActionClipState.Play) return;

        if (playable.GetTime() >= playable.GetDuration() - 0.01f)
        {
            state = Enums.ActionClipState.Finish;
            OnClipFinish(true);
            CleanUp();
        }
        else
        {
            state = Enums.ActionClipState.Pause;
            OnClipPause();
        }
    }

    public virtual void OnGraphSwitch(PlayableDirector playableDirector)
    {
        if (state != Enums.ActionClipState.Play) return;

        state = Enums.ActionClipState.Finish;
        OnClipFinish(false);
        CleanUp();
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