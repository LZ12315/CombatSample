using UnityEditor;
using UnityEngine;
using System; // 需要添加这个命名空间来使用Enum

public class BoneColliderEditor // 必须包裹在类中
{
    [MenuItem("Tools/Add Bone Colliders")]
    static void AddBoneColliders()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;

        Animator animator = selected.GetComponent<Animator>();
        if (animator == null) return;

        foreach (HumanBodyBones boneType in Enum.GetValues(typeof(HumanBodyBones)))
        {
            Transform bone = animator.GetBoneTransform(boneType);
            if (bone == null) continue;

            bone.gameObject.layer = 6;
            CapsuleCollider col = bone.gameObject.AddComponent<CapsuleCollider>();
            col.radius = CalculateRadius(bone);
            col.height = CalculateHeight(bone);
            col.isTrigger = true;
        }
    }

    static float CalculateRadius(Transform bone)
    {
        if (bone.childCount == 0) return 0.05f;
        float minDistance = Vector3.Distance(bone.position, bone.GetChild(0).position);
        return Mathf.Clamp(minDistance * 0.3f, 0.03f, 0.1f);
    }

    static float CalculateHeight(Transform bone)
    {
        if (bone.childCount == 0) return 0.1f;
        return Vector3.Distance(bone.position, bone.GetChild(0).position);
    }
}
