using System;
using System.Collections;
using System.Collections.Generic;
using NodeCanvas.Framework;
using NodeCanvas.Tasks.Actions;
using ParadoxNotion.Design;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Conditions {

    [Name("Obtain Input")]
    [Category("Custom")]
	[Description("Transfer to next [Action] when all the inputChecks has been met")]
	public class InputCheckTask : ConditionTask {

        [Header("≈‰÷√")]
        public BBParameter<InputData> inputData;

        [Header(" Ù–‘")]
        public int waitFrame = 40;
        public List<InputCheckWrapper> inputChecks;

        private int waitCounter = 0;
        private int checkIndex = 0;

		protected override void OnEnable() {
			waitCounter = 0;
			checkIndex = 0;
		}

        protected override bool OnCheck() {
            if (inputChecks[checkIndex].CheckInputData(inputData.value))
            {
                waitCounter = waitFrame;
                checkIndex++;
            }
            //Debug.Log(checkIndex);
            if (checkIndex >= inputChecks.Count)
            {
                waitCounter = 0;
                checkIndex = 0;
                return true;
            }

            if(waitCounter > 0)
            {
                waitCounter--;
                if (waitCounter == 0)
                {
                    checkIndex = 0;
                    waitCounter = 0;
                }
            }

            return false;
		}

        protected override void OnDisable()
        {
            waitCounter = 0;
            checkIndex = 0;
        }
    }
}