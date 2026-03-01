using UnityEngine.UIElements;

namespace DeiveEx.TagTree.Editor
{
    public abstract class TagTreeEditorPanelBase
    {
        protected TagTreeEditorWindow ParentWindow;
        
        protected VisualElement RootVisualElement => ParentWindow.rootVisualElement;

        protected TagTreeEditorPanelBase(TagTreeEditorWindow parentWindow)
        {
            ParentWindow = parentWindow;
        }

        internal abstract void Build();
        
        protected void ToggleElementVisibility(VisualElement element, bool enable)
        {
            element.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
