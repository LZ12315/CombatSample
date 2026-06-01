using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Runtime behaviour for impulse Timeline clips.
/// It injects horizontal and/or vertical initial velocity once when the clip starts.
/// </summary>
public class ActionImpulseBehavior : ActionBehaviourBase
{
    private const float DirectionEpsilonSqr = 0.001f * 0.001f;

    public ImpulseConfig config;

    protected override void OnClipStart(Playable playable)
    {
        if (actor == null || actor.actorMotor == null || config == null)
            return;

        Vector3 horizontalVelocity;
        float verticalVelocity;

        if (config.directionMode == ImpulseDirectionMode.ToCombatTarget3D &&
            TryBuildCombatTarget3DVelocity(out horizontalVelocity, out verticalVelocity))
        {
            ApplyImpulse(horizontalVelocity, verticalVelocity);
            LogStart("combat-target-3d", horizontalVelocity, verticalVelocity);
            return;
        }

        Vector3 horizontalDirection = MotionDirectionResolver.Resolve(
            ToPlanarMode(config.directionMode),
            actor,
            actionInstance,
            config.localHorizontalDirection);

        horizontalVelocity = horizontalDirection * config.horizontalForce;
        verticalVelocity = config.verticalForce;

        ApplyImpulse(horizontalVelocity, verticalVelocity);
        LogStart(config.directionMode.ToString(), horizontalVelocity, verticalVelocity);
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

    private bool TryBuildCombatTarget3DVelocity(out Vector3 horizontalVelocity, out float verticalVelocity)
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;

        GameObject target = actor.combater != null ? actor.combater.CombatTarget : null;
        if (target == null)
        {
            if (config.debugLog)
                Debug.LogWarning($"[Impulse] ToCombatTarget3D has no CombatTarget on {actor.name}. Falling back to planar impulse.");
            return false;
        }

        Vector3 sourcePoint = ResolveActorAimPoint(actor);
        Vector3 targetPoint = ResolveTargetAimPoint(target);
        Vector3 toTarget = targetPoint - sourcePoint;

        if (toTarget.sqrMagnitude < DirectionEpsilonSqr)
        {
            if (config.debugLog)
                Debug.LogWarning($"[Impulse] ToCombatTarget3D target direction is too small on {actor.name}. Falling back to planar impulse.");
            return false;
        }

        Vector3 dir3D = toTarget.normalized;
        Vector3 planarDir = new Vector3(dir3D.x, 0f, dir3D.z);
        if (planarDir.sqrMagnitude > DirectionEpsilonSqr)
            horizontalVelocity = planarDir.normalized * Mathf.Abs(config.horizontalForce);

        verticalVelocity = dir3D.y * Mathf.Abs(config.verticalForce);
        return horizontalVelocity.sqrMagnitude > DirectionEpsilonSqr ||
               Mathf.Abs(verticalVelocity) > 0.001f;
    }

    private void ApplyImpulse(Vector3 horizontalVelocity, float verticalVelocity)
    {
        if (horizontalVelocity.sqrMagnitude > DirectionEpsilonSqr)
            actor.actorMotor.AddHorizontalImpulse(horizontalVelocity);

        if (Mathf.Abs(verticalVelocity) > 0.001f)
            actor.actorMotor.AddVerticalImpulse(verticalVelocity);
    }

    private void LogStart(string mode, Vector3 horizontalVelocity, float verticalVelocity)
    {
        if (!config.debugLog)
            return;

        Debug.Log($"[Impulse] Start mode={mode}, horizontalVelocity={horizontalVelocity}, verticalVelocity={verticalVelocity}");
    }

    private static MotionDirectionMode ToPlanarMode(ImpulseDirectionMode mode)
    {
        return mode == ImpulseDirectionMode.FromContext
            ? MotionDirectionMode.FromContext
            : MotionDirectionMode.LocalHorizontal;
    }

    private static Vector3 ResolveActorAimPoint(Actor source)
    {
        if (source == null)
            return Vector3.zero;

        if (source.actorMotor != null && source.actorMotor.Capsule != null)
            return source.actorMotor.Capsule.transform.TransformPoint(source.actorMotor.Capsule.center);

        return source.CameraTarget != null ? source.CameraTarget.position : source.transform.position;
    }

    private static Vector3 ResolveTargetAimPoint(GameObject target)
    {
        if (target == null)
            return Vector3.zero;

        Actor targetActor = target.GetComponent<Actor>();
        if (targetActor == null)
            targetActor = target.GetComponentInChildren<Actor>();
        if (targetActor == null)
            targetActor = target.GetComponentInParent<Actor>();

        if (targetActor != null)
            return ResolveActorAimPoint(targetActor);

        Collider collider = target.GetComponentInChildren<Collider>();
        if (collider != null)
            return collider.bounds.center;

        return target.transform.position;
    }
}
