#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class ActionPreviewPoseTests
{
    [Test]
    public void AbsolutePose_IsIndependentOfSeekOrderAndPreviouslyAppliedWorldMotion()
    {
        using (var rig = new TestRig())
        using (var sampler = new ActionPreviewPoseSampler(rig.Root, rig.Animator))
        {
            foreach (double previousTime in new[] { 0.9d, 0d, 1d, 0.25d, 0.251d, 0.249d })
            {
                sampler.Sample(rig.PositionClip, previousTime);
                rig.Root.transform.SetPositionAndRotation(new Vector3(200f, 0f, -100f), Quaternion.Euler(0f, 90f, 0f));
                rig.Bone.localScale = Vector3.one * 2f;
                sampler.Sample(rig.PositionClip, 0.25d);

                Assert.That(Vector3.Distance(new Vector3(1f, 1f, 0f), rig.Bone.localPosition), Is.LessThan(0.00001f),
                    $"Unexpected pose: {rig.Bone.localPosition}");
                Assert.AreEqual(Vector3.one, rig.Bone.localScale);
                Assert.That(Vector3.Distance(Vector3.zero, rig.Root.transform.position), Is.LessThan(0.00001f));
                Assert.That(Quaternion.Angle(Quaternion.identity, rig.Root.transform.rotation), Is.LessThan(0.001f));
            }
        }
    }

    [Test]
    public void ClipSwitchEmptyPoseAndInvalidation_RestoreUnkeyedChannelsAndResampleChangedClip()
    {
        using (var rig = new TestRig())
        using (var sampler = new ActionPreviewPoseSampler(rig.Root, rig.Animator))
        {
            sampler.Sample(rig.PositionClip, 0.75d);
            Assert.That(rig.Bone.localPosition.x, Is.EqualTo(3f).Within(0.00001f));
            sampler.Sample(rig.ScaleClip, 0.5d);
            Assert.That(Vector3.Distance(Vector3.up, rig.Bone.localPosition), Is.LessThan(0.00001f),
                "The new Clip must not inherit position channels from the old Clip.");
            Assert.That(rig.Bone.localScale.x, Is.EqualTo(2f).Within(0.00001f));

            sampler.Sample(null, 0d);
            Assert.That(Vector3.Distance(Vector3.up, rig.Bone.localPosition), Is.LessThan(0.00001f));
            Assert.AreEqual(Vector3.one, rig.Bone.localScale);
            sampler.Sample(rig.PositionClip, 0.25d);
            rig.PositionClip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 8f));
            sampler.Invalidate();
            sampler.Sample(rig.PositionClip, 0.25d);
            Assert.That(rig.Bone.localPosition.x, Is.EqualTo(2f).Within(0.00001f),
                "Changing a Clip in place must refresh the native playable even at the same time.");
        }
    }

    private sealed class TestRig : System.IDisposable
    {
        internal readonly GameObject Root;
        internal readonly Animator Animator;
        internal readonly Transform Bone;
        internal readonly AnimationClip PositionClip;
        internal readonly AnimationClip ScaleClip;

        internal TestRig()
        {
            Root = new GameObject("Preview Pose Test");
            var animatorObject = new GameObject("Rig");
            animatorObject.transform.SetParent(Root.transform, false);
            Animator = animatorObject.AddComponent<Animator>();
            Animator.applyRootMotion = false;
            Animator.fireEvents = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Bone = new GameObject("Bone").transform;
            Bone.SetParent(animatorObject.transform, false);
            Bone.localPosition = Vector3.up;
            PositionClip = new AnimationClip();
            PositionClip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 4f));
            PositionClip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.y", AnimationCurve.Constant(0f, 1f, 1f));
            PositionClip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Constant(0f, 1f, 0f));
            ScaleClip = new AnimationClip();
            ScaleClip.SetCurve("Bone", typeof(Transform), "m_LocalScale.x", AnimationCurve.Linear(0f, 1f, 1f, 3f));
            ScaleClip.SetCurve("Bone", typeof(Transform), "m_LocalScale.y", AnimationCurve.Constant(0f, 1f, 1f));
            ScaleClip.SetCurve("Bone", typeof(Transform), "m_LocalScale.z", AnimationCurve.Constant(0f, 1f, 1f));
        }

        public void Dispose()
        {
            Object.DestroyImmediate(Root);
            Object.DestroyImmediate(PositionClip);
            Object.DestroyImmediate(ScaleClip);
        }
    }
}
#endif
