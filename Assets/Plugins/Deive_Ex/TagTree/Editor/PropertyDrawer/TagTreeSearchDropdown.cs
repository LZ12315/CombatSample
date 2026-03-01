using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DeiveEx.TagTree.Editor
{
    internal class SelectTagDropdownItem : AdvancedDropdownItem
    {
        public Tag Tag { get; }

        public SelectTagDropdownItem(string name, Tag tag) : base(name)
        {
            Tag = tag;
        }
    }
    
    internal class TagTreeSearchDropdown : AdvancedDropdown
    {
        private readonly Action<Tag> _onTagSelected;
        private Texture2D _tagIcon;
        private Texture2D _addIcon;

        public TagTreeSearchDropdown(AdvancedDropdownState state, Action<Tag> onTagSelected) : base(state)
        {
            _onTagSelected = onTagSelected;
            _tagIcon = (Texture2D) EditorGUIUtility.IconContent("d_FilterByLabel@2x").image;
            _addIcon = (Texture2D) EditorGUIUtility.IconContent("d_ol_plus").image;
        }
        
        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Tags");

            var rootTags = TagManager.Tags.Values.Where(TagTreeUtils.IsRootTag).OrderBy(x => x.FullTagName);

            foreach (var tag in rootTags)
            {
                AddTagsRecursive(root, tag);
            }

            return root;
        }

        private void AddTagsRecursive(AdvancedDropdownItem parentEntry, Tag parentTag)
        {
            var entry = new AdvancedDropdownItem(parentTag.TagName) { icon = _tagIcon };
            parentEntry.AddChild(entry);
            
            //Add a child entry to the current entry so we can set the parent tag, not only child/leaf tags
            var selectTagEntry = new SelectTagDropdownItem($"Set '{parentTag.FullTagName}'", parentTag) { icon = _addIcon };
            entry.AddChild(selectTagEntry);

            foreach (var childTag in parentTag.ChildrenTags.OrderBy(x => x.FullTagName))
            {
                AddTagsRecursive(entry, childTag);
            }
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            var tagEntry = (SelectTagDropdownItem)item;
            _onTagSelected(tagEntry.Tag);
        }

        internal void SetMinHeight(float minSize)
        {
            var currentSize = minimumSize;
            currentSize.y = minSize;
            minimumSize = currentSize;
        }
        
        internal void SetMaxHeight(float maxSize)
        {
            //Max size is internal for some reason??? We can use reflection to change its value, though
            PropertyInfo maxSizeProperty = typeof(AdvancedDropdown).GetProperty("maximumSize", BindingFlags.Instance | BindingFlags.NonPublic);

            if (maxSizeProperty == null)
            {
                Debug.LogError("Max Size property not found");
                return;
            }
            
            var currentSize = (Vector2) maxSizeProperty.GetValue(this);
            currentSize.y = maxSize;
            maxSizeProperty.SetValue(this, currentSize);
        }
    }
}
