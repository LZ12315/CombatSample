using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;
using JetBrains.Annotations;

[Serializable]
public class ActionHitBoxConfig
{
    public Vector3 center = Vector3.zero;
    public Quaternion rotation = Quaternion.identity;
    public float height = 0.5f;
    public float radius = 0.1f;
}

[Serializable]
public class AttackConfig
{
    [Header("配置")]
    public LayerMask targetLayers = 1 << 8;
    public float _baseDamage = 10f;

    [Header("调试")]
    public bool _debugMode = true;
}

public class ActionHitBoxAsset : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<Transform> boneTransform;
    public ActionHitBoxConfig hitboxConfig = new ActionHitBoxConfig();
    public AttackConfig attackConfig = new AttackConfig();

    [HideInInspector]
    public ActionHitBoxClip behavior;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionHitBoxClip>.Create(graph);
        behavior = playable.GetBehaviour();

        behavior.boneTransform = boneTransform.Resolve(graph.GetResolver());
        behavior.hitboxConfig = hitboxConfig;
        behavior.attackConfig = attackConfig;

        return playable;
    }
}

public class ActionHitBoxClip : ActionClipBase
{
    public Transform boneTransform;
    public ActionHitBoxConfig hitboxConfig;
    public AttackConfig attackConfig;

    public GameObject _hitboxObject;
    public CapsuleCollider _collider;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        base.OnBehaviourPlay(playable, info);
        CreateHitbox();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        base.OnBehaviourPause(playable, info);
        DestroyHitbox();
    }

    private void CreateHitbox()
    {
        if (boneTransform == null) return;

        _hitboxObject = new GameObject("HitBox");
        _hitboxObject.hideFlags = HideFlags.HideInHierarchy;

        // 添加Collider
        _collider = _hitboxObject.AddComponent<CapsuleCollider>();
        _collider.isTrigger = true;

        // 若在游戏中 添加Controller
        if(Application.isPlaying)
        {
            var hitbox = _collider.gameObject.AddComponent<AttackHitBox>();
            hitbox.Init(actor.combater, attackConfig);
        }

        // 添加辅助组件Updater
        var updater = _hitboxObject.AddComponent<HitBoxUpdater>();
        updater.Init(this);
    }

    public void UpdateHitbox()
    {
        if (_hitboxObject == null || boneTransform == null) return;

        // 更新位置和旋转
        _hitboxObject.transform.position = boneTransform.TransformPoint(hitboxConfig.center);
        _hitboxObject.transform.rotation = boneTransform.rotation * hitboxConfig.rotation;

        if (_collider == null) return;

        // 更新碰撞体参数
        _collider.height = hitboxConfig.height;
        _collider.radius = hitboxConfig.radius;

        // 设置胶囊方向（默认为Y轴）
        _collider.direction = 1; // 1 = Y轴
    }

    private void DestroyHitbox()
    {
        if (_hitboxObject != null)
        {
            // 先销毁辅助组件
            var updater = _hitboxObject.GetComponent<HitBoxUpdater>();
            if (updater != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(updater);
                else
                    UnityEngine.Object.DestroyImmediate(updater);
            }

            // 然后销毁游戏对象
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_hitboxObject);
            else
                UnityEngine.Object.DestroyImmediate(_hitboxObject);

            _hitboxObject = null;
            _collider = null;
        }
    }

    //辅助更新组件 在编辑模式下更新HitBox状态 否则其相对动画会慢一帧
    [ExecuteInEditMode]
    private class HitBoxUpdater : MonoBehaviour
    {
        private ActionHitBoxClip _clip;

        public void Init(ActionHitBoxClip clip)
        {
            _clip = clip;
        }

        private void Update()
        {
            UpdateInEditMode();
        }

        private void OnEnable()
        {
        #if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.update += UpdateInEditMode;
        #endif
        }

        private void OnDisable()
        {
        #if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.update -= UpdateInEditMode;
        #endif
        }

        private void UpdateInEditMode()
        {
            if (_clip != null && _clip.state == Enums.ActionClipState.Play)
            {
                _clip.UpdateHitbox();
            }
        }
    }
}

