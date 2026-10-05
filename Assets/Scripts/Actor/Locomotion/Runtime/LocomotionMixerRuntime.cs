public sealed class LocomotionMixerRuntime : LocomotionAnimationRuntime
{
    public LocomotionMixerRuntime(LocomotionMixerAsset asset) : base(asset) { }

    public override LocomotionAnimationRequest UpdateAnimation(in LocomotionRuntimeAnimationContext context) =>
        IsEntered && context.DeltaTime > 0f ? MoveRequest(context, 0.1f) : default;
}
