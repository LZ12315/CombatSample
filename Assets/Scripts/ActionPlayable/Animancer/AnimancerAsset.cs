using Animancer;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class AnimancerAsset : PlayableAsset
{
    public TransitionAsset transitionAsset = null;

    public override double duration
    {
        get
        {
            if(transitionAsset == null) return base.duration; // 默认值

            ClipTransition clipTransition = transitionAsset.Transition as ClipTransition;
            if (clipTransition.Clip != null)
                return clipTransition.Clip.length; // 使用动画实际时长
            else
                return base.duration;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<AnimancerClip>.Create(graph);
        AnimancerClip clip = playable.GetBehaviour();

        clip.transitionAsset = transitionAsset;

        return playable;
    }
}

public class AnimancerClip : ActionClipBase
{
    public TransitionAsset transitionAsset = null;

    private AnimancerState animancerState;
    private float pauseTime;
    private bool clipOver = false;

    protected override void OnClipPlay()
    {
        base.OnClipPlay();

        ClipTransition clipTransition = transitionAsset.Transition as ClipTransition;
        if (clipTransition.Clip == null) return;

        if (animancerState == null) //刚开始播放
            animancerState = actor.animancer.Play(clipTransition);
        else if (!animancerState.IsPlaying) //从暂停恢复
        {
            animancerState.Time = pauseTime;
            animancerState.Speed = 1;
            animancerState.Play();
        }
    }

    protected override void OnClipPause()
    {
        base.OnClipPause();

        if (animancerState == null) return;

        if(clipOver) //Clip播放结束
        {
            animancerState = null;
            pauseTime = 0;
        }
        else //Clip被暂停
        {
            pauseTime = animancerState.Time;
            animancerState.IsPlaying = false;
            animancerState.Speed = 0;
        }
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        //判断Clip是否结束
        if (playable.GetTime() >= playable.GetDuration() - 0.0001f)
            clipOver = true;
        else
            clipOver = false;

#if UNITY_EDITOR

        if (animancerState != null)
        {
            float clipProgress = (float)(playable.GetTime() / playable.GetDuration());
            animancerState.NormalizedTime = clipProgress;
            animancerState.Speed = 0;
        }

#endif
    }

}
