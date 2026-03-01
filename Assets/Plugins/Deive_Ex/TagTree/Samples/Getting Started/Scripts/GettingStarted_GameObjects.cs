using System.Linq;
using DeiveEx.TagTree.GameObjects;
using UnityEngine;

//This class shows how to use the GameObjects extension of TagTree
namespace DeiveEx.TagTree.Examples
{
    public class GettingStarted_GameObjects : MonoBehaviour
    {
        [SerializeField] private GameObject[] _objects;
        [SerializeField] private Material _matchMaterial;
        [SerializeField] private Material _normalMaterial;
        
        private void Start()
        {
            //TagTree provides some extra functionality to GameObjects. The first one is that you can get a TagContainer
            //reference directly from the GameObject
            var myGoContainer = this.gameObject.GetTagContainer();
            
            //The second functionality is that you can use the class "TagTreeGOQuery" to query GameObjects with specific queries
            Tag tagA = Tag.GetTagFromFullName("a");
            var matches = TagTreeGOQuery.GetAllWithTag(tagA);

            Debug.Log($"Found {matches.Count()} objects with tag {tagA.FullTagName}");
            
            //IMPORTANT: Note that it's only possible to query for GameObjects that has at least one tag inside their container.
            //GameObjects without any tags cannot be queried using the TagTreeGOQuery class.
            //You can still keep track of GameObjects yourself and loop through the list to check which GameObject have no tags, though.
            
            //You can even use a TagQuery...
            var resultsTagQuery = TagTreeGOQuery.GetAllWithQuery(new TagQuery(ConditionMatchType.AnyConditionMatches));
            
            //... Or a custom condition.
            var tagX = Tag.GetTagFromFullName("x");
            var resultsCustomCondition = TagTreeGOQuery.GetAllWithCondition(tagContainer => tagContainer.HasTag(tagA) && !tagContainer.HasTag(tagX));
        }

        public void GetAllWithTag(string tagName)
        {
            //The second functionality is that you can use the class "TagTreeGOQuery" to query GameObjects with specific queries
            Tag tag = Tag.GetTagFromFullName(tagName);
            var matches = TagTreeGOQuery.GetAllWithTag(tag).ToArray();

            foreach (var go in _objects)
            {
                if(matches.Contains(go))
                    go.GetComponent<Renderer>().material = _matchMaterial;
                else
                    go.GetComponent<Renderer>().material = _normalMaterial;
            }
        }
    }
}
