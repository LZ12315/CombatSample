using CombatSample.Consts;
using CombatSample.Utils;
using NodeCanvas.Framework;
using ParadoxNotion.Design;


namespace NodeCanvas.Tasks.Conditions {

	[Name("Check InputState")]
	[Category("Custom")]
	[Description("Transfer to next [Action] Once one of the stateChecks has been met")]
	public class InputStateCheckTask : ConditionTask {

		[Header("≈‰÷√")]
		public BBParameter<PlayerInputController> inputController;

        [Header(" Ù–‘")]
		public bool requiredPressState;
        public Enums.InputCheckType inputCheckType;
		public Enums.InputButton button;
		public Enums.InputJoystick joystick;

		protected override bool OnCheck() {
			if(inputController == null) return false;

			switch(inputCheckType)
			{
				case Enums.InputCheckType.Button:
                    foreach (var button in EnumUtils.GetFlags(button))
                    {
                        if (inputController.value.GetInputState(button) == requiredPressState)
                            return true;
                    }
					break;
				case Enums.InputCheckType.Joystick:
                    foreach (var joystick in EnumUtils.GetFlags(joystick))
                    {
                        if ((inputController.value.GetInputState(joystick) == requiredPressState))
                            return true;
                    }
					break;
            }

            return false;
        }
	}
}