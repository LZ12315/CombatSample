using System;

/// <summary>Mutable per-Actor execution state created by an immutable LocomotionAsset.</summary>
public abstract class LocomotionRuntime : IDisposable
{
    private bool _disposed;
    protected LocomotionRuntime(LocomotionAsset asset)
    {
        Asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
        MovementConfig = asset.MovementConfig;
        HasValidRuntimeConfig = asset.HasValidRuntimeConfig;
    }

    public LocomotionAsset Asset { get; }
    public LocomotionMovementConfig MovementConfig { get; }
    internal bool HasValidRuntimeConfig { get; }
    public bool IsEntered { get; private set; }

    public void Enter(ActorLocomotion owner, Actor actor)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LocomotionRuntime));
        if (IsEntered)
            return;

        IsEntered = true;
        OnEnter(owner, actor);
    }

    public void Exit(ActorLocomotion owner, Actor actor)
    {
        if (!IsEntered)
            return;

        OnExit(owner, actor);
        IsEntered = false;
    }

    public abstract LocomotionMotionRequest UpdateMotion(in LocomotionRuntimeMotionContext context);

    public virtual LocomotionAnimationRequest UpdateAnimation(in LocomotionRuntimeAnimationContext context) => default;
    public virtual void ResetAnimation() { }
    public virtual void SuspendAnimationFeedback() { }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        OnDispose();
        IsEntered = false;
    }

    protected virtual void OnEnter(ActorLocomotion owner, Actor actor) { }
    protected virtual void OnExit(ActorLocomotion owner, Actor actor) { }
    protected virtual void OnDispose() { }

    protected LocomotionMotionRequest UpdateSharedMotion(in LocomotionRuntimeMotionContext context)
    {
        if (context.Motion == null || context.Motor.EffectiveDeltaTime <= 0f)
            return default;

        context.Motion.Prepare(context.Intent, context.HasIntent, context.Motor.EffectiveDeltaTime,
            MovementConfig);
        return new LocomotionMotionRequest(
            context.Motion.CachedVelocity,
            context.Motion.PendingRotation,
            true,
            true);
    }
}
