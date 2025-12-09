using UnityEngine;
using UnityEngine.Playables;
using Animancer;

public class AnimancerBehaviour : ActionBehaviourBase
{
    // --- 数据 (由 AnimancerClip 传入) ---
    public TransitionAsset transitionAsset;

    // --- 运行时状态 ---
    private AnimancerState _state;

    // 1. 开始播放 (对应 OnEnter)
    protected override void OnClipPlay(Playable playable)
    {
        // actor 已经在基类中被自动获取了，直接使用即可
        if (actor == null || actor.animancer == null) return;
        if (transitionAsset == null || transitionAsset.Transition == null) return;

        // 直接播放 Transition
        // Animancer 会自动处理 ClipTransition 或 DirectionalClipTransition 的播放逻辑
        _state = actor.animancer.Play(transitionAsset.Transition, 0.25f);

        if (_state != null)
        {
            _state.IsPlaying = true;
            // 强制同步一次时间，防止从中间开始播放时的跳变
            _state.Time = (float)playable.GetTime();
        }
    }

    // 2. 每帧更新 (对应 ProcessFrame)
    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (_state == null || actor == null || actor.animancer == null) return;

        // --- 编辑器预览支持 ---
        if (!Application.isPlaying)
        {
            _state.Speed = 0; // 停止 Animancer 内部计时
            _state.Time = (float)playable.GetTime(); // 由 Timeline 接管时间

            // 防止 Directional 动画在预览时因为未设置方向而报错或不显示
            if (transitionAsset.Transition is DirectionalClipTransition directional)
            {
                directional.SetDirection(0);
            }

            actor.animancer.Evaluate(); // 强制刷新模型姿势
            return;
        }

        // --- 运行时逻辑 ---
        _state.Speed = info.effectiveSpeed; // 同步 Timeline 的速度
    }

    // 3. 暂停 (对应 Timeline 暂停但未退出)
    protected override void OnClipPause()
    {
        if (_state != null)
        {
            _state.IsPlaying = false;
        }
    }

    // 4. 完成或中断 (对应 Timeline 结束或被切断)
    protected override void OnClipFinish(bool isNormal)
    {
        if (_state != null)
        {
            // 无论是正常结束还是被中断，都停止播放状态
            // 如果你希望正常结束时保持最后一帧，可以只在 !isNormal 时 Stop
            _state.IsPlaying = false;
        }
    }

    // 5. 清理引用
    protected override void CleanUp()
    {
        _state = null;
        // actor 引用在基类中，不需要我们清理
    }
}