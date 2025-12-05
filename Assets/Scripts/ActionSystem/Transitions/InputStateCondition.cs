using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Utils;

[Serializable]
public class InputStateCondition : TransitionCondition
{
    [Header("≈‰÷√")]
    [SerializeReference, SubclassSelector]
    private InputStateCheckBase stateCheck;

    protected override bool OnCheck()
    {
        var controller = actor.logicInput.InputController;

        if (stateCheck is ButtonStateCheck buttonCheck)
        {
            foreach (var button in EnumUtils.GetFlags(buttonCheck.check))
            {
                if (controller.GetInputState(button) == buttonCheck.requiredState)
                    return true;
            }
        }

        if (stateCheck is JoystickStateCheck joystickCheck)
        {
            foreach (var joyStick in EnumUtils.GetFlags(joystickCheck.check))
            {
                if (controller.GetInputState(joyStick) == joystickCheck.requiredState)
                    return true;
            }
        }

        return false;
    }

    public override TransitionCondition Clone()
    {
        return new InputStateCondition
        {
            stateCheck = this.stateCheck
        };
    }
}