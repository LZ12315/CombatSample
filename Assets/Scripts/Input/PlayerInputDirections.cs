using System;
using System.Collections.Generic;
using UnityEngine;

public enum InputDirection { Neutral = 0, Forward = 1, Back = 2, Left = 3, Right = 4 }

/// <summary>One fixed-boundary coordinate sample. Never stored in the device journal.</summary>
internal readonly struct InputDirectionFrame
{
    public readonly int TargetId;
    public readonly int ReferenceId;
    public readonly int Mode;
    public readonly long Revision;
    private readonly Vector3 _moveForward, _moveRight, _targetForward;
    public bool IsValid => TargetId != 0 && _targetForward.sqrMagnitude > 0.0001f;

    public InputDirectionFrame(int targetId, int referenceId, int mode,
        Vector3 moveForward, Vector3 moveRight, Vector3 targetForward, long revision = 0)
    {
        TargetId = targetId;
        ReferenceId = referenceId;
        Mode = mode;
        Revision = revision;
        _moveForward = moveForward;
        _moveRight = moveRight;
        _targetForward = targetForward.normalized;
    }

    public Vector2 ToTarget(Vector2 move)
    {
        Vector3 world = _moveForward * move.y + _moveRight * move.x;
        world.y = 0f;
        Vector3 right = Vector3.Cross(Vector3.up, _targetForward);
        return new Vector2(Vector3.Dot(world, right), Vector3.Dot(world, _targetForward));
    }

    public bool SameReference(InputDirectionFrame other) => TargetId == other.TargetId
        && ReferenceId == other.ReferenceId && Mode == other.Mode && Revision == other.Revision && IsValid == other.IsValid;
}

/// <summary>
/// Gameplay interpretation of raw movement changes. Samples coordinates once per fixed
/// boundary, not retroactively at every query. Reference motion alone is not an input edge.
/// </summary>
internal sealed class PlayerInputDirections
{
    internal readonly struct Entry
    {
        public readonly PlayerInputEvent Source;
        public readonly InputDirection Direction;
        public Entry(PlayerInputEvent source, InputDirection direction) { Source = source; Direction = direction; }
    }

    private readonly List<Entry> _events = new List<Entry>();
    private PlayerInputBuffer _owner;
    private long _inputGeneration, _tick, _referenceAfterId;
    private double _referenceStartedAt;
    private InputDirectionFrame _frame;
    private Vector2 _lastMove;
    private InputDirection _lastDirection;
    public long Generation { get; private set; }
    public bool HasReference => _frame.IsValid;
    public InputDirection Current { get; private set; }
    internal IReadOnlyList<Entry> Events => _events;

    public bool IsCurrentFor(PlayerInputBuffer input) => ReferenceEquals(input, _owner)
        && input != null && _inputGeneration == input.Generation && _tick == input.Tick;

    internal bool IsInReference(PlayerInputEvent input) => input.Id > _referenceAfterId
        && input.Time >= _referenceStartedAt - PlayerInputBuffer.TimeEpsilon;

    public void Publish(PlayerInputBuffer input, InputDirectionFrame frame)
    {
        bool alreadyPublished = ReferenceEquals(input, _owner) && _tick == input.Tick;
        bool reset = !ReferenceEquals(input, _owner) || _inputGeneration != input.Generation
            || !_frame.SameReference(frame);
        _owner = input;
        _inputGeneration = input.Generation;
        _tick = input.Tick;
        _frame = frame;
        if (reset)
        {
            Generation++;
            _events.Clear();
            _referenceAfterId = input.LastCapturedId;
            _referenceStartedAt = input.Now;
            _lastMove = input.Move;
            Current = _lastDirection = Classify(frame, _lastMove, InputDirection.Neutral);
            // Establish a baseline, not a direction command from an already-held axis.
            return;
        }

        // Rebase the old physical position without emitting events if the camera/target moved.
        _lastDirection = Classify(frame, _lastMove, _lastDirection);
        if (!alreadyPublished)
        {
            foreach (PlayerInputEvent move in input.PublishedMoves)
            {
                InputDirection next = Classify(frame, move.Move, _lastDirection);
                if (frame.IsValid && IsInReference(move) && next != _lastDirection) _events.Add(new Entry(move, next));
                _lastMove = move.Move;
                _lastDirection = next;
            }
        }
        Current = _lastDirection;
        for (int i = _events.Count - 1; i >= 0; i--)
            if (input.Now - _events[i].Source.Time > PlayerInputBuffer.MaxContextSeconds + input.BufferSeconds + PlayerInputBuffer.TimeEpsilon)
                _events.RemoveAt(i);
    }

    private static InputDirection Classify(InputDirectionFrame frame, Vector2 move, InputDirection previous)
    {
        if (!frame.IsValid) return InputDirection.Neutral;
        // Center hysteresis around the existing 0.1 locomotion threshold.
        double threshold = previous == InputDirection.Neutral ? 0.12d : 0.10d;
        if (move.sqrMagnitude <= threshold * threshold) return InputDirection.Neutral;
        Vector2 relative = frame.ToTarget(move).normalized;
        if (relative.sqrMagnitude < 0.0001f) return InputDirection.Neutral;
        float best = Math.Max(Math.Abs(relative.x), Math.Abs(relative.y));
        float oldScore = Score(previous, relative);
        // About six degrees of angular hysteresis near the four-way boundaries.
        if (previous != InputDirection.Neutral && oldScore >= best - 0.15f) return previous;
        if (Math.Abs(relative.y) >= Math.Abs(relative.x))
            return relative.y >= 0f ? InputDirection.Forward : InputDirection.Back;
        return relative.x >= 0f ? InputDirection.Right : InputDirection.Left;
    }

    private static float Score(InputDirection direction, Vector2 relative)
    {
        switch (direction)
        {
            case InputDirection.Forward: return relative.y;
            case InputDirection.Back: return -relative.y;
            case InputDirection.Left: return -relative.x;
            case InputDirection.Right: return relative.x;
            default: return -1f;
        }
    }
}
