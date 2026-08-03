#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class ActionSequencePlayheadView : VisualElement
{
    private readonly VisualElement head;

    public ActionSequencePlayheadView()
    {
        AddToClassList("asv2-playhead-overlay");
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;

        head = new VisualElement();
        head.AddToClassList("asv2-playhead-head");
        head.pickingMode = PickingMode.Ignore;
        Add(head);
    }

    public void Refresh(ActionSequenceEditorState state)
    {
        bool visible = state != null && state.IsSupported;
        ActionSequenceViewUtility.SetDisplay(this, visible);
        if (!visible)
            return;

        float x = state.Transform.FrameToViewportX(state.CurrentFrame);
        style.left = x;
        style.top = 0f;
        style.bottom = 16f;
        MarkDirtyRepaint();
    }
}
#endif
