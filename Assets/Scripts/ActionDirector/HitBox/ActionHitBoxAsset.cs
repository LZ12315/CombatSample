using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public struct ActionHitBoxConfig
{
    public Vector3 center;
    public Quaternion rotation;
    public float height;
    public float radius;

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
    public ActionHitBoxConfig config = new ActionHitBoxConfig
    {
        center = Vector3.zero,
        rotation = Quaternion.identity,
        height = 1.0f,
        radius = 0.5f
    };

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

        UpdateHitbox();
    }

    private void UpdateHitbox()
    {
        if (_hitboxObject == null || boneTransform == null) return;

        // 更新位置和旋转
        _hitboxObject.transform.position = boneTransform.TransformPoint(config.center);
        _hitboxObject.transform.rotation = boneTransform.rotation * config.rotation;

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
        }
    }

#if UNITY_EDITOR
    public void DrawGizmo()
    {
        if (boneTransform == null) return;

        // 计算世界空间矩阵
        Matrix4x4 matrix = config.GetWorldMatrix(boneTransform);

        // 保存当前矩阵
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = matrix;

        // 绘制胶囊体
        DrawCapsuleGizmo(Vector3.zero, config.height, config.radius);

        // 恢复矩阵
        Gizmos.matrix = originalMatrix;
    }

    private void DrawCapsuleGizmo(Vector3 center, float height, float radius)
    {
        // 绘制胶囊体中间圆柱部分
        float cylinderHeight = height / 2 - radius;
        Vector3 top = center + Vector3.up * cylinderHeight;
        Vector3 bottom = center - Vector3.up * cylinderHeight;

        // 绘制圆柱
        Gizmos.DrawWireCube(center, new Vector3(radius * 2, height - radius * 2, radius * 2));

        // 绘制顶部半球
        DrawHemisphere(top, Vector3.up, radius);

        // 绘制底部半球
        DrawHemisphere(bottom, Vector3.down, radius);
    }

    private void DrawHemisphere(Vector3 center, Vector3 direction, float radius)
    {
        // 简化版半球绘制
        Gizmos.DrawWireSphere(center, radius);
    }
#endif
}
