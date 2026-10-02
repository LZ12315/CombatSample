#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal enum ActionTimelineOperationKind
{
    Move,
    ResizeLeft,
    ResizeRight,
    TrimLeft,
    TrimRight,
    SetPointTiming,
    SetRangeTiming,
    SetAnimationTiming,
}

internal enum ActionTimelineOperationState
{
    Allowed,
    Rejected,
    NoChange,
}

internal readonly struct ActionNavigationRange
{
    internal readonly double Start;
    internal readonly double Span;

    internal ActionNavigationRange(double start, double span)
    {
        Start = start;
        Span = span;
    }
}

internal readonly struct ActionTimelineSnapResult
{
    internal readonly int Delta;
    internal readonly int TargetFrame;
    internal readonly int MovingEdgeIndex;

    internal ActionTimelineSnapResult(int delta, int targetFrame, int movingEdgeIndex)
    {
        Delta = delta;
        TargetFrame = targetFrame;
        MovingEdgeIndex = movingEdgeIndex;
    }
}

/// <summary>Deterministic interaction math shared by production presentation and diagnostic assertions.</summary>
internal static class ActionTimelineInteractionMath
{
    internal const float DragThresholdPixels = 5f;
    internal const float SnapDistancePixels = 10f;
    internal const float SnapReleaseDistancePixels = 16f;
    internal const float AutoPanMaximumDistance = 100f;

    internal static ActionNavigationRange Pan(double domain, double start, double span, double delta)
    {
        double safeDomain = Math.Max(1d, domain);
        double safeSpan = Math.Max(0d, Math.Min(span, safeDomain));
        return new ActionNavigationRange(Math.Max(0d, Math.Min(safeDomain - safeSpan, start + delta)), safeSpan);
    }

    internal static ActionNavigationRange ResizeLeft(double domain, double start, double span, double minimumSpan, double delta)
    {
        double safeDomain = Math.Max(1d, domain);
        double minimum = Math.Max(0.0001d, Math.Min(minimumSpan, safeDomain));
        double fixedEnd = Math.Max(minimum, Math.Min(safeDomain, start + span));
        double nextStart = Math.Max(0d, Math.Min(fixedEnd - minimum, start + delta));
        return new ActionNavigationRange(nextStart, fixedEnd - nextStart);
    }

    internal static ActionNavigationRange ResizeRight(double domain, double start, double span, double minimumSpan, double delta)
    {
        double safeDomain = Math.Max(1d, domain);
        double safeStart = Math.Max(0d, Math.Min(safeDomain, start));
        double minimum = Math.Max(0.0001d, Math.Min(minimumSpan, safeDomain - safeStart));
        double end = Math.Max(safeStart + minimum, Math.Min(safeDomain, start + span + delta));
        return new ActionNavigationRange(safeStart, end - safeStart);
    }

    internal static List<List<int>> BoundedGroups(IReadOnlyList<double> sortedPositions, double maximumSpan)
    {
        var groups = new List<List<int>>();
        if (sortedPositions == null || sortedPositions.Count == 0)
            return groups;
        List<int> group = null;
        double first = 0d;
        for (int index = 0; index < sortedPositions.Count; index++)
        {
            double position = sortedPositions[index];
            if (group == null || position - first >= maximumSpan)
            {
                group = new List<int>();
                groups.Add(group);
                first = position;
            }
            group.Add(index);
        }
        return groups;
    }

    internal static bool CanPlaceMarker(Rect candidate, IReadOnlyList<Rect> occupied) =>
        occupied == null || !occupied.Any(rect => RectanglesOverlap(
            candidate.xMin, candidate.yMin, candidate.xMax, candidate.yMax,
            rect.xMin, rect.yMin, rect.xMax, rect.yMax));

    internal static bool RectanglesOverlap(float leftA, float topA, float rightA, float bottomA,
        float leftB, float topB, float rightB, float bottomB) =>
        leftA <= rightB && rightA >= leftB && topA <= bottomB && bottomA >= topB;

    internal static int ChooseHitIndex(IReadOnlyList<bool> visualHits, IReadOnlyList<bool> expandedHits,
        IReadOnlyList<int> layerRanks, IReadOnlyList<int> authoringOrder)
    {
        if (visualHits == null || expandedHits == null || layerRanks == null || authoringOrder == null)
            return -1;
        int count = Math.Min(Math.Min(visualHits.Count, expandedHits.Count),
            Math.Min(layerRanks.Count, authoringOrder.Count));
        bool hasVisual = Enumerable.Range(0, count).Any(index => visualHits[index]);
        int best = -1;
        for (int index = 0; index < count; index++)
        {
            if (hasVisual ? !visualHits[index] : !expandedHits[index])
                continue;
            if (best < 0 || layerRanks[index] > layerRanks[best] ||
                layerRanks[index] == layerRanks[best] && authoringOrder[index] > authoringOrder[best])
                best = index;
        }
        return best;
    }

    internal static bool PassedDragThreshold(Vector2 start, Vector2 current) =>
        Mathf.Abs(current.x - start.x) >= DragThresholdPixels ||
        Mathf.Abs(current.y - start.y) >= DragThresholdPixels;

    internal static List<int> MovingSnapEdges(IReadOnlyList<ActionDocumentEntry> entries,
        ActionTimelineOperationKind kind, ActionDocumentEntry primary)
    {
        if (entries == null || entries.Count == 0) return new List<int>();
        if (kind == ActionTimelineOperationKind.ResizeLeft || kind == ActionTimelineOperationKind.TrimLeft)
            return new List<int> { entries[0].StartFrame };
        if (kind == ActionTimelineOperationKind.ResizeRight || kind == ActionTimelineOperationKind.TrimRight)
            return new List<int> { SnapEndFrame(entries[0]) };
        primary = entries.Contains(primary) ? primary : entries[0];
        // A Point occupies one Frame for overlap validation, but only its event Frame is a
        // visible snap edge. Multi-selection aligns the grabbed Item or the group's outer edges.
        return new[] { primary.StartFrame, SnapEndFrame(primary),
            entries.Min(entry => entry.StartFrame), entries.Max(SnapEndFrame) }.Distinct().ToList();
    }

    private static int SnapEndFrame(ActionDocumentEntry entry) => entry.Source is PointGameplayItem
        ? entry.StartFrame
        : (int)Math.Min(int.MaxValue, entry.RawEndFrameExclusive);

    internal static bool TrySnap(
        double rawDelta,
        IReadOnlyList<int> movingEdges,
        IReadOnlyList<int> targetFrames,
        double pixelsPerFrame,
        Func<int, bool> acceptsDelta,
        out ActionTimelineSnapResult result,
        ActionTimelineSnapResult? retainedSnap = null,
        int? preferredTargetFrame = null)
    {
        result = default;
        if (movingEdges == null || targetFrames == null || movingEdges.Count == 0 || targetFrames.Count == 0 ||
            double.IsNaN(rawDelta) || double.IsInfinity(rawDelta) ||
            double.IsNaN(pixelsPerFrame) || double.IsInfinity(pixelsPerFrame) || pixelsPerFrame <= 0d)
            return false;

        if (retainedSnap.HasValue)
        {
            ActionTimelineSnapResult retained = retainedSnap.Value;
            if (retained.MovingEdgeIndex >= 0 && retained.MovingEdgeIndex < movingEdges.Count &&
                (long)movingEdges[retained.MovingEdgeIndex] + retained.Delta == retained.TargetFrame &&
                targetFrames.Contains(retained.TargetFrame) &&
                Math.Abs(retained.Delta - rawDelta) * pixelsPerFrame <= SnapReleaseDistancePixels &&
                (acceptsDelta == null || acceptsDelta(retained.Delta)))
            {
                result = retained;
                return true;
            }
        }

        double bestDistance = double.PositiveInfinity;
        int bestTarget = int.MaxValue;
        int bestEdge = int.MaxValue;
        int bestDelta = 0;
        for (int edgeIndex = 0; edgeIndex < movingEdges.Count; edgeIndex++)
        {
            for (int targetIndex = 0; targetIndex < targetFrames.Count; targetIndex++)
            {
                int target = targetFrames[targetIndex];
                long candidateLong = (long)target - movingEdges[edgeIndex];
                if (candidateLong < int.MinValue || candidateLong > int.MaxValue)
                    continue;
                int candidate = (int)candidateLong;
                double distance = Math.Abs(candidate - rawDelta) * pixelsPerFrame;
                if (distance > SnapDistancePixels + 0.0001d ||
                    acceptsDelta != null && !acceptsDelta(candidate))
                    continue;
                bool better = distance < bestDistance - 0.0001d ||
                              Math.Abs(distance - bestDistance) <= 0.0001d &&
                              (target == preferredTargetFrame && bestTarget != preferredTargetFrame ||
                               (target == preferredTargetFrame) == (bestTarget == preferredTargetFrame) &&
                               (edgeIndex < bestEdge || edgeIndex == bestEdge && target < bestTarget));
                if (!better)
                    continue;
                bestDistance = distance;
                bestTarget = target;
                bestEdge = edgeIndex;
                bestDelta = candidate;
            }
        }

        if (double.IsPositiveInfinity(bestDistance))
            return false;
        result = new ActionTimelineSnapResult(bestDelta, bestTarget, bestEdge);
        return true;
    }

    internal static float AutoPanSpeed(float outsideDistance, float maximumPixelsPerSecond)
    {
        if (!ActionAuthoringMath.IsFinite(outsideDistance) ||
            !ActionAuthoringMath.IsFinite(maximumPixelsPerSecond) || maximumPixelsPerSecond <= 0f)
            return 0f;
        float direction = Mathf.Sign(outsideDistance);
        float factor = Mathf.Clamp01(Mathf.Abs(outsideDistance) / AutoPanMaximumDistance);
        return direction * maximumPixelsPerSecond * factor;
    }
}

/// <summary>One Editor-session pointer gesture gate shared by Timeline and Details.</summary>
internal static class ActionEditorInteractionGate
{
    private static object _owner;
    internal static event Action Changed;

    internal static bool IsActive => _owner != null;

    internal static bool TryAcquire(object owner)
    {
        if (owner == null || _owner != null)
            return false;
        _owner = owner;
        Changed?.Invoke();
        return true;
    }

    internal static void Release(object owner)
    {
        if (!ReferenceEquals(_owner, owner))
            return;
        _owner = null;
        Changed?.Invoke();
    }
}

internal sealed class ActionTimelineOperationCandidate
{
    internal object Source;
    internal int StartFrame;
    internal int DurationFrames;
    internal int LaneIndex;
    internal AnimationAsset AnimationAsset;
    internal AnimationClip Clip;
    internal float ClipLength;
    internal float SourceStartTime;
    internal float SourceEndTime;
    internal float PlayRate;
}

internal sealed class ActionTimelineOperationResult
{
    internal ActionTimelineOperationState State;
    internal string Message = string.Empty;
    internal readonly List<ActionTimelineOperationCandidate> Candidates = new List<ActionTimelineOperationCandidate>();

    internal ActionTimelineOperationCandidate ForSource(object source) =>
        Candidates.FirstOrDefault(candidate => ReferenceEquals(candidate.Source, source));
}

internal readonly struct ActionTimelineOperationInput
{
    private ActionTimelineOperationInput(bool absolute, int frame, int lane, int duration,
        AnimationAsset animationAsset, float sourceStartTime, float sourceEndTime, float playRate)
    {
        IsAbsolute = absolute;
        Frame = frame;
        Lane = lane;
        Duration = duration;
        AnimationAsset = animationAsset;
        SourceStartTime = sourceStartTime;
        SourceEndTime = sourceEndTime;
        PlayRate = playRate;
    }

    internal bool IsAbsolute { get; }
    internal int Frame { get; }
    internal int Lane { get; }
    internal int Duration { get; }
    internal AnimationAsset AnimationAsset { get; }
    internal float SourceStartTime { get; }
    internal float SourceEndTime { get; }
    internal float PlayRate { get; }

    internal static ActionTimelineOperationInput Delta(int frameDelta, int laneDelta = 0) =>
        new ActionTimelineOperationInput(false, frameDelta, laneDelta, 0, null, 0f, 0f, 1f);

    internal static ActionTimelineOperationInput Absolute(int startFrame, int durationFrames,
        AnimationAsset animationAsset = null, float sourceStartTime = 0f, float sourceEndTime = 0f,
        float playRate = 1f) =>
        new ActionTimelineOperationInput(true, startFrame, 0, durationFrames, animationAsset,
            sourceStartTime, sourceEndTime, playRate);
}

/// <summary>
/// Frozen source state for one Timeline gesture. Evaluation and commit both consume this object so
/// mouse feedback cannot be generated from different authoring data than the final Undo operation.
/// </summary>
internal sealed class ActionTimelineOperationSnapshot
{
    private sealed class Entry
    {
        internal object Source;
        internal string EditorId;
        internal int StartFrame;
        internal int DurationFrames;
        internal int LaneIndex;
        internal AnimationAsset AnimationAsset;
        internal AnimationClip Clip;
        internal float ClipLength;
        internal float SourceStartTime;
        internal float SourceEndTime;
        internal float PlayRate;
    }

    private readonly List<Entry> _targets = new List<Entry>();
    private readonly List<Entry> _animations = new List<Entry>();
    private readonly List<AnimationSegment> _animationOrder = new List<AnimationSegment>();
    private readonly List<GameplayLane> _lanes = new List<GameplayLane>();
    private readonly List<List<Entry>> _laneItems = new List<List<Entry>>();

    internal ActionAsset Asset { get; private set; }
    internal ActionTimelineOperationKind Kind { get; private set; }

    internal static ActionTimelineOperationSnapshot Capture(
        ActionEditorDocument document, ActionTimelineOperationKind kind, IReadOnlyList<string> ids)
    {
        if (document?.Asset?.Timeline == null || ids == null)
            return null;
        var snapshot = new ActionTimelineOperationSnapshot
        {
            Asset = document.Asset,
            Kind = kind,
        };
        var requested = new HashSet<string>(ids.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
        snapshot._animationOrder.AddRange(document.Asset.Timeline.AnimationSegments);
        foreach (ActionDocumentEntry documentEntry in document.ContentEntries)
        {
            if (documentEntry.Source == null)
                continue;
            Entry entry = CaptureEntry(documentEntry);
            if (documentEntry.Source is AnimationSegment)
                snapshot._animations.Add(entry);
            if (requested.Contains(documentEntry.EditorId))
                snapshot._targets.Add(entry);
        }
        foreach (ActionDocumentEntry laneEntry in document.Lanes.OrderBy(entry => entry.LaneIndex))
        {
            GameplayLane lane = laneEntry.Source as GameplayLane;
            snapshot._lanes.Add(lane);
            var itemEntries = new List<Entry>();
            foreach (ActionDocumentEntry item in document.GameplayItems.Where(item => item.LaneIndex == laneEntry.LaneIndex))
                itemEntries.Add(CaptureEntry(item));
            snapshot._laneItems.Add(itemEntries);
        }
        return snapshot._targets.Count > 0 ? snapshot : null;
    }

    internal ActionTimelineOperationResult Evaluate(ActionTimelineOperationInput input)
    {
        if (_targets.Count == 0)
            return Rejected("The operation target no longer exists.");
        if (input.IsAbsolute)
            return EvaluateAbsolute(input.Frame, input.Duration, input.AnimationAsset,
                input.SourceStartTime, input.SourceEndTime, input.PlayRate);
        if (input.Frame == 0 && (Kind != ActionTimelineOperationKind.Move || input.Lane == 0))
            return new ActionTimelineOperationResult { State = ActionTimelineOperationState.NoChange };

        switch (Kind)
        {
            case ActionTimelineOperationKind.Move:
                return EvaluateMove(input.Frame, input.Lane);
            case ActionTimelineOperationKind.ResizeLeft:
            case ActionTimelineOperationKind.ResizeRight:
                return EvaluateResize(input.Frame);
            case ActionTimelineOperationKind.TrimLeft:
            case ActionTimelineOperationKind.TrimRight:
                return EvaluateTrim(input.Frame);
            default:
                return Rejected("Unsupported Timeline operation.");
        }
    }

    internal int ConstrainPointerDelta(int requestedDelta, out string reason)
    {
        reason = string.Empty;
        if (Kind == ActionTimelineOperationKind.Move && _targets.Count > 0)
        {
            // Clamp the group as a whole. Authoring validation still rejects overlaps and bad Lanes;
            // pointer overshoot alone must not make dragging to Frame 0 impossible.
            int lower = -_targets.Min(entry => entry.StartFrame);
            int upper = int.MaxValue - _targets.Max(entry => entry.StartFrame);
            int clamped = Math.Max(lower, Math.Min(upper, requestedDelta));
            if (clamped != requestedDelta)
                reason = clamped == lower ? "Reached Frame 0." : "Reached the supported Frame limit.";
            return clamped;
        }
        if (_targets.Count != 1)
            return requestedDelta;
        Entry target = _targets[0];
        int minimum = int.MinValue;
        int maximum = int.MaxValue;

        if ((Kind == ActionTimelineOperationKind.ResizeLeft || Kind == ActionTimelineOperationKind.ResizeRight) &&
            target.Source is RangeGameplayItem)
        {
            if (Kind == ActionTimelineOperationKind.ResizeLeft)
            {
                minimum = Math.Max(-target.StartFrame, PreviousGameplayEnd(target) - target.StartFrame);
                maximum = target.DurationFrames - 1;
            }
            else
            {
                minimum = 1 - target.DurationFrames;
                int nextStart = NextGameplayStart(target);
                if (nextStart != int.MaxValue)
                    maximum = nextStart - target.StartFrame - target.DurationFrames;
            }
        }
        else if ((Kind == ActionTimelineOperationKind.TrimLeft || Kind == ActionTimelineOperationKind.TrimRight) &&
                 target.Source is AnimationSegment && target.PlayRate > 0f)
        {
            double framesPerSourceSecond = ActionTimelineData.FrameRate / (double)target.PlayRate;
            if (Kind == ActionTimelineOperationKind.TrimLeft)
            {
                minimum = Math.Max(-target.StartFrame, PreviousAnimationEnd(target) - target.StartFrame);
                minimum = Math.Max(minimum, SaturatingCeil(-target.SourceStartTime * framesPerSourceSecond - 0.00001d));
                maximum = SaturatingCeil((target.SourceEndTime - target.SourceStartTime) * framesPerSourceSecond) - 1;
            }
            else
            {
                minimum = SaturatingFloor((target.SourceStartTime - target.SourceEndTime) * framesPerSourceSecond) + 1;
                maximum = SaturatingFloor((target.ClipLength - target.SourceEndTime) * framesPerSourceSecond + 0.00001d);
                int nextStart = NextAnimationStart(target);
                if (nextStart != int.MaxValue)
                    maximum = Math.Min(maximum, nextStart - target.StartFrame - target.DurationFrames);
            }
        }
        else
        {
            return requestedDelta;
        }

        if (maximum < minimum)
            return 0;
        int constrained = Math.Max(minimum, Math.Min(maximum, requestedDelta));
        if (constrained != requestedDelta)
            reason = constrained == minimum ? "Reached the start boundary." : "Reached the end boundary.";
        return constrained;
    }

    private int PreviousGameplayEnd(Entry target)
    {
        if (target.LaneIndex < 0 || target.LaneIndex >= _laneItems.Count)
            return 0;
        int result = 0;
        foreach (Entry entry in _laneItems[target.LaneIndex])
            if (!ReferenceEquals(entry.Source, target.Source) && entry.StartFrame + entry.DurationFrames <= target.StartFrame)
                result = Math.Max(result, entry.StartFrame + entry.DurationFrames);
        return result;
    }

    private int NextGameplayStart(Entry target)
    {
        if (target.LaneIndex < 0 || target.LaneIndex >= _laneItems.Count)
            return int.MaxValue;
        int result = int.MaxValue;
        int targetEnd = target.StartFrame + target.DurationFrames;
        foreach (Entry entry in _laneItems[target.LaneIndex])
            if (!ReferenceEquals(entry.Source, target.Source) && entry.StartFrame >= targetEnd)
                result = Math.Min(result, entry.StartFrame);
        return result;
    }

    private int PreviousAnimationEnd(Entry target)
    {
        int result = 0;
        foreach (Entry entry in _animations)
            if (!ReferenceEquals(entry.Source, target.Source) && entry.StartFrame + entry.DurationFrames <= target.StartFrame)
                result = Math.Max(result, entry.StartFrame + entry.DurationFrames);
        return result;
    }

    private int NextAnimationStart(Entry target)
    {
        int result = int.MaxValue;
        int targetEnd = target.StartFrame + target.DurationFrames;
        foreach (Entry entry in _animations)
            if (!ReferenceEquals(entry.Source, target.Source) && entry.StartFrame >= targetEnd)
                result = Math.Min(result, entry.StartFrame);
        return result;
    }

    private static int SaturatingFloor(double value) => value <= int.MinValue
        ? int.MinValue
        : value >= int.MaxValue ? int.MaxValue : (int)Math.Floor(value);

    private static int SaturatingCeil(double value) => value <= int.MinValue
        ? int.MinValue
        : value >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(value);

    /// <summary>Evaluates an absolute Details draft against the same frozen source used by Timeline gestures.</summary>
    internal ActionTimelineOperationResult EvaluateAbsolute(int startFrame, int durationFrames,
        AnimationAsset animationAsset = null, float sourceStartTime = 0f, float sourceEndTime = 0f, float playRate = 1f)
    {
        if (_targets.Count != 1)
            return Rejected("Details timing requires exactly one current target.");

        Entry target = _targets[0];
        if (Kind == ActionTimelineOperationKind.SetPointTiming)
        {
            if (!(target.Source is PointGameplayItem))
                return Rejected("Point timing requires a PointGameplayItem.");
            if (startFrame < 0)
                return Rejected("Point Frame must be at or after Frame 0.");
            if (startFrame == target.StartFrame)
                return new ActionTimelineOperationResult { State = ActionTimelineOperationState.NoChange };
            ActionTimelineOperationResult pointResult = Allowed(Candidate(target, startFrame, 1, target.LaneIndex));
            return ModifiedGameplayItemsOverlap(pointResult.Candidates)
                ? Rejected("Point timing would overlap another GameplayItem on the same Lane.")
                : pointResult;
        }

        if (Kind == ActionTimelineOperationKind.SetRangeTiming)
        {
            if (!(target.Source is RangeGameplayItem))
                return Rejected("Range timing requires a RangeGameplayItem.");
            if (startFrame < 0 || durationFrames < 1)
                return Rejected("Range Start must be non-negative and Duration must be at least one Frame.");
            if (startFrame == target.StartFrame && durationFrames == target.DurationFrames)
                return new ActionTimelineOperationResult { State = ActionTimelineOperationState.NoChange };
            ActionTimelineOperationResult rangeResult = Allowed(Candidate(target, startFrame, durationFrames, target.LaneIndex));
            return ModifiedGameplayItemsOverlap(rangeResult.Candidates)
                ? Rejected("Range timing would overlap another GameplayItem on the same Lane.")
                : rangeResult;
        }

        if (Kind != ActionTimelineOperationKind.SetAnimationTiming || !(target.Source is AnimationSegment))
            return Rejected("Animation timing requires an AnimationSegment.");
        AnimationClip clip = animationAsset != null ? animationAsset.Clip : null;
        float clipLength = clip != null ? clip.length : 0f;
        if (startFrame < 0 || animationAsset == null || clip == null ||
            !ActionAuthoringMath.IsFinite(clipLength) || clipLength <= 0f ||
            !ActionAuthoringMath.IsFinite(sourceStartTime) || !ActionAuthoringMath.IsFinite(sourceEndTime) ||
            !ActionAuthoringMath.IsFinite(playRate) || playRate <= 0f || sourceStartTime < 0f ||
            sourceEndTime <= sourceStartTime || sourceEndTime > clipLength + 0.00001f)
            return Rejected("Animation source must have a valid Clip, increasing in-range Source values, and Play Rate above zero.");
        int derivedDuration = CalculateAnimationDuration(sourceStartTime, sourceEndTime, playRate);
        if (derivedDuration < 1)
            return Rejected("Animation source does not derive a valid Duration.");

        bool unchanged = startFrame == target.StartFrame && animationAsset == target.AnimationAsset &&
                         Mathf.Approximately(sourceStartTime, target.SourceStartTime) &&
                         Mathf.Approximately(sourceEndTime, target.SourceEndTime) &&
                         Mathf.Approximately(playRate, target.PlayRate);
        if (unchanged)
            return new ActionTimelineOperationResult { State = ActionTimelineOperationState.NoChange };

        ActionTimelineOperationCandidate candidate = Candidate(target, startFrame, derivedDuration, target.LaneIndex);
        candidate.AnimationAsset = animationAsset;
        candidate.Clip = clip;
        candidate.ClipLength = clipLength;
        candidate.SourceStartTime = sourceStartTime;
        candidate.SourceEndTime = sourceEndTime;
        candidate.PlayRate = playRate;
        ActionTimelineOperationResult result = Allowed(candidate);
        if (ModifiedAnimationsOverlap(result.Candidates))
            return Rejected("Animation timing would overlap another AnimationSegment.");
        return result;
    }

    internal bool MatchesCurrentSource(out string message)
    {
        message = string.Empty;
        ActionTimelineData timeline = Asset != null ? Asset.Timeline : null;
        if (timeline == null || timeline.AnimationSegments.Count != _animationOrder.Count || timeline.GameplayLanes.Count != _lanes.Count)
        {
            message = "Timeline structure changed during the gesture.";
            return false;
        }
        for (int i = 0; i < _animationOrder.Count; i++)
        {
            if (!ReferenceEquals(timeline.AnimationSegments[i], _animationOrder[i]))
            {
                message = "Animation data changed during the gesture.";
                return false;
            }
        }
        foreach (Entry animation in _animations)
        {
            if (!(animation.Source is AnimationSegment current) || !Matches(animation, current))
            {
                message = "Animation data changed during the gesture.";
                return false;
            }
        }
        for (int laneIndex = 0; laneIndex < _lanes.Count; laneIndex++)
        {
            GameplayLane lane = timeline.GameplayLanes[laneIndex];
            if (!ReferenceEquals(lane, _lanes[laneIndex]))
            {
                message = "Gameplay Lane structure changed during the gesture.";
                return false;
            }
            if (lane == null)
                continue;
            if (lane.Items.Count != _laneItems[laneIndex].Count)
            {
                message = "Gameplay Lane structure changed during the gesture.";
                return false;
            }
            for (int itemIndex = 0; itemIndex < lane.Items.Count; itemIndex++)
            {
                Entry entry = _laneItems[laneIndex][itemIndex];
                if (!ReferenceEquals(lane.Items[itemIndex], entry.Source) || !MatchesGameplay(entry, lane.Items[itemIndex]))
                {
                    message = "Gameplay Item data changed during the gesture.";
                    return false;
                }
            }
        }
        foreach (Entry target in _targets)
        {
            if (!MatchesTarget(target))
            {
                message = "Timeline target data changed during the gesture.";
                return false;
            }
        }
        return true;
    }

    private ActionTimelineOperationResult EvaluateMove(int frameDelta, int laneDelta)
    {
        var result = new ActionTimelineOperationResult { State = ActionTimelineOperationState.Allowed };
        foreach (Entry target in _targets)
        {
            long start = (long)target.StartFrame + frameDelta;
            if (start < 0L || start > int.MaxValue)
                return Rejected("Move would place content outside the supported Frame range.");
            int laneIndex = target.LaneIndex;
            if (laneDelta != 0)
            {
                if (target.Source is AnimationSegment || laneIndex + laneDelta < 0 || laneIndex + laneDelta >= _lanes.Count ||
                    _lanes[laneIndex + laneDelta] == null)
                    return Rejected("Every moved GameplayItem must have a valid destination Lane.");
                laneIndex += laneDelta;
            }
            result.Candidates.Add(Candidate(target, (int)start, target.DurationFrames, laneIndex));
        }
        if (ModifiedAnimationsOverlap(result.Candidates))
            return Rejected("Every moved AnimationSegment must finish without an overlap.");
        if (ModifiedGameplayItemsOverlap(result.Candidates))
            return Rejected("Every moved GameplayItem must finish without an overlap on its destination Lane.");
        return result;
    }

    private ActionTimelineOperationResult EvaluateResize(int frameDelta)
    {
        if (_targets.Count != 1 || !(_targets[0].Source is RangeGameplayItem))
            return Rejected("Range resize requires one RangeGameplayItem.");
        Entry target = _targets[0];
        bool left = Kind == ActionTimelineOperationKind.ResizeLeft;
        long start = left ? (long)target.StartFrame + frameDelta : target.StartFrame;
        long duration = left ? (long)target.DurationFrames - frameDelta : (long)target.DurationFrames + frameDelta;
        if (start < 0L || start > int.MaxValue || duration < 1L || duration > int.MaxValue)
            return Rejected("Range must remain at or after Frame 0 and at least one Frame long.");
        var result = new ActionTimelineOperationResult { State = ActionTimelineOperationState.Allowed };
        result.Candidates.Add(Candidate(target, (int)start, (int)duration, target.LaneIndex));
        if (ModifiedGameplayItemsOverlap(result.Candidates))
            return Rejected("Range resize would overlap another GameplayItem on the same Lane.");
        return result;
    }

    private ActionTimelineOperationResult EvaluateTrim(int frameDelta)
    {
        if (_targets.Count != 1 || !(_targets[0].Source is AnimationSegment))
            return Rejected("Animation trim requires one AnimationSegment.");
        Entry target = _targets[0];
        if (target.AnimationAsset == null || target.Clip == null || !ActionAuthoringMath.IsFinite(target.PlayRate) ||
            target.PlayRate <= 0f || !ActionAuthoringMath.IsFinite(target.SourceStartTime) ||
            !ActionAuthoringMath.IsFinite(target.SourceEndTime))
            return Rejected("AnimationSegment cannot be trimmed until its source data is valid.");
        bool left = Kind == ActionTimelineOperationKind.TrimLeft;
        long start = left ? (long)target.StartFrame + frameDelta : target.StartFrame;
        float seconds = frameDelta / (float)ActionTimelineData.FrameRate * target.PlayRate;
        float sourceStart = left ? target.SourceStartTime + seconds : target.SourceStartTime;
        float sourceEnd = left ? target.SourceEndTime : target.SourceEndTime + seconds;
        if (start < 0L || start > int.MaxValue || !ActionAuthoringMath.IsFinite(sourceStart) ||
            !ActionAuthoringMath.IsFinite(sourceEnd) || sourceStart < 0f || sourceEnd <= sourceStart ||
            sourceEnd > target.ClipLength + 0.00001f)
            return Rejected("Trim would create an invalid source range or move before Frame 0.");
        int duration = CalculateAnimationDuration(sourceStart, sourceEnd, target.PlayRate);
        if (duration < 1)
            return Rejected("Trim would create an invalid Animation duration.");
        var result = new ActionTimelineOperationResult { State = ActionTimelineOperationState.Allowed };
        ActionTimelineOperationCandidate candidate = Candidate(target, (int)start, duration, target.LaneIndex);
        candidate.SourceStartTime = sourceStart;
        candidate.SourceEndTime = sourceEnd;
        result.Candidates.Add(candidate);
        if (ModifiedAnimationsOverlap(result.Candidates))
            return Rejected("Trim would overlap another AnimationSegment.");
        return result;
    }

    private bool ModifiedAnimationsOverlap(List<ActionTimelineOperationCandidate> candidates)
    {
        var candidateBySource = candidates.Where(candidate => candidate.Source is AnimationSegment)
            .ToDictionary(candidate => candidate.Source, candidate => candidate);
        if (candidateBySource.Count == 0)
            return false;
        for (int left = 0; left < _animations.Count; left++)
        for (int right = left + 1; right < _animations.Count; right++)
        {
            Entry leftEntry = _animations[left];
            Entry rightEntry = _animations[right];
            ActionTimelineOperationCandidate leftCandidate = candidateBySource.TryGetValue(leftEntry.Source, out ActionTimelineOperationCandidate lc) ? lc : null;
            ActionTimelineOperationCandidate rightCandidate = candidateBySource.TryGetValue(rightEntry.Source, out ActionTimelineOperationCandidate rc) ? rc : null;
            if (leftCandidate == null && rightCandidate == null)
                continue;
            long leftStart = leftCandidate != null ? leftCandidate.StartFrame : leftEntry.StartFrame;
            long leftEnd = leftStart + (leftCandidate != null ? leftCandidate.DurationFrames : leftEntry.DurationFrames);
            long rightStart = rightCandidate != null ? rightCandidate.StartFrame : rightEntry.StartFrame;
            long rightEnd = rightStart + (rightCandidate != null ? rightCandidate.DurationFrames : rightEntry.DurationFrames);
            if (leftEnd > leftStart && rightEnd > rightStart && leftStart < rightEnd && rightStart < leftEnd)
                return true;
        }
        return false;
    }

    private bool ModifiedGameplayItemsOverlap(List<ActionTimelineOperationCandidate> candidates)
    {
        var candidateBySource = candidates.Where(candidate => candidate.Source is GameplayItem)
            .ToDictionary(candidate => candidate.Source, candidate => candidate);
        if (candidateBySource.Count == 0)
            return false;

        var finalItems = new List<(object source, int lane, long start, long end, bool modified)>();
        for (int laneIndex = 0; laneIndex < _laneItems.Count; laneIndex++)
        {
            foreach (Entry entry in _laneItems[laneIndex])
            {
                if (entry.Source == null)
                    continue;
                bool modified = candidateBySource.TryGetValue(entry.Source, out ActionTimelineOperationCandidate candidate);
                int finalLane = modified ? candidate.LaneIndex : laneIndex;
                long start;
                long end;
                if (modified)
                {
                    start = candidate.StartFrame;
                    end = start + candidate.DurationFrames;
                }
                else
                {
                    start = entry.StartFrame;
                    end = start + entry.DurationFrames;
                }
                finalItems.Add((entry.Source, finalLane, start, end, modified));
            }
        }

        for (int left = 0; left < finalItems.Count; left++)
        for (int right = left + 1; right < finalItems.Count; right++)
        {
            var a = finalItems[left];
            var b = finalItems[right];
            if (a.lane == b.lane && (a.modified || b.modified)
                                 && ActionGameplayLaneOccupancy.Overlaps(a.start, a.end, b.start, b.end))
                return true;
        }
        return false;
    }

    private bool MatchesTarget(Entry target)
    {
        if (target.Source is PointGameplayItem point)
            return point.EditorId == target.EditorId && point.Frame == target.StartFrame;
        if (target.Source is RangeGameplayItem range)
            return range.EditorId == target.EditorId && range.StartFrame == target.StartFrame &&
                   range.DurationFrames == target.DurationFrames;
        return target.Source is AnimationSegment animation && Matches(target, animation);
    }

    private static bool MatchesGameplay(Entry entry, GameplayItem item)
    {
        if (item == null) return entry.Source == null;
        if (item.EditorId != entry.EditorId) return false;
        if (item is PointGameplayItem point)
            return point.Frame == entry.StartFrame && entry.DurationFrames == 1;
        return item is RangeGameplayItem range && range.StartFrame == entry.StartFrame &&
               range.DurationFrames == entry.DurationFrames;
    }

    private static bool Matches(Entry entry, AnimationSegment animation)
    {
        AnimationClip clip = animation.AnimationAsset != null ? animation.AnimationAsset.Clip : null;
        float length = clip != null ? clip.length : 0f;
        return ReferenceEquals(entry.Source, animation) && animation.EditorId == entry.EditorId &&
               animation.StartFrame == entry.StartFrame && animation.AnimationAsset == entry.AnimationAsset &&
               clip == entry.Clip && Mathf.Approximately(length, entry.ClipLength) &&
               Mathf.Approximately(animation.SourceStartTime, entry.SourceStartTime) &&
               Mathf.Approximately(animation.SourceEndTime, entry.SourceEndTime) &&
               Mathf.Approximately(animation.PlayRate, entry.PlayRate);
    }

    private static Entry CaptureEntry(ActionDocumentEntry documentEntry)
    {
        var entry = new Entry
        {
            Source = documentEntry.Source,
            EditorId = documentEntry.EditorId,
            StartFrame = documentEntry.StartFrame,
            DurationFrames = documentEntry.Source is AnimationSegment animationDuration ? animationDuration.DerivedDurationFrames :
                documentEntry.Source is RangeGameplayItem range ? range.DurationFrames : 1,
            LaneIndex = documentEntry.LaneIndex,
        };
        if (documentEntry.Source is AnimationSegment animation)
        {
            entry.AnimationAsset = animation.AnimationAsset;
            entry.Clip = animation.AnimationAsset != null ? animation.AnimationAsset.Clip : null;
            entry.ClipLength = entry.Clip != null ? entry.Clip.length : 0f;
            entry.SourceStartTime = animation.SourceStartTime;
            entry.SourceEndTime = animation.SourceEndTime;
            entry.PlayRate = animation.PlayRate;
        }
        return entry;
    }

    private static ActionTimelineOperationCandidate Candidate(Entry entry, int start, int duration, int laneIndex)
    {
        return new ActionTimelineOperationCandidate
        {
            Source = entry.Source,
            StartFrame = start,
            DurationFrames = duration,
            LaneIndex = laneIndex,
            AnimationAsset = entry.AnimationAsset,
            Clip = entry.Clip,
            ClipLength = entry.ClipLength,
            SourceStartTime = entry.SourceStartTime,
            SourceEndTime = entry.SourceEndTime,
            PlayRate = entry.PlayRate,
        };
    }

    private static ActionTimelineOperationResult Allowed(ActionTimelineOperationCandidate candidate)
    {
        var result = new ActionTimelineOperationResult { State = ActionTimelineOperationState.Allowed };
        result.Candidates.Add(candidate);
        return result;
    }

    private static ActionTimelineOperationResult Rejected(string message) =>
        new ActionTimelineOperationResult { State = ActionTimelineOperationState.Rejected, Message = message ?? string.Empty };

    private static int CalculateAnimationDuration(float sourceStart, float sourceEnd, float playRate) =>
        AnimationSegment.CalculateDerivedDurationFrames(sourceStart, sourceEnd, playRate);
}

#endif
