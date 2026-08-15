using NUnit.Framework;
using UnityEngine;

public sealed class ActionContextContractTests
{
    [Test]
    public void WithDirection_NormalizesAndMarksDirectionWithoutMutatingOriginal()
    {
        var owner = new GameObject("ActionContext Owner");
        try
        {
            ActionContext original = ActionContext.ForSelf(owner);
            ActionContext withDirection = original.WithDirection(new Vector3(2f, 0f, 0f));

            Assert.IsFalse(original.HasDirection);
            Assert.IsTrue(withDirection.HasDirection);
            Assert.AreEqual(Vector3.right, withDirection.Direction);
            Assert.AreSame(owner, withDirection.Instigator);
            Assert.AreSame(owner, withDirection.Target);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void WithDirection_RejectsZeroAndNonFiniteValues()
    {
        Assert.Throws<System.ArgumentException>(() => ActionContext.None.WithDirection(Vector3.zero));
        Assert.Throws<System.ArgumentException>(() => ActionContext.None.WithDirection(new Vector3(float.NaN, 0f, 0f)));
        Assert.Throws<System.ArgumentException>(() => ActionContext.None.WithDirection(new Vector3(float.PositiveInfinity, 0f, 0f)));
    }

    [Test]
    public void WithPointAndMagnitude_UseExplicitPresenceFlags()
    {
        Vector3 point = new Vector3(1f, 2f, 3f);
        ActionContext context = ActionContext.None
            .WithPoint(point)
            .WithMagnitude(0f);

        Assert.IsTrue(context.HasPoint);
        Assert.IsTrue(context.HasMagnitude);
        Assert.AreEqual(point, context.Point);
        Assert.AreEqual(0f, context.Magnitude);
        Assert.AreEqual(ActionContextFieldMask.Point | ActionContextFieldMask.Magnitude, context.Fields);
    }

    [Test]
    public void ParticipantReferences_AreReflectedInFields()
    {
        var instigator = new GameObject("ActionContext Instigator");
        var target = new GameObject("ActionContext Target");
        try
        {
            ActionContext context = ActionContext.ForParticipants(instigator, target);

            Assert.IsTrue(context.HasInstigator);
            Assert.IsTrue(context.HasTarget);
            Assert.AreEqual(
                ActionContextFieldMask.Instigator | ActionContextFieldMask.Target,
                context.Fields);
        }
        finally
        {
            Object.DestroyImmediate(instigator);
            Object.DestroyImmediate(target);
        }
    }
}
