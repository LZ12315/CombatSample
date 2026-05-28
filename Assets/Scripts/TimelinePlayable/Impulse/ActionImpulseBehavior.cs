using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Runtime behaviour for impulse Timeline clips.
/// It injects horizontal and/or vertical initial velocity once when the clip starts.
/// </summary>
public class ActionImpulseBehavior : ActionBehaviourBase
{
    public ImpulseConfig config;

    protected override void OnClipStart(Playable playable)
    {
        if (actor == null || actor.actorMotor == null || config == null)
            return;

        Vector3 horizontalDirection = MotionDirectionResolver.Resolve(
            config.directionMode,
            actor,
            actionInstance,
            config.localHorizontalDirection);

        if (Mathf.Abs(config.horizontalForce) > 0.001f)
            actor.actorMotor.AddHorizontalImpulse(horizontalDirection * config.horizontalForce);

        if (Mathf.Abs(config.verticalForce) > 0.001f)
            actor.actorMotor.AddVerticalImpulse(config.verticalForce);

        if (config.debugLog)
        {
            Debug.Log($"[Impulse] Start mode={config.directionMode}, dir={horizontalDirection}, " +
                      $"hSpeed={config.horizontalForce}, vSpeed={config.verticalForce}");
        }
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        // Impulse energy is injected on start; drag and gravity evolve it in ActorMotor.
    }

    protected override void OnClipStop(bool isFinish)
    {
        if (config != null && config.debugLog)
            Debug.Log($"[Impulse] Stop isFinish={isFinish}");
    }
}
