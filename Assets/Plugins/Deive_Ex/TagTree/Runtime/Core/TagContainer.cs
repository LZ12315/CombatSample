using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeiveEx.TagTree
{
    public enum TagChangedEvent
    {
        TagAdded,
        TagRemoved,
        TagCounterIncreased,
        TagCounterDecreased,
    }
    
    public class TagChangedEventArgs
    {
        public Tag Tag;
        public TagChangedEvent Event;
    }
    
    public class TagContainer
    {
        #region Fields

        private const string NULL_TAG_ERROR_MESSAGE = "Tag Container cannot be null";

        internal readonly Dictionary<Tag, int> CurrentTags; //Tag/TagCount

        #endregion

        #region Properties

        public IReadOnlyCollection<Tag> Tags => CurrentTags.Keys;

        #endregion

        #region Events & Delegates

        public event EventHandler<TagChangedEventArgs> TagChanged;

        #endregion

        #region Constructors

        public TagContainer(params Tag[] initialTagIds) : this((IEnumerable<Tag>)initialTagIds) {}

        public TagContainer(IEnumerable<Tag> initialTagIds)
        {
            CurrentTags = new();

            foreach (var tagId in initialTagIds)
            {
                AddTag(tagId);
            }
        }

        #endregion
        
        #region Public methods

        /// <summary>
        /// Adds the entire hierarchy of the tag to this container. This means that adding tag "a.b.c" will add the following tags:
        /// <list type="bullet">
        /// <item>a</item>
        /// <item>a.b</item>
        /// <item>a.b.c</item>
        /// </list>
        /// Also, adding the same tag multiple times will increase its counter. A is only fully removed when its counter reaches zero.
        /// </summary>
        /// <param name="tag">The id of the tag to add</param>
        public void AddTag(Tag tag)
        {
            //Check if the tag exists
            if (tag == null || TagTreeUtils.GetTagFromId(tag.Id, TagManager.Tags) == null)
                throw new InvalidOperationException($"Trying to add invalid tag '{(tag == null ? "null" : tag.FullTagName)}'. " +
                                                    "Make sure the tag is loaded. You can enable the 'Show Logs' option in the TagTree " +
                                                    "settings to see which tags are loaded.");
            
            //Add the entire tag hierarchy
            var currentTag = tag;

            do
            {
                if (CurrentTags.TryAdd(currentTag, 1))
                {
                    RaiseTagChangedEvent(tag, TagChangedEvent.TagAdded);
                }
                else
                {
                    CurrentTags[currentTag]++;
                    RaiseTagChangedEvent(tag, TagChangedEvent.TagCounterIncreased);
                }

                currentTag = currentTag.ParentTag;
            }
            while(currentTag != null);
        }
        
        /// <summary>
        /// Removes the entire hierarchy of the tag. This means that removing tag "a.b.c" will remove the following tags:
        /// <list type="bullet">
        /// <item>a</item>
        /// <item>a.b</item>
        /// <item>a.b.c</item>
        /// </list>
        /// <remarks>
        /// <b>IMPORTANT:</b> note that when removing tags, the system actually decreases an internal counter
        /// for the tag. A tag is only fully removed when this counter reaches zero.</remarks>
        /// </summary>
        /// <param name="tag">The tag id to be removed</param>
        /// <returns>True if the tag was removed OR its counter was decreased.</returns>
        public bool RemoveTag(Tag tag)
        {
            //Check if we have the tag
            if(!CurrentTags.ContainsKey(tag))
                return false;
            
            //Decrease the counter for the entire tag hierarchy
            var currentTag = tag;
            
            do
            {
                CurrentTags[currentTag]--;

                //If the counter reaches zero, remove the tag
                if (CurrentTags[currentTag] <= 0)
                {
                    //We can only remove a tag if it's a leaf tag
                    if (TagTreeUtils.IsLeafTagInCollection(currentTag, CurrentTags.Keys))
                    {
                        CurrentTags.Remove(currentTag);
                        RaiseTagChangedEvent(tag, TagChangedEvent.TagRemoved);
                    }
                    else
                    {
                        Debug.LogWarning($"Tag [{currentTag.FullTagName}] is not a leaf tag and thus cannot be removed");
                        CurrentTags[currentTag] = 1;
                    }
                }
                else
                {
                    RaiseTagChangedEvent(tag, TagChangedEvent.TagCounterDecreased);
                }

                currentTag = currentTag.ParentTag;
            }
            while(currentTag != null);
            
            return true;
        }
        
        /// <summary>
        /// Removes the entire hierarchy of the tag. This method removes the given tag and all of its children completely,
        /// regardless of their internal counters.
        /// </summary>
        /// <remarks>
        /// <b>IMPORTANT:</b> Calling this method means that if a container has tags "a" and "a.b", and you pass tag "a"
        /// as a parameter, both tags "a" and "a.b" will be removed, since tag "a.b" cannot exist without tag "a". 
        /// </remarks>
        /// <param name="tag">The tag id to be removed</param>
        /// <returns>True if the tag was removed.</returns>
        public bool RemoveTagCompletely(Tag tag)
        {
            if(!CurrentTags.ContainsKey(tag))
                return false;

            //We need to remove this tag as well as any child tags below it
            HashSet<Tag> tagsToRemove = new() { tag };
            Queue<Tag> parents = new();
            parents.Enqueue(tag);

            while (parents.Count > 0)
            {
                var currentParent = parents.Dequeue();

                foreach (var other in CurrentTags.Keys)
                {
                    if (other.ParentTag != currentParent)
                        continue;
                    
                    parents.Enqueue(other);
                    tagsToRemove.Add(other);
                }
            }

            foreach (var tagToRemove in tagsToRemove)
            {
                CurrentTags.Remove(tagToRemove);
                RaiseTagChangedEvent(tagToRemove, TagChangedEvent.TagRemoved);
            }
            
            return true;
        }
        
        /// <summary>
        /// Returns the current counter for the given tag in this container
        /// </summary>
        /// <param name="tag">The tag to get the counter for</param>
        /// <returns>The current counter for the tag. Returns zero if the tag is not found.</returns>
        public int GetTagCount(Tag tag) => CurrentTags.GetValueOrDefault(tag);
        
        /// <summary>
        /// Checks if the tag exists in this container
        /// </summary>
        /// <param name="tag">The tag to search for</param>
        /// <returns>True if the tag exists, false otherwise</returns>
        public bool HasTag(Tag tag) => CurrentTags.ContainsKey(tag);

        /// <summary>
        /// Checks if any tags from the current container match any tag from the given container
        /// </summary>
        /// <param name="other">The container to compare with</param>
        /// <returns>True is any tag matches, false if no tag matches</returns>
        /// <exception cref="NullReferenceException">The given container is null</exception>
        public bool HasAny(TagContainer other)
        {
            if (other == null)
                throw new NullReferenceException(NULL_TAG_ERROR_MESSAGE);
            
            //There should be at least one tag on each side for anything to match
            if (other.CurrentTags.Count == 0 || CurrentTags.Count == 0)
                return false;

            foreach (var currentTag in CurrentTags.Keys)
            {
                if (currentTag.MatchesAny(other))
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// Checks if all tags in the current container match all tags in the given container
        /// </summary>
        /// <param name="other">The container to compare with</param>
        /// <returns>True if all tags match, False if at least one Tag doesn't match</returns>
        /// <exception cref="NullReferenceException">The given container is null</exception>
        public bool HasAll(TagContainer other)
        {
            if (other == null)
                throw new NullReferenceException(NULL_TAG_ERROR_MESSAGE);
            
            //If there are no tags to match, then technically there are no tags that DON'T match
            if (other.CurrentTags.Count == 0)
                return true;
            
            //If we have no tags but there at least one tag to match, then we can't match it
            if (CurrentTags.Count == 0)
                return false;

            foreach (var tagId in other.CurrentTags.Keys)
            {
                bool foundMatch = false;
                
                foreach (var currentTag in CurrentTags.Keys)
                {
                    if (currentTag.Matches(tagId))
                    {
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                    return false;
            }

            return true;
        }

        public void ClearTags()
        {
            CurrentTags.Clear();
        }

        public bool IsLeafTagInContainer(Tag tag) => TagTreeUtils.IsLeafTagInCollection(tag, CurrentTags.Keys);

        public override string ToString() => string.Join("\n", Tags);

        #endregion

        #region Private Methods

        private void RaiseTagChangedEvent(Tag tag,  TagChangedEvent eventType)
        {
            TagChanged?.Invoke(this, new TagChangedEventArgs()
            {
                Tag = tag,
                Event = eventType
            });
        }

        #endregion
    }
}
