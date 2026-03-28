using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Utils;

[Serializable]
public class InputStateCondition : ActionCondition
{
    [Header("Settings")]
    [SerializeReference, SubclassSelector]
    private InputStateCheckBase stateCheck;

    // 1. 读当前 Actor 的输入状态（通过单例 PlayerInputController，避免每帧传 actor 进 InputSystem）。
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

    // 2. 条件为 ScriptableObject 时由 Clone() 深拷贝；此处无额外状态可略。
}