#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class ActionSequenceRulerView : VisualElement
{
    private readonly List<Label> labels = new List<Label>();
    private ActionSequenceTimelineTransform timelineTransform;

    public ActionSequenceRulerView()
    {
        AddToClassList("asv2-ruler");
        style.height = ActionSequenceViewUtility.RulerHeight;
        pickingMode = PickingMode.Ignore;
        generateVisualContent += DrawRuler;
    }

    public void Refresh(ActionSequenceTimelineTransform newTransform)
    {
        timelineTransform = newTransform;
        MarkDirtyRepaint();
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (timelineTransform == null)
        {
            SetLabelCount(0);
            return;
        }

        IReadOnlyList<ActionSequenceTimelineTick> ticks = timelineTransform.BuildVisibleTicks();
        int labelledCount = 0;
        for (int i = 0; i < ticks.Count; i++)
        {
            if (ticks[i].Labelled)
                labelledCount++;
        }

        SetLabelCount(labelledCount);
        int labelIndex = 0;
        for (int i = 0; i < ticks.Count; i++)
        {
            ActionSequenceTimelineTick tick = ticks[i];
            if (!tick.Labelled)
                continue;

            Label label = labels[labelIndex++];
            label.text = tick.Frame.ToString();
            label.style.left = tick.X + 3f;
            label.style.top = 2f;
        }
    }

    private void SetLabelCount(int count)
    {
        while (labels.Count < count)
        {
            var label = new Label();
            label.AddToClassList("asv2-ruler-label");
            label.style.position = Position.Absolute;
            labels.Add(label);
            Add(label);
        }

        for (int i = 0; i < labels.Count; i++)
            ActionSequenceViewUtility.SetDisplay(labels[i], i < count);
    }

    private void DrawRuler(MeshGenerationContext context)
    {
        if (timelineTransform == null)
            return;

        Painter2D painter = context.painter2D;
        painter.lineWidth = 1f;
        painter.strokeColor = new Color(0.26f, 0.26f, 0.26f, 1f);
        DrawLine(painter, 0f, contentRect.height - 1f, contentRect.width, contentRect.height - 1f);

        IReadOnlyList<ActionSequenceTimelineTick> ticks = timelineTransform.BuildVisibleTicks();
        for (int i = 0; i < ticks.Count; i++)
        {
            ActionSequenceTimelineTick tick = ticks[i];
            if (tick.X < -1f || tick.X > contentRect.width + 1f)
                continue;

            float height = tick.Labelled ? 18f : tick.Major ? 12f : 7f;
            painter.strokeColor = tick.Labelled ? new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(0.36f, 0.36f, 0.36f, 1f);
            DrawLine(painter, tick.X, contentRect.height, tick.X, contentRect.height - height);
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
