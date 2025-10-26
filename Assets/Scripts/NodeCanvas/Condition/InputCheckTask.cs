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
        public float waitTime = 0.2f;
        public bool useBuffer = false;
        public Enums.ActionPriority priority;
        public List<InputCheckWrapper> inputChecks;

        private InputCheckHandler checkHandler;
        private bool isTriggered = false;

        protected override void OnEnable() {
            isTriggered = false;

            checkHandler = new InputCheckHandler(waitTime, inputChecks, priority, useBuffer);
            actor.value.logicInput.RegisterForInputEvent(checkHandler, GetResult);
        }

        protected override void OnDisable(){
            isTriggered = false;
            actor.value.logicInput.UnregisterFromInputEvent(checkHandler);
        }

        protected override bool OnCheck() {
            return isTriggered;
		}

        void GetResult(bool triggered) 
        {
            //Debug.Log(triggered);
            isTriggered = triggered;
        }

    }
}