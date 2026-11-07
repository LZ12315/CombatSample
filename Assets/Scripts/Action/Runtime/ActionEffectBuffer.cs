using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A range's realized presentation, independent of its Action scheduler.</summary>
internal interface IActionPresentation : IDisposable { }

internal interface IActionSpatialPresentation : IActionPresentation
{
    void UpdateSourcePosition(Vector3 position);
}

/// <summary>
/// Per-actor presentation scopes. Enter records parameters; World realizes each scope once.
/// Exiting cancels pending work or disposes only the instance/contribution owned by that range.
/// </summary>
internal sealed class ActionEffectBuffer
{
    private readonly List<Scope> _scopes = new List<Scope>(4);
    private bool _updating;
    private bool _clearing;

    internal IDisposable BeginEffect(EffectItemConfig config, UnityEngine.Object diagnosticContext = null)
    {
        if (!ActionEffectRequest.TryCreate(config, out ActionEffectRequest request, out string error))
        {
            if (diagnosticContext != null) Debug.LogWarning($"[Action Effect] {error} Playback skipped.", diagnosticContext);
            return null;
        }
        var scope = new Scope(request);
        _scopes.Add(scope);
        return scope;
    }

    internal IDisposable BeginScreenShake(ScreenShakeItemConfig config, Func<float> progress, UnityEngine.Object diagnosticContext = null)
    {
        if (!ActionScreenShakeRequest.TryCreate(config, out ActionScreenShakeRequest request, out string error))
        {
            if (diagnosticContext != null && (config?.screenShake?.enabled ?? true))
                Debug.LogWarning($"[Action Shake] {error} Playback skipped.", diagnosticContext);
            return null;
        }
        var scope = new Scope(request, progress);
        _scopes.Add(scope);
        return scope;
    }

    internal void UpdateWorld(Vector3 sourcePosition, Func<ActionEffectRequest, IActionPresentation> startEffect,
        Func<ActionScreenShakeRequest, Vector3, Func<float>, IActionPresentation> startShake)
    {
        if (!ActionEffectRequest.IsFinite(sourcePosition))
        {
            Clear();
            return;
        }

        _updating = true;
        try
        {
            // A prefab's Awake can cancel this Actor or start another Action. New scopes wait for the next World.
            int count = _scopes.Count;
            for (int i = 0; i < count; i++)
                _scopes[i].UpdateWorld(sourcePosition, startEffect, startShake);
        }
        finally
        {
            _updating = false;
            _scopes.RemoveAll(scope => scope.IsDisposed);
        }
    }

    internal void Clear()
    {
        if (_clearing)
            return;
        _clearing = true;
        try
        {
            for (int i = 0; i < _scopes.Count; i++)
            {
                try { _scopes[i].Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
        finally
        {
            _clearing = false;
            if (!_updating)
                _scopes.Clear();
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly ActionEffectRequest _effect;
        private readonly ActionScreenShakeRequest _shake;
        private readonly bool _isEffect;
        private Func<float> _progress;
        private bool _started;
        private IActionPresentation _presentation;

        internal Scope(ActionEffectRequest effect) { _effect = effect; _isEffect = true; }
        internal Scope(ActionScreenShakeRequest shake, Func<float> progress) { _shake = shake; _progress = progress; }
        internal bool IsDisposed { get; private set; }

        internal void UpdateWorld(Vector3 position, Func<ActionEffectRequest, IActionPresentation> startEffect,
            Func<ActionScreenShakeRequest, Vector3, Func<float>, IActionPresentation> startShake)
        {
            if (IsDisposed)
                return;
            if (!_started)
            {
                // Missing executors/anchors are not retried every tick.
                _started = true;
                IActionPresentation presentation = _isEffect ? startEffect?.Invoke(_effect) : startShake?.Invoke(_shake, position, _progress);
                if (IsDisposed)
                    presentation?.Dispose(); // Actor may have been disabled during prefab Awake.
                else
                    _presentation = presentation;
            }
            if (!IsDisposed && _presentation is IActionSpatialPresentation spatial) spatial.UpdateSourcePosition(position);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;
            _progress = null;
            IActionPresentation presentation = _presentation;
            _presentation = null;
            presentation?.Dispose();
        }
    }
}
