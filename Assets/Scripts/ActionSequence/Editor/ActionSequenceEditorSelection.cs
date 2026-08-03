#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public readonly struct ActionSequenceEditorSelectionValue : IEquatable<ActionSequenceEditorSelectionValue>
{
    public ActionSequenceEditorSelectionValue(
        Object target,
        ActionSequenceEditorSelection.SelectionKind kind,
        string trackId,
        string clipId)
    {
        Target = target;
        Kind = kind;
        TrackId = trackId;
        ClipId = clipId;
    }

    public Object Target { get; }
    public ActionSequenceEditorSelection.SelectionKind Kind { get; }
    public string TrackId { get; }
    public string ClipId { get; }
    public bool IsEmpty => Target == null || Kind == ActionSequenceEditorSelection.SelectionKind.None;

    public bool Equals(ActionSequenceEditorSelectionValue other)
    {
        return Target == other.Target
            && Kind == other.Kind
            && string.Equals(TrackId, other.TrackId, StringComparison.Ordinal)
            && string.Equals(ClipId, other.ClipId, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is ActionSequenceEditorSelectionValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Target != null ? Target.GetHashCode() : 0;
            hash = (hash * 397) ^ (int)Kind;
            hash = (hash * 397) ^ (TrackId != null ? TrackId.GetHashCode() : 0);
            hash = (hash * 397) ^ (ClipId != null ? ClipId.GetHashCode() : 0);
            return hash;
        }
    }

    public static bool operator ==(ActionSequenceEditorSelectionValue left, ActionSequenceEditorSelectionValue right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ActionSequenceEditorSelectionValue left, ActionSequenceEditorSelectionValue right)
    {
        return !left.Equals(right);
    }
}

public static class ActionSequenceEditorSelection
{
    public enum SelectionKind
    {
        None,
        Sequence,
        Track,
        Clip,
    }

    public static event Action<ActionSequenceEditorSelectionValue> Changed;

    public static Object Target { get; private set; }
    public static SelectionKind Kind { get; private set; }
    public static string TrackId { get; private set; }
    public static string ClipId { get; private set; }
    public static int TrackIndex { get; private set; } = -1;
    public static int ClipIndex { get; private set; } = -1;
    public static string TrackPropertyPath { get; private set; }
    public static string ClipPropertyPath { get; private set; }

    public static ActionSequenceEditorSelectionValue Value =>
        new ActionSequenceEditorSelectionValue(Target, Kind, TrackId, ClipId);

    public static bool HasSelection => Kind != SelectionKind.None && Target != null;
    public static bool HasTrackSelection => Kind == SelectionKind.Track && Target != null;
    public static bool HasClipSelection => Kind == SelectionKind.Clip && Target != null;

    public static void SelectSequence(Object target)
    {
        SetValue(target, SelectionKind.Sequence, null, null, -1, -1, null, null);
    }

    public static void SelectTrack(Object target, string trackId)
    {
        if (!TryResolveValidTrack(target, trackId, out int trackIndex))
        {
            SelectSequence(target);
            return;
        }

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        SerializedProperty property = document.GetTrackProperty(trackIndex);
        SetValue(target, SelectionKind.Track, trackId, null, trackIndex, -1, property?.propertyPath, null);
    }

    public static void SelectClip(Object target, string clipId)
    {
        if (!TryResolveValidClip(target, clipId, out int trackIndex, out int clipIndex))
        {
            SelectSequence(target);
            return;
        }

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        ActionSequenceTrackSnapshot track = document.Tracks[trackIndex];
        if (track.Locked)
        {
            SelectTrack(target, track.EditorId);
            return;
        }

        SerializedProperty trackProperty = document.GetTrackProperty(trackIndex);
        SerializedProperty clipProperty = document.GetClipProperty(trackIndex, clipIndex);
        SetValue(target, SelectionKind.Clip, track.EditorId, clipId, trackIndex, clipIndex, trackProperty?.propertyPath, clipProperty?.propertyPath);
    }

    public static void Select(Object target, ActionSequenceEditorSelectionSuggestion suggestion)
    {
        switch (suggestion.Kind)
        {
            case ActionSequenceEditorDocumentItemKind.Track:
                SelectTrack(target, suggestion.EditorId);
                break;
            case ActionSequenceEditorDocumentItemKind.Clip:
                SelectClip(target, suggestion.EditorId);
                break;
            default:
                SelectSequence(target);
                break;
        }
    }

    public static void SetTrack(Object target, int trackIndex, string trackPropertyPath)
    {
        string stableId = TryReadEditorIdFromPropertyPath(target, trackPropertyPath);
        if (!string.IsNullOrEmpty(stableId))
        {
            SelectTrack(target, stableId);
            return;
        }

        SetValue(target, SelectionKind.Track, null, null, trackIndex, -1, trackPropertyPath, null);
    }

    public static void SetClip(Object target, int trackIndex, int clipIndex, string trackPropertyPath, string clipPropertyPath)
    {
        string trackId = TryReadEditorIdFromPropertyPath(target, trackPropertyPath);
        string clipId = TryReadEditorIdFromPropertyPath(target, clipPropertyPath);
        if (!string.IsNullOrEmpty(clipId))
        {
            SelectClip(target, clipId);
            return;
        }

        SetValue(target, SelectionKind.Clip, trackId, clipId, trackIndex, clipIndex, trackPropertyPath, clipPropertyPath);
    }

    public static void Clear()
    {
        SetValue(null, SelectionKind.None, null, null, -1, -1, null, null);
    }

    public static void ClearIfTarget(Object target)
    {
        if (Target == target)
            Clear();
    }

    public static void ClearIfTargetNot(Object target)
    {
        if (Target != null && Target != target)
            Clear();
    }

    public static bool IsTrackSelected(Object target, int trackIndex)
    {
        return Target == target && Kind == SelectionKind.Track && TrackIndex == trackIndex;
    }

    public static bool IsClipSelected(Object target, int trackIndex, int clipIndex)
    {
        return Target == target && Kind == SelectionKind.Clip && TrackIndex == trackIndex && ClipIndex == clipIndex;
    }

    public static SerializedProperty GetSelectedTrackProperty(SerializedObject serializedObject)
    {
        if (!HasTrackSelection || serializedObject == null || serializedObject.targetObject != Target)
            return null;

        if (TryResolveValidTrack(serializedObject.targetObject, TrackId, out int trackIndex))
            return GetTrackPropertyFallback(serializedObject, trackIndex, null);

        if (string.IsNullOrEmpty(TrackId))
            return GetTrackPropertyFallback(serializedObject, TrackIndex, TrackPropertyPath);

        return null;
    }

    public static SerializedProperty GetSelectedClipProperty(SerializedObject serializedObject)
    {
        if (!HasClipSelection || serializedObject == null || serializedObject.targetObject != Target)
            return null;

        if (TryResolveValidClip(serializedObject.targetObject, ClipId, out int trackIndex, out int clipIndex))
            return GetClipPropertyFallback(serializedObject, trackIndex, clipIndex);

        if (!string.IsNullOrEmpty(ClipId))
            return null;

        SerializedProperty trackProperty = GetTrackPropertyFallback(serializedObject, TrackIndex, TrackPropertyPath);
        SerializedProperty clipsProperty = trackProperty?.FindPropertyRelative("clips");
        if (clipsProperty == null || ClipIndex < 0 || ClipIndex >= clipsProperty.arraySize)
            return null;

        return clipsProperty.GetArrayElementAtIndex(ClipIndex);
    }

    public static SerializedProperty GetSelectedClipTrackProperty(SerializedObject serializedObject)
    {
        if (!HasClipSelection || serializedObject == null || serializedObject.targetObject != Target)
            return null;

        if (TryResolveValidClip(serializedObject.targetObject, ClipId, out int trackIndex, out _))
            return GetTrackPropertyFallback(serializedObject, trackIndex, null);

        return string.IsNullOrEmpty(ClipId)
            ? GetTrackPropertyFallback(serializedObject, TrackIndex, TrackPropertyPath)
            : null;
    }

    public static bool IsSelectedClipTrackLocked(SerializedObject serializedObject)
    {
        SerializedProperty trackProperty = GetSelectedClipTrackProperty(serializedObject);
        SerializedProperty lockedProperty = trackProperty?.FindPropertyRelative("locked");
        return lockedProperty != null && lockedProperty.boolValue;
    }

    public static SerializedProperty GetTracksProperty(SerializedObject serializedObject)
    {
        return GetSequenceDataProperty(serializedObject)?.FindPropertyRelative("tracks");
    }

    public static SerializedProperty GetLegacyClipsProperty(SerializedObject serializedObject)
    {
        return GetSequenceDataProperty(serializedObject)?.FindPropertyRelative("clips");
    }

    public static SerializedProperty GetSequenceDataProperty(SerializedObject serializedObject)
    {
        if (serializedObject == null)
            return null;

        if (serializedObject.targetObject is ActionAsset)
            return serializedObject.FindProperty("_sequenceData");

        if (serializedObject.targetObject is ActionSequenceAsset)
            return serializedObject.FindProperty("data");

        return null;
    }

    public static List<Type> GetTrackTypes()
    {
        var result = new List<Type>();
        TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ActionSequenceTrackDefinition>();

        foreach (Type type in types)
        {
            if (IsCreatableTrackType(type))
                result.Add(type);
        }

        result.Sort(CompareTrackTypes);
        return result;
    }

    public static List<Type> GetClipTypesForTrack(ActionSequenceTrackDefinition track)
    {
        var result = new List<Type>();
        if (track == null)
            return result;

        Type[] allowed = track.AllowedClipTypes;
        if (allowed == null)
            return result;

        for (int i = 0; i < allowed.Length; i++)
        {
            Type type = allowed[i];
            if (type == null || type.IsAbstract || type.IsGenericType)
                continue;

            result.Add(type);
        }

        result.Sort((a, b) => string.Compare(GetClipTypeDisplayName(a), GetClipTypeDisplayName(b), StringComparison.Ordinal));
        return result;
    }

    public static string GetTrackTypeDisplayName(Type type)
    {
        return ActionSequenceEditorTypeRegistry.GetTrackTypeDisplayName(type);
    }

    public static string GetClipTypeDisplayName(Type type)
    {
        return ActionSequenceEditorTypeRegistry.GetClipTypeDisplayName(type);
    }

    public static bool IsCreatableTrackType(Type type)
    {
        return ActionSequenceEditorTypeRegistry.IsCreatableTrackType(type);
    }

    private static void SetValue(
        Object target,
        SelectionKind kind,
        string trackId,
        string clipId,
        int trackIndex,
        int clipIndex,
        string trackPropertyPath,
        string clipPropertyPath)
    {
        var previous = Value;

        Target = target;
        Kind = kind;
        TrackId = trackId;
        ClipId = clipId;
        TrackIndex = trackIndex;
        ClipIndex = clipIndex;
        TrackPropertyPath = trackPropertyPath;
        ClipPropertyPath = clipPropertyPath;

        if (target != null)
            Selection.activeObject = target;

        var current = Value;
        if (previous != current)
            Changed?.Invoke(current);
    }

    private static bool TryResolveValidTrack(Object target, string trackId, out int trackIndex)
    {
        trackIndex = -1;
        if (target == null || !ActionSequenceEditorIdentity.IsValidEditorId(trackId))
            return false;

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        return document.IsSupported
            && document.ResolveTrack(trackId, out trackIndex) == ActionSequenceEditorResolveStatus.Found;
    }

    private static bool TryResolveValidClip(Object target, string clipId, out int trackIndex, out int clipIndex)
    {
        trackIndex = -1;
        clipIndex = -1;
        if (target == null || !ActionSequenceEditorIdentity.IsValidEditorId(clipId))
            return false;

        using ActionSequenceSerializedDocument document = ActionSequenceSerializedDocument.Open(target);
        return document.IsSupported
            && document.ResolveClip(clipId, out trackIndex, out clipIndex) == ActionSequenceEditorResolveStatus.Found;
    }

    private static SerializedProperty GetTrackPropertyFallback(SerializedObject serializedObject, int trackIndex, string propertyPath)
    {
        SerializedProperty property = !string.IsNullOrEmpty(propertyPath) ? serializedObject.FindProperty(propertyPath) : null;
        if (property != null)
            return property;

        SerializedProperty tracksProperty = GetTracksProperty(serializedObject);
        if (tracksProperty == null || trackIndex < 0 || trackIndex >= tracksProperty.arraySize)
            return null;

        return tracksProperty.GetArrayElementAtIndex(trackIndex);
    }

    private static SerializedProperty GetClipPropertyFallback(SerializedObject serializedObject, int trackIndex, int clipIndex)
    {
        SerializedProperty trackProperty = GetTrackPropertyFallback(serializedObject, trackIndex, null);
        SerializedProperty clipsProperty = trackProperty?.FindPropertyRelative("clips");
        if (clipsProperty == null || clipIndex < 0 || clipIndex >= clipsProperty.arraySize)
            return null;

        return clipsProperty.GetArrayElementAtIndex(clipIndex);
    }

    private static string TryReadEditorIdFromPropertyPath(Object target, string propertyPath)
    {
        if (target == null || string.IsNullOrEmpty(propertyPath))
            return null;

        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        return property?.FindPropertyRelative("editorId")?.stringValue;
    }

    private static int CompareTrackTypes(Type a, Type b)
    {
        int phaseCompare = GetTrackPhaseSortValue(a).CompareTo(GetTrackPhaseSortValue(b));
        if (phaseCompare != 0)
            return phaseCompare;

        return string.Compare(GetTrackTypeDisplayName(a), GetTrackTypeDisplayName(b), StringComparison.Ordinal);
    }

    private static int GetTrackPhaseSortValue(Type type)
    {
        try
        {
            if (Activator.CreateInstance(type) is ActionSequenceTrackDefinition track)
                return (int)track.Phase;
        }
        catch
        {
            // Ignore custom editor-only construction failures and sort them last.
        }

        return int.MaxValue;
    }
}
#endif
