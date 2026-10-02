#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Absolute-time pose sampling in a fixed reference space, independent of seek history.</summary>
internal sealed class ActionPreviewPoseSampler : IDisposable
{
    private readonly Animator _animator;
    private readonly Transform[] _transforms;
    private readonly Vector3[] _positions;
    private readonly Quaternion[] _rotations;
    private readonly Vector3[] _scales;
    private PlayableGraph _graph;
    private AnimationClipPlayable _playable;
    private AnimationClip _clip;

    internal ActionPreviewPoseSampler(GameObject instance, Animator animator)
    {
        _animator = animator;
        _animator.Rebind();
        RootPosition = instance.transform.position;
        RootRotation = instance.transform.rotation;
        _transforms = instance.GetComponentsInChildren<Transform>(true);
        _positions = new Vector3[_transforms.Length];
        _rotations = new Quaternion[_transforms.Length];
        _scales = new Vector3[_transforms.Length];
        for (int i = 0; i < _transforms.Length; i++)
        {
            _positions[i] = _transforms[i].localPosition;
            _rotations[i] = _transforms[i].localRotation;
            _scales[i] = _transforms[i].localScale;
        }
    }

    internal Vector3 RootPosition { get; }
    internal Quaternion RootRotation { get; }

    internal void Sample(AnimationClip clip, double sourceTime)
    {
        // The previous preview frame's world motion and any unkeyed bone channels must never
        // become inputs to the next sample. This also covers seeks within the same Clip.
        RestoreBaseline();
        if (clip == null)
        {
            Invalidate();
            _animator.Rebind();
            RestoreBaseline();
            return;
        }

        EnsureGraph(clip);
        // SetTime also moves current time to previous time. Setting it twice makes the
        // sample a zero-length seek, rather than a root-motion delta from the last visited time.
        _playable.SetTime(sourceTime);
        _playable.SetTime(sourceTime);
        _graph.Evaluate(0f);
    }

    internal void Invalidate()
    {
        if (_graph.IsValid()) _graph.Destroy();
        _graph = default;
        _playable = default;
        _clip = null;
    }

    public void Dispose() => Invalidate();

    private void EnsureGraph(AnimationClip clip)
    {
        if (_graph.IsValid() && _clip == clip) return;
        Invalidate();
        _animator.Rebind();
        // Rebind may write Animator defaults; preserve the captured character reference pose.
        RestoreBaseline();
        _graph = PlayableGraph.Create($"ActionPreview:{clip.name}");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        _playable = AnimationClipPlayable.Create(_graph, clip);
        _playable.SetApplyFootIK(false);
        _playable.SetApplyPlayableIK(false);
        _playable.SetSpeed(0d);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Pose", _animator);
        output.SetSourcePlayable(_playable);
        output.SetWeight(1f);
        _graph.Play();
        _clip = clip;
    }

    private void RestoreBaseline()
    {
        for (int i = 0; i < _transforms.Length; i++)
        {
            Transform transform = _transforms[i];
            if (transform == null) continue;
            transform.localPosition = _positions[i];
            transform.localRotation = _rotations[i];
            transform.localScale = _scales[i];
        }
    }
}
#endif
