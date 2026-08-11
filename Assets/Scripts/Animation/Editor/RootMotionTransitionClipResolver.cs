#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public static class RootMotionTransitionClipResolver
{
    public static bool TryResolve(
        TransitionAsset transitionAsset,
        out AnimationClip clip,
        out RootMotionClipResolutionDiagnostic diagnostic)
    {
        clip = null;
        if (transitionAsset == null)
        {
            diagnostic = new RootMotionClipResolutionDiagnostic(
                RootMotionClipResolutionCode.MissingTransitionAsset,
                "TransitionAsset is missing.");
            return false;
        }

        if (!transitionAsset.HasTransition)
        {
            diagnostic = new RootMotionClipResolutionDiagnostic(
                RootMotionClipResolutionCode.MissingTransition,
                $"TransitionAsset '{transitionAsset.name}' has no transition data.");
            return false;
        }

        try
        {
            var gatheredClips = new List<AnimationClip>();
            transitionAsset.GetAnimationClips(gatheredClips);
            var uniqueClips = new HashSet<AnimationClip>();

            for (int i = 0; i < gatheredClips.Count; i++)
            {
                AnimationClip gatheredClip = gatheredClips[i];
                if (gatheredClip != null)
                    uniqueClips.Add(gatheredClip);
            }

            if (uniqueClips.Count == 0)
            {
                diagnostic = new RootMotionClipResolutionDiagnostic(
                    RootMotionClipResolutionCode.NoAnimationClip,
                    $"TransitionAsset '{transitionAsset.name}' does not resolve to an AnimationClip.");
                return false;
            }

            if (uniqueClips.Count != 1)
            {
                diagnostic = new RootMotionClipResolutionDiagnostic(
                    RootMotionClipResolutionCode.MultipleAnimationClips,
                    $"TransitionAsset '{transitionAsset.name}' resolves to {uniqueClips.Count} AnimationClips; Root Motion baking requires exactly one.");
                return false;
            }

            foreach (AnimationClip uniqueClip in uniqueClips)
            {
                clip = uniqueClip;
                break;
            }

            diagnostic = RootMotionClipResolutionDiagnostic.Success;
            return true;
        }
        catch (Exception exception)
        {
            diagnostic = new RootMotionClipResolutionDiagnostic(
                RootMotionClipResolutionCode.ResolutionException,
                $"Failed to resolve TransitionAsset '{transitionAsset.name}': {exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }
}

public enum RootMotionClipResolutionCode
{
    None,
    MissingTransitionAsset,
    MissingTransition,
    NoAnimationClip,
    MultipleAnimationClips,
    ResolutionException,
}

public readonly struct RootMotionClipResolutionDiagnostic
{
    public static RootMotionClipResolutionDiagnostic Success => new RootMotionClipResolutionDiagnostic(RootMotionClipResolutionCode.None, string.Empty);

    public RootMotionClipResolutionCode Code { get; }
    public string Message { get; }

    public RootMotionClipResolutionDiagnostic(RootMotionClipResolutionCode code, string message)
    {
        Code = code;
        Message = message ?? string.Empty;
    }
}
#endif
