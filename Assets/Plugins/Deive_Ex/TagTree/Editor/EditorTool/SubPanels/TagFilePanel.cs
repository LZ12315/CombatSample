using System;
using System.Linq;
using UnityEngine.UIElements;

namespace DeiveEx.TagTree.Editor
{
    public class TagFilePanel : TagTreeEditorPanelBase
    {
        private DropdownField _fileDropdown;
        private Button _saveFileButton;

        internal Button SaveFileButton => _saveFileButton; 
        
        public TagFilePanel(TagTreeEditorWindow parentWindow) : base(parentWindow) { }
        
        internal override void Build()
        {
            var panel = RootVisualElement.Q<VisualElement>("tag-file-panel");
            _fileDropdown = panel.Q<DropdownField>("select-tag-file-dropdown");
            var createFileButton = panel.Q<Button>("create-tag-file-button");
            _saveFileButton = panel.Q<Button>("save-tag-file-button");
            
            _fileDropdown.RegisterValueChangedCallback(e =>
            {
                ParentWindow.CheckForUnsavedChanges();
                LoadTagsFromFile(e.newValue);
            });

            createFileButton.clicked += () => ParentWindow.CreateNewTagFile(createFileButton.worldBound);
            _saveFileButton.clicked += ParentWindow.SaveCurrentFile;
            
            _saveFileButton.SetEnabled(ParentWindow.IsCurrentFileDirty);
        }
        
        internal void PopulateFileDropdown()
        {
            //Populate dropdown with available Tag Files
            _fileDropdown.choices.Clear();
            var tagFiles = TagManager.LoadStrategy.GetFileNames().ToArray();
            
            //Do we have any Tag files?
            if (tagFiles.Length == 0)
            {
                _fileDropdown.SetEnabled(false);
                _fileDropdown.SetValueWithoutNotify("No Tag file found");
                ParentWindow.ToolsPanel.SetNoFilePanelVisible(true);
            }
            else
            {
                _fileDropdown.SetEnabled(true);
                _fileDropdown.choices.AddRange(tagFiles);
                ParentWindow.ToolsPanel.SetPanelVisible(true);

                var lastLoadedFile = ParentWindow.LastLoadedTagFile;
                var fileIndex = Array.IndexOf(tagFiles, lastLoadedFile);

                if (fileIndex < 0)
                    fileIndex = 0;
                
                _fileDropdown.SetValueWithoutNotify(tagFiles[fileIndex]);
            }
        }
        
        internal void LoadTagsFromFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !TagManager.LoadStrategy.ContainFile(fileName))
            {
                ParentWindow.HierarchyPanel.SetPanelVisible(false);
                ParentWindow.HierarchyPanel.SetNoTagPanelVisible(false);
                return;
            }
            
            var fileTags = TagManager.LoadStrategy.GetTagsFromFile(fileName).ToArray();
            TagTreeUtils.LoadTagsFromNames(fileTags, ParentWindow.LoadedTags);
            ParentWindow.LastLoadedTagFile = fileName;

            ParentWindow.HierarchyPanel.PopulateTags();
            ParentWindow.HierarchyPanel.TagTreeView.CollapseAll();
        }
    }
}
