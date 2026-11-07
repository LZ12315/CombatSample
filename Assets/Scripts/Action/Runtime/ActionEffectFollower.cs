using UnityEngine;

/// <summary>Position/orientation binding only, owned and destroyed with its VFX instance.</summary>
[AddComponentMenu("")]
[DefaultExecutionOrder(10000)] // Resolve continuous screen-facing after Cinemachine has updated.
internal sealed class ActionEffectFollower : MonoBehaviour
{
    private ActionEffectRequest _request;
    private Transform _anchor, _actor;
    private Vector3 _localScale;
    private Vector2 _angles;
    internal void Bind(ActionEffectRequest request, Transform anchor, Transform actor, Vector3 localScale, Vector2 angles)
    { _request = request; _anchor = anchor; _actor = actor; _localScale = localScale; _angles = angles; }
    private void LateUpdate()
    {
        if (_anchor == null || _actor == null) { Stop(); return; }
        if (_request.FollowAnchor)
        {
            transform.position = _anchor.TransformPoint(_request.LocalPosition);
            transform.localScale = Vector3.Scale(_anchor.lossyScale, _localScale);
        }
        if (!_request.ContinuousOrientation) return;
        Camera camera = null;
        if (_request.NeedsCamera && !ImpactRotationUtility.TryGetPrimaryCamera(out camera))
        {
            Debug.LogWarning("[Action Effect] Continuous orientation lost its camera; stopping the effect.", this);
            Stop(); return;
        }
        if (_request.TryResolveRotation(_anchor.rotation, _actor.rotation,
            camera != null ? (Quaternion?)camera.transform.rotation : null, _angles, out Quaternion rotation))
            transform.rotation = rotation;
    }
    private void Stop() { gameObject.SetActive(false); Destroy(gameObject); }
}
