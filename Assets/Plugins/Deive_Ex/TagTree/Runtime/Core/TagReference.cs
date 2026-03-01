using System;
using UnityEngine;

namespace DeiveEx.TagTree
{
    /// <summary>
    /// Helper class to select tags in the inspector
    /// </summary>
    [Serializable]
    public class TagReference
    {
        [SerializeField] internal int TagId;

        public Tag GetTag() => Tag.GetTagFromId(TagId);
    }
}
