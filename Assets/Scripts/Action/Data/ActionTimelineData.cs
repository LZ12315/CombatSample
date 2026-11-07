using System;
using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

/// <summary>
/// Inline ActionRuntime action data shared by authoring, Preview and the ActionRuntime ActionRuntime.
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

    public int CalculateDerivedDurationFrames() =>
        CalculateDerivedDurationFrames(_sourceStartTime, _sourceEndTime, _playRate);

    internal static int CalculateDerivedDurationFrames(float sourceStartTime, float sourceEndTime, float playRate)
    {
        if (!ActionAuthoringMath.IsFinite(sourceStartTime)
            || !ActionAuthoringMath.IsFinite(sourceEndTime)
            || !ActionAuthoringMath.IsFinite(playRate)
            || playRate <= 0f)
            return 0;

        double timelineSeconds = (sourceEndTime - sourceStartTime) / playRate;
        if (timelineSeconds <= 0d || double.IsNaN(timelineSeconds) || double.IsInfinity(timelineSeconds))
            return 0;

        // Serialized source values are floats; remove only sub-microframe representation noise
        // before applying the documented ceil rule (for example 2f / 60f must remain 2 Frames).
        double frames = Math.Ceiling(timelineSeconds * ActionTimelineData.FrameRate - 1e-6d);
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
    /// Stage 2 scheduler hook. Concrete ActionRuntime items override this in Stage 3;
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
    public virtual int DurationFrames => _durationFrames;
    public long EndFrameExclusiveLong => (long)_startFrame + DurationFrames;

    /// <summary>
    /// Stage 2 scheduler hook. Concrete ActionRuntime items override this in Stage 3;
    /// returning null keeps Stage 1 data inert on the runtime side path.
    /// </summary>
    protected internal virtual IActionRangeRuntime CreateRuntime() => null;

    /// <summary>
    /// Snapshot-aware factory used by the scheduler. Existing fixtures can
    /// retain the zero-argument hook; production ActionRuntime ranges capture duration.
    /// </summary>
    protected internal virtual IActionRangeRuntime CreateRuntime(int durationFrames) => CreateRuntime();

    protected void RestoreLegacyPointTiming(int frame)
    {
        _startFrame = frame;
        _durationFrames = 1;
    }

#if UNITY_EDITOR
    public virtual void EditorSetTiming(int startFrame, int durationFrames)
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
    protected internal override IActionPointRuntime CreateRuntime() => new ActionImpulseItemRuntime(_config);
#if UNITY_EDITOR
    internal void EditorEnsureConfig() => _config ??= new ImpulseItemConfig();
#endif
}

/// <summary>Spawns a VFX prefab independently of contact and combat resolution.</summary>
[Serializable]
public sealed class EffectItem : RangeGameplayItem, ISerializationCallbackReceiver
{
    // Exact legacy field name is intentional: point timing moved across base classes.
    [SerializeField, HideInInspector] private int _frame = int.MinValue;
    [SerializeField] private EffectItemConfig _config = new EffectItemConfig();
    public EffectItemConfig Config => _config;
    protected internal override IActionRangeRuntime CreateRuntime() => new ActionEffectItemRuntime(_config);
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize()
    {
        if (_frame == int.MinValue) return;
        RestoreLegacyPointTiming(_frame);
        _frame = int.MinValue;
    }
#if UNITY_EDITOR
    internal void EditorEnsureConfig() => _config ??= new EffectItemConfig();
#endif
}

/// <summary>Owns one directional camera-shake contribution for its half-open frame interval.</summary>
[Serializable]
public sealed class ScreenShakeItem : RangeGameplayItem, ISerializationCallbackReceiver
{
    [SerializeField, HideInInspector] private int _frame = int.MinValue;
    [SerializeField] private ScreenShakeItemConfig _config = new ScreenShakeItemConfig();
    public ScreenShakeItemConfig Config => _config;
    protected internal override IActionRangeRuntime CreateRuntime(int durationFrames) =>
        new ActionScreenShakeItemRuntime(_config, durationFrames);
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize()
    {
        if (_frame == int.MinValue) return;
        RestoreLegacyPointTiming(_frame);
        _frame = int.MinValue;
    }
#if UNITY_EDITOR
    internal void EditorEnsureConfig()
    {
        _config ??= new ScreenShakeItemConfig();
        _config.screenShake ??= new ActionScreenShakeSettings();
    }
#endif
}

[Serializable]
public sealed class HitBoxItem : RangeGameplayItem
{
    [SerializeField] private HitBoxItemConfig _config = new HitBoxItemConfig();
    public HitBoxItemConfig Config => _config;
    protected internal override IActionRangeRuntime CreateRuntime(int durationFrames) =>
        new ActionHitBoxItemRuntime(_config, EditorId);
#if UNITY_EDITOR
    internal void EditorEnsureConfig()
    {
        _config ??= new HitBoxItemConfig();
        _config.hitboxConfig ??= new ActionHitBoxConfig();
        _config.dataConfig ??= new AttackDataConfig();
        _config.effects ??= new List<ImpactEffectConfig>();
    }

    internal void EditorInitializeNewDefaults()
    {
        EditorEnsureConfig();
        _config.anchor = ActionHitBoxAnchor.ActorRoot;
        _config.hitboxConfig.shape = ActionHitBoxShape.Box;
        _config.hitboxConfig.center = Vector3.zero;
        _config.hitboxConfig.rotation = Quaternion.identity;
        _config.hitboxConfig.size = new Vector3(0.5f, 0.5f, 0.5f);
        _config.hitboxConfig.height = 0.5f;
        _config.hitboxConfig.radius = 0.1f;
    }
#endif
}

[Serializable]
public sealed class RootMotionItem : RangeGameplayItem
{
    [SerializeField] private RootMotionItemConfig _config = new RootMotionItemConfig();
    [SerializeField, HideInInspector] private bool _hasSourceRange;

    public RootMotionItemConfig Config => _config;
    // Old assets retain their exact serialized duration until an explicit source edit is applied.
    // Merely reading/opening them neither dirties assets nor clamps out-of-Clip legacy ranges.
    public float SourceEndTime => _hasSourceRange ? _config?.sourceEndTime ?? 0f :
        (_config?.sourceStartTime ?? 0f) + (float)(base.DurationFrames / (double)ActionTimelineData.FrameRate * (_config?.playRate ?? 1f));
    public override int DurationFrames => _hasSourceRange && _config != null
        ? AnimationSegment.CalculateDerivedDurationFrames(_config.sourceStartTime, _config.sourceEndTime, _config.playRate)
        : base.DurationFrames;

    protected internal override IActionRangeRuntime CreateRuntime(int durationFrames) =>
        new ActionRootMotionItemRuntime(_config, SourceEndTime);
#if UNITY_EDITOR
    internal void EditorEnsureConfig() => _config ??= new RootMotionItemConfig();

    internal void EditorSetSource(int startFrame, AnimationAsset animationAsset, float sourceStartTime,
        float sourceEndTime, float playRate)
    {
        EditorEnsureConfig();
        _config.animationAsset = animationAsset;
        _config.sourceStartTime = sourceStartTime;
        _config.sourceEndTime = sourceEndTime;
        _config.playRate = playRate;
        _hasSourceRange = true;
        base.EditorSetTiming(startFrame, DurationFrames);
    }

    public override void EditorSetTiming(int startFrame, int durationFrames)
    {
        if (_hasSourceRange && _config != null && durationFrames != DurationFrames)
            EditorSetSource(startFrame, _config.animationAsset, _config.sourceStartTime,
                _config.sourceStartTime + (float)(durationFrames / (double)ActionTimelineData.FrameRate * _config.playRate), _config.playRate);
        else
            base.EditorSetTiming(startFrame, durationFrames);
    }
#endif
}

[Serializable]
public sealed class SelfRotationItem : RangeGameplayItem
{
    [SerializeField] private SelfRotationItemConfig _config = new SelfRotationItemConfig();
    public SelfRotationItemConfig Config => _config;
    protected internal override IActionRangeRuntime CreateRuntime(int durationFrames) =>
        new ActionSelfRotationItemRuntime(_config, durationFrames);
#if UNITY_EDITOR
    internal void EditorEnsureConfig() => _config ??= new SelfRotationItemConfig();
#endif
}

[Serializable]
public sealed class VelocityOverrideItem : RangeGameplayItem
{
    [SerializeField] private VelocityOverrideItemConfig _config = new VelocityOverrideItemConfig();
    public VelocityOverrideItemConfig Config => _config;
    protected internal override IActionRangeRuntime CreateRuntime(int durationFrames) =>
        new ActionVelocityOverrideItemRuntime(_config, durationFrames);
#if UNITY_EDITOR
    internal void EditorEnsureConfig() => _config ??= new VelocityOverrideItemConfig();
#endif
}

[Serializable]
public sealed class MoveToTargetItem : RangeGameplayItem
{
    [SerializeField] private MoveToTargetItemConfig _config = new MoveToTargetItemConfig();
    public MoveToTargetItemConfig Config => _config;
    protected internal override IActionRangeRuntime CreateRuntime(int durationFrames) =>
        new ActionMoveToTargetItemRuntime(_config, durationFrames);
#if UNITY_EDITOR
    internal void EditorEnsureConfig() => _config ??= new MoveToTargetItemConfig();
#endif
}

[Serializable]
public sealed class MotionPolicyItem : RangeGameplayItem
{
    [SerializeField] private MotionPolicyItemConfig _config = new MotionPolicyItemConfig();
    public MotionPolicyItemConfig Config => _config;
    // Resolved from an immutable frame snapshot by the owning ActionRuntime, not an independent Motor owner.
    protected internal override IActionRangeRuntime CreateRuntime() => null;
#if UNITY_EDITOR
    internal void EditorEnsureConfig() => _config ??= new MotionPolicyItemConfig();
#endif
}

[Serializable]
public sealed class TagItem : RangeGameplayItem
{
    [SerializeField] private TagItemConfig _config = new TagItemConfig();
    public TagItemConfig Config => _config;
    protected internal override IActionRangeRuntime CreateRuntime() => new ActionTagItemRuntime(_config);
#if UNITY_EDITOR
    internal void EditorEnsureConfig() => _config ??= new TagItemConfig();
#endif
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

public enum ActionEffectAnchor
{
    ActorRoot = 0,
    HumanoidBone = 1,
    TransformPath = 2,
}

public enum ActionEffectOrientation { AnchorRelative = 0, ScreenOriented = 1, WorldDirectional = 2 }
public enum ActionEffectWorldDirection { ActorForward = 0, AnchorForward = 1, WorldUp = 2, CameraForward = 3, Custom = 4 }
public enum ActionEffectOrientationUpdate { MatchPositionBinding = 0, OnEntry = 1, Continuous = 2 }

[Serializable]
public sealed class EffectItemConfig : ISerializationCallbackReceiver
{
    [SerializeField, HideInInspector] private bool _hasPlaybackSettings = true;
    public GameObject prefab;
    public ActionEffectAnchor anchor = ActionEffectAnchor.ActorRoot;
    public HumanBodyBones bone = HumanBodyBones.RightHand;
    [Tooltip("Transform path relative to the Actor root, including the model root if needed. No fallback on missing paths.")]
    public string transformPath = string.Empty;
    public Vector3 localPosition;
    [Tooltip("Euler offset in degrees relative to the selected orientation, applied after facing.")]
    public Vector3 localEulerAngles;
    [Min(0.001f)] public float scale = 1f;
    [Tooltip("Follow the anchor's position and scale. Off = keep the initial world position and scale.")]
    public bool followAnchor;
    public ActionEffectOrientation orientation;
    public ActionEffectOrientationUpdate orientationUpdate;
    public ActionEffectWorldDirection worldDirection;
    public Vector3 customWorldDirection = Vector3.forward;
    public ScreenAngleMode screenAngleMode;
    public float anglePresetDegrees;
    [Range(0f, 360f)] public float angleRandomRange = 360f;
    public VFXRollMode rollMode = VFXRollMode.Preset;
    public float rollPresetDegrees;
    [Range(0f, 360f)] public float rollRandomRange = 360f;
    public HitVfxOcclusionMode occlusionMode;
    [Min(0.01f), Tooltip("Explicit particle simulation multiplier, independent of Action speed.")]
    public float simulationSpeed = 1f;
    public void OnBeforeSerialize() => _hasPlaybackSettings = true;
    public void OnAfterDeserialize()
    {
        if (!_hasPlaybackSettings)
        {
            simulationSpeed = 1f;
            customWorldDirection = Vector3.forward;
            angleRandomRange = rollRandomRange = 360f;
            rollMode = VFXRollMode.Preset;
        }
        _hasPlaybackSettings = true;
    }
}

public enum ActionScreenShakeDirection
{
    Vertical = 0,
    Horizontal = 1,
    Forward = 2,
    Custom = 3,
}

public enum ActionScreenShakeMode { Directional = 0, IndependentAxes = 1 }

[Serializable]
public sealed class ActionScreenShakeSettings
{
    public bool enabled = true;
    [Min(0f)] public float intensity = 0.3f;
}

[Serializable]
public sealed class ScreenShakeItemConfig : ISerializationCallbackReceiver
{
    [SerializeField, HideInInspector] private bool _hasDirectionalSettings = true;
    [SerializeField, HideInInspector] private bool _hasCurveSettings = true;
    // Preserve the existing enabled/intensity data; this is not a repeated Impulse request.
    public ActionScreenShakeSettings screenShake = new ActionScreenShakeSettings();
    [Tooltip("Direction in each receiving virtual camera's space, before native camera blending.")]
    public ActionScreenShakeDirection direction = ActionScreenShakeDirection.Vertical;
    public ActionScreenShakeMode mode;
    public Vector3 customDirection = Vector3.up;
    [Tooltip("Signed displacement over normalized Action interval progress, not a strength envelope around a hidden sine.")]
    public AnimationCurve waveform = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f),
        new Keyframe(0.35f, -0.6f), new Keyframe(0.6f, 0.25f), new Keyframe(1f, 0f));
    public AnimationCurve positionX = AnimationCurve.Constant(0f, 1f, 0f);
    public AnimationCurve positionY = AnimationCurve.Constant(0f, 1f, 0f);
    public AnimationCurve positionZ = AnimationCurve.Constant(0f, 1f, 0f);
    public bool useRotation;
    [Min(0f)] public float rotationAmplitude = 1f;
    public AnimationCurve pitch = AnimationCurve.Constant(0f, 1f, 0f);
    public AnimationCurve yaw = AnimationCurve.Constant(0f, 1f, 0f);
    public AnimationCurve roll = AnimationCurve.Constant(0f, 1f, 0f);
    [Min(0.01f), Tooltip("Linear attenuation to zero at this distance from the Actor root to the camera.")]
    public float maxDistance = 20f;

    public void OnBeforeSerialize() { _hasDirectionalSettings = true; _hasCurveSettings = true; }
    public void OnAfterDeserialize()
    {
        if (!_hasDirectionalSettings)
        {
            direction = ActionScreenShakeDirection.Vertical;
            maxDistance = 20f;
        }
        if (!_hasCurveSettings)
        {
            var defaults = new ScreenShakeItemConfig();
            waveform = defaults.waveform;
            positionX = defaults.positionX; positionY = defaults.positionY; positionZ = defaults.positionZ;
            pitch = defaults.pitch; yaw = defaults.yaw; roll = defaults.roll;
            customDirection = Vector3.up;
            rotationAmplitude = 1f;
        }
        _hasDirectionalSettings = _hasCurveSettings = true;
    }
}

[Serializable]
public sealed class HitBoxItemConfig
{
    public ActionHitBoxAnchor anchor = ActionHitBoxAnchor.ActorRoot;
    public ActionHitBoxConfig hitboxConfig = new ActionHitBoxConfig();
    public AttackDataConfig dataConfig = new AttackDataConfig();
    [SerializeReference, SubclassSelector] public List<ImpactEffectConfig> effects = new List<ImpactEffectConfig>();
}

[Serializable]
public sealed class RootMotionItemConfig : ISerializationCallbackReceiver
{
    public AnimationAsset animationAsset;
    public float sourceStartTime;
    public float sourceEndTime;
    public float playRate = 1f;
    [Min(0f), Tooltip("Scales horizontal displacement only; does not change timing, rotation or baked data.")]
    public float distanceScale = 1f;
    [SerializeField, HideInInspector] private bool _hasDistanceScale = true;

    // JSON/Unity deserialization can zero missing fields without running initializers. Distinguish
    // a legacy missing multiplier from an explicitly authored zero without rewriting asset files.
    public void OnBeforeSerialize() => _hasDistanceScale = true;
    public void OnAfterDeserialize()
    {
        if (!_hasDistanceScale)
            distanceScale = 1f;
    }

    internal Vector3 ScalePlanarTranslation(Vector3 delta)
    {
        delta.y = 0f;
        float scale = float.IsNaN(distanceScale) || float.IsInfinity(distanceScale) ? 0f : Mathf.Max(0f, distanceScale);
        return delta * scale;
    }
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

public enum ActionMoveToTargetSource
{
    CombatTarget = 0,
    ContextInstigator = 1,
    ContextTarget = 2,
}

[Serializable]
public sealed class MoveToTargetItemConfig
{
    public ActionMoveToTargetSource targetSource = ActionMoveToTargetSource.CombatTarget;
    [Min(0f)] public float speed = 5f;
    [Min(0f), Tooltip("Horizontal distance between actor roots, not collider surfaces.")]
    public float stopDistance = 1f;
    public AnimationCurve speedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
}

[Serializable]
public sealed class MotionPolicyItemConfig
{
    public bool useMovementControl;
    public ActionMovementControl movement;
    public bool useTurnControl;
    public ActionTurnControl turning;
    public float maxTurnSpeed = 180f;
    // Preserve old serialized fields. The authoring UI interprets these without writing migrations on view.
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

/// <summary>
/// Defines the authoring occupancy of GameplayItems. A point occupies one frame and a range uses
/// the half-open interval [StartFrame, EndFrameExclusive), so adjacent content is allowed.
/// Muting is deliberately ignored: muted content still reserves its authored place on a lane.
/// </summary>
public static class ActionGameplayLaneOccupancy
{
    public static bool TryGetInterval(GameplayItem item, out long startFrame, out long endFrameExclusive)
    {
        switch (item)
        {
            case PointGameplayItem point:
                startFrame = point.Frame;
                endFrameExclusive = (long)point.Frame + 1L;
                return point.Frame >= 0;
            case RangeGameplayItem range:
                startFrame = range.StartFrame;
                endFrameExclusive = range.EndFrameExclusiveLong;
                return range.StartFrame >= 0 && range.DurationFrames > 0 && endFrameExclusive > startFrame;
            default:
                startFrame = 0L;
                endFrameExclusive = 0L;
                return false;
        }
    }

    public static bool Overlaps(long leftStart, long leftEndExclusive, long rightStart, long rightEndExclusive)
    {
        return leftEndExclusive > leftStart && rightEndExclusive > rightStart
               && leftStart < rightEndExclusive && rightStart < leftEndExclusive;
    }

    public static bool WouldOverlap(
        GameplayLane lane,
        GameplayItem ignore,
        long startFrame,
        long endFrameExclusive)
    {
        IReadOnlyList<GameplayItem> items = lane != null ? lane.Items : null;
        for (int i = 0; items != null && i < items.Count; i++)
        {
            GameplayItem existing = items[i];
            if (existing == null || ReferenceEquals(existing, ignore)
                                 || !TryGetInterval(existing, out long existingStart, out long existingEnd))
                continue;
            if (Overlaps(startFrame, endFrameExclusive, existingStart, existingEnd))
                return true;
        }
        return false;
    }
}
