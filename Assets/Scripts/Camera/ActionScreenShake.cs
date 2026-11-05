using System;
using Cinemachine;
using UnityEngine;

/// <summary>Immutable camera-space signal authored over normalized Action progress.</summary>
internal sealed class ActionScreenShakeRequest
{
    private readonly AnimationCurve _x, _y, _z, _pitch, _yaw, _roll;
    private readonly Vector3 _direction;
    private readonly bool _directional;
    internal const int Channel = 2; // Dedicated to Action intervals; resolved-hit Impulses remain on channel 1.
    internal float Intensity { get; }
    internal float MaxDistance { get; }
    private readonly float _rotationAmplitude;

    private ActionScreenShakeRequest(ScreenShakeItemConfig config)
    {
        Intensity = config.screenShake.intensity;
        MaxDistance = config.maxDistance;
        _directional = config.mode == ActionScreenShakeMode.Directional;
        _direction = Direction(config).normalized;
        _x = Copy(_directional ? config.waveform : config.positionX);
        _y = Copy(config.positionY);
        _z = Copy(config.positionZ);
        _rotationAmplitude = config.useRotation ? config.rotationAmplitude : 0f;
        _pitch = Copy(config.pitch); _yaw = Copy(config.yaw); _roll = Copy(config.roll);
    }

    internal static bool TryCreate(ScreenShakeItemConfig config, out ActionScreenShakeRequest request) =>
        TryCreate(config, out request, out _);

    internal static bool TryCreate(ScreenShakeItemConfig config, out ActionScreenShakeRequest request, out string error)
    {
        request = null;
        error = string.Empty;
        if (config?.screenShake == null) error = "Screen Shake configuration is missing.";
        else if (!config.screenShake.enabled) error = "Screen Shake is disabled.";
        else if (!FiniteNonNegative(config.screenShake.intensity) || !Positive(config.maxDistance)
            || !FiniteNonNegative(config.rotationAmplitude)) error = "Amplitudes must be finite/nonnegative; Max Distance must be positive.";
        else if (config.mode != ActionScreenShakeMode.Directional && config.mode != ActionScreenShakeMode.IndependentAxes)
            error = "Unknown signal mode.";
        else if (config.mode == ActionScreenShakeMode.Directional
            && (!Finite(Direction(config)) || Direction(config).sqrMagnitude < 1e-8f)) error = "Direction must be a finite, nonzero vector.";
        else if (!ValidCurve(config.mode == ActionScreenShakeMode.Directional ? config.waveform : config.positionX)
            || (config.mode == ActionScreenShakeMode.IndependentAxes && (!ValidCurve(config.positionY) || !ValidCurve(config.positionZ)))
            || (config.useRotation && (!ValidCurve(config.pitch) || !ValidCurve(config.yaw) || !ValidCurve(config.roll))))
            error = "Signal curves need finite values and keys within normalized time [0, 1].";
        if (error.Length != 0) return false;
        request = new ActionScreenShakeRequest(config);
        return true;
    }

    internal void Sample(float progress, out Vector3 position, out Quaternion rotation)
    {
        float t = Finite(progress) ? Mathf.Clamp01(progress) : 0f;
        position = (_directional ? _direction * _x.Evaluate(t)
            : new Vector3(_x.Evaluate(t), _y.Evaluate(t), _z.Evaluate(t))) * Intensity;
        if (!Finite(position)) position = Vector3.zero;
        Vector3 euler = _rotationAmplitude > 0f ? new Vector3(_pitch.Evaluate(t),
            _yaw.Evaluate(t), _roll.Evaluate(t)) * _rotationAmplitude : Vector3.zero;
        rotation = Finite(euler) ? Quaternion.Euler(euler) : Quaternion.identity;
    }

    internal static float Normalize(double actionPosition, int startFrame, int durationFrames) =>
        durationFrames <= 1 ? 0f : Mathf.Clamp01((float)((actionPosition - startFrame) / (durationFrames - 1d)));

    internal bool HasNonZeroEndpoint()
    {
        Sample(0f, out Vector3 start, out Quaternion startRotation);
        Sample(1f, out Vector3 end, out Quaternion endRotation);
        return start.sqrMagnitude > 1e-8f || end.sqrMagnitude > 1e-8f
            || Quaternion.Angle(startRotation, Quaternion.identity) > 0.01f
            || Quaternion.Angle(endRotation, Quaternion.identity) > 0.01f;
    }

    private static Vector3 Direction(ScreenShakeItemConfig config) => config.direction switch
    {
        ActionScreenShakeDirection.Vertical => Vector3.up,
        ActionScreenShakeDirection.Horizontal => Vector3.right,
        ActionScreenShakeDirection.Forward => Vector3.forward,
        ActionScreenShakeDirection.Custom => config.customDirection,
        _ => Vector3.zero,
    };
    private static AnimationCurve Copy(AnimationCurve curve) => curve == null ? AnimationCurve.Constant(0f, 1f, 0f)
        : new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
    private static bool ValidCurve(AnimationCurve curve)
    {
        if (curve == null || curve.length == 0) return false;
        foreach (Keyframe key in curve.keys)
            if (!Finite(key.time) || !Finite(key.value) || key.time < 0f || key.time > 1f
                || float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent)
                || !Finite(key.inWeight) || !Finite(key.outWeight)) return false;
        return true; // Infinite tangents are valid Unity stepped keys.
    }
    private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    private static bool Positive(float v) => Finite(v) && v > 0f;
    private static bool FiniteNonNegative(float v) => Finite(v) && v >= 0f;
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}

/// <summary>
/// One native Impulse event per Action interval. No Camera Transform writes or global event cancellation.
/// The raw 6D signal reads the Action clock; native time only holds the event open until explicit release.
/// </summary>
internal static class ActionScreenShakeOutput
{
    internal static IActionPresentation Begin(ActionScreenShakeRequest request, Vector3 sourcePosition, Func<float> progress) =>
        new Session(request, sourcePosition, progress);

    private sealed class Signal : ISignalSource6D
    {
        private Func<float> _progress;
        private readonly ActionScreenShakeRequest _request;
        internal Signal(ActionScreenShakeRequest request, Func<float> progress) { _request = request; _progress = progress; }
        public float SignalDuration => 0f;
        internal void Stop() => _progress = null;
        public void GetSignal(float timeSinceSignalStart, out Vector3 pos, out Quaternion rot)
        {
            if (_progress == null) { pos = Vector3.zero; rot = Quaternion.identity; return; }
            _request.Sample(_progress(), out pos, out rot);
        }
    }

    private sealed class Session : IActionSpatialPresentation
    {
        private CinemachineImpulseManager.ImpulseEvent _event;
        private readonly Signal _signal;
        internal Session(ActionScreenShakeRequest request, Vector3 position, Func<float> progress)
        {
            _signal = new Signal(request, progress);
            var manager = CinemachineImpulseManager.Instance;
            _event = manager.NewImpulseEvent();
            _event.m_SignalSource = _signal;
            _event.m_Envelope = new CinemachineImpulseManager.EnvelopeDefinition { m_HoldForever = true };
            _event.m_Channel = ActionScreenShakeRequest.Channel;
            _event.m_Position = position;
            _event.m_Radius = 0f;
            _event.m_DissipationDistance = request.MaxDistance;
            _event.m_CustomDissipation = -1f;
            _event.m_DissipationMode = CinemachineImpulseManager.ImpulseEvent.DissipationMode.LinearDecay;
            _event.m_DirectionMode = CinemachineImpulseManager.ImpulseEvent.DirectionMode.Fixed;
            _event.m_PropagationSpeed = float.MaxValue; // No delayed waves after the interval ends.
            manager.AddImpulseEvent(_event);
        }
        public void UpdateSourcePosition(Vector3 position)
        {
            if (_event != null && ReferenceEquals(_event.m_SignalSource, _signal)) _event.m_Position = position;
        }
        public void Dispose()
        {
            _signal.Stop(); // Also clears the Action-clock closure, even if the native event was recycled externally.
            if (_event != null && ReferenceEquals(_event.m_SignalSource, _signal))
            {
                _event.Cancel(CinemachineImpulseManager.Instance.CurrentTime, true);
                // CM 2.10 treats a zero-duration envelope as non-expiring. Ensure same-tick cancel can recycle.
                if (_event.m_Envelope.Duration <= 0f) _event.m_Envelope.m_SustainTime = 0.0001f;
            }
            _event = null;
        }
    }
}
