#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class LocomotionAnalysisWorkflow
{
    public static AnimationAssetBakeStatus GetStatus(AnimationAsset asset)
    {
        if (asset == null || asset.Clip == null || asset.LocomotionData == null)
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Invalid, "Locomotion: Clip or metadata is missing.");
        var data = asset.LocomotionData;
        if (data.Role == LocomotionAnimationRole.None)
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Missing, "Locomotion analysis is not configured (Idle/Air may leave Role as None).");
        if (!asset.IsLocomotionDataCurrent)
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Stale, "Locomotion data is missing or stale. Analyze or confirm manual corrections for the current Clip/Rig/settings.");
        if (data.Role == LocomotionAnimationRole.MoveCycle && !data.TryGetCycle(asset.Clip, out _, out string reason))
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Invalid, reason);
        if (data.Role == LocomotionAnimationRole.Transition && !data.TryGetExitPhase(asset.Clip, out _))
            return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Missing, "Contacts analyzed; transition exit phase still needs manual confirmation.");
        return new AnimationAssetBakeStatus(AnimationAssetBakeStatusCode.Ready, "Locomotion metadata is current. Character contact quality still requires visual review.");
    }

    public static bool TryAnalyze(AnimationAsset asset, out string reason)
    {
        reason = "Choose a Clip, a Humanoid AnimationRigAsset and a MoveCycle/Transition role first.";
        if (asset == null || asset.Clip == null || asset.AnimationRigAsset == null
            || asset.LocomotionData == null || asset.LocomotionData.Role == LocomotionAnimationRole.None) return false;
        var samples = new LocomotionFootSamples();
        if (!RootMotionBaker.TryBakeWithFeet(asset.Clip, asset.RootMotionBakeSettings, samples, out var diagnostic))
        { reason = diagnostic.Message; return false; }
        if (!LocomotionFootAnalyzer.TryAnalyze(samples.Times, samples.Left, samples.Right, samples.LegLength,
            asset.AnimationRigAsset.BakeSettings, asset.Clip.isLooping, asset.LocomotionData.Role, out var result, out reason)) return false;
        Undo.RecordObject(asset, "Analyze Locomotion Feet");
        asset.LocomotionData.EditorApplyAnalysis(asset.Clip, LocomotionDataSourceHash.Compute(asset), result);
        Save(asset);
        reason = "Foot candidates saved; existing manual corrections and Root Motion data were preserved.";
        return true;
    }

    public static bool TryConfirmCorrections(AnimationAsset asset, out string reason)
    {
        reason = "Choose a valid Clip and Locomotion role first.";
        if (asset == null || asset.Clip == null || asset.LocomotionData == null
            || asset.LocomotionData.Role == LocomotionAnimationRole.None) return false;
        // Validate a draft before committing, so failed confirmation leaves the asset untouched.
        var draft = JsonUtility.FromJson<LocomotionAnimationData>(JsonUtility.ToJson(asset.LocomotionData));
        draft.EditorConfirmSource(asset.Clip, LocomotionDataSourceHash.Compute(asset));
        if (draft.Role == LocomotionAnimationRole.MoveCycle && !draft.TryGetCycle(asset.Clip, out _, out reason)) return false;
        if (draft.Role == LocomotionAnimationRole.Transition && !draft.TryGetExitPhase(asset.Clip, out _))
        { reason = "Set Exit Phase Confirmed and a finite exit phase between 0 and 1."; return false; }
        Undo.RecordObject(asset, "Confirm Locomotion Corrections");
        asset.EditorSetLocomotionData(draft);
        Save(asset);
        reason = "Manual corrections confirmed for the current source.";
        return true;
    }

    public static void DrawControls(AnimationAsset asset)
    {
        var status = GetStatus(asset);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Locomotion Foot Analysis", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(status.Message, status.Code == AnimationAssetBakeStatusCode.Ready ? MessageType.Info : MessageType.Warning);
        var automatic = asset != null ? asset.LocomotionData?.Automatic : null;
        if (automatic != null)
        {
            EditorGUILayout.LabelField("Contact candidates", $"Left: {automatic.LeftContacts.Length}, Right: {automatic.RightContacts.Length}");
            if (automatic.HasExitPhaseCandidate)
                EditorGUILayout.LabelField("Exit phase candidate (unconfirmed)", automatic.ExitPhaseCandidate.ToString("F3"));
            using (new EditorGUI.DisabledScope(!asset.IsLocomotionDataCurrent || !asset.LocomotionData.AutomaticMatchesSource))
            {
                if (automatic.Cycle != null && GUILayout.Button("Copy Cycle Candidate To Manual Corrections"))
                {
                    Undo.RecordObject(asset, "Copy Locomotion Cycle Candidate");
                    var copy = JsonUtility.FromJson<LocomotionCycleMapping>(JsonUtility.ToJson(automatic.Cycle));
                    asset.LocomotionData.EditorSetCycle(copy);
                    Save(asset);
                }
                if (automatic.HasExitPhaseCandidate && GUILayout.Button("Use And Confirm Exit Phase Candidate"))
                {
                    // An explicit author action confirms the semantic point, not the analyzer itself.
                    Undo.RecordObject(asset, "Confirm Locomotion Exit Candidate");
                    asset.LocomotionData.EditorSetExitPhase(automatic.ExitPhaseCandidate);
                    Save(asset);
                }
            }
            using (new EditorGUI.DisabledScope(true))
            {
                if (asset != null)
                {
                    var serialized = new SerializedObject(asset);
                    var property = serialized.FindProperty("_locomotionData").FindPropertyRelative("automatic");
                    EditorGUILayout.PropertyField(property, new GUIContent("Automatic Candidates"), true);
                }
            }
        }
        using (new EditorGUI.DisabledScope(asset == null || asset.Clip == null))
        {
            if (GUILayout.Button("Analyze Foot Contacts (Preserve Corrections)"))
            {
                bool ok = TryAnalyze(asset, out string message);
                if (ok) Debug.Log(message, asset); else Debug.LogWarning(message, asset);
            }
            if (GUILayout.Button("Confirm Manual Corrections For Current Source"))
            {
                bool ok = TryConfirmCorrections(asset, out string message);
                if (ok) Debug.Log(message, asset); else Debug.LogWarning(message, asset);
            }
        }
    }

    private static void Save(AnimationAsset asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
#endif
