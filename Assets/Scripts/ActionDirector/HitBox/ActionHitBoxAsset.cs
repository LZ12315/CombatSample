using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;

[Serializable]
public class ActionHitBoxConfig
{
    public Vector3 center = Vector3.zero;
    public Quaternion rotation = Quaternion.identity;
    public float height = 0;
    public float radius = 0;
}

[Serializable]
public class ActionHitBoxAsset : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<Transform> boneTransform;
    public ActionHitBoxConfig config = new ActionHitBoxConfig();

    [HideInInspector]
    public ActionHitBoxClip behavior;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionHitBoxClip>.Create(graph);
        behavior = playable.GetBehaviour();

        behavior.boneTransform = boneTransform.Resolve(graph.GetResolver());
        behavior.config = config;

        return playable;
    }
}

public class ActionHitBoxClip : ActionClipBase
{
    public Transform boneTransform;
    public ActionHitBoxConfig config;

    public GameObject _hitboxObject;
    public CapsuleCollider hitbox;

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
        hitbox = _hitboxObject.AddComponent<CapsuleCollider>();
        hitbox.isTrigger = true;

        // 添加更新组件
        var updater = _hitboxObject.AddComponent<HitBoxUpdater>();
        updater.Init(this);
    }

    public void UpdateHitbox()
    {
        if (_hitboxObject == null || boneTransform == null) return;

        // 更新位置和旋转
        _hitboxObject.transform.position = boneTransform.TransformPoint(config.center);
        _hitboxObject.transform.rotation = boneTransform.rotation * config.rotation;

        if (hitbox == null) return;

        // 更新碰撞体参数
        hitbox.height = config.height;
        hitbox.radius = config.radius;

        // 设置胶囊方向（默认为Y轴）
        hitbox.direction = 1; // 1 = Y轴
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
            hitbox = null;
        }
    }

    // 辅助更新组件 用此组件更新HitBox状态 否则相对动画会有一帧偏移
    [ExecuteInEditMode]
    private class HitBoxUpdater : MonoBehaviour
    {
        private ActionHitBoxClip _clip;

        public void Init(ActionHitBoxClip clip)
        {
            _clip = clip;
        }

        private void OnEnable()
        {
        #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update += UpdateInEditMode;
            }
        #endif
        }

        private void OnDisable()
        {
        #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= UpdateInEditMode;
            }
        #endif
        }

        private void UpdateInEditMode()
        {
            if (_clip != null && _clip.isPlaying)
            {
                _clip.UpdateHitbox();
            }
        }
    }
}

