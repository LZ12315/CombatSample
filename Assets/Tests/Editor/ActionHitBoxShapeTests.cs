#if UNITY_EDITOR
using NUnit.Framework;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools.Utils;

public sealed class ActionHitBoxShapeTests
{
    [Test]
    public void HitBoxDefaultsToBoxAndActorRoot()
    {
        var config = new ActionHitBoxConfig();
        Assert.AreEqual(ActionHitBoxShape.Box, config.shape);

        var item = new HitBoxItem();
        item.EditorInitializeNewDefaults();
        Assert.AreEqual(ActionHitBoxShape.Box, item.Config.hitboxConfig.shape);
        Assert.AreEqual(ActionHitBoxAnchor.ActorRoot, item.Config.anchor);
        Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.5f), item.Config.hitboxConfig.size);
    }

    [Test]
    public void RootBinding_ManagedCopyPreservesShapeData()
    {
        ActionAsset asset = ScriptableObject.CreateInstance<ActionAsset>();
        try
        {
            var lane = new GameplayLane();
            var item = new HitBoxItem();
            item.EditorInitializeNewDefaults();
            lane.EditorItems.Add(item);
            asset.Timeline.EditorGameplayLanes.Add(lane);
            ActionAuthoringIdentity.RepairInvalidIds(asset);

            ActionHitBoxConfig copy = ActionEditorCommands.CloneManaged(item.Config.hitboxConfig);
            Assert.AreEqual(ActionHitBoxShape.Box, copy.shape);
            Assert.AreEqual(item.Config.hitboxConfig.size, copy.size);
            Assert.AreEqual(item.Config.hitboxConfig.rotation, copy.rotation);
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void WorldShape_ScalesLocalCenterButKeepsDimensionsInWorldUnits()
    {
        var root = new GameObject("Binding");
        try
        {
            root.transform.position = new Vector3(3f, 4f, 5f);
            root.transform.localScale = new Vector3(2f, 3f, 4f);
            var config = new ActionHitBoxConfig
            {
                shape = ActionHitBoxShape.Box,
                center = new Vector3(1f, 1f, 1f),
                rotation = Quaternion.Euler(0f, 30f, 0f),
                size = new Vector3(2f, 4f, 6f),
            };

            Assert.IsTrue(ActionHitBoxGeometry.TryBuild(root.transform, config, out ActionHitBoxWorldShape shape, out _));
            Assert.That(shape.Center, Is.EqualTo(new Vector3(5f, 7f, 9f)).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(shape.HalfExtents, Is.EqualTo(new Vector3(1f, 2f, 3f)).Using(Vector3ComparerWithEqualsOperator.Instance));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Capsule_UsesTotalHeightAndCollapsesShortBodyToSphere()
    {
        var root = new GameObject("Binding");
        try
        {
            var config = new ActionHitBoxConfig
            {
                shape = ActionHitBoxShape.Capsule,
                height = 0.5f,
                radius = 0.5f,
            };
            Assert.IsTrue(ActionHitBoxGeometry.TryBuild(root.transform, config, out ActionHitBoxWorldShape shape, out _));
            Assert.AreEqual(shape.PointA, shape.PointB);
            Assert.AreEqual(0.5f, shape.Radius);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [TestCase(ActionHitBoxShape.Box)]
    [TestCase(ActionHitBoxShape.Sphere)]
    [TestCase(ActionHitBoxShape.Capsule)]
    public void PhysicsQuery_DetectsColliderForEverySupportedShape(ActionHitBoxShape shapeType)
    {
        var binding = new GameObject("Binding");
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            target.transform.position = new Vector3(0.25f, 0f, 0f);
            target.transform.localScale = Vector3.one * 0.25f;
            Physics.SyncTransforms();
            var config = new ActionHitBoxConfig
            {
                shape = shapeType,
                size = Vector3.one,
                radius = 0.5f,
                height = 1f,
            };
            Assert.IsTrue(ActionHitBoxGeometry.TryBuild(binding.transform, config, out ActionHitBoxWorldShape shape, out _));
            var results = new Collider[8];
            int count = ActionHitBoxGeometry.QueryNonAlloc(shape, results, ~0);
            Assert.Greater(count, 0);
            Assert.Contains(target.GetComponent<Collider>(), results);
        }
        finally
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(binding);
        }
    }

    [Test]
    public void HumanoidAnchor_WithoutHumanoidAvatar_FailsSafely()
    {
        var root = new GameObject("Animator");
        try
        {
            Animator animator = root.AddComponent<Animator>();
            Assert.IsFalse(ActionHitBoxAnchorResolver.TryResolve(
                ActionHitBoxAnchor.RightHand,
                root.transform,
                animator,
                out Transform binding,
                out string failureReason));
            Assert.IsNull(binding);
            Assert.IsNotEmpty(failureReason);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
#endif
