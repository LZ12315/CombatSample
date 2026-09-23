using UnityEngine;

/// <summary>
/// Stable, action-facing attachment points for authored HitBoxes.
/// Actions describe intent; this resolver owns the concrete humanoid bone mapping.
/// </summary>
public enum ActionHitBoxAnchor
{
    ActorRoot = 0,
    Torso = 1,
    LeftHand = 2,
    RightHand = 3,
    LeftLeg = 4,
    RightLeg = 5,
}

public static class ActionHitBoxAnchorResolver
{
    public static bool TryResolve(
        ActionHitBoxAnchor anchor,
        Transform actorRoot,
        Animator animator,
        out Transform binding,
        out string failureReason)
    {
        binding = null;
        failureReason = string.Empty;

        if (anchor == ActionHitBoxAnchor.ActorRoot)
        {
            binding = actorRoot;
            if (binding != null)
                return true;

            failureReason = "The character root is missing.";
            return false;
        }

        if (!TryGetHumanBone(anchor, out HumanBodyBones bone))
        {
            failureReason = $"Anchor '{anchor}' is not supported.";
            return false;
        }

        if (animator == null)
        {
            failureReason = $"Anchor '{anchor}' requires an Animator.";
            return false;
        }

        if (animator.avatar == null || !animator.isHuman)
        {
            failureReason = $"Anchor '{anchor}' requires a valid Humanoid Avatar.";
            return false;
        }

        binding = animator.GetBoneTransform(bone);
        if (binding != null)
            return true;

        failureReason = $"The Humanoid Avatar does not provide the bone required by anchor '{anchor}'.";
        return false;
    }

    private static bool TryGetHumanBone(ActionHitBoxAnchor anchor, out HumanBodyBones bone)
    {
        switch (anchor)
        {
            case ActionHitBoxAnchor.Torso:
                bone = HumanBodyBones.Spine;
                return true;
            case ActionHitBoxAnchor.LeftHand:
                bone = HumanBodyBones.LeftHand;
                return true;
            case ActionHitBoxAnchor.RightHand:
                bone = HumanBodyBones.RightHand;
                return true;
            case ActionHitBoxAnchor.LeftLeg:
                bone = HumanBodyBones.LeftLowerLeg;
                return true;
            case ActionHitBoxAnchor.RightLeg:
                bone = HumanBodyBones.RightLowerLeg;
                return true;
            default:
                bone = HumanBodyBones.LastBone;
                return false;
        }
    }
}
