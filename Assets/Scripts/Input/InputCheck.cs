using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSample.Input
{

    [Serializable]
    public class InputCheckWrapper
    {
        public Enums.InputCheckType checkType;
        public ButtonInputCheck buttonInputCheck;
        public JoystickInputCheck joystickInputCheck;

        [SerializeReference] private InputCheckBase inputCheck;

        public bool CheckInputData(InputData input)
        {
            switch (checkType)
            {
                case Enums.InputCheckType.Button:
                    if(buttonInputCheck == null) return false;
                    return buttonInputCheck.CheckInput(input);

                case Enums.InputCheckType.Joystick:
                    if(joystickInputCheck == null) return false;
                    return joystickInputCheck.CheckInput(input);
            }

            return false;
        }
    }

    [Serializable]
    public abstract class InputCheckBase
    {
        public abstract bool CheckInput(InputData input);
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

}



public partial class Enums
{
    public enum InputCheckType
    {
        Button, Joystick
    }
}