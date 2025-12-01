using NodeCanvas.Framework;
using ParadoxNotion.Design;
using CombatSample.Utils;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;
using UnityEngine;
using NodeCanvas.StateMachines;


namespace NodeCanvas.Tasks.Conditions {

    [Name("Check ActionData")]
    [Category("Custom")]
	[Description("Transfer to next [Action] when ActionData meets the needs")]
	public class ActionDataCheckTask : ConditionTask {

        [Header("≈‰÷√")]
        public BBParameter<ActionAsset> currentAction;
        public Enums.ActionDataCheckType dataChecks;

        [Header(" Ù–‘")]
        public Enums.ActionPhase requiredPhase = Enums.ActionPhase.Neutral;
        [SliderField(0, 1f)] public float requiredProgress = 0;

		protected override bool OnCheck() {
			bool isCorrect = false;
            //ActionData actionData = currentAction.value.ActionData;

            //foreach (var check in EnumUtils.GetFlags(dataChecks))
            //{
            //    switch (check)
            //    {
            //        case Enums.ActionDataCheckType.Phase:
            //            isCorrect = EnumUtils.ContainsAny(actionData.phase, requiredPhase);
            //            break;
            //        case Enums.ActionDataCheckType.Progress:
            //            isCorrect = (actionData.normalizedTime >= requiredProgress - 0.03f);
            //            break;
            //    }
            //}

            return isCorrect;
		}
	}
}

public static partial class Enums
{
    [System.Flags]
    public enum ActionDataCheckType
    {
        None = 0,
        Phase = 2,
        Progress = 4
    }
}