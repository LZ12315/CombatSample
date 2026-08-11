#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public static class RootMotionBaker
{
    public const int CurrentBakerVersion = 1;
    public const string PreviewRootName = "__CombatSample_RootMotionBaker_Preview__";

    private const double TimeEpsilon = 1e-9;

    public static bool TryBake(
        AnimationClip clip,
        AnimationConfig config,
        out RootMotionBakeResult result,
        out RootMotionBakeDiagnostic diagnostic)
    {
        result = null;
        diagnostic = default;

        if (clip == null)
        {
            diagnostic = new RootMotionBakeDiagnostic(RootMotionBakeDiagnosticCode.MissingAnimationClip, "AnimationClip is missing.");
            return false;
        }

        if (!IsFinite(clip.length) || clip.length <= 0f)
        {
            diagnostic = new RootMotionBakeDiagnostic(
                RootMotionBakeDiagnosticCode.InvalidAnimationClipDuration,
                $"AnimationClip '{clip.name}' must have a finite duration greater than zero.");
            return false;
        }

        if (config == null)
        {
            diagnostic = new RootMotionBakeDiagnostic(RootMotionBakeDiagnosticCode.MissingAnimationConfig, "AnimationConfig is missing.");
            return false;
        }

        RootMotionBakeSettingsValidationResult settingsValidation = config.ValidateRootMotionBakeSettings();
        if (!settingsValidation.IsValid)
        {
            diagnostic = new RootMotionBakeDiagnostic(
                RootMotionBakeDiagnosticCode.InvalidBakeSettings,
                BuildSettingsValidationMessage(settingsValidation));
            return false;
        }

        GameObject previewRoot = null;
        PlayableGraph graph = default;

        try
        {
            previewRoot = EditorUtility.CreateGameObjectWithHideFlags(
                PreviewRootName,
                HideFlags.HideAndDontSave);
            previewRoot.SetActive(false);

            GameObject instance = Object.Instantiate(config.RootMotionReferenceRigPrefab, previewRoot.transform, false);
            instance.name = config.RootMotionReferenceRigPrefab.name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1)
            {
                diagnostic = new RootMotionBakeDiagnostic(
                    RootMotionBakeDiagnosticCode.InvalidInstantiatedRig,
                    $"Instantiated Reference Rig must contain exactly one Animator, but found {animators.Length}.");
                return false;
            }

            Animator animator = animators[0];
            DisableOtherBehaviours(instance, animator);
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.fireEvents = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.applyRootMotion = true;
            animator.enabled = true;

            previewRoot.SetActive(true);
            animator.Rebind();

            Vector3 scale = animator.transform.lossyScale;
            if (!ApproximatelyOne(scale.x) || !ApproximatelyOne(scale.y) || !ApproximatelyOne(scale.z))
            {
                diagnostic = new RootMotionBakeDiagnostic(
                    RootMotionBakeDiagnosticCode.InvalidInstantiatedRig,
                    $"Instantiated Animator must have unit world scale, but found {scale}.");
                return false;
            }

            graph = PlayableGraph.Create($"RootMotionBaker:{clip.name}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetApplyPlayableIK(false);
            clipPlayable.SetTime(0d);
            clipPlayable.SetSpeed(1d);
            clipPlayable.SetDuration(clip.length);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(clipPlayable);
            output.SetWeight(1f);
            graph.Play();
            graph.Evaluate(0f);

            RootMotionTransform baseline = Capture(animator.transform);
            var sampleTimes = new List<float> { 0f };
            var cumulativePositions = new List<Vector3> { Vector3.zero };
            var cumulativeRotations = new List<Quaternion> { Quaternion.identity };

            double duration = clip.length;
            double sampleInterval = 1d / config.RootMotionSampleRate;
            double currentTime = 0d;
            int sampleIndex = 1;

            while (currentTime < duration - TimeEpsilon)
            {
                double targetTime = Math.Min(duration, sampleIndex * sampleInterval);
                double deltaTime = targetTime - currentTime;
                if (deltaTime <= TimeEpsilon)
                    break;

                graph.Evaluate((float)deltaTime);
                currentTime = targetTime;

                RootMotionTransform cumulative = RootMotionTransform.Delta(baseline, Capture(animator.transform));
                float sampleTime = (float)currentTime;
                int previousIndex = sampleTimes.Count - 1;
                if (sampleTime > sampleTimes[previousIndex])
                {
                    sampleTimes.Add(sampleTime);
                    cumulativePositions.Add(cumulative.Position);
                    cumulativeRotations.Add(cumulative.Rotation);
                }
                else
                {
                    // The final double-precision remainder can be smaller than one
                    // float ULP. The Graph still reaches the exact duration, but the
                    // serialized time grid must remain strictly increasing.
                    sampleTimes[previousIndex] = sampleTime;
                    cumulativePositions[previousIndex] = cumulative.Position;
                    cumulativeRotations[previousIndex] = cumulative.Rotation;
                }
                sampleIndex++;
            }

            int finalIndex = sampleTimes.Count - 1;
            if (finalIndex < 1 || Mathf.Abs(sampleTimes[finalIndex] - clip.length) > 1e-5f)
            {
                diagnostic = new RootMotionBakeDiagnostic(
                    RootMotionBakeDiagnosticCode.EvaluationDidNotReachDuration,
                    $"Baker stopped at {sampleTimes[finalIndex]:R}s instead of clip duration {clip.length:R}s.");
                return false;
            }

            result = new RootMotionBakeResult(
                clip,
                config,
                CurrentBakerVersion,
                config.RootMotionSampleRate,
                clip.length,
                sampleTimes,
                cumulativePositions,
                cumulativeRotations);
            diagnostic = RootMotionBakeDiagnostic.Success;
            return true;
        }
        catch (Exception exception)
        {
            diagnostic = new RootMotionBakeDiagnostic(
                RootMotionBakeDiagnosticCode.EvaluationException,
                $"Failed to bake '{clip.name}': {exception.GetType().Name}: {exception.Message}");
            return false;
        }
        finally
        {
            if (graph.IsValid())
                graph.Destroy();

            if (previewRoot != null)
                Object.DestroyImmediate(previewRoot);
        }
    }

    private static RootMotionTransform Capture(Transform transform)
    {
        return new RootMotionTransform(transform.position, transform.rotation);
    }

    private static void DisableOtherBehaviours(GameObject instance, Animator selectedAnimator)
    {
        Behaviour[] behaviours = instance.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour != selectedAnimator)
                behaviour.enabled = false;
        }
    }

    private static string BuildSettingsValidationMessage(RootMotionBakeSettingsValidationResult validation)
    {
        var messages = new List<string>();
        for (int i = 0; i < validation.Issues.Count; i++)
            messages.Add(validation.Issues[i].Message);
        return string.Join(" ", messages);
    }

    private static bool ApproximatelyOne(float value)
    {
        return Mathf.Abs(value - 1f) <= 1e-4f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public enum RootMotionBakeDiagnosticCode
{
    None,
    MissingAnimationClip,
    InvalidAnimationClipDuration,
    MissingAnimationConfig,
    InvalidBakeSettings,
    InvalidInstantiatedRig,
    EvaluationDidNotReachDuration,
    EvaluationException,
}

public readonly struct RootMotionBakeDiagnostic
{
    public static RootMotionBakeDiagnostic Success => new RootMotionBakeDiagnostic(RootMotionBakeDiagnosticCode.None, string.Empty);

    public RootMotionBakeDiagnosticCode Code { get; }
    public string Message { get; }

    public RootMotionBakeDiagnostic(RootMotionBakeDiagnosticCode code, string message)
    {
        Code = code;
        Message = message ?? string.Empty;
    }
}
#endif
