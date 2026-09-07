#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum ActionAuthoringValidationCode
{
    MissingTimeline,
    NullAnimationSegment,
    NullGameplayLane,
    NullGameplayItem,
    MissingReference,
    InvalidTiming,
    InvalidSourceRange,
    InvalidPlayRate,
    AnimationOverlap,
    InvalidConfig,
    MissingEditorId,
    MalformedEditorId,
    DuplicateEditorId,
    MissingOrInvalidRootMotionData,
}

public sealed class ActionAuthoringValidationIssue
{
    public ActionAuthoringValidationCode Code { get; }
    public string EditorId { get; }
    public string AuthoringPath { get; }
    public string Message { get; }

    internal ActionAuthoringValidationIssue(ActionAuthoringValidationCode code, string editorId, string authoringPath, string message)
    {
        Code = code;
        EditorId = editorId;
        AuthoringPath = authoringPath;
        Message = message;
    }
}

public sealed class ActionAuthoringValidationResult
{
    private readonly List<ActionAuthoringValidationIssue> _issues = new List<ActionAuthoringValidationIssue>();
    public IReadOnlyList<ActionAuthoringValidationIssue> Issues => _issues;
    public bool IsValid => _issues.Count == 0;

    internal void Add(ActionAuthoringValidationCode code, string editorId, string authoringPath, string message)
    {
        _issues.Add(new ActionAuthoringValidationIssue(code, editorId, authoringPath, message));
    }
}

/// <summary>Validates V1 authoring data only; it has no playback side effects.</summary>
public static class ActionAuthoringValidator
{
    private const float DirectionEpsilonSqr = 0.000001f;
    private const float QuaternionEpsilonSqr = 0.000001f;

    public static ActionAuthoringValidationResult Validate(ActionAsset actionAsset)
    {
        var result = new ActionAuthoringValidationResult();
        ActionTimelineData timeline = actionAsset != null ? actionAsset.Timeline : null;
        if (timeline == null)
        {
            result.Add(ActionAuthoringValidationCode.MissingTimeline, null, string.Empty, "Action V1 Timeline is missing.");
            return result;
        }

        ValidateIdentity(timeline, result);
        ValidateAnimationSegments(timeline.AnimationSegments, result);
        ValidateGameplayLanes(timeline.GameplayLanes, result);
        return result;
    }

    private static void ValidateAnimationSegments(
        IReadOnlyList<AnimationSegment> segments,
        ActionAuthoringValidationResult result)
    {
        var validRanges = new List<(AnimationSegment segment, string path)>();
        for (int i = 0; segments != null && i < segments.Count; i++)
        {
            string path = $"AnimationSegments[{i}]";
            AnimationSegment segment = segments[i];
            if (segment == null)
            {
                result.Add(ActionAuthoringValidationCode.NullAnimationSegment, null, path, $"AnimationSegment {i} is null.");
                continue;
            }

            bool valid = true;
            if (segment.StartFrame < 0)
            {
                result.Add(ActionAuthoringValidationCode.InvalidTiming, segment.EditorId, path, "AnimationSegment StartFrame must be non-negative.");
                valid = false;
            }
            if (segment.AnimationAsset == null || segment.AnimationAsset.Clip == null)
            {
                result.Add(ActionAuthoringValidationCode.MissingReference, segment.EditorId, path, "AnimationSegment requires AnimationAsset and AnimationClip.");
                valid = false;
            }
            else if (!ActionAuthoringMath.IsFinite(segment.AnimationAsset.Clip.length)
                || segment.AnimationAsset.Clip.length <= 0f)
            {
                result.Add(ActionAuthoringValidationCode.InvalidSourceRange, segment.EditorId, path, "AnimationSegment AnimationClip duration must be finite and greater than zero.");
                valid = false;
            }
            if (!ActionAuthoringMath.IsFinite(segment.SourceStartTime)
                || !ActionAuthoringMath.IsFinite(segment.SourceEndTime)
                || segment.SourceStartTime < 0f
                || segment.SourceEndTime <= segment.SourceStartTime
                || (segment.AnimationAsset != null
                    && segment.AnimationAsset.Clip != null
                    && segment.SourceEndTime > segment.AnimationAsset.Clip.length + 1e-5f))
            {
                result.Add(ActionAuthoringValidationCode.InvalidSourceRange, segment.EditorId, path, "AnimationSegment SourceRange must be finite, increasing, and inside the AnimationClip.");
                valid = false;
            }
            if (!ActionAuthoringMath.IsFinite(segment.PlayRate) || segment.PlayRate <= 0f)
            {
                result.Add(ActionAuthoringValidationCode.InvalidPlayRate, segment.EditorId, path, "AnimationSegment PlayRate must be finite and greater than zero.");
                valid = false;
            }
            if (segment.DerivedDurationFrames <= 0)
            {
                result.Add(ActionAuthoringValidationCode.InvalidTiming, segment.EditorId, path, "AnimationSegment derived duration must be greater than zero.");
                valid = false;
            }
            if (valid)
                validRanges.Add((segment, path));
        }

        validRanges.Sort((left, right) => left.segment.StartFrame.CompareTo(right.segment.StartFrame));
        for (int i = 1; i < validRanges.Count; i++)
        {
            AnimationSegment previous = validRanges[i - 1].segment;
            AnimationSegment current = validRanges[i].segment;
            if (current.StartFrame < previous.EndFrameExclusiveLong)
            {
                result.Add(ActionAuthoringValidationCode.AnimationOverlap, current.EditorId, validRanges[i].path, "AnimationSegments cannot overlap in V1.");
            }
        }
    }

    private static void ValidateGameplayLanes(
        IReadOnlyList<GameplayLane> lanes,
        ActionAuthoringValidationResult result)
    {
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            string lanePath = $"GameplayLanes[{laneIndex}]";
            GameplayLane lane = lanes[laneIndex];
            if (lane == null)
            {
                result.Add(ActionAuthoringValidationCode.NullGameplayLane, null, lanePath, $"GameplayLane {laneIndex} is null.");
                continue;
            }

            IReadOnlyList<GameplayItem> items = lane.Items;
            for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
            {
                string itemPath = $"{lanePath}.Items[{itemIndex}]";
                GameplayItem item = items[itemIndex];
                if (item == null)
                {
                    result.Add(ActionAuthoringValidationCode.NullGameplayItem, null, itemPath, $"GameplayLane {laneIndex} Item {itemIndex} is null.");
                    continue;
                }

                if (item is PointGameplayItem point && point.Frame < 0)
                    result.Add(ActionAuthoringValidationCode.InvalidTiming, item.EditorId, itemPath, "Point Item Frame must be non-negative.");
                if (item is RangeGameplayItem range && (range.StartFrame < 0 || range.DurationFrames <= 0))
                    result.Add(ActionAuthoringValidationCode.InvalidTiming, item.EditorId, itemPath, "Range Item StartFrame must be non-negative and DurationFrames must be greater than zero.");

                ValidateItemConfig(item, itemPath, result);
            }
        }
    }

    private static void ValidateItemConfig(GameplayItem item, string authoringPath, ActionAuthoringValidationResult result)
    {
        switch (item)
        {
            case ImpulseItem impulse:
                ValidateImpulse(impulse.Config, item.EditorId, authoringPath, result);
                break;
            case HitBoxItem hitBox:
                ValidateHitBox(hitBox.Config, item.EditorId, authoringPath, result);
                break;
            case RootMotionItem rootMotion:
                ValidateRootMotion(rootMotion.Config, rootMotion.DurationFrames, item.EditorId, authoringPath, result);
                break;
            case SelfRotationItem selfRotation:
                ValidateSelfRotation(selfRotation.Config, selfRotation.DurationFrames, item.EditorId, authoringPath, result);
                break;
            case VelocityOverrideItem velocity:
                ValidateVelocity(velocity.Config, item.EditorId, authoringPath, result);
                break;
            case MotionPolicyItem motionPolicy:
                ValidateMotionPolicy(motionPolicy.Config, item.EditorId, authoringPath, result);
                break;
            case TagItem tag:
                if (tag.Config == null || tag.Config.tag == null || tag.Config.tag.GetTag() == null)
                    InvalidConfig(result, item.EditorId, authoringPath, "TagItem requires a TagReference.");
                if (tag.Config != null && !IsDefined(tag.Config.targetContainer))
                    InvalidConfig(result, item.EditorId, authoringPath, "TagItem target container is invalid.");
                break;
            default:
                InvalidConfig(result, item.EditorId, authoringPath, $"Unsupported GameplayItem type '{item.GetType().Name}'.");
                break;
        }
    }

    private static void ValidateImpulse(ImpulseItemConfig config, string editorId, string authoringPath, ActionAuthoringValidationResult result)
    {
        if (config == null || config.impulse == null)
        {
            InvalidConfig(result, editorId, authoringPath, "ImpulseItem requires ImpulseItemConfig and ImpulseConfig.");
            return;
        }
        if (!config.useHorizontalImpulse && !config.useVerticalBallistic)
            InvalidConfig(result, editorId, authoringPath, "ImpulseItem requires at least one contribution.");
        if (!IsDefined(config.impulse.directionMode))
            InvalidConfig(result, editorId, authoringPath, "ImpulseItem direction mode is invalid.");
        if (!IsDefined(config.verticalOperation))
            InvalidConfig(result, editorId, authoringPath, "ImpulseItem vertical operation is invalid.");
        if (config.overrideGravityScale)
            InvalidConfig(result, editorId, authoringPath, "V1 ImpulseItem cannot override gravity; use MotionPolicyItem.");
        if (!ActionAuthoringMath.IsFinite(config.impulse.horizontalForce)
            || !ActionAuthoringMath.IsFinite(config.impulse.verticalForce)
            || (config.overrideGravityScale && (!ActionAuthoringMath.IsFinite(config.gravityScale) || config.gravityScale < 0f)))
            InvalidConfig(result, editorId, authoringPath, "ImpulseItem values must be finite and gravity scale non-negative.");
        if (config.useHorizontalImpulse
            && config.impulse.directionMode == ImpulseDirectionMode.LocalHorizontal
            && (!ActionAuthoringMath.IsFinite(config.impulse.localHorizontalDirection)
                || new Vector2(config.impulse.localHorizontalDirection.x, config.impulse.localHorizontalDirection.z).sqrMagnitude <= DirectionEpsilonSqr))
            InvalidConfig(result, editorId, authoringPath, "ImpulseItem local horizontal direction is invalid.");
    }

    private static void ValidateHitBox(HitBoxItemConfig config, string editorId, string authoringPath, ActionAuthoringValidationResult result)
    {
        if (config == null || config.hitboxConfig == null || config.dataConfig == null)
        {
            InvalidConfig(result, editorId, authoringPath, "HitBoxItem requires hitbox and attack data config.");
            return;
        }
        if ((config.boneReference.mode == BoneReference.Mode.Path || config.boneReference.mode == BoneReference.Mode.ActorPath)
            && string.IsNullOrWhiteSpace(config.boneReference.bonePath))
            InvalidConfig(result, editorId, authoringPath, "HitBoxItem path BoneReference requires a bone path.");
        if (!IsDefined(config.boneReference.mode)
            || (config.boneReference.mode == BoneReference.Mode.HumanBone
                && (!IsDefined(config.boneReference.humanBone)
                    || config.boneReference.humanBone == HumanBodyBones.LastBone)))
            InvalidConfig(result, editorId, authoringPath, "HitBoxItem BoneReference is invalid.");
        if (!ActionAuthoringMath.IsFinite(config.hitboxConfig.center)
            || !IsFinite(config.hitboxConfig.rotation)
            || QuaternionSqrMagnitude(config.hitboxConfig.rotation) <= QuaternionEpsilonSqr
            || !ActionAuthoringMath.IsFinite(config.hitboxConfig.height)
            || config.hitboxConfig.height < 0f
            || !ActionAuthoringMath.IsFinite(config.hitboxConfig.radius)
            || config.hitboxConfig.radius <= 0f)
            InvalidConfig(result, editorId, authoringPath, "HitBoxItem shape values must be finite, with non-negative height, positive radius, and a valid rotation.");
        if (config.dataConfig.targetLayers.value == 0
            || !ActionAuthoringMath.IsFinite(config.dataConfig._baseDamage)
            || config.dataConfig._baseDamage < 0f)
            InvalidConfig(result, editorId, authoringPath, "HitBoxItem attack data requires a non-empty target layer mask and finite non-negative damage.");
        if (config.effects == null)
            InvalidConfig(result, editorId, authoringPath, "HitBoxItem effects list is missing.");
        else
        {
            for (int i = 0; i < config.effects.Count; i++)
            {
                if (config.effects[i] == null)
                    InvalidConfig(result, editorId, authoringPath, $"HitBoxItem effect {i} is null.");
            }
        }
    }

    private static void ValidateRootMotion(RootMotionItemConfig config, int durationFrames, string editorId, string authoringPath, ActionAuthoringValidationResult result)
    {
        if (config == null || config.animationAsset == null)
        {
            InvalidConfig(result, editorId, authoringPath, "RootMotionItem requires AnimationAsset.");
            return;
        }
        ValidateRootMotionSampling(config.animationAsset, config.sourceStartTime, config.playRate, durationFrames, editorId, authoringPath, result);
    }

    private static void ValidateSelfRotation(SelfRotationItemConfig config, int durationFrames, string editorId, string authoringPath, ActionAuthoringValidationResult result)
    {
        if (config == null)
        {
            InvalidConfig(result, editorId, authoringPath, "SelfRotationItem requires config.");
            return;
        }
        if (!IsDefined(config.source)
            || !IsDefined(config.mode)
            || (config.source == ActionSelfRotationSource.Target && !IsDefined(config.targetSource))
            || (config.source == ActionSelfRotationSource.Direction && !IsDefined(config.directionSource)))
            InvalidConfig(result, editorId, authoringPath, "SelfRotationItem contains an invalid source or mode combination.");
        if (!ActionAuthoringMath.IsFinite(config.angularSpeedDegrees) || config.angularSpeedDegrees < 0f)
            InvalidConfig(result, editorId, authoringPath, "SelfRotationItem angular speed must be finite and non-negative.");
        if (config.source == ActionSelfRotationSource.Direction
            && config.directionSource == ActionSelfRotationDirectionSource.PresetLocal
            && (!ActionAuthoringMath.IsFinite(config.presetLocalDirection)
                || new Vector2(config.presetLocalDirection.x, config.presetLocalDirection.z).sqrMagnitude <= DirectionEpsilonSqr))
            InvalidConfig(result, editorId, authoringPath, "SelfRotationItem preset direction is invalid.");
        if (config.source == ActionSelfRotationSource.RootMotion)
        {
            if (config.animationAsset == null)
                InvalidConfig(result, editorId, authoringPath, "RootMotion SelfRotationItem requires AnimationAsset.");
            else
                ValidateRootMotionSampling(config.animationAsset, config.sourceStartTime, config.playRate, durationFrames, editorId, authoringPath, result);
        }
    }

    private static void ValidateRootMotionSampling(
        AnimationAsset animationAsset,
        float sourceStartTime,
        float playRate,
        int durationFrames,
        string editorId,
        string authoringPath,
        ActionAuthoringValidationResult result)
    {
        if (!ActionAuthoringMath.IsFinite(sourceStartTime) || sourceStartTime < 0f
            || !ActionAuthoringMath.IsFinite(playRate) || playRate <= 0f)
            InvalidConfig(result, editorId, authoringPath, "Root Motion source offset must be non-negative and PlayRate must be finite and greater than zero.");

        float sourceEndTime = sourceStartTime + Math.Max(0, durationFrames) / (float)ActionTimelineData.FrameRate * playRate;
        if (!ActionAuthoringMath.IsFinite(sourceEndTime)
            || animationAsset.Clip == null
            || sourceStartTime > animationAsset.Clip.length + 1e-5f
            || sourceEndTime > animationAsset.Clip.length + 1e-5f)
            InvalidConfig(result, editorId, authoringPath, "Root Motion source window must be inside its AnimationClip.");

        AnimationAssetBakeStatus status = AnimationAssetBakeWorkflow.GetStatus(animationAsset);
        if (status.Code != AnimationAssetBakeStatusCode.Ready)
            result.Add(ActionAuthoringValidationCode.MissingOrInvalidRootMotionData, editorId, authoringPath, status.Message);
    }

    private static void ValidateVelocity(VelocityOverrideItemConfig config, string editorId, string authoringPath, ActionAuthoringValidationResult result)
    {
        VelocityConfig velocity = config != null ? config.velocity : null;
        if (velocity == null || (!velocity.useHorizontalVelocity && !velocity.useVerticalVelocity))
        {
            InvalidConfig(result, editorId, authoringPath, "VelocityOverrideItem requires at least one velocity axis.");
            return;
        }
        if (velocity.useHorizontalVelocity && !IsDefined(velocity.directionMode))
            InvalidConfig(result, editorId, authoringPath, "VelocityOverrideItem direction mode is invalid.");
        if (!ActionAuthoringMath.IsFinite(velocity.horizontalSpeed) || !ActionAuthoringMath.IsFinite(velocity.verticalSpeed))
            InvalidConfig(result, editorId, authoringPath, "VelocityOverrideItem speeds must be finite.");
        if (velocity.useHorizontalVelocity
            && velocity.directionMode == MotionDirectionMode.LocalHorizontal
            && (!ActionAuthoringMath.IsFinite(velocity.localHorizontalDirection)
                || new Vector2(velocity.localHorizontalDirection.x, velocity.localHorizontalDirection.z).sqrMagnitude <= DirectionEpsilonSqr))
            InvalidConfig(result, editorId, authoringPath, "VelocityOverrideItem local horizontal direction is invalid.");
    }

    private static void ValidateMotionPolicy(MotionPolicyItemConfig config, string editorId, string authoringPath, ActionAuthoringValidationResult result)
    {
        if (config == null || (!config.useLocomotionScale && !config.useAirLocomotionScale && !config.useGravityScale))
        {
            InvalidConfig(result, editorId, authoringPath, "MotionPolicyItem requires at least one policy.");
            return;
        }
        if ((config.useLocomotionScale && (!ActionAuthoringMath.IsFinite(config.locomotionScale) || config.locomotionScale < 0f || config.locomotionScale > 1f))
            || (config.useAirLocomotionScale && (!ActionAuthoringMath.IsFinite(config.airLocomotionScale) || config.airLocomotionScale < 0f || config.airLocomotionScale > 1f))
            || (config.useGravityScale && (!ActionAuthoringMath.IsFinite(config.gravityScale) || config.gravityScale < 0f)))
            InvalidConfig(result, editorId, authoringPath, "MotionPolicyItem values are invalid.");
    }

    private static void InvalidConfig(ActionAuthoringValidationResult result, string editorId, string authoringPath, string message)
    {
        result.Add(ActionAuthoringValidationCode.InvalidConfig, editorId, authoringPath, message);
    }

    private static bool IsDefined<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return Enum.IsDefined(typeof(TEnum), value);
    }

    private static bool IsFinite(Quaternion value)
    {
        return ActionAuthoringMath.IsFinite(value.x)
            && ActionAuthoringMath.IsFinite(value.y)
            && ActionAuthoringMath.IsFinite(value.z)
            && ActionAuthoringMath.IsFinite(value.w);
    }

    private static float QuaternionSqrMagnitude(Quaternion value)
    {
        return value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
    }

    private static void ValidateIdentity(ActionTimelineData timeline, ActionAuthoringValidationResult result)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (ActionAuthoringIdentityTarget target in ActionAuthoringIdentity.Collect(timeline))
        {
            string id = target.Id;
            if (string.IsNullOrEmpty(id))
                result.Add(ActionAuthoringValidationCode.MissingEditorId, id, target.AuthoringPath, $"{target.Label} EditorId is missing.");
            else if (!ActionAuthoringIdentity.IsValidEditorId(id))
                result.Add(ActionAuthoringValidationCode.MalformedEditorId, id, target.AuthoringPath, $"{target.Label} EditorId is malformed.");
            else if (!used.Add(id))
                result.Add(ActionAuthoringValidationCode.DuplicateEditorId, id, target.AuthoringPath, $"{target.Label} EditorId is duplicated.");
        }
    }
}

public readonly struct ActionAuthoringIdentityTarget
{
    private readonly Action<string> _set;
    public string Id { get; }
    public string Label { get; }
    public string AuthoringPath { get; }

    public ActionAuthoringIdentityTarget(string id, string label, Action<string> set)
        : this(id, label, string.Empty, set)
    {
    }

    public ActionAuthoringIdentityTarget(string id, string label, string authoringPath, Action<string> set)
    {
        Id = id;
        Label = label;
        AuthoringPath = authoringPath;
        _set = set;
    }

    public void Set(string value) => _set(value);
}

public static class ActionAuthoringIdentity
{
    public static bool IsValidEditorId(string editorId)
    {
        return editorId != null
            && editorId.Length == 32
            && Guid.TryParseExact(editorId, "N", out _);
    }

    public static int RepairInvalidIds(ActionAsset actionAsset)
    {
        if (actionAsset == null || actionAsset.Timeline == null)
            return 0;

        List<ActionAuthoringIdentityTarget> targets = Collect(actionAsset.Timeline);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var repair = new List<ActionAuthoringIdentityTarget>();
        for (int i = 0; i < targets.Count; i++)
        {
            ActionAuthoringIdentityTarget target = targets[i];
            if (!IsValidEditorId(target.Id) || !used.Add(target.Id))
                repair.Add(target);
        }
        if (repair.Count == 0)
            return 0;

        var replacements = new string[repair.Count];
        for (int i = 0; i < repair.Count; i++)
        {
            string id;
            do { id = Guid.NewGuid().ToString("N"); }
            while (!used.Add(id));
            replacements[i] = id;
        }

        Undo.RecordObject(actionAsset, "Repair Action V1 Editor IDs");
        for (int i = 0; i < repair.Count; i++)
            repair[i].Set(replacements[i]);
        EditorUtility.SetDirty(actionAsset);
        return repair.Count;
    }

    internal static List<ActionAuthoringIdentityTarget> Collect(ActionTimelineData timeline)
    {
        var targets = new List<ActionAuthoringIdentityTarget>();
        if (timeline == null)
            return targets;

        IReadOnlyList<AnimationSegment> segments = timeline.AnimationSegments;
        for (int i = 0; segments != null && i < segments.Count; i++)
        {
            AnimationSegment segment = segments[i];
            if (segment != null)
                targets.Add(new ActionAuthoringIdentityTarget(segment.EditorId, $"AnimationSegment {i}", $"AnimationSegments[{i}]", segment.EditorSetEditorId));
        }

        IReadOnlyList<GameplayLane> lanes = timeline.GameplayLanes;
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            GameplayLane lane = lanes[laneIndex];
            if (lane == null)
                continue;
            string lanePath = $"GameplayLanes[{laneIndex}]";
            targets.Add(new ActionAuthoringIdentityTarget(lane.EditorId, $"GameplayLane {laneIndex}", lanePath, lane.EditorSetEditorId));
            IReadOnlyList<GameplayItem> items = lane.Items;
            for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
            {
                GameplayItem item = items[itemIndex];
                if (item != null)
                    targets.Add(new ActionAuthoringIdentityTarget(item.EditorId, $"GameplayLane {laneIndex} Item {itemIndex}", $"{lanePath}.Items[{itemIndex}]", item.EditorSetEditorId));
            }
        }
        return targets;
    }
}
#endif
