using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Runtime behaviour for velocity Timeline clips.
/// It owns selected axes for the clip duration and writes velocity every frame.
/// </summary>
public class ActionVelocityBehavior : ActionBehaviourBase
{
    public VelocityConfig config;

    private MotionOwner _horizontalOwner;
    private MotionOwner _verticalOwner;

    protected override void OnClipStart(Playable playable)
    {
        if (actor?.actorMotor == null || config == null)
            return;

        if (ShouldUseHorizontalVelocity())
            _horizontalOwner = actor.actorMotor.BeginHorizontalVelocity();

        if (ShouldUseVerticalVelocity())
            _verticalOwner = actor.actorMotor.BeginVerticalVelocity();

        if (config.debugLog)
            Debug.Log($"[Velocity] Start hOwner={_horizontalOwner.IsValid}, vOwner={_verticalOwner.IsValid}");
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        if (actor == null || actor.actorMotor == null || config == null)
            return;

        double duration = playable.GetDuration();
        float t = duration > 0.0 ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 0f;

        if (_horizontalOwner.IsValid)
        {
            Vector3 dir = MotionDirectionResolver.Resolve(
                config.directionMode,
                actor,
                actionInstance,
                config.localHorizontalDirection);

            float hCurve = config.horizontalCurve?.Evaluate(t) ?? 1f;
            actor.actorMotor.SetHorizontalVelocity(_horizontalOwner, dir * (config.horizontalSpeed * hCurve));
        }

        if (_verticalOwner.IsValid)
        {
            float vCurve = config.verticalCurve?.Evaluate(t) ?? 1f;
            actor.actorMotor.SetVerticalVelocity(_verticalOwner, config.verticalSpeed * vCurve);
        }
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (actor == null || actor.actorMotor == null)
            return;

        actor.actorMotor.EndHorizontalVelocity(_horizontalOwner);
        actor.actorMotor.EndVerticalVelocity(_verticalOwner);
        _horizontalOwner = default;
        _verticalOwner = default;

        if (config != null && config.debugLog)
            Debug.Log($"[Velocity] Stop isFinish={isFinish}");
    }

    private bool ShouldUseHorizontalVelocity()
    {
        return config.useHorizontalVelocity || Mathf.Abs(config.horizontalSpeed) > 0.001f;
    }

    private bool ShouldUseVerticalVelocity()
    {
        return config.useVerticalVelocity || Mathf.Abs(config.verticalSpeed) > 0.001f;
    }
}
