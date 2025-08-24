using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class ActionHitBoxConfig
{
    public Vector3 center = Vector3.zero;
    public Quaternion rotation = Quaternion.identity;
    public float height = 0;
    public float radius = 0;

    // 计算世界空间矩阵
    public Matrix4x4 GetWorldMatrix(Transform bone)
    {
        return Matrix4x4.TRS(
            bone.TransformPoint(center),
            bone.rotation * rotation,
            Vector3.one
        );
    }
}

[Serializable]
public class ActionHitBoxAsset : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<Transform> boneTransform;
    public ActionHitBoxConfig config = new ActionHitBoxConfig();

    [HideInInspector]
    public CapsuleCollider hitbox;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ActionHitBoxClip>.Create(graph);
        var behavior = playable.GetBehaviour();

        behavior.boneTransform = boneTransform.Resolve(graph.GetResolver());
        behavior.config = config;
        behavior.asset = this; // 用于编辑器引用

        return playable;
    }
}

public class ActionHitBoxClip : ActionClipBase
{
    public Transform boneTransform;
    public ActionHitBoxConfig config;
    public ActionHitBoxAsset asset; // 用于保存数据回写到asset

    private GameObject _hitboxObject;
    private CapsuleCollider _collider;

    protected override void OnClipPlay ()
    {
        base.OnClipPlay();
        CreateHitbox();
    }

    protected override void OnClipPause()
    {
        base.OnClipPause();
        DestroyHitbox();
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        UpdateHitbox();
    }

    private void CreateHitbox()
    {
        if (boneTransform == null) return;

        _hitboxObject = new GameObject("HitBox");
        _hitboxObject.hideFlags = HideFlags.HideInHierarchy;
        _collider = _hitboxObject.AddComponent<CapsuleCollider>();
        _collider.isTrigger = true;
        asset.hitbox = _collider;

        UpdateHitbox();
    }

    private void UpdateHitbox()
    {
        if (_hitboxObject == null || boneTransform == null) return;

        // 更新位置和旋转
        _hitboxObject.transform.position = boneTransform.TransformPoint(config.center);
        _hitboxObject.transform.rotation = boneTransform.rotation * config.rotation;

        if(_collider == null) return;

        // 更新碰撞体参数
        _collider.height = config.height;
        _collider.radius = config.radius;

        // 设置胶囊方向（默认为Y轴）
        _collider.direction = 1; // 1 = Y轴

        
    }

    private void DestroyHitbox()
    {
        if (_hitboxObject != null)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_hitboxObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_hitboxObject);
            }
            _hitboxObject = null;
            _collider = null;
            asset.hitbox = null;
        }
    }

}
