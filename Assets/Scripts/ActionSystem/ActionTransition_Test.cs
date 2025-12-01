using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionTransition_Test : MonoBehaviour
{
    [ContextMenuItem("CheckButton", nameof(CheckButton), order = 0)]
    [ContextMenuItem("CheckJoystick", nameof(CheckJoystick), order = 1)]
    [SerializeReference]
    private List<InputCheckBase> inputChecks = new List<InputCheckBase>();

    private void CheckButton() => inputChecks.Add(new ButtonInputCheck());
    private void CheckJoystick() => inputChecks.Add(new JoystickInputCheck());
}
