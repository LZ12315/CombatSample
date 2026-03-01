using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using PopupWindow = UnityEditor.PopupWindow;

namespace DeiveEx.TagTree.Editor
{
    public class TagTreeEditorWindow : EditorWindow
    {
        #region Fields

        private const string EDITOR_WINDOW_TITLE = "TagTree Editor";
        private const string LAST_LOADED_TAG_FILE = "TagTree LastLoadedTagFile";
        
        private static TagTreeEditorWindow _window;
        
        [SerializeField] private VisualTreeAsset _mainUxmlFile;
        [SerializeField] private VisualTreeAsset _initErrorUxml;
        [SerializeField] private VisualTreeAsset _tagEntryUxml;
        [SerializeField] private VisualTreeAsset _newTagPopupUxml;

        internal Dictionary<int, Tag> LoadedTags;
        internal TagFilePanel FilePanel;
        internal TagToolsPanel ToolsPanel;
        internal TagHierarchyPanel HierarchyPanel;
        
        #endregion

        #region Properties

        internal bool IsCurrentFileDirty { get; private set; }
        internal string LastLoadedTagFile
        {
            get => SessionState.GetString(LAST_LOADED_TAG_FILE, null);
            set => SessionState.SetString(LAST_LOADED_TAG_FILE, value);
        }

        #endregion

        #region Unity Events

        [MenuItem("Tools/TagTree Editor")]
        public static void ShowWindow()
        {
            _window = GetWindow<TagTreeEditorWindow>();
            _window.titleContent = new GUIContent(EDITOR_WINDOW_TITLE);
        }

        public void CreateGUI()
        {
            TagFilesAssetPostProcessor.TagFilesChanged -= TagFileChanged;
            TagFilesAssetPostProcessor.TagFilesChanged += TagFileChanged;

            //Clear everything to force a redraw
            rootVisualElement.Clear();

            TagManager.EditorInitialize();
            LoadedTags = new();

            if (!TagManager.IsInitialized)
            {
                CreateNoSettingsPanel();
                return;
            }
            
            CreateMainPanel();
            TrackSettingsChanges();
        }

        private void OnDestroy()
        {
            TagFilesAssetPostProcessor.TagFilesChanged -= TagFileChanged;
            CheckForUnsavedChanges();
        }

        #endregion

        #region Internal Methods

        internal void ShowInputPopup(Rect sourceRect, string popupTitle, Action<string> onConfirm, string placeholder = null)
        {
            PopupWindow.Show(sourceRect, new TagInputPopup(_newTagPopupUxml, popupTitle, onConfirm, placeholder));
        }
        
        internal void CheckForUnsavedChanges()
        {
            if(!IsCurrentFileDirty)
                return;

            if (EditorUtility.DisplayDialog(TagTreeUtils.LOG_CHANNEL_NAME, "You have unsaved changes. Do you want to save them now?", "Save", "Discard"))
                SaveCurrentFile();
            
            SetCurrentFileDirty(false);
        }

        internal void SetCurrentFileDirty(bool isDirty)
        {
            IsCurrentFileDirty = isDirty;
            FilePanel.SaveFileButton.SetEnabled(IsCurrentFileDirty);
        }
        
        internal void CreateNewTagFile(Rect sourceRect)
        {
            ShowInputPopup(sourceRect, "Create new Tag file:", fileName =>
            {
                TagManager.LoadStrategy.CreateTagFile(fileName, "");
                LastLoadedTagFile = fileName;
                AssetDatabase.Refresh();
            }, "File name");
        }
        
        internal void SaveCurrentFile()
        {
            if(LastLoadedTagFile == null)
                return;

            //Saving only leaf tags allows us to decrease the number of entries in the file, which is more manageable
            var leafTags = LoadedTags.Values
                .Where(x => TagTreeUtils.IsLeafTagInCollection(x, LoadedTags.Values))
                .Select(x => x.FullTagName);
            
            TagManager.LoadStrategy.SaveTags(LastLoadedTagFile, leafTags);
            AssetDatabase.Refresh();
            
            SetCurrentFileDirty(false);
        }
        
        internal void CreateAndAddTag(string fullTagName)
        {
            var hierarchy = TagTreeUtils.CreateTagHierarchyFromName(fullTagName);
                
            if(hierarchy == null || hierarchy.Count == 0)
                return;

            foreach (var tag in hierarchy)
            {
                if (!LoadedTags.TryAdd(tag.Id, tag))
                {
                    if(TagManager.Settings.ShowLogs)
                        TagTreeUtils.LogMessage($"Tag '{tag.FullTagName}' already exists!", LogType.Warning);
                }
                else
                    SetCurrentFileDirty(true);
            }
                
            foreach (var tag in hierarchy)
            {
                TagTreeUtils.PopulateParentTag(tag, LoadedTags);
            }
                
            foreach (var tag in hierarchy)
            {
                //Since it's possible for the "hierarchy" array to have a duplicate of a tag that already exists, we
                //have to get the reference from the one that already exists instead of using the one we created here
                var actualReference = TagTreeUtils.GetTagFromId(tag.Id, LoadedTags);
                TagTreeUtils.PopulateTagChildrenRecursive(actualReference, LoadedTags);
            }
        }

        internal void RemoveTagRecursive(Tag targetTag)
        {
            var tagsToRemove = LoadedTags.Values
                .Where(x => x.FullTagName.StartsWith(targetTag.FullTagName))
                .ToList();

            //Remove from the current list
            foreach (var tagToRemove in tagsToRemove)
            {
                LoadedTags.Remove(tagToRemove.Id);
            }

            //Remove any children references
            foreach (var tag in LoadedTags.Values)
            {
                foreach (var tagToRemove in tagsToRemove)
                {
                    if(tag.ChildrenTags is IList<Tag> childList)
                        childList.Remove(tagToRemove);
                }
            }
            
            SetCurrentFileDirty(true);
        }

        #endregion

        #region Private Methods
        
        private void TagFileChanged()
        {
            //Reload the UI
            CreateGUI();
        }

        private void CreateNoSettingsPanel()
        {
            _initErrorUxml.CloneTree(rootVisualElement);
        }

        private void CreateMainPanel()
        {
            _mainUxmlFile.CloneTree(rootVisualElement);

            FilePanel = new(this);
            ToolsPanel = new(this);
            HierarchyPanel = new(this, _tagEntryUxml);
            
            FilePanel.Build();
            ToolsPanel.Build();
            HierarchyPanel.Build();
            
            FilePanel.PopulateFileDropdown();

            //Load the last used file or the first available file
            var tagFileName = LastLoadedTagFile;
            
            if(string.IsNullOrWhiteSpace(tagFileName) || !TagManager.LoadStrategy.ContainFile(tagFileName))
                tagFileName = TagManager.LoadStrategy.GetFileNames().FirstOrDefault();
            
            FilePanel.LoadTagsFromFile(tagFileName);
        }

        private void TrackSettingsChanges()
        {
            var serializedObject = new SerializedObject(TagManager.Settings);
            var loadStrategyProperty =  serializedObject.FindProperty(nameof(TagManager.Settings.LoadSource));
            
            //We need to unbind before tracking new properties
            rootVisualElement.Unbind();
            rootVisualElement.TrackPropertyValue(loadStrategyProperty, _ => CreateGUI());
        }

        #endregion
    }
}
