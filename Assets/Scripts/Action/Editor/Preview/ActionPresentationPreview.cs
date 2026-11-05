#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Isolated, seekable common-ParticleSystem preview. Never runs Action runtimes, Impulse events or Prefab scripts.
/// Particle age is nominal 1x timeline elapsed time, not a replay of arbitrary gameplay speed/pause history.
/// </summary>
internal sealed class ActionPresentationPreview : IDisposable
{
    private readonly Dictionary<EffectItem, Effect> _effects = new Dictionary<EffectItem, Effect>();
    private readonly Dictionary<ScreenShakeItem, ActionScreenShakeRequest> _shakes = new Dictionary<ScreenShakeItem, ActionScreenShakeRequest>();
    private readonly Dictionary<EffectItem, string> _failures = new Dictionary<EffectItem, string>();
    private readonly HashSet<EffectItem> _active = new HashSet<EffectItem>();
    private readonly List<EffectItem> _remove = new List<EffectItem>();
    internal string Diagnostic { get; private set; }

    internal static string ValidatePrefab(GameObject prefab)
    {
        if (prefab == null) return "Assign a VFX Prefab.";
        if (!prefab.activeSelf) return "The VFX Prefab root is inactive; neither runtime autoplay nor particle preview will start it.";
        foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            if (!(component is Transform || component is ParticleSystem || component is Renderer || component is MeshFilter))
                return $"Particle preview skips '{prefab.name}': component '{(component == null ? "Missing Script" : component.GetType().Name)}' is outside the isolated particle-only scope.";
        if (prefab.GetComponentInChildren<ParticleSystem>(true) == null)
            return "Particle preview requires a ParticleSystem; scripted/Animator/Trail-only effects remain runtime-only.";
        if (!PrefabUtility.IsPartOfPrefabAsset(prefab)) return "Isolated preview requires a saved Prefab asset, not a scene template.";
        return string.Empty;
    }

    internal void Evaluate(ActionAsset action, double position, Transform root, Animator animator,
        PreviewRenderUtility preview, Action<double> samplePose)
    {
        Diagnostic = string.Empty; _active.Clear();
        bool resampled = false;
        try
        {
            foreach (GameplayLane lane in action.Timeline.GameplayLanes)
            {
                if (lane == null || lane.Muted) continue;
                foreach (GameplayItem item in lane.Items)
                {
                    if (!(item is EffectItem effect) || effect.Muted || position < effect.StartFrame || position >= effect.EndFrameExclusiveLong) continue;
                    EffectItem id = effect;
                    _active.Add(id);
                    if (_failures.TryGetValue(id, out string failure)) { Report(failure); continue; }
                    if (_effects.ContainsKey(id)) continue;
                    if (!ActionEffectRequest.TryCreate(effect.Config, out var request, out failure)) { Fail(id, failure); continue; }
                    failure = ValidatePrefab(request.Prefab);
                    if (!string.IsNullOrEmpty(failure)) { Fail(id, failure); continue; }
                    // World-left VFX must use the actual entry pose, even when seeking directly into the middle.
                    resampled = true; samplePose(effect.StartFrame);
                    if (!ActionEffectAnchorResolver.TryResolve(request, root, animator, out Transform anchor, out failure))
                    { Fail(id, failure); continue; }
                    try { _effects.Add(id, new Effect(request, anchor, root, preview, effect.EditorId ?? "Effect")); }
                    catch (Exception exception) { Fail(id, $"Particle preview: {exception.GetType().Name}: {exception.Message}"); }
                }
            }
        }
        finally { if (resampled) samplePose(position); }
        _remove.Clear();
        foreach (var pair in _effects)
        {
            if (!_active.Contains(pair.Key)) { _remove.Add(pair.Key); continue; }
            pair.Value.Evaluate((float)((position - pair.Key.StartFrame) / ActionTimelineData.FrameRate), preview.camera);
        }
        foreach (EffectItem id in _remove) { _effects[id].Dispose(); _effects.Remove(id); }
    }
    private void Report(string message) { if (string.IsNullOrEmpty(Diagnostic)) Diagnostic = message; }
    private void Fail(EffectItem id, string message) { _failures[id] = message; Report(message); }

    internal void DrawGuides(ActionAsset action, double position, Transform root, Camera camera)
    {
        foreach (Effect effect in _effects.Values) effect.DrawGuide();
        if (action?.Timeline == null) return;
        foreach (GameplayLane lane in action.Timeline.GameplayLanes)
        {
            if (lane == null || lane.Muted) continue;
            foreach (GameplayItem item in lane.Items)
            {
                if (!(item is ScreenShakeItem shake) || shake.Muted || position < shake.StartFrame || position >= shake.EndFrameExclusiveLong) continue;
                if (!_shakes.TryGetValue(shake, out var request))
                {
                    ActionScreenShakeRequest.TryCreate(shake.Config, out request);
                    _shakes.Add(shake, request);
                }
                if (request == null) continue;
                request.Sample(ActionScreenShakeRequest.Normalize(position, shake.StartFrame, shake.DurationFrames), out Vector3 offset, out Quaternion rotation);
                Vector3 origin = root.position + Vector3.up * 1.3f;
                Vector3 endpoint = origin + camera.transform.rotation * offset;
                Handles.color = new Color(1f, 0.8f, 0.2f);
                Handles.DrawLine(origin, endpoint);
                Handles.Label(origin, "Shake sample (guide only)");
                DrawAxes(endpoint, camera.transform.rotation * rotation);
            }
        }
    }
    private static void DrawAxes(Vector3 position, Quaternion rotation)
    {
        Handles.color = Color.red; Handles.DrawLine(position, position + rotation * Vector3.right * 0.15f);
        Handles.color = Color.green; Handles.DrawLine(position, position + rotation * Vector3.up * 0.15f);
        Handles.color = Color.blue; Handles.DrawLine(position, position + rotation * Vector3.forward * 0.15f);
    }
    public void Dispose()
    {
        foreach (Effect effect in _effects.Values) effect.Dispose();
        _effects.Clear(); _shakes.Clear(); _failures.Clear(); _active.Clear(); _remove.Clear(); Diagnostic = string.Empty;
    }

    private sealed class Effect : IDisposable
    {
        private readonly ActionEffectRequest _request;
        private readonly Transform _anchor, _root;
        private readonly GameObject _instance;
        private readonly ParticleSystem[] _roots;
        private readonly Pose _entryPose;
        private readonly Vector3 _entryScale, _prefabScale;
        private readonly Vector2 _angles;
        internal Effect(ActionEffectRequest request, Transform anchor, Transform root, PreviewRenderUtility preview, string id)
        {
            _request = request; _anchor = anchor; _root = root;
            var randomState = UnityEngine.Random.state;
            try { UnityEngine.Random.InitState(id.GetHashCode()); _angles = request.SampleAngles(); }
            finally { UnityEngine.Random.state = randomState; }
            request.TryResolveRotation(anchor.rotation, root.rotation, preview.camera.transform.rotation, _angles, out Quaternion rotation);
            _entryPose = new Pose(anchor.TransformPoint(request.LocalPosition), rotation);
            _prefabScale = request.Prefab.transform.localScale * request.Scale;
            _entryScale = Vector3.Scale(anchor.lossyScale, _prefabScale);
            _instance = preview.InstantiatePrefabInScene(request.Prefab);
            if (_instance == null) throw new InvalidOperationException("PreviewRenderUtility could not instantiate the VFX Prefab asset.");
            try
            {
                _instance.hideFlags = HideFlags.HideAndDontSave;
                VfxPlaybackUtility.Initialize(_instance, request.Occlusion, request.SimulationSpeed);
                var particles = _instance.GetComponentsInChildren<ParticleSystem>(true);
                var subEmitters = new HashSet<ParticleSystem>();
                foreach (var particle in particles)
                {
                    var sub = particle.subEmitters;
                    for (int i = 0; i < sub.subEmittersCount; i++) subEmitters.Add(sub.GetSubEmitterSystem(i));
                }
                var roots = new List<ParticleSystem>();
                for (int i = 0; i < particles.Length; i++)
                {
                    var particle = particles[i];
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particle.main;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.simulationSpeed = 1f; // Simulate receives the explicit speed-adjusted age below, exactly once.
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    particle.useAutoRandomSeed = false; particle.randomSeed = (uint)(17 + i * 31);
                    var parent = particle.transform.parent;
                    bool hasAutoplayAncestor = false;
                    if (parent != null)
                        foreach (var ancestor in parent.GetComponentsInParent<ParticleSystem>(true))
                            hasAutoplayAncestor |= ancestor.main.playOnAwake && !subEmitters.Contains(ancestor);
                    if (!hasAutoplayAncestor && !subEmitters.Contains(particle) && main.playOnAwake) roots.Add(particle);
                }
                _roots = roots.ToArray();
            }
            catch { UnityEngine.Object.DestroyImmediate(_instance); throw; }
        }
        internal void Evaluate(float elapsed, Camera camera)
        {
            _instance.transform.SetPositionAndRotation(_request.FollowAnchor ? _anchor.TransformPoint(_request.LocalPosition) : _entryPose.position,
                _entryPose.rotation);
            _instance.transform.localScale = _request.FollowAnchor ? Vector3.Scale(_anchor.lossyScale, _prefabScale) : _entryScale;
            if (_request.ContinuousOrientation && _request.TryResolveRotation(_anchor.rotation, _root.rotation, camera.transform.rotation, _angles, out Quaternion rotation))
                _instance.transform.rotation = rotation;
            foreach (ParticleSystem particle in _roots)
                if (particle.gameObject.activeInHierarchy) particle.Simulate(Mathf.Max(0f, elapsed * _request.SimulationSpeed), true, true, true);
        }
        internal void DrawGuide()
        {
            Handles.color = new Color(0.95f, 0.45f, 1f);
            Handles.DrawDottedLine(_anchor.position, _instance.transform.position, 3f);
            Handles.Label(_instance.transform.position, _request.Prefab.name);
            DrawAxes(_instance.transform.position, _instance.transform.rotation);
        }
        public void Dispose() { if (_instance != null) UnityEngine.Object.DestroyImmediate(_instance); }
    }
}
#endif
