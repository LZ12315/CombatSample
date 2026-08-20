using UnityEngine;

/// <summary>
/// The single fixed-simulation entry owned by one Actor.
/// Gameplay subsystems remain behind this boundary instead of registering
/// individual clips, conditions, or sessions with the world Driver.
/// </summary>
internal sealed class ActorSimulationRuntime
{
    private readonly Actor _actor;
    private ActionPlayer _actionPlayer;
    private ActionPlayer _tickActionPlayer;
    private bool _tickClosed;

    public ActorSimulationRuntime(Actor actor)
    {
        _actor = actor;
    }

    public bool IsActive => _actor != null && _actor.isActiveAndEnabled;
    public int StableId => _actor != null ? _actor.GetInstanceID() : 0;

    public void ExecutePreWorld(float deltaSeconds)
    {
        _tickActionPlayer = null;
        _tickClosed = false;

        if (!IsActive)
            return;

        _tickActionPlayer = ResolveActionPlayer();
        _tickActionPlayer?.ExecuteSimulationPreWorld(deltaSeconds);
    }

    public void ExecutePostWorld(ICombatHitIntentSink hitIntentSink)
    {
        _tickActionPlayer?.ExecuteSimulationPostWorldWithHitSink(hitIntentSink);
    }

    public void EndSimulationTick()
    {
        if (_tickClosed)
            return;

        _tickClosed = true;
        _tickActionPlayer?.EndSimulationTick();
        _tickActionPlayer = null;
    }

    public void AbortSimulationTick()
    {
        if (_tickClosed)
            return;

        _tickClosed = true;
        (_tickActionPlayer ?? ResolveActionPlayer())?.AbortSimulationTick();
        _tickActionPlayer = null;
    }

    private ActionPlayer ResolveActionPlayer()
    {
        if (_actionPlayer == null && _actor != null)
            _actionPlayer = _actor.actionPlayer != null
                ? _actor.actionPlayer
                : _actor.GetComponent<ActionPlayer>();

        return _actionPlayer != null && _actionPlayer.isActiveAndEnabled
            ? _actionPlayer
            : null;
    }
}
