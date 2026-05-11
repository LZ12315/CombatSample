using UnityEngine.Playables;

public class ActionInputBufferCleanupBehaviour : ActionBehaviourBase
{
    public bool clearOnClipStart;
    public bool clearOnEndFinished;
    public bool clearOnEndCut;

    protected override void OnClipStart(Playable playable)
    {
        if (clearOnClipStart)
            ClearInputBuffer();
    }

    protected override void OnClipStop(bool isNormal)
    {
        if (isNormal)
        {
            if (clearOnEndFinished)
                ClearInputBuffer();
        }
        else
        {
            if (clearOnEndCut)
                ClearInputBuffer();
        }
    }

    private void ClearInputBuffer()
    {
        if (actor == null) return;
        actor.GetComponent<ActorLogicInput>()?.ClearBuffer();
    }
}
