#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public static class RootMotionOracleEvaluator
{
    public const string PreviewRootName = "__CombatSample_RootMotionOracle_Preview__";

    public static bool TryEvaluate(
        AnimationClip clip,
        AnimationConfig config,
        IReadOnlyList<float> sampleTimes,
        out RootMotionOracleResult result,
        out RootMotionOracleDiagnostic diagnostic)
    {
        return TryEvaluate(
            clip,
            RootMotionBakeSettings.FromLegacy(config),
            config,
            sampleTimes,
            out result,
            out diagnostic);
    }

    public static bool TryEvaluate(
        AnimationClip clip,
        RootMotionBakeSettings settings,
        IReadOnlyList<float> sampleTimes,
        out RootMotionOracleResult result,
        out RootMotionOracleDiagnostic diagnostic)
    {
        return TryEvaluate(clip, settings, null, sampleTimes, out result, out diagnostic);
    }

    private static bool TryEvaluate(
        AnimationClip clip,
        RootMotionBakeSettings settings,
        AnimationConfig legacyConfig,
        IReadOnlyList<float> sampleTimes,
        out RootMotionOracleResult result,
        out RootMotionOracleDiagnostic diagnostic)
    {
        result = null;
        diagnostic = default;

        if (clip == null)
        {
            diagnostic = new RootMotionOracleDiagnostic(RootMotionOracleDiagnosticCode.MissingAnimationClip, "AnimationClip is missing.");
            return false;
        }

        if (settings == null)
        {
            diagnostic = new RootMotionOracleDiagnostic(RootMotionOracleDiagnosticCode.MissingBakeSettings, "Root Motion Bake Settings are missing.");
            return false;
        }

        RootMotionBakeSettingsValidationResult settingsValidation = settings.Validate();
        if (!settingsValidation.IsValid)
        {
            diagnostic = new RootMotionOracleDiagnostic(
                RootMotionOracleDiagnosticCode.InvalidBakeSettings,
                BuildSettingsValidationMessage(settingsValidation));
            return false;
        }

        if (!ValidateSampleTimes(clip, sampleTimes, out diagnostic))
            return false;

        GameObject previewRoot = null;
        PlayableGraph graph = default;

        try
        {
            previewRoot = EditorUtility.CreateGameObjectWithHideFlags(
                PreviewRootName,
                HideFlags.HideAndDontSave);
            previewRoot.SetActive(false);

            GameObject instance = Object.Instantiate(settings.ReferenceRigPrefab, previewRoot.transform, false);
            instance.name = settings.ReferenceRigPrefab.name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1)
            {
                diagnostic = new RootMotionOracleDiagnostic(
                    RootMotionOracleDiagnosticCode.InvalidInstantiatedRig,
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

            RootMotionOracleRecorder recorder = animator.gameObject.AddComponent<RootMotionOracleRecorder>();
            recorder.hideFlags = HideFlags.HideAndDontSave;
            recorder.Initialize(animator);

            previewRoot.SetActive(true);
            animator.Rebind();

            graph = PlayableGraph.Create($"RootMotionOracle:{clip.name}");
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
            recorder.BeginSampling();

            var resultTimes = new List<float>(sampleTimes.Count) { 0f };
            var cumulativePositions = new List<Vector3>(sampleTimes.Count) { Vector3.zero };
            var cumulativeRotations = new List<Quaternion>(sampleTimes.Count) { Quaternion.identity };
            float currentTime = 0f;

            for (int i = 1; i < sampleTimes.Count; i++)
            {
                float targetTime = sampleTimes[i];
                float deltaTime = targetTime - currentTime;
                int callbacksBefore = recorder.CallbackCount;
                graph.Evaluate(deltaTime);

                if (recorder.CallbackCount <= callbacksBefore)
                {
                    diagnostic = new RootMotionOracleDiagnostic(
                        RootMotionOracleDiagnosticCode.MissingAnimatorMoveCallback,
                        $"Animator did not publish root motion while advancing from {currentTime:R}s to {targetTime:R}s.");
                    return false;
                }

                currentTime = targetTime;
                resultTimes.Add(targetTime);
                cumulativePositions.Add(recorder.CumulativePosition);
                cumulativeRotations.Add(recorder.CumulativeRotation);
            }

            result = legacyConfig != null
                ? new RootMotionOracleResult(clip, legacyConfig, resultTimes, cumulativePositions, cumulativeRotations)
                : new RootMotionOracleResult(clip, settings, resultTimes, cumulativePositions, cumulativeRotations);
            diagnostic = RootMotionOracleDiagnostic.Success;
            return true;
        }
        catch (Exception exception)
        {
            diagnostic = new RootMotionOracleDiagnostic(
                RootMotionOracleDiagnosticCode.EvaluationException,
                $"Oracle failed to evaluate '{clip.name}': {exception.GetType().Name}: {exception.Message}");
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

    private static bool ValidateSampleTimes(
        AnimationClip clip,
        IReadOnlyList<float> sampleTimes,
        out RootMotionOracleDiagnostic diagnostic)
    {
        if (sampleTimes == null || sampleTimes.Count < 2)
        {
            diagnostic = new RootMotionOracleDiagnostic(
                RootMotionOracleDiagnosticCode.InvalidSampleTimes,
                "Oracle requires at least two sample times.");
            return false;
        }

        if (Mathf.Abs(sampleTimes[0]) > 1e-6f)
        {
            diagnostic = new RootMotionOracleDiagnostic(
                RootMotionOracleDiagnosticCode.InvalidSampleTimes,
                "The first Oracle sample time must be zero.");
            return false;
        }

        for (int i = 0; i < sampleTimes.Count; i++)
        {
            float time = sampleTimes[i];
            if (float.IsNaN(time) || float.IsInfinity(time) || (i > 0 && !(time > sampleTimes[i - 1])))
            {
                diagnostic = new RootMotionOracleDiagnostic(
                    RootMotionOracleDiagnosticCode.InvalidSampleTimes,
                    $"Oracle sample time at index {i} is invalid or not strictly increasing.");
                return false;
            }
        }

        if (Mathf.Abs(sampleTimes[sampleTimes.Count - 1] - clip.length) > 1e-5f)
        {
            diagnostic = new RootMotionOracleDiagnostic(
                RootMotionOracleDiagnosticCode.InvalidSampleTimes,
                "The final Oracle sample time must equal AnimationClip duration.");
            return false;
        }

        diagnostic = RootMotionOracleDiagnostic.Success;
        return true;
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
}

public enum RootMotionOracleDiagnosticCode
{
    None,
    MissingAnimationClip,
    MissingAnimationConfig,
    MissingBakeSettings,
    InvalidBakeSettings,
    InvalidSampleTimes,
    InvalidInstantiatedRig,
    MissingAnimatorMoveCallback,
    EvaluationException,
}

public readonly struct RootMotionOracleDiagnostic
{
    public static RootMotionOracleDiagnostic Success => new RootMotionOracleDiagnostic(RootMotionOracleDiagnosticCode.None, string.Empty);

    public RootMotionOracleDiagnosticCode Code { get; }
    public string Message { get; }

    public RootMotionOracleDiagnostic(RootMotionOracleDiagnosticCode code, string message)
    {
        Code = code;
        Message = message ?? string.Empty;
    }
}
#endif
