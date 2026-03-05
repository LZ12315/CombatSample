using Animancer;
using DeiveEx.TagTree;
using DeiveEx.TagTree.GameObjects; // 必须引入这个命名空间，才能使用 GetTagContainer() 扩展方法
using NodeCanvas.Framework;
using NodeCanvas.StateMachines;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(CharacterController))]
public class Actor : MonoBehaviour
{
    public CharacterController characterController;
    public ActorLogicInput logicInput;
    public ActorMovement movement;
    public ActionPlayer actionPlayer;
    public AnimancerComponent animancer;
    public ActorCameraControl cameraControl;
    public ActorCombater combater;

    // 🌟 直接声明插件原生的黑板容器
    public TagContainer tagContainer;

    private void Awake()
    {
        // 🌟 使用插件提供的安全扩展方法，为这个 GameObject 绑定或获取一个黑板实例
        tagContainer = this.gameObject.GetTagContainer();
    }
}