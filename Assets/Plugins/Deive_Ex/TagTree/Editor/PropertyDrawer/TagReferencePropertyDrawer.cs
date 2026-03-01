using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DeiveEx.TagTree.Editor
{
    [CustomPropertyDrawer(typeof(TagReference))]
    public class TagReferencePropertyDrawer : PropertyDrawer
    {
        #region SubTypes

        private class FieldData
        {
            public SerializedProperty MainProperty;
            public SerializedProperty TagIdProperty;
            public DropdownField TagDropdown;
            public VisualElement TagDropdownPanel;
        }

        #endregion
        
        #region Fields

        private const string INVALID_TAG_CLASS = "invalid-tag";
        
        private static bool _isInitialized;
        private static VisualTreeAsset _uxmlFile;
        private static bool _needsReload = true;
        private static SerializedObject _settingSerializedObject;

        #endregion

        #region Unity Events

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var rootElement = new VisualElement();

            //Since Unity reuses PropertyDrawers when drawing arrays, we can't store data in the class itself.
            //So, instead, we use the "userData" field, which is unique for each VisualElement
            var state = new FieldData()
            {
                MainProperty = property,
                TagIdProperty = property.FindPropertyRelative("TagId"),
            };
            
            rootElement.userData = state;

            if (!_isInitialized)
                Initialize();

            if(_uxmlFile == null)
                _uxmlFile = LoadAsset<VisualTreeAsset>(nameof(TagReferencePropertyDrawer)); //we assume the uxml has the same name as the cs script

            //We're also forced to pass the root element as a parameter since it would be overriden for each entry in the
            //array if we stored it in the class
            if (_uxmlFile == null)
            {
                BuildErrorGUI(rootElement, "Failed to load UI Layout");
            }
            else
            {
                ReloadTags();

                if (TagManager.IsInitialized)
                {
                    TrackSettingsChanges(rootElement);
                    BuildGUI(rootElement);
                }
                else
                {
                    BuildErrorGUI(rootElement, "Failed to Initialize System. Make sure that the TagTree settings asset exists.");
                    var openEditorButton = new Button(TagTreeEditorWindow.ShowWindow)
                    {
                        text = "Open TagTree Editor"
                    };
                    rootElement.Add(openEditorButton);
                }
            }
            
            return rootElement;
        }

        #endregion

        #region Private Methods

        private static void Initialize()
        {
            TagFilesAssetPostProcessor.TagFilesChanged += () =>  _needsReload = true;
            TagTreeSettingsSO.SettingsChanged += () =>  _needsReload = true;
            
            _isInitialized = true;
            _needsReload = true;
        }

        private void BuildErrorGUI(VisualElement root, string errorMessage)
        {
            root.Add(new HelpBox(errorMessage, HelpBoxMessageType.Error));
            TagTreeUtils.LogMessage(errorMessage, LogType.Error);
        }

        private void BuildGUI(VisualElement root)
        {
            root.Clear();
            var data = GetData(root);
            _uxmlFile.CloneTree(root);
            
            var tagDropdown = root.Q<DropdownField>("tag-selector");
            var dropdownPanel = tagDropdown.ElementAt(1); //Unfortunately, the actual dropdown doesn't have a name/Id, so we need to use their index
            var openTagEditorButton = root.Q<Button>("open-tag-editor");

            data.TagDropdown = tagDropdown;
            data.TagDropdownPanel = dropdownPanel;
            var currentValue = data.TagIdProperty.intValue;

            tagDropdown.label = data.MainProperty.displayName;
            openTagEditorButton.clicked += TagTreeEditorWindow.ShowWindow;
            tagDropdown.RegisterCallback<PointerDownEvent>(e =>
            {
                //Dropdown was not made to be used like a button, so when we register to the PointerDownEvent, it'll react
                //even if we click on the label. It also stops the event propagation, so if we try to register to the
                //PointerDownEvent in a child element, the child will not get the event. Since we want the search window
                //to appear only when we click the actual dropdown, we can check if the click was inside the child bounds instead
                if(!dropdownPanel.worldBound.Contains(e.position))
                    return;
                
                OnDropdownClicked(root);
            });
            
            //Since we're not changing the dropdown value, and we're using the dropdown just as a display/button, we need
            //to track the property directly
            tagDropdown.TrackPropertyValue(data.TagIdProperty, x => UpdateSelectedTagDisplay(root, x.intValue));
            
            //We also want to know when some setting has changed so we can update ourselves
            Action onTagFilesChangedAction = () => OnTagFilesChanged(root);
            tagDropdown.RegisterCallback<AttachToPanelEvent>(_ => TagFilesAssetPostProcessor.TagFilesChanged += onTagFilesChangedAction);
            tagDropdown.RegisterCallback<DetachFromPanelEvent>(_ => TagFilesAssetPostProcessor.TagFilesChanged -= onTagFilesChangedAction);
            
            UpdateSelectedTagDisplay(root, currentValue);
        }

        private void OnDropdownClicked(VisualElement root)
        {
            if(TagManager.Tags.Count == 0)
                return;
            
            var data = GetData(root);
            var tagDropdownSelector = new TagTreeSearchDropdown(new AdvancedDropdownState(), x => SelectTag(data.TagIdProperty, x.Id));
            tagDropdownSelector.SetMinHeight(300);
            tagDropdownSelector.SetMaxHeight(300);
            tagDropdownSelector.Show(data.TagDropdownPanel.worldBound);
        }
        
        private void OnTagFilesChanged(VisualElement root)
        {
            var data = GetData(root);
            ReloadTags();
            UpdateSelectedTagDisplay(root, data.TagIdProperty.intValue);
        }

        private void UpdateSelectedTagDisplay(VisualElement root, int tagId)
        {
            var data = GetData(root);
            
            if (TagManager.Tags.TryGetValue(tagId, out var tag))
            {
                data.TagDropdown.SetValueWithoutNotify(tag.FullTagName);
                root.RemoveFromClassList(INVALID_TAG_CLASS);
            }
            else
            {
                root.AddToClassList(INVALID_TAG_CLASS);
                data.TagDropdown.SetValueWithoutNotify("Tag not found!");
            }
        }

        private void ReloadTags()
        {
            if (!_needsReload || Application.isPlaying)
                return;
            
            TagManager.EditorInitialize();
            TagManager.LoadTagsFromFiles();
            _needsReload = false;
        }
        
        private void SelectTag(SerializedProperty property, int tagId)
        {
            property.intValue = tagId;
            property.serializedObject.ApplyModifiedProperties();
        }
        
        private void TrackSettingsChanges(VisualElement root)
        {
            if(_settingSerializedObject == null || _settingSerializedObject.targetObject == null)
                _settingSerializedObject = new SerializedObject(TagManager.Settings);
            
            var data = GetData(root);
            var loadSourceProperty = _settingSerializedObject.FindProperty(nameof(TagManager.Settings.LoadSource));
            
            root.Unbind();
            root.TrackPropertyValue(loadSourceProperty, _ =>
            {
                ReloadTags();
                UpdateSelectedTagDisplay(root, data.TagIdProperty.intValue);
            });
        }
        
        private T LoadAsset<T>(string assetName) where T : Object
        {
            // Search for the UI document by type and name
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name} {assetName}");

            if (guids.Length == 0)
                return null;
        
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            return asset;
        }

        private FieldData GetData(VisualElement root) => (FieldData)root.userData;

        #endregion
    }
}
