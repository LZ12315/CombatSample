#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Editor preview clock controlled and owned by Action Timeline.</summary>
[InitializeOnLoad]
internal static class ActionEditorPlayback
{
    private static double _lastUpdateTime;

    internal static bool IsPlaying { get; private set; }

    static ActionEditorPlayback()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
    }

    internal static void Toggle()
    {
        if (IsPlaying) Stop(); else Play();
    }

    internal static void Play()
    {
        ActionAsset action = ActionEditorContext.Shared.CurrentAction;
        if (IsPlaying || action == null || action.Timeline == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        int duration = Mathf.Max(1, action.Timeline.DurationFrames);
        if (ActionEditorContext.Shared.PreviewPosition >= duration)
            ActionEditorContext.Shared.SetPreviewPosition(0d);
        _lastUpdateTime = EditorApplication.timeSinceStartup;
        IsPlaying = true;
        EditorApplication.update += Update;
        ActionEditorContext.Shared.NotifyPlaybackChanged();
    }

    internal static void Stop(bool notify = true)
    {
        if (!IsPlaying)
            return;
        IsPlaying = false;
        EditorApplication.update -= Update;
        if (notify)
            ActionEditorContext.Shared.NotifyPlaybackChanged();
    }

    private static void Update()
    {
        ActionAsset action = ActionEditorContext.Shared.CurrentAction;
        if (action == null || action.Timeline == null)
        {
            Stop();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        double elapsed = Math.Max(0d, now - _lastUpdateTime);
        _lastUpdateTime = now;
        if (elapsed <= 0d)
            return;

        double duration = Math.Max(1, action.Timeline.DurationFrames);
        double next = ActionEditorContext.Shared.PreviewPosition + elapsed * ActionTimelineData.FrameRate;
        if (next >= duration)
        {
            if (ActionEditorContext.Shared.PreviewLoop)
                next %= duration;
            else
            {
                next = duration;
                ActionEditorContext.Shared.SetPreviewPosition(next);
                Stop();
                return;
            }
        }
        ActionEditorContext.Shared.SetPreviewPosition(next);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            Stop();
    }

    private static void OnBeforeAssemblyReload() => Stop(false);
}
#endif
