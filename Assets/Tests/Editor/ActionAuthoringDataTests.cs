#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ActionAuthoringDataTests
{
    private const string TempAssetPath = "Assets/__ActionAuthoringDataTests.asset";

    [TearDown]
    public void TearDown()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(TempAssetPath) != null)
            AssetDatabase.DeleteAsset(TempAssetPath);
    }

    [Test]
    public void Duration_UsesAllContentAndFixedSixtyHertz()
    {
        ActionAsset asset = ScriptableObject.CreateInstance<ActionAsset>();
        GameplayLane lane = new GameplayLane();
        var point = new ImpulseItem();
        point.EditorSetFrame(12);
        var range = new MotionPolicyItem();
        range.EditorSetTiming(20, 4);
        lane.EditorItems.Add(point);
        lane.EditorItems.Add(range);
        asset.Timeline.EditorGameplayLanes.Add(lane);

        Assert.AreEqual(24, asset.Timeline.DurationFrames);
        Object.DestroyImmediate(asset);
    }

    [Test]
    public void Duration_EmptyTimelineIsOneFrame()
    {
        ActionAsset asset = ScriptableObject.CreateInstance<ActionAsset>();
        Assert.AreEqual(1, asset.Timeline.DurationFrames);
        Object.DestroyImmediate(asset);
    }

    [Test]
    public void IdentityRepair_IsStableAcrossReorderAndSerialization()
    {
        ActionAsset asset = ScriptableObject.CreateInstance<ActionAsset>();
        GameplayLane lane = new GameplayLane();
        lane.EditorItems.Add(new ImpulseItem());
        lane.EditorItems.Add(new TagItem());
        asset.Timeline.EditorGameplayLanes.Add(lane);

        Assert.AreEqual(3, ActionAuthoringIdentity.RepairInvalidIds(asset));
        string laneId = lane.EditorId;
        string firstItemId = lane.EditorItems[0].EditorId;
        string secondItemId = lane.EditorItems[1].EditorId;

        lane.EditorItems.Reverse();
        Assert.AreEqual(secondItemId, lane.EditorItems[0].EditorId);
        Assert.AreEqual(firstItemId, lane.EditorItems[1].EditorId);
        Assert.AreEqual(laneId, lane.EditorId);

        AssetDatabase.CreateAsset(asset, TempAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(TempAssetPath, ImportAssetOptions.ForceUpdate);
        ActionAsset reloaded = AssetDatabase.LoadAssetAtPath<ActionAsset>(TempAssetPath);
        GameplayLane reloadedLane = reloaded.Timeline.EditorGameplayLanes[0];
        Assert.AreEqual(laneId, reloadedLane.EditorId);
        Assert.AreEqual(secondItemId, reloadedLane.EditorItems[0].EditorId);
        Assert.AreEqual(firstItemId, reloadedLane.EditorItems[1].EditorId);
    }

    [Test]
    public void IdentityRepair_IsUndoableAndRedoable()
    {
        ActionAsset asset = ScriptableObject.CreateInstance<ActionAsset>();
        GameplayLane lane = new GameplayLane();
        asset.Timeline.EditorGameplayLanes.Add(lane);
        AssetDatabase.CreateAsset(asset, TempAssetPath);

        Assert.AreEqual(1, ActionAuthoringIdentity.RepairInvalidIds(asset));
        string repairedId = lane.EditorId;
        Assert.IsTrue(ActionAuthoringIdentity.IsValidEditorId(repairedId));

        Undo.PerformUndo();
        Assert.IsTrue(string.IsNullOrEmpty(lane.EditorId));
        Undo.PerformRedo();
        Assert.AreEqual(repairedId, lane.EditorId);
    }

    [Test]
    public void SerializeReference_PreservesAllV1ItemTypes()
    {
        ActionAsset asset = ScriptableObject.CreateInstance<ActionAsset>();
        GameplayLane lane = new GameplayLane();
        lane.EditorItems.Add(new ImpulseItem());
        lane.EditorItems.Add(new HitBoxItem());
        lane.EditorItems.Add(new RootMotionItem());
        lane.EditorItems.Add(new SelfRotationItem());
        lane.EditorItems.Add(new VelocityOverrideItem());
        lane.EditorItems.Add(new MotionPolicyItem());
        lane.EditorItems.Add(new TagItem());
        asset.Timeline.EditorGameplayLanes.Add(lane);
        ActionAuthoringIdentity.RepairInvalidIds(asset);

        AssetDatabase.CreateAsset(asset, TempAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(TempAssetPath, ImportAssetOptions.ForceUpdate);
        ActionAsset reloaded = AssetDatabase.LoadAssetAtPath<ActionAsset>(TempAssetPath);
        var items = reloaded.Timeline.EditorGameplayLanes[0].EditorItems;

        Assert.IsInstanceOf<ImpulseItem>(items[0]);
        Assert.IsInstanceOf<HitBoxItem>(items[1]);
        Assert.IsInstanceOf<RootMotionItem>(items[2]);
        Assert.IsInstanceOf<SelfRotationItem>(items[3]);
        Assert.IsInstanceOf<VelocityOverrideItem>(items[4]);
        Assert.IsInstanceOf<MotionPolicyItem>(items[5]);
        Assert.IsInstanceOf<TagItem>(items[6]);
    }

    [Test]
    public void Validator_ReportsInvalidRangeAndRootMotionData()
    {
        ActionAsset asset = ScriptableObject.CreateInstance<ActionAsset>();
        GameplayLane lane = new GameplayLane();
        var rootMotion = new RootMotionItem();
        rootMotion.EditorSetTiming(-1, 0);
        lane.EditorItems.Add(rootMotion);
        asset.Timeline.EditorGameplayLanes.Add(lane);

        ActionAuthoringValidationResult validation = ActionAuthoringValidator.Validate(asset);
        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(System.Array.Exists(
            System.Linq.Enumerable.ToArray(validation.Issues),
            issue => issue.Code == ActionAuthoringValidationCode.InvalidTiming));
        Assert.IsTrue(System.Array.Exists(
            System.Linq.Enumerable.ToArray(validation.Issues),
            issue => issue.Code == ActionAuthoringValidationCode.InvalidConfig));
        Object.DestroyImmediate(asset);
    }
}
#endif
