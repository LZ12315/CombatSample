#if UNITY_EDITOR
using System.Collections.Generic;

[System.Flags]
public enum ActionSequenceEditorChangeFlags
{
    None = 0,
    Structure = 1 << 0,
    Content = 1 << 1,
    Timing = 1 << 2,
    Validation = 1 << 3,
}

public sealed class ActionSequenceEditorChangeSet
{
    private readonly List<string> trackIds = new List<string>();
    private readonly List<string> clipIds = new List<string>();

    public ActionSequenceEditorChangeSet(ActionSequenceEditorChangeFlags flags)
    {
        Flags = flags;
    }

    public ActionSequenceEditorChangeFlags Flags { get; }
    public IReadOnlyList<string> TrackIds => trackIds;
    public IReadOnlyList<string> ClipIds => clipIds;

    public ActionSequenceEditorChangeSet AddTrack(string editorId)
    {
        if (!string.IsNullOrEmpty(editorId) && !trackIds.Contains(editorId))
            trackIds.Add(editorId);

        return this;
    }

    public ActionSequenceEditorChangeSet AddClip(string editorId)
    {
        if (!string.IsNullOrEmpty(editorId) && !clipIds.Contains(editorId))
            clipIds.Add(editorId);

        return this;
    }
}
#endif
