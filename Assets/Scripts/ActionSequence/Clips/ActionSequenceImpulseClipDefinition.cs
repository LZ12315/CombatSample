using UnityEngine;

[System.Serializable]
public sealed class ActionSequenceImpulseClipDefinition : ActionSequenceClipDefinition
{
    private const float DirectionEpsilonSqr = 0.001f * 0.001f;

    public ImpulseConfig config = new ImpulseConfig();

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.Motion;

    public override ActionSequenceClipRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : ActionSequenceClipRuntime
    {
        private readonly ActionSequenceImpulseClipDefinition _definition;
        private bool _applied;

        public Runtime(ActionSequenceImpulseClipDefinition definition)
        {
            _definition = definition;
        }

        public override void OnEnter(ActionSequenceContext context)
        {
            if (_applied)
                return;

            _applied = true;
            ApplyImpulse(context);
        }

        private void ApplyImpulse(ActionSequenceContext context)
        {
            Actor actor = context.Actor;
            ImpulseConfig config = _definition.config;
            if (actor == null || actor.actorMotor == null || config == null)
                return;

            Vector3 horizontalVelocity;
            float verticalVelocity;

            if (config.directionMode == ImpulseDirectionMode.ToCombatTarget3D &&
                TryBuildCombatTarget3DVelocity(actor, config, out horizontalVelocity, out verticalVelocity))
            {
                Apply(actor, horizontalVelocity, verticalVelocity);
                Log(config, "combat-target-3d", horizontalVelocity, verticalVelocity);
                return;
            }

            Vector3 horizontalDirection = ResolvePlanarDirection(actor, context, config);
            horizontalVelocity = horizontalDirection * config.horizontalForce;
            verticalVelocity = config.verticalForce;

            Apply(actor, horizontalVelocity, verticalVelocity);
            Log(config, config.directionMode.ToString(), horizontalVelocity, verticalVelocity);
        }

        private static void Apply(Actor actor, Vector3 horizontalVelocity, float verticalVelocity)
        {
            if (horizontalVelocity.sqrMagnitude > DirectionEpsilonSqr)
                actor.actorMotor.AddHorizontalImpulse(horizontalVelocity);

            if (Mathf.Abs(verticalVelocity) > 0.001f)
                actor.actorMotor.AddVerticalImpulse(verticalVelocity);
        }

        private static Vector3 ResolvePlanarDirection(Actor actor, ActionSequenceContext context, ImpulseConfig config)
        {
            Vector3 direction = Vector3.zero;
            switch (config.directionMode)
            {
                case ImpulseDirectionMode.FromContext:
                    direction = context.EventContext.Direction;
                    break;
                case ImpulseDirectionMode.LocalHorizontal:
                    Vector3 local = new Vector3(config.localHorizontalDirection.x, 0f, config.localHorizontalDirection.z);
                    direction = actor.transform.TransformDirection(local);
                    break;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < DirectionEpsilonSqr)
            {
                direction = actor.transform.forward;
                direction.y = 0f;
            }

            return direction.sqrMagnitude < DirectionEpsilonSqr ? Vector3.forward : direction.normalized;
        }

        private static bool TryBuildCombatTarget3DVelocity(
            Actor actor,
            ImpulseConfig config,
            out Vector3 horizontalVelocity,
            out float verticalVelocity)
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;

            GameObject target = actor.combater != null ? actor.combater.CombatTarget : null;
            if (target == null)
                return false;

            Vector3 sourcePoint = ResolveActorAimPoint(actor);
            Vector3 targetPoint = ResolveTargetAimPoint(target);
            Vector3 toTarget = targetPoint - sourcePoint;

            if (toTarget.sqrMagnitude < DirectionEpsilonSqr)
                return false;

            Vector3 direction3D = toTarget.normalized;
            Vector3 planarDirection = new Vector3(direction3D.x, 0f, direction3D.z);
            if (planarDirection.sqrMagnitude > DirectionEpsilonSqr)
                horizontalVelocity = planarDirection.normalized * Mathf.Abs(config.horizontalForce);

            verticalVelocity = direction3D.y * Mathf.Abs(config.verticalForce);
            return horizontalVelocity.sqrMagnitude > DirectionEpsilonSqr || Mathf.Abs(verticalVelocity) > 0.001f;
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

            Actor targetActor = target.GetComponent<Actor>() ??
                                target.GetComponentInChildren<Actor>() ??
                                target.GetComponentInParent<Actor>();

            if (targetActor != null)
                return ResolveActorAimPoint(targetActor);

            Collider collider = target.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.center : target.transform.position;
        }

        private static void Log(ImpulseConfig config, string mode, Vector3 horizontalVelocity, float verticalVelocity)
        {
            if (config.debugLog)
                Debug.Log($"[ActionSequence Impulse] Start mode={mode}, horizontalVelocity={horizontalVelocity}, verticalVelocity={verticalVelocity}");
        }
    }
}
