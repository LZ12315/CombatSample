using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using System;

[CreateAssetMenu(menuName = "CombatSystem/Combo")]
public class InputCombo : ScriptableObject
{
    public string skillName;
    public int waitTime = 40;
    public List<InputCommand> commands = new List<InputCommand>();
}

[Serializable]
public class InputCommand
{
    [SerializeReference]
    public InputDataCheck inputData;

    public void CheckButtonData() => inputData = new InputButtonDataCheck();
    public void CheckJoystickData() => inputData = new InputJoystickDataCheck();
}

public abstract class InputDataCheck
{
}

[Serializable]
public class InputButtonDataCheck : InputDataCheck
{
    public List<Enums.InputButton> inputButtons = new ();
    public List<Enums.ButtonState> inputState = new();
}

[Serializable]
public class InputJoystickDataCheck : InputDataCheck
{
    public List<Enums.InputJoystick> inputJoysticks = new ();
    public List<Enums.JoystickVigor> joystickVigors = new ();
}

