using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using System;
using UnityEngine.InputSystem;

[Serializable]
public class InputSequence
{
    [SerializeField] int _waitTime = 40; // 使用字段初始化器
    [SerializeField] List<InputCondition> _dataChecks = new List<InputCondition>();

    public int waitTime
    {
        get => _waitTime;
        set => _waitTime = value;
    }

    public List<InputCondition> dataChecks
    {
        get => _dataChecks;
        set => _dataChecks = value;
    }

    // 添加默认值初始化方法
    public void InitializeDefaults()
    {
        _waitTime = 40;
        _dataChecks = new List<InputCondition>();
    }
}

[Serializable]
public class InputCondition
{
    [SerializeReference]
    public InputConditionBase dataCheck = new ButtonInputCondition();

    public void CheckButtonData() => dataCheck = new ButtonInputCondition();
    public void CheckJoystickData() => dataCheck = new JoystickInputCondition();

    public bool CheckInputData(InputData input)
    {
        return dataCheck.CheckInput(input);
    }
}

[Serializable]
public abstract class InputConditionBase
{
    public virtual bool CheckInput(InputData input)
    {
        return false;
    }
}

[Serializable]
public class ButtonInputCondition : InputConditionBase
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
public class JoystickInputCondition : InputConditionBase
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

