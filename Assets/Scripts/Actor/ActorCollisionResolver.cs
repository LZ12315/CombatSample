using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// Resolves actor-on-actor horizontal overlaps after the KCC tick.
///
/// Rules:
/// - Horizontal push is inverse-mass based: higher ActorPushMass = more solid.
/// - Both CanBeActorPushed: share separation by inverse-mass ratio.
/// - One CanBeActorPushed=false: the pushable actor takes full separation.
/// - Neither pushable: no movement (anchored actors).
/// - All corrections are horizontal only (no vertical displacement).
/// - Side-push and head-slide use distinct strategies for smooth results.
///
/// Attach this to a scene-level manager object (e.g. Manager.prefab) so the project
/// has one explicit, reviewable place where actor separation is coordinated.
/// Runs in FixedUpdate at execution order -99, right after KinematicCharacterSystem (-100).
/// </summary>
[DefaultExecutionOrder(-99)]
public class ActorCollisionResolver : MonoBehaviour
{
    #region === Registration ===

    private static readonly List<ActorMotor> _actors = new List<ActorMotor>(32);

    public static void Register(ActorMotor motor)
    {
        if (motor == null) return;
        if (!_actors.Contains(motor))
            _actors.Add(motor);
    }

    public static void Unregister(ActorMotor motor)
    {
        _actors.Remove(motor);
    }

    #endregion

    #region === Settings ===

    [Header("Resolution")]
    [SerializeField, Range(1, 10), Tooltip("Maximum iterations per tick to prevent jitter.")]
    private int _maxIterations = 3;

    [SerializeField, Range(0.01f, 1f), Tooltip("Maximum horizontal correction per iteration (meters).")]
    private float _maxCorrectionPerIteration = 0.3f;

    [SerializeField, Tooltip("Minimum penetration to trigger resolution (meters).")]
    private float _minPenetration = 0.001f;

    [Header("Smoothing")]
    [SerializeField, Range(0f, 0.5f), Tooltip("Fraction of average capsule height one actor must be above another to trigger top-slide instead of side-push.")]
    private float _topSlideHeightFraction = 0.2f;

    [SerializeField, Tooltip("Max frames to remember a pair's slide direction while still in contact.")]
    private int _maxPairContactFrames = 120;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;
    [SerializeField] private bool _enableDebugGizmos;

    #endregion

    #region === Per-Pair Contact State ===

    private struct PairContact
    {
        public Vector3 slideDirection;
        public int framesInContact;
        public bool directionLocked;
    }

    private readonly Dictionary<ulong, PairContact> _contacts = new Dictionary<ulong, PairContact>();

    // Reusable key buffer for aging contact state without allocating a new list each frame.
    private readonly List<ulong> _agedKeys = new List<ulong>(64);

    private static ulong PairKey(int idA, int idB)
    {
        uint a = (uint)idA;
        uint b = (uint)idB;
        if (a < b) return ((ulong)a << 32) | b;
        return ((ulong)b << 32) | a;
    }

    #endregion

    #region === Unity Lifecycle ===

    private void FixedUpdate()
    {
        // Copy keys first; Dictionary cannot be modified while its enumerator is active.
        _agedKeys.Clear();
        foreach (var key in _contacts.Keys)
            _agedKeys.Add(key);

        for (int i = 0; i < _agedKeys.Count; i++)
        {
            ulong key = _agedKeys[i];
            if (!_contacts.TryGetValue(key, out PairContact contact))
                continue;

            contact.framesInContact++;
            if (contact.framesInContact > _maxPairContactFrames)
                _contacts.Remove(key);
            else
                _contacts[key] = contact;
        }

        ResolveOverlaps();
    }

    #endregion

    #region === Resolution ===

    private void ResolveOverlaps()
    {
        for (int i = _actors.Count - 1; i >= 0; i--)
        {
            if (_actors[i] == null)
                _actors.RemoveAt(i);
        }

        int count = _actors.Count;
        if (count < 2) return;

        for (int iteration = 0; iteration < _maxIterations; iteration++)
        {
            bool anyResolved = false;

            for (int i = 0; i < count - 1; i++)
            {
                ActorMotor a = _actors[i];
                if (a == null || !a.isActiveAndEnabled) continue;

                for (int j = i + 1; j < count; j++)
                {
                    ActorMotor b = _actors[j];
                    if (b == null || !b.isActiveAndEnabled) continue;

                    if (ResolvePair(a, b))
                        anyResolved = true;
                }
            }

            if (!anyResolved) break;
        }
    }

    private static readonly RaycastHit[] _sweepHitBuffer = new RaycastHit[16];

    /// <summary>
    /// Check whether two motor capsules overlap (vertically AND horizontally),
    /// and compute the horizontal separation vector that moves A away from B.
    /// Also reports which actor (if either) is notably above the other for top-slide detection.
    /// </summary>
    private bool CheckHorizontalOverlap(ActorMotor a, ActorMotor b,
        out Vector3 separation, out bool aIsAboveB, out bool bIsAboveA)
    {
        separation = Vector3.zero;
        aIsAboveB = false;
        bIsAboveA = false;

        if (a == null || b == null || a == b || a.Capsule == null || b.Capsule == null) return false;

        Vector3 posA = a.Motor.TransientPosition;
        Vector3 posB = b.Motor.TransientPosition;

        Vector3 centerA = posA + a.Capsule.center;
        Vector3 centerB = posB + b.Capsule.center;

        float halfHtA = a.Capsule.height * 0.5f;
        float halfHtB = b.Capsule.height * 0.5f;
        float bottomA = centerA.y - halfHtA;
        float topA = centerA.y + halfHtA;
        float bottomB = centerB.y - halfHtB;
        float topB = centerB.y + halfHtB;

        const float verticalTolerance = 0.01f;
        if (bottomA >= topB - verticalTolerance || bottomB >= topA - verticalTolerance)
            return false;

        // Which actor is notably above the other? Uses the serialized threshold.
        float avgCapsuleHeight = (a.Capsule.height + b.Capsule.height) * 0.5f;
        float topSlideThreshold = avgCapsuleHeight * _topSlideHeightFraction;
        float verticalOffset = centerA.y - centerB.y;
        aIsAboveB = verticalOffset > topSlideThreshold;
        bIsAboveA = verticalOffset < -topSlideThreshold;

        float totalRadius = a.Capsule.radius + b.Capsule.radius;
        Vector3 horizA = new Vector3(centerA.x, 0f, centerA.z);
        Vector3 horizB = new Vector3(centerB.x, 0f, centerB.z);
        Vector3 delta = horizA - horizB;
        float horizDistance = delta.magnitude;

        if (horizDistance < totalRadius && horizDistance > 0.0001f)
        {
            separation = delta / horizDistance * (totalRadius - horizDistance);
            return true;
        }

        if (horizDistance <= 0.0001f)
        {
            separation = Vector3.right * totalRadius;
            return true;
        }

        return false;
    }

    private bool ResolvePair(ActorMotor a, ActorMotor b)
    {
        if (!CheckHorizontalOverlap(a, b, out Vector3 separation, out bool aIsAboveB, out bool bIsAboveA))
        {
            _contacts.Remove(PairKey(a.GetInstanceID(), b.GetInstanceID()));
            return false;
        }

        float penetration = separation.magnitude;
        if (penetration < _minPenetration)
            return false;

        if (penetration > _maxCorrectionPerIteration)
            separation = separation.normalized * _maxCorrectionPerIteration;

        bool anyDisplaced = false;

        // --- Top-slide branch ---
        // One actor is notably above the other. Push the upper one in a stable
        // horizontal direction; the lower one stays put as a non-stable surface.
        if (aIsAboveB || bIsAboveA)
        {
            ActorMotor upper = aIsAboveB ? a : b;
            ActorMotor lower = aIsAboveB ? b : a;
            Vector3 upperSlideDir = GetStableSlideDirection(upper, lower, separation, aIsAboveB);

            if (upper.CanBeActorPushed)
                anyDisplaced |= PushMotor(upper, upperSlideDir * penetration);

            if (_enableDebugLog && anyDisplaced)
                Debug.Log($"[ActorCollisionResolver] Top-slide {upper.name} off {lower.name}: pen={penetration:F3}m");

            return anyDisplaced;
        }

        // --- Side-push branch ---
        // Mass-based horizontal separation: the inverse of mass determines how much
        // each actor yields. Higher mass = more solid, lower mass = pushed aside more.
        //
        // CheckHorizontalOverlap returns the displacement that moves A away from B.
        // Moving B away uses the opposite direction.

        float massA = Mathf.Max(a.ActorPushMass, 0.1f);
        float massB = Mathf.Max(b.ActorPushMass, 0.1f);
        float invMassA = 1f / massA;
        float invMassB = 1f / massB;
        float totalInvMass = invMassA + invMassB;

        bool pushableA = a.CanBeActorPushed;
        bool pushableB = b.CanBeActorPushed;

        if (pushableA && pushableB)
        {
            // Both can be pushed: share separation by inverse mass.
            // Higher mass → smaller share.
            float shareA = invMassA / totalInvMass;
            float shareB = invMassB / totalInvMass;
            anyDisplaced |= PushMotor(a, separation * shareA);
            anyDisplaced |= PushMotor(b, -separation * shareB);
        }
        else if (pushableA)
        {
            // Only A yields — A takes the full separation to avoid B pass-through.
            anyDisplaced |= PushMotor(a, separation);
        }
        else if (pushableB)
        {
            // Only B yields — B takes the full separation to avoid A pass-through.
            anyDisplaced |= PushMotor(b, -separation);
        }
        // else: neither pushable — no movement; pair stays overlapped (by design for anchored actors).

        if (_enableDebugLog && anyDisplaced)
            Debug.Log($"[ActorCollisionResolver] Side-push {a.name} vs {b.name}: penetration={penetration:F3}m");

        // In side-push mode, clear any stale top-slide contact state.
        _contacts.Remove(PairKey(a.GetInstanceID(), b.GetInstanceID()));

        return anyDisplaced;
    }

    /// <summary>
    /// Returns a stable horizontal direction for the upper actor to slide off the lower one.
    /// Caches the direction on first contact and reuses it until the pair separates.
    /// </summary>
    private Vector3 GetStableSlideDirection(ActorMotor upper, ActorMotor lower,
        Vector3 rawSeparation, bool aIsAboveB)
    {
        ulong key = PairKey(upper.GetInstanceID(), lower.GetInstanceID());

        if (_contacts.TryGetValue(key, out PairContact contact) && contact.directionLocked)
        {
            contact.framesInContact = 0;
            _contacts[key] = contact;
            return contact.slideDirection;
        }

        Vector3 upperHoriz = new Vector3(
            upper.Motor.TransientPosition.x + upper.Capsule.center.x, 0f,
            upper.Motor.TransientPosition.z + upper.Capsule.center.z);
        Vector3 lowerHoriz = new Vector3(
            lower.Motor.TransientPosition.x + lower.Capsule.center.x, 0f,
            lower.Motor.TransientPosition.z + lower.Capsule.center.z);

        Vector3 toUpper = upperHoriz - lowerHoriz;
        Vector3 stableDir;
        if (toUpper.sqrMagnitude > 0.0001f)
            stableDir = toUpper.normalized;
        else
            stableDir = aIsAboveB ? rawSeparation.normalized : (-rawSeparation).normalized;

        _contacts[key] = new PairContact
        {
            slideDirection = stableDir,
            framesInContact = 0,
            directionLocked = true
        };

        return stableDir;
    }

    /// <summary>
    /// Push a motor horizontally along the given offset, using the KCC's own
    /// collision sweep to clip against environment geometry. Uses bypassInterpolation=false
    /// so KCC interpolates the correction smoothly over the next frame.
    /// Returns true if any displacement was actually applied.
    /// </summary>
    private bool PushMotor(ActorMotor motor, Vector3 horizontalOffset)
    {
        if (motor == null || motor.Motor == null || motor.Capsule == null) return false;

        float distance = horizontalOffset.magnitude;
        if (distance < _minPenetration) return false;

        Vector3 direction = horizontalOffset / distance;
        Vector3 currentPos = motor.Motor.TransientPosition;
        KinematicCharacterMotor kccMotor = motor.Motor;

        int hitCount = kccMotor.CharacterCollisionsSweep(
            currentPos,
            kccMotor.TransientRotation,
            direction,
            distance,
            out RaycastHit closestHit,
            _sweepHitBuffer);

        float safeDistance = distance;

        if (hitCount > 0 && closestHit.collider != null)
        {
            float clipped = closestHit.distance - KinematicCharacterMotor.CollisionOffset;
            if (clipped < safeDistance)
                safeDistance = clipped;
        }

        if (safeDistance < _minPenetration)
            return false;

        Vector3 actualOffset = direction * safeDistance;

        if (_enableDebugLog && safeDistance < distance)
            Debug.Log($"[ActorCollisionResolver] Push clipped from {distance:F3}m to {safeDistance:F3}m by environment for {motor.name}");

        // bypassInterpolation: false — KCC interpolates correction over the next frame.
        kccMotor.SetPosition(currentPos + actualOffset, false);
        return true;
    }

    #endregion

    #region === Debug ===

    private void OnDrawGizmos()
    {
        if (!_enableDebugGizmos) return;

        for (int i = 0; i < _actors.Count; i++)
        {
            var motor = _actors[i];
            if (motor == null || motor.Capsule == null) continue;

            Gizmos.color = motor.ActorPushMass > 1f ? Color.green : Color.yellow;
            Vector3 center = motor.Motor != null
                ? motor.Motor.TransientPosition + motor.Capsule.center
                : motor.transform.position + motor.Capsule.center;

            Gizmos.DrawWireSphere(center, motor.Capsule.radius);
        }
    }

    #endregion
}
