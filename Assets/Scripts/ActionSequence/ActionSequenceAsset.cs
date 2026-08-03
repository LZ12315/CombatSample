using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ActionSequence", menuName = "Combat/Action Sequence")]
public class ActionSequenceAsset : ScriptableObject
{
    [SerializeField]
    private ActionSequenceData data = new ActionSequenceData();

    public ActionSequenceData Data => data;
    public int FrameRate => data != null ? data.FrameRate : 60;
    public ActionSequenceDurationMode DurationMode => data != null ? data.DurationMode : ActionSequenceDurationMode.FixedFrames;
    public int DurationFrames => data != null ? data.DurationFrames : 1;
    public IReadOnlyList<ActionSequenceClipDefinition> Clips => data != null ? data.Clips : Array.Empty<ActionSequenceClipDefinition>();

#if UNITY_EDITOR
    public List<ActionSequenceTrackDefinition> EditorTracks => data.EditorTracks;
    public List<ActionSequenceClipDefinition> EditorClips => data.EditorClips;

    public void EditorSetTiming(int newFrameRate, int newDurationFrames)
    {
        EnsureData();
        data.EditorSetTiming(newFrameRate, newDurationFrames);
        OnValidate();
    }

    public void EditorSetDurationMode(ActionSequenceDurationMode newDurationMode)
    {
        EnsureData();
        data.EditorSetDurationMode(newDurationMode);
        OnValidate();
    }
#endif

    private void OnValidate()
    {
        EnsureData();
        data.Normalize();
    }

    private void EnsureData()
    {
        if (data == null)
            data = new ActionSequenceData();
    }
}
