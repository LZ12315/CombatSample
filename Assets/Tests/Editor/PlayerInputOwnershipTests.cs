using NUnit.Framework;
using UnityEngine;

public sealed class PlayerInputOwnershipTests
{
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
            Assert.AreEqual(Vector3.zero, intent.FacingDirection);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
