using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using System.Security.Cryptography;
using System;

public abstract class InputData
{
}

[Serializable]
public class InputButtonData : InputData
{
    public Enums.InputButton inputButton;
    public Enums.InputState inputState;

    public InputButtonData(Enums.InputButton button = Enums.InputButton.None, Enums.InputState state = Enums.InputState.None)
    {
        inputButton = button;
        inputState = state;
    }

    public void SetValue(Enums.InputButton button, Enums.InputState state)
    {
        inputButton = button;
        inputState = state;
    }
}

public class InputJoystickData : InputData
{
    public Enums.InputJoystick inputJoystick;
    public Enums.InputState inputState;

    public InputJoystickData(Enums.InputJoystick joystick = Enums.InputJoystick.None, Enums.InputState state = Enums.InputState.None)
    {
        inputJoystick = joystick;
        inputState = state;
    }

    public void SetValue(Enums.InputJoystick joystick, Enums.InputState state)
    {
        inputJoystick = joystick;
        inputState = state;
    }
}

public static partial class Enums
{
    public enum InputDataType
    {
        InputButton,
        InputJoystick
    }

    public enum InputButton
    {
        None,
        Dodge,
        Defence,
        LightAttack,
        HeavyAttack
    }

    public enum InputJoystick
    {
        None,
        East,
        South,
        West,
        North
    }

    public enum InputState
    {
        None,
        ShortPress,
        Hold,
        Release
    }
}