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
    protected Enums.ActionClipState state = Enums.ActionClipState.Idle;

    protected virtual void OnClipInit(Playable playable) { }

    protected virtual void OnClipStart(Playable playable) { }

    protected virtual void OnClipPause() { }

    protected virtual void OnClipResume(Playable playable) { }

    protected virtual void OnClipUpdate(Playable playable, FrameData info) { }

    protected virtual void OnClipStop(bool isFinish) { }

    protected virtual void CleanUp() { }

    #region 方法继承

    public override void OnGraphStart(Playable playable)
    {
        base.OnGraphStart(playable);

        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director == null) return;

        actor = director.GetComponent<Actor>();
        if (actor == null)
        {
            actor = director.GetComponentInParent<Actor>();
            if (actor == null)
            {
                Debug.LogWarning("ActionPlayableBase: No Actor found on PlayableDirector or its parents.");
                return;
            }
        }

        OnClipInit(playable);
    }

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (state == Enums.ActionClipState.Play) return;

        if(state == Enums.ActionClipState.Pause)
            OnClipResume(playable);
        else if(state == Enums.ActionClipState.Idle)
            OnClipStart(playable);

        state = Enums.ActionClipState.Play;
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (state != Enums.ActionClipState.Play) return;

        if (playable.GetTime() >= playable.GetDuration() - 0.01f)
        {
            // 正常播放完成

            state = Enums.ActionClipState.Idle;
            OnClipStop(true);
            CleanUp();
        }
        else
        {
            // 判断当前Timeline是否有效
            bool isTimelineStillActive = IsTimelineStillActive(playable);

            if (isTimelineStillActive)
            {
                // 情况1：Timeline有效，只是暂停或空白区域
                state = Enums.ActionClipState.Pause;
                OnClipPause();
            }
            else
            {
                // 情况2：Timeline无效（切换或选中其他物体）
                state = Enums.ActionClipState.Idle;
                OnClipStop(false);
                CleanUp();
            }
        }
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        OnClipUpdate(playable, info);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 判断Timeline是否有效（是否切换到其他Timeline或选中了其他物体）
    /// </summary>
    private bool IsTimelineStillActive(Playable playable)
    {
        // 方法1：检查Graph是否有效
        if (!playable.GetGraph().IsValid())
            return false;

        // 方法2：检查Director是否还存在
        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director == null)
            return false;

        // 方法3：检查Director是否还在播放状态（暂停也算存在）
        // 关键：暂停状态下Graph.IsPlaying()为false，但Graph仍然有效
        bool isGraphValid = playable.GetGraph().IsValid();
        bool hasValidDirector = director != null;

        return isGraphValid && hasValidDirector;
    }

    #endregion

}

public partial class Enums
{
    public enum ActionClipState
    {
        Idle, Play, Pause
    }
}