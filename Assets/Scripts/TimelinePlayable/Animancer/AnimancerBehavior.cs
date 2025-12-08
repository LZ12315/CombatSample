using UnityEngine;
using UnityEngine.Playables;
using Animancer;

public class AnimancerBehaviour : PlayableBehaviour
{
    // 由 AnimancerClip 传入
    public TransitionAsset transitionAsset;

    private Actor _actor;
    private AnimancerState _currentState;
    private DirectionalAnimationHandler _directionalHandler;
    private bool _hasEntered;

    #region PlayableBehaviour 生命周期

    public override void OnGraphStart(Playable playable)
    {
        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director != null)
        {
            _actor = director.GetComponent<Actor>();
        }
    }

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (_actor == null || _actor.animancer == null) return;

        if (!_hasEntered)
        {
            _hasEntered = true;
            OnEnter(playable);
        }

        if (_currentState != null)
        {
            _currentState.IsPlaying = true;
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (!_hasEntered) return;

        bool isFinished = playable.GetTime() >= playable.GetDuration() - 0.001f;
        bool isGraphValid = playable.GetGraph().IsValid();

        if (isFinished || !isGraphValid)
        {
            OnExit();
        }
        else
        {
            OnPause();
        }
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (_hasEntered && _currentState != null)
        {
            OnUpdate(playable, info);
        }
    }

    #endregion

    #region 核心逻辑

    private void OnEnter(Playable playable)
    {
        // 1. 验证数据
        if (!IsValidTransition()) return;

        ITransition aTransition = transitionAsset.Transition;
        bool isEditorPreview = !Application.isPlaying;

        // 2. 这里的顺序至关重要：必须先初始化方向逻辑
        if (aTransition is DirectionalClipTransition directional)
        {
            if (isEditorPreview)
            {
                directional.SetDirection(0);
            }
            else
            {
                // 创建Handler，它会在构造函数里根据Input计算出正确的方向
                _directionalHandler = new DirectionalAnimationHandler(_actor, directional);
            }
        }

        // 3. 然后再播放。此时 Transition 内部的方向已经是正确的了。
        _currentState = _actor.animancer.Play(aTransition, 0.15f);

        // 4. 同步时间 (用于编辑器拖拽预览)
        if (_currentState != null)
        {
            _currentState.Time = (float)playable.GetTime();
        }
    }

    private void OnPause()
    {
        if (_currentState != null)
        {
            _currentState.IsPlaying = false;
        }
    }

    private void OnUpdate(Playable playable, FrameData info)
    {
        // --- 编辑器预览逻辑 ---
        if (!Application.isPlaying)
        {
            if (_actor != null && _actor.animancer != null && _currentState != null)
            {
                _currentState.Speed = 0;
                _currentState.Time = (float)playable.GetTime();
                _actor.animancer.Evaluate();
            }
            return;
        }

        // --- 运行时逻辑 ---
        if (_currentState == null) return;

        _currentState.Speed = info.effectiveSpeed;

        // 更新方向逻辑
        if (_directionalHandler != null)
        {
            // Handler 返回新状态 (如果有切换)
            AnimancerState newState = _directionalHandler.Update();

            // 如果发生了切换（newState不为空），更新引用
            if (newState != null)
            {
                _currentState = newState;
                _currentState.Speed = info.effectiveSpeed;
            }
        }
    }

    private void OnExit()
    {
        _currentState = null;
        _directionalHandler = null;
        _hasEntered = false;
    }

    private bool IsValidTransition()
    {
        if (transitionAsset == null)
        {
            if (Application.isPlaying) Debug.LogWarning("AnimancerBehaviour: TransitionAsset is null.");
            return false;
        }

        ITransition transition = transitionAsset.Transition;
        if (transition == null)
        {
            if (Application.isPlaying) Debug.LogWarning($"AnimancerBehaviour: Transition inside '{transitionAsset.name}' is null.");
            return false;
        }

        if (transition is ClipTransition clipTransition && clipTransition.Clip == null)
        {
            if (Application.isPlaying) Debug.LogWarning($"AnimancerBehaviour: Clip in '{transitionAsset.name}' is null.");
            return false;
        }

        if (transition is DirectionalClipTransition directionalTransition)
        {
            // 【修正】使用 AnimationSet 属性避免编译器报错
            if (directionalTransition.AnimationSet == null)
            {
                if (Application.isPlaying) Debug.LogWarning($"AnimancerBehaviour: AnimationSet in '{transitionAsset.name}' is null.");
                return false;
            }

            if (directionalTransition.AnimationSet.GetClip(0) == null)
            {
                if (Application.isPlaying) Debug.LogWarning($"AnimancerBehaviour: Default Clip (0) in Set '{directionalTransition.AnimationSet.name}' is null.");
                return false;
            }
        }

        return true;
    }

    #endregion
}