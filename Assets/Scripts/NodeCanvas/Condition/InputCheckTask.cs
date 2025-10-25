using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NodeCanvas.Framework;
using NodeCanvas.StateMachines;
using NodeCanvas.Tasks.Actions;
using ParadoxNotion.Design;
using UnityEngine;
using UnityEngine.Playables;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Conditions {

    [Name("Obtain Input")]
    [Category("Custom")]
	[Description("Transfer to next [Action] when all the inputChecks has been met")]
	public class InputCheckTask : ConditionTask{

        [Header("≈‰÷√")]
        public BBParameter<Actor> actor;

        [Header(" Ù–‘")]
        public bool useBuffer = false;
        public float waitTime = 0.6f;
        public List<InputCheckWrapper> inputChecks;

        private float waitCounter = 0;
        private int checkIndex = 0;

        protected override void OnEnable() {
            waitCounter = 0;
            checkIndex = 0;

            var logicInput = actor.value.logicInput;
            logicInput.RegisterForInputEvent(this, GetInputData);
        }

        protected override void OnDisable(){
            waitCounter = 0;
            checkIndex = 0;

            var logicInput = actor.value.logicInput;
            logicInput.UnregisterFromInputEvent(this);
        }

        protected override bool OnCheck() {

            if (checkIndex >= inputChecks.Count)
                return true;

            if (waitCounter > 0)
            {
                waitCounter -= Time.deltaTime;
                if (waitCounter <= 0)
                {
                    checkIndex = 0;
                    waitCounter = 0;
                }
            }

            return false;
		}

        void GetInputData(InputData inputData) 
        {
            if (checkIndex >= inputChecks.Count)
                return;

            if (inputChecks[checkIndex].CheckInputData(inputData))
            {
                waitCounter = waitTime;
                checkIndex++;
            }
        }

    }
}