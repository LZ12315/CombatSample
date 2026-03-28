using NodeCanvas.Framework;
using ParadoxNotion.Design;
using CombatSample.Utils;


namespace NodeCanvas.Tasks.Conditions {

	[Category("Custom")]
	[Description("Transfer to next [Action] when ActionAttribute meets the needs")]
	public class ActionAttributeCheckTask : ConditionTask {
        [Header("Settings")]
        public BBParameter<ActionAsset> currentAction;
        public Enums.ActionAttributeCheckType attributeChecks;

        protected override bool OnCheck()
        {
            bool isCorrect = false;
            //ActionData actionData = currentAction.value.ActionData;

            foreach (var check in EnumUtils.GetFlags(attributeChecks))
            {
                switch (check)
                {


                    default:
                        break;
                }
            }

            return isCorrect;
        }
    }
}

public static partial class Enums
{
    [System.Flags]
    public enum ActionAttributeCheckType
    {
        None = 0,
    }
}