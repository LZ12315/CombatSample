using Animancer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(CharacterController))]
public class Actor : MonoBehaviour
{
    public ActorLogicInput logicInput;
    public ActorMovement movement;
    public ActionPlayableDirector actionPlayerDirector;
    public CharacterController characterController;
    public AnimancerComponent animancer;
    public ActorCameraControl cameraControl;
    public ActorCombater combater;
}
