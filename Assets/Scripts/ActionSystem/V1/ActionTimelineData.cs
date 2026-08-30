using System;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

/// <summary>
/// Inline V1 action authoring data. It is intentionally data-only: the current
/// playback path does not read it until the later ActionRuntime stages.
/// </summary>
[Serializable]
public sealed class ActionTimelineData
{
    public const int FrameRate = 60;

    [SerializeField] private List<AnimationSegment> _animationSegments = new List<AnimationSegment>();
    [SerializeField] private List<GameplayLane> _gameplayLanes = new List<GameplayLane>();

    public IReadOnlyList<AnimationSegment> AnimationSegments => _animationSegments;
    public IReadOnlyList<GameplayLane> GameplayLanes => _gameplayLanes;
    public int DurationFrames => CalculateDurationFrames();

#if UNITY_EDITOR
    public List<AnimationSegment> EditorAnimationSegments => _animationSegments;
    public List<GameplayLane> EditorGameplayLanes => _gameplayLanes;
#endif

    public int CalculateDurationFrames()
    {
        long duration = 1;

        if (_animationSegments != null)
        {
            for (int i = 0; i < _animationSegments.Count; i++)
            {
                AnimationSegment segment = _animationSegments[i];
                if (segment != null)
                    duration = Math.Max(duration, segment.EndFrameExclusiveLong);
            }
        }

        if (_gameplayLanes != null)
        {
            for (int laneIndex = 0; laneIndex < _gameplayLanes.Count; laneIndex++)
            {
                GameplayLane lane = _gameplayLanes[laneIndex];
                IReadOnlyList<GameplayItem> items = lane != null ? lane.Items : null;
                for (int itemIndex = 0; items != null && itemIndex < items.Count; itemIndex++)
                {
                    GameplayItem item = items[itemIndex];
                    switch (item)
                    {
                        case PointGameplayItem point:
                            duration = Math.Max(duration, (long)point.Frame + 1L);
                            break;
                        case RangeGameplayItem range:
                            duration = Math.Max(duration, range.EndFrameExclusiveLong);
                            break;
                    }
                }
            }
        }

        return duration >= int.MaxValue ? int.MaxValue : (int)Math.Max(1L, duration);
    }
}

[Serializable]
public sealed class AnimationSegment
{
    [SerializeField, HideInInspector] private string _editorId;
    [SerializeField] private int _startFrame;
    [SerializeField] private AnimationAsset _animationAsset;
    [SerializeField] private float _sourceStartTime;
    [SerializeField] private float _sourceEndTime;
    [SerializeField] private float _playRate = 1f;

    public string EditorId => _editorId;
    public int StartFrame => _startFrame;
    public AnimationAsset AnimationAsset => _animationAsset;
    public float SourceStartTime => _sourceStartTime;
    public float SourceEndTime => _sourceEndTime;
    public float PlayRate => _playRate;
    public float SourceDurationSeconds => _sourceEndTime - _sourceStartTime;
    public int DerivedDurationFrames => CalculateDerivedDurationFrames();
    public long EndFrameExclusiveLong => (long)_startFrame + DerivedDurationFrames;

    public int CalculateDerivedDurationFrames()
    {
        if (!ActionAuthoringMath.IsFinite(_sourceStartTime)
            || !ActionAuthoringMath.IsFinite(_sourceEndTime)
            || !ActionAuthoringMath.IsFinite(_playRate)
            || _playRate <= 0f)
            return 0;

        double timelineSeconds = (_sourceEndTime - _sourceStartTime) / _playRate;
        if (timelineSeconds <= 0d || double.IsNaN(timelineSeconds) || double.IsInfinity(timelineSeconds))
            return 0;

        double frames = Math.Ceiling(timelineSeconds * ActionTimelineData.FrameRate);
        return frames >= int.MaxValue ? int.MaxValue : (int)frames;
    }

#if UNITY_EDITOR
    public void EditorSetEditorId(string editorId) => _editorId = editorId;

    public void EditorSetData(
        int startFrame,
        AnimationAsset animationAsset,
        float sourceStartTime,
        float sourceEndTime,
        float playRate)
    {
        _startFrame = startFrame;
        _animationAsset = animationAsset;
        _sourceStartTime = sourceStartTime;
        _sourceEndTime = sourceEndTime;
        _playRate = playRate;
    }
#endif
}

[Serializable]
public sealed class GameplayLane
{
    [SerializeField, HideInInspector] private string _editorId;
    [SerializeField] private string _name = "Gameplay";
    [SerializeField] private bool _muted;
    [SerializeReference, SubclassSelector] private List<GameplayItem> _items = new List<GameplayItem>();

    public string EditorId => _editorId;
    public string Name => _name;
    public bool Muted => _muted;
    public IReadOnlyList<GameplayItem> Items => _items;

#if UNITY_EDITOR
    public List<GameplayItem> EditorItems => _items;
    public void EditorSetEditorId(string editorId) => _editorId = editorId;
    public void EditorSetName(string name) => _name = name ?? string.Empty;
    public void EditorSetMuted(bool muted) => _muted = muted;
#endif
}

[Serializable]
public abstract class GameplayItem
{
    [SerializeField, HideInInspector] private string _editorId;
    [SerializeField] private bool _muted;

    public string EditorId => _editorId;
    public bool Muted => _muted;

#if UNITY_EDITOR
    public void EditorSetEditorId(string editorId) => _editorId = editorId;
    public void EditorSetMuted(bool muted) => _muted = muted;
#endif
}

[Serializable]
public abstract class PointGameplayItem : GameplayItem
{
    [SerializeField] private int _frame;
    public int Frame => _frame;

    /// <summary>
    /// Stage 2 scheduler hook. Concrete V1 items override this in Stage 3;
    /// returning null keeps Stage 1 data inert on the runtime side path.
    /// </summary>
    protected internal virtual IActionPointRuntime CreateRuntime() => null;

#if UNITY_EDITOR
    public void EditorSetFrame(int frame) => _frame = frame;
#endif
}

[Serializable]
public abstract class RangeGameplayItem : GameplayItem
{
    [SerializeField] private int _startFrame;
    [SerializeField] private int _durationFrames = 1;

    public int StartFrame => _startFrame;
    public int DurationFrames => _durationFrames;
    public long EndFrameExclusiveLong => (long)_startFrame + _durationFrames;

    /// <summary>
    /// Stage 2 scheduler hook. Concrete V1 items override this in Stage 3;
    /// returning null keeps Stage 1 data inert on the runtime side path.
    /// </summary>
    protected internal virtual IActionRangeRuntime CreateRuntime() => null;

#if UNITY_EDITOR
    public void EditorSetTiming(int startFrame, int durationFrames)
    {
        _startFrame = startFrame;
        _durationFrames = durationFrames;
    }
#endif
}

[Serializable]
public sealed class ImpulseItem : PointGameplayItem
{
    [SerializeField] private ImpulseItemConfig _config = new ImpulseItemConfig();
    public ImpulseItemConfig Config => _config;
}

[Serializable]
public sealed class HitBoxItem : RangeGameplayItem
{
    [SerializeField] private HitBoxItemConfig _config = new HitBoxItemConfig();
    public HitBoxItemConfig Config => _config;
}

[Serializable]
public sealed class RootMotionItem : RangeGameplayItem
{
    [SerializeField] private RootMotionItemConfig _config = new RootMotionItemConfig();
    public RootMotionItemConfig Config => _config;
}

[Serializable]
public sealed class SelfRotationItem : RangeGameplayItem
{
    [SerializeField] private SelfRotationItemConfig _config = new SelfRotationItemConfig();
    public SelfRotationItemConfig Config => _config;
}

[Serializable]
public sealed class VelocityOverrideItem : RangeGameplayItem
{
    [SerializeField] private VelocityOverrideItemConfig _config = new VelocityOverrideItemConfig();
    public VelocityOverrideItemConfig Config => _config;
}

[Serializable]
public sealed class MotionPolicyItem : RangeGameplayItem
{
    [SerializeField] private MotionPolicyItemConfig _config = new MotionPolicyItemConfig();
    public MotionPolicyItemConfig Config => _config;
}

[Serializable]
public sealed class TagItem : RangeGameplayItem
{
    [SerializeField] private TagItemConfig _config = new TagItemConfig();
    public TagItemConfig Config => _config;
}

[Serializable]
public sealed class ImpulseItemConfig
{
    public ImpulseConfig impulse = new ImpulseConfig();
    public bool useHorizontalImpulse = true;
    public bool useVerticalBallistic;
    public ActionBallisticVelocityOperation verticalOperation = ActionBallisticVelocityOperation.Add;
    public bool overrideGravityScale;
    public float gravityScale = 1f;
}

public enum ActionBallisticVelocityOperation
{
    Add = 0,
    Set = 1,
}

[Serializable]
public sealed class HitBoxItemConfig
{
    public BoneReference boneReference;
    public ActionHitBoxConfig hitboxConfig = new ActionHitBoxConfig();
    public AttackDataConfig dataConfig = new AttackDataConfig();
    [SerializeReference, SubclassSelector] public List<ImpactEffectConfig> effects = new List<ImpactEffectConfig>();
}

[Serializable]
public sealed class RootMotionItemConfig
{
    public AnimationAsset animationAsset;
    public float sourceStartTime;
    public float playRate = 1f;
}

public enum ActionSelfRotationSource
{
    RootMotion = 0,
    Target = 1,
    Direction = 2,
}

public enum ActionSelfRotationMode
{
    Snap = 0,
    RotateBySpeed = 1,
}

public enum ActionSelfRotationTargetSource
{
    CombatTarget = 0,
    ContextInstigator = 1,
    ContextTarget = 2,
}

public enum ActionSelfRotationDirectionSource
{
    PresetLocal = 0,
    ContextDirection = 1,
}

[Serializable]
public sealed class SelfRotationItemConfig
{
    public ActionSelfRotationSource source = ActionSelfRotationSource.RootMotion;
    public ActionSelfRotationMode mode = ActionSelfRotationMode.Snap;
    public ActionSelfRotationTargetSource targetSource = ActionSelfRotationTargetSource.CombatTarget;
    public ActionSelfRotationDirectionSource directionSource = ActionSelfRotationDirectionSource.PresetLocal;
    public Vector3 presetLocalDirection = Vector3.forward;
    public float angularSpeedDegrees = 720f;
    public AnimationAsset animationAsset;
    public float sourceStartTime;
    public float playRate = 1f;
}

[Serializable]
public sealed class VelocityOverrideItemConfig
{
    public VelocityConfig velocity = new VelocityConfig();
}

[Serializable]
public sealed class MotionPolicyItemConfig
{
    public bool useLocomotionScale;
    public float locomotionScale = 1f;
    public bool useAirLocomotionScale;
    public float airLocomotionScale = 1f;
    public bool useGravityScale;
    public float gravityScale = 1f;
}

[Serializable]
public sealed class TagItemConfig
{
    public TagReference tag;
    public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;
}

public static class ActionAuthoringMath
{
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static bool IsFinite(Vector3 value) =>
        IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
}
