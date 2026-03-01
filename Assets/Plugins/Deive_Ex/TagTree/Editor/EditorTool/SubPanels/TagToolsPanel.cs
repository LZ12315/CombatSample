using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DeiveEx.TagTree.Editor
{
    public class TagToolsPanel : TagTreeEditorPanelBase
    {
        private VisualElement _tagToolsPanel;
        private ToolbarSearchField _filterTagInput;
        private Label _noFileLabel;

        public ToolbarSearchField FilterTagInput => _filterTagInput;

        public TagToolsPanel(TagTreeEditorWindow parentWindow) : base(parentWindow) { }
        
        internal override void Build()
        {
            _tagToolsPanel = RootVisualElement.Q<VisualElement>("tag-tools-panel");
            var expandAllButton = _tagToolsPanel.Q<Button>("expand-all-button");
            var collapseAllButton = _tagToolsPanel.Q<Button>("collapse-all-button");
            var createTagButton = _tagToolsPanel.Q<Button>("create-tag-button");
            _filterTagInput = _tagToolsPanel.Q<ToolbarSearchField>("search-tag-input");
            var inputPlaceholderText = _filterTagInput.Q<Label>("placeholder");
            _noFileLabel = RootVisualElement.Q<Label>("no-file-label");

            expandAllButton.clicked += () => ParentWindow.HierarchyPanel.TagTreeView.ExpandAll();
            collapseAllButton.clicked += () => ParentWindow.HierarchyPanel.TagTreeView.CollapseAll();
            
            //Only Unity 2023.1 and above has placeholder support, so we do our own placeholder
            _filterTagInput.RegisterValueChangedCallback(e =>
            {
                inputPlaceholderText.style.display = string.IsNullOrEmpty(e.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                ParentWindow.HierarchyPanel.PopulateTags();
            });

            createTagButton.clicked += () => ParentWindow.ShowInputPopup(createTagButton.worldBound, "Create new Tag:", tagName =>
            {
                ParentWindow.CreateAndAddTag(tagName);
                ParentWindow.HierarchyPanel.PopulateTags();
                
                var itemId = TagTreeUtils.GenerateIdFromFullName(tagName);
                ParentWindow.HierarchyPanel.TagTreeView.SetSelectionById(itemId);
                ParentWindow.HierarchyPanel.TagTreeView.ScrollToItemById(itemId);
            });
            
            ToggleElementVisibility(_noFileLabel, false);
        }
        
        internal void SetPanelVisible(bool isVisible)
        {
            ToggleElementVisibility(_tagToolsPanel, isVisible);

            if (isVisible)
            {
                SetNoFilePanelVisible(false);
            }
            else
            {
                ParentWindow.HierarchyPanel.SetNoTagPanelVisible(false);
                ParentWindow.HierarchyPanel.SetPanelVisible(false);
            }
        }

        internal void SetNoFilePanelVisible(bool isVisible)
        {
            ToggleElementVisibility(_noFileLabel, isVisible);

            if (isVisible)
                SetPanelVisible(false);
        }
    }
}
