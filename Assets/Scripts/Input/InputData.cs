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

    public bool isSame(InputJoystickData other)
    {
        if (!(other.inputJoystick == inputJoystick)) return false;

        if (!(other.joystickVigor == joystickVigor)) return false;

        return true;
    }
}

public static partial class Enums
{
    public enum InputButton
    {
        Idle,
        Dodge,
        Defence,
        LightAttack,
        HeavyAttack
    }

    public enum ButtonState
    {
        Idle,
        ShortPress,
        LongPress_Start,
        LongPress_Cancel
    }

    public enum InputJoystick
    {
        Idle,
        East,
        South,
        West,
        North,
    }

    public enum JoystickVigor
    {
        Idle,
        Light,
        Hard
    }
}