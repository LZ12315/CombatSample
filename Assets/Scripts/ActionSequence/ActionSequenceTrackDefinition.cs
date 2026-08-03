using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public abstract class ActionSequenceTrackDefinition
{
    [SerializeField, HideInInspector]
    private string editorId;

    public string displayName;
    public bool muted;
    public bool locked;
    public bool collapsed;

    [SerializeReference, SubclassSelector]
    private List<ActionSequenceClipDefinition> clips = new List<ActionSequenceClipDefinition>();

    public IReadOnlyList<ActionSequenceClipDefinition> Clips => clips;
    public string EditorId => editorId;

#if UNITY_EDITOR
    public List<ActionSequenceClipDefinition> EditorClips => clips;

    public void EditorSetEditorId(string value)
    {
        editorId = value;
    }
#endif

    public abstract ActionSequenceClipPhase Phase { get; }
    public abstract Type[] AllowedClipTypes { get; }

    public virtual string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? GetDefaultDisplayName() : displayName;
    }

    public bool AllowsClipType(Type clipType)
    {
        if (clipType == null)
            return false;

        Type[] allowed = AllowedClipTypes;
        if (allowed == null)
            return false;

        for (int i = 0; i < allowed.Length; i++)
        {
            Type allowedType = allowed[i];
            if (allowedType != null && allowedType.IsAssignableFrom(clipType))
                return true;
        }

        return false;
    }

    public bool CanAddClip(ActionSequenceClipDefinition clip)
    {
        return clip != null && clip.Phase == Phase && AllowsClipType(clip.GetType());
    }

    public bool TryAddClip(ActionSequenceClipDefinition clip)
    {
        if (clip == null)
            return false;

        if (!CanAddClip(clip))
            return false;

        if (clips == null)
            clips = new List<ActionSequenceClipDefinition>();

        clips.Add(clip);
        return true;
    }

    public void AddClip(ActionSequenceClipDefinition clip)
    {
        TryAddClip(clip);
    }

    public void NormalizeFrames(int sequenceDurationFrames)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = GetDefaultDisplayName();

        if (clips == null)
            clips = new List<ActionSequenceClipDefinition>();

        for (int i = clips.Count - 1; i >= 0; i--)
        {
            ActionSequenceClipDefinition clip = clips[i];
            if (clip == null)
                continue;

            if (clip.Phase != Phase || !AllowsClipType(clip.GetType()))
                continue;

            clip.NormalizeFrames(sequenceDurationFrames);
        }
    }

    private string GetDefaultDisplayName()
    {
        string typeName = GetType().Name;
        typeName = typeName.Replace("ActionSequence", string.Empty);
        typeName = typeName.Replace("Track", string.Empty);
        return NicifyTypeName(typeName);
    }

    private static string NicifyTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "Track";

        var builder = new StringBuilder(typeName.Length + 8);
        for (int i = 0; i < typeName.Length; i++)
        {
            char current = typeName[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(typeName[i - 1]))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }
}

[Serializable]
public sealed class ActionSequenceStateTrack : ActionSequenceTrackDefinition
{
    private static readonly Type[] ClipTypes =
    {
        typeof(ActionSequenceTagClipDefinition),
    };

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.State;
    public override Type[] AllowedClipTypes => ClipTypes;
}

[Serializable]
public sealed class ActionSequenceAnimationTrack : ActionSequenceTrackDefinition
{
    private static readonly Type[] ClipTypes =
    {
        typeof(ActionSequenceAnimancerClipDefinition),
    };

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.Animation;
    public override Type[] AllowedClipTypes => ClipTypes;
}

[Serializable]
public sealed class ActionSequenceMotionTrack : ActionSequenceTrackDefinition
{
    private static readonly Type[] ClipTypes =
    {
        typeof(ActionSequenceImpulseClipDefinition),
    };

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.Motion;
    public override Type[] AllowedClipTypes => ClipTypes;
}

[Serializable]
public sealed class ActionSequenceHitBoxTrack : ActionSequenceTrackDefinition
{
    private static readonly Type[] ClipTypes =
    {
        typeof(ActionSequenceHitBoxClipDefinition),
    };

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.HitBox;
    public override Type[] AllowedClipTypes => ClipTypes;
}

[Serializable]
public sealed class ActionSequenceCleanupTrack : ActionSequenceTrackDefinition
{
    private static readonly Type[] ClipTypes = Array.Empty<Type>();

    public override ActionSequenceClipPhase Phase => ActionSequenceClipPhase.Cleanup;
    public override Type[] AllowedClipTypes => ClipTypes;
}
