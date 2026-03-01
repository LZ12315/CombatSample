using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeiveEx.TagTree.Editor
{
    public class TagInputPopup : PopupWindowContent
    {
        private readonly VisualTreeAsset _bodyUxmlFile;
        private readonly Action<string> _onConfirm;
        private readonly Action _onCancel;

        private TextField _inputField;
        private string _title;
        private string _placeholder;

        private VisualElement RootVisualElement => editorWindow.rootVisualElement;

        public TagInputPopup(VisualTreeAsset bodyUxmlFile, string title, Action<string> onConfirm, string placeholderText = null)
        {
            _bodyUxmlFile = bodyUxmlFile;
            _title = title;
            _onConfirm = onConfirm;
            _placeholder = placeholderText;
        }

        public override void OnGUI(Rect rect)
        {
            //no op
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(200, 80);
        }

        public override void OnOpen()
        {
            _bodyUxmlFile.CloneTree(RootVisualElement);
            
            _inputField = RootVisualElement.Q<TextField>("tag-name-input");
            var confirmButton = RootVisualElement.Q<Button>("confirm-button");
            var placeholderLabel = RootVisualElement.Q<Label>("placeholder");
            var titleLabel = RootVisualElement.Q<Label>("title");

            titleLabel.text = _title;
            placeholderLabel.text = _placeholder;
            
            _inputField.RegisterValueChangedCallback(evt =>
            {
                var isValid = !string.IsNullOrEmpty(evt.newValue);
                placeholderLabel.style.display = isValid ? DisplayStyle.None : DisplayStyle.Flex;
                confirmButton.SetEnabled(isValid);
            });
            
            _inputField.RegisterCallback<KeyDownEvent>(e =>
            {
                //Confirm when pressing enter
                if (e.keyCode == KeyCode.Return && !string.IsNullOrEmpty(_inputField.value))
                    Confirm();
                
                if(e.keyCode == KeyCode.Escape)
                    editorWindow.Close();
            });

            confirmButton.clicked += Confirm;
            confirmButton.SetEnabled(false);

            //Elements are not yet attached when we create then, so we can use this event to know when they're attached
            _inputField.RegisterCallback<AttachToPanelEvent>(e => _inputField.Focus());
        }

        private void Confirm()
        {
            _onConfirm(_inputField.value);
            editorWindow.Close();
        }
    }
}
