using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using UnityEngine.InputSystem;
using JetBrains.Annotations;

[Serializable]
public class InputCheckSequence
{
    public int waitFrame;
    public List<InputCheck> inputChecks;

    public InputCheckSequence()
    {
        waitFrame = 40;
        inputChecks = new List<InputCheck>();
    }
}

//这样一看 InputCheck类似乎可以去掉 因为本来是作为绘制的中间件
//现在不进行绘制了 也就不需要了
[Serializable]
public class InputCheck
{
    public Enums.InputCheckType checkType;
    public ButtonInputCheck buttonInputCheck;
    public JoystickInputCheck joystickInputCheck;

    [SerializeReference] private InputCheckBase inputCheck;

    public bool CheckInputData(InputData input)
    {
        switch (checkType)
        {
            case Enums.InputCheckType.None:
                return true;
            case Enums.InputCheckType.Button:
                return buttonInputCheck.CheckInput(input);
            case Enums.InputCheckType.Joystick:
                return joystickInputCheck.CheckInput(input);
        }

        return false;
    }
}

[Serializable]
public abstract class InputCheckBase
{
    public virtual bool CheckInput(InputData input)
    {
        return false;
    }
}

[Serializable]
public class ButtonInputCheck : InputCheckBase
{
    public Enums.InputButton inputButtons = new ();
    public Enums.ButtonState inputState = new();

    public override bool CheckInput(InputData input)
    {
        bool isButtonSame = false;
        bool isStateSame = false;

        if(input is InputButtonData buttonData)
        {
            isButtonSame = (buttonData.inputButton & inputButtons) == buttonData.inputButton;
            isStateSame = (buttonData.buttonState & inputState) == buttonData.buttonState;

            return (isButtonSame && isStateSame);
        }

        return false;
    }
}

[Serializable]
public class JoystickInputCheck : InputCheckBase
{
    public Enums.InputJoystick inputJoysticks = new ();
    public Enums.JoystickVigor joystickVigors = new ();

    public override bool CheckInput(InputData input)
    {
        bool isJoystickSame = false;
        bool isVigorSame = false;

        if (input is InputJoystickData joyStickData)
        {
            if (joyStickData.joystickVigor == Enums.JoystickVigor.Idle &&
                ((joystickVigors & Enums.JoystickVigor.Idle) == Enums.JoystickVigor.Idle))
                return true;

            isJoystickSame = (joyStickData.inputJoystick & inputJoysticks) == joyStickData.inputJoystick;
            isVigorSame = (joyStickData.joystickVigor & joystickVigors) == joyStickData.joystickVigor;

            return (isJoystickSame && isVigorSame);
        }

        return false;
    }
}

public partial class Enums
{
    public enum InputCheckType
    {
        None, Button, Joystick
    }
}