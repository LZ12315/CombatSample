using UnityEngine;

/// <summary>Immutable placement and playback parameters captured on range entry.</summary>
internal readonly struct ActionEffectRequest
{
    private readonly EffectItemConfig _snapshot;
    private ActionEffectRequest(EffectItemConfig config)
    {
        // Keep mutable authoring data out of a live presentation, including its orientation policy.
        _snapshot = new EffectItemConfig {
            prefab = config.prefab, anchor = config.anchor, bone = config.bone, transformPath = config.transformPath,
            localPosition = config.localPosition, localEulerAngles = config.localEulerAngles, scale = config.scale,
            followAnchor = config.followAnchor, orientation = config.orientation, orientationUpdate = config.orientationUpdate,
            worldDirection = config.worldDirection, customWorldDirection = config.customWorldDirection,
            screenAngleMode = config.screenAngleMode, anglePresetDegrees = config.anglePresetDegrees,
            angleRandomRange = config.angleRandomRange, rollMode = config.rollMode, rollPresetDegrees = config.rollPresetDegrees,
            rollRandomRange = config.rollRandomRange, occlusionMode = config.occlusionMode, simulationSpeed = config.simulationSpeed
        };
    }
    internal GameObject Prefab => _snapshot.prefab;
    internal ActionEffectAnchor Anchor => _snapshot.anchor;
    internal HumanBodyBones Bone => _snapshot.bone;
    internal string TransformPath => _snapshot.transformPath;
    internal Vector3 LocalPosition => _snapshot.localPosition;
    internal Vector3 LocalEulerAngles => _snapshot.localEulerAngles;
    internal float Scale => _snapshot.scale;
    internal bool FollowAnchor => _snapshot.followAnchor;
    internal HitVfxOcclusionMode Occlusion => _snapshot.occlusionMode;
    internal float SimulationSpeed => _snapshot.simulationSpeed;
    internal bool ContinuousOrientation => _snapshot.orientationUpdate == ActionEffectOrientationUpdate.Continuous
        || (_snapshot.orientationUpdate == ActionEffectOrientationUpdate.MatchPositionBinding && FollowAnchor);
    internal bool ParentToAnchor => FollowAnchor && ContinuousOrientation && _snapshot.orientation == ActionEffectOrientation.AnchorRelative;
    internal bool NeedsCamera => _snapshot.orientation == ActionEffectOrientation.ScreenOriented
        || (_snapshot.orientation == ActionEffectOrientation.WorldDirectional && _snapshot.worldDirection == ActionEffectWorldDirection.CameraForward);

    internal static bool TryCreate(EffectItemConfig config, out ActionEffectRequest request) => TryCreate(config, out request, out _);
    internal static bool TryCreate(EffectItemConfig c, out ActionEffectRequest request, out string error)
    {
        request = default; error = string.Empty;
        if (c == null || c.prefab == null) error = "Assign a VFX Prefab.";
        else if (!IsFinite(c.localPosition) || !IsFinite(c.localEulerAngles) || !Positive(c.scale) || !Positive(c.simulationSpeed) || c.simulationSpeed < 0.01f)
            error = "Placement must be finite; scale must be positive; particle speed must be at least 0.01.";
        else if (!Known(c.anchor) || !Known(c.orientation) || !Known(c.orientationUpdate) || !Known(c.occlusionMode))
            error = "Unknown anchor, orientation, update policy or occlusion mode.";
        else if (c.anchor == ActionEffectAnchor.TransformPath && string.IsNullOrWhiteSpace(c.transformPath)) error = "Enter a path relative to the Actor root.";
        else if (c.anchor == ActionEffectAnchor.HumanoidBone && (c.bone < 0 || c.bone >= HumanBodyBones.LastBone)) error = "Choose a valid Humanoid bone.";
        else if (c.orientation == ActionEffectOrientation.ScreenOriented && (!Known(c.screenAngleMode)
            || !Finite(c.anglePresetDegrees) || !NonNegative(c.angleRandomRange))) error = "Screen angle settings are invalid.";
        else if (c.orientation == ActionEffectOrientation.WorldDirectional && (!Known(c.worldDirection) || !Known(c.rollMode)
            || !Finite(c.rollPresetDegrees) || !NonNegative(c.rollRandomRange)
            || (c.worldDirection == ActionEffectWorldDirection.Custom && (!IsFinite(c.customWorldDirection) || c.customWorldDirection.sqrMagnitude < 1e-8f))))
            error = "World direction must be nonzero and finite; Roll settings must be valid.";
        if (error.Length != 0) return false;
        request = new ActionEffectRequest(c); return true;
    }

    internal Vector2 SampleAngles() => new Vector2(
        _snapshot.orientation == ActionEffectOrientation.ScreenOriented
            ? SampleAngle(_snapshot.screenAngleMode == ScreenAngleMode.Random, _snapshot.anglePresetDegrees, _snapshot.angleRandomRange) : 0f,
        _snapshot.orientation == ActionEffectOrientation.WorldDirectional
            ? SampleAngle(_snapshot.rollMode == VFXRollMode.Random, _snapshot.rollPresetDegrees, _snapshot.rollRandomRange) : 0f);
    private static float SampleAngle(bool random, float preset, float range) => !random ? preset : range > 0f ? Random.Range(0f, range) : 0f;

    internal bool TryResolveRotation(Quaternion anchorRotation, Quaternion actorRotation, Quaternion? cameraRotation, Vector2 angles, out Quaternion rotation)
    {
        rotation = anchorRotation;
        if (NeedsCamera && !cameraRotation.HasValue) return false;
        if (_snapshot.orientation == ActionEffectOrientation.ScreenOriented)
            rotation = ScreenOrientedImpactRotationResolver.Resolve(ScreenAngleMode.Preset, angles.x, 0f, cameraRotation.Value);
        else if (_snapshot.orientation == ActionEffectOrientation.WorldDirectional)
        {
            Vector3 forward = _snapshot.worldDirection switch {
                ActionEffectWorldDirection.ActorForward => actorRotation * Vector3.forward,
                ActionEffectWorldDirection.AnchorForward => anchorRotation * Vector3.forward,
                ActionEffectWorldDirection.WorldUp => Vector3.up,
                ActionEffectWorldDirection.CameraForward => cameraRotation.Value * Vector3.forward,
                _ => _snapshot.customWorldDirection
            };
            rotation = WorldDirectionalImpactRotationResolver.Resolve(forward, VFXRollMode.Preset, 0f, 0f)
                * Quaternion.AngleAxis(angles.y, Vector3.forward);
        }
        rotation *= Quaternion.Euler(LocalEulerAngles);
        return true;
    }

    internal Pose ResolveWorldPose(Matrix4x4 anchorLocalToWorld, Quaternion anchorWorldRotation) =>
        new Pose(anchorLocalToWorld.MultiplyPoint3x4(LocalPosition), anchorWorldRotation * Quaternion.Euler(LocalEulerAngles));
    internal static bool IsFinite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    private static bool Positive(float v) => Finite(v) && v > 0f;
    private static bool NonNegative(float v) => Finite(v) && v >= 0f;
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static bool Known<T>(T value) where T : System.Enum => System.Enum.IsDefined(typeof(T), value);
}

/// <summary>Action-only anchors, with no target/HitBox fallback or scene references in Action data.</summary>
internal static class ActionEffectAnchorResolver
{
    internal static bool TryResolve(in ActionEffectRequest request, Transform actorRoot, Animator animator,
        out Transform binding, out string failureReason)
    {
        binding = null; failureReason = string.Empty;
        if (actorRoot == null) failureReason = "The Actor root is missing.";
        else if (request.Anchor == ActionEffectAnchor.ActorRoot) binding = actorRoot;
        else if (request.Anchor == ActionEffectAnchor.TransformPath)
        {
            binding = actorRoot.Find(request.TransformPath);
            if (binding == null) failureReason = $"Transform path '{request.TransformPath}' does not exist under the Actor root.";
        }
        else if (request.Anchor == ActionEffectAnchor.HumanoidBone)
        {
            if (animator == null || animator.avatar == null || !animator.isHuman)
                failureReason = "HumanoidBone requires an Animator with a valid Humanoid Avatar.";
            else
            {
                binding = animator.GetBoneTransform(request.Bone);
                if (binding == null) failureReason = $"The Humanoid Avatar does not provide '{request.Bone}'.";
            }
        }
        else failureReason = "The configured Effect anchor is invalid.";
        return binding != null;
    }
}

/// <summary>Range-owned VFX; shares initialization/facing with Damage, not its spatial context or lifetime.</summary>
internal static class ActionEffectPlayer
{
    private static bool _warnedMissingLayer;
    internal static IActionPresentation Play(Actor actor, ActionEffectRequest request)
    {
        if (!Application.isPlaying || actor == null || !actor.isActiveAndEnabled || request.Prefab == null) return null;
        Animator animator = request.Anchor == ActionEffectAnchor.HumanoidBone
            ? (actor.animancer != null ? actor.animancer.Animator : actor.GetComponentInChildren<Animator>()) : null;
        if (!ActionEffectAnchorResolver.TryResolve(request, actor.transform, animator, out Transform anchor, out string failure))
        { Debug.LogWarning($"[Action Effect] {failure} Effect '{request.Prefab.name}' was skipped.", actor); return null; }
        Camera camera = null;
        if (request.NeedsCamera && !ImpactRotationUtility.TryGetPrimaryCamera(out camera))
        { Debug.LogWarning($"[Action Effect] '{request.Prefab.name}' requires an enabled camera. Playback was skipped.", actor); return null; }
        // Sample once per entry, before Prefab Awake can interrupt the owning Action.
        Vector2 angles = request.SampleAngles();
        request.TryResolveRotation(anchor.rotation, actor.transform.rotation,
            camera != null ? (Quaternion?)camera.transform.rotation : null, angles, out Quaternion rotation);
        Vector3 position = anchor.TransformPoint(request.LocalPosition);
        Vector3 localScale = request.Prefab.transform.localScale * request.Scale;
        Vector3 worldScale = Vector3.Scale(anchor.lossyScale, localScale);
        if (!ActionEffectRequest.IsFinite(position) || !ActionEffectRequest.IsFinite(worldScale)) return null;
        GameObject instance = Object.Instantiate(request.Prefab, position, rotation, request.ParentToAnchor ? anchor : null);
        var presentation = new Instance(instance);
        try
        {
            instance.transform.localScale = request.ParentToAnchor ? localScale : worldScale;
            if (!VfxPlaybackUtility.Initialize(instance, request.Occlusion, request.SimulationSpeed) && !_warnedMissingLayer)
            {
                _warnedMissingLayer = true;
                Debug.LogWarning($"Layer '{HitConfirmVfxRenderUtility.HitConfirmVfxLayerName}' is missing. Action VFX will use normal depth.", actor);
            }
            if (!request.ParentToAnchor && (request.FollowAnchor || request.ContinuousOrientation))
                instance.AddComponent<ActionEffectFollower>().Bind(request, anchor, actor.transform, localScale, angles);
            return presentation;
        }
        catch
        {
            // The buffer cannot release a handle that failed before Play returned it.
            presentation.Dispose();
            throw;
        }
    }

    private sealed class Instance : IActionPresentation
    {
        private GameObject _instance;
        internal Instance(GameObject instance) => _instance = instance;
        public void Dispose()
        {
            if (_instance == null) return;
            GameObject instance = _instance; _instance = null;
            instance.SetActive(false); // Destroy is deferred; hide and stop at the exact range boundary.
            Object.Destroy(instance);
        }
    }
}
