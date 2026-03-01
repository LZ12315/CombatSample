using System;
using System.Text;
using UnityEngine;

//This class shows how to use the basic functionality of TagTree.
namespace DeiveEx.TagTree.Examples
{
    public class GettingStarted_UnderstandingTags : MonoBehaviour
    {
        //You can use a TagReference to select a tag in the inspector. This is the recommended way.
        [SerializeField] private TagReference _tag;

        //Tag containers hold tags. You can add a TagContainer to anything!
        private TagContainer _tagContainer;

        private void Start()
        {
            //You can get a reference to a Tag by using a TagReference...
            Tag tag1 = _tag.GetTag();
            
            //... Or you can use the full tag name directly!
            Tag tag2 = Tag.GetTagFromFullName("a.b");
            
            //IMPORTANT: Just be aware that only tags defined in a loaded tag file are available! So make sure your tag exists
            //inside a Tag file and that you have the correct Load Source defined in the TagTree settings!
            //Also, if you decide to use string names, note that their names are case-sensitive and need to be exactly the same.

            //To add a tag to a container, just call "AddTag" on a TagContainer
            _tagContainer = new TagContainer();
            _tagContainer.AddTag(tag1);
            _tagContainer.AddTag(tag2);
            
            //To remove a tag, just call "RemoveTag"
            _tagContainer.RemoveTag(tag1);
            
            //You can check if a tag exists by calling "HasTag"
            bool containerHasTag1 = _tagContainer.HasTag(tag1);
            bool containerHasTag2 = _tagContainer.HasTag(tag2);
            Debug.Log($"Container has tag '{tag1.FullTagName}'? {containerHasTag1}");
            Debug.Log($"Container has tag '{tag2.FullTagName}'? {containerHasTag2}");
            
            //When you add a child tag to a container, their parent Tags are also added to the same container, so you can
            //check for their parent as well. This means that adding tag "a.b" adds both Tag "a" and Tag "a.b".
            Tag parentTag = Tag.GetTagFromFullName("a");
            bool hasParentTag = _tagContainer.HasTag(parentTag);
            Debug.Log($"Container has parent tag '{parentTag.FullTagName}'? {hasParentTag}"); //Will print "true", because Tag "a" was added implicitly
            
            //If you add the same tag to a container multiple times, their counter will be increased inside that specific container.
            //You can get the current counter by calling "GetTagCounter"
            _tagContainer.AddTag(tag2);
            int tag2Count = _tagContainer.GetTagCount(tag2);

            Debug.Log($"{tag2.FullTagName} counter is: {tag2Count}"); //Will print "2"
            
            //A tag is only fully removed from their container when their counter reaches 0.
            //This example will print "true", since we've added Tag2 twice, but only remove it once
            _tagContainer.RemoveTag(tag2);
            bool stillHasTag2 = _tagContainer.HasTag(tag2);
            Debug.Log($"Container still  has tag '{tag2.FullTagName}'? {stillHasTag2}");
            
            //You can force full removal of Tag by calling "RemoveTagCompletely".
            _tagContainer.AddTag(tag2);
            _tagContainer.AddTag(tag2);
            _tagContainer.AddTag(tag2);
            _tagContainer.AddTag(tag2);
            _tagContainer.RemoveTagCompletely(tag2);
            stillHasTag2 = _tagContainer.HasTag(tag2);
            Debug.Log($"Container still  has tag '{tag2.FullTagName}'? {stillHasTag2}"); //Will print "false"
            
            //You usually want to use "HasTag" to check if a Tag was added to a container, but you can check all Tags that are
            //currently inside a container by accessing its "Tags" property. Useful for debugging purposes.
            StringBuilder sb = new("All Tags in Container:\n");

            foreach (var tagInsideContainer in _tagContainer.Tags)
            {
                sb.AppendLine(tagInsideContainer.FullTagName);
            }

            Debug.Log(sb.ToString());
            
            //And you can remove all tags from a container by calling "ClearTags"
            _tagContainer.ClearTags();
            
            //You can also check if 2 tags match by calling "Matches".
            //Note that child Tags can match their parent, but parent Tags cannot match their children! 
            Tag tagA = Tag.GetTagFromFullName("a");
            Tag tagAB = Tag.GetTagFromFullName("a.b");
            Tag tagX = Tag.GetTagFromFullName("x");

            Debug.Log($"'{tagA.FullTagName}' matches '{tagAB.FullTagName}': {tagA.Matches(tagAB)}"); //False
            Debug.Log($"'{tagA.FullTagName}' matches '{tagX.FullTagName}': {tagA.Matches(tagX)}"); //False
            Debug.Log($"'{tagAB.FullTagName}' matches '{tagA.FullTagName}': {tagAB.Matches(tagA)}"); //True
            
            //If you want to know 2 tags are the same, and not a parent, you can use "MatchesExact"
            Tag tagA2 = Tag.GetTagFromFullName("a");
            Debug.Log($"'{tagA.FullTagName}' matches exactly '{tagAB.FullTagName}': {tagA.MatchesExact(tagAB)}"); //False
            Debug.Log($"'{tagA.FullTagName}' matches exactly '{tagA2.FullTagName}': {tagA.MatchesExact(tagA2)}"); //True
            
            //You can also compare Tags to TagContainers...
            _tagContainer.AddTag(tagA);
            _tagContainer.AddTag(tagX);
            bool tagMatchesAny = tagA.MatchesAny(_tagContainer);
            bool tagMatchesAll = tagA.MatchesAll(_tagContainer);
            Debug.Log($"Tag '{tagA.FullTagName}' matches any tag in container? {tagMatchesAny}"); //True
            Debug.Log($"Tag '{tagA.FullTagName}' matches all tags in container? {tagMatchesAll}"); //False, because TagA doesn't match TagX
            
            //... Or TagContainers with TagContainers
            TagContainer containerA = new TagContainer(tagA);
            TagContainer containerB = new TagContainer(tagA, tagX);

            Debug.Log($"ContainerA has any tag in ContainerB? {containerA.HasAny(containerB)}"); //True
            Debug.Log($"ContainerA has all tags in ContainerB? {containerA.HasAll(containerB)}"); //False, because ContainerA doesn't have TagX
            
            //If you want to be notified when a Tag change has happened to a container, you can listen to the "TagChanged" event
            _tagContainer.ClearTags();
            
            _tagContainer.TagChanged += (sender, args) =>
            {
                switch (args.Event)
                {
                    case TagChangedEvent.TagAdded:
                        Debug.Log($"Tag '{args.Tag.FullTagName}' has been added to container");
                        break;
                    case TagChangedEvent.TagRemoved:
                        Debug.Log($"Tag '{args.Tag.FullTagName}' has been removed from container");
                        break;
                    case TagChangedEvent.TagCounterIncreased:
                        Debug.Log($"Tag '{args.Tag.FullTagName}' counter increased inside this specific container. Current counter is now: {_tagContainer.GetTagCount(args.Tag)}");
                        break;
                    case TagChangedEvent.TagCounterDecreased:
                        Debug.Log($"Tag '{args.Tag.FullTagName}' counter decreased inside this specific container. Current counter is now: {_tagContainer.GetTagCount(args.Tag)}");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            };
            
            _tagContainer.AddTag(tagA);
            _tagContainer.AddTag(tagA);
            _tagContainer.RemoveTag(tagA);
            _tagContainer.RemoveTag(tagA);
        }
    }
}

