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
    public ActorMovement movement;
    public ActionPlayer actionPlayer;
    public AnimancerComponent animancer;
    public ActorCameraControl cameraControl;
    public ActorCombater combater;
    public TagContainer persistentTags;
    public TagContainer transientTags;

    // Backward-compatible alias while the project migrates to dual containers.
    public TagContainer tagContainer => persistentTags;

    private void Awake()
    {
        persistentTags = gameObject.GetTagContainer();
        transientTags = new TagContainer();
    }

    public TagContainer GetTagContainer(ActorTagContainerType containerType)
    {
        return containerType == ActorTagContainerType.Persistent ? persistentTags : transientTags;
    }

    public void AddTag(Tag tag, ActorTagContainerType containerType)
    {
        if (tag == null) return;
        GetTagContainer(containerType)?.AddTag(tag);
    }

    public bool RemoveTag(Tag tag, ActorTagContainerType containerType)
    {
        if (tag == null) return false;
        return GetTagContainer(containerType)?.RemoveTag(tag) ?? false;
    }

    public bool HasTag(Tag tag, ActorTagContainerType containerType, ActorTagMatchMode matchMode)
    {
        if (tag == null) return false;

        var container = GetTagContainer(containerType);
        if (container == null) return false;

        if (matchMode == ActorTagMatchMode.Fuzzy)
            return container.HasTag(tag);

        return container.HasTag(tag) && container.IsLeafTagInContainer(tag);
    }

    public void ClearTransientTags()
    {
        transientTags?.ClearTags();
    }
}
