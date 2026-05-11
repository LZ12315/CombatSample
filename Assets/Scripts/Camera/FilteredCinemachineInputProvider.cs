using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Extends CinemachineInputProvider with DMC5-style dominant-axis filtering.
/// The filtered XY value is cached once per frame so axis 0 and 1 share the
/// same processed input sample.
/// </summary>
[DisallowMultipleComponent]
public class FilteredCinemachineInputProvider : CinemachineInputProvider
{
    [Header("Mouse Filtering")]
    [SerializeField] private bool enableMouseDominantAxisFilter = true;
    [SerializeField] private float mouseDeadZone = 0.1f;
    [SerializeField] private float dominantAxisRatio = 1.5f;
    [SerializeField, Range(0f, 1f)] private float minorAxisSuppression = 0.3f;
    [SerializeField] private bool filterOnlyMouse = true;

    private int _cachedFrame = -1;
    private Vector2 _cachedFilteredXY;

    public override float GetAxisValue(int axis)
    {
        if (axis == 2)
            return base.GetAxisValue(2);
        if (axis < 0 || axis > 2)
            return 0f;

        if (Time.frameCount != _cachedFrame)
        {
            _cachedFrame = Time.frameCount;
            _cachedFilteredXY = ReadAndFilterXY();
        }

        return axis == 0 ? _cachedFilteredXY.x : _cachedFilteredXY.y;
    }

    private Vector2 ReadAndFilterXY()
    {
        InputAction action = ResolveForPlayer(0, XYAxis);
        if (action == null)
            return Vector2.zero;

        Vector2 raw = action.ReadValue<Vector2>();
        if (!enableMouseDominantAxisFilter)
            return raw;

        if (filterOnlyMouse && !IsMouseAction(action))
            return raw;

        if (raw.magnitude < mouseDeadZone)
            return Vector2.zero;

        float absDx = Mathf.Abs(raw.x);
        float absDy = Mathf.Abs(raw.y);

        if (absDy > absDx * dominantAxisRatio)
            raw.x *= minorAxisSuppression;
        else if (absDx > absDy * dominantAxisRatio)
            raw.y *= minorAxisSuppression;

        return raw;
    }

    private static bool IsMouseAction(InputAction action)
    {
        return action.activeControl != null
            && action.activeControl.device is Mouse;
    }
}
