using NodeCanvas.Framework;
using ParadoxNotion.Design;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;


namespace NodeCanvas.Tasks.Conditions {

    [Name("ActionStop")]
	[Category("Custom")]
	[Description("A")]
	public class ActionStopTask : ConditionTask {

        [Header("≈‰÷√")]
        public BBParameter<Actor> actor;

		bool isStopped = false;	

        protected override void OnEnable() {

            StartCoroutine(RegistToActionStop());
        }

        IEnumerator RegistToActionStop()
        {
            yield return new WaitForSeconds(0.05f);

            var playableDirector = actor.value.actionPlayerDirector.director;
            playableDirector.stopped += ActionStop;
        }

        protected override void OnDisable() {
            var playableDirector = actor.value.actionPlayerDirector.director;
            playableDirector.stopped -= ActionStop;

            isStopped = false ;
        }

		protected override bool OnCheck() {
            return isStopped;
		}

		void ActionStop(PlayableDirector director)
		{
            isStopped = true;
		}
	}
}