using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum LocomotionAnimationRole { None, MoveCycle, Transition }

[Serializable]
public struct FootContactInterval
{
    public float Start;
    public float End;
    public FootContactInterval(float start, float end) { Start = start; End = end; }
}

[Serializable]
public struct FootPhaseAnchor
{
    public float Time;
    public float Phase;
    public FootPhaseAnchor(float time, float phase) { Time = time; Phase = phase; }
}

/// <summary>One complete looping clip, with an unwrapped left/right gait phase.</summary>
[Serializable]
public sealed class LocomotionCycleMapping
{
    [SerializeField] private FootPhaseAnchor[] anchors = Array.Empty<FootPhaseAnchor>();
    public float Duration => anchors != null && anchors.Length >= 2
        ? anchors[anchors.Length - 1].Time - anchors[0].Time : 0f;
    public LocomotionCycleMapping() { }
    public LocomotionCycleMapping(params FootPhaseAnchor[] values) => anchors = values;

    public bool Validate(float clipLength, out string reason)
    {
        reason = "Cycle needs left (0), right (0.5), and next left (1) anchors.";
        if (anchors == null || anchors.Length < 3) return false;
        bool right = false;
        for (int i = 0; i < anchors.Length; i++)
        {
            if (!RootMotionTransform.IsFinite(anchors[i].Time) || !RootMotionTransform.IsFinite(anchors[i].Phase)) return false;
            if (i > 0 && (anchors[i].Time <= anchors[i - 1].Time || anchors[i].Phase <= anchors[i - 1].Phase)) return false;
            right |= Mathf.Abs(anchors[i].Phase - 0.5f) < 0.0001f;
        }
        if (!right || Mathf.Abs(anchors[0].Phase) > 0.0001f
            || Mathf.Abs(anchors[anchors.Length - 1].Phase - 1f) > 0.0001f) return false;
        reason = "Cycle duration must equal the looping clip length; the first left contact must lie within the clip.";
        if (!RootMotionTransform.IsFinite(clipLength) || clipLength <= 0f || Mathf.Abs(Duration - clipLength) > 0.001f
            || anchors[0].Time < 0f || anchors[0].Time >= clipLength) return false;
        reason = string.Empty;
        return true;
    }

    // Callers validate once when binding; the hot queries allocate nothing.
    public double PhaseToTime(double phase)
    {
        double cycle = Math.Floor(phase);
        double local = phase - cycle;
        for (int i = 1; i < anchors.Length; i++)
            if (local <= anchors[i].Phase)
                return cycle * Duration + anchors[i - 1].Time
                    + (local - anchors[i - 1].Phase) / (anchors[i].Phase - anchors[i - 1].Phase)
                    * (anchors[i].Time - anchors[i - 1].Time);
        return cycle * Duration + anchors[anchors.Length - 1].Time;
    }

    public double TimeToPhase(double time)
    {
        double cycle = Math.Floor((time - anchors[0].Time) / Duration);
        double local = time - cycle * Duration;
        for (int i = 1; i < anchors.Length; i++)
            if (local <= anchors[i].Time)
                return cycle + anchors[i - 1].Phase
                    + (local - anchors[i - 1].Time) / (anchors[i].Time - anchors[i - 1].Time)
                    * (anchors[i].Phase - anchors[i - 1].Phase);
        return cycle + 1d;
    }
}

[Serializable]
public sealed class LocomotionAnalyzedMotion
{
    public LocomotionCycleMapping Cycle;
    public FootContactInterval[] LeftContacts = Array.Empty<FootContactInterval>();
    public FootContactInterval[] RightContacts = Array.Empty<FootContactInterval>();
    public bool HasExitPhaseCandidate;
    [Range(0f, 1f)] public float ExitPhaseCandidate;
}

/// <summary>Shared immutable playback facts. Automatic analysis never overwrites manual corrections.</summary>
[Serializable]
public sealed class LocomotionAnimationData
{
    [SerializeField] private LocomotionAnimationRole role;
    [SerializeField, HideInInspector] private AnimationClip sourceClip;
    [SerializeField, HideInInspector] private float sourceDuration;
    [SerializeField, HideInInspector] private string sourceHash;
    [SerializeField, HideInInspector] private string manualSourceHash;
    [SerializeField, HideInInspector] private string automaticSourceHash;
    [SerializeField, HideInInspector] private LocomotionAnalyzedMotion automatic;
    [SerializeField] private bool overrideCycle;
    [SerializeField] private LocomotionCycleMapping manualCycle = new LocomotionCycleMapping();
    [SerializeField] private bool overrideLeftContacts;
    [SerializeField] private FootContactInterval[] manualLeftContacts = Array.Empty<FootContactInterval>();
    [SerializeField] private bool overrideRightContacts;
    [SerializeField] private FootContactInterval[] manualRightContacts = Array.Empty<FootContactInterval>();
    [SerializeField] private bool exitPhaseConfirmed;
    [SerializeField, Range(0f, 1f)] private float exitPhase;

    public LocomotionAnimationRole Role => role;
    public string SourceHash => sourceHash;
    public LocomotionAnalyzedMotion Automatic => automatic;
    public bool AutomaticMatchesSource => automatic != null && automaticSourceHash == sourceHash;
    public FootContactInterval[] LeftContacts => overrideLeftContacts ? manualLeftContacts : automatic?.LeftContacts;
    public FootContactInterval[] RightContacts => overrideRightContacts ? manualRightContacts : automatic?.RightContacts;

    public bool MatchesClip(AnimationClip clip) => clip != null && sourceClip == clip
        && Mathf.Abs(sourceDuration - clip.length) <= 0.0001f;

    public bool TryGetCycle(AnimationClip clip, out LocomotionCycleMapping cycle, out string reason)
    {
        cycle = null;
        reason = "Move cycle data is missing, stale, or the source clip is not looping.";
        if (role != LocomotionAnimationRole.MoveCycle || !MatchesClip(clip) || !clip.isLooping) return false;
        if (overrideCycle && manualSourceHash != sourceHash) { reason = "Manual cycle corrections need source confirmation."; return false; }
        cycle = overrideCycle ? manualCycle : automaticSourceHash == sourceHash ? automatic?.Cycle : null;
        if (cycle == null) return false;
        return cycle.Validate(clip.length, out reason);
    }

    public bool TryGetExitPhase(AnimationClip clip, out float phase)
    {
        phase = exitPhase;
        return role == LocomotionAnimationRole.Transition && MatchesClip(clip) && exitPhaseConfirmed && manualSourceHash == sourceHash
            && RootMotionTransform.IsFinite(phase) && phase >= 0f && phase <= 1f;
    }

#if UNITY_EDITOR
    public void EditorSetRole(LocomotionAnimationRole value) => role = value;
    public void EditorApplyAnalysis(AnimationClip clip, string hash, LocomotionAnalyzedMotion result)
    {
        sourceClip = clip;
        sourceDuration = clip.length;
        sourceHash = hash;
        automatic = result;
        automaticSourceHash = hash;
        // A reanalysis must not silently rebind old corrections to a different source.
    }
    public void EditorConfirmSource(AnimationClip clip, string hash)
    {
        sourceClip = clip;
        sourceDuration = clip.length;
        sourceHash = hash;
        manualSourceHash = hash;
    }
    public void EditorSetCycle(LocomotionCycleMapping cycle) { overrideCycle = true; manualCycle = cycle; manualSourceHash = sourceHash; }
    public void EditorSetExitPhase(float phase) { exitPhase = phase; exitPhaseConfirmed = true; manualSourceHash = sourceHash; }
#endif
}

#if UNITY_EDITOR
/// <summary>Independent from RootMotionBakerVersion and the Action trajectory bake hash.</summary>
public static class LocomotionDataSourceHash
{
    public const int Version = 1;
    public static string Compute(AnimationAsset asset)
    {
        if (asset == null || asset.Clip == null) return string.Empty;
        string clipPath = AssetDatabase.GetAssetPath(asset.Clip);
        string rigPath = asset.AnimationRigAsset != null ? AssetDatabase.GetAssetPath(asset.AnimationRigAsset.BakeRigPrefab) : "";
        string rigIdentity = "";
        if (asset.AnimationRigAsset != null)
            rigIdentity = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.AnimationRigAsset, out string rigGuid, out long rigLocalId)
                ? rigGuid + ":" + rigLocalId : asset.AnimationRigAsset.GetInstanceID().ToString();
        var settings = asset.AnimationRigAsset != null ? asset.AnimationRigAsset.BakeSettings : null;
        string clipIdentity = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.Clip, out string guid, out long localId)
            ? guid + ":" + localId : asset.Clip.GetInstanceID().ToString();
        return Hash128.Compute(Version + "|" + clipIdentity + "|" + asset.Clip.length.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            + "|" + (clipPath.Length > 0 ? AssetDatabase.GetAssetDependencyHash(clipPath).ToString() : "")
            + "|" + rigIdentity + "|" + rigPath + "|" + (rigPath.Length > 0 ? AssetDatabase.GetAssetDependencyHash(rigPath).ToString() : "")
            + "|" + (settings != null ? JsonUtility.ToJson(settings) : "")
            + "|" + asset.LocomotionData.Role).ToString();
    }
}
#endif
