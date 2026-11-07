using System;
using System.Collections.Generic;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Camera binding ownership only; no Brain, scene assets, framing or blend geometry.</summary>
public sealed class CameraRigLifecycleTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private Action _release;
    private CinemachineVirtualCamera _camera;
    private Transform _originalFollow;
    private Transform _originalLookAt;
    private Transform[] _ownedTargets;

    [TearDown]
    public void TearDown()
    {
        _release?.Invoke();
        _release = null;
        for (int i = _objects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [TestCase(false)] [TestCase(true)]
    public void Release_RestoresPreviousBindingsAndDestroysOwnedTargets(bool hardLock)
    {
        BindRig(hardLock);
        Assert.AreNotSame(_originalFollow, _camera.Follow);
        Assert.AreNotSame(_originalLookAt, _camera.LookAt);

        _release();
        _release();

        // Keep failure formatting away from destroyed Transform enumerators.
        Assert.IsTrue(ReferenceEquals(_originalFollow, _camera.Follow), "Previous Follow must be restored.");
        Assert.IsTrue(ReferenceEquals(_originalLookAt, _camera.LookAt), "Previous LookAt must be restored.");
        foreach (Transform target in _ownedTargets)
            Assert.IsTrue(target == null, "Every runtime target must be destroyed on release.");
    }

    [TestCase(false)] [TestCase(true)]
    public void Release_DoesNotOverwriteAReplacementBinding(bool hardLock)
    {
        BindRig(hardLock);
        Transform replacement = Create("Replacement Follow").transform;
        _camera.Follow = replacement;

        _release();
        _release();

        Assert.IsTrue(ReferenceEquals(replacement, _camera.Follow), "A replacement Follow must be preserved.");
        Assert.IsTrue(ReferenceEquals(_originalLookAt, _camera.LookAt),
            "LookAt is still owned and must be returned independently of Follow.");
    }

    private void BindRig(bool hardLock)
    {
        _camera = Create("Camera").AddComponent<CinemachineVirtualCamera>();
        _originalFollow = Create("Original Follow").transform;
        _originalLookAt = Create("Original LookAt").transform;
        _camera.Follow = _originalFollow;
        _camera.LookAt = _originalLookAt;
        Actor player = Create("Player").AddComponent<Actor>();
        Transform enemy = Create("Enemy").transform;
        enemy.position = Vector3.forward * 5f;
        if (hardLock)
        {
            var rig = new HardLockCameraRig(_camera, Vector3.zero);
            _release = rig.DestroyRuntime;
            rig.Refresh(player.transform, enemy, new HardLockCameraRig.Settings
            {
                PlayerWeight = 1f, EnemyWeight = 1f, TargetPadding = 1f,
                Fov = 45f, FramingSize = 0.68f, BaseFollowDistance = 3f, MaxFollowDistance = 11f,
            }, instant: true);
            _ownedTargets = new[] { _camera.Follow, rig.TargetGroup.transform };
        }
        else
        {
            var rig = new SoftLockCameraRig(_camera);
            _release = rig.DestroyRuntime;
            rig.Refresh(player, enemy, new SoftLockCameraRig.Settings
            {
                PlayerWeight = 1f, EnemyWeight = 1f, PlayerRadius = 1f, EnemyRadius = 1f,
            });
            _ownedTargets = new[] { _camera.Follow, rig.TargetGroup.transform,
                rig.TargetGroup.m_Targets[0].target, rig.TargetGroup.m_Targets[1].target };
        }
    }

    private GameObject Create(string name)
    {
        var value = new GameObject(name);
        _objects.Add(value);
        return value;
    }
}
