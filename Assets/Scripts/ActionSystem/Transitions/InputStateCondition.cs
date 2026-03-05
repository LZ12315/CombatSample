using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Utils;

[Serializable]
public class InputStateCondition : ActionCondition
{
    [Header("??")]
    [SerializeReference, SubclassSelector]
    private InputStateCheckBase stateCheck;

    // 1. ??????? Actor ?? (???????? actor ????????????????)
    protected override bool OnCheck(Actor actor)
    {
        if (stateCheck is ButtonStateCheck buttonCheck)
        {
            foreach (var button in EnumUtils.GetFlags(buttonCheck.check))
            {
                if (PlayerInputController.Instance.GetInputState(button) == buttonCheck.requiredState)
                    return true;
            }
        }

        if (stateCheck is JoystickStateCheck joystickCheck)
        {
            foreach (var joyStick in EnumUtils.GetFlags(joystickCheck.check))
            {
                if (PlayerInputController.Instance.GetInputState(joyStick) == joystickCheck.requiredState)
                    return true;
            }
        }

        return false;
    }

    // 2. ???? Clone() ?????????????????????
}