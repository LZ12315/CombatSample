/// <summary>Immutable per-candidate group, with a separate direction reference guard.</summary>
internal readonly struct PlayerInputMatch
{
    private readonly PlayerInputEventMatch[] _events;
    private readonly PlayerInputDirections _directions;
    private readonly long _directionGeneration;

    internal PlayerInputMatch(PlayerInputEventMatch[] events, PlayerInputDirections directions)
    {
        _events = events;
        _directions = directions;
        _directionGeneration = directions != null ? directions.Generation : 0;
    }

    public bool IsCurrentFor(PlayerInputBuffer owner) => owner != null && _events != null
        && (_directions == null || (_directions.IsCurrentFor(owner) && _directions.HasReference
            && _directions.Generation == _directionGeneration)) && owner.CanConsume(_events);

    internal void AppendEvents(System.Collections.Generic.List<PlayerInputEventMatch> destination) => destination.AddRange(_events);

    public bool TryConsume(PlayerInputBuffer owner) => IsCurrentFor(owner) && owner.TryConsume(_events);
}
