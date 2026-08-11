#if UNITY_EDITOR
using UnityEngine;

[AddComponentMenu("")]
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class RootMotionOracleRecorder : MonoBehaviour
{
    private Animator _animator;
    private Vector3 _baselinePosition;
    private Quaternion _baselineRotation = Quaternion.identity;
    private Vector3 _worldPosition;
    private Quaternion _worldRotation = Quaternion.identity;
    private bool _sampling;

    public int CallbackCount { get; private set; }
    public Vector3 CumulativePosition { get; private set; }
    public Quaternion CumulativeRotation { get; private set; } = Quaternion.identity;

    public void Initialize(Animator animator)
    {
        _animator = animator;
        _worldPosition = transform.position;
        _worldRotation = NormalizeSafe(transform.rotation);
    }

    public void BeginSampling()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        _baselinePosition = transform.position;
        _baselineRotation = NormalizeSafe(transform.rotation);
        _worldPosition = _baselinePosition;
        _worldRotation = _baselineRotation;
        CumulativePosition = Vector3.zero;
        CumulativeRotation = Quaternion.identity;
        CallbackCount = 0;
        _sampling = true;
    }

    private void OnAnimatorMove()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_animator == null)
            return;

        Vector3 deltaPosition = _animator.deltaPosition;
        Quaternion deltaRotation = NormalizeSafe(_animator.deltaRotation);

        // This deliberately mirrors Unity/Animancer's documented Animator-delta
        // application path instead of reading the transform path used by the Baker.
        _worldPosition += deltaPosition;
        _worldRotation = NormalizeSafe(_worldRotation * deltaRotation);
        transform.SetPositionAndRotation(_worldPosition, _worldRotation);

        if (!_sampling)
            return;

        Quaternion inverseBaseline = Quaternion.Inverse(_baselineRotation);
        CumulativePosition = inverseBaseline * (_worldPosition - _baselinePosition);
        CumulativeRotation = NormalizeSafe(inverseBaseline * _worldRotation);
        CallbackCount++;
    }

    private static Quaternion NormalizeSafe(Quaternion value)
    {
        float sqrMagnitude = value.x * value.x
            + value.y * value.y
            + value.z * value.z
            + value.w * value.w;
        if (float.IsNaN(sqrMagnitude) || float.IsInfinity(sqrMagnitude) || sqrMagnitude < 1e-12f)
            return Quaternion.identity;

        float inverseMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
        return new Quaternion(
            value.x * inverseMagnitude,
            value.y * inverseMagnitude,
            value.z * inverseMagnitude,
            value.w * inverseMagnitude);
    }
}
#endif
