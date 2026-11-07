#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DeiveEx.TagTree;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class LocomotionSelfTagTests
{
    private readonly List<Object> _assets = new();
    private Dictionary<int, Tag> _registry;
    private Dictionary<int, Tag> _savedTags;
    private GameObject _owner;
    private Actor _actor;
    private ActorLocomotion _locomotion;
    private ActionPlayer _player;
    private Tag _tag;
    private Tag _otherTag;
    private LocomotionMixerAsset _asset;

    [SetUp]
    public void SetUp()
    {
        // TagTree has no public in-memory registration API. Preserve the registry so this
        // fixture needs no repository tag assets and cannot invalidate other fixtures' tags.
        Type manager = typeof(Tag).Assembly.GetType("DeiveEx.TagTree.TagManager");
        _registry = (Dictionary<int, Tag>)manager.GetField("Tags", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        _savedTags = new Dictionary<int, Tag>(_registry);
        manager.GetMethod("LoadTagsFromNames", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { new[] { "Tests.Locomotion.Base", "Tests.Locomotion.Other" } });
        _tag = Tag.GetTagFromFullName("Tests.Locomotion.Base");
        _otherTag = Tag.GetTagFromFullName("Tests.Locomotion.Other");

        _owner = new GameObject("Locomotion SelfTag Actor");
        _actor = _owner.AddComponent<Actor>();
        _actor.transientTags = new TagContainer();
        _locomotion = _owner.AddComponent<ActorLocomotion>();
        _actor.actorLocomotion = _locomotion;
        _locomotion.Bind(_actor);
        _player = _owner.AddComponent<ActionPlayer>();
        _actor.actionPlayer = _player;
        // EditMode does not guarantee Awake for ordinary MonoBehaviours. Use the serialized
        // dependency rather than invoking private lifecycle methods in this contract fixture.
        JsonUtility.FromJsonOverwrite($"{{\"_actor\":{{\"instanceID\":{_actor.GetInstanceID()}}}}}", _player);
        _asset = Locomotion(_tag);
        LocomotionContractTests.ConfigureAssets(_locomotion, _asset);
    }

    [TearDown]
    public void TearDown()
    {
        _player?.StopAction();
        _locomotion?.CancelSimulation();
        if (_owner != null)
            Object.DestroyImmediate(_owner);
        foreach (Object asset in _assets)
            Object.DestroyImmediate(asset);
        _assets.Clear();
        if (_registry != null && _savedTags != null)
        {
            _registry.Clear();
            foreach (var pair in _savedTags)
                _registry.Add(pair.Key, pair.Value);
        }
    }

    [Test]
    public void ActionTakeover_RemovesImmediatelyEvenWhenMovementIsAllowed_AndStopRestoresOnce()
    {
        Tick(); // Idle is still base locomotion.
        Tick();
        AssertCount(_tag, 1);
        Begin(Action());
        AssertCount(_tag, 0);
        _locomotion.SetLocomotionIntent(new LocomotionIntent
        {
            WorldMoveDirection = Vector3.forward,
            MoveStrength = 1f,
        });
        Tick();
        Assert.That(_locomotion.DebugLocomotionVelocity.sqrMagnitude, Is.GreaterThan(0f));
        Assert.That(_locomotion.CurrentAsset, Is.SameAs(_asset));
        AssertCount(_tag, 0);
        _player.Pause();
        Tick();
        AssertCount(_tag, 0);
        _player.StopAction();
        AssertCount(_tag, 1); // No extra motion tick required.
        _player.StopAction();
        Tick();
        AssertCount(_tag, 1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CompletionRestoresTags_ButLoopRestartRemainsSuppressed(bool loop)
    {
        Tick();
        ActionAsset action = Action(); // An empty timeline lasts one frame.
        JsonUtility.FromJsonOverwrite($"{{\"isLoop\":{(loop ? "true" : "false")}}}", action);
        Begin(action);
        bool finishedWithTag = false;
        _player.OnActionFinished += _ => finishedWithTag = _actor.transientTags.GetTagCount(_tag) == 1;
        Assert.That(_player.PlayActionFrame(CombatSimulationTiming.FixedDeltaTime), Is.True);
        _player.FinishActionFrame();
        Assert.That(_player.CurrentAction != null, Is.EqualTo(loop));
        AssertCount(_tag, loop ? 0 : 1);
        Assert.That(finishedWithTag, Is.EqualTo(!loop));
    }

    [Test]
    public void SelectionDuringAction_RestoresOnlyLatestAsset_WithoutConsumingOtherTagOwners()
    {
        _actor.AddTag(_tag, ActorTagContainerType.Transient);
        Tick();
        AssertCount(_tag, 2);
        Begin(Action());
        AssertCount(_tag, 1);
        var other = Locomotion(_otherTag);
        LocomotionContractTests.ConfigureAssets(_locomotion, other);
        Tick();
        Assert.That(_locomotion.CurrentAsset, Is.SameAs(other));
        AssertCount(_otherTag, 0);
        Begin(Action()); // Replacement Actions must not leave base tags acquired.
        AssertCount(_tag, 1);
        AssertCount(_otherTag, 0);
        _player.StopAction();
        AssertCount(_tag, 1);
        AssertCount(_otherTag, 1);
        _locomotion.CancelSimulation();
        AssertCount(_tag, 1);
        AssertCount(_otherTag, 0);
    }

    [Test]
    public void ActionBeforeFirstSelection_AndCancelledLocomotion_DoNotAcquireTags()
    {
        Begin(Action());
        Tick();
        Assert.That(_locomotion.CurrentAsset, Is.SameAs(_asset));
        AssertCount(_tag, 0);
        _locomotion.CancelSimulation();
        _player.StopAction();
        AssertCount(_tag, 0);
    }

    [Test]
    public void FailedActionStart_RestoresBaseTagsAfterRuntimeCleanup()
    {
        Tick();
        ActionAsset invalid = Action();
        var segment = new AnimationSegment();
        segment.EditorSetData(0, null, 0f, 1f, 1f);
        invalid.Timeline.EditorAnimationSegments.Add(segment);
        Assert.That(_player.TryBeginAction(invalid, ActionContext.ForSelf(_actor), out _), Is.False);
        Assert.That(_player.CurrentAction, Is.Null);
        AssertCount(_tag, 1);
        Tick();
        AssertCount(_tag, 1);
    }

    private LocomotionMixerAsset Locomotion(Tag tag)
    {
        var asset = ScriptableObject.CreateInstance<LocomotionMixerAsset>();
        _assets.Add(asset);
        // Duplicate entries must still own only one tag reference.
        JsonUtility.FromJsonOverwrite(
            $"{{\"selfTags\":[{{\"TagId\":{tag.Id}}},{{\"TagId\":{tag.Id}}}]}}", asset);
        return asset;
    }

    private ActionAsset Action()
    {
        var asset = ScriptableObject.CreateInstance<ActionAsset>();
        asset.MotionPolicy.movement = ActionMovementControl.Allow;
        asset.MotionPolicy.turning = ActionTurnControl.Normal;
        _assets.Add(asset);
        return asset;
    }

    private void Begin(ActionAsset action) =>
        Assert.That(_player.TryBeginAction(action, ActionContext.ForSelf(_actor), out string reason), Is.True, reason);

    private void Tick()
    {
        _locomotion.BeginControlTick();
        _locomotion.BuildMotionRequest(new LocomotionMotionContext(
            0.1f, ActorGroundState.Grounded, Vector3.up, Quaternion.identity, default, default));
    }

    private void AssertCount(Tag tag, int expected) =>
        Assert.That(_actor.transientTags.GetTagCount(tag), Is.EqualTo(expected));
}
#endif
