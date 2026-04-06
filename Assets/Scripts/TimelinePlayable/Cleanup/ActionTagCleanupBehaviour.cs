using UnityEngine.Playables;

public class ActionTagCleanupBehaviour : ActionBehaviourBase
{
    public ActionTagCleanupPhaseConfig onClipStart;
    public ActionTagCleanupPhaseConfig onEndFinished;
    public ActionTagCleanupPhaseConfig onEndCut;

    protected override void OnClipStart(Playable playable)
    {
        ActionTagCleanupUtility.Apply(actor, onClipStart);
    }

    protected override void OnClipStop(bool isNormal)
    {
        ActionTagCleanupUtility.Apply(actor, isNormal ? onEndFinished : onEndCut);
    }
}
