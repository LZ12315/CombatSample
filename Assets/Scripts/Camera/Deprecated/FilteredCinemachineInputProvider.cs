using Cinemachine;
using UnityEngine;

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
        if (!isActiveAndEnabled || axis < 0 || axis > 1)
            return 0f;
        if (PlayerInputController.Instance == null || !PlayerInputController.Instance.IsLookInputEnabled)
        {
            _cachedFrame = -1;
            return 0f;
        }

        if (Time.frameCount != _cachedFrame)
        {
            _cachedFrame = Time.frameCount;
            _cachedFilteredXY = ReadAndFilterXY();
        }

        return axis == 0 ? _cachedFilteredXY.x : _cachedFilteredXY.y;
    }

    private Vector2 ReadAndFilterXY()
    {
        PlayerInputController input = PlayerInputController.Instance;
        if (input == null || !input.TryGetLookInput(out Vector2 raw, out bool isMouse))
            return Vector2.zero;
        // Do not ResolveForPlayer/auto-enable XYAxis: that would create a second
        // independent input authority outside the player's menu/focus permissions.
        if (!enableMouseDominantAxisFilter)
            return raw;

        if (filterOnlyMouse && !isMouse)
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
}
