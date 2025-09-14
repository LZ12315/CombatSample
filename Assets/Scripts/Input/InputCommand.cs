using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using System;

[CreateAssetMenu(menuName = "Input/Command")]
public class InputCommand : ScriptableObject
{
    public string commandName;
    public int waitTime = 40;
    public List<InputDataSetting> inputs = new List<InputDataSetting>();
}

[Serializable]
public class InputDataSetting
{
    [SerializeReference]
    public InputData inputData;

    public void CreateButtonData() => inputData = new InputButtonData();
    public void CreateJoystickData() => inputData = new InputJoystickData();
}

