using NodeCanvas.Framework;
using ParadoxNotion.Design;
using System.Collections;
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
        public int value;
        [Header(" Ù–‘")]
        public float waitTime = 0.2f;
        public List<InputCheckWrapper> inputChecks;

        private int checkIndex = 0;

        protected override void OnEnable() {
            checkIndex = 0;
            BufferCheck(actor.value.logicInput.InputBuffers);
        }

		protected override void OnDisable() {
            checkIndex = 0;
            actor.value.logicInput.ClearBuffer();
		}

		protected override bool OnCheck() {
            return checkIndex >= inputChecks.Count;
        }

        void BufferCheck(List<InputBuffer> inputBuffers)
        {
            if (inputBuffers.Count == 0) return;
            Debug.Log(value);
            float lastTime = 0;
            foreach (var buffer in inputBuffers)
            {
                if (inputChecks[checkIndex].CheckInputData(buffer.inputData))
                {
                    float intervalTime = buffer.time - lastTime;
                    if (lastTime == 0 || intervalTime <= waitTime)
                    {
                        checkIndex++;
                        lastTime = buffer.time;
                    }
                }

                if (checkIndex >= inputChecks.Count)
                    break;
            }
        }

	}
}