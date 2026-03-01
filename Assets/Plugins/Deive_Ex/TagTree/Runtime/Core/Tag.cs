using System;
using System.Collections.Generic;

namespace DeiveEx.TagTree
{
    public class Tag
    {
        #region Fields

        private const string NULL_CONTAINER_ERROR_MESSAGE = "Tag Container cannot be null";

        #endregion

        #region Properties

        public int Id { get; }
        public Tag ParentTag { get; internal set; }
        public IReadOnlyList<Tag> ChildrenTags { get; internal set; }
        /// <summary>
        /// This Tag's name, not including their parent's name.
        /// <example>
        /// Tag "a" name is "a"<br/>
        /// Tag "a.b" name is "b"
        /// </example>
        /// </summary>
        public string TagName { get; }
        /// <summary>
        /// The Tag's full name, including their parent's name.
        /// <example>
        /// Tag "a" full name is "a"<br/>
        /// Tag "a.b" full name is "a.b"
        /// </example>
        /// </summary>
        public string FullTagName { get; }

        #endregion

        #region Constructors

        //Making constructor internal so tags cannot be created from normal means
        internal Tag() { }

        internal Tag(int id, string tagName, string fullTagName)
        {
            Id = id;
            TagName = tagName;
            FullTagName = fullTagName;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Check if this tag is similar to the given tag. Note that you can match a child Tag to their parent Tag,
        /// but not the other way around<br/>
        /// Also, <see cref="Matches"/> don't check for exactness. To check for hierarchical exactness,
        /// use <see cref="MatchesExact(Tag)"/> instead
        /// </summary>
        /// <example>
        /// a.b.c == a.b.c -> TRUE<br/>
        /// a.b.c == a -> TRUE<br/>
        /// a == a.b.c -> FALSE<br/>
        /// a.b.x == a.b.y -> FALSE<br/>
        /// </example>
        /// <param name="other">The tag to compare with</param>
        /// <returns>True if the given Tag is either the same or a parent of this Tag. False otherwise</returns>
        public bool Matches(Tag other)
        {
            var currentTag = this;
            
            do
            {
                if (currentTag.Id == other.Id)
                    return true;

                currentTag = currentTag.ParentTag;
            }
            while (currentTag != null);

            return false;
        }

        /// <summary>
        /// Check if both tags are exactly the same.
        /// </summary>
        /// <example>
        /// a.b.c == a.b.c => TRUE<br/>
        /// a.b.c == a -> FALSE<br/>
        /// a == a.b.c -> FALSE<br/>
        /// a.b.c == a.b.x -> FALSE<br/>
        /// </example>
        /// <param name="other">The tag to compare with</param>
        /// <returns>True if both tags have the exact same hierarchy</returns>
        public bool MatchesExact(Tag other)
        {
            return Id == other.Id;
        }

        /// <summary>
        /// Checks if this tag matches any of the tags inside the given container. This method calls <see cref="Matches"/>
        /// internally.
        /// </summary>
        /// <param name="container">The container to match</param>
        /// <returns>True if the container has any tag that matches this one. False otherwise.</returns>
        /// <exception cref="NullReferenceException">Throws if the container is null</exception>
        public bool MatchesAny(TagContainer container)
        {
            if (container == null)
                throw new NullReferenceException(NULL_CONTAINER_ERROR_MESSAGE);
            
            foreach (var other in container.CurrentTags.Keys)
            {
                if (Matches(other))
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// Checks if this tag has an exact match with any of the tags inside the given container. This method calls <see cref="MatchesExact"/>
        /// internally.
        /// </summary>
        /// <param name="container">The container to match</param>
        /// <returns>True if the container has any tag that matches this one. False otherwise.</returns>
        /// <exception cref="NullReferenceException">Throws if the container is null</exception>
        public bool MatchesAnyExact(TagContainer container)
        {
            if (container == null)
                throw new NullReferenceException(NULL_CONTAINER_ERROR_MESSAGE);
            
            foreach (var other in container.CurrentTags.Keys)
            {
                if (MatchesExact(other))
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// Checks if this tag matches all tags inside the given container. This method calls <see cref="Matches"/>
        /// internally.
        /// </summary>
        /// <param name="container">The container to match</param>
        /// <returns>True if all tags in the container matches this one. False otherwise.</returns>
        /// <exception cref="NullReferenceException">Throws if the container is null</exception>
        public bool MatchesAll(TagContainer container)
        {
            if (container == null)
                throw new NullReferenceException(NULL_CONTAINER_ERROR_MESSAGE);
            
            foreach (var otherId in container.CurrentTags.Keys)
            {
                if (!Matches(otherId))
                    return false;
            }

            return true;
        }
        
        /// <summary>
        /// This method checks if the given container contains the entire hierarchy from this tag. So, if we call this method
        /// using tag "a.b", it'll check if the container has tags "a" and tag "a.b" and nothing else, because any other tag
        /// would not match.
        /// This method calls <see cref="MatchesExact"/> internally.
        /// </summary>
        /// <param name="container">The container to match</param>
        /// <returns>True if all tags in the container matches this one. False otherwise.</returns>
        /// <exception cref="NullReferenceException">Throws if the container is null</exception>
        public bool MatchesAllExact(TagContainer container)
        {
            if (container == null)
                throw new NullReferenceException(NULL_CONTAINER_ERROR_MESSAGE);

            //Special case where, if the container is empty, then there's no tags that DON'T match us
            if (container.CurrentTags.Count == 0)
                return true;

            //Because of the way we add tags to containers (by adding the entire hierarchy), we can make some assumptions
            //here to decrease the number of comparisons.
            //Since we're comparing this specific tag with all tags in a container, then this means for it to be an
            //exact match, the container must only have all tags in our hierarchy and nothing else. This means that
            //if we're tag "a.b", the tag container MUST have only tags "a" and "a.b", and since "a.b" cannot be in a
            //container without "a", then this means that the amount of tags in the container should be the same as our
            //depth.
            var depth = GetDepth();

            if (depth != container.CurrentTags.Count - 1)
                return false;

            return MatchesAnyExact(container);
        }
        
        /// <summary>
        /// Returns the depth level of this tag, starting at zero.
        /// </summary>
        /// <example>
        /// Tag "a" has a depth of 0<br/>
        /// Tag "a.b" has a depth of 1<br/>
        /// Tag "a.b.c.d" has a depth of 3
        /// </example>
        /// <returns>The depth level</returns>
        public int GetDepth()
        {
            var depth = 0;
            var currentParent = ParentTag;

            while (currentParent != null)
            {
                currentParent = currentParent.ParentTag;
                depth++;
            }

            return depth;
        }
        
        /// <summary>
        /// Checks if this Tag is a leaf Tag, meaning this Tag has no children. This considers the Global scope of Tags.
        /// If you want to see if this is a leaf Tag inside a <see cref="TagContainer"/>, use <see cref="IsLeafTagInContainer"/>
        /// instead
        /// </summary>
        /// <returns>True if the Tag has no children, false otherwise</returns>
        public bool IsLeafTag() => TagTreeUtils.IsLeafTagInCollection(this, TagManager.Tags.Values);
        
        /// <summary>
        /// Checks if this Tag is a leaf Tag, meaning this Tag has no children. This considers the Local scope of a <see cref="TagContainer"/>.
        /// If you want to see if this is a leaf Tag in a Global scope, use <see cref="IsLeafTag"/> instead.
        /// instead
        /// </summary>
        /// <returns>True if the Tag has no children, false otherwise</returns>
        public bool IsLeafTagInContainer(TagContainer container) => TagTreeUtils.IsLeafTagInCollection(this, container.CurrentTags.Keys);

        public override string ToString() => $"{FullTagName} (id: {Id})";

        #endregion

        #region Static Methods

        /// <summary>
        /// Generates a Tag ID from a Tag's full name. Note that this will generate an ID even if the tag doesn't exist.
        /// </summary>
        /// <param name="fullTagName">The Full tag name to use to generate a Tag ID</param>
        /// <returns>The generated Tag ID</returns>
        public static int GetIdFromFullName(string fullTagName) => TagTreeUtils.GenerateIdFromFullName(fullTagName);

        /// <summary>
        /// Finds a Tag reference from their ID.
        /// </summary>
        /// <param name="tagId">The Tag ID to search for</param>
        /// <returns>The Tag reference if it exists and is loaded. Null otherwise.</returns>
        public static Tag GetTagFromId(int tagId) => TagTreeUtils.GetTagFromId(tagId, TagManager.Tags);

        /// <summary>
        /// Finds a Tag reference from their full name
        /// </summary>
        /// <param name="fullTagName">The Tag's Full Name to search for</param>
        /// <returns>The Tag reference if it exists and is loaded. Null otherwise.</returns>
        public static Tag GetTagFromFullName(string fullTagName) => TagTreeUtils.GetTagFromFullName(fullTagName, TagManager.Tags);

        #endregion
    }
}
