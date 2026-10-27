using System.Collections.Generic;
using NodeCanvas.Framework;
using NodeCanvas.Framework.Internal;
using NodeCanvas.Tasks.Actions;
using NodeCanvas.Tasks.Conditions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NodeCanvasActorTargetTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }

    [Test]
    public void Distance_UsesExplicitActorAndRespectsHorizontalOnly()
    {
        Actor self = CreateActor("Self", Vector3.zero);
        Actor target = CreateActor("Explicit Target", new Vector3(0f, 5f, 2f));
        SetStoredTarget(self, CreateActor("Stored Target", Vector3.right * 100f).gameObject);
        var task = new CheckTargetDistanceTask
        {
            target = new BBParameter<Actor>(target),
            maxDistance = new BBParameter<float>(3f),
            horizontalOnly = new BBParameter<bool>(true),
        };
        Bind(task, self);

        // A Transform owner must resolve its own Actor, not require a separate Self variable.
        Assert.IsTrue(task.CheckOnce(self.transform, null));
        task.horizontalOnly.value = false;
        Assert.IsFalse(task.CheckOnce(self.transform, null));
        task.target.value = null;
        Assert.IsFalse(task.CheckOnce(self.transform, null), "An empty target must not fall back to Combater.");
    }

    [Test]
    public void Angle_UsesExplicitActorAndRejectsMissingTarget()
    {
        Actor self = CreateActor("Self", Vector3.zero);
        Actor target = CreateActor("Explicit Target", new Vector3(0f, 5f, 2f));
        SetStoredTarget(self, CreateActor("Stored Target", Vector3.right * 2f).gameObject);
        var task = new CheckTargetAngleTask { target = new BBParameter<Actor>(target), maxAngle = 30f };
        Bind(task, self);

        Assert.IsTrue(task.CheckOnce(self.transform, null));
        target.transform.position = Vector3.right * 2f;
        Assert.IsFalse(task.CheckOnce(self.transform, null));
        task.target.value = null;
        Assert.IsFalse(task.CheckOnce(self.transform, null));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Movement_UsesExplicitActorAndFailsWithoutIt(bool strafe)
    {
        Actor self = CreateActor("Self", Vector3.zero);
        Actor target = CreateActor("Explicit Target", Vector3.forward * 3f);
        SetStoredTarget(self, CreateActor("Stored Target", Vector3.left * 3f).gameObject);
        self.actorLocomotion = self.gameObject.AddComponent<ActorLocomotion>();
        self.actorLocomotion.Bind(self);
        var targetParameter = new BBParameter<Actor>(target);
        ActionTask task = strafe
            ? (ActionTask)new StrafeAroundCombatTargetTask
            {
                target = targetParameter,
                side = StrafeAroundCombatTargetTask.StrafeSide.Right,
                duration = 10f,
            }
            : new MoveTowardCombatTargetTask { target = targetParameter };
        Bind(task, self);

        Assert.AreEqual(strafe ? Status.Running : Status.Success, task.Execute(self.transform, null));
        self.actorLocomotion.BeginControlTick();
        Assert.IsTrue(self.actorLocomotion.TryGetControlIntent(out LocomotionIntent intent));
        Assert.That(Vector3.Dot(intent.WorldMoveDirection, strafe ? Vector3.right : Vector3.forward),
            Is.GreaterThan(0.99f));
        Assert.That(Vector3.Dot(intent.FacingDirection, Vector3.forward), Is.GreaterThan(0.99f));

        task.EndAction(null);
        targetParameter.value = null;
        Assert.AreEqual(Status.Failure, task.Execute(self.transform, null));
    }

    [Test]
    public void GetCombatTarget_WritesActorSnapshotAndClearsItOnNextFailedRead()
    {
        Actor self = CreateActor("Self", Vector3.zero);
        Actor target = CreateActor("Stored Target", Vector3.forward);
        SetStoredTarget(self, target.gameObject);
        var blackboard = new BlackboardSource();
        var output = new Variable<Actor> { name = "Target" };
        blackboard.variables.Add(output.name, output);
        var task = new GetCombatTargetTask
        {
            saveAs = new BBParameter<Actor> { bb = blackboard, name = output.name },
        };
        task.SetOwnerSystem(new TestTaskSystem(self.transform, blackboard));

        Assert.AreEqual(Status.Success, task.Execute(self.transform, blackboard));
        Assert.AreSame(target, output.value);
        SetStoredTarget(self, null);
        Assert.AreSame(target, output.value, "The output is a snapshot, not a live Combater binding.");
        Assert.AreEqual(Status.Failure, task.Execute(self.transform, blackboard));
        Assert.IsNull(output.value);
    }

    [TestCase(0)] // Missing Combater.
    [TestCase(1)] // Combater with no stored target.
    [TestCase(2)] // Stored GameObject without Actor.
    public void GetCombatTarget_InvalidSourceClearsPreviousOutput(int source)
    {
        Actor self = CreateActor("Self", Vector3.zero);
        Actor previous = CreateActor("Previous Target", Vector3.forward);
        if (source != 0)
        {
            GameObject stored = null;
            if (source == 2)
            {
                stored = new GameObject("Non Actor Target");
                _objects.Add(stored);
            }
            SetStoredTarget(self, stored);
        }
        var output = new BBParameter<Actor>(previous);
        var task = new GetCombatTargetTask { saveAs = output };
        Bind(task, self);

        Assert.AreEqual(Status.Failure, task.Execute(self.transform, null));
        Assert.IsNull(output.value);
    }

    private Actor CreateActor(string name, Vector3 position)
    {
        var gameObject = new GameObject(name);
        _objects.Add(gameObject);
        gameObject.transform.position = position;
        return gameObject.AddComponent<Actor>();
    }

    private static void SetStoredTarget(Actor self, GameObject target)
    {
        if (self.combater == null)
            self.combater = self.gameObject.AddComponent<ActorCombater>();
        // Seed the serialized Inspector state, without invoking target acquisition or private lifecycle methods.
        using (var serialized = new SerializedObject(self.combater))
        {
            serialized.FindProperty("combatTarget").objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void Bind(Task task, Actor self) => task.SetOwnerSystem(new TestTaskSystem(self.transform, null));

    private sealed class TestTaskSystem : ITaskSystem
    {
        public TestTaskSystem(Component agent, IBlackboard blackboard)
        {
            this.agent = agent;
            this.blackboard = blackboard;
        }

        public Component agent { get; }
        public IBlackboard blackboard { get; }
        public Object contextObject => agent;
        public float elapsedTime => 0f;
        public float deltaTime => 0f;
        public void UpdateTasksOwner() { }
        public void SendEvent(string name, object value, object sender) { }
        public void SendEvent<T>(string name, T value, object sender) { }
    }
}
