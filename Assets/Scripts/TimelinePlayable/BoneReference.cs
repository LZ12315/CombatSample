using System;
using UnityEngine;

/// <summary>
/// 骨骼/挂点引用：纯数据，不依赖 Timeline ExposedReference。Humanoid 用枚举，非标节点用 Animator 子层级路径。
/// </summary>
[Serializable]
public struct BoneReference
{
    public enum Mode
    {
        HumanBone = 0,
        Path = 1,
    }

    public Mode mode;

    [Tooltip("Humanoid Rig 标准骨骼")]
    public HumanBodyBones humanBone;

    [Tooltip("相对 Animator.transform 的路径，如 Hips/Spine/Chest/RightHand")]
    public string bonePath;

    public Transform Resolve(Animator animator)
    {
        if (animator == null) return null;

        switch (mode)
        {
            case Mode.HumanBone:
                return animator.GetBoneTransform(humanBone);
            case Mode.Path:
                return string.IsNullOrEmpty(bonePath) ? null : animator.transform.Find(bonePath);
            default:
                return null;
        }
    }
}
