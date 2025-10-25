using NodeCanvas.Framework;
using ParadoxNotion.Design;
using System.Collections.Generic;


namespace NodeCanvas.Tasks.Conditions {

    [Name("Check LatestInput")]
    [Category("Custom")]
	[Description("Transfer to next [Action] when latest input meets the needs")]
	public class LatestInputCheckTask : ConditionTask {

        [Header("≈‰÷√")]
        public BBParameter<InputData> latestInput;

        [Header(" Ù–‘")]
        public InputCheckWrapper inputCheck;

        protected override bool OnCheck()
        {
            return inputCheck.CheckInputData(latestInput.value);
        }
    }
}