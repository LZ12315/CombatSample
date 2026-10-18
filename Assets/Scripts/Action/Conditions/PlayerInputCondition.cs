using System;

/// <summary>
/// Read-only player checks. No per-actor match state lives in the shared Action asset.
/// Input absence always fails closed; event-triggered actions can skip this category.
/// </summary>
[Serializable]
public abstract class PlayerInputCondition : ActionCondition
{
    public sealed override bool RequiresPlayerInput => true;
    public sealed override bool SupportsInversion => false;
    public sealed override bool IsConfigurationValid => !invertResult && IsInputConfigurationValid;
    protected abstract bool IsInputConfigurationValid { get; }

    internal bool TryMatchInput(PlayerInputBuffer input, out PlayerInputMatch match, PlayerInputDirections directions = null)
    {
        match = default;
        return input != null && IsConfigurationValid && OnCheckInput(input, directions, out match);
    }

    internal sealed override bool TryEvaluate(Actor actor, ActionContext context, out PlayerInputMatch match) =>
        TryMatchInput(actor != null ? actor.PlayerInput : null, out match, actor != null ? actor.PlayerInputDirections : null);

    protected sealed override bool OnCheck(Actor actor) => TryMatchInput(actor.PlayerInput, out _, actor.PlayerInputDirections);

    internal abstract bool OnCheckInput(PlayerInputBuffer input, PlayerInputDirections directions, out PlayerInputMatch match);
}
