#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

internal readonly struct ActionVisibleFrameRange
{
    internal ActionVisibleFrameRange(int first, int lastExclusive)
    {
        First = first;
        LastExclusive = lastExclusive;
    }

    internal int First { get; }
    internal int LastExclusive { get; }
}

internal readonly struct ActionEntryGeometry
{
    internal ActionEntryGeometry(float left, float width, bool overflow)
        : this(left, width, 0f, width, overflow)
    {
    }

    internal ActionEntryGeometry(float left, float width, float visualLeft, float visualWidth, bool overflow)
    {
        Left = left;
        Width = width;
        VisualLeft = visualLeft;
        VisualWidth = visualWidth;
        Overflow = overflow;
    }

    internal float Left { get; }
    internal float Width { get; }
    internal float VisualLeft { get; }
    internal float VisualWidth { get; }
    internal bool Overflow { get; }
}

/// <summary>
/// Session-only horizontal time state. It owns the visible range; pixel scale is always derived from
/// the current viewport so the timeline never exposes an unpainted horizontal canvas.
/// </summary>
internal sealed class ActionTimeViewport
{
    internal const double MaximumPixelsPerFrame = 64d;
    private const double MinimumWorkspaceFrames = 120d;

    internal double VisibleStartFrame { get; private set; }
    internal double VisibleSpanFrames { get; private set; } = 60d;
    internal double VisibleEndFrame => VisibleStartFrame + VisibleSpanFrames;
    internal double WorkspaceEndFrame { get; private set; } = MinimumWorkspaceFrames;
    internal double NavigationEndFrame { get; private set; } = 60d;
    internal double NavigatorDomainEndFrame => Math.Max(NavigationEndFrame, VisibleEndFrame);
    internal float ViewportWidth { get; private set; } = 1f;
    internal double UsableWidth => Math.Max(1d, ViewportWidth - ActionTimelineGeometry.HorizontalPresentationInset * 2d);
    internal double PixelsPerFrame => UsableWidth / Math.Max(VisibleSpanFrames, double.Epsilon);
    internal double MinimumVisibleSpan => Math.Max(1d, UsableWidth / MaximumPixelsPerFrame);

    internal void Initialize(int horizonFrames, float viewportWidth, double restoredStart, double restoredSpan,
        double restoredWorkspace, double restoredNavigationEnd, bool restore)
    {
        ViewportWidth = SanitizeWidth(viewportWidth);
        WorkspaceEndFrame = InitialWorkspace(horizonFrames);
        NavigationEndFrame = Math.Max(1d, horizonFrames);
        if (restore && IsFinite(restoredWorkspace))
            WorkspaceEndFrame = Math.Max(WorkspaceEndFrame, restoredWorkspace);
        if (restore && IsFinite(restoredNavigationEnd))
            NavigationEndFrame = Math.Max(NavigationEndFrame, Math.Min(WorkspaceEndFrame, restoredNavigationEnd));

        if (restore && IsFinite(restoredStart) && IsFinite(restoredSpan) && restoredSpan > 0d)
            SetRange(restoredStart, restoredSpan);
        else
            Fit(horizonFrames);
    }

    internal void UpdateWorkspace(int horizonFrames)
    {
        WorkspaceEndFrame = Math.Max(WorkspaceEndFrame, InitialWorkspace(horizonFrames));
        NavigationEndFrame = Math.Max(NavigationEndFrame, Math.Max(1d, horizonFrames));
        SetRange(VisibleStartFrame, VisibleSpanFrames);
    }

    internal void SetViewportWidth(float viewportWidth)
    {
        ViewportWidth = SanitizeWidth(viewportWidth);
        SetRange(VisibleStartFrame, VisibleSpanFrames);
    }

    internal void Fit(double horizonFrames)
    {
        NavigationEndFrame = Math.Max(1d, Math.Min(WorkspaceEndFrame, horizonFrames));
        SetRange(0d, Math.Max(1d, Math.Min(WorkspaceEndFrame, horizonFrames)));
    }

    internal void SetRange(double startFrame, double spanFrames)
    {
        double minimum = MinimumVisibleSpan;
        double span = IsFinite(spanFrames) ? spanFrames : WorkspaceEndFrame;
        VisibleSpanFrames = Math.Max(minimum, Math.Min(WorkspaceEndFrame, span));
        double maximumStart = Math.Max(0d, WorkspaceEndFrame - VisibleSpanFrames);
        double start = IsFinite(startFrame) ? startFrame : 0d;
        VisibleStartFrame = Math.Max(0d, Math.Min(maximumStart, start));
    }

    internal void ZoomAround(double anchorFrame, double anchorRatio, double factor)
    {
        double ratio = Math.Max(0d, Math.Min(1d, IsFinite(anchorRatio) ? anchorRatio : 0.5d));
        double span = VisibleSpanFrames * (IsFinite(factor) ? factor : 1d);
        span = Math.Max(MinimumVisibleSpan, Math.Min(WorkspaceEndFrame, span));
        SetRange(anchorFrame - ratio * span, span);
    }

    internal void PanPixels(double pixels)
    {
        if (!IsFinite(pixels))
            return;
        SetRange(VisibleStartFrame + pixels / Math.Max(PixelsPerFrame, double.Epsilon), VisibleSpanFrames);
        ExtendNavigationForPan();
    }

    internal void ExtendNavigationForPan()
    {
        if (VisibleStartFrame <= 0d || VisibleEndFrame <= NavigationEndFrame)
            return;
        double reserve = Math.Max(30d, VisibleSpanFrames * 0.25d);
        double requested = Math.Max(VisibleEndFrame, NavigationEndFrame + reserve);
        double stepped = Math.Ceiling(requested / 30d) * 30d;
        NavigationEndFrame = Math.Min(WorkspaceEndFrame, Math.Max(NavigationEndFrame, stepped));
    }

    internal double PixelToFrame(double pixel)
    {
        double x = IsFinite(pixel) ? pixel : ActionTimelineGeometry.HorizontalPresentationInset;
        return VisibleStartFrame +
               (x - ActionTimelineGeometry.HorizontalPresentationInset) / Math.Max(PixelsPerFrame, double.Epsilon);
    }

    private static double InitialWorkspace(int horizonFrames)
    {
        return Math.Max(MinimumWorkspaceFrames, Math.Max(1d, horizonFrames) * 2d);
    }

    private static float SanitizeWidth(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) || value < 1f ? 1f : value;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

/// <summary>Finite presentation geometry for an integer-frame Action timeline.</summary>
internal sealed class ActionTimelineGeometry
{
    internal const float MaxPixelsPerFrame = 64f;
    internal const float MaximumLayoutPixels = 4194304f;
    internal const float PointHitWidth = 14f;
    internal const float HorizontalPresentationInset = 8f;
    internal const float InvalidMinimumWidth = 52f;

    internal ActionTimelineGeometry(int durationFrames, int horizonFrames, int gameplayLaneCount,
        double visibleStartFrame, double visibleSpanFrames, float viewportWidth)
    {
        DurationFrames = Mathf.Max(1, durationFrames);
        HorizonFrames = Mathf.Max(60, horizonFrames);
        GameplayLaneCount = Mathf.Max(0, gameplayLaneCount);
        VisibleStartFrame = IsFinite(visibleStartFrame) ? Math.Max(0d, visibleStartFrame) : 0d;
        VisibleSpanFrames = IsFinite(visibleSpanFrames) ? Math.Max(1d, visibleSpanFrames) : HorizonFrames;
        ContentWidth = IsFinite(viewportWidth) ? Mathf.Max(1f, viewportWidth) : 1f;
        double usableWidth = Math.Max(1d, ContentWidth - HorizontalPresentationInset * 2d);
        PixelsPerFrame = (float)Math.Min(MaxPixelsPerFrame, usableWidth / VisibleSpanFrames);
        ContentHeight = ClampLayout(
            ActionEditorTheme.AnimationLaneHeight + (double)GameplayLaneCount * ActionEditorTheme.GameplayLaneHeight,
            1f);
    }

    internal int DurationFrames { get; }
    internal int HorizonFrames { get; }
    internal int GameplayLaneCount { get; }
    internal double VisibleStartFrame { get; }
    internal double VisibleSpanFrames { get; }
    internal double VisibleEndFrame => VisibleStartFrame + VisibleSpanFrames;
    internal float PixelsPerFrame { get; }
    internal float ContentWidth { get; }
    internal float ContentHeight { get; }
    internal bool HasHorizontalOverflow => false;

    internal float FrameToPixel(long frame, out bool overflow)
    {
        return FramePositionToPixel(frame, out overflow);
    }

    internal float FramePositionToPixel(double frame, out bool overflow)
    {
        double raw = HorizontalPresentationInset + (frame - VisibleStartFrame) * PixelsPerFrame;
        overflow = Math.Abs(raw) > MaximumLayoutPixels || double.IsNaN(raw) || double.IsInfinity(raw);
        if (!overflow)
            return (float)raw;
        return frame < VisibleStartFrame ? 0f : ContentWidth;
    }

    internal int PixelToFrame(float pixel)
    {
        if (!IsFinite(pixel))
            return 0;
        double frame = Math.Round(
            VisibleStartFrame + (pixel - HorizontalPresentationInset) / Math.Max(0.000001f, PixelsPerFrame),
            MidpointRounding.AwayFromZero);
        return frame >= int.MaxValue ? int.MaxValue : Mathf.Max(0, (int)frame);
    }

    internal float LaneTop(int laneIndex)
    {
        if (laneIndex < 0)
            return 0f;
        return ClampLayout(
            ActionEditorTheme.AnimationLaneHeight + (double)laneIndex * ActionEditorTheme.GameplayLaneHeight,
            0f);
    }

    internal ActionEntryGeometry EntryRect(ActionDocumentEntry entry)
    {
        if (entry == null)
            return new ActionEntryGeometry(0f, InvalidMinimumWidth, false);

        float start = FrameToPixel(Math.Max(0, entry.StartFrame), out bool startOverflow);
        if (entry.Source is PointGameplayItem)
        {
            bool pointOverflow = startOverflow;
            float left = pointOverflow
                ? Mathf.Max(0f, ContentWidth - PointHitWidth)
                : start - PointHitWidth * 0.5f;
            return new ActionEntryGeometry(left, PointHitWidth, pointOverflow);
        }
        float end = FrameToPixel(Math.Max(0L, entry.RawEndFrameExclusive), out bool endOverflow);
        bool invalid = entry.DisplayState != ActionEntryDisplayState.Normal;
        float semanticWidth = Mathf.Max(0f, end - start);
        if (!invalid && !startOverflow && !endOverflow)
        {
            float visualWidth = Mathf.Max(1f, semanticWidth);
            float hitWidth = Mathf.Max(PointHitWidth, visualWidth);
            float hitLeft = start - (hitWidth - visualWidth) * 0.5f;
            return new ActionEntryGeometry(hitLeft, hitWidth, start - hitLeft, visualWidth, false);
        }

        float minimum = InvalidMinimumWidth;
        float width = Mathf.Max(minimum, semanticWidth);
        bool overflow = startOverflow || endOverflow || start + width > MaximumLayoutPixels;
        if (overflow)
        {
            width = Mathf.Min(width, InvalidMinimumWidth);
            start = entry.StartFrame < VisibleStartFrame ? 0f : Mathf.Max(0f, ContentWidth - width);
        }
        else
        {
            float clippedStart = Mathf.Max(-minimum, start);
            float clippedEnd = Mathf.Min(ContentWidth + minimum, start + width);
            start = clippedStart;
            width = Mathf.Max(minimum, clippedEnd - clippedStart);
        }
        return new ActionEntryGeometry(start, width, overflow);
    }

    internal ActionVisibleFrameRange VisibleFrames(int paddingFrames = 2)
    {
        long padding = Mathf.Max(0, paddingFrames);
        long firstValue = (long)SaturatingFloor(VisibleStartFrame) - padding;
        long lastValue = (long)SaturatingCeil(VisibleEndFrame) + padding + 1L;
        int first = (int)Math.Max(0L, Math.Min(int.MaxValue, firstValue));
        int last = (int)Math.Max(first, Math.Min(int.MaxValue, lastValue));
        return new ActionVisibleFrameRange(first, last);
    }

    internal float ClampVerticalOffset(float value, float viewportHeight)
    {
        float height = IsFinite(viewportHeight) ? Mathf.Max(0f, viewportHeight) : 0f;
        return Mathf.Clamp(IsFinite(value) ? value : 0f, 0f, Mathf.Max(0f, ContentHeight - height));
    }

    private static float ClampLayout(double value, float minimum)
    {
        if (double.IsNaN(value) || value <= minimum)
            return minimum;
        return value >= MaximumLayoutPixels ? MaximumLayoutPixels : (float)value;
    }

    private static int SaturatingFloor(double value)
    {
        if (value <= 0d || double.IsNaN(value)) return 0;
        if (value >= int.MaxValue) return int.MaxValue;
        return (int)Math.Floor(value);
    }

    private static int SaturatingCeil(double value)
    {
        if (value <= 0d || double.IsNaN(value)) return 0;
        if (value >= int.MaxValue) return int.MaxValue;
        return (int)Math.Ceiling(value);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

internal static class ActionEditorTheme
{
    internal const string StylePath = "Assets/Scripts/Action/Editor/Styles/ActionEditorStyles.uss";
    internal const float RulerHeight = 24f;
    internal const float AnimationLaneHeight = 36f;
    internal const float GameplayLaneHeight = 32f;
    internal const float DefaultHeaderWidth = 220f;
    internal const float MinHeaderWidth = 180f;
    internal const float MaxHeaderWidth = 320f;

    internal static Color GridLine => Gray(EditorGUIUtility.isProSkin ? 0.30f : 0.67f, 0.72f);
    internal static Color MinorGridLine => Gray(EditorGUIUtility.isProSkin ? 0.27f : 0.72f, 0.36f);
    internal static Color LaneDivider => Gray(EditorGUIUtility.isProSkin ? 0.32f : 0.61f);
    internal static Color RulerTick => Gray(EditorGUIUtility.isProSkin ? 0.48f : 0.40f);
    internal static Color MinorRulerTick => Gray(EditorGUIUtility.isProSkin ? 0.36f : 0.56f);
    internal static Color RulerText => EditorStyles.miniLabel.normal.textColor;
    internal static Color OriginLine => Gray(EditorGUIUtility.isProSkin ? 0.65f : 0.38f, 0.8f);
    internal static Color HorizonLine => Gray(EditorGUIUtility.isProSkin ? 0.60f : 0.45f, 0.55f);
    private static Color Gray(float value, float alpha = 1f) => new Color(value, value, value, alpha);

    internal static void Apply(VisualElement root, string windowClass)
    {
        root.AddToClassList("action-editor-root");
        root.EnableInClassList("action-editor-light", !EditorGUIUtility.isProSkin);
        root.AddToClassList(windowClass);
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
        if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            root.styleSheets.Add(styleSheet);
    }

}

internal static class ActionEditorChrome
{
    internal static VisualElement ContextBar(
        ActionAsset action,
        Func<ActionAsset, bool> setAction,
        params (string text, Action clicked)[] navigation)
    {
        var bar = new Toolbar();
        bar.AddToClassList("action-editor-context-bar");
        var field = new ObjectField("Action")
        {
            objectType = typeof(ActionAsset),
            allowSceneObjects = false,
            value = action,
        };
        field.AddToClassList("action-editor-action-field");
        field.SetEnabled(setAction != null);
        field.RegisterValueChangedCallback(evt =>
        {
            if (setAction != null && !setAction(evt.newValue as ActionAsset))
                field.SetValueWithoutNotify(ActionEditorContext.Shared.CurrentAction);
        });
        bar.Add(field);

        var spacer = new VisualElement();
        spacer.AddToClassList("action-editor-spacer");
        bar.Add(spacer);
        foreach ((string text, Action clicked) item in navigation)
        {
            var button = new ToolbarButton(item.clicked) { text = item.text };
            button.AddToClassList("action-editor-chrome-button");
            bar.Add(button);
        }
        return bar;
    }

    internal static VisualElement EmptyState(string title, string body)
    {
        var host = new VisualElement();
        host.AddToClassList("action-editor-empty-state");
        var titleLabel = new Label(title);
        titleLabel.AddToClassList("action-editor-empty-title");
        host.Add(titleLabel);
        var bodyLabel = new Label(body);
        bodyLabel.AddToClassList("action-editor-empty-body");
        host.Add(bodyLabel);
        return host;
    }

    internal static void SyncActionField(VisualElement root)
    {
        ObjectField field = root?.Q<ObjectField>(className: "action-editor-action-field");
        if (field != null && field.value != ActionEditorContext.Shared.CurrentAction)
            field.SetValueWithoutNotify(ActionEditorContext.Shared.CurrentAction);
    }
}

#endif
