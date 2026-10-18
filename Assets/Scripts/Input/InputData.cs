/// <summary>Physical button edges, not tap/hold gameplay interpretations.</summary>
public enum InputButtonEventKind
{
    Pressed = 0,
    Released = 1,
}

/// <summary>Immutable captured fact. A release retains its press identity and actual duration.</summary>
public readonly struct PlayerInputEvent
{
    public long Id { get; }
    public long PressId { get; }
    public Enums.InputButton Button { get; }
    public InputButtonEventKind Kind { get; }
    public double Time { get; }
    public double HeldSeconds { get; }
    public bool IsMove { get; }
    public UnityEngine.Vector2 Move { get; }

    internal PlayerInputEvent(long id, UnityEngine.Vector2 move, double time)
    {
        Id = id;
        Move = move;
        Time = time;
        IsMove = true;
        PressId = 0;
        Button = Enums.InputButton.None;
        Kind = default;
        HeldSeconds = 0d;
    }

    internal PlayerInputEvent(long id, long pressId, Enums.InputButton button,
        InputButtonEventKind kind, double time, double heldSeconds)
    {
        Id = id;
        PressId = pressId;
        Button = button;
        Kind = kind;
        Time = time;
        HeldSeconds = heldSeconds;
        IsMove = false;
        Move = default;
    }
}

/// <summary>A read-only receipt, valid only for its owner, session and fixed tick.</summary>
internal readonly struct PlayerInputEventMatch
{
    internal PlayerInputBuffer Owner { get; }
    internal long Tick { get; }
    internal long Generation { get; }
    public PlayerInputEvent Input { get; }
    internal double ValidUntil { get; }

    internal PlayerInputEventMatch(PlayerInputBuffer owner, PlayerInputEvent input, double? validUntil = null)
    {
        Owner = owner;
        Tick = owner.Tick;
        Generation = owner.Generation;
        Input = input;
        ValidUntil = validUntil ?? input.Time + owner.BufferSeconds;
    }
}

public static partial class Enums
{
    // Numeric values are retained for existing button selections. A condition selects
    // one button; masks are only used internally for lifecycle re-arming.
    public enum InputButton
    {
        None = 0,
        Dodge = 2,
        Defence = 4,
        LightAttack = 8,
        HeavyAttack = 16,
        Jump = 32,
        Lock = 64,
    }
}
