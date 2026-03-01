using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeiveEx.TagTree
{
    internal static class TagTreeUtils
    {
        #region Fields

        internal const string LOG_CHANNEL_NAME = "[TagTree]";

        private const string INVALID_CHARACTERS_REGEX = "[^a-zA-Z0-9._-]"; //Matches any character that is not in the set
        private const string INVALID_TAG_ERROR_MESSAGE = "Tag [{0}] is invalid. Make sure tags follows the standard [a.b.c], and only have the following characters: [letters, numbers, ., -, _]";

        #endregion

        #region Internal Methods

        internal static void LoadTagsFromNames(ICollection<string> tagNames, Dictionary<int, Tag> tagDictionary)
        {
            tagDictionary.Clear();
            
            if (tagNames == null || tagNames.Count == 0)
            {
                if(Application.isPlaying) LogMessage("No Tags loaded! Tags cannot be assigned to objects!", LogType.Error);
                
                return;
            }

            PopulateTagDictionary(tagNames, tagDictionary);
            
            if(TagManager.ShowLogs) LogMessage($"Loaded tags:\n- {string.Join("\n- ", tagDictionary.Values.Select(x => x.FullTagName).OrderBy(x => x))}");
        }

        internal static void PopulateTagDictionary(IEnumerable<string> tagNames, Dictionary<int, Tag> tagDictionary)
        {   
            //Create tags
            foreach (var tagName in tagNames)
            {
                var tagHierarchy = CreateTagHierarchyFromName(tagName);
                
                if(tagHierarchy == null)
                    continue;
                
                foreach (var newTag in tagHierarchy)
                {
                    tagDictionary.TryAdd(newTag.Id, newTag);
                }
            }
            
            //Populate parents
            foreach (var tag in tagDictionary.Values)
            {
                PopulateParentTag(tag, tagDictionary);
            }
            
            //Populate children
            var rootTags = tagDictionary.Values.Where(x => x.ParentTag == null);

            foreach (var tag in rootTags)
            {
                PopulateTagChildrenRecursive(tag, tagDictionary);
            }
        }

        internal static void PopulateParentTag(Tag tag, Dictionary<int, Tag> tagDictionary)
        {
            int lastPeriodIndex = tag.FullTagName.LastIndexOf('.');
                
            if(lastPeriodIndex == -1)
                return;
                
            string parentTagName = tag.FullTagName.Substring(0, lastPeriodIndex);
            tag.ParentTag = GetTagFromFullName(parentTagName, tagDictionary);
        }

        internal static void PopulateTagChildrenRecursive(Tag parentTag, Dictionary<int, Tag> tagDictionary)
        {
            parentTag.ChildrenTags = tagDictionary.Values
                .Where(x => x.ParentTag == parentTag)
                .ToList();

            foreach (var child in parentTag.ChildrenTags)
            {
                var childTag = tagDictionary.Values.First(x => x == child);
                PopulateTagChildrenRecursive(childTag, tagDictionary);
            }
        }

        internal static List<Tag> CreateTagHierarchyFromName(string fullTagName)
        {
            //Validate tag formatting
            if (!IsTagFormattingValid(fullTagName))
            {
                Debug.LogError(string.Format(INVALID_TAG_ERROR_MESSAGE, fullTagName));
                return null;
            }
            
            //Generate the unique name for each level in the hierarchy
            IEnumerable<string> uniqueTagNames = GenerateUniqueTagNames(fullTagName);

            //Iterate and configure each Tag
            List<Tag> generatedTags = new();
            
            foreach (var uniqueTagName in uniqueTagNames)
            {
                //Create the tag and add to the collections
                var tag = CreateSingleTagFromName(uniqueTagName);
                generatedTags.Add(tag);
            }

            return generatedTags;
        }

        internal static Tag CreateSingleTagFromName(string fullTagName)
        {
            int lastPeriodIndex = fullTagName.LastIndexOf('.');
            string tagName = lastPeriodIndex != -1 ? fullTagName.Substring(lastPeriodIndex + 1) : fullTagName;
            
            //Note that this tag has no parent nor children yet!
            var tag = new Tag(GenerateIdFromFullName(fullTagName),
                tagName,
                fullTagName
            );

            return tag;
        }

        internal static bool IsTagFormattingValid(string fullTagName)
        {
            if (string.IsNullOrWhiteSpace(fullTagName))
                return false;
            
            var tagHierarchy = fullTagName.Split('.');
            int periodAmount = fullTagName.Count(x => x == '.');

            if (tagHierarchy.Length == 0 ||
                periodAmount != tagHierarchy.Length - 1 ||
                tagHierarchy.Any(string.IsNullOrEmpty) ||
                Regex.IsMatch(fullTagName, INVALID_CHARACTERS_REGEX))
                return false;

            return true;
        }

        internal static bool IsLeafTagInCollection(Tag tag, IReadOnlyCollection<Tag> tagCollection)
        {
            if (!tagCollection.Contains(tag))
                return false;

            //Since tags can be a leaf in a specific container but not in a global scope, to find a leaf tag we need to
            //see if any other tag has the given collection has this tag as parent. If there's none, then this is a leaf tag
            foreach (var other in tagCollection)
            {
                if(other == tag)
                    continue;

                if (other.ParentTag == tag)
                    return false;
            }

            return true;
        }

        internal static bool IsRootTag(Tag tag)
        {
            //A root tag simple doesn't have a parent
            return tag.ParentTag == null;
        }

        internal static int GenerateIdFromFullName(string fullTagName)
        {
            if (fullTagName == null)
                throw new NullReferenceException("Tag name cannot be null");
            
            return fullTagName.GetHashCode();
        }

        internal static Tag GetTagFromId(int tagId, Dictionary<int, Tag> tagDictionary)
        {
            return tagDictionary.GetValueOrDefault(tagId);
        }

        internal static Tag GetTagFromFullName(string fullTagName, Dictionary<int, Tag> tagDictionary)
        {
            if (string.IsNullOrEmpty(fullTagName))
                return null;
            
            return GetTagFromId(GenerateIdFromFullName(fullTagName), tagDictionary);
        }

        internal static void LogMessage(string message, LogType logType = LogType.Log)
        {
            switch (logType)
            {
                case LogType.Warning:
                    Debug.LogWarning($"{LOG_CHANNEL_NAME} {message}");
                    break;
                case LogType.Log:
                    Debug.Log($"{LOG_CHANNEL_NAME} {message}");
                    break;
                default:
                    Debug.LogError($"{LOG_CHANNEL_NAME} {message}");
                    break;
            }
        }
        
        #endregion

        #region Private Methods
        
        private static HashSet<string> GenerateUniqueTagNames(string fullTagName)
        {
            //Split the tag name into its parts
            var tagHierarchy = fullTagName.Split('.');
            
            HashSet<string> uniqueTagNames = new();
            StringBuilder sb = new();

            for (int i = 0; i < tagHierarchy.Length; i++)
            {
                sb.Clear();
                    
                for (int j = 0; j <= i; j++)
                {
                    if (sb.Length > 0)
                        sb.Append(".");

                    sb.Append(tagHierarchy[j]);
                }

                uniqueTagNames.Add(sb.ToString());
            }

            return uniqueTagNames;
        }

        #endregion
    }
}