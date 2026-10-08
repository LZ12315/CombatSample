using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Enables a rim-glow overlay on this actor's model while the actor owns a configured tag.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Combat/Actor Tag Glow Indicator")]
public sealed class ActorTagGlowIndicator : MonoBehaviour
{
    private const string DefaultModelRootPath = "ModelRoot";
    private const string DefaultShaderName = "CombatSample/Actor Rim Glow";
    private const float VisibleThreshold = 0.001f;

    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    [Header("Tag")]
    [SerializeField, Tooltip("Actor that owns the tag containers. Resolved from this GameObject when empty.")]
    private Actor actor;

    [SerializeField, Tooltip("Glow is active while this tag is present.")]
    private TagReference watchedTag;

    [SerializeField, Tooltip("Read Actor transient tags. Action tags usually live here.")]
    private bool checkTransientTags = true;

    [SerializeField, Tooltip("Read Actor persistent tags.")]
    private bool checkPersistentTags = true;

    [SerializeField, Tooltip("Exact matches only the selected leaf tag. Fuzzy allows parent tags to match children.")]
    private ActorTagMatchMode matchMode = ActorTagMatchMode.Exact;

    [Header("Renderers")]
    [SerializeField, Tooltip("Root used for automatic renderer collection. Defaults to Actor/ModelRoot, then Actor.")]
    private Transform modelRoot;

    [SerializeField, Tooltip("Optional explicit renderer list. When empty, renderers are collected from ModelRoot.")]
    private Renderer[] targetRenderers;

    [SerializeField, Tooltip("Include inactive child renderers during automatic collection.")]
    private bool includeInactiveRenderers;

    [Header("Glow")]
    [SerializeField, Tooltip("Optional overlay material. When empty, a runtime material is created from CombatSample/Actor Rim Glow.")]
    private Material overlayMaterial;

    [SerializeField, ColorUsage(true, true), Tooltip("HDR rim color.")]
    private Color glowColor = new Color(1f, 0.75f, 0.12f, 1f);

    [SerializeField, Min(0f), Tooltip("Color multiplier for the rim glow.")]
    private float intensity = 2.5f;

    [SerializeField, Range(0.5f, 8f), Tooltip("Higher values make the rim thinner.")]
    private float rimPower = 2.5f;

    [SerializeField, Range(0f, 1f), Tooltip("Final overlay alpha before additive blending.")]
    private float alpha = 0.8f;

    [SerializeField, Min(0f), Tooltip("Visibility change speed. Zero switches instantly.")]
    private float fadeSpeed = 16f;

    private readonly List<GlowBinding> _bindings = new List<GlowBinding>();

    private MaterialPropertyBlock _propertyBlock;
    private Material _runtimeMaterial;
    private float _visibility;
    private bool _isBuilt;

    private void Reset()
    {
        actor = ResolveActor();
        modelRoot = ResolveDefaultModelRoot();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        RebuildGlowRenderers();

        bool active = HasWatchedTag();
        _visibility = active ? 1f : 0f;
        ApplyGlow(_visibility);
    }

    private void LateUpdate()
    {
        if (!_isBuilt)
            RebuildGlowRenderers();

        float targetVisibility = HasWatchedTag() ? 1f : 0f;
        if (fadeSpeed <= 0f)
        {
            _visibility = targetVisibility;
        }
        else
        {
            _visibility = Mathf.MoveTowards(
                _visibility,
                targetVisibility,
                fadeSpeed * Time.deltaTime);
        }

        ApplyGlow(_visibility);
    }

    private void OnDisable()
    {
        ApplyGlow(0f);
        DestroyGlowRenderers();
    }

    private void OnDestroy()
    {
        DestroyGlowRenderers();
        DestroyRuntimeMaterial();
    }

    [ContextMenu("Rebuild Glow Renderers")]
    public void RebuildGlowRenderers()
    {
        DestroyGlowRenderers();
        ResolveReferences();

        if (!Application.isPlaying)
        {
            _isBuilt = true;
            return;
        }

        Material material = ResolveOverlayMaterial();
        if (material == null)
        {
            _isBuilt = true;
            return;
        }

        Renderer[] sources = GetSourceRenderers();
        for (int i = 0; i < sources.Length; i++)
        {
            Renderer source = sources[i];
            if (source == null || IsGlowOverlay(source))
                continue;

            Renderer overlay = CreateOverlayRenderer(source, material);
            if (overlay != null)
                _bindings.Add(new GlowBinding(source, overlay));
        }

        _isBuilt = true;
    }

    private void ResolveReferences()
    {
        if (actor == null)
            actor = ResolveActor();

        if (modelRoot == null)
            modelRoot = ResolveDefaultModelRoot();

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
    }

    private Transform ResolveDefaultModelRoot()
    {
        Transform searchRoot = actor != null ? actor.transform : transform;
        Transform root = searchRoot.Find(DefaultModelRootPath);
        return root != null ? root : transform;
    }

    private Actor ResolveActor()
    {
        Actor resolvedActor = GetComponent<Actor>();
        return resolvedActor != null ? resolvedActor : GetComponentInParent<Actor>();
    }

    private bool HasWatchedTag()
    {
        if (actor == null || watchedTag == null)
            return false;

        Tag tag = watchedTag.GetTag();
        if (tag == null)
            return false;

        bool hasTag = false;
        if (checkTransientTags)
            hasTag |= actor.HasTag(tag, ActorTagContainerType.Transient, matchMode);
        if (checkPersistentTags)
            hasTag |= actor.HasTag(tag, ActorTagContainerType.Persistent, matchMode);

        return hasTag;
    }

    private Renderer[] GetSourceRenderers()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
            return targetRenderers;

        Transform root = modelRoot != null ? modelRoot : transform;
        return root.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
    }

    private Material ResolveOverlayMaterial()
    {
        if (overlayMaterial != null)
            return overlayMaterial;

        if (_runtimeMaterial != null)
            return _runtimeMaterial;

        Shader shader = Shader.Find(DefaultShaderName);
        if (shader == null)
        {
            Debug.LogWarning(
                $"[{nameof(ActorTagGlowIndicator)}] Shader '{DefaultShaderName}' was not found.",
                this);
            return null;
        }

        _runtimeMaterial = new Material(shader)
        {
            name = "Runtime Actor Tag Glow",
            hideFlags = HideFlags.DontSave
        };
        return _runtimeMaterial;
    }

    private Renderer CreateOverlayRenderer(Renderer source, Material material)
    {
        if (source is SkinnedMeshRenderer skinnedSource)
            return CreateSkinnedOverlay(skinnedSource, material);

        if (source is MeshRenderer meshSource &&
            source.TryGetComponent(out MeshFilter sourceFilter) &&
            sourceFilter.sharedMesh != null)
        {
            return CreateMeshOverlay(meshSource, sourceFilter, material);
        }

        return null;
    }

    private static bool IsGlowOverlay(Renderer renderer)
    {
        return renderer.gameObject.name.EndsWith("_TagGlowOverlay");
    }

    private SkinnedMeshRenderer CreateSkinnedOverlay(SkinnedMeshRenderer source, Material material)
    {
        if (source.sharedMesh == null)
            return null;

        GameObject overlayObject = CreateOverlayObject(source);
        SkinnedMeshRenderer overlay = overlayObject.AddComponent<SkinnedMeshRenderer>();
        overlay.sharedMesh = source.sharedMesh;
        overlay.rootBone = source.rootBone;
        overlay.bones = source.bones;
        overlay.localBounds = source.localBounds;
        overlay.quality = source.quality;
        overlay.updateWhenOffscreen = source.updateWhenOffscreen;
        overlay.skinnedMotionVectors = false;

        CopyRendererSettings(source, overlay);
        overlay.sharedMaterials = BuildMaterialArray(source.sharedMaterials.Length, material);
        overlay.enabled = false;
        return overlay;
    }

    private MeshRenderer CreateMeshOverlay(MeshRenderer source, MeshFilter sourceFilter, Material material)
    {
        GameObject overlayObject = CreateOverlayObject(source);
        MeshFilter overlayFilter = overlayObject.AddComponent<MeshFilter>();
        overlayFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer overlay = overlayObject.AddComponent<MeshRenderer>();
        CopyRendererSettings(source, overlay);
        overlay.sharedMaterials = BuildMaterialArray(source.sharedMaterials.Length, material);
        overlay.enabled = false;
        return overlay;
    }

    private GameObject CreateOverlayObject(Renderer source)
    {
        GameObject overlayObject = new GameObject($"{source.name}_TagGlowOverlay");
        overlayObject.hideFlags = HideFlags.DontSave;
        overlayObject.layer = source.gameObject.layer;

        Transform overlayTransform = overlayObject.transform;
        overlayTransform.SetParent(source.transform, false);
        overlayTransform.localPosition = Vector3.zero;
        overlayTransform.localRotation = Quaternion.identity;
        overlayTransform.localScale = Vector3.one;

        return overlayObject;
    }

    private static void CopyRendererSettings(Renderer source, Renderer overlay)
    {
        overlay.shadowCastingMode = ShadowCastingMode.Off;
        overlay.receiveShadows = false;
        overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        overlay.lightProbeUsage = LightProbeUsage.Off;
        overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
        overlay.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder;
    }

    private static Material[] BuildMaterialArray(int sourceMaterialCount, Material material)
    {
        int count = Mathf.Max(1, sourceMaterialCount);
        Material[] materials = new Material[count];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = material;
        return materials;
    }

    private void ApplyGlow(float visibility)
    {
        bool visible = visibility > VisibleThreshold;
        for (int i = _bindings.Count - 1; i >= 0; i--)
        {
            GlowBinding binding = _bindings[i];
            if (binding.Source == null || binding.Overlay == null)
            {
                _bindings.RemoveAt(i);
                continue;
            }

            bool rendererVisible = visible &&
                                   binding.Source.enabled &&
                                   binding.Source.gameObject.activeInHierarchy;
            binding.Overlay.enabled = rendererVisible;
            if (!rendererVisible)
                continue;

            binding.Overlay.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(GlowColorId, glowColor);
            _propertyBlock.SetFloat(GlowIntensityId, intensity * visibility);
            _propertyBlock.SetFloat(RimPowerId, rimPower);
            _propertyBlock.SetFloat(AlphaId, alpha * visibility);
            binding.Overlay.SetPropertyBlock(_propertyBlock);
        }
    }

    private void DestroyGlowRenderers()
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            Renderer overlay = _bindings[i].Overlay;
            if (overlay != null)
                DestroyObject(overlay.gameObject);
        }

        _bindings.Clear();
        _isBuilt = false;
    }

    private void DestroyRuntimeMaterial()
    {
        if (_runtimeMaterial == null)
            return;

        DestroyObject(_runtimeMaterial);
        _runtimeMaterial = null;
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private readonly struct GlowBinding
    {
        public readonly Renderer Source;
        public readonly Renderer Overlay;

        public GlowBinding(Renderer source, Renderer overlay)
        {
            Source = source;
            Overlay = overlay;
        }
    }
}
