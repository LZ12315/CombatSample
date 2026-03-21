using Animancer;
using DeiveEx.TagTree;
using DeiveEx.TagTree.GameObjects;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Actor : MonoBehaviour
{
    public CharacterController characterController;
    public ActorLogicInput logicInput;
    public ActionStateManager actionManager;
    public ActorLocomotion locomotion;
    public ActorMovement movement;
    public ActionPlayer actionPlayer;
    public AnimancerComponent animancer;
    public ActorCameraControl cameraControl;
    public ActorCombater combater;
    public TagContainer tagContainer;

    private void Awake()
    {
        tagContainer = gameObject.GetTagContainer();
    }
}
