using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ActionSequenceData
{
    private const int MinimumAutoDurationFrames = 1;

    [SerializeField, Min(1)]
    private int frameRate = 60;

    [SerializeField]
    private ActionSequenceDurationMode durationMode = ActionSequenceDurationMode.FixedFrames;

    [SerializeField, Min(1)]
    private int durationFrames = 60;

    [SerializeReference, SubclassSelector]
    private List<ActionSequenceTrackDefinition> tracks = new List<ActionSequenceTrackDefinition>();

    [SerializeReference, SubclassSelector, HideInInspector]
    private List<ActionSequenceClipDefinition> clips = new List<ActionSequenceClipDefinition>();

    [SerializeField, HideInInspector]
    private bool defaultTracksInitialized;

    public int FrameRate => Mathf.Max(1, frameRate);
    public ActionSequenceDurationMode DurationMode => durationMode;
    public int FixedDurationFrames => Mathf.Max(1, durationFrames);
    public int DurationFrames => durationMode == ActionSequenceDurationMode.AutoFromClips
        ? CalculateAutoDurationFrames()
        : FixedDurationFrames;
    public IReadOnlyList<ActionSequenceTrackDefinition> Tracks => tracks;
    public IReadOnlyList<ActionSequenceClipDefinition> Clips => BuildFlatClipList();
    public IReadOnlyList<ActionSequenceClipDefinition> LegacyClips => clips;

    public int CalculateAutoDurationFrames()
    {
        int maxEndFrame = MinimumAutoDurationFrames;

        if (tracks != null)
        {
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                ActionSequenceTrackDefinition track = tracks[trackIndex];
                if (track == null || track.Clips == null)
                    continue;

                IReadOnlyList<ActionSequenceClipDefinition> trackClips = track.Clips;
                for (int clipIndex = 0; clipIndex < trackClips.Count; clipIndex++)
                {
                    ActionSequenceClipDefinition clip = trackClips[clipIndex];
                    if (clip != null)
                        maxEndFrame = Mathf.Max(maxEndFrame, clip.EndFrame);
                }
            }
        }

        if (clips != null)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                ActionSequenceClipDefinition clip = clips[i];
                if (clip != null)
                    maxEndFrame = Mathf.Max(maxEndFrame, clip.EndFrame);
            }
        }

        return maxEndFrame;
    }

    public List<ActionSequenceValidationIssue> Validate()
    {
        var issues = new List<ActionSequenceValidationIssue>();
        Validate(issues);
        return issues;
    }

    public void Validate(IList<ActionSequenceValidationIssue> issues)
    {
        if (issues == null)
            return;

        if (frameRate <= 0)
            issues.Add(ActionSequenceValidationIssue.Error("Frame rate must be greater than zero."));
        if (durationMode == ActionSequenceDurationMode.FixedFrames && durationFrames <= 0)
            issues.Add(ActionSequenceValidationIssue.Error("Duration frames must be greater than zero."));

        if (tracks == null)
        {
            issues.Add(ActionSequenceValidationIssue.Error("Track list is null."));
            return;
        }

        for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
        {
            ActionSequenceTrackDefinition track = tracks[trackIndex];
            if (track == null)
            {
                issues.Add(ActionSequenceValidationIssue.Warning("Track is null.", trackIndex));
                continue;
            }

            IReadOnlyList<ActionSequenceClipDefinition> trackClips = track.Clips;
            if (trackClips == null)
            {
                issues.Add(ActionSequenceValidationIssue.Warning("Track clip list is null.", trackIndex));
                continue;
            }

            for (int clipIndex = 0; clipIndex < trackClips.Count; clipIndex++)
            {
                ActionSequenceClipDefinition clip = trackClips[clipIndex];
                if (clip == null)
                {
                    issues.Add(ActionSequenceValidationIssue.Warning("Clip is null.", trackIndex, clipIndex));
                    continue;
                }

                if (clip.Phase != track.Phase)
                {
                    issues.Add(ActionSequenceValidationIssue.Error(
                        $"Clip phase {clip.Phase} does not match track phase {track.Phase}.",
                        trackIndex,
                        clipIndex));
                }

                if (!track.AllowsClipType(clip.GetType()))
                {
                    issues.Add(ActionSequenceValidationIssue.Error(
                        $"{clip.GetType().Name} is not allowed on {track.GetType().Name}.",
                        trackIndex,
                        clipIndex));
                }
            }
        }

        if (clips != null && clips.Count > 0)
            issues.Add(ActionSequenceValidationIssue.Warning($"Legacy flat clip list still contains {clips.Count} clip(s)."));
    }

    public void Normalize()
    {
        frameRate = Mathf.Max(1, frameRate);
        durationFrames = Mathf.Max(1, durationFrames);

        if (tracks == null)
            tracks = new List<ActionSequenceTrackDefinition>();
        if (clips == null)
            clips = new List<ActionSequenceClipDefinition>();
    }

    public void InitializeNewSequenceDefaults()
    {
        Normalize();
        durationMode = ActionSequenceDurationMode.AutoFromClips;
        if (tracks.Count == 0)
            CreateDefaultTracks();
        defaultTracksInitialized = true;
    }

#if UNITY_EDITOR
    public List<ActionSequenceTrackDefinition> EditorTracks => tracks;
    public List<ActionSequenceClipDefinition> EditorClips => clips;

    public void EditorSetTiming(int newFrameRate, int newDurationFrames)
    {
        frameRate = Mathf.Max(1, newFrameRate);
        durationFrames = Mathf.Max(1, newDurationFrames);
        Normalize();
    }

    public void EditorSetDurationMode(ActionSequenceDurationMode newDurationMode)
    {
        durationMode = newDurationMode;
        Normalize();
    }
#endif

    private void CreateDefaultTracks()
    {
        tracks.Add(new ActionSequenceStateTrack());
        tracks.Add(new ActionSequenceAnimationTrack());
        tracks.Add(new ActionSequenceMotionTrack());
        tracks.Add(new ActionSequenceHitBoxTrack());
        tracks.Add(new ActionSequenceCleanupTrack());
    }

    public static ActionSequenceTrackDefinition CreateDefaultTrackForPhase(ActionSequenceClipPhase phase)
    {
        return phase switch
        {
            ActionSequenceClipPhase.State => new ActionSequenceStateTrack(),
            ActionSequenceClipPhase.Animation => new ActionSequenceAnimationTrack(),
            ActionSequenceClipPhase.Motion => new ActionSequenceMotionTrack(),
            ActionSequenceClipPhase.HitBox => new ActionSequenceHitBoxTrack(),
            ActionSequenceClipPhase.Cleanup => new ActionSequenceCleanupTrack(),
            _ => new ActionSequenceStateTrack(),
        };
    }

#if UNITY_EDITOR
    public int EditorMigrateLegacyClips(Func<ActionSequenceTrackDefinition, bool> canUseTrack = null)
    {
        if (clips.Count == 0)
            return 0;

        int migrated = 0;

        for (int i = 0; i < clips.Count;)
        {
            ActionSequenceClipDefinition clip = clips[i];
            if (clip == null)
            {
                i++;
                continue;
            }

            ActionSequenceTrackDefinition track = FindOrCreateTrackForClip(clip, canUseTrack);
            if (track != null && track.TryAddClip(clip))
            {
                clips.RemoveAt(i);
                migrated++;
                continue;
            }

            i++;
        }

        return migrated;
    }

    private ActionSequenceTrackDefinition FindOrCreateTrackForClip(ActionSequenceClipDefinition clip, Func<ActionSequenceTrackDefinition, bool> canUseTrack)
    {
        for (int i = 0; i < tracks.Count; i++)
        {
            ActionSequenceTrackDefinition track = tracks[i];
            if (track != null && track.Phase == clip.Phase && track.AllowsClipType(clip.GetType()) && (canUseTrack == null || canUseTrack(track)))
                return track;
        }

        ActionSequenceTrackDefinition created = CreateDefaultTrackForPhase(clip.Phase);
        if (created == null || !created.CanAddClip(clip))
            return null;

        tracks.Add(created);
        return created;
    }
#endif

    private IReadOnlyList<ActionSequenceClipDefinition> BuildFlatClipList()
    {
        var result = new List<ActionSequenceClipDefinition>();

        if (tracks != null)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                ActionSequenceTrackDefinition track = tracks[i];
                if (track == null)
                    continue;

                IReadOnlyList<ActionSequenceClipDefinition> trackClips = track.Clips;
                if (trackClips == null)
                    continue;

                for (int j = 0; j < trackClips.Count; j++)
                {
                    if (trackClips[j] != null)
                        result.Add(trackClips[j]);
                }
            }
        }

        if (clips != null)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                    result.Add(clips[i]);
            }
        }

        return result;
    }
}

public enum ActionSequenceDurationMode
{
    FixedFrames = 0,
    AutoFromClips = 1,
}

public enum ActionSequenceValidationSeverity
{
    Warning,
    Error,
}

public readonly struct ActionSequenceValidationIssue
{
    public readonly ActionSequenceValidationSeverity Severity;
    public readonly string Message;
    public readonly int TrackIndex;
    public readonly int ClipIndex;

    private ActionSequenceValidationIssue(ActionSequenceValidationSeverity severity, string message, int trackIndex, int clipIndex)
    {
        Severity = severity;
        Message = message;
        TrackIndex = trackIndex;
        ClipIndex = clipIndex;
    }

    public static ActionSequenceValidationIssue Warning(string message, int trackIndex = -1, int clipIndex = -1)
    {
        return new ActionSequenceValidationIssue(ActionSequenceValidationSeverity.Warning, message, trackIndex, clipIndex);
    }

    public static ActionSequenceValidationIssue Error(string message, int trackIndex = -1, int clipIndex = -1)
    {
        return new ActionSequenceValidationIssue(ActionSequenceValidationSeverity.Error, message, trackIndex, clipIndex);
    }
}
