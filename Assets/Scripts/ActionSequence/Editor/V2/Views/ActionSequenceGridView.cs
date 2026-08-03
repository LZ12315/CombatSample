#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class ActionSequenceGridView : VisualElement
{
    private ActionSequenceEditorState state;

    public ActionSequenceGridView()
    {
        AddToClassList("asv2-grid");
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = 0f;
        style.right = 0f;
        style.top = 0f;
        style.bottom = 0f;
        generateVisualContent += DrawGrid;
    }

    public void Refresh(ActionSequenceEditorState newState)
    {
        state = newState;
        MarkDirtyRepaint();
    }

    private void DrawGrid(MeshGenerationContext context)
    {
        if (state == null || !state.IsSupported)
            return;

        Painter2D painter = context.painter2D;
        ActionSequenceTimelineTransform transform = state.Transform;
        float height = contentRect.height;

        IReadOnlyList<ActionSequenceTimelineTick> ticks = transform.BuildVisibleTicks();
        for (int i = 0; i < ticks.Count; i++)
        {
            ActionSequenceTimelineTick tick = ticks[i];
            if (tick.X < -1f || tick.X > contentRect.width + 1f)
                continue;

            painter.lineWidth = tick.Labelled ? 1.2f : 1f;
            painter.strokeColor = tick.Labelled ? new Color(0.25f, 0.25f, 0.25f, 1f) : new Color(0.18f, 0.18f, 0.18f, 1f);
            DrawLine(painter, tick.X, 0f, tick.X, height);
        }

        float durationX = transform.FrameToViewportX(state.CalculateSequenceDurationFrames());
        if (durationX >= -1f && durationX <= contentRect.width + 1f)
        {
            painter.lineWidth = 2f;
            painter.strokeColor = new Color(0.74f, 0.62f, 0.32f, 1f);
            DrawLine(painter, durationX, 0f, durationX, height);
        }

        float y = 0f;
        IReadOnlyList<ActionSequenceDisplayTrack> tracks = state.DisplayTracks;
        for (int i = 0; i < tracks.Count; i++)
        {
            y += ActionSequenceViewUtility.GetTrackHeight(tracks[i].Snapshot);
            painter.lineWidth = 1f;
            painter.strokeColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            DrawLine(painter, 0f, y, contentRect.width, y);
        }
    }

    private static void DrawLine(Painter2D painter, float x1, float y1, float x2, float y2)
    {
        painter.BeginPath();
        painter.MoveTo(new Vector2(x1, y1));
        painter.LineTo(new Vector2(x2, y2));
        painter.Stroke();
    }
}
#endif
