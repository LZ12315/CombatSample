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

        [Header("Settings")]
        public BBParameter<ActionAsset> currentAction;
        public Enums.ActionDataType dataChecks;

        [Header("Progress")]
        [SliderField(0, 1f)] public float requiredProgress = 0;

        protected override bool OnCheck() {
            bool isCorrect = false;
            // TODO: 按 ActionData 类型与进度校验是否满足切状态条件。

            return isCorrect;
        }
    }
}
