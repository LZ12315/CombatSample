using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NodeCanvas.Framework;
using ParadoxNotion.Design;


namespace NodeCanvas.Tasks.Conditions {

    [Name("Obtain Input")]
    [Category("Custom")]
	[Description("Transfer to next [Action] when all the inputChecks has been met")]
	public class InputCheck : ConditionTask {

        public BBParameter<int> waitFrame = 40;
        public BBParameter<List<global::InputCheck>> inputChecks;

		private CommandStateHandler handler;

        protected override string OnInit(){
            return null;
		}

		protected override void OnEnable() {

		}

		protected override void OnDisable() {

        }

		protected override bool OnCheck() {
			var lastestInput = blackboard.GetVariable<InputData>("InputData");
            return true;
		}
	}
}