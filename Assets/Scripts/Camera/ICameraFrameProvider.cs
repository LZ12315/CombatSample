using UnityEngine;

public interface ICameraFrameProvider
{
    Vector3 ToWorldMoveDirection(Vector2 moveInput);
}
