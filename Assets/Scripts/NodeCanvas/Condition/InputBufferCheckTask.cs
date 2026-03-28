using NodeCanvas.Framework;
using ParadoxNotion.Design;
using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Conditions {

    [Name("Check InputBuffer")]
	[Category("Custom")]
	[Description("Transfer to next [Action] when all the bufferChecks has been met")]
	public class InputBufferCheckTask : ConditionTask {

        [Header("Settings")]
        public BBParameter<Actor> actor;

        [Header("Properties")]
        public float waitTime = 0.2f;
        public Enums.ActionPriority priority;
        //public List<InputCheckBase> inputChecks;

        //private InputCheckHandler handler;
        private bool isTriggered = false;

        protected override void OnEnable()
        {
            isTriggered = false;
            //handler = new InputCheckHandler(waitTime, inputChecks, priority);
            //actor.value.logicInput.RegisterForBufferEvent(handler, GetResult);
        }

        protected override void OnDisable()
        {
            isTriggered = false;
           // actor.value.logicInput.UnregisterFromBufferEvent(handler);
            //handler = null;
        }

        protected override bool OnCheck() {
            return isTriggered;
        }

        void GetResult(bool triggered)
        {
            isTriggered = triggered;

            //if (!isTriggered)
            //    actor.value.logicInput.UnregisterFromBufferEvent(handler);
        }

	}
}