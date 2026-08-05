#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public enum ActionSequenceEditorDocumentStatus
{
    Supported,
    UnsupportedTarget,
}

public enum ActionSequenceEditorResolveStatus
{
    Found,
    NotFound,
    MissingId,
    Ambiguous,
}

public enum ActionSequenceEditorDocumentItemKind
{
    Sequence,
    Track,
    Clip,
    LegacyClip,
}

public readonly struct ActionSequenceEditorItemLocation
{
    public ActionSequenceEditorItemLocation(
        ActionSequenceEditorDocumentItemKind kind,
        string editorId,
        int trackIndex,
        int clipIndex,
        int legacyClipIndex)
    {
        Kind = kind;
        EditorId = editorId;
        TrackIndex = trackIndex;
        ClipIndex = clipIndex;
        LegacyClipIndex = legacyClipIndex;
    }

    public ActionSequenceEditorDocumentItemKind Kind { get; }
    public string EditorId { get; }
    public int TrackIndex { get; }
    public int ClipIndex { get; }
    public int LegacyClipIndex { get; }
}

public sealed class ActionSequenceSerializedDocument : IDisposable
{
    private readonly List<ActionSequenceTrackSnapshot> tracks = new List<ActionSequenceTrackSnapshot>();
    private readonly List<ActionSequenceClipSnapshot> legacyClips = new List<ActionSequenceClipSnapshot>();
    private readonly Dictionary<string, List<ActionSequenceEditorItemLocation>> idMap =
        new Dictionary<string, List<ActionSequenceEditorItemLocation>>(StringComparer.Ordinal);
    private readonly Dictionary<long, ActionSequenceMissingManagedReferenceInfo> missingTypesByReferenceId =
        new Dictionary<long, ActionSequenceMissingManagedReferenceInfo>();
    private string snapshotFingerprint;

    private ActionSequenceSerializedDocument(Object target, SerializedObject serializedObject, string rootPropertyPath)
    {
        Target = target;
        SerializedObject = serializedObject;
        RootPropertyPath = rootPropertyPath;
        Status = ActionSequenceEditorDocumentStatus.Supported;
    }

    private ActionSequenceSerializedDocument(ActionSequenceEditorDocumentStatus status)
    {
        Status = status;
    }

    public Object Target { get; }
    public SerializedObject SerializedObject { get; }
    public string RootPropertyPath { get; }
    public ActionSequenceEditorDocumentStatus Status { get; }
    public bool IsSupported => Status == ActionSequenceEditorDocumentStatus.Supported;
    public int Revision { get; private set; }
    public ActionSequenceSnapshot Sequence { get; private set; }
    public IReadOnlyList<ActionSequenceTrackSnapshot> Tracks => tracks;
    public IReadOnlyList<ActionSequenceClipSnapshot> LegacyClips => legacyClips;

    public static ActionSequenceSerializedDocument Open(Object target)
    {
        if (!TryGetRootPath(target, out string rootPath))
            return new ActionSequenceSerializedDocument(ActionSequenceEditorDocumentStatus.UnsupportedTarget);

        var document = new ActionSequenceSerializedDocument(target, new SerializedObject(target), rootPath);
        document.Refresh();
        return document;
    }

    public bool Refresh()
    {
        if (!IsSupported)
            return false;

        SerializedObject.UpdateIfRequiredOrScript();
        RebuildSnapshots();
        string newFingerprint = BuildSnapshotFingerprint();
        if (Revision > 0 && string.Equals(snapshotFingerprint, newFingerprint, StringComparison.Ordinal))
            return false;

        snapshotFingerprint = newFingerprint;
        Revision++;
        return true;
    }

    public SerializedProperty GetRootProperty()
    {
        return IsSupported ? SerializedObject.FindProperty(RootPropertyPath) : null;
    }

    public SerializedProperty GetTracksProperty()
    {
        return GetRootProperty()?.FindPropertyRelative("tracks");
    }

    public SerializedProperty GetLegacyClipsProperty()
    {
        return GetRootProperty()?.FindPropertyRelative("clips");
    }

    public SerializedProperty GetTrackProperty(int trackIndex)
    {
        SerializedProperty tracksProperty = GetTracksProperty();
        if (tracksProperty == null || trackIndex < 0 || trackIndex >= tracksProperty.arraySize)
            return null;

        return tracksProperty.GetArrayElementAtIndex(trackIndex);
    }

    public SerializedProperty GetTrackClipsProperty(int trackIndex)
    {
        return GetTrackProperty(trackIndex)?.FindPropertyRelative("clips");
    }

    public SerializedProperty GetClipProperty(int trackIndex, int clipIndex)
    {
        SerializedProperty clipsProperty = GetTrackClipsProperty(trackIndex);
        if (clipsProperty == null || clipIndex < 0 || clipIndex >= clipsProperty.arraySize)
            return null;

        return clipsProperty.GetArrayElementAtIndex(clipIndex);
    }

    public SerializedProperty GetLegacyClipProperty(int legacyClipIndex)
    {
        SerializedProperty clipsProperty = GetLegacyClipsProperty();
        if (clipsProperty == null || legacyClipIndex < 0 || legacyClipIndex >= clipsProperty.arraySize)
            return null;

        return clipsProperty.GetArrayElementAtIndex(legacyClipIndex);
    }

    public ActionSequenceEditorResolveStatus ResolveTrack(string editorId, out int trackIndex)
    {
        trackIndex = -1;
        ActionSequenceEditorResolveStatus status = Resolve(editorId, ActionSequenceEditorDocumentItemKind.Track, out ActionSequenceEditorItemLocation location);
        if (status == ActionSequenceEditorResolveStatus.Found)
            trackIndex = location.TrackIndex;

        return status;
    }

    public ActionSequenceEditorResolveStatus ResolveClip(string editorId, out int trackIndex, out int clipIndex)
    {
        trackIndex = -1;
        clipIndex = -1;
        ActionSequenceEditorResolveStatus status = Resolve(editorId, ActionSequenceEditorDocumentItemKind.Clip, out ActionSequenceEditorItemLocation location);
        if (status == ActionSequenceEditorResolveStatus.Found)
        {
            trackIndex = location.TrackIndex;
            clipIndex = location.ClipIndex;
        }

        return status;
    }

    public ActionSequenceEditorResolveStatus ResolveLegacyClip(string editorId, out int legacyClipIndex)
    {
        legacyClipIndex = -1;
        ActionSequenceEditorResolveStatus status = Resolve(editorId, ActionSequenceEditorDocumentItemKind.LegacyClip, out ActionSequenceEditorItemLocation location);
        if (status == ActionSequenceEditorResolveStatus.Found)
            legacyClipIndex = location.LegacyClipIndex;

        return status;
    }

    public ActionSequenceEditorResolveStatus Resolve(
        string editorId,
        ActionSequenceEditorDocumentItemKind kind,
        out ActionSequenceEditorItemLocation location)
    {
        location = default;
        if (string.IsNullOrEmpty(editorId))
            return ActionSequenceEditorResolveStatus.MissingId;

        if (!idMap.TryGetValue(editorId, out List<ActionSequenceEditorItemLocation> locations))
            return ActionSequenceEditorResolveStatus.NotFound;

        if (locations.Count > 1)
            return ActionSequenceEditorResolveStatus.Ambiguous;

        if (locations[0].Kind != kind)
            return ActionSequenceEditorResolveStatus.NotFound;

        location = locations[0];
        return ActionSequenceEditorResolveStatus.Found;
    }

    public bool HasDuplicateId(string editorId)
    {
        return !string.IsNullOrEmpty(editorId) && idMap.TryGetValue(editorId, out List<ActionSequenceEditorItemLocation> locations) && locations.Count > 1;
    }

    public void Dispose()
    {
    }

    private void RebuildSnapshots()
    {
        tracks.Clear();
        legacyClips.Clear();
        idMap.Clear();
        RebuildMissingTypeMap();

        SerializedProperty root = GetRootProperty();
        if (root == null)
        {
            Sequence = new ActionSequenceSnapshot(60, ActionSequenceDurationMode.FixedFrames, 1);
            return;
        }

        Sequence = new ActionSequenceSnapshot(
            ReadInt(root.FindPropertyRelative("frameRate"), 60),
            (ActionSequenceDurationMode)ReadInt(root.FindPropertyRelative("durationMode"), (int)ActionSequenceDurationMode.FixedFrames),
            ReadInt(root.FindPropertyRelative("durationFrames"), 1));

        SerializedProperty tracksProperty = root.FindPropertyRelative("tracks");
        if (tracksProperty != null && tracksProperty.isArray)
        {
            for (int trackIndex = 0; trackIndex < tracksProperty.arraySize; trackIndex++)
            {
                SerializedProperty trackProperty = tracksProperty.GetArrayElementAtIndex(trackIndex);
                ActionSequenceTrackSnapshot track = BuildTrackSnapshot(trackProperty, trackIndex);
                tracks.Add(track);
                AddId(track.EditorId, new ActionSequenceEditorItemLocation(ActionSequenceEditorDocumentItemKind.Track, track.EditorId, trackIndex, -1, -1));
            }
        }

        SerializedProperty legacyProperty = root.FindPropertyRelative("clips");
        if (legacyProperty != null && legacyProperty.isArray)
        {
            for (int legacyIndex = 0; legacyIndex < legacyProperty.arraySize; legacyIndex++)
            {
                SerializedProperty clipProperty = legacyProperty.GetArrayElementAtIndex(legacyIndex);
                ActionSequenceClipSnapshot clip = BuildClipSnapshot(clipProperty, -1, legacyIndex, true, null);
                legacyClips.Add(clip);
                AddId(clip.EditorId, new ActionSequenceEditorItemLocation(ActionSequenceEditorDocumentItemKind.LegacyClip, clip.EditorId, -1, -1, legacyIndex));
            }
        }
    }

    private ActionSequenceTrackSnapshot BuildTrackSnapshot(SerializedProperty trackProperty, int trackIndex)
    {
        var track = trackProperty?.managedReferenceValue as ActionSequenceTrackDefinition;
        Type type = track?.GetType();
        bool isNull = trackProperty == null || trackProperty.managedReferenceValue == null && string.IsNullOrEmpty(trackProperty.managedReferenceFullTypename);
        bool missingType = trackProperty != null && trackProperty.managedReferenceValue == null && !string.IsNullOrEmpty(trackProperty.managedReferenceFullTypename);

        string editorId = ReadString(trackProperty?.FindPropertyRelative("editorId"));
        var snapshot = new ActionSequenceTrackSnapshot(
            trackIndex,
            editorId,
            type,
            track != null ? track.Phase : default,
            ReadString(trackProperty?.FindPropertyRelative("displayName")),
            ReadBool(trackProperty?.FindPropertyRelative("muted")),
            ReadBool(trackProperty?.FindPropertyRelative("locked")),
            ReadBool(trackProperty?.FindPropertyRelative("collapsed")),
            isNull,
            missingType,
            ReadManagedReferenceId(trackProperty),
            GetMissingTypeInfo(trackProperty));

        SerializedProperty clipsProperty = trackProperty?.FindPropertyRelative("clips");
        if (clipsProperty != null && clipsProperty.isArray)
        {
            for (int clipIndex = 0; clipIndex < clipsProperty.arraySize; clipIndex++)
            {
                SerializedProperty clipProperty = clipsProperty.GetArrayElementAtIndex(clipIndex);
                ActionSequenceClipSnapshot clip = BuildClipSnapshot(clipProperty, trackIndex, clipIndex, false, track);
                snapshot.AddClip(clip);
                AddId(clip.EditorId, new ActionSequenceEditorItemLocation(ActionSequenceEditorDocumentItemKind.Clip, clip.EditorId, trackIndex, clipIndex, -1));
            }
        }

        return snapshot;
    }

    private ActionSequenceClipSnapshot BuildClipSnapshot(
        SerializedProperty clipProperty,
        int trackIndex,
        int clipOrLegacyIndex,
        bool isLegacy,
        ActionSequenceTrackDefinition owningTrack)
    {
        var clip = clipProperty?.managedReferenceValue as ActionSequenceClipDefinition;
        Type type = clip?.GetType();
        bool isNull = clipProperty == null || clipProperty.managedReferenceValue == null && string.IsNullOrEmpty(clipProperty.managedReferenceFullTypename);
        bool missingType = clipProperty != null && clipProperty.managedReferenceValue == null && !string.IsNullOrEmpty(clipProperty.managedReferenceFullTypename);

        bool allowedByTrack = isLegacy || owningTrack == null || clip == null || owningTrack.AllowsClipType(type);
        bool phaseMatchesTrack = isLegacy || owningTrack == null || clip == null || clip.Phase == owningTrack.Phase;

        return new ActionSequenceClipSnapshot(
            trackIndex,
            isLegacy ? -1 : clipOrLegacyIndex,
            isLegacy ? clipOrLegacyIndex : -1,
            ReadString(clipProperty?.FindPropertyRelative("editorId")),
            type,
            clip != null ? clip.Phase : default,
            ReadString(clipProperty?.FindPropertyRelative("displayName")),
            ReadInt(clipProperty?.FindPropertyRelative("startFrame"), 0),
            ReadInt(clipProperty?.FindPropertyRelative("endFrame"), 0),
            isLegacy,
            isNull,
            missingType,
            allowedByTrack,
            phaseMatchesTrack,
            ReadManagedReferenceId(clipProperty),
            GetMissingTypeInfo(clipProperty));
    }

    private void RebuildMissingTypeMap()
    {
        missingTypesByReferenceId.Clear();
        if (Target == null)
            return;

        foreach (object missingType in SerializationUtility.GetManagedReferencesWithMissingTypes(Target))
        {
            ActionSequenceMissingManagedReferenceInfo info = ActionSequenceMissingManagedReferenceInfo.FromUnityMissingType(missingType);
            if (info.ManagedReferenceId != 0 && !missingTypesByReferenceId.ContainsKey(info.ManagedReferenceId))
                missingTypesByReferenceId.Add(info.ManagedReferenceId, info);
        }
    }

    private ActionSequenceMissingManagedReferenceInfo GetMissingTypeInfo(SerializedProperty property)
    {
        long referenceId = ReadManagedReferenceId(property);
        if (referenceId != 0 && missingTypesByReferenceId.TryGetValue(referenceId, out ActionSequenceMissingManagedReferenceInfo info))
            return info;

        string fullTypeName = property != null ? property.managedReferenceFullTypename : null;
        return ActionSequenceMissingManagedReferenceInfo.FromSerializedTypename(referenceId, fullTypeName);
    }

    private static bool TryGetRootPath(Object target, out string rootPath)
    {
        rootPath = null;
        if (target is ActionAsset actionAsset)
        {
            if (!actionAsset.UsesSequence)
                return false;

            rootPath = "_sequenceData";
            return true;
        }

        if (target is ActionSequenceAsset)
        {
            rootPath = "data";
            return true;
        }

        return false;
    }

    private void AddId(string editorId, ActionSequenceEditorItemLocation location)
    {
        if (string.IsNullOrEmpty(editorId))
            return;

        if (!idMap.TryGetValue(editorId, out List<ActionSequenceEditorItemLocation> locations))
        {
            locations = new List<ActionSequenceEditorItemLocation>();
            idMap.Add(editorId, locations);
        }

        locations.Add(location);
    }

    private static string ReadString(SerializedProperty property)
    {
        return property != null ? property.stringValue : null;
    }

    private static bool ReadBool(SerializedProperty property)
    {
        return property != null && property.boolValue;
    }

    private static int ReadInt(SerializedProperty property, int fallback)
    {
        return property != null ? property.intValue : fallback;
    }

    private static long ReadManagedReferenceId(SerializedProperty property)
    {
        return property != null ? property.managedReferenceId : 0;
    }

    private string BuildSnapshotFingerprint()
    {
        var builder = new StringBuilder(512);
        builder.Append(Sequence.FrameRate).Append('|');
        builder.Append((int)Sequence.DurationMode).Append('|');
        builder.Append(Sequence.FixedDurationFrames).Append('|');
        builder.Append(tracks.Count).Append('|');

        for (int i = 0; i < tracks.Count; i++)
        {
            ActionSequenceTrackSnapshot track = tracks[i];
            builder.Append('T').Append(track.TrackIndex).Append(':');
            AppendCommon(builder, track.EditorId, track.Type, track.Phase, track.DisplayName, track.IsNull, track.MissingType, track.ManagedReferenceId, track.MissingTypeInfo);
            builder.Append(track.Muted).Append(':').Append(track.Locked).Append(':').Append(track.Collapsed).Append(':');
            builder.Append(track.Clips.Count).Append('|');

            IReadOnlyList<ActionSequenceClipSnapshot> clips = track.Clips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                AppendClip(builder, clips[clipIndex]);
        }

        builder.Append("L|").Append(legacyClips.Count).Append('|');
        for (int i = 0; i < legacyClips.Count; i++)
            AppendClip(builder, legacyClips[i]);

        return builder.ToString();
    }

    private static void AppendClip(StringBuilder builder, ActionSequenceClipSnapshot clip)
    {
        builder.Append('C').Append(clip.TrackIndex).Append(':').Append(clip.ClipIndex).Append(':').Append(clip.LegacyClipIndex).Append(':');
        AppendCommon(builder, clip.EditorId, clip.Type, clip.Phase, clip.DisplayName, clip.IsNull, clip.MissingType, clip.ManagedReferenceId, clip.MissingTypeInfo);
        builder.Append(clip.StartFrame).Append(':').Append(clip.EndFrame).Append(':');
        builder.Append(clip.IsLegacy).Append(':').Append(clip.AllowedByTrack).Append(':').Append(clip.PhaseMatchesTrack).Append('|');
    }

    private static void AppendCommon(
        StringBuilder builder,
        string editorId,
        Type type,
        ActionSequenceClipPhase phase,
        string displayName,
        bool isNull,
        bool missingType,
        long managedReferenceId,
        ActionSequenceMissingManagedReferenceInfo missingTypeInfo)
    {
        builder.Append(editorId).Append(':');
        builder.Append(type != null ? type.AssemblyQualifiedName : string.Empty).Append(':');
        builder.Append((int)phase).Append(':');
        builder.Append(displayName).Append(':');
        builder.Append(isNull).Append(':').Append(missingType).Append(':').Append(managedReferenceId).Append(':');
        if (missingTypeInfo.HasTypeName)
            builder.Append(missingTypeInfo.AssemblyName).Append('/').Append(missingTypeInfo.NamespaceName).Append('/').Append(missingTypeInfo.ClassName);
        builder.Append(':');
    }
}

public readonly struct ActionSequenceMissingManagedReferenceInfo
{
    public ActionSequenceMissingManagedReferenceInfo(long managedReferenceId, string assemblyName, string namespaceName, string className)
    {
        ManagedReferenceId = managedReferenceId;
        AssemblyName = assemblyName ?? string.Empty;
        NamespaceName = namespaceName ?? string.Empty;
        ClassName = className ?? string.Empty;
    }

    public long ManagedReferenceId { get; }
    public string AssemblyName { get; }
    public string NamespaceName { get; }
    public string ClassName { get; }
    public bool HasTypeName => !string.IsNullOrEmpty(ClassName) || !string.IsNullOrEmpty(AssemblyName) || !string.IsNullOrEmpty(NamespaceName);

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(NamespaceName) && !string.IsNullOrEmpty(ClassName))
                return NamespaceName + "." + ClassName;
            if (!string.IsNullOrEmpty(ClassName))
                return ClassName;
            return string.Empty;
        }
    }

    public string Tooltip
    {
        get
        {
            string displayName = DisplayName;
            if (string.IsNullOrEmpty(AssemblyName))
                return displayName;
            if (string.IsNullOrEmpty(displayName))
                return AssemblyName;
            return displayName + " (" + AssemblyName + ")";
        }
    }

    public static ActionSequenceMissingManagedReferenceInfo FromUnityMissingType(object missingType)
    {
        if (missingType == null)
            return default;

        Type type = missingType.GetType();
        long referenceId = ReadLongField(type, missingType, "referenceId");
        string assemblyName = ReadStringField(type, missingType, "assemblyName");
        string namespaceName = ReadStringField(type, missingType, "namespaceName");
        string className = ReadStringField(type, missingType, "className");
        return new ActionSequenceMissingManagedReferenceInfo(referenceId, assemblyName, namespaceName, className);
    }

    public static ActionSequenceMissingManagedReferenceInfo FromSerializedTypename(long referenceId, string fullTypename)
    {
        if (string.IsNullOrEmpty(fullTypename))
            return default;

        int separator = fullTypename.IndexOf(' ');
        if (separator < 0)
            return new ActionSequenceMissingManagedReferenceInfo(referenceId, string.Empty, string.Empty, fullTypename);

        string assemblyName = fullTypename.Substring(0, separator);
        string typeName = fullTypename.Substring(separator + 1);
        int lastDot = typeName.LastIndexOf('.');
        string namespaceName = lastDot > 0 ? typeName.Substring(0, lastDot) : string.Empty;
        string className = lastDot > 0 ? typeName.Substring(lastDot + 1) : typeName;
        return new ActionSequenceMissingManagedReferenceInfo(referenceId, assemblyName, namespaceName, className);
    }

    private static long ReadLongField(Type type, object instance, string fieldName)
    {
        var field = type.GetField(fieldName);
        if (field == null)
            return 0;

        object value = field.GetValue(instance);
        return value is long longValue ? longValue : 0;
    }

    private static string ReadStringField(Type type, object instance, string fieldName)
    {
        var field = type.GetField(fieldName);
        return field != null ? field.GetValue(instance) as string : null;
    }
}

public readonly struct ActionSequenceSnapshot
{
    public ActionSequenceSnapshot(int frameRate, ActionSequenceDurationMode durationMode, int fixedDurationFrames)
    {
        FrameRate = frameRate;
        DurationMode = durationMode;
        FixedDurationFrames = fixedDurationFrames;
    }

    public int FrameRate { get; }
    public ActionSequenceDurationMode DurationMode { get; }
    public int FixedDurationFrames { get; }
}

public sealed class ActionSequenceTrackSnapshot
{
    private readonly List<ActionSequenceClipSnapshot> clips = new List<ActionSequenceClipSnapshot>();

    public ActionSequenceTrackSnapshot(
        int trackIndex,
        string editorId,
        Type type,
        ActionSequenceClipPhase phase,
        string displayName,
        bool muted,
        bool locked,
        bool collapsed,
        bool isNull,
        bool missingType,
        long managedReferenceId,
        ActionSequenceMissingManagedReferenceInfo missingTypeInfo)
    {
        TrackIndex = trackIndex;
        EditorId = editorId;
        Type = type;
        Phase = phase;
        DisplayName = displayName;
        Muted = muted;
        Locked = locked;
        Collapsed = collapsed;
        IsNull = isNull;
        MissingType = missingType;
        ManagedReferenceId = managedReferenceId;
        MissingTypeInfo = missingTypeInfo;
    }

    public int TrackIndex { get; }
    public string EditorId { get; }
    public Type Type { get; }
    public ActionSequenceClipPhase Phase { get; }
    public string DisplayName { get; }
    public bool Muted { get; }
    public bool Locked { get; }
    public bool Collapsed { get; }
    public bool IsNull { get; }
    public bool MissingType { get; }
    public long ManagedReferenceId { get; }
    public ActionSequenceMissingManagedReferenceInfo MissingTypeInfo { get; }
    public IReadOnlyList<ActionSequenceClipSnapshot> Clips => clips;

    internal void AddClip(ActionSequenceClipSnapshot clip)
    {
        clips.Add(clip);
    }
}

public sealed class ActionSequenceClipSnapshot
{
    public ActionSequenceClipSnapshot(
        int trackIndex,
        int clipIndex,
        int legacyClipIndex,
        string editorId,
        Type type,
        ActionSequenceClipPhase phase,
        string displayName,
        int startFrame,
        int endFrame,
        bool isLegacy,
        bool isNull,
        bool missingType,
        bool allowedByTrack,
        bool phaseMatchesTrack,
        long managedReferenceId,
        ActionSequenceMissingManagedReferenceInfo missingTypeInfo)
    {
        TrackIndex = trackIndex;
        ClipIndex = clipIndex;
        LegacyClipIndex = legacyClipIndex;
        EditorId = editorId;
        Type = type;
        Phase = phase;
        DisplayName = displayName;
        StartFrame = startFrame;
        EndFrame = endFrame;
        IsLegacy = isLegacy;
        IsNull = isNull;
        MissingType = missingType;
        AllowedByTrack = allowedByTrack;
        PhaseMatchesTrack = phaseMatchesTrack;
        ManagedReferenceId = managedReferenceId;
        MissingTypeInfo = missingTypeInfo;
    }

    public int TrackIndex { get; }
    public int ClipIndex { get; }
    public int LegacyClipIndex { get; }
    public string EditorId { get; }
    public Type Type { get; }
    public ActionSequenceClipPhase Phase { get; }
    public string DisplayName { get; }
    public int StartFrame { get; }
    public int EndFrame { get; }
    public bool IsLegacy { get; }
    public bool IsNull { get; }
    public bool MissingType { get; }
    public bool AllowedByTrack { get; }
    public bool PhaseMatchesTrack { get; }
    public long ManagedReferenceId { get; }
    public ActionSequenceMissingManagedReferenceInfo MissingTypeInfo { get; }
}
#endif
