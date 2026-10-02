using NUnit.Framework;
using UnityEngine;

public sealed class PlayerInputOwnershipTests
{
    [Test]
    public void PlayerResolver_DiagonalInputProducesValidStrength()
    {
        var resolver = new PlayerLocomotionIntentResolver();
        LocomotionIntent intent = resolver.Resolve(null, null, new Vector2(0.707107f, 0.707107f));

        Assert.That(intent.MoveStrength, Is.EqualTo(1f));
        Assert.That(intent.WorldMoveDirection.x, Is.GreaterThan(0f));
        Assert.That(intent.WorldMoveDirection.z, Is.GreaterThan(0f));
        Assert.DoesNotThrow(() => new LocomotionIntentBuffer().Submit(intent));
    }

    [Test]
    public void PlayerResolver_ProducesNormalizedLocomotionIntentFromRawMove()
    {
        var owner = new GameObject("PlayerInputOwnershipTests Actor");
        try
        {
            Actor actor = owner.AddComponent<Actor>();
            var resolver = new PlayerLocomotionIntentResolver();

            LocomotionIntent intent = resolver.Resolve(actor, null, new Vector2(2f, 0f));

            Assert.That(intent.WorldMoveDirection.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(intent.WorldMoveDirection.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(intent.MoveStrength, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(intent.FacingDirection.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(intent.FacingDirection.z, Is.EqualTo(0f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
