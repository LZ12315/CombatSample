using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;

    public void InputMove(Vector2 inputDir)
    {
        actor.actorMovement.UpdateTurn(new Vector3(inputDir.x, 0 , inputDir.y));
    }
}
