using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using NodeCanvas.StateMachines;
using ParadoxNotion.Design;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using HeaderAttribute = ParadoxNotion.Design.HeaderAttribute;

namespace NodeCanvas.Tasks.Actions {

	[Name("Play Action")]
	[Category("Custom")]
	[Description("Play a ActionTimeline when performed")]
	public class ActionAssetTask : ActionTask {

		[Header("≈‰÷√")]
		public BBParameter<Actor> actor;
        public BBParameter<ActionAsset> actionToPlay;

		[Header(" Ù–‘")]
		public bool isLoop = false;

		protected override void OnExecute() {
			StartCoroutine(LateRegistration());
			PlayAction();
		}

		protected override void OnStop() {
            var playableDirector = actor.value.actionPlayerDirector.director;
            playableDirector.stopped -= ActionStopped;
        }

		IEnumerator LateRegistration()
		{
			yield return new WaitForSeconds(0.05f);

            var playableDirector = actor.value.actionPlayerDirector.director;
            playableDirector.stopped += ActionStopped;
        }

		void PlayAction()
		{
			actor.value.actionPlayerDirector.PlayAction(actionToPlay.value);
			actionToPlay.value.DataReset();
        }

		void ActionStopped(PlayableDirector director)
		{
			if(isLoop)
                PlayAction();
			else
				EndAction();
        }
	}
}