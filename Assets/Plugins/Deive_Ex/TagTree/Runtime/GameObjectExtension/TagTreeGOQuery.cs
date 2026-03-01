using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeiveEx.TagTree.GameObjects
{
    /// <summary>
    /// Class with helper methods to perform a search through GameObjects that have AT LEAST 1 tag. GameObjects with zero
    /// tags won't be included in the search
    /// </summary>
    public static class TagTreeGOQuery
    {
        //Extension static methods aren't a thing (even though they're static by default). I'd love to have something
        //like "GameObject.GetAllWithTag()"
        public static IEnumerable<GameObject> GetAllWithTag(Tag tag) => GetAllWithCondition(x => x.HasTag(tag));
        
        public static IEnumerable<GameObject> GetAllWithAnyTag(TagContainer container) => GetAllWithCondition(x => x.HasAny(container));
        
        public static IEnumerable<GameObject> GetAllWithAllTags(TagContainer container) => GetAllWithCondition(x => x.HasAll(container));
        
        public static IEnumerable<GameObject> GetAllWithQuery(TagQuery query) => GetAllWithCondition(query.Match);

        public static IEnumerable<GameObject> GetAllWithCondition(Func<TagContainer, bool> condition)
        {
            //Since we only clear the GameObject list at regular intervals, it's possible that a GO has been destroyed
            //but is still in the list, so we need to filter them out. We also filter objects without any tags, because
            //GOs that never had a tag added in their lifetime are also never added to the list so, if we don't filter
            //them out, it would be possible to search these objects, creating inconsistent results where certain objects
            //are returned but others aren't
            return TagTreeGOExtensions.GoTagContainers
                .Where(x => x.Key != null && x.Value.CurrentTags.Count > 0 && condition(x.Value))
                .Select(x => x.Key);
        }
    }
}
