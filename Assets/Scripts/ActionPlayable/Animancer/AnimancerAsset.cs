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
            if (transitionAsset == null) return base.duration;

            // 支持ClipTransition和DirectionalClipTransition
            if (transitionAsset.Transition is ClipTransition clipTransition)
            {
                if (clipTransition.Clip != null)
                    return clipTransition.Clip.length;
            }
            else if (transitionAsset.Transition is DirectionalClipTransition directionalTransition)
            {
                if (directionalTransition.AnimationSet != null &&
                    directionalTransition.AnimationSet.GetClip(0) != null)
                {
                    // 使用第一个方向的长度作为基准
                    return directionalTransition.AnimationSet.GetClip(0).length;
                }
            }

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
    private int lastDirection = -1;

    protected override void OnClipPlay()
    {
        base.OnClipPlay();

        if(transitionAsset == null) return;

        // 恢复播放
        if (animancerState != null && !animancerState.IsPlaying)
        {
            animancerState.Time = pauseTime;
            animancerState.Play();
        }
        // 开始播放
        else
        {
            // 处理DirectionalTransition
            if (transitionAsset.Transition is DirectionalClipTransition directionalTransition)
            {
                // 运行时获取角色方向控制Transition
                if (Application.isPlaying)
                    HandleDirectionalTransition();
                // 编辑模式下播放选择的Clip
                else
                    animancerState = actor.animancer.Play(directionalTransition.Clip, 0.1f);

                animancerState.Speed = 1;
            }
            // 处理ClipTransition
            else if (transitionAsset.Transition is ClipTransition clipTransition)
            {
                animancerState = actor.animancer.Play(clipTransition);
                animancerState.Speed = 1;
            }
        }
    }

    protected override void OnClipPause()
    {
        base.OnClipPause();

        if (animancerState == null || !animancerState.IsValid()) return;

        if (Application.isPlaying) 
        {
            animancerState.IsPlaying = false;
            pauseTime = animancerState.Time;
            animancerState.Speed = 0;
        }
    }

    protected override void OnClipFrame(Playable playable)
    {
        base.OnClipFrame(playable);

        if(Application.isPlaying)
            HandleDirectionalTransition();

        if (!Application.isPlaying)
            ProcessFramEditor(playable);
    }

    protected override void OnClipFinish()
    {
        base.OnClipFinish();

        animancerState = null;
        pauseTime = 0;
    }

    int CalculateDirection(Vector2 input)
    {
        return (int)DirectionalAnimationSet8.VectorToDirection(input);
    }

    void HandleDirectionalTransition()
    {
        if (transitionAsset.Transition is DirectionalClipTransition directionalTransition)
        {
            Vector2 moveInput = actor.logicInput.MoveInput;
            int moveDirection = CalculateDirection(moveInput);

            if (moveDirection != lastDirection)
            {
                directionalTransition.SetDirection(moveDirection);

                animancerState = actor.animancer.Play(directionalTransition.Clip, 0.35f);
                lastDirection = moveDirection;
            }
        }
    }

    private void ProcessFramEditor(Playable playable)
    {
        if (transitionAsset == null) return;

        if (animancerState != null)
        {
            float clipProgress = (float)(playable.GetTime() / playable.GetDuration());
            animancerState.NormalizedTime = clipProgress;
            animancerState.Speed = 0;
        }

        // 更新动画进度
        if (animancerState != null && animancerState.IsValid())
        {
            float progress = (float)(playable.GetTime() / playable.GetDuration());
            animancerState.NormalizedTime = progress;
            animancerState.IsPlaying = false; // 暂停状态，手动控制进度
            animancerState.Speed = 0;
        }
    }

}
