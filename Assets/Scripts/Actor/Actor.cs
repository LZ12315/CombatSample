using Animancer;
using DeiveEx.TagTree;
using DeiveEx.TagTree.GameObjects; // 必须引入这个命名空间，才能使用 GetTagContainer() 扩展方法
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
    public TagContainer tagContainer; //插件原生的黑板容器

    private void Awake()
    {
        // 为这个 GameObject 绑定或获取一个黑板实例
        tagContainer = this.gameObject.GetTagContainer();
    }
}