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

    public InputJoystickData(Enums.InputJoystick joystick = Enums.InputJoystick.None, Enums.JoystickVigor vigor = Enums.JoystickVigor.None)
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
        None,
        Dodge,
        Defence,
        LightAttack,
        HeavyAttack
    }

    public enum ButtonState
    {
        None,
        ShortPress,
        LongPress_Start,
        LongPress_Cancel
    }

    public enum InputJoystick
    {
        None,
        East,
        South,
        West,
        North,
    }

    public enum JoystickVigor
    {
        None,
        Light,
        Hard
    }
}