using System;
using DeiveEx.TagTree;
using UnityEngine;

/// <summary>
/// Tags applied for the lifetime of an <see cref="ActionAsset"/> play, with explicit container choice.
/// </summary>
[Serializable]
public class ActionRuntimeTagBinding
{
    public TagReference tag;

    [Tooltip("Which tag container receives this tag while the action plays.")]
    public ActorTagContainerType targetContainer = ActorTagContainerType.Transient;
}
