using System;
using UnityEngine;

/// <summary>
/// Data-only reference to a humanoid bone or actor-local attachment point.
/// </summary>
[Serializable]
public struct BoneReference
{
    public enum Mode
    {
        HumanBone = 0,
        Path = 1,
        ActorPath = 2,
    }

    public Mode mode;

    [Tooltip("Humanoid rig bone.")]
    public HumanBodyBones humanBone;

    [Tooltip("Relative path. Path mode starts at Animator.transform; ActorPath mode starts at Actor.transform.")]
    public string bonePath;

    public Transform Resolve(Actor actor)
    {
        if (actor == null)
            return null;

        if (mode == Mode.ActorPath)
            return string.IsNullOrEmpty(bonePath) ? actor.transform : actor.transform.Find(bonePath);

        Animator animator = actor.animancer != null ? actor.animancer.Animator : null;
        return Resolve(animator);
    }

    public Transform Resolve(Animator animator)
    {
        if (animator == null)
            return null;

        switch (mode)
        {
            case Mode.HumanBone:
                return animator.GetBoneTransform(humanBone);
            case Mode.Path:
                return string.IsNullOrEmpty(bonePath) ? null : animator.transform.Find(bonePath);
            case Mode.ActorPath:
                return null;
            default:
                return null;
        }
    }
}
