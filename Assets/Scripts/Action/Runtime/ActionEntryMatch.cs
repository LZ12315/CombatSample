using System.Collections.Generic;

/// <summary>Per-candidate receipts, never stored in a shared ActionAsset/Condition.</summary>
internal readonly struct ActionEntryMatch
{
    private readonly PlayerInputMatch[] _inputs;

    internal ActionEntryMatch(List<PlayerInputMatch> inputs)
    {
        _inputs = inputs != null && inputs.Count > 0 ? inputs.ToArray() : null;
    }

    internal ActionEntryMatch Combine(ActionEntryMatch other)
    {
        if (_inputs == null) return other;
        if (other._inputs == null) return this;
        var inputs = new List<PlayerInputMatch>(_inputs.Length + other._inputs.Length);
        inputs.AddRange(_inputs);
        inputs.AddRange(other._inputs);
        return new ActionEntryMatch(inputs);
    }

    public bool IsCurrentFor(PlayerInputBuffer owner)
    {
        if (_inputs == null) return true;
        foreach (PlayerInputMatch input in _inputs)
            if (!input.IsCurrentFor(owner)) return false;
        return true;
    }

    public bool TryConsume(PlayerInputBuffer owner)
    {
        if (!IsCurrentFor(owner)) return false;
        if (_inputs == null) return true;
        // Validate directions for every group, then commit one flat set atomically.
        // Duplicate references across conditions consume an edge only once.
        var events = new List<PlayerInputEventMatch>();
        foreach (PlayerInputMatch input in _inputs) input.AppendEvents(events);
        return owner.TryConsume(events);
    }
}
