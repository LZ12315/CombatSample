using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Animancer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public static class BuildGameplayAuthorityCutoverMigrator
{
    private const int FrameRate = CombatSimulationTiming.FrameRate;
    private const string MenuRoot = "Tools/CombatSample/E3-G/";
    private const string JaegerActionListPath = "Assets/Create/ActionLists/Jaeger.asset";
    private const string KianaActionListPath = "Assets/Create/ActionLists/Kiana.asset";
    private const string MigratedActionRoot = "Assets/Create/ActionAssets/E3G_BuildCutover";
    private const string JaegerAnimationConfigPath = "Assets/Create/Jaeger_AnimationConfig.asset";
    private const string JaegerReferenceRigPath = "Assets/Resources/Models/StrikeJaeger/Monster_StrikeJaeger_Model.fbx";
    private const string KianaAnimationConfigPath = "Assets/Create/Kiana_AnimationConfig.asset";
    private const string KianaLocomotionModePath = "Assets/Create/Kiana_NormalLocomotionMode.asset";
    private const string KianaReferenceRigPath = "Assets/Resources/Models/Kiana/Avatar_Kiana_C2_Model.FBX";
    private const string KianaIdleActionPath = "Assets/Create/ActionAssets/Kiana/Locomotion/Kiana_Idle/Kiana_Idle.asset";
    private const string KianaMoveActionPath = "Assets/Create/ActionAssets/Kiana/Locomotion/Kiana_NormalLoco/Kiana_NormalLoco.asset";
    private const string JaegerRetreatAttackPath = "Assets/Create/ActionAssets/Jaeger/Battle/Jaeger_RetreatAttack/Jaeger_RetreatAttack.asset";
    private const string JaegerActionRoot = "Assets/Create/ActionAssets/Jaeger/";
    private const string KianaActionRoot = "Assets/Create/ActionAssets/Kiana/";

    private static readonly Regex ActionListGuidRegex = new(
        @"propertyPath:\s*_actionList\s*\r?\n\s*value:\s*\r?\n\s*objectReference:\s*\{fileID:\s*11400000,\s*guid:\s*([0-9a-f]{32}),",
        RegexOptions.Compiled);

    [MenuItem(MenuRoot + "Preview Active Build Cutover")]
    public static void PreviewActiveBuildCutover()
    {
        CutoverReport report = BuildReport();
        Debug.Log(report.ToString());
    }

    [MenuItem(MenuRoot + "Bake Active Build Support Assets")]
    public static void BakeActiveBuildSupportAssets()
    {
        AnimationConfig jaegerConfig = CreateOrUpdateJaegerAnimationConfig();
        AnimationConfig kianaConfig = CreateOrUpdateKianaAnimationConfig();
        RootMotionBakeBatchResult jaegerBakeResult = BakeRootMotion(jaegerConfig, "Jaeger");
        RootMotionBakeBatchResult kianaBakeResult = BakeRootMotion(kianaConfig, "Kiana");
        LocomotionModeAsset kianaLocomotion = CreateOrUpdateKianaLocomotionMode();
        BindKianaRuntimeProfiles(kianaConfig, kianaLocomotion);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[E3-G] Baked support assets. AnimationConfigs: {JaegerAnimationConfigPath}, " +
            $"{KianaAnimationConfigPath}; LocomotionMode: {KianaLocomotionModePath}; " +
            $"Jaeger {jaegerBakeResult.Summary} Kiana {kianaBakeResult.Summary}");
    }

    [MenuItem(MenuRoot + "Apply Active Build Cutover")]
    public static void ApplyActiveBuildCutover()
    {
        CutoverReport report = BuildReport();
        if (report.UnsupportedActiveActionListPaths.Count > 0)
            throw new InvalidOperationException(report.BuildUnsupportedActionListMessage());

        AnimationConfig jaegerConfig = CreateOrUpdateJaegerAnimationConfig();
        AnimationConfig kianaConfig = CreateOrUpdateKianaAnimationConfig();
        RootMotionBakeBatchResult jaegerBakeResult = BakeRootMotion(jaegerConfig, "Jaeger");
        RootMotionBakeBatchResult kianaBakeResult = BakeRootMotion(kianaConfig, "Kiana");
        if (!jaegerBakeResult.IsSuccess || !kianaBakeResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Active Root Motion bake failed. Jaeger: {jaegerBakeResult.Summary} " +
                $"Kiana: {kianaBakeResult.Summary}");
        }

        LocomotionModeAsset kianaLocomotion = CreateOrUpdateKianaLocomotionMode();
        BindKianaRuntimeProfiles(kianaConfig, kianaLocomotion);
        RemoveKianaLegacyLocomotionActions();
        AnimationKeyResolver keyResolver = AnimationKeyResolver.Build();
        var actionListUsage = BuildActionListUsage();
        var processedActions = new HashSet<ActionAsset>();
        int generatedOrRebuilt = 0;
        int duplicated = 0;
        int directDependencies = 0;

        foreach (string actionListPath in GetSupportedActiveActionListPaths())
        {
            ActionAssetList list = AssetDatabase.LoadAssetAtPath<ActionAssetList>(actionListPath);
            if (list == null)
                throw new InvalidOperationException($"Missing active ActionList at {actionListPath}.");

            SerializedObject serializedList = new SerializedObject(list);
            SerializedProperty actions = serializedList.FindProperty("_globalActions");
            if (actions == null || !actions.isArray)
                throw new InvalidOperationException($"{actionListPath} has no _globalActions array.");

            for (int i = 0; i < actions.arraySize; i++)
            {
                SerializedProperty actionSlot = actions.GetArrayElementAtIndex(i);
                ActionAsset source = actionSlot.objectReferenceValue as ActionAsset;
                if (source == null)
                    continue;

                ActionAsset target = EnsureActiveSequenceCopy(source, actionListPath, actionListUsage, out bool copied);
                if (copied)
                {
                    actionSlot.objectReferenceValue = target;
                    duplicated++;
                }

                bool rebuildGeneratedSequence = target.UsesSequence
                                                && !string.Equals(
                                                    AssetDatabase.GetAssetPath(target),
                                                    JaegerRetreatAttackPath,
                                                    StringComparison.Ordinal);
                if (processedActions.Add(target)
                    && ConvertActionToSequence(target, keyResolver, rebuildGeneratedSequence))
                    generatedOrRebuilt++;
            }

            serializedList.ApplyModifiedProperties();
            EditorUtility.SetDirty(list);
        }

        // Persist list rewrites before asking AssetDatabase for the final active dependency
        // closure, otherwise copied shared actions can still appear through stale list links.
        AssetDatabase.SaveAssets();

        // ActionAssets can also be referenced directly by NodeCanvas graphs. They are not
        // necessarily members of an ActionList, but they are still active build content and
        // must cross the same Sequence authority boundary.
        foreach (ActionAsset action in FindActionAssetsReferencedByEnabledBuildScenes())
        {
            if (action == null || !processedActions.Add(action))
                continue;

            string actionPath = AssetDatabase.GetAssetPath(action);
            bool rebuildGeneratedSequence = action.UsesSequence
                                            && !string.Equals(
                                                actionPath,
                                                JaegerRetreatAttackPath,
                                                StringComparison.Ordinal);
            if (ConvertActionToSequence(action, keyResolver, rebuildGeneratedSequence))
            {
                generatedOrRebuilt++;
                directDependencies++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CutoverReport finalReport = BuildReport();
        if (finalReport.AnimationConfigIssues.Count > 0)
            throw new InvalidOperationException(finalReport.BuildAnimationConfigIssueMessage());

        Debug.Log(
            $"[E3-G] Active build cutover applied. generatedOrRebuilt={generatedOrRebuilt}, " +
            $"directDependencies={directDependencies}, duplicatedSharedLegacy={duplicated}");
    }

    private static CutoverReport BuildReport()
    {
        var report = new CutoverReport();
        HashSet<string> supportedLists = GetSupportedActiveActionListPaths();

        foreach (string actionListPath in FindActionListsReferencedByEnabledBuildScenes())
        {
            report.ActiveActionListPaths.Add(actionListPath);
            if (!supportedLists.Contains(actionListPath))
                report.UnsupportedActiveActionListPaths.Add(actionListPath);
        }

        var activeActions = new HashSet<ActionAsset>(FindActionAssetsReferencedByEnabledBuildScenes());
        foreach (string actionListPath in supportedLists)
        {
            ActionAssetList list = AssetDatabase.LoadAssetAtPath<ActionAssetList>(actionListPath);
            if (list == null)
            {
                report.MissingSupportedActionLists.Add(actionListPath);
                continue;
            }

            foreach (ActionAsset action in ReadActionListActions(list))
            {
                if (action == null)
                    continue;

                activeActions.Add(action);
            }
        }

        foreach (ActionAsset action in activeActions)
        {
            string actionPath = AssetDatabase.GetAssetPath(action);
            if (string.IsNullOrWhiteSpace(actionPath))
                continue;

            report.ActiveActionPaths.Add(actionPath);
            if (action.UsesTimeline)
                report.LegacyActionPaths.Add(actionPath);
        }

        report.ActiveActionPaths.Sort(StringComparer.Ordinal);
        report.LegacyActionPaths.Sort(StringComparer.Ordinal);
        AppendAnimationConfigIssues(
            report,
            "Jaeger",
            JaegerActionListPath,
            JaegerActionRoot,
            JaegerAnimationConfigPath);
        AppendAnimationConfigIssues(
            report,
            "Kiana",
            KianaActionListPath,
            KianaActionRoot,
            KianaAnimationConfigPath);

        return report;
    }

    private static HashSet<string> GetSupportedActiveActionListPaths()
    {
        return new HashSet<string>(StringComparer.Ordinal)
        {
            JaegerActionListPath,
            KianaActionListPath,
        };
    }

    private static IEnumerable<string> FindActionListsReferencedByEnabledBuildScenes()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene == null || !scene.enabled || string.IsNullOrWhiteSpace(scene.path))
                continue;

            string fullPath = Path.GetFullPath(scene.path);
            if (!File.Exists(fullPath))
                continue;

            string text = File.ReadAllText(fullPath);
            foreach (Match match in ActionListGuidRegex.Matches(text))
            {
                string guid = match.Groups[1].Value;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    result.Add(path);
            }
        }

        return result;
    }

    private static IEnumerable<ActionAsset> FindActionAssetsReferencedByEnabledBuildScenes()
    {
        var result = new HashSet<ActionAsset>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene == null || !scene.enabled || string.IsNullOrWhiteSpace(scene.path))
                continue;

            foreach (string dependencyPath in AssetDatabase.GetDependencies(scene.path, true))
            {
                ActionAsset action = AssetDatabase.LoadAssetAtPath<ActionAsset>(dependencyPath);
                if (action != null)
                    result.Add(action);
            }
        }

        return result;
    }

    private static Dictionary<ActionAsset, List<string>> BuildActionListUsage()
    {
        var usage = new Dictionary<ActionAsset, List<string>>();
        string[] guids = AssetDatabase.FindAssets("t:ActionAssetList");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ActionAssetList list = AssetDatabase.LoadAssetAtPath<ActionAssetList>(path);
            if (list == null)
                continue;

            foreach (ActionAsset action in ReadActionListActions(list))
            {
                if (action == null)
                    continue;

                if (!usage.TryGetValue(action, out List<string> paths))
                {
                    paths = new List<string>();
                    usage.Add(action, paths);
                }

                if (!paths.Contains(path))
                    paths.Add(path);
            }
        }

        return usage;
    }

    private static IEnumerable<ActionAsset> ReadActionListActions(ActionAssetList list)
    {
        SerializedObject serializedList = new SerializedObject(list);
        SerializedProperty actions = serializedList.FindProperty("_globalActions");
        if (actions == null || !actions.isArray)
            yield break;

        for (int i = 0; i < actions.arraySize; i++)
            yield return actions.GetArrayElementAtIndex(i).objectReferenceValue as ActionAsset;
    }

    private static ActionAsset EnsureActiveSequenceCopy(
        ActionAsset source,
        string activeActionListPath,
        Dictionary<ActionAsset, List<string>> actionListUsage,
        out bool copied)
    {
        copied = false;
        if (source == null)
            return null;

        if (!actionListUsage.TryGetValue(source, out List<string> usagePaths))
            return source;

        bool usedByInactiveList = false;
        foreach (string usagePath in usagePaths)
        {
            if (!string.Equals(usagePath, activeActionListPath, StringComparison.Ordinal)
                && !GetSupportedActiveActionListPaths().Contains(usagePath))
            {
                usedByInactiveList = true;
                break;
            }
        }

        if (!usedByInactiveList)
            return source;

        string sourcePath = AssetDatabase.GetAssetPath(source);
        string targetDirectory = $"{MigratedActionRoot}/{source.name}";
        string targetPath = $"{targetDirectory}/{source.name}_Sequence.asset";
        Directory.CreateDirectory(targetDirectory);

        ActionAsset existing = AssetDatabase.LoadAssetAtPath<ActionAsset>(targetPath);
        if (existing != null)
            return existing;

        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            throw new InvalidOperationException($"Failed to duplicate shared legacy ActionAsset: {sourcePath} -> {targetPath}");

        copied = true;
        return AssetDatabase.LoadAssetAtPath<ActionAsset>(targetPath);
    }

    private static bool ConvertActionToSequence(
        ActionAsset action,
        AnimationKeyResolver keyResolver,
        bool rebuildGeneratedSequence)
    {
        if (action == null)
            return false;
        if (!rebuildGeneratedSequence
            && action.UsesSequence
            && action.SequenceData != null
            && action.SequenceData.Clips.Count > 0)
            return false;

        TimelineAsset timeline = action.TimelineAsset;
        if (timeline == null)
            throw new InvalidOperationException($"{AssetDatabase.GetAssetPath(action)} has no TimelineAsset.");

        List<ActionSequenceClipDefinition> stateClips = new();
        List<ActionSequenceClipDefinition> animationClips = new();
        List<ActionSequenceClipDefinition> motionClips = new();
        List<ActionSequenceClipDefinition> hitBoxClips = new();

        int durationFrames = CalculateDurationFrames(timeline);
        AddWholeActionMotionPolicy(action, motionClips, durationFrames);
        AddFacingSnapFromMotionConfig(action, motionClips);

        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track == null || track.muted)
                continue;

            foreach (TimelineClip timelineClip in track.GetClips())
            {
                ActionSequenceClipDefinition convertedClip = ConvertTimelineClip(action, timelineClip, keyResolver, out bool addRootMotionPair);
                if (convertedClip == null)
                    continue;

                AddClipToBucket(convertedClip, stateClips, animationClips, motionClips, hitBoxClips);

                if (addRootMotionPair && convertedClip is ActionSequenceAnimationPoseClipDefinition poseClip)
                {
                    AddRootMotionPair(poseClip.AnimationKey, timelineClip, motionClips);
                }
            }
        }

        if (animationClips.Count == 0)
            throw new InvalidOperationException($"{AssetDatabase.GetAssetPath(action)} cannot be converted: no Animancer pose clip was found.");

        ActionSequenceData data = action.SequenceData;
        data.InitializeNewSequenceDefaults();
        data.EditorTracks.Clear();
        data.EditorClips.Clear();
        data.EditorSetTiming(FrameRate, durationFrames);
        data.EditorSetDurationMode(ActionSequenceDurationMode.FixedFrames);

        AddTrack(data, new ActionSequenceStateTrack { displayName = "State" }, stateClips);
        AddTrack(data, new ActionSequenceAnimationTrack { displayName = "Animation" }, animationClips);
        AddTrack(data, new ActionSequenceMotionTrack { displayName = "Motion" }, motionClips);
        AddTrack(data, new ActionSequenceHitBoxTrack { displayName = "HitBox" }, hitBoxClips);

        action.SetPlaybackBackend(ActionPlaybackBackend.Sequence);
        EditorUtility.SetDirty(action);
        return true;
    }

    private static ActionSequenceClipDefinition ConvertTimelineClip(
        ActionAsset action,
        TimelineClip timelineClip,
        AnimationKeyResolver keyResolver,
        out bool addRootMotionPair)
    {
        addRootMotionPair = false;
        if (timelineClip == null || timelineClip.asset == null)
            return null;

        int startFrame = SecondsToStartFrame(timelineClip.start);
        int endFrame = SecondsToEndFrame(timelineClip.start + timelineClip.duration, startFrame);

        switch (timelineClip.asset)
        {
            case AnimancerClip clip:
            {
                string key = keyResolver.Resolve(clip.transitionAsset);
                var sequenceClip = new ActionSequenceAnimationPoseClipDefinition
                {
                    displayName = timelineClip.displayName,
                    startFrame = startFrame,
                    endFrame = endFrame,
                    parameterMode = clip.parameterMode,
                    fallbackVector2 = clip.fallbackVector2,
                    fallbackFloat = clip.fallbackFloat,
                    startOffsetSeconds = (float)timelineClip.clipIn,
                    playbackSpeed = (float)timelineClip.timeScale,
                };
                SetPrivateField(sequenceClip, "animationKey", key);
                addRootMotionPair = RequireManagedRootMotionTrajectory(
                    action,
                    clip.transitionAsset,
                    keyResolver,
                    key);
                return sequenceClip;
            }

            case ContinuousAnimancerClip clip:
            {
                string key = keyResolver.Resolve(clip.transitionAsset);
                var sequenceClip = new ActionSequenceAnimationPoseClipDefinition
                {
                    displayName = timelineClip.displayName,
                    startFrame = startFrame,
                    endFrame = endFrame,
                    parameterMode = ConvertParameterSource(clip.parameterSource),
                    startOffsetSeconds = (float)timelineClip.clipIn,
                    playbackSpeed = (float)timelineClip.timeScale,
                };
                SetPrivateField(sequenceClip, "animationKey", key);
                addRootMotionPair = RequireManagedRootMotionTrajectory(
                    action,
                    clip.transitionAsset,
                    keyResolver,
                    key);
                return sequenceClip;
            }

            case ActionHitBoxClip clip:
                return new ActionSequenceHitBoxClipDefinition
                {
                    displayName = timelineClip.displayName,
                    startFrame = startFrame,
                    endFrame = endFrame,
                    boneReference = clip.boneReference,
                    hitboxConfig = CloneHitBoxConfig(clip.hitboxConfig),
                    dataConfig = CloneAttackDataConfig(clip.dataConfig),
                    effects = CloneManagedList(clip.effects),
                };

            case ActionTagClip clip:
                return new ActionSequenceTagClipDefinition
                {
                    displayName = timelineClip.displayName,
                    startFrame = startFrame,
                    endFrame = endFrame,
                    tag = clip.tag,
                    targetContainer = clip.targetContainer,
                };

            case ActionVelocityClip clip:
                return new ActionSequenceVelocityOverrideClipDefinition
                {
                    displayName = timelineClip.displayName,
                    startFrame = startFrame,
                    endFrame = endFrame,
                    config = CloneVelocityConfig(clip.config),
                };

            case ActionImpulseClip clip:
            {
                float horizontalForce = clip.config != null ? clip.config.horizontalForce : 0f;
                float verticalForce = clip.config != null ? clip.config.verticalForce : 0f;
                bool useHorizontal = Math.Abs(horizontalForce) > 0.0001f;
                bool useVertical = Math.Abs(verticalForce) > 0.0001f;
                if (!useHorizontal && !useVertical)
                    return null;

                return new ActionSequenceImpulseClipDefinition
                {
                    displayName = timelineClip.displayName,
                    startFrame = startFrame,
                    endFrame = endFrame,
                    config = CloneImpulseConfig(clip.config),
                    useHorizontalImpulse = useHorizontal,
                    useVerticalBallistic = useVertical,
                    verticalOperation = ActionSequenceBallisticVelocityOperation.Add,
                };
            }

            case ActionMagnetismClip clip:
                if (clip.config == null || !clip.config.rotateToTarget || clip.config.rotationMode == MagnetismRotationMode.None)
                    return null;

                var rotation = new ActionSequenceSelfRotationClipDefinition
                {
                    displayName = timelineClip.displayName,
                    startFrame = startFrame,
                    endFrame = endFrame,
                };
                SetPrivateField(rotation, "source", clip.useCombatTarget
                    ? SelfRotationSource.Target
                    : SelfRotationSource.Direction);
                SetPrivateField(rotation, "mode", clip.config.rotationMode == MagnetismRotationMode.InstantSnap
                    ? SelfRotationMode.Snap
                    : SelfRotationMode.RotateBySpeed);
                SetPrivateField(rotation, "targetSource", SelfRotationTargetSource.CombatTarget);
                SetPrivateField(rotation, "directionSource", SelfRotationDirectionSource.ContextDirection);
                SetPrivateField(rotation, "angularSpeedDegrees", clip.config.rotationAngularSpeed);
                return rotation;
        }

        return null;
    }

    private static bool RequireManagedRootMotionTrajectory(
        ActionAsset action,
        TransitionAsset transition,
        AnimationKeyResolver keyResolver,
        string animationKey)
    {
        if (action.MotionConfig.rootMotionMode != RootMotionApplyMode.Managed)
            return false;

        if (!keyResolver.HasTrajectory(transition))
        {
            throw new InvalidOperationException(
                $"{AssetDatabase.GetAssetPath(action)} requires migrated Managed Root Motion, " +
                $"but animation key '{animationKey}' has no baked trajectory.");
        }

        return true;
    }

    private static void AddWholeActionMotionPolicy(
        ActionAsset action,
        List<ActionSequenceClipDefinition> motionClips,
        int durationFrames)
    {
        ActionMotionConfig motionConfig = action.MotionConfig;
        bool useLocomotion = motionConfig.suppressLocomotion;
        bool useGravity = motionConfig.gravityScale >= 0f;
        if (!useLocomotion && !useGravity)
            return;

        var clip = new ActionSequenceMotionPolicyClipDefinition
        {
            displayName = "Migrated Motion Policy",
            startFrame = 0,
            endFrame = durationFrames,
        };
        SetPrivateField(clip, "useLocomotionScale", useLocomotion);
        SetPrivateField(clip, "locomotionScale", useLocomotion ? 0f : 1f);
        SetPrivateField(clip, "useAirLocomotionScale", useLocomotion);
        SetPrivateField(clip, "airLocomotionScale", useLocomotion ? 0f : 1f);
        SetPrivateField(clip, "useGravityScale", useGravity);
        SetPrivateField(clip, "gravityScale", useGravity ? motionConfig.gravityScale : 1f);
        motionClips.Add(clip);
    }

    private static void AddFacingSnapFromMotionConfig(
        ActionAsset action,
        List<ActionSequenceClipDefinition> motionClips)
    {
        ActionFacingOnStart facing = action.MotionConfig.facingOnStart;
        if (facing == ActionFacingOnStart.None)
            return;

        var clip = new ActionSequenceFacingSnapClipDefinition
        {
            displayName = "Migrated Facing Snap",
            startFrame = 0,
            endFrame = 1,
        };
        SetPrivateField(clip, "source", facing == ActionFacingOnStart.SnapToInput
            ? ActionSequenceFacingSnapSource.LocomotionIntentThenContextDirection
            : ActionSequenceFacingSnapSource.CombatTargetThenContextDirection);
        motionClips.Add(clip);
    }

    private static void AddRootMotionPair(
        string animationKey,
        TimelineClip timelineClip,
        List<ActionSequenceClipDefinition> motionClips)
    {
        int startFrame = SecondsToStartFrame(timelineClip.start);
        int endFrame = SecondsToEndFrame(timelineClip.start + timelineClip.duration, startFrame);
        float startOffset = (float)timelineClip.clipIn;
        float speed = (float)timelineClip.timeScale;

        var rootMotion = new ActionSequenceRootMotionClipDefinition
        {
            displayName = "Migrated Root Motion",
            startFrame = startFrame,
            endFrame = endFrame,
            startOffsetSeconds = startOffset,
            playbackSpeed = speed,
        };
        SetPrivateField(rootMotion, "animationKey", animationKey);
        motionClips.Add(rootMotion);

        var rootRotation = new ActionSequenceRootRotationClipDefinition
        {
            displayName = "Migrated Root Rotation",
            startFrame = startFrame,
            endFrame = endFrame,
            startOffsetSeconds = startOffset,
            playbackSpeed = speed,
        };
        SetPrivateField(rootRotation, "animationKey", animationKey);
        motionClips.Add(rootRotation);
    }

    private static void AddTrack(
        ActionSequenceData data,
        ActionSequenceTrackDefinition track,
        List<ActionSequenceClipDefinition> clips)
    {
        if (clips.Count == 0)
            return;

        foreach (ActionSequenceClipDefinition clip in clips)
            track.AddClip(clip);

        data.EditorTracks.Add(track);
    }

    private static void AddClipToBucket(
        ActionSequenceClipDefinition clip,
        List<ActionSequenceClipDefinition> stateClips,
        List<ActionSequenceClipDefinition> animationClips,
        List<ActionSequenceClipDefinition> motionClips,
        List<ActionSequenceClipDefinition> hitBoxClips)
    {
        switch (clip.Kind)
        {
            case ActionSequenceTrackKind.State:
                stateClips.Add(clip);
                break;
            case ActionSequenceTrackKind.Animation:
                animationClips.Add(clip);
                break;
            case ActionSequenceTrackKind.Motion:
                motionClips.Add(clip);
                break;
            case ActionSequenceTrackKind.HitBox:
                hitBoxClips.Add(clip);
                break;
        }
    }

    private static int CalculateDurationFrames(TimelineAsset timeline)
    {
        int maxEndFrame = 0;
        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track == null || track.muted)
                continue;

            foreach (TimelineClip clip in track.GetClips())
            {
                int startFrame = SecondsToStartFrame(clip.start);
                maxEndFrame = Math.Max(
                    maxEndFrame,
                    SecondsToEndFrame(clip.start + clip.duration, startFrame));
            }
        }

        if (maxEndFrame <= 0 && timeline.duration > 0d && !double.IsInfinity(timeline.duration))
            maxEndFrame = SecondsToEndFrame(timeline.duration, 0);

        return Mathf.Max(1, maxEndFrame);
    }

    private static int SecondsToStartFrame(double seconds)
    {
        return Mathf.Max(0, Mathf.RoundToInt((float)(seconds * FrameRate)));
    }

    private static int SecondsToEndFrame(double seconds, int startFrame)
    {
        double frames = seconds * FrameRate;
        double nearestFrame = Math.Round(frames);
        int endFrame = Math.Abs(frames - nearestFrame) <= 0.001d
            ? (int)nearestFrame
            : (int)Math.Ceiling(frames);
        return Mathf.Max(startFrame + 1, endFrame);
    }

    private static AnimancerParameterMode ConvertParameterSource(ContinuousParameterSource source)
    {
        return source switch
        {
            ContinuousParameterSource.VerticalVelocity => AnimancerParameterMode.VerticalVelocity,
            ContinuousParameterSource.LocomotionIntent => AnimancerParameterMode.ContextDirection2D,
            _ => AnimancerParameterMode.None,
        };
    }

    private static List<T> CloneManagedList<T>(List<T> source) where T : class
    {
        return source != null ? new List<T>(source) : new List<T>();
    }

    private static ActionHitBoxConfig CloneHitBoxConfig(ActionHitBoxConfig source)
    {
        if (source == null)
            return new ActionHitBoxConfig();

        return new ActionHitBoxConfig
        {
            center = source.center,
            rotation = source.rotation,
            height = source.height,
            radius = source.radius,
        };
    }

    private static AttackDataConfig CloneAttackDataConfig(AttackDataConfig source)
    {
        if (source == null)
            return new AttackDataConfig();

        return new AttackDataConfig
        {
            targetLayers = source.targetLayers,
            _baseDamage = source._baseDamage,
            hitEventTag = source.hitEventTag,
        };
    }

    private static VelocityConfig CloneVelocityConfig(VelocityConfig source)
    {
        if (source == null)
            return new VelocityConfig();

        return new VelocityConfig
        {
            directionMode = source.directionMode,
            localHorizontalDirection = source.localHorizontalDirection,
            useHorizontalVelocity = source.useHorizontalVelocity,
            horizontalSpeed = source.horizontalSpeed,
            useVerticalVelocity = source.useVerticalVelocity,
            verticalSpeed = source.verticalSpeed,
            horizontalCurve = source.horizontalCurve,
            verticalCurve = source.verticalCurve,
            debugLog = source.debugLog,
        };
    }

    private static ImpulseConfig CloneImpulseConfig(ImpulseConfig source)
    {
        if (source == null)
            return new ImpulseConfig();

        return new ImpulseConfig
        {
            directionMode = source.directionMode,
            localHorizontalDirection = source.localHorizontalDirection,
            horizontalForce = source.horizontalForce,
            verticalForce = source.verticalForce,
            debugLog = source.debugLog,
        };
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);

        field.SetValue(target, value);
    }

    private static AnimationConfig CreateOrUpdateJaegerAnimationConfig()
    {
        AnimationConfig config = AssetDatabase.LoadAssetAtPath<AnimationConfig>(JaegerAnimationConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<AnimationConfig>();
            AssetDatabase.CreateAsset(config, JaegerAnimationConfigPath);
        }

        var entries = new List<AnimationConfigEntry>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var seenTransitions = new HashSet<TransitionAsset>();
        var existingTrajectories = new Dictionary<string, RootMotionTrajectory>(StringComparer.Ordinal);

        foreach (AnimationConfigEntry existingEntry in config.Entries)
        {
            if (existingEntry == null
                || existingEntry.TransitionAsset == null
                || string.IsNullOrWhiteSpace(existingEntry.Key))
            {
                continue;
            }

            if (existingEntry.RootMotionTrajectory != null)
            {
                existingTrajectories[BuildAnimationEntryIdentity(
                    existingEntry.Key,
                    existingEntry.TransitionAsset)] = existingEntry.RootMotionTrajectory;
            }

            if (!seenKeys.Add(existingEntry.Key))
                continue;

            entries.Add(new AnimationConfigEntry(
                existingEntry.Key,
                existingEntry.TransitionAsset,
                existingEntry.RootMotionTrajectory));
            seenTransitions.Add(existingEntry.TransitionAsset);
        }

        foreach (TransitionAsset transition in CollectTransitionsFromActorActionClosure(
                     JaegerActionListPath,
                     JaegerActionRoot))
        {
            if (transition == null || !seenTransitions.Add(transition))
                continue;

            AddEntry(entries, seenKeys, existingTrajectories, transition.name, transition);
        }

        GameObject referenceRig = AssetDatabase.LoadAssetAtPath<GameObject>(JaegerReferenceRigPath);
        config.EditorSetRootMotionBakeSettings(referenceRig, FrameRate);
        config.EditorSetEntries(entries.ToArray());
        EditorUtility.SetDirty(config);
        return config;
    }

    private static AnimationConfig CreateOrUpdateKianaAnimationConfig()
    {
        AnimationConfig config = AssetDatabase.LoadAssetAtPath<AnimationConfig>(KianaAnimationConfigPath);
        var existingTrajectories = new Dictionary<string, RootMotionTrajectory>(StringComparer.Ordinal);
        if (config != null)
        {
            foreach (AnimationConfigEntry existingEntry in config.Entries)
            {
                if (existingEntry == null
                    || existingEntry.TransitionAsset == null
                    || existingEntry.RootMotionTrajectory == null
                    || string.IsNullOrWhiteSpace(existingEntry.Key))
                {
                    continue;
                }

                existingTrajectories[BuildAnimationEntryIdentity(existingEntry.Key, existingEntry.TransitionAsset)] =
                    existingEntry.RootMotionTrajectory;
            }
        }

        var entries = new List<AnimationConfigEntry>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (TransitionAsset transition in CollectTransitionsFromActorActionClosure(
                     KianaActionListPath,
                     KianaActionRoot))
        {
            if (transition == null)
                continue;

            AddEntry(entries, seenKeys, existingTrajectories, transition.name, transition);
            if (string.Equals(transition.name, "Kiana_Idle", StringComparison.Ordinal))
                AddEntry(entries, seenKeys, existingTrajectories, "locomotion_idle", transition);
            if (string.Equals(transition.name, "Kiana_NormalLoco", StringComparison.Ordinal))
                AddEntry(entries, seenKeys, existingTrajectories, "locomotion_move", transition);
        }

        AddTransitionsFromActionPath(KianaIdleActionPath, entries, seenKeys, existingTrajectories);
        AddTransitionsFromActionPath(KianaMoveActionPath, entries, seenKeys, existingTrajectories);

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<AnimationConfig>();
            Directory.CreateDirectory(Path.GetDirectoryName(KianaAnimationConfigPath) ?? "Assets/Create");
            AssetDatabase.CreateAsset(config, KianaAnimationConfigPath);
        }

        GameObject referenceRig = AssetDatabase.LoadAssetAtPath<GameObject>(KianaReferenceRigPath);
        config.EditorSetRootMotionBakeSettings(referenceRig, FrameRate);
        config.EditorSetEntries(entries.ToArray());
        EditorUtility.SetDirty(config);
        return config;
    }

    private static void AddEntry(
        List<AnimationConfigEntry> entries,
        HashSet<string> seenKeys,
        Dictionary<string, RootMotionTrajectory> existingTrajectories,
        string key,
        TransitionAsset transition)
    {
        if (string.IsNullOrWhiteSpace(key) || transition == null || !seenKeys.Add(key))
            return;

        existingTrajectories.TryGetValue(
            BuildAnimationEntryIdentity(key, transition),
            out RootMotionTrajectory trajectory);
        entries.Add(new AnimationConfigEntry(key, transition, trajectory));
    }

    private static string BuildAnimationEntryIdentity(string key, TransitionAsset transition)
    {
        return $"{key}\n{AssetDatabase.GetAssetPath(transition)}";
    }

    private static void AddTransitionsFromActionPath(
        string actionPath,
        List<AnimationConfigEntry> entries,
        HashSet<string> seenKeys,
        Dictionary<string, RootMotionTrajectory> existingTrajectories)
    {
        ActionAsset action = AssetDatabase.LoadAssetAtPath<ActionAsset>(actionPath);
        foreach (TransitionAsset transition in CollectTransitionsFromAction(action))
        {
            if (transition == null)
                continue;

            AddEntry(entries, seenKeys, existingTrajectories, transition.name, transition);
            if (string.Equals(actionPath, KianaIdleActionPath, StringComparison.Ordinal))
                AddEntry(entries, seenKeys, existingTrajectories, "locomotion_idle", transition);
            if (string.Equals(actionPath, KianaMoveActionPath, StringComparison.Ordinal))
                AddEntry(entries, seenKeys, existingTrajectories, "locomotion_move", transition);
        }
    }

    private static IEnumerable<TransitionAsset> CollectTransitionsFromActorActionClosure(
        string actionListPath,
        string actorActionRoot)
    {
        foreach (ActionAsset action in CollectActorActionClosure(actionListPath, actorActionRoot))
        {
            foreach (TransitionAsset transition in CollectTransitionsFromAction(action))
                yield return transition;
        }
    }

    private static List<ActionAsset> CollectActorActionClosure(
        string actionListPath,
        string actorActionRoot)
    {
        var roots = new HashSet<ActionAsset>();
        ActionAssetList list = AssetDatabase.LoadAssetAtPath<ActionAssetList>(actionListPath);
        if (list != null)
        {
            foreach (ActionAsset action in ReadActionListActions(list))
            {
                if (action != null)
                    roots.Add(action);
            }
        }

        foreach (ActionAsset action in FindActionAssetsReferencedByEnabledBuildScenes())
        {
            string actionPath = AssetDatabase.GetAssetPath(action);
            if (actionPath.StartsWith(actorActionRoot, StringComparison.Ordinal))
                roots.Add(action);
        }

        var closure = new HashSet<ActionAsset>(roots);
        foreach (ActionAsset root in roots)
        {
            string rootPath = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrWhiteSpace(rootPath))
                continue;

            foreach (string dependencyPath in AssetDatabase.GetDependencies(rootPath, true))
            {
                ActionAsset dependency = AssetDatabase.LoadAssetAtPath<ActionAsset>(dependencyPath);
                if (dependency != null)
                    closure.Add(dependency);
            }
        }

        var orderedClosure = new List<ActionAsset>(closure);
        orderedClosure.Sort((left, right) => string.Compare(
            AssetDatabase.GetAssetPath(left),
            AssetDatabase.GetAssetPath(right),
            StringComparison.Ordinal));
        return orderedClosure;
    }

    private static void AppendAnimationConfigIssues(
        CutoverReport report,
        string actorName,
        string actionListPath,
        string actorActionRoot,
        string animationConfigPath)
    {
        AnimationConfig config = AssetDatabase.LoadAssetAtPath<AnimationConfig>(animationConfigPath);
        if (config == null)
        {
            report.AnimationConfigIssues.Add($"{actorName}: missing AnimationConfig at {animationConfigPath}.");
            return;
        }

        var seenIssues = new HashSet<string>(StringComparer.Ordinal);
        foreach (ActionAsset action in CollectActorActionClosure(actionListPath, actorActionRoot))
        {
            if (action == null || !action.UsesSequence || action.SequenceData == null)
                continue;

            string actionPath = AssetDatabase.GetAssetPath(action);
            bool requiresMigratedRootMotion = action.MotionConfig.rootMotionMode == RootMotionApplyMode.Managed
                                                && !string.Equals(
                                                    actionPath,
                                                    JaegerRetreatAttackPath,
                                                    StringComparison.Ordinal);
            foreach (ActionSequenceClipDefinition clip in action.SequenceData.Clips)
            {
                switch (clip)
                {
                    case ActionSequenceAnimationPoseClipDefinition pose:
                        AddAnimationConfigIssueIfMissing(
                            config,
                            actorName,
                            actionPath,
                            pose.AnimationKey,
                            false,
                            report.AnimationConfigIssues,
                            seenIssues);
                        if (requiresMigratedRootMotion)
                        {
                            AddAnimationConfigIssueIfMissing(
                                config,
                                actorName,
                                actionPath,
                                pose.AnimationKey,
                                true,
                                report.AnimationConfigIssues,
                                seenIssues);
                        }
                        break;

                    case ActionSequenceRootMotionClipDefinition rootMotion:
                        AddAnimationConfigIssueIfMissing(
                            config,
                            actorName,
                            actionPath,
                            rootMotion.AnimationKey,
                            true,
                            report.AnimationConfigIssues,
                            seenIssues);
                        break;

                    case ActionSequenceRootRotationClipDefinition rootRotation:
                        AddAnimationConfigIssueIfMissing(
                            config,
                            actorName,
                            actionPath,
                            rootRotation.AnimationKey,
                            true,
                            report.AnimationConfigIssues,
                            seenIssues);
                        break;

                    case ActionSequenceSelfRotationClipDefinition selfRotation
                        when selfRotation.Source == SelfRotationSource.RootRotation:
                        AddAnimationConfigIssueIfMissing(
                            config,
                            actorName,
                            actionPath,
                            selfRotation.AnimationKey,
                            true,
                            report.AnimationConfigIssues,
                            seenIssues);
                        break;
                }
            }
        }
    }

    private static void AddAnimationConfigIssueIfMissing(
        AnimationConfig config,
        string actorName,
        string actionPath,
        string animationKey,
        bool requireTrajectory,
        List<string> issues,
        HashSet<string> seenIssues)
    {
        string issue = null;
        if (string.IsNullOrWhiteSpace(animationKey)
            || !config.TryGetTransition(animationKey, out TransitionAsset _))
        {
            issue = $"{actorName}: action {actionPath} is missing animation key '{animationKey}' in its AnimationConfig.";
        }
        else if (requireTrajectory
                 && !config.TryGetTrajectory(animationKey, out RootMotionTrajectory _))
        {
            issue = $"{actorName}: action {actionPath} is missing Root Motion trajectory for key '{animationKey}'.";
        }

        if (issue != null && seenIssues.Add(issue))
            issues.Add(issue);
    }

    private static IEnumerable<TransitionAsset> CollectTransitionsFromAction(ActionAsset action)
    {
        TimelineAsset timeline = action != null ? action.TimelineAsset : null;
        if (timeline == null)
            yield break;

        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track == null || track.muted)
                continue;

            foreach (TimelineClip clip in track.GetClips())
            {
                switch (clip.asset)
                {
                    case AnimancerClip animancerClip:
                        yield return animancerClip.transitionAsset;
                        break;
                    case ContinuousAnimancerClip continuousClip:
                        yield return continuousClip.transitionAsset;
                        break;
                }
            }
        }
    }

    private static RootMotionBakeBatchResult BakeRootMotion(AnimationConfig config, string actorName)
    {
        if (config == null)
            throw new InvalidOperationException($"{actorName} AnimationConfig is missing.");

        RootMotionBakeBatchResult result = RootMotionBakeWorkflow.BakeAll(config);
        if (!result.IsSuccess)
            Debug.LogError($"[E3-G] {actorName} {result.Summary}", config);
        return result;
    }

    private static LocomotionModeAsset CreateOrUpdateKianaLocomotionMode()
    {
        LocomotionModeAsset mode = AssetDatabase.LoadAssetAtPath<LocomotionModeAsset>(KianaLocomotionModePath);
        if (mode == null)
        {
            mode = ScriptableObject.CreateInstance<LocomotionModeAsset>();
            AssetDatabase.CreateAsset(mode, KianaLocomotionModePath);
        }

        var serializedMode = new SerializedObject(mode);
        serializedMode.FindProperty("priority").intValue = 0;
        serializedMode.FindProperty("entryConditions").arraySize = 0;
        serializedMode.FindProperty("selfTags").arraySize = 0;

        SerializedProperty tuning = serializedMode.FindProperty("tuning");
        tuning.FindPropertyRelative("MoveSpeed").floatValue = 5f;
        tuning.FindPropertyRelative("AirControlFactor").floatValue = 0.4f;
        tuning.FindPropertyRelative("RotateSpeed").floatValue = 600f;

        SerializedProperty animation = serializedMode.FindProperty("animationProfile");
        animation.FindPropertyRelative("IdleKey").stringValue = "locomotion_idle";
        animation.FindPropertyRelative("MoveKey").stringValue = "locomotion_move";
        animation.FindPropertyRelative("MoveThreshold").floatValue = 0.01f;
        animation.FindPropertyRelative("ParameterSource").enumValueIndex =
            (int)LocomotionAnimationParameterSource.LocalDirection2D;

        serializedMode.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mode);
        return mode;
    }

    private static void BindKianaRuntimeProfiles(AnimationConfig config, LocomotionModeAsset locomotionMode)
    {
        ActionAssetList targetList = AssetDatabase.LoadAssetAtPath<ActionAssetList>(KianaActionListPath);
        if (targetList == null || config == null || locomotionMode == null)
            return;

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (buildScene == null || !buildScene.enabled)
                    continue;

                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                bool changed = false;
                foreach (ActionStateManager manager in UnityEngine.Object.FindObjectsOfType<ActionStateManager>(true))
                {
                    SerializedObject managerSo = new SerializedObject(manager);
                    SerializedProperty actionListProperty = managerSo.FindProperty("_actionList");
                    if (actionListProperty == null || actionListProperty.objectReferenceValue != targetList)
                        continue;

                    Actor actor = manager.GetComponent<Actor>();
                    if (actor == null)
                        continue;

                    SerializedObject actorSo = new SerializedObject(actor);
                    SerializedProperty configProperty = actorSo.FindProperty("animationConfig");
                    if (configProperty != null)
                        configProperty.objectReferenceValue = config;

                    ActorLocomotion locomotion = actor.GetComponent<ActorLocomotion>();
                    if (locomotion == null)
                        locomotion = actor.gameObject.AddComponent<ActorLocomotion>();

                    SerializedObject locomotionSo = new SerializedObject(locomotion);
                    locomotionSo.FindProperty("actor").objectReferenceValue = actor;
                    locomotionSo.FindProperty("fallbackMode").objectReferenceValue = locomotionMode;
                    locomotionSo.FindProperty("candidateModes").arraySize = 0;
                    locomotionSo.ApplyModifiedPropertiesWithoutUndo();

                    SerializedProperty actorLocomotionProperty = actorSo.FindProperty("actorLocomotion");
                    if (actorLocomotionProperty != null)
                        actorLocomotionProperty.objectReferenceValue = locomotion;
                    actorSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(locomotion);
                    EditorUtility.SetDirty(actor);
                    changed = true;
                }

                if (changed)
                    EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    private static void RemoveKianaLegacyLocomotionActions()
    {
        ActionAssetList list = AssetDatabase.LoadAssetAtPath<ActionAssetList>(KianaActionListPath);
        ActionAsset idle = AssetDatabase.LoadAssetAtPath<ActionAsset>(KianaIdleActionPath);
        ActionAsset move = AssetDatabase.LoadAssetAtPath<ActionAsset>(KianaMoveActionPath);
        if (list == null || idle == null || move == null)
            throw new InvalidOperationException("Kiana locomotion migration assets are incomplete.");

        var retained = new List<ActionAsset>();
        foreach (ActionAsset action in ReadActionListActions(list))
        {
            if (action != null && action != idle && action != move)
                retained.Add(action);
        }

        var serializedList = new SerializedObject(list);
        SerializedProperty actions = serializedList.FindProperty("_globalActions");
        actions.arraySize = retained.Count;
        for (int i = 0; i < retained.Count; i++)
            actions.GetArrayElementAtIndex(i).objectReferenceValue = retained[i];

        serializedList.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(list);
    }

    private sealed class AnimationKeyResolver
    {
        private readonly Dictionary<TransitionAsset, string> _keys = new();
        private readonly HashSet<TransitionAsset> _trajectoryTransitions = new();

        public static AnimationKeyResolver Build()
        {
            var resolver = new AnimationKeyResolver();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationConfig"))
            {
                AnimationConfig config = AssetDatabase.LoadAssetAtPath<AnimationConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config == null)
                    continue;

                foreach (AnimationConfigEntry entry in config.Entries)
                {
                    if (entry == null || entry.TransitionAsset == null || string.IsNullOrWhiteSpace(entry.Key))
                        continue;

                    if (!resolver._keys.ContainsKey(entry.TransitionAsset))
                        resolver._keys.Add(entry.TransitionAsset, entry.Key);
                    if (entry.RootMotionTrajectory != null)
                        resolver._trajectoryTransitions.Add(entry.TransitionAsset);
                }
            }

            return resolver;
        }

        public string Resolve(TransitionAsset transition)
        {
            if (transition == null)
                throw new InvalidOperationException("Timeline Animancer clip has no TransitionAsset.");

            if (_keys.TryGetValue(transition, out string key))
                return key;

            if (!string.IsNullOrWhiteSpace(transition.name))
                return transition.name;

            throw new InvalidOperationException($"Could not resolve AnimationConfig key for transition {transition}.");
        }

        public bool HasTrajectory(TransitionAsset transition)
        {
            return transition != null && _trajectoryTransitions.Contains(transition);
        }
    }

    private sealed class CutoverReport
    {
        public readonly List<string> ActiveActionListPaths = new();
        public readonly List<string> UnsupportedActiveActionListPaths = new();
        public readonly List<string> MissingSupportedActionLists = new();
        public readonly List<string> ActiveActionPaths = new();
        public readonly List<string> LegacyActionPaths = new();
        public readonly List<string> AnimationConfigIssues = new();

        public string BuildUnsupportedActionListMessage()
        {
            return "E3-G cutover supports only Jaeger/Kiana active build lists. Unsupported active ActionLists:\n"
                + string.Join("\n", UnsupportedActiveActionListPaths);
        }

        public string BuildAnimationConfigIssueMessage()
        {
            return "E3-G active Action closure has incomplete AnimationConfig data:\n"
                + string.Join("\n", AnimationConfigIssues);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine("[E3-G] Active Build Cutover Preview");
            builder.AppendLine($"Active ActionLists: {string.Join(", ", ActiveActionListPaths)}");
            builder.AppendLine($"Active build action count: {ActiveActionPaths.Count}");
            builder.AppendLine($"Legacy actions requiring conversion: {LegacyActionPaths.Count}");
            foreach (string path in LegacyActionPaths)
                builder.AppendLine($"  - {path}");

            builder.AppendLine($"AnimationConfig issues: {AnimationConfigIssues.Count}");
            foreach (string issue in AnimationConfigIssues)
                builder.AppendLine($"  - {issue}");

            if (UnsupportedActiveActionListPaths.Count > 0)
                builder.AppendLine(BuildUnsupportedActionListMessage());
            if (MissingSupportedActionLists.Count > 0)
                builder.AppendLine($"Missing supported lists: {string.Join(", ", MissingSupportedActionLists)}");

            return builder.ToString();
        }
    }
}
