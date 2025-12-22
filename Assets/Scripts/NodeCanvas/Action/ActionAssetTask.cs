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

        protected override void OnExecute() {
			PlayAction();

            var actionDirector = actor.value.actionPlayer;
            actionDirector.OnActionFinished += OnActionStopped;
        }

        void PlayAction()
		{
			actor.value.actionPlayer.Play(actionToPlay.value);
        }

		void OnActionStopped(ActionInstance action)
		{
            if (action.Config != actionToPlay.value) return;

            EndAction();

            var actionDirector = actor.value.actionPlayer;
            actionDirector.OnActionFinished -= OnActionStopped;
        }
	}

}