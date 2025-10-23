using System;

public abstract class InputData
{
}

[Serializable]
public class InputButtonData : InputData
{
    public Enums.InputButton inputButton;
    public Enums.ButtonState buttonState;

    public InputButtonData(Enums.InputButton button = Enums.InputButton.None, Enums.ButtonState state = Enums.ButtonState.None)
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
        None = 0,
        Dodge = 2,
        Defence = 4,
        LightAttack = 8,
        HeavyAttack = 16
    }

    [System.Flags]
    public enum ButtonState
    {
        None = 0,
        ShortPress = 2,
        LongPress_Start = 4,
        LongPress_Cancel = 8
    }

    [System.Flags]
    public enum InputJoystick
    {
        None = 0,
        Idle = 2,
        East = 4,
        South = 8,
        West = 16,
        North = 32,
    }

    [System.Flags]
    public enum JoystickVigor
    {
        None = 0,
        Idle = 2,
        Light = 4,
        Hard = 8
    }
}