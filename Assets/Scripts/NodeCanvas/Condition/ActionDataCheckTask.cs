using NodeCanvas.Framework;
using ParadoxNotion.Design;


namespace NodeCanvas.Tasks.Conditions {

    [Name("CheckActionData")]
    [Category("Custom")]
	[Description("Transfer to next [Action] when ActionData meets the needs")]
	public class ActionDataCheckTask : ConditionTask {

		public BBParameter<ActionAsset> currentAction;
        public Enums.ActionPhase phase = Enums.ActionPhase.Neutral;

		protected override bool OnCheck() {
			bool isCorrect = false;
			ActionAssetData data = currentAction.value.actionAssetData;

			isCorrect = (data.phase == phase);

			return isCorrect;
		}
	}
}