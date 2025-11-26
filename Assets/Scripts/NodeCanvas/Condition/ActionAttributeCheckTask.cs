using NodeCanvas.Framework;
using ParadoxNotion.Design;
using CombatSample.Utils;


namespace NodeCanvas.Tasks.Conditions {

	[Category("Custom")]
	[Description("Transfer to next [Action] when ActionAttribute meets the needs")]
	public class ActionAttributeCheckTask : ConditionTask {
        [Header("≈‰÷√")]
        public BBParameter<ActionAsset> currentAction;
        public Enums.ActionAttributeCheckType attributeChecks;

        [Header(" Ù–‘")]
        public Enums.ActionType requiredType = Enums.ActionType.None;

        protected override bool OnCheck()
        {
            bool isCorrect = false;
            ActionAssetData actionData = currentAction.value.ActionAssetData;

            foreach (var check in EnumUtils.GetFlags(attributeChecks))
            {
                switch (check)
                {
                    case Enums.ActionAttributeCheckType.ActionType:
                        isCorrect = EnumUtils.ContainsAny(actionData.phase, requiredType);
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
        ActionType = 2,
    }
}