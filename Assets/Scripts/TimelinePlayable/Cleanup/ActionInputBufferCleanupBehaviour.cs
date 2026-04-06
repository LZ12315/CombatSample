using UnityEngine.Playables;

public class ActionInputBufferCleanupBehaviour : ActionBehaviourBase
{
    public bool clearOnClipStart;
    public bool clearOnEndFinished;
    public bool clearOnEndCut;

    protected override void OnClipStart(Playable playable)
    {
        if (clearOnClipStart)
            actor?.logicInput?.ClearBuffer();
    }

    protected override void OnClipStop(bool isNormal)
    {
        if (isNormal)
        {
            if (clearOnEndFinished)
                actor?.logicInput?.ClearBuffer();
        }
        else
        {
            if (clearOnEndCut)
                actor?.logicInput?.ClearBuffer();
        }
    }
}
