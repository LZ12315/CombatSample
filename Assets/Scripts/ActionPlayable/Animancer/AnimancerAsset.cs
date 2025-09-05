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

        if (transitionAsset.Transition is DirectionalClipTransition directionalTransition)
        {
            // 运行时获取角色方向控制Transition
            Vector2 moveInput = actor.logicInput.MoveInput;
            int moveDirection = CalculateDirection(moveInput);
            lastDirection = moveDirection;

            directionalTransition.SetDirection(moveDirection);
            animancerState = actor.animancer.Play(directionalTransition.Clip, 0.15f);
        }
        // 处理ClipTransition
        else if (transitionAsset.Transition is ClipTransition clipTransition)
        {
            animancerState = actor.animancer.Play(clipTransition);
        }
    }

    protected override void OnClipPause()
    {
        base.OnClipPause();
    }

    protected override void OnClipFinish()
    {
        base.OnClipFinish();

        animancerState = null;
        pauseTime = 0;
        lastDirection = -1;
    }

    int CalculateDirection(Vector2 input)
    {
        return (int)DirectionalAnimationSet8.VectorToDirection(input);
    }

}
