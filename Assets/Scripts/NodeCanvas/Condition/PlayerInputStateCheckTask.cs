using CombatSample.Utils;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Conditions
{
    /// <summary>
    /// 替代旧版 InputStateCheckTask：查询 <see cref="PlayerInputController"/> 当前按住状态。
    /// </summary>
    [Name("Player Input State Check")]
    [Category("Custom/Combat")]
    public class PlayerInputStateCheckTask : ConditionTask
    {
        [Header("Settings")]
        public BBParameter<PlayerInputController> inputController;

        [Header("Properties")]
        public bool requiredPressState;
        public Enums.InputCheckType inputCheckType;
        public Enums.InputButton button;
        public Enums.InputJoystick joystick;

        protected override bool OnCheck()
        {
            var ctrl = inputController?.value;
            if (ctrl == null)
                return false;

            switch (inputCheckType)
            {
                case Enums.InputCheckType.Button:
                    foreach (var b in EnumUtils.GetFlags(button))
                    {
                        if (ctrl.GetInputState(b) == requiredPressState)
                            return true;
                    }
                    break;

                case Enums.InputCheckType.Joystick:
                    foreach (var j in EnumUtils.GetFlags(joystick))
                    {
                        if (ctrl.GetInputState(j) == requiredPressState)
                            return true;
                    }
                    break;
            }

            return false;
        }
    }
}
