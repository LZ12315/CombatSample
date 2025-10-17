using NodeCanvas.Framework;
using ParadoxNotion.Design;

namespace NodeCanvas.Tasks.Actions {

	[Name("ActionTimeline")]
	[Category("Custom")]
	[Description("Play a ActionTimeline when performed")]
	public class ActionAssetTask : ActionTask {

		[Header("≈‰÷√")]
		public BBParameter<Actor> actor;
		public BBParameter<ActionPlayableDirector> director;
        public BBParameter<ActionAsset> action;

		[Header(" Ù–‘")]
		public bool isLoop = false;

        protected override string OnInit() {
			return null;
		}

		protected override void OnExecute() {

			PlayAction();

			EndAction(true);
		}

		protected override void OnUpdate() {
			var actionProgress = action.value.ActionAssetData.progress;

			if (actionProgress == Enums.ActionProgress.Finish && isLoop)
				PlayAction();
        }

		protected override void OnStop() {
			
		}

		protected override void OnPause() {
			
		}

		void PlayAction()
		{
            director.value.PlayAction(action.value);
			action.value.Init();
        }
	}
}