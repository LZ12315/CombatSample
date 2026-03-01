using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeiveEx.TagTree.Editor
{
    public class TagHierarchyPanel : TagTreeEditorPanelBase
    {
        private VisualElement _tagHierarchyPanel;
        private TreeView _tagTreeView;
        private Label _noTagsLabel;
        
        private Dictionary<VisualElement, Tag> _treeViewBinds;
        private VisualTreeAsset _tagEntryUxml;

        public TreeView TagTreeView => _tagTreeView;

        public TagHierarchyPanel(TagTreeEditorWindow parentWindow, VisualTreeAsset tagEntryUxml) : base(parentWindow)
        {
            _tagEntryUxml = tagEntryUxml;
        }
        
        internal override void Build()
        {
            _treeViewBinds = new();
            
            _tagHierarchyPanel = RootVisualElement.Q<VisualElement>("tag-hierarchy-panel");
            _tagTreeView = _tagHierarchyPanel.Q<TreeView>("tag-hierarchy-tree");
            _noTagsLabel = RootVisualElement.Q<Label>("no-tags-label");
            
            _tagTreeView.makeItem = ConfigureTreeViewEntry;
            _tagTreeView.bindItem = BindTreeViewEntry;
            _tagTreeView.Rebuild();
            
            ToggleElementVisibility(_noTagsLabel, false);
            PopulateTags();
        }
        
        internal void SetPanelVisible(bool isVisible)
        {
            ToggleElementVisibility(_tagHierarchyPanel, isVisible);
        }
        
        internal void SetNoTagPanelVisible(bool isVisible)
        {
            ToggleElementVisibility(_noTagsLabel, isVisible);
        }
        
        internal void PopulateTags()
        {
            ToggleElementVisibility(_noTagsLabel, ParentWindow.LoadedTags.Count == 0);
            ToggleElementVisibility(_tagHierarchyPanel, ParentWindow.LoadedTags.Count != 0);
            
            var orderedTags = ParentWindow.LoadedTags.Values.OrderBy(x => x.FullTagName);
            
            //Populate the tree view. The order the items are added to the list defines the order in the tree view
            var items = new List<TreeViewItemData<Tag>>();
            var rootTags = orderedTags
                .Where(x => x.ParentTag == null)
                .OrderBy(x => x.TagName);

            foreach (var rootTag in rootTags)
            {
                PopulateTreeViewDataRecursive(rootTag, items, tag => tag.FullTagName.Contains(ParentWindow.ToolsPanel.FilterTagInput.value));
            }
            
            _tagTreeView.SetRootItems(items);
            _tagTreeView.RefreshItems();
        }
        
        private bool PopulateTreeViewDataRecursive(Tag parentTag, List<TreeViewItemData<Tag>> treeViewItems, Predicate<Tag> filter = null)
        {
            var childrenTags = parentTag.ChildrenTags;
            List<TreeViewItemData<Tag>> treeViewChildren = null;
            bool isVisible = filter == null || filter(parentTag);
            
            if (childrenTags.Count > 0)
            {
                treeViewChildren = new List<TreeViewItemData<Tag>>();
                
                foreach (var childTag in childrenTags.OrderBy(x => x.TagName))
                {
                    if (PopulateTreeViewDataRecursive(childTag, treeViewChildren, filter))
                        isVisible = true;
                }
            }

            if (isVisible)
                treeViewItems.Add(new TreeViewItemData<Tag>(parentTag.Id, parentTag, treeViewChildren));
            
            return isVisible;
        }

        private VisualElement ConfigureTreeViewEntry()
        {
            var entry = _tagEntryUxml.Instantiate();
            var addChildButton = entry.Q<Button>("add-child-button");
            var removeTagButton = entry.Q<Button>("remove-tag-button");

            addChildButton.clicked += () => AddChildTag(entry, addChildButton);
            removeTagButton.clicked += () => DeleteSelectedTag(entry);
            
            return entry;
        }

        private void BindTreeViewEntry(VisualElement entry, int index)
        {
            var tag = _tagTreeView.GetItemDataForIndex<Tag>(index);
            var tagLabel = entry.Q<Label>("tag-name-label");
            var childCountLabel = entry.Q<Label>("child-count-label");
            
            tagLabel.text = tag.TagName;
            childCountLabel.text = $"Children: {tag.ChildrenTags.Count}";
            
            //We gotta save the current bind so we can access from the button clicked event, since we can't re-register the event
            _treeViewBinds[entry] = tag;
        }

        private void AddChildTag(VisualElement entry, Button button)
        {
            var boundTag = _treeViewBinds[entry];
                
            ParentWindow.ShowInputPopup(button.worldBound, $"Create new child Tag of:\n'{boundTag.FullTagName}'", tagName =>
            {
                var tagFullname = $"{boundTag.FullTagName}.{tagName}";

                ParentWindow.CreateAndAddTag(tagFullname);
                PopulateTags();

                var itemId = TagTreeUtils.GenerateIdFromFullName(tagFullname);
                _tagTreeView.SetSelectionById(itemId);
                _tagTreeView.ScrollToItemById(itemId);
            });
        }
        
        private void DeleteSelectedTag(VisualElement entry)
        {
            var boundTag = _treeViewBinds[entry];

            if (EditorUtility.DisplayDialog(TagTreeUtils.LOG_CHANNEL_NAME, "Are you sure you want to delete this Tag?\nNote that deleting a Tag will also delete all children tags and break existing references.", "Yes, delete it", "No, keep it"))
            {
                ParentWindow.RemoveTagRecursive(boundTag);
                PopulateTags();
            }
        }
    }
}
