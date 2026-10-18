using System;
using UnityEngine;

public enum InputStateType { Button = 0, Direction = 1 }

/// <summary>Current input only. Does not constrain the state when a cached event happened.</summary>
[Serializable]
public sealed class InputStateCondition : PlayerInputCondition
{
    public InputStateType type;
    public Enums.InputButton button;
    public bool held = true;
    [Min(0f), Tooltip("Minimum current hold in real seconds; ignored for Not Held.")]
    public float minimumHoldSeconds;
    [Tooltip("Current target-relative direction, not a recent direction-enter event.")]
    public InputDirection direction;

    protected override bool IsInputConfigurationValid => type == InputStateType.Direction
        ? direction >= InputDirection.Neutral && direction <= InputDirection.Right
        : type == InputStateType.Button && PlayerInputBuffer.IsButton(button)
            && (!held || (!float.IsNaN(minimumHoldSeconds) && !float.IsInfinity(minimumHoldSeconds) && minimumHoldSeconds >= 0f));

    internal override bool OnCheckInput(PlayerInputBuffer input, PlayerInputDirections directions, out PlayerInputMatch match)
    {
        match = default;
        if (type == InputStateType.Direction)
            return directions != null && directions.IsCurrentFor(input) && directions.HasReference && directions.Current == direction;
        return held ? input.IsHeld(button) && input.HeldSeconds(button) >= minimumHoldSeconds : !input.IsHeld(button);
    }
}
