using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;

public class ActorLogicInput : MonoBehaviour
{
    public Actor actor;
    Dictionary<Enums.InputType, EventInfo> inputActions;
    Dictionary<Enums.InputType, EventInfo> inputThisFrame;

    public void InputMove(Vector2 inputDir)
    {
        actor.actorMovement.UpdateTurn(new Vector3(inputDir.x, 0 , inputDir.y));
    }
}

public static partial class Enums
{
    public enum InputType
    {
        None,
        Move,
        MoveCancel
    }
}
