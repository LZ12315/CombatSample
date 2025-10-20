using System;

public abstract class InputData
{
}

[Serializable]
public class InputButtonData : InputData
{
    public Enums.InputButton inputButton;
    public Enums.ButtonState buttonState;

    public InputButtonData(Enums.InputButton button = Enums.InputButton.Idle, Enums.ButtonState state = Enums.ButtonState.Idle)
    {
        inputButton = button;
        buttonState = state;
    }
}

[Serializable]
public class InputJoystickData : InputData
{
    public Enums.InputJoystick inputJoystick;
    public Enums.JoystickVigor joystickVigor;

    public InputJoystickData(Enums.InputJoystick joystick = Enums.InputJoystick.Idle, Enums.JoystickVigor vigor = Enums.JoystickVigor.Idle)
    {
        inputJoystick = joystick;
        joystickVigor = vigor;
    }

}

public static partial class Enums
{
    [System.Flags]
    public enum InputButton
    {
        Idle = 0,
        Dodge = 2,
        Defence = 4,
        LightAttack = 8,
        HeavyAttack = 16
    }

    [System.Flags]
    public enum ButtonState
    {
        Idle = 0,
        ShortPress = 2,
        LongPress_Start = 4,
        LongPress_Cancel = 8
    }

    [System.Flags]
    public enum InputJoystick
    {
        Idle = 0,
        East = 2,
        South = 4,
        West = 6,
        North = 8,
    }

    [System.Flags]
    public enum JoystickVigor
    {
        Idle = 0,
        Light = 2,
        Hard = 4
    }
}