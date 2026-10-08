#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;

/// <summary>Visual evaluator for the shared Action editor time. It does not run gameplay.</summary>
[Serializable]
internal sealed class ActionPreviewPanel : IDisposable
{
    [NonSerialized] private ActionPreviewRenderer _renderer;
    [NonSerialized] private IMGUIContainer _view;
    [NonSerialized] private int _cameraControlId;
    [SerializeField] private Vector2 _orbit = new Vector2(135f, 15f);
    [SerializeField] private float _distance = 3f;
    [SerializeField] private Vector3 _cameraTarget = new Vector3(0f, 1f, 0f);
    [SerializeField] private bool _showSettings;
    private Vector2 _lastMouse;
    private bool _orbiting;
    private bool _panning;
    private bool _initialFramingRequested = true;
    private ActionAsset _observedAction;
    private GameObject _observedPreviewCharacter;

    internal IMGUIContainer CreateView()
    {
        CancelCameraInteraction();
        var view = new IMGUIContainer(Draw);
        view.AddToClassList("action-editor-preview-panel");
        view.RegisterCallback<DetachFromPanelEvent>(_ =>
        {
            if (_view == view) CancelCameraInteraction();
        });
        _view = view;
        return view;
    }

    public void Dispose()
    {
        CancelCameraInteraction();
        _renderer?.Dispose();
        _renderer = null;
        _view = null;
    }

    private void Repaint() => _view?.MarkDirtyRepaint();

    private void Draw()
    {
        if (_renderer == null) _renderer = new ActionPreviewRenderer();
        DrawPreviewSettings();
        UpdateInitialFramingRequest();

        Rect previewRect = GUILayoutUtility.GetRect(100f, 10000f, 160f, 10000f,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        HandleCameraInput(previewRect);
        if (Event.current.type == EventType.Repaint)
        {
            ActionEditorContext context = ActionEditorContext.Shared;
            var input = new ActionPreviewInput(context.CurrentAction, context.PreviewPosition,
                context.CurrentFrame, context.PreviewCharacter, context.SelectedEntries);
            _renderer.Evaluate(input, out bool instanceNeedsFraming);
            _renderer.Draw(previewRect, _orbit, ref _distance, ref _cameraTarget,
                _initialFramingRequested || instanceNeedsFraming);
            _initialFramingRequested = false;
        }

        if (!string.IsNullOrEmpty(_renderer.Diagnostic))
            EditorGUI.HelpBox(new Rect(previewRect.x + 8f, previewRect.y + 8f, previewRect.width - 16f, 42f),
                _renderer.Diagnostic, MessageType.Warning);
    }

    private void DrawPreviewSettings()
    {
        _showSettings = EditorGUILayout.Foldout(_showSettings, "Preview Settings", true);
        if (!_showSettings)
            return;

        ActionEditorContext context = ActionEditorContext.Shared;
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUI.BeginChangeCheck();
            GameObject character = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Character Override", "Optional override. Otherwise uses AnimationRigAsset.DefaultPreviewPrefab."),
                context.PreviewCharacter, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) context.SetPreviewCharacter(character);
        }
    }

    private void HandleCameraInput(Rect rect)
    {
        Event evt = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (!rect.Contains(evt.mousePosition) && !_orbiting && !_panning) return;
        if (evt.type == EventType.ScrollWheel && rect.Contains(evt.mousePosition))
        {
            _distance = Mathf.Clamp(_distance * Mathf.Exp(evt.delta.y * 0.04f), 0.01f, 10000f);
            evt.Use();
            Repaint();
        }
        else if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition) && (evt.button == 0 || evt.button == 2))
        {
            _cameraControlId = controlId;
            GUIUtility.hotControl = controlId;
            _orbiting = evt.button == 0 && !evt.shift;
            _panning = evt.button == 2 || evt.shift;
            _lastMouse = evt.mousePosition;
            evt.Use();
        }
        else if (evt.type == EventType.MouseDrag && (_orbiting || _panning))
        {
            Vector2 delta = evt.mousePosition - _lastMouse;
            _lastMouse = evt.mousePosition;
            if (_orbiting)
            {
                _orbit.x += delta.x * 0.35f;
                _orbit.y = Mathf.Clamp(_orbit.y - delta.y * 0.35f, -80f, 80f);
            }
            else
            {
                Quaternion rotation = Quaternion.Euler(_orbit.y, _orbit.x, 0f);
                _cameraTarget += (rotation * Vector3.left * delta.x + rotation * Vector3.up * delta.y)
                                 * (_distance * 0.0025f);
            }
            evt.Use();
            Repaint();
        }
        else if (evt.type == EventType.MouseUp && (_orbiting || _panning))
        {
            CancelCameraInteraction();
            evt.Use();
        }
    }

    internal void CancelCameraInteraction()
    {
        if (_cameraControlId != 0 && GUIUtility.hotControl == _cameraControlId)
            GUIUtility.hotControl = 0;
        _cameraControlId = 0;
        _orbiting = false;
        _panning = false;
    }

    private void UpdateInitialFramingRequest()
    {
        ActionEditorContext context = ActionEditorContext.Shared;
        if (_observedAction == context.CurrentAction && _observedPreviewCharacter == context.PreviewCharacter)
            return;
        _observedAction = context.CurrentAction;
        _observedPreviewCharacter = context.PreviewCharacter;
        _initialFramingRequested = true;
    }

    internal void OnContextChanged(ActionEditorChange change)
    {
        ActionEditorChangeFlags flags = change.Flags;
        if ((flags & ActionEditorChangeFlags.PreviewResources) != 0)
            _renderer?.InvalidateExternalData();
        if (RequiresDataInvalidation(flags))
            _renderer?.InvalidateData();
        if (RequiresRepaint(flags))
            Repaint();
    }

    internal static bool RequiresDataInvalidation(ActionEditorChangeFlags flags) =>
        (flags & (ActionEditorChangeFlags.Context | ActionEditorChangeFlags.Structure |
                  ActionEditorChangeFlags.Timing | ActionEditorChangeFlags.Content)) != 0;

    internal static bool RequiresRepaint(ActionEditorChangeFlags flags) =>
        (flags & (ActionEditorChangeFlags.Context | ActionEditorChangeFlags.Structure |
                  ActionEditorChangeFlags.Timing | ActionEditorChangeFlags.Content |
                  ActionEditorChangeFlags.Selection | ActionEditorChangeFlags.Frame |
                  ActionEditorChangeFlags.Preview | ActionEditorChangeFlags.PreviewPosition |
                  ActionEditorChangeFlags.PreviewCharacter |
                  ActionEditorChangeFlags.PreviewResources)) != 0;

    internal void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            CancelCameraInteraction();
            _renderer?.Dispose();
            _renderer = null;
        }
        Repaint();
    }
}

internal readonly struct ActionPreviewInput
{
    internal ActionPreviewInput(ActionAsset action, double position, int frame, GameObject characterOverride,
        IReadOnlyList<ActionSelectionValue> selection)
    {
        Action = action;
        Position = position;
        Frame = frame;
        CharacterOverride = characterOverride;
        Selection = selection;
    }

    internal ActionAsset Action { get; }
    internal double Position { get; }
    internal int Frame { get; }
    internal GameObject CharacterOverride { get; }
    internal IReadOnlyList<ActionSelectionValue> Selection { get; }

    internal bool IsSelected(string editorId)
    {
        for (int i = 0; Selection != null && i < Selection.Count; i++)
            if (string.Equals(Selection[i].EditorId, editorId, StringComparison.Ordinal)) return true;
        return false;
    }
}

internal sealed class ActionPreviewRenderer : IDisposable
{
    private const float GroundSize = 100f;
    private const string GroundShaderName = "Universal Render Pipeline/Unlit";
    // Tuned inputs for the camera background, ambient light and ground material respectively.
    // They pass through different rendering paths; do not apply a blanket color-space conversion.
    private static readonly Color BackgroundColor = new Color(49f / 255f, 77f / 255f, 121f / 255f, 1f);
    private static readonly Color AmbientColor = new Color(0.40f, 0.41f, 0.43f, 1f);
    private static readonly Color GroundColor = new Color(0.14f, 0.15f, 0.17f, 1f);
    private readonly List<ActionRuntimeAnimationRecord> _animationRecords = new List<ActionRuntimeAnimationRecord>();
    private readonly List<PreviewHitBox> _hitBoxes = new List<PreviewHitBox>();
    private readonly Dictionary<int, Mesh> _bakedPoseMeshes = new Dictionary<int, Mesh>();
    private readonly Vector3[] _markerLine = new Vector3[2];
    private readonly ActionPreviewSpatialEvaluator _spatial = new ActionPreviewSpatialEvaluator();
    private Vector3[] _pathPoints = Array.Empty<Vector3>();
    private PreviewRenderUtility _preview;
    private GameObject _sourcePrefab;
    private string _sourceGuid;
    private Hash128 _sourceDependencyHash;
    private bool _instanceInvalidated;
    private GameObject _instance;
    private GameObject _groundObject;
    private Mesh _groundMesh;
    private Material _groundMaterial;
    private Animator _animator;
    private ActionPreviewPoseSampler _poseSampler;
    private ActionAsset _snapshotAction;
    private Vector3 _baselineRootPosition;
    private Quaternion _baselineRootRotation;
    private float _characterRadius = 1f;
    private int _visiblePathBoundary;
    private bool _showInterpolatedPathEndpoint;
    private Vector3 _interpolatedPathEndpoint;
    private bool _evaluationInvalidated = true;
    private bool _hasEvaluatedInput;
    private ActionAsset _evaluatedAction;
    private double _evaluatedPosition;
    private int _evaluatedFrame;
    private int _evaluatedSelectionHash;
    private string _evaluationDiagnostic = string.Empty;
    private string _hitBoxDiagnostic = string.Empty;

    internal string Diagnostic { get; private set; }

    internal void InvalidateData()
    {
        _snapshotAction = null;
        _spatial.Invalidate();
        _poseSampler?.Invalidate();
        _evaluationInvalidated = true;
    }

    internal void InvalidateExternalData()
    {
        InvalidateData();
        if (_sourcePrefab == null) return;
        string path = AssetDatabase.GetAssetPath(_sourcePrefab);
        if (!string.IsNullOrEmpty(path) &&
            AssetDatabase.GetAssetDependencyHash(path) != _sourceDependencyHash)
            _instanceInvalidated = true;
    }

    internal bool Evaluate(ActionPreviewInput input, out bool needsInitialFraming)
    {
        needsInitialFraming = false;
        Diagnostic = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Diagnostic = "Preview resources are released while entering or running Play Mode.";
            return false;
        }

        ActionAsset action = input.Action;
        if (action == null)
        {
            DisposePreviewObjects();
            Diagnostic = "Select an Action in Action Timeline.";
            return false;
        }
        if (action.Timeline == null)
        {
            DisposePreviewObjects();
            Diagnostic = "The current Timeline Action has no ActionRuntime timeline data.";
            return false;
        }

        GameObject source = ActionPreviewCharacterResolver.Resolve(action, input.CharacterOverride);
        if (source == null)
        {
            DisposePreviewObjects();
            Diagnostic = "Set AnimationRigAsset.DefaultPreviewPrefab, or choose a Character Override in Preview Settings.";
            return false;
        }
        if (!EnsureInstance(source, out needsInitialFraming))
            return false;

        EnsureAnimationSnapshot(action);
        int selectionHash = CalculateSelectionHash(input.Selection);
        bool poseChanged = _evaluationInvalidated || !_hasEvaluatedInput ||
                           _evaluatedAction != action || Math.Abs(_evaluatedPosition - input.Position) > 1e-9d;
        bool overlaysChanged = poseChanged || _evaluatedFrame != input.Frame ||
                               _evaluatedSelectionHash != selectionHash;
        if (poseChanged)
            EvaluatePoseAndPosition(action, input.Position);
        if (overlaysChanged)
            BuildActiveHitBoxes(action, input);

        _evaluationInvalidated = false;
        _hasEvaluatedInput = true;
        _evaluatedAction = action;
        _evaluatedPosition = input.Position;
        _evaluatedFrame = input.Frame;
        _evaluatedSelectionHash = selectionHash;
        Diagnostic = CombineDiagnostics(_evaluationDiagnostic, _hitBoxDiagnostic);
        return true;
    }

    internal void Draw(Rect rect, Vector2 orbit, ref float distance,
        ref Vector3 cameraTarget, bool initialFramingRequested)
    {
        if (_preview == null || _instance == null || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            return;
        }

        if (initialFramingRequested &&
            !TryApplyInitialFraming(rect, orbit, ref distance, ref cameraTarget, out string framingMessage))
            Diagnostic = framingMessage;
        ConfigureCamera(orbit, distance, cameraTarget);

        float supersampleScale = ActionPreviewCameraUtility.CalculateSupersampleScale(
            rect.size, EditorGUIUtility.pixelsPerPoint, SystemInfo.maxTextureSize, 16000000L, 2f);
        var renderRect = new Rect(0f, 0f, rect.width * supersampleScale, rect.height * supersampleScale);
        bool previewOpen = false;
        Texture previewTexture = null;
        try
        {
            _preview.BeginPreview(renderRect, GUIStyle.none);
            previewOpen = true;
            _preview.Render(true, false);
            // Draw into the same color/depth target before EndPreview restores the window target.
            // Drawing after GUI.DrawTexture would update a texture that has already been presented.
            DrawOverlays();
        }
        finally
        {
            if (previewOpen) previewTexture = _preview.EndPreview();
        }
        if (previewTexture != null)
        {
            previewTexture.filterMode = FilterMode.Bilinear;
            GUI.DrawTexture(rect, previewTexture, ScaleMode.StretchToFill, false);
        }
    }

    private void DrawOverlays()
    {
        if (_preview == null || _instance == null || Event.current.type != EventType.Repaint) return;
        CompareFunction previousZTest = Handles.zTest;
        Color previousColor = Handles.color;
        Matrix4x4 previousMatrix = Handles.matrix;
        Camera previousCamera = Camera.current;
        RenderTexture previousTarget = RenderTexture.active;
        GL.PushMatrix();
        try
        {
            RenderTexture.active = _preview.camera.targetTexture;
            // Keep the full supersampled camera viewport, rather than using the window GUI rect.
            Handles.SetCamera(_preview.camera);
            Handles.zTest = CompareFunction.LessEqual;
            using (new Handles.DrawingScope(Matrix4x4.identity))
            {
                DrawOriginMarker();
                IReadOnlyList<Vector3> path = _spatial.Boundaries;
                int boundaryCount = Mathf.Min(path.Count, _visiblePathBoundary + 1);
                int pointCount = boundaryCount + (_showInterpolatedPathEndpoint ? 1 : 0);
                if (pointCount > 1)
                {
                    if (_pathPoints.Length < pointCount)
                        _pathPoints = new Vector3[Mathf.Max(pointCount, Mathf.Max(64, _pathPoints.Length * 2))];
                    for (int i = 0; i < boundaryCount; i++)
                        _pathPoints[i] = _baselineRootPosition + _baselineRootRotation * path[i];
                    if (_showInterpolatedPathEndpoint)
                        _pathPoints[boundaryCount] = _baselineRootPosition +
                            _baselineRootRotation * _interpolatedPathEndpoint;
                    Handles.color = new Color(0.2f, 0.85f, 1f, 0.9f);
                    Handles.DrawAAPolyLine(3f, pointCount, _pathPoints);
                }
                foreach (PreviewHitBox hitBox in _hitBoxes)
                {
                    Handles.color = hitBox.Selected
                        ? new Color(1f, 0.85f, 0.15f, 1f)
                        : new Color(1f, 0.25f, 0.15f, 0.95f);
                    DrawWireHitBox(hitBox.Shape);
                }
            }
        }
        finally
        {
            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
            Handles.zTest = previousZTest;
            Camera.SetupCurrent(previousCamera);
            RenderTexture.active = previousTarget;
            GL.PopMatrix();
        }
    }

    public void Dispose()
    {
        DisposePreviewObjects();
        _spatial.Invalidate();
    }

    private bool EnsureInstance(GameObject source, out bool needsInitialFraming)
    {
        needsInitialFraming = false;
        if (_preview != null && _instance != null && _sourcePrefab == source && !_instanceInvalidated) return true;
        string sourcePath = AssetDatabase.GetAssetPath(source);
        string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
        bool frameCharacter = _preview == null || _instance == null ||
                              (_sourcePrefab != source && (string.IsNullOrEmpty(sourceGuid) || _sourceGuid != sourceGuid));
        DisposePreviewObjects();
        Animator[] sourceAnimators = source.GetComponentsInChildren<Animator>(true);
        if (sourceAnimators.Length != 1)
        {
            Diagnostic = $"Preview prefab must contain exactly one Animator; found {sourceAnimators.Length}.";
            return false;
        }
        MonoBehaviour[] behaviours = source.GetComponentsInChildren<MonoBehaviour>(true);
        if (behaviours.Length > 0)
        {
            Diagnostic = $"Preview prefab must be presentation-only and cannot contain MonoBehaviour '{behaviours[0]?.GetType().Name ?? "Missing Script"}'.";
            return false;
        }

        try
        {
            _preview = new PreviewRenderUtility();
            _preview.cameraFieldOfView = 30f;
            _preview.camera.allowHDR = false;
            _preview.camera.allowMSAA = false;
            _preview.camera.clearFlags = CameraClearFlags.Color;
            _preview.camera.backgroundColor = BackgroundColor;
            _preview.ambientColor = AmbientColor;
            _preview.lights[0].color = Color.white;
            _preview.lights[0].intensity = 1.15f;
            _preview.lights[1].color = Color.white;
            _preview.lights[1].intensity = 0.65f;
            _instance = _preview.InstantiatePrefabInScene(source);
            _sourcePrefab = source;
            _sourceGuid = sourceGuid;
            _sourceDependencyHash = AssetDatabase.GetAssetDependencyHash(sourcePath);
            _animator = _instance.GetComponentInChildren<Animator>(true);
            _animator.runtimeAnimatorController = null;
            _animator.applyRootMotion = false;
            _animator.fireEvents = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.enabled = true;
            _instance.SetActive(true);
            _poseSampler = new ActionPreviewPoseSampler(_instance, _animator);
            _baselineRootPosition = _poseSampler.RootPosition;
            _baselineRootRotation = _poseSampler.RootRotation;
            CreateGround();
            InvalidateData();
            needsInitialFraming = frameCharacter;
            return true;
        }
        catch (Exception exception)
        {
            Diagnostic = $"Preview character could not be created: {exception.GetType().Name}: {exception.Message}";
            DisposePreviewObjects();
            return false;
        }
    }

    private void EnsureAnimationSnapshot(ActionAsset action)
    {
        if (ReferenceEquals(_snapshotAction, action)) return;
        _snapshotAction = action;
        _animationRecords.Clear();
        IReadOnlyList<AnimationSegment> segments = action?.Timeline?.AnimationSegments;
        for (int i = 0; segments != null && i < segments.Count; i++)
            if (segments[i] != null) _animationRecords.Add(new ActionRuntimeAnimationRecord(segments[i]));
    }

    private void EvaluatePoseAndPosition(ActionAsset action, double position)
    {
        _evaluationDiagnostic = string.Empty;
        if (ActionAnimationSampleResolver.TryResolve(_animationRecords, position, out ActionAnimationSample sample))
        {
            _poseSampler.Sample(sample.Clip, sample.SourceTime);
        }
        else
        {
            _poseSampler.Sample(null, 0d);
        }

        Vector3 rootPosition = _spatial.Evaluate(action, position);
        _visiblePathBoundary = Mathf.Clamp(Mathf.FloorToInt((float)position) + 1, 0,
            action?.Timeline?.DurationFrames ?? 0);
        _showInterpolatedPathEndpoint = position < (action?.Timeline?.DurationFrames ?? 0) &&
            position - Math.Floor(position) > 0.000001d;
        _interpolatedPathEndpoint = rootPosition;
        _instance.transform.position = _baselineRootPosition + _baselineRootRotation * rootPosition;
        _instance.transform.rotation = _baselineRootRotation;
        if (!string.IsNullOrEmpty(_spatial.Diagnostic)) _evaluationDiagnostic = _spatial.Diagnostic;
    }

    private static int CalculateSelectionHash(IReadOnlyList<ActionSelectionValue> selection)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; selection != null && i < selection.Count; i++)
            {
                hash = hash * 31 + (int)selection[i].Kind;
                hash = hash * 31 + (selection[i].EditorId?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }

    private void BuildActiveHitBoxes(ActionAsset action, ActionPreviewInput input)
    {
        _hitBoxes.Clear();
        _hitBoxDiagnostic = string.Empty;
        if (action?.Timeline == null || input.Position >= action.Timeline.DurationFrames)
            return;
        int frame = input.Frame;
        IReadOnlyList<GameplayLane> lanes = action?.Timeline?.GameplayLanes;
        for (int laneIndex = 0; lanes != null && laneIndex < lanes.Count; laneIndex++)
        {
            GameplayLane lane = lanes[laneIndex];
            if (lane == null || lane.Muted) continue;
            foreach (GameplayItem item in lane.Items)
            {
                if (!(item is HitBoxItem hitBox) || hitBox.Muted || hitBox.Config?.hitboxConfig == null
                                                    || frame < hitBox.StartFrame || frame >= hitBox.EndFrameExclusiveLong)
                    continue;
                if (!ActionHitBoxAnchorResolver.TryResolve(
                        hitBox.Config.anchor,
                        _instance.transform,
                        _animator,
                        out Transform binding,
                        out string bindingFailure))
                {
                    AppendHitBoxDiagnostic($"HitBox '{hitBox.EditorId}' anchor could not be resolved: {bindingFailure}");
                    continue;
                }
                if (!ActionHitBoxGeometry.TryBuild(
                        binding, hitBox.Config.hitboxConfig, out ActionHitBoxWorldShape shape, out string failureReason))
                {
                    AppendHitBoxDiagnostic($"HitBox '{hitBox.EditorId}' is invalid: {failureReason}");
                    continue;
                }
                _hitBoxes.Add(new PreviewHitBox(shape, input.IsSelected(hitBox.EditorId)));
            }
        }
    }

    private void AppendHitBoxDiagnostic(string message)
    {
        if (string.IsNullOrEmpty(message) || !string.IsNullOrEmpty(_hitBoxDiagnostic))
            return;
        _hitBoxDiagnostic = message;
    }

    private static string CombineDiagnostics(string first, string second)
    {
        if (string.IsNullOrEmpty(first)) return second ?? string.Empty;
        if (string.IsNullOrEmpty(second)) return first;
        return first + "\n" + second;
    }

    private void ConfigureCamera(Vector2 orbit, float distance, Vector3 target)
    {
        Quaternion rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
        distance = Mathf.Max(0.01f, distance);
        _preview.camera.transform.position = target - rotation * Vector3.forward * distance;
        _preview.camera.transform.rotation = rotation;
        _preview.camera.nearClipPlane = Mathf.Max(0.001f, Mathf.Min(distance * 0.02f, _characterRadius * 0.01f));
        _preview.camera.farClipPlane = Mathf.Max(distance + GroundSize * 1.5f, distance + _characterRadius * 4f);
        _preview.lights[0].transform.rotation = rotation * Quaternion.Euler(35f, 35f, 0f);
        _preview.lights[1].transform.rotation = rotation * Quaternion.Euler(340f, 218f, 177f);
    }

    private bool TryApplyInitialFraming(
        Rect previewRect,
        Vector2 orbit,
        ref float distance,
        ref Vector3 cameraTarget,
        out string message)
    {
        message = string.Empty;
        if (!TryCalculateCharacterBounds(out Bounds bounds))
        {
            message = "The preview character has no visible mesh for initial framing.";
            return false;
        }

        Quaternion rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
        float aspect = previewRect.height > 0f ? previewRect.width / previewRect.height : 1f;
        if (!ActionPreviewCameraUtility.TryCalculateInitialFraming(
                bounds, rotation, _preview.cameraFieldOfView, aspect, 0.42f, out cameraTarget, out distance))
        {
            message = "The preview character bounds are invalid.";
            return false;
        }

        _characterRadius = Mathf.Max(0.001f, bounds.extents.magnitude);
        return true;
    }

    private bool TryCalculateCharacterBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        Renderer[] renderers = _instance.GetComponentsInChildren<Renderer>(false);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                !renderer.gameObject.activeInHierarchy ||
                (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)))
                continue;

            Bounds rendererBounds;
            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                int rendererId = skinnedRenderer.GetInstanceID();
                if (!_bakedPoseMeshes.TryGetValue(rendererId, out Mesh bakedMesh) || bakedMesh == null)
                {
                    bakedMesh = new Mesh
                    {
                        name = $"Action Preview Pose Bounds: {skinnedRenderer.name}",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    _bakedPoseMeshes[rendererId] = bakedMesh;
                }
                bakedMesh.Clear(false);
                skinnedRenderer.BakeMesh(bakedMesh, false);
                rendererBounds = ActionPreviewCameraUtility.TransformBounds(
                    bakedMesh.bounds, skinnedRenderer.transform.localToWorldMatrix);
            }
            else
            {
                rendererBounds = renderer.bounds;
            }

            if (!ActionPreviewCameraUtility.IsFinite(rendererBounds) || rendererBounds.extents.sqrMagnitude <= 1e-10f)
                continue;
            if (hasBounds) bounds.Encapsulate(rendererBounds);
            else
            {
                bounds = rendererBounds;
                hasBounds = true;
            }
        }
        return hasBounds;
    }

    private void CreateGround()
    {
        Shader shader = Shader.Find(GroundShaderName);
        if (shader == null)
            throw new InvalidOperationException($"Required preview shader '{GroundShaderName}' was not found.");

        float halfSize = GroundSize * 0.5f;
        _groundMesh = new Mesh
        {
            name = "Action Preview Ground Mesh",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = new[]
            {
                new Vector3(-halfSize, 0f, -halfSize),
                new Vector3(-halfSize, 0f, halfSize),
                new Vector3(halfSize, 0f, halfSize),
                new Vector3(halfSize, 0f, -halfSize),
            },
            normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
            triangles = new[] { 0, 1, 2, 0, 2, 3 },
        };
        _groundMesh.RecalculateBounds();

        _groundMaterial = new Material(shader)
        {
            name = "Action Preview Ground Material",
            hideFlags = HideFlags.HideAndDontSave,
        };
        _groundMaterial.SetColor("_BaseColor", GroundColor);

        _groundObject = new GameObject("Action Preview Ground", typeof(MeshFilter), typeof(MeshRenderer))
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        _groundObject.transform.position = _baselineRootPosition;
        _groundObject.GetComponent<MeshFilter>().sharedMesh = _groundMesh;
        MeshRenderer renderer = _groundObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = _groundMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _preview.AddSingleGO(_groundObject);
    }

    private void DrawOriginMarker()
    {
        Vector3 origin = _baselineRootPosition + Vector3.up * 0.01f;
        const float crossRadius = 0.25f;
        Handles.color = new Color(1f, 0.7f, 0.12f, 1f);
        DrawMarkerLine(origin - Vector3.right * crossRadius, origin + Vector3.right * crossRadius);
        DrawMarkerLine(origin - Vector3.forward * crossRadius, origin + Vector3.forward * crossRadius);

        Vector3 facing = Vector3.ProjectOnPlane(_baselineRootRotation * Vector3.forward, Vector3.up).normalized;
        if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;
        Vector3 tip = origin + facing * 0.8f;
        Vector3 side = Vector3.Cross(Vector3.up, facing) * 0.16f;
        DrawMarkerLine(origin, tip);
        DrawMarkerLine(tip, tip - facing * 0.22f + side);
        DrawMarkerLine(tip, tip - facing * 0.22f - side);
    }

    private void DrawMarkerLine(Vector3 from, Vector3 to)
    {
        _markerLine[0] = from;
        _markerLine[1] = to;
        Handles.DrawAAPolyLine(3f, _markerLine);
    }

    private static void DrawWireHitBox(ActionHitBoxWorldShape shape)
    {
        switch (shape.Shape)
        {
            case ActionHitBoxShape.Box:
                using (new Handles.DrawingScope(Handles.color, Matrix4x4.TRS(shape.Center, shape.Rotation, Vector3.one)))
                    Handles.DrawWireCube(Vector3.zero, shape.HalfExtents * 2f);
                break;
            case ActionHitBoxShape.Sphere:
                DrawWireSphere(shape.Center, shape.Radius);
                break;
            case ActionHitBoxShape.Capsule:
                DrawWireCapsule(shape);
                break;
        }
    }

    private static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }

    private static void DrawWireCapsule(ActionHitBoxWorldShape capsule)
    {
        Vector3 axis = capsule.Rotation * Vector3.up;
        Vector3 right = capsule.Rotation * Vector3.right * capsule.Radius;
        Vector3 forward = capsule.Rotation * Vector3.forward * capsule.Radius;
        Handles.DrawWireDisc(capsule.PointA, axis, capsule.Radius);
        Handles.DrawWireDisc(capsule.PointB, axis, capsule.Radius);
        Handles.DrawLine(capsule.PointA + right, capsule.PointB + right);
        Handles.DrawLine(capsule.PointA - right, capsule.PointB - right);
        Handles.DrawLine(capsule.PointA + forward, capsule.PointB + forward);
        Handles.DrawLine(capsule.PointA - forward, capsule.PointB - forward);
        Handles.DrawWireArc(capsule.PointA, forward.normalized, right.normalized, 180f, capsule.Radius);
        Handles.DrawWireArc(capsule.PointB, forward.normalized, right.normalized, -180f, capsule.Radius);
        Handles.DrawWireArc(capsule.PointA, right.normalized, forward.normalized, -180f, capsule.Radius);
        Handles.DrawWireArc(capsule.PointB, right.normalized, forward.normalized, 180f, capsule.Radius);
    }

    private void DisposePreviewObjects()
    {
        _poseSampler?.Dispose();
        _poseSampler = null;
        if (_preview != null) _preview.Cleanup();
        if (_groundObject != null) UnityEngine.Object.DestroyImmediate(_groundObject);
        if (_groundMaterial != null) UnityEngine.Object.DestroyImmediate(_groundMaterial);
        if (_groundMesh != null) UnityEngine.Object.DestroyImmediate(_groundMesh);
        foreach (Mesh bakedMesh in _bakedPoseMeshes.Values)
            if (bakedMesh != null) UnityEngine.Object.DestroyImmediate(bakedMesh);
        _bakedPoseMeshes.Clear();
        _preview = null;
        _instance = null;
        _groundObject = null;
        _groundMaterial = null;
        _groundMesh = null;
        _animator = null;
        _sourcePrefab = null;
        _sourceGuid = null;
        _sourceDependencyHash = default;
        _instanceInvalidated = false;
        _snapshotAction = null;
        _animationRecords.Clear();
        _hitBoxes.Clear();
        _pathPoints = Array.Empty<Vector3>();
        _visiblePathBoundary = 0;
        _showInterpolatedPathEndpoint = false;
        _evaluationInvalidated = true;
        _hasEvaluatedInput = false;
        _evaluatedAction = null;
        _evaluatedPosition = 0d;
        _evaluatedFrame = 0;
        _evaluatedSelectionHash = 0;
        _evaluationDiagnostic = string.Empty;
        _hitBoxDiagnostic = string.Empty;
    }

    private readonly struct PreviewHitBox
    {
        internal PreviewHitBox(ActionHitBoxWorldShape shape, bool selected)
        {
            Shape = shape;
            Selected = selected;
        }
        internal ActionHitBoxWorldShape Shape { get; }
        internal bool Selected { get; }
    }
}

internal static class ActionPreviewCameraUtility
{
    internal static bool TryCalculateInitialFraming(
        Bounds bounds,
        Quaternion cameraRotation,
        float verticalFieldOfView,
        float aspect,
        float viewportFill,
        out Vector3 target,
        out float distance)
    {
        target = bounds.center;
        distance = 0f;
        if (!IsFinite(bounds) || bounds.extents.sqrMagnitude <= 1e-10f)
            return false;

        verticalFieldOfView = Mathf.Clamp(verticalFieldOfView, 1f, 179f);
        aspect = Mathf.Max(0.01f, aspect);
        viewportFill = Mathf.Clamp(viewportFill, 0.05f, 0.95f);
        float verticalTangent = Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
        float horizontalTangent = verticalTangent * aspect;
        Vector3 extents = bounds.extents;
        Vector3 right = cameraRotation * Vector3.right;
        Vector3 up = cameraRotation * Vector3.up;
        Vector3 forward = cameraRotation * Vector3.forward;
        float projectedHalfWidth = AbsDot(right, extents);
        float projectedHalfHeight = AbsDot(up, extents);
        float projectedHalfDepth = AbsDot(forward, extents);
        float horizontalDistance = projectedHalfWidth / (horizontalTangent * viewportFill);
        float verticalDistance = projectedHalfHeight / (verticalTangent * viewportFill);
        distance = projectedHalfDepth + Mathf.Max(horizontalDistance, verticalDistance);
        distance = Mathf.Max(distance, Mathf.Max(0.01f, extents.magnitude * 0.05f));
        return !float.IsNaN(distance) && !float.IsInfinity(distance);
    }

    internal static float CalculateSupersampleScale(
        Vector2 guiSize,
        float pixelsPerPoint,
        int maxTextureSize,
        long maxPixelCount,
        float desiredScale)
    {
        double width = Math.Max(1d, guiSize.x * Math.Max(0.01f, pixelsPerPoint));
        double height = Math.Max(1d, guiSize.y * Math.Max(0.01f, pixelsPerPoint));
        double dimensionLimit = Math.Min(
            Math.Max(1, maxTextureSize) / width,
            Math.Max(1, maxTextureSize) / height);
        double pixelLimit = Math.Sqrt(Math.Max(1L, maxPixelCount) / (width * height));
        double scale = Math.Min(Math.Max(0.01f, desiredScale), Math.Min(dimensionLimit, pixelLimit));
        return (float)Math.Max((double)float.Epsilon, scale);
    }

    internal static Bounds TransformBounds(Bounds localBounds, Matrix4x4 localToWorld)
    {
        Vector3 localExtents = localBounds.extents;
        Vector3 axisX = localToWorld.MultiplyVector(new Vector3(localExtents.x, 0f, 0f));
        Vector3 axisY = localToWorld.MultiplyVector(new Vector3(0f, localExtents.y, 0f));
        Vector3 axisZ = localToWorld.MultiplyVector(new Vector3(0f, 0f, localExtents.z));
        Vector3 worldExtents = Abs(axisX) + Abs(axisY) + Abs(axisZ);
        return new Bounds(localToWorld.MultiplyPoint3x4(localBounds.center), worldExtents * 2f);
    }

    internal static bool IsFinite(Bounds bounds)
    {
        return IsFinite(bounds.center) && IsFinite(bounds.extents);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float AbsDot(Vector3 axis, Vector3 extents)
    {
        axis = Abs(axis);
        return Vector3.Dot(axis, extents);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
#endif
