using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerInputOwnershipTests
{
    private readonly List<Object> _objects = new List<Object>();
    private PlayerInputController _previousInstance;

    [SetUp]
    public void SetUp()
    {
        _previousInstance = PlayerInputController.Instance;
        SetPlayerInputControllerInstance(null);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] != null)
                Object.DestroyImmediate(_objects[i]);
        }

        _objects.Clear();
        SetPlayerInputControllerInstance(_previousInstance);
        _previousInstance = null;
    }

    [Test]
    public void PlayerResolver_ProducesLocomotionIntentFromRawMove()
    {
        Actor actor = CreateActor("Resolver Actor");
        var resolver = new PlayerLocomotionIntentResolver();

        LocomotionIntent intent = resolver.Resolve(actor, null, new Vector2(2f, 0f));

        Assert.That(intent.WorldMoveDirection.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(intent.WorldMoveDirection.z, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(intent.MoveStrength, Is.EqualTo(1f).Within(0.0001f));
        Assert.AreEqual(Vector3.zero, intent.FacingDirection);
    }

    [Test]
    public void LocomotionRuntime_ConsumesPendingIntentOnceAndThenBecomesIdle()
    {
        var runtime = new LocomotionRuntime();

        runtime.SetIntent(new LocomotionIntent
        {
            WorldMoveDirection = Vector3.right,
            MoveStrength = 0.75f,
            FacingDirection = Vector3.right,
        });

        Assert.IsTrue(runtime.HasPendingIntent);
        Assert.That(runtime.PendingIntent.MoveStrength, Is.EqualTo(0.75f).Within(0.0001f));

        runtime.Tick(baseSpeed: 6f, airControlFactor: 1f, isAirborne: false);

        Assert.IsFalse(runtime.HasPendingIntent);
        Assert.That(runtime.EffectiveIntent.MoveStrength, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(runtime.CachedVelocity.x, Is.GreaterThan(0f));

        runtime.Tick(baseSpeed: 6f, airControlFactor: 1f, isAirborne: false);

        Assert.IsFalse(runtime.HasPendingIntent);
        Assert.That(runtime.EffectiveIntent.MoveStrength, Is.EqualTo(0f).Within(0.0001f));
        Assert.AreEqual(Vector3.zero, runtime.CachedVelocity);
    }

    [Test]
    public void InputSequence_CheckIsReadOnlyAndClaimConsumesAtomically()
    {
        Actor actor = CreateActor("Input Sequence Actor");
        PlayerInputController input = CreatePlayerInput(actor);
        var condition = new InputSequenceCondition();
        SetPrivateField(condition, "inputSequence", new List<InputCheckBase>
        {
            new ButtonInputCheck
            {
                requiredButtons = Enums.InputButton.LightAttack,
                requiredState = Enums.ButtonState.ShortPress,
            },
        });

        InvokePrivate(input, "SendButtonInputData", Enums.InputButton.LightAttack, Enums.ButtonState.ShortPress);

        Assert.IsTrue(condition.Check(actor));
        Assert.IsFalse(input.InputHistory[0].IsConsumed);

        condition.OnClaim(actor);

        Assert.IsTrue(input.InputHistory[0].IsConsumed);
        condition.OnClaim(actor);
        Assert.IsTrue(input.InputHistory[0].IsConsumed);
    }

    private Actor CreateActor(string name)
    {
        var owner = new GameObject(name);
        _objects.Add(owner);
        return owner.AddComponent<Actor>();
    }

    private PlayerInputController CreatePlayerInput(Actor actor)
    {
        var owner = new GameObject("PlayerInputController Test Owner");
        _objects.Add(owner);
        PlayerInputController input = owner.AddComponent<PlayerInputController>();
        input.controlledActor = actor;
        SetPlayerInputControllerInstance(input);
        return input;
    }

    private static void SetPlayerInputControllerInstance(PlayerInputController value)
    {
        FieldInfo field = typeof(PlayerInputController).GetField(
            "<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(null, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Missing method {methodName} on {target.GetType().Name}.");
        method.Invoke(target, arguments);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}
