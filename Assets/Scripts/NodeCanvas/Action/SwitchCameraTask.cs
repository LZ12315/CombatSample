using NodeCanvas.Framework;
using ParadoxNotion.Design;


namespace NodeCanvas.Tasks.Actions {

	[Category("Custom")]
	[Description("Switch Camera")]
	public class SwitchCameraTask : ActionTask {

		[Header("Settings")]
		public BBParameter<ActorCameraControl> cameraControl;

		[Header("Properties")]
        public Enums.PlayerCameraState cameraState;

        protected override void OnExecute() {
			if(cameraControl != null)
				cameraControl.value.SetCameraState(cameraState);

			EndAction(true);
		}

	}
}