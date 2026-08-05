#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

internal static class ActionSequenceViewUtility
{
    public const float RulerHeight = 28f;
    public const float TrackHeight = 34f;
    public const float CollapsedTrackHeight = 26f;
    public const float ClipHeight = 22f;
    public const float ClipTop = 6f;
    public const float MinimumClipWidth = 4f;

    public static string GetTrackDisplayName(ActionSequenceTrackSnapshot track)
    {
        if (track.IsNull)
            return "Null Track";
        if (track.MissingType)
            return !string.IsNullOrEmpty(track.MissingTypeInfo.DisplayName) ? track.MissingTypeInfo.DisplayName : "Missing Track Type";
        if (!string.IsNullOrWhiteSpace(track.DisplayName))
            return track.DisplayName;

        return NicifyTypeName(track.Type, "Track", "ActionSequence", "Track");
    }

    public static string GetTrackTypeDisplayName(ActionSequenceTrackSnapshot track)
    {
        if (track.IsNull)
            return "Null";
        if (track.MissingType)
            return !string.IsNullOrEmpty(track.MissingTypeInfo.DisplayName) ? "Missing: " + track.MissingTypeInfo.DisplayName : "Missing Type";

        return NicifyTypeName(track.Type, "Track", "ActionSequence");
    }

    public static string GetFullTypeName(Type type)
    {
        return type != null ? type.FullName : string.Empty;
    }

    public static string GetClipDisplayName(ActionSequenceClipSnapshot clip)
    {
        if (clip.IsNull)
            return "Null Clip";
        if (clip.MissingType)
            return !string.IsNullOrEmpty(clip.MissingTypeInfo.DisplayName) ? clip.MissingTypeInfo.DisplayName : "Missing Clip Type";
        if (!string.IsNullOrWhiteSpace(clip.DisplayName))
            return clip.DisplayName;

        return NicifyTypeName(clip.Type, "Clip", "ActionSequence", "ClipDefinition");
    }

    public static string GetPhaseClass(ActionSequenceClipPhase phase)
    {
        return phase switch
        {
            ActionSequenceClipPhase.State => "asv2-phase-state",
            ActionSequenceClipPhase.Animation => "asv2-phase-animation",
            ActionSequenceClipPhase.Motion => "asv2-phase-motion",
            ActionSequenceClipPhase.HitBox => "asv2-phase-hitbox",
            ActionSequenceClipPhase.Cleanup => "asv2-phase-cleanup",
            _ => "asv2-phase-unknown",
        };
    }

    public static float GetTrackHeight(ActionSequenceTrackSnapshot track)
    {
        return track.Collapsed ? CollapsedTrackHeight : TrackHeight;
    }

    public static void SetDisplay(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static string NicifyTypeName(Type type, string fallback, params string[] removals)
    {
        if (type == null)
            return fallback;

        string text = type.Name;
        for (int i = 0; i < removals.Length; i++)
            text = text.Replace(removals[i], string.Empty);

        if (string.IsNullOrEmpty(text))
            return fallback;

        var builder = new System.Text.StringBuilder(text.Length + 8);
        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(text[i - 1]))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }
}
#endif
