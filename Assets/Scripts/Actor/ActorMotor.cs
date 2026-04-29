using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// KCC adapter for ActorMovement.
/// Keeps gameplay movement semantics in ActorMovement while delegating collision solving/grounding to KinematicCharacterMotor.
/// </summary>
[RequireComponent(typeof(KinematicCharacterMotor))]
public class ActorMotor : MonoBehaviour, ICharacterController
{
    [SerializeField] private Actor _actor;
    [SerializeField] private KinematicCharacterMotor _motor;
    [SerializeField] private Transform _selfRoot;

    private bool _hitCeilingThisFrame;

    public KinematicCharacterMotor Motor => _motor;
    public bool IsStableGrounded => _motor != null && _motor.GroundingStatus.IsStableOnGround;
    public bool FoundAnyGround => _motor != null && _motor.GroundingStatus.FoundAnyGround;
    public bool HitCeilingThisFrame => _hitCeilingThisFrame;

    private void Awake()
    {
        if (_actor == null)
            _actor = GetComponent<Actor>();
        if (_motor == null)
            _motor = GetComponent<KinematicCharacterMotor>();
        if (_selfRoot == null)
            _selfRoot = transform.root;

        if (_motor != null)
            _motor.CharacterController = this;
    }

    public void ForceUnground(float time = 0.1f)
    {
        _motor?.ForceUnground(time);
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_actor == null || _actor.movement == null)
            return;

        currentRotation = _actor.movement.BuildMotorRotation(deltaTime, currentRotation);
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (_actor == null || _actor.movement == null)
            return;

        currentVelocity = _actor.movement.BuildMotorVelocity(deltaTime, IsStableGrounded, _hitCeilingThisFrame);
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        _hitCeilingThisFrame = false;
    }

    public void PostGroundingUpdate(float deltaTime) { }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (_actor == null || _actor.movement == null || _motor == null)
            return;

        _actor.movement.AfterMotorUpdate(_motor.BaseVelocity);
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        if (coll == null)
            return false;

        if (_motor != null && _motor.Capsule != null && ReferenceEquals(coll, _motor.Capsule))
            return false;

        // Prevent self-collision/depenetration against colliders on the same actor hierarchy.
        if (_selfRoot != null && coll.transform.root == _selfRoot)
            return false;

        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        if (Vector3.Dot(hitNormal, _motor.CharacterUp) < -0.5f)
            _hitCeilingThisFrame = true;
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        if (Vector3.Dot(hitNormal, _motor.CharacterUp) < -0.5f)
            _hitCeilingThisFrame = true;
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
}
