using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using System;
using UnityEngine.InputSystem;
using JetBrains.Annotations;

[Serializable]
public class InputCheckSequence
{
    public int waitTime;
    public List<InputCheck> inputChecks;

    public InputCheckSequence()
    {
        waitTime = 40;
        inputChecks = new List<InputCheck>();
    }
}

[Serializable]
public class InputCheck
{
    public Enums.InputCheckType checkType;
    [SerializeReference]
    public InputCheckBase inputCheck;

    public bool CheckInputData(InputData input)
    {
        return inputCheck.CheckInput(input);
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
    public List<Enums.InputButton> inputButtons = new ();
    public List<Enums.ButtonState> inputState = new();

    public override bool CheckInput(InputData input)
    {
        bool isButtonSame = false;
        bool isStateSame = false;

        if(input is InputButtonData buttonData)
        {
            foreach(var button in inputButtons)
            {
                if (button == buttonData.inputButton)
                {
                    isButtonSame = true;
                    break;
                }
            }

            foreach(var state in inputState)
            {
                if(state == buttonData.buttonState)
                {
                    isStateSame = true;
                    break;
                }
            }

            return (isButtonSame && isStateSame);
        }

        return false;
    }
}

[Serializable]
public class JoystickInputCheck : InputCheckBase
{
    public List<Enums.InputJoystick> inputJoysticks = new ();
    public List<Enums.JoystickVigor> joystickVigors = new ();

    public override bool CheckInput(InputData input)
    {
        bool isJoystickSame = false;
        bool isVigorSame = false;

        if (input is InputJoystickData joyStickData)
        {
            if (joyStickData.joystickVigor == Enums.JoystickVigor.Idle &&
                joystickVigors.Contains(Enums.JoystickVigor.Idle))
                return true;

            foreach (var joystick in inputJoysticks)
            {
                if (joystick == joyStickData.inputJoystick)
                {
                    isJoystickSame = true;
                    break;
                }
            }

            foreach (var vigor in joystickVigors)
            {
                if (vigor == joyStickData.joystickVigor)
                {
                    isVigorSame = true;
                    break;
                }
            }

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