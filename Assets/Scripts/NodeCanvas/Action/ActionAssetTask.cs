using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine.Playables;

namespace NodeCanvas.Tasks.Actions {

	[Name("ActionTimeline")]
	[Category("Custom")]
	[Description("Play a ActionTimeline when performed")]
	public class ActionAssetTask : ActionTask {

		[Header("≈‰÷√")]
		public BBParameter<Actor> actor;
        public BBParameter<ActionAsset> action;

		[Header(" Ù–‘")]
		public bool isLoop = false;

		protected override void OnExecute() {
			PlayAction(actor.value.actionPlayerDirector.director);

			if(isLoop)
			{
				var playableDirector = actor.value.actionPlayerDirector.director;
				playableDirector.stopped += PlayAction;
			}
			else
				EndAction();
		}

		protected override void OnStop() {

            if (isLoop)
            {
                var playableDirector = actor.value.actionPlayerDirector.director;
                playableDirector.stopped -= PlayAction;
            }
        }

		void PlayAction(PlayableDirector director)
		{
			actor.value.actionPlayerDirector.PlayAction(action.value);
			action.value.DataReset();

			var agentBlackBoard = agent.GetComponent<Blackboard>();
			agentBlackBoard.SetVariableValue("currentAction", action.value);
        }
	}
}