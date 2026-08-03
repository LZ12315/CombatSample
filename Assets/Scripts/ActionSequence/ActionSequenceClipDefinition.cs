using System;
using System.Text;
using UnityEngine;

public enum ActionSequenceClipPhase
{
    State = 0,
    Animation = 10,
    Motion = 20,
    HitBox = 30,
    Cleanup = 40,
}

[Serializable]
public abstract class ActionSequenceClipDefinition
{
    [SerializeField, HideInInspector]
    private string editorId;

    public string displayName;

    [Min(0)]
    public int startFrame;

    [Min(1)]
    public int endFrame = 1;

    [NonSerialized]
    private string guid;

    public string Guid
    {
        get
        {
            if (!string.IsNullOrEmpty(editorId))
                return editorId;

            if (string.IsNullOrEmpty(guid))
                guid = System.Guid.NewGuid().ToString("N");
            return guid;
        }
    }

    public string EditorId => editorId;

#if UNITY_EDITOR
    public void EditorSetEditorId(string value)
    {
        editorId = value;
    }
#endif

    public int StartFrame => Mathf.Max(0, startFrame);
    public int EndFrame => Mathf.Max(StartFrame + 1, endFrame);
    public int DurationFrames => EndFrame - StartFrame;
    public bool ContainsFrame(int frame) => frame >= StartFrame && frame < EndFrame;

    public abstract ActionSequenceClipPhase Phase { get; }

    public virtual string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? GetDefaultDisplayName() : displayName;
    }

    protected virtual string GetDefaultDisplayName()
    {
        string typeName = GetType().Name;
        typeName = typeName.Replace("ActionSequence", string.Empty);
        typeName = typeName.Replace("ClipDefinition", string.Empty);
        return NicifyTypeName(typeName);
    }

    private static string NicifyTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "Clip";

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

    public void NormalizeFrames(int sequenceDurationFrames)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = GetDefaultDisplayName();

        startFrame = Mathf.Max(0, startFrame);
        endFrame = Mathf.Max(startFrame + 1, endFrame);

        if (sequenceDurationFrames > 0)
        {
            startFrame = Mathf.Min(startFrame, sequenceDurationFrames - 1);
            endFrame = Mathf.Clamp(endFrame, startFrame + 1, sequenceDurationFrames);
        }
    }

    public abstract ActionSequenceClipRuntime CreateRuntime();
}

public abstract class ActionSequenceClipRuntime
{
    public virtual void OnEnter(ActionSequenceContext context) { }
    public virtual void OnTick(ActionSequenceContext context) { }
    public virtual void OnExit(ActionSequenceContext context, bool completed) { }
}
