#if UNITY_EDITOR
using UnityEngine;

public sealed partial class ActorLocomotion
{
    private const int DebugTraceTickCount = 120;
    private bool _debugTraceArmed;
    private int _debugAnimationTicksRemaining;
    private bool _debugTracePending;
    private LocomotionRuntimeAnimationContext _debugTraceContext;
    private LocomotionAnimationRequest _debugTraceRequest;
    private bool _debugTraceAccepted;

    [ContextMenu("Debug/Trace Next Turn or Release (120 Ticks)")]
    private void TraceNextAnimationTicks()
    {
        _debugTraceArmed = true;
        _debugAnimationTicksRemaining = 0;
        _debugTracePending = false;
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this,
            "[Locomotion Trace] Actor '{0}': armed; waiting for a moving reversal or input release.", name);
    }

    private void TraceAnimationTick(in LocomotionRuntimeAnimationContext context,
        in LocomotionAnimationRequest request, bool accepted)
    {
        if (_debugTraceArmed)
        {
            Vector3 source = Vector3.ProjectOnPlane(context.VelocityBeforeMotion, context.Motor.CharacterUp);
            Vector3 target = Vector3.ProjectOnPlane(context.Intent.WorldMoveDirection, context.Motor.CharacterUp);
            if (source.sqrMagnitude < 0.25f
                || (context.HasMovingInput && Vector3.Dot(source.normalized, target.normalized) > -0.5f))
                return;
            _debugTraceArmed = false;
            _debugAnimationTicksRemaining = DebugTraceTickCount;
        }
        if (_debugAnimationTicksRemaining <= 0)
            return;
        _debugTraceContext = context;
        _debugTraceRequest = request;
        _debugTraceAccepted = accepted;
        _debugTracePending = true;
    }

    internal void TraceEvaluatedAnimationTick(ActorAnimation animation)
    {
        if (!_debugTracePending)
            return;
        _debugTracePending = false;
        _debugAnimationTicksRemaining--;
        var context = _debugTraceContext;
        var request = _debugTraceRequest;
        float idleWeight = 0f;
        if (request.IsMove && request.State != null && request.IdleClip != null)
            for (int i = 0; i < request.State.ChildCount; i++)
                if (request.State.GetChild(i).Clip == request.IdleClip)
                    idleWeight += request.State.GetChild(i).Weight;
        string state = _currentRuntime is LocomotionSetRuntime set ? set.AnimationState.ToString() : "Move";
        string message = $"[Locomotion Trace] tick={DebugTraceTickCount - _debugAnimationTicksRemaining}, frame={Time.frameCount}, "
            + $"Actor '{name}', Asset '{CurrentAsset.name}', owner={_animationOwner.Id}, action={context.ActionOwnerId}, "
            + $"input={context.Intent.WorldMoveDirection:F3}/{context.Intent.MoveStrength:F3}, "
            + $"before={context.VelocityBeforeMotion:F3}, after={context.ModelVelocity:F3}, "
            + $"state={state}, accepted={_debugTraceAccepted}, parameter={request.Parameter:F3}, "
            + $"idleSampleWeight={idleWeight:F3}, dt={context.DeltaTime:F4}; "
            + animation.DescribeEvaluatedLocomotionLayers();
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this, "{0}", message);
        _debugTraceContext = default;
        _debugTraceRequest = default;
    }

    private void ClearDebugTrace()
    {
        _debugTraceArmed = false;
        _debugTracePending = false;
        _debugAnimationTicksRemaining = 0;
        _debugTraceContext = default;
        _debugTraceRequest = default;
    }

    private void OnValidate() => MigrateLegacyFallback();
}
#endif
