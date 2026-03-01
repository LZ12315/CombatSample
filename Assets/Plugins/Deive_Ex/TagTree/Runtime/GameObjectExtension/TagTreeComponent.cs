using DeiveEx.TagTree.GameObjects;
using UnityEngine;

namespace DeiveEx.TagTree
{
    /// <summary>
    /// Helper class that allows users to see which tags are currently in the attached GameObject, as well as defining
    /// some tags to be added on Awake
    /// </summary>
    [DisallowMultipleComponent]
    public class TagTreeComponent : MonoBehaviour
    {
        [SerializeField] private TagReference[] _initialTags;

        private void Awake()
        {
            var container = gameObject.GetTagContainer();

            foreach (var tagReference in _initialTags)
            {
                container.AddTag(tagReference.GetTag());
            }
        }
    }
}
