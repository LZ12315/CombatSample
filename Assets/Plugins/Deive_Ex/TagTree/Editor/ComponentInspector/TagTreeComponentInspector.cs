using System.Collections.Generic;
using System.Linq;
using DeiveEx.TagTree.GameObjects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeiveEx.TagTree.Editor
{   
    //Layouts and Colors values in the USS tries to follow Unity standards, which can be referenced here:
    //https://www.foundations.unity.com/fundamentals/color-palette#Window-Backgrounds
    [CustomEditor(typeof(TagTreeComponent))]
    public class TagTreeComponentInspector : UnityEditor.Editor
    {
        #region Fields

        private const string FOLDOUT_STATE_KEY = "TagTreeComponent_Foldout";
        private const string SHOW_HIERARCHY_STATE_KEY = "TagTreeComponent_ShowHierarchy";
        
        [SerializeField] private VisualTreeAsset _mainUxmlFile;
        [SerializeField] private VisualTreeAsset _tagLabelUxml;
        
        private TagTreeComponent _instance;
        private TagContainer _container;
        private VisualElement _root;
        private Label _noTagsLabel;
        private Foldout _tagsFoldout;
        private ListView _tagListView;
        private List<Tag> _visibleTagList = new();

        #endregion

        #region Properties

        private bool IsFoldoutOpen
        {
            get => SessionState.GetBool(FOLDOUT_STATE_KEY, false);
            set => SessionState.SetBool(FOLDOUT_STATE_KEY, value);
        }
        
        private bool ShowFullTagHierarchy
        {
            get => SessionState.GetBool(SHOW_HIERARCHY_STATE_KEY, false);
            set => SessionState.SetBool(SHOW_HIERARCHY_STATE_KEY, value);
        }

        #endregion

        #region Unity Events

        private void OnEnable()
        {
            _instance = (TagTreeComponent)target;
            _container = _instance.gameObject.GetTagContainer();
        }

        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();
            
            //Draw default inspector
            InspectorElement.FillDefaultInspector(_root, serializedObject, this);
            
            //Draw Everything else
            _root.Add(_mainUxmlFile.Instantiate());
            
            _tagsFoldout = _root.Q<Foldout>("tags-foldout");
            _noTagsLabel = _root.Q<Label>("no-tags-label");
            _tagListView = _root.Q<ListView>("tags-list");
            
            _tagsFoldout.value = IsFoldoutOpen;
            _tagsFoldout.RegisterValueChangedCallback(evt =>
            {
                //Since we have child elements that uses the same event, we need to filter this out
                if(evt.target != evt.currentTarget)
                    return;
                
                IsFoldoutOpen = evt.newValue;
            });
            
            if(_container == null || _container.CurrentTags.Count == 0)
                DrawNoTagsPanel();
            else
                DrawTagsPanel();
            
            return _root;
        }

        #endregion

        #region Private Methods

        private void DrawNoTagsPanel()
        {
            ToggleTagsDisplay(false);
        }

        private void DrawTagsPanel()
        {
            ToggleTagsDisplay(true);
            
            var showHierarchyToggle = _root.Q<Toggle>("show-hierarchy-toggle");
            showHierarchyToggle.value = ShowFullTagHierarchy;
            showHierarchyToggle.RegisterValueChangedCallback(evt =>
            {
                ShowFullTagHierarchy = evt.newValue;
                ToggleHierarchyView(evt.newValue);
            });
            
            _tagListView.makeItem = () => _tagLabelUxml.Instantiate();
            _tagListView.bindItem = (element, i) => PopulateTagLabel(_visibleTagList[i], element);
            _tagListView.Rebuild();
            
            ToggleHierarchyView(ShowFullTagHierarchy);
        }

        private void ToggleTagsDisplay(bool showTags)
        {
            _noTagsLabel.style.display = showTags ? DisplayStyle.None : DisplayStyle.Flex;
            _tagListView.style.display = showTags ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        private void ToggleHierarchyView(bool showFullHierarchy)
        {
            _visibleTagList = _container.Tags
                .Where(x => showFullHierarchy || _container.IsLeafTagInContainer(x))
                .OrderBy(x => x.FullTagName)
                .ToList();

            _tagListView.itemsSource = _visibleTagList;
            _tagListView.RefreshItems();
        }
        
        private void PopulateTagLabel(Tag tag, VisualElement entry)
        {
            var tagNameLabel = entry.Q<Label>("tag-name");
            var tagIdLabel = entry.Q<Label>("tag-id");
            var tagCounterLabel = entry.Q<Label>("tag-counter");

            tagNameLabel.text = $"{tag.FullTagName}";
            tagIdLabel.text = $"(id: {tag.Id})";
            tagCounterLabel.text = $"{_container.GetTagCount(tag)}";
        }

        #endregion
    }
}
