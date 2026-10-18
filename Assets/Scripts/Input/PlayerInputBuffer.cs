using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Device-independent facts published at a fixed boundary. No Action/Condition types,
/// tap/hold thresholds, recognition sequences or rendered-frame counters belong here.
/// Times use the Input System realtime clock, including during hit stop.
/// </summary>
internal sealed class PlayerInputBuffer
{
    // Bounded context horizon; command freshness remains independently configurable.
    public const double MaxContextSeconds = 2d;
    internal const double TimeEpsilon = 1e-6d;

    private struct PendingInput
    {
        public long Id;
        public Enums.InputButton Button;
        public bool Down;
        public bool IsMove;
        public Vector2 Move;
        public double Time;
    }

    private sealed class ButtonHold
    {
        public bool Held;
        public double StartedAt;
        public long PressId;
    }

    private readonly List<PendingInput> _pending = new List<PendingInput>();
    private readonly List<PlayerInputEvent> _events = new List<PlayerInputEvent>();
    private readonly List<PlayerInputEvent> _moves = new List<PlayerInputEvent>();
    private readonly List<PlayerInputEvent> _publishedMoves = new List<PlayerInputEvent>();
    private readonly HashSet<long> _consumed = new HashSet<long>();
    private readonly Dictionary<Enums.InputButton, ButtonHold> _buttons = new Dictionary<Enums.InputButton, ButtonHold>();
    private Enums.InputButton _observedButtons;
    private Enums.InputButton _blockedUntilRelease;
    private Vector2 _observedMove;
    private long _nextId;
    private long _nextPressId;

    internal long LastCapturedId => _nextId;
    public IReadOnlyList<PlayerInputEvent> Events => _events;
    public IReadOnlyList<PlayerInputEvent> MoveEvents => _moves;
    public IReadOnlyList<PlayerInputEvent> PublishedMoves => _publishedMoves;
    public double Now { get; private set; }
    public double BufferSeconds { get; private set; } = 0.2d;
    public long Tick { get; private set; }
    public long Generation { get; private set; }
    public Vector2 Move { get; private set; }

    public static bool IsButton(Enums.InputButton button)
    {
        switch (button)
        {
            case Enums.InputButton.Dodge:
            case Enums.InputButton.Defence:
            case Enums.InputButton.LightAttack:
            case Enums.InputButton.HeavyAttack:
            case Enums.InputButton.Jump:
            case Enums.InputButton.Lock:
                return true;
            default:
                return false;
        }
    }

    public void QueueButton(Enums.InputButton button, bool down, double time)
    {
        if (!IsButton(button) || !Finite(time)) return;
        bool wasDown = (_observedButtons & button) != 0;
        if (down) _observedButtons |= button;
        else _observedButtons &= ~button;
        if ((_blockedUntilRelease & button) != 0)
        {
            if (!down) _blockedUntilRelease &= ~button;
            return;
        }
        if (down != wasDown)
            _pending.Add(new PendingInput { Id = ++_nextId, Button = button, Down = down, Time = time });
    }

    public void QueueMove(Vector2 move, double time)
    {
        if (!Finite(time) || !Finite(move.x) || !Finite(move.y) || _observedMove == move) return;
        _observedMove = move;
        _pending.Add(new PendingInput { Id = ++_nextId, IsMove = true, Move = move, Time = time });
    }

    public void BeginFixedTick(double now, double bufferSeconds = 0.2d)
    {
        if (!Finite(now) || !Finite(bufferSeconds) || bufferSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(now), "Input times must be finite and durations non-negative.");
        Now = now;
        BufferSeconds = bufferSeconds;
        Tick++;
        _publishedMoves.Clear();
        _pending.Sort((a, b) => Compare(a.Time, a.Id, b.Time, b.Id));
        // Do not publish future input early. Equal timestamps keep capture order.
        for (int i = 0; i < _pending.Count;)
        {
            PendingInput pending = _pending[i];
            if (pending.Time > now) { i++; continue; }
            if (pending.IsMove)
            {
                Move = pending.Move;
                var move = new PlayerInputEvent(pending.Id, pending.Move, pending.Time);
                _moves.Add(move);
                _publishedMoves.Add(move);
            }
            else ApplyButton(pending);
            _pending.RemoveAt(i);
        }
        _events.Sort((a, b) => Compare(a.Time, a.Id, b.Time, b.Id));
        _moves.Sort((a, b) => Compare(a.Time, a.Id, b.Time, b.Id));
        Prune(_events);
        Prune(_moves);
    }

    private void Prune(List<PlayerInputEvent> events)
    {
        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (Now - events[i].Time <= MaxContextSeconds + BufferSeconds + TimeEpsilon) continue;
            _consumed.Remove(events[i].Id);
            events.RemoveAt(i);
        }
    }

    internal static int Compare(double timeA, long idA, double timeB, long idB)
    {
        int time = timeA.CompareTo(timeB);
        return time != 0 ? time : idA.CompareTo(idB);
    }

    private void ApplyButton(PendingInput pending)
    {
        if (!_buttons.TryGetValue(pending.Button, out ButtonHold hold))
        {
            hold = new ButtonHold();
            _buttons.Add(pending.Button, hold);
        }
        if (pending.Down)
        {
            hold.Held = true;
            hold.StartedAt = pending.Time;
            hold.PressId = ++_nextPressId;
            Add(pending, hold, InputButtonEventKind.Pressed, 0d);
        }
        else if (hold.Held)
        {
            hold.Held = false;
            Add(pending, hold, InputButtonEventKind.Released, Math.Max(0d, pending.Time - hold.StartedAt));
        }
    }

    private void Add(PendingInput pending, ButtonHold hold, InputButtonEventKind kind, double duration) =>
        _events.Add(new PlayerInputEvent(pending.Id, hold.PressId, pending.Button, kind, pending.Time, duration));

    public bool IsHeld(Enums.InputButton button) =>
        _buttons.TryGetValue(button, out ButtonHold hold) && hold.Held;

    public double HeldSeconds(Enums.InputButton button) =>
        _buttons.TryGetValue(button, out ButtonHold hold) && hold.Held ? Math.Max(0d, Now - hold.StartedAt) : 0d;

    public bool TryFindEvent(Enums.InputButton button, InputButtonEventKind kind, out PlayerInputEventMatch match)
    {
        // Oldest eligible event first: rapid clicks remain distinct requests.
        for (int i = 0; i < _events.Count; i++)
        {
            PlayerInputEvent input = _events[i];
            if (input.Button != button || input.Kind != kind || _consumed.Contains(input.Id)
                || Now - input.Time > BufferSeconds + TimeEpsilon) continue;
            match = new PlayerInputEventMatch(this, input);
            return true;
        }
        match = default;
        return false;
    }

    public bool CanConsume(PlayerInputEventMatch match)
    {
        if (!ReferenceEquals(match.Owner, this) || match.Generation != Generation || match.Tick != Tick
            || Now > match.ValidUntil + TimeEpsilon) return false;
        return IsAvailable(match.Input);
    }

    internal bool IsAvailable(PlayerInputEvent input)
    {
        if (_consumed.Contains(input.Id)) return false;
        IReadOnlyList<PlayerInputEvent> events = input.IsMove ? _moves : _events;
        for (int i = 0; i < events.Count; i++)
            if (events[i].Id == input.Id) return true;
        return false;
    }

    public bool CanConsume(IReadOnlyList<PlayerInputEventMatch> matches)
    {
        if (matches == null || matches.Count == 0) return false;
        for (int i = 0; i < matches.Count; i++)
            if (!CanConsume(matches[i])) return false;
        return true;
    }

    public bool TryConsume(PlayerInputEventMatch match)
    {
        if (!CanConsume(match)) return false;
        return _consumed.Add(match.Input.Id);
    }

    public bool TryConsume(IReadOnlyList<PlayerInputEventMatch> matches)
    {
        if (!CanConsume(matches)) return false;
        // Validate the entire set first. Repeated references consume the same edge once.
        for (int i = 0; i < matches.Count; i++) _consumed.Add(matches[i].Input.Id);
        return true;
    }

    public bool TryConsumePress(Enums.InputButton button) =>
        TryFindEvent(button, InputButtonEventKind.Pressed, out PlayerInputEventMatch match) && TryConsume(match);

    public void ClearEvents()
    {
        _events.Clear();
        _moves.Clear();
        _publishedMoves.Clear();
        _consumed.Clear();
        Generation++;
    }

    public void ResetPreservingHeldButtons() => Reset(_observedButtons);

    public void Reset(Enums.InputButton physicallyHeld = Enums.InputButton.None)
    {
        _pending.Clear();
        ClearEvents();
        _buttons.Clear();
        _observedButtons = physicallyHeld;
        _blockedUntilRelease = physicallyHeld;
        _observedMove = Move = Vector2.zero;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
