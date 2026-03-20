using UnityEngine;

namespace CombatSample.Magnetism
{
    /// <summary>
    /// 任意世界采样点与敌人 CapsuleCollider 之间的纯几何（无状态）。
    /// </summary>
    public static class MagnetismCapsuleGeometry
    {
        /// <summary>
        /// signedClearance：采样点到胶囊表面的最短间隙（米）；在壳内时为负的近似穿透。
        /// outwardNormal：增加间隙时，根节点应沿该方向平移（世界空间，已归一化，指向壳外）。
        /// </summary>
        public static bool TryGetClearanceInfo(
            Vector3 samplePointWorld,
            CapsuleCollider enemyCapsule,
            out Vector3 closestSurfaceWorld,
            out Vector3 outwardNormal,
            out float signedClearance)
        {
            closestSurfaceWorld = enemyCapsule.ClosestPoint(samplePointWorld);
            float d = Vector3.Distance(samplePointWorld, closestSurfaceWorld);

            const float eps = 1e-4f;
            if (d > eps)
            {
                outwardNormal = (samplePointWorld - closestSurfaceWorld).normalized;
                signedClearance = d;
                return true;
            }

            Vector3 capCenterWorld = enemyCapsule.transform.TransformPoint(enemyCapsule.center);
            outwardNormal = samplePointWorld - capCenterWorld;
            outwardNormal.y = 0f;
            if (outwardNormal.sqrMagnitude < 1e-8f)
                outwardNormal = Vector3.forward;
            outwardNormal.Normalize();

            signedClearance = -Mathf.Max(enemyCapsule.radius * 0.25f, 0.01f);
            return true;
        }

        /// <summary>
        /// 单帧根位移：使间隙向 [ideal-dead, ideal+dead] 靠拢（水平分量）。
        /// 偏远用 -n（靠近壳/敌人），偏近用 +n（沿外法线推出）。
        /// </summary>
        public static Vector3 ComputeRootDeltaForGapBand(
            Vector3 samplePointWorld,
            CapsuleCollider enemyCapsule,
            float idealGap,
            float deadZone,
            float pullSpeed,
            float pushSpeed,
            float deltaTime,
            bool horizontalOnly,
            float maxStepMagnitude)
        {
            if (enemyCapsule == null || deltaTime <= 0f) return Vector3.zero;

            if (!TryGetClearanceInfo(samplePointWorld, enemyCapsule, out _, out Vector3 n, out float gap))
                return Vector3.zero;

            if (horizontalOnly)
            {
                n.y = 0f;
                if (n.sqrMagnitude < 1e-8f) return Vector3.zero;
                n.Normalize();
            }

            float low = idealGap - deadZone;
            float high = idealGap + deadZone;

            if (gap >= low && gap <= high)
                return Vector3.zero;

            Vector3 moveDir;
            float step;

            if (gap > high)
            {
                moveDir = -n;
                step = Mathf.Min(gap - high, pullSpeed * deltaTime);
            }
            else
            {
                moveDir = n;
                step = Mathf.Min(low - gap, pushSpeed * deltaTime);
            }

            Vector3 delta = moveDir * step;
            if (maxStepMagnitude > 0f && delta.sqrMagnitude > maxStepMagnitude * maxStepMagnitude)
                delta = delta.normalized * maxStepMagnitude;

            return delta;
        }
    }
}
