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

        [Header(" Ù–‘")]
        public float waitTime = 0.2f;
        public List<InputCheckWrapper> inputChecks;

        private int checkIndex = 0;

        protected override void OnEnable() {

        }

		protected override void OnDisable() {
            checkIndex = 0;
		}

		protected override bool OnCheck() {
            return checkIndex >= inputChecks.Count;
        }

        void GetBuffer(List<InputBuffer> inputBuffers)
        {
            if (inputBuffers.Count == 0) return;

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

                if (checkIndex >= inputBuffers.Count)
                    break;
            }
        }

	}
}