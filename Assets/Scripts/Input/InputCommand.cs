using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using System;
using UnityEngine.InputSystem;

public class SkillCommand
{
    public ActionTimelineAsset actionToPlay;
    public InputCommand command;
}

[Serializable]
public class InputCommand
{
    public int waitTime = 40;
    public List<InputDataCheck> dataChecks = new List<InputDataCheck>();

    private double waitCounter = 0;
    private int checkIndex = 0;

    public void Init()
    {
        checkIndex = 0;
        waitCounter = 0;
    }

    public void CommandUpdate(double deltaTime)
    {
        waitCounter += deltaTime;
    }

    public void GetInputData(InputData inputData)
    {
        if (dataChecks.Count == 0) return;

        if(IsCommandComplished()) return;

        if (dataChecks[checkIndex].CheckInputData(inputData))
            checkIndex++;
    }

    public bool IsCommandComplished()
    {
        return checkIndex == dataChecks.Count;
    }

}

[Serializable]
public class InputDataCheck
{
    [SerializeReference]
    public InputCheckSetting dataCheck;

    public void CheckButtonData() => dataCheck = new ButtonCheckSetting();
    public void CheckJoystickData() => dataCheck = new JoystickCheckSetting();

    public bool CheckInputData(InputData input)
    {
        return dataCheck.CheckInput(input);
    }
}

[Serializable]
public abstract class InputCheckSetting
{
    public virtual bool CheckInput(InputData input)
    {
        return false;
    }
}

[Serializable]
public class ButtonCheckSetting : InputCheckSetting
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
public class JoystickCheckSetting : InputCheckSetting
{
    public List<Enums.InputJoystick> inputJoysticks = new ();
    public List<Enums.JoystickVigor> joystickVigors = new ();

    public override bool CheckInput(InputData input)
    {
        bool isJoystickSame = false;
        bool isVigorSame = false;

        if (input is InputJoystickData joyStickData)
        {
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

