using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;


[Serializable]
public abstract class InputCheckBase
{
    public abstract bool CheckInput(InputData input);
}

[Serializable]
public class ButtonInputCheck : InputCheckBase
{
    [Tooltip("Button to match")]
    public Enums.InputButton requiredButtons;

    [Tooltip("Button state to match")]
    public Enums.ButtonState requiredState;

    public override bool CheckInput(InputData input)
    {
        if (input is InputButtonData buttonData)
        {
            // 1. 检查按钮匹配：匹配至少一个按钮
            bool buttonsMatch = (buttonData.inputButton & requiredButtons) != 0;

            // 2. 检查状态匹配：匹配至少一个状态
            bool stateMatch = (buttonData.buttonState & requiredState) != 0;

            return buttonsMatch && stateMatch;
        }
        return false;
    }
}

[Serializable]
public class JoystickInputCheck : InputCheckBase
{
    [Tooltip("Stick direction to match")]
    public Enums.InputJoystick requiredDirections;

    [Tooltip("Stick strength to match")]
    public Enums.JoystickVigor requiredVigors;

    public override bool CheckInput(InputData input)
    {
        if (input is InputJoystickData joyStickData)
        {
            // 特殊处理：Idle状态
            if (joyStickData.joystickVigor == Enums.JoystickVigor.Idle)
            {
                // 检查是否要求Idle状态
                // 如果是 则无需匹配方向
                return (requiredVigors & Enums.JoystickVigor.Idle) != 0;
            }

            // 1. 检查方向匹配：匹配至少一个方向
            bool directionsMatch = (joyStickData.inputJoystick & requiredDirections) != 0;

            // 2. 检查力度匹配：匹配至少一个力度
            bool vigorMatch = (joyStickData.joystickVigor & requiredVigors) != 0;

            return directionsMatch && vigorMatch;
        }
        return false;
    }
}

[Serializable]
public abstract class InputStateCheckBase
{
    [Tooltip("Input state to match")]
    public bool requiredState = false;
}

[Serializable]
public class ButtonStateCheck : InputStateCheckBase
{
    [Tooltip("Button flags to match")]
    public Enums.InputButton check;
}

[Serializable]
public class JoystickStateCheck : InputStateCheckBase
{
    [Tooltip("Stick direction to match")]
    public Enums.InputJoystick check;
}


public partial class Enums
{
    public enum InputCheckType
    {
        Button, Joystick
    }
}