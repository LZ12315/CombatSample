using NodeCanvas.Framework;
using ParadoxNotion.Design;
using CombatSample.Utils;


namespace NodeCanvas.Tasks.Conditions {

    [Name("Check ActionData")]
    [Category("Custom")]
	[Description("Transfer to next [Action] when ActionData meets the needs")]
	public class ActionDataCheckTask : ConditionTask {

        [Header("≈‰÷√")]
        public BBParameter<ActionAsset> currentAction;

        [Header(" Ù–‘")]
        public Enums.ActionPhase requiredPhase = Enums.ActionPhase.Neutral;

		protected override bool OnCheck() {
			bool isCorrect = false;
			ActionAssetData data = currentAction.value.actionAssetData;

			isCorrect = EnumUtils.ContainsAny(data.phase, requiredPhase);
            // (data.phase == requiredPhase);
            return isCorrect;
		}
	}
}