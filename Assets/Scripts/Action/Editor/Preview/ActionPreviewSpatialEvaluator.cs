#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministically accumulates RootMotion translation for Preview. SelfRotation remains a runtime
/// concern because Preview deliberately does not simulate runtime rotation behavior.
/// </summary>
internal sealed class ActionPreviewSpatialEvaluator
{
    private const int MaximumCachedFrames = 100000;

    private readonly List<Vector3> _boundaries = new List<Vector3>();
    private readonly Dictionary<RootMotionItem, MotionOwner> _translationOwners = new Dictionary<RootMotionItem, MotionOwner>();

    private ActionAsset _action;
    private TranslationDomain _translation;

    internal IReadOnlyList<Vector3> Boundaries => _boundaries;
    internal string Diagnostic { get; private set; }

    internal void Invalidate()
    {
        _action = null;
        _boundaries.Clear();
    }

    internal Vector3 Evaluate(ActionAsset action, double position)
    {
        EnsureContext(action);
        if (_boundaries.Count == 0)
            return Vector3.zero;

        // Frame N shows the world position seen by Runtime's Hit stage, after tick N's motion.
        // Fractional preview positions interpolate visually between adjacent post-tick positions.
        double duration = action?.Timeline?.DurationFrames ?? 0;
        double clamped = Math.Max(0d, Math.Min(duration, position + 1d));
        int lower = Math.Max(0, Math.Min(MaximumCachedFrames, (int)Math.Floor(clamped)));
        int upper = Math.Max(lower, Math.Min(MaximumCachedFrames, (int)Math.Ceiling(clamped)));
        EnsureBoundary(upper);
        if (lower >= _boundaries.Count)
            return _boundaries[_boundaries.Count - 1];
        if (upper >= _boundaries.Count || upper == lower)
            return _boundaries[lower];

        float fraction = (float)(clamped - lower);
        return Vector3.LerpUnclamped(_boundaries[lower], _boundaries[upper], fraction);
    }

    private void EnsureContext(ActionAsset action)
    {
        if (ReferenceEquals(_action, action) && _boundaries.Count > 0)
            return;

        _action = action;
        _translation = new TranslationDomain();
        _translationOwners.Clear();
        _boundaries.Clear();
        _boundaries.Add(Vector3.zero);
        Diagnostic = string.Empty;
    }

    private void EnsureBoundary(int boundary)
    {
        if (_action == null || _action.Timeline == null)
            return;
        if (boundary >= MaximumCachedFrames)
            Diagnostic = $"Preview motion accumulation is limited to {MaximumCachedFrames} Frames.";
        while (_boundaries.Count <= boundary && _boundaries.Count <= MaximumCachedFrames)
            StepFrame(_boundaries.Count - 1);
    }

    private void StepFrame(int frame)
    {
        Vector3 start = _boundaries[_boundaries.Count - 1];
        IReadOnlyList<GameplayLane> lanes = _action.Timeline.GameplayLanes;

        VisitActiveRanges(lanes, frame, enterOnly: true, (range, localFrame) => Enter(range));
        VisitActiveRanges(lanes, frame, enterOnly: false, Submit);

        _translation.BeginMotionTick();
        Vector3 nextPosition = start;
        if (_translation.HasTrajectoryRootMotionTick)
            nextPosition += _translation.TrajectoryRootMotionLocalPosition;

        VisitActiveRanges(lanes, frame, enterOnly: false, (range, localFrame) =>
        {
            if (range.EndFrameExclusiveLong == (long)frame + 1L)
                Exit(range);
        });
        _boundaries.Add(nextPosition);
    }

    private static void VisitActiveRanges(
        IReadOnlyList<GameplayLane> lanes,
        int frame,
        bool enterOnly,
        Action<RangeGameplayItem, int> visitor)
    {
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            GameplayLane lane = lanes[laneIndex];
            if (lane == null || lane.Muted)
                continue;
            IReadOnlyList<GameplayItem> items = lane.Items;
            for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
            {
                if (!(items[itemIndex] is RangeGameplayItem range) || range.Muted)
                    continue;
                if (enterOnly)
                {
                    if (range.StartFrame == frame)
                        visitor(range, 0);
                }
                else if (range.StartFrame <= frame && frame < range.EndFrameExclusiveLong)
                {
                    visitor(range, frame - range.StartFrame);
                }
            }
        }
    }

    private void Enter(RangeGameplayItem range)
    {
        if (range is RootMotionItem root)
            _translationOwners[root] = _translation.BeginTrajectoryRootMotion();
    }

    private void Submit(RangeGameplayItem range, int localFrame)
    {
        if (range is RootMotionItem root && _translationOwners.TryGetValue(root, out MotionOwner translationOwner))
        {
            RootMotionItemConfig config = root.Config;
            RootMotionTrajectory trajectory = config?.animationAsset != null ? config.animationAsset.RootMotionData : null;
            ActionItemRuntimeUtility.GetSourceWindow(config?.sourceStartTime ?? 0f, config?.playRate ?? 1f,
                localFrame, out float sourceStart, out float sourceEnd);
            if (trajectory != null && trajectory.TryExtract(sourceStart, sourceEnd, out RootMotionTransform delta))
            {
                Vector3 local = delta.Position;
                local.y = 0f;
                _translation.SubmitTrajectoryRootMotion(translationOwner, local);
            }
        }
    }

    private void Exit(RangeGameplayItem range)
    {
        if (range is RootMotionItem root && _translationOwners.TryGetValue(root, out MotionOwner translationOwner))
        {
            _translation.EndTrajectoryRootMotion(translationOwner);
            _translationOwners.Remove(root);
        }
    }
}
#endif
