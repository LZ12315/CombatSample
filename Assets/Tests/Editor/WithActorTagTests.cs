using System;
using System.Collections.Generic;
using System.Reflection;
using DeiveEx.TagTree;
using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using NUnit.Framework;
using ParadoxNotion.Design;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class WithActorTagTests
{
    private readonly List<Object> _objects = new List<Object>();
    private Dictionary<int, Tag> _registry;
    private Dictionary<int, Tag> _savedTags;
    private Actor _actor;
    private Tag _tag;
    private Tag _otherTag;
    private BehaviourTree _tree;
    private WithActorTag _scope;
    private ProbeNode _child;

    [SetUp]
    public void SetUp()
    {
        // TagTree exposes no public in-memory registration API. Restore the exact registry objects
        // afterward, so these tests neither load repository assets nor invalidate other test fixtures.
        Type manager = typeof(Tag).Assembly.GetType("DeiveEx.TagTree.TagManager");
        _registry = (Dictionary<int, Tag>)manager.GetField("Tags", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        _savedTags = new Dictionary<int, Tag>(_registry);
        manager.GetMethod("LoadTagsFromNames", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { new[] { "Tests.TagScope.Active", "Tests.TagScope.Other" } });
        _tag = Tag.GetTagFromFullName("Tests.TagScope.Active");
        _otherTag = Tag.GetTagFromFullName("Tests.TagScope.Other");
        _actor = CreateActor("Tag Scope Actor");
        _tree = ScriptableObject.CreateInstance<BehaviourTree>();
        _objects.Add(_tree);
        _scope = _tree.AddNode<WithActorTag>();
        _scope.tagToHold = Reference(_tag);
        _child = _scope.AddChild<ProbeNode>();

        // NodeCanvas's public StartGraph is PlayMode-only. Open only its execution gate for this
        // in-memory EditMode harness; exercise public Execute/Reset and graph lifecycle notifications.
        // The fixture does not depend on our node's private methods or fields.
        typeof(Graph).GetProperty(nameof(Graph.isRunning)).SetValue(_tree, true);
    }

    [TearDown]
    public void TearDown()
    {
        _scope?.Reset();
        _scope?.OnGraphStoped();
        _actor?.actorLocomotion?.CancelSimulation();
        if (_tree != null)
            typeof(Graph).GetProperty(nameof(Graph.isRunning)).SetValue(_tree, false);
        for (int i = _objects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
        if (_registry != null && _savedTags != null)
        {
            _registry.Clear();
            foreach (var pair in _savedTags)
                _registry.Add(pair.Key, pair.Value);
        }
    }

    [TestCase(Status.Success)]
    [TestCase(Status.Failure)]
    public void HoldsOnceBeforeChildAndReleasesOnEitherOutcome(Status outcome)
    {
        TagContainer container = _actor.transientTags;
        container.AddTag(_tag); // Another producer already owns one reference.
        _child.OnTick = () => Assert.AreEqual(2, container.GetTagCount(_tag));

        Assert.AreEqual(Status.Running, Tick());
        Assert.AreEqual(Status.Running, Tick());
        _child.Result = outcome;
        Assert.AreEqual(outcome, Tick());
        Assert.AreEqual(1, container.GetTagCount(_tag));
        _scope.Reset();
        _scope.OnGraphStoped();
        Assert.AreEqual(1, container.GetTagCount(_tag), "Cleanup must not consume another producer's reference.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResetOrGraphStopReleasesRunningScopeIdempotently(bool graphStop)
    {
        Assert.AreEqual(Status.Running, Tick());
        if (graphStop)
            _scope.OnGraphStoped();
        else
            _scope.Reset();
        Assert.AreEqual(0, _actor.transientTags.GetTagCount(_tag));
        _scope.OnGraphStoped();
        _scope.Reset();
        Assert.AreEqual(0, _actor.transientTags.GetTagCount(_tag));
    }

    [Test]
    public void PauseReleasesAndFirstResumedExecutionReacquiresOnce()
    {
        Assert.AreEqual(Status.Running, Tick());
        _scope.OnGraphPaused();
        _scope.OnGraphPaused();
        Assert.AreEqual(0, _actor.transientTags.GetTagCount(_tag));
        _scope.OnGraphUnpaused();
        Assert.AreEqual(Status.Running, Tick());
        Assert.AreEqual(Status.Running, Tick());
        Assert.AreEqual(1, _actor.transientTags.GetTagCount(_tag));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CleanupUsesAcquiredTagAndContainerNotChangedConfiguration(bool replaceContainer)
    {
        TagContainer original = _actor.transientTags;
        original.AddTag(_tag);
        Assert.AreEqual(Status.Running, Tick());
        _scope.tagToHold = Reference(_otherTag);
        if (replaceContainer)
        {
            _actor.transientTags = new TagContainer(_otherTag);
            Assert.AreEqual(Status.Failure, Tick(), "Do not continue a child after its Actor container changes.");
        }
        else
        {
            Assert.AreEqual(Status.Running, Tick());
            Assert.AreEqual(0, original.GetTagCount(_otherTag));
            _scope.Reset();
        }
        Assert.AreEqual(1, original.GetTagCount(_tag));
        if (replaceContainer)
            Assert.AreEqual(1, _actor.transientTags.GetTagCount(_otherTag));
    }

    [Test]
    public void InvalidActorContainerOrTagFailsWithoutExecutingChild()
    {
        int calls = 0;
        _child.OnTick = () => calls++;
        _scope.tagToHold = new TagReference();
        Assert.AreEqual(Status.Failure, Tick());
        _scope.Reset();
        _scope.tagToHold = Reference(_tag);
        _actor.transientTags = null;
        Assert.AreEqual(Status.Failure, Tick());
        _scope.Reset();
        var noActor = new GameObject("No Actor");
        _objects.Add(noActor);
        Assert.AreEqual(Status.Failure, _scope.Execute(noActor.transform, null));
        Assert.AreEqual(0, calls);
    }

    [Test]
    public void ChildExceptionReleasesTagBeforePropagating()
    {
        _child.OnTick = () => throw new InvalidOperationException("Probe failure");
        Assert.Throws<InvalidOperationException>(() => Tick());
        Assert.AreEqual(0, _actor.transientTags.GetTagCount(_tag));
    }

    [Test]
    public void ScopeSelectsMutuallyExclusiveGroundStylesAndLeavesAirIndependent()
    {
        var locomotion = _actor.gameObject.AddComponent<ActorLocomotion>();
        _actor.actorLocomotion = locomotion;
        locomotion.Bind(_actor);
        var normal = Asset(Ground(3), TagCondition(inverted: true));
        var strafe = Asset(Ground(3), TagCondition(inverted: false));
        var air = Asset(Ground(12));
        using (var serialized = new SerializedObject(locomotion))
        {
            var assets = serialized.FindProperty("locomotionAssets");
            assets.arraySize = 3;
            assets.GetArrayElementAtIndex(0).objectReferenceValue = normal;
            assets.GetArrayElementAtIndex(1).objectReferenceValue = strafe;
            assets.GetArrayElementAtIndex(2).objectReferenceValue = air;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.AreEqual(normal.Priority, strafe.Priority);
        void Select(LocomotionAsset expected, ActorGroundState state = ActorGroundState.Grounded)
        {
            locomotion.BeginControlTick();
            locomotion.BuildMotionRequest(new LocomotionMotionContext(0.02f,
                state, Vector3.up, Quaternion.identity, default, default));
            Assert.AreSame(expected, locomotion.CurrentAsset);
        }

        Select(normal);
        Assert.AreEqual(Status.Running, Tick());
        Select(strafe);
        Select(air, ActorGroundState.JustLeftGround);
        Select(air, ActorGroundState.Airborne);
        Select(strafe, ActorGroundState.JustLanded);
        _scope.OnGraphPaused();
        Select(normal);
        Assert.AreEqual(Status.Running, Tick());
        Select(strafe);
        _child.Result = Status.Success;
        Assert.AreEqual(Status.Success, Tick());
        Select(normal);
        locomotion.CancelSimulation();
    }

    private Status Tick() => _scope.Execute(_actor.transform, null);

    private Actor CreateActor(string name)
    {
        var owner = new GameObject(name);
        _objects.Add(owner);
        var actor = owner.AddComponent<Actor>();
        actor.transientTags = new TagContainer();
        return actor;
    }

    private static TagReference Reference(Tag tag) =>
        JsonUtility.FromJson<TagReference>($"{{\"TagId\":{tag.Id}}}");

    private static LocomotionGroundStateCondition Ground(int mask) =>
        JsonUtility.FromJson<LocomotionGroundStateCondition>($"{{\"acceptedStates\":{mask}}}");

    private LocomotionActorTagCondition TagCondition(bool inverted) =>
        JsonUtility.FromJson<LocomotionActorTagCondition>(
            $"{{\"invertResult\":{(inverted ? "true" : "false")},\"requiredTag\":{{\"TagId\":{_tag.Id}}},\"targetContainer\":0,\"matchMode\":1}}");

    private LocomotionSetAsset Asset(params LocomotionModeCondition[] conditions)
    {
        var asset = ScriptableObject.CreateInstance<LocomotionSetAsset>();
        _objects.Add(asset);
        using (var serialized = new SerializedObject(asset))
        {
            var entries = serialized.FindProperty("entryConditions");
            entries.arraySize = conditions.Length;
            for (int i = 0; i < conditions.Length; i++)
                entries.GetArrayElementAtIndex(i).managedReferenceValue = conditions[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        return asset;
    }

    [DoNotList]
    public sealed class ProbeNode : BTNode
    {
        public Status Result = Status.Running;
        public Action OnTick;
        protected override Status OnExecute(Component agent, IBlackboard blackboard)
        {
            OnTick?.Invoke();
            return Result;
        }
    }
}
