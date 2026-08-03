#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

public sealed class ActionSequenceEditorTrackTypeInfo
{
    public ActionSequenceEditorTrackTypeInfo(Type type, string displayName, ActionSequenceClipPhase phase)
    {
        Type = type;
        DisplayName = displayName;
        Phase = phase;
    }

    public Type Type { get; }
    public string DisplayName { get; }
    public ActionSequenceClipPhase Phase { get; }
}

public sealed class ActionSequenceEditorClipTypeInfo
{
    public ActionSequenceEditorClipTypeInfo(Type type, string displayName, ActionSequenceClipPhase phase)
    {
        Type = type;
        DisplayName = displayName;
        Phase = phase;
    }

    public Type Type { get; }
    public string DisplayName { get; }
    public ActionSequenceClipPhase Phase { get; }
}

public static class ActionSequenceEditorTypeRegistry
{
    private static List<ActionSequenceEditorTrackTypeInfo> trackTypes;

    public static IReadOnlyList<ActionSequenceEditorTrackTypeInfo> TrackTypes
    {
        get
        {
            if (trackTypes == null)
                trackTypes = BuildTrackTypes();

            return trackTypes;
        }
    }

    public static bool IsCreatableTrackType(Type type)
    {
        if (type == null)
            return false;
        if (!typeof(ActionSequenceTrackDefinition).IsAssignableFrom(type))
            return false;
        if (type.IsAbstract || type.IsGenericType)
            return false;
        if (!type.IsPublic || type.IsNested)
            return false;
        if (!type.IsSerializable)
            return false;

        return type.GetConstructor(Type.EmptyTypes) != null;
    }

    public static bool IsCreatableClipType(Type type)
    {
        if (type == null)
            return false;
        if (!typeof(ActionSequenceClipDefinition).IsAssignableFrom(type))
            return false;
        if (type.IsAbstract || type.IsGenericType)
            return false;
        if (!type.IsPublic || type.IsNested)
            return false;
        if (!type.IsSerializable)
            return false;

        return type.GetConstructor(Type.EmptyTypes) != null;
    }

    public static IReadOnlyList<ActionSequenceEditorClipTypeInfo> GetClipTypesForTrack(ActionSequenceTrackDefinition track)
    {
        var result = new List<ActionSequenceEditorClipTypeInfo>();
        if (track == null || track.AllowedClipTypes == null)
            return result;

        Type[] allowedTypes = track.AllowedClipTypes;
        for (int i = 0; i < allowedTypes.Length; i++)
        {
            Type type = allowedTypes[i];
            if (!IsCreatableClipType(type))
                continue;
            if (!track.AllowsClipType(type))
                continue;

            ActionSequenceClipDefinition clip = CreateClip(type);
            if (clip == null || clip.Phase != track.Phase)
                continue;

            result.Add(new ActionSequenceEditorClipTypeInfo(type, GetClipTypeDisplayName(type), clip.Phase));
        }

        result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
        return result;
    }

    public static ActionSequenceTrackDefinition CreateTrack(Type type)
    {
        return IsCreatableTrackType(type) ? Activator.CreateInstance(type) as ActionSequenceTrackDefinition : null;
    }

    public static ActionSequenceClipDefinition CreateClip(Type type)
    {
        return IsCreatableClipType(type) ? Activator.CreateInstance(type) as ActionSequenceClipDefinition : null;
    }

    public static string GetTrackTypeDisplayName(Type type)
    {
        if (type == null)
            return "Track";

        string typeName = type.Name.Replace("ActionSequence", string.Empty).Replace("Track", string.Empty);
        return ObjectNames.NicifyVariableName(typeName);
    }

    public static string GetClipTypeDisplayName(Type type)
    {
        if (type == null)
            return "Clip";

        string typeName = type.Name.Replace("ActionSequence", string.Empty).Replace("ClipDefinition", string.Empty);
        return ObjectNames.NicifyVariableName(typeName);
    }

    private static List<ActionSequenceEditorTrackTypeInfo> BuildTrackTypes()
    {
        var result = new List<ActionSequenceEditorTrackTypeInfo>();
        TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ActionSequenceTrackDefinition>();

        foreach (Type type in types)
        {
            if (!IsCreatableTrackType(type))
                continue;

            ActionSequenceTrackDefinition track = CreateTrack(type);
            if (track == null)
                continue;

            result.Add(new ActionSequenceEditorTrackTypeInfo(type, GetTrackTypeDisplayName(type), track.Phase));
        }

        result.Sort(CompareTrackTypes);
        return result;
    }

    private static int CompareTrackTypes(ActionSequenceEditorTrackTypeInfo a, ActionSequenceEditorTrackTypeInfo b)
    {
        int phaseCompare = a.Phase.CompareTo(b.Phase);
        if (phaseCompare != 0)
            return phaseCompare;

        return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
    }
}
#endif
