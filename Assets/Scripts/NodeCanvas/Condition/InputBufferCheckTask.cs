using NodeCanvas.Framework;
using ParadoxNotion.Design;
using System.Collections.Generic;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Conditions {

    [Name("Check InputBuffer")]
	[Category("Custom")]
	[Description("A")]
	public class InputBufferCheckTask : ConditionTask {

        [Header("≈‰÷√")]
        public BBParameter<Actor> actor;
        public BBParameter<List<InputBuffer>> inputBuffers;

        [Header(" Ù–‘")]
        public float waitTime = 0.6f;
        public List<InputCheckWrapper> inputChecks;

        private int checkIndex = 0;

        protected override void OnEnable() {

            if (inputBuffers.value.Count == 0) return;

            var buffers = inputBuffers.value;
            float lastTime = buffers[0].time;

            foreach (var buffer in buffers)
            {
                if (inputChecks[checkIndex].CheckInputData(buffer.inputData))
                {
                    if(buffer.time - lastTime <= waitTime)
                        checkIndex++;
                }
            }

            if (checkIndex >= inputBuffers.value.Count)
                actor.value.logicInput.CleanInputBuffers();
        }

		protected override void OnDisable() {
            checkIndex = 0;
		}

		protected override bool OnCheck() {
			return checkIndex >= inputChecks.Count;
		}

	}
}