using System;
using MackySoft.SerializeReferenceExtensions.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

/// <summary>Shared IMGUI list layout for serialized ImpactEffectConfig collections.</summary>
internal sealed class EffectListGUI
{
    private const float FoldoutWidth = 16f;
    private const float HeaderGap = 4f;

    private readonly Action<SerializedObject, string, Type> _onTypeSelected;
    private ReorderableList _list;

    internal EffectListGUI(Action<SerializedObject, string, Type> onTypeSelected)
    {
        _onTypeSelected = onTypeSelected;
    }

    internal void Draw(SerializedProperty property)
    {
        if (_list == null || _list.serializedProperty == null ||
            _list.serializedProperty.serializedObject != property.serializedObject ||
            _list.serializedProperty.propertyPath != property.propertyPath)
            _list = CreateList(property);
        else
            _list.serializedProperty = property;

        int previousSize = property.arraySize;
        Rect header = EditorGUILayout.GetControlRect();
        var label = new GUIContent("Effects", property.tooltip);
        using (new EditorGUI.PropertyScope(header, label, property))
        {
            Rect sizeRect = header;
            sizeRect.xMin = sizeRect.xMax - EditorGUIUtility.fieldWidth;
            header.xMax = sizeRect.xMin - EditorGUIUtility.standardVerticalSpacing;
            property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, label, true);
            EditorGUI.PropertyField(sizeRect, property.FindPropertyRelative("Array.size"), GUIContent.none);
        }

        // Editing Array.size directly bypasses ReorderableList's own add/remove cache invalidation.
        if (property.arraySize != previousSize)
        {
            int selectedIndex = _list.index;
            _list = CreateList(property);
            _list.index = Mathf.Min(selectedIndex, property.arraySize - 1);
        }
        if (property.isExpanded)
            _list.DoLayoutList();
    }

    private ReorderableList CreateList(SerializedProperty property)
    {
        // Keep Unity's list controls without its depth-dependent element label width and padding.
        var list = new ReorderableList(property.serializedObject, property, true, false, true, true)
        {
            headerHeight = 3f,
        };
        list.elementHeightCallback = index => GetElementHeight(
            list.serializedProperty.GetArrayElementAtIndex(index));
        list.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            try
            {
                rect.y += 1f;
                rect.height = GetElementHeight(element);
                using (new EditorGUI.PropertyScope(rect, GUIContent.none, element))
                {
                    Rect header = rect;
                    header.height = EditorGUIUtility.singleLineHeight;
                    Rect typeRect = header;
                    typeRect.xMin += FoldoutWidth + HeaderGap;
                    bool hasType = !string.IsNullOrEmpty(element.managedReferenceFullTypename);
                    if (hasType)
                    {
                        Rect foldout = header;
                        foldout.width = FoldoutWidth;
                        // Inspector hierarchy mode shifts foldout arrows left into the list's drag handle.
                        bool hierarchyMode = EditorGUIUtility.hierarchyMode;
                        try
                        {
                            EditorGUIUtility.hierarchyMode = false;
                            element.isExpanded = EditorGUI.Foldout(
                                foldout, element.isExpanded, GUIContent.none, false);
                        }
                        finally
                        {
                            EditorGUIUtility.hierarchyMode = hierarchyMode;
                        }
                    }
                    if (EditorGUI.DropdownButton(typeRect, GetTypeLabel(element), FocusType.Keyboard))
                        ShowTypeMenu(typeRect, element);

                    if (hasType && element.isExpanded)
                    {
                        // Align immediate fields with the type picker; nested fields retain their own indentation.
                        EditorGUIUtility.labelWidth = Mathf.Min(180f, typeRect.width * 0.6f);
                        Rect fieldRect = typeRect;
                        fieldRect.y += EditorGUIUtility.singleLineHeight;
                        foreach (SerializedProperty child in element.GetChildProperties())
                        {
                            fieldRect.y += EditorGUIUtility.standardVerticalSpacing;
                            fieldRect.height = EditorGUI.GetPropertyHeight(child, true);
                            EditorGUI.PropertyField(fieldRect, child, true);
                            fieldRect.y += fieldRect.height;
                        }
                    }
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        };
        return list;
    }

    private static float GetElementHeight(SerializedProperty element)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (element.isExpanded && !string.IsNullOrEmpty(element.managedReferenceFullTypename))
        {
            foreach (SerializedProperty child in element.GetChildProperties())
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
        }
        return height;
    }

    private static GUIContent GetTypeLabel(SerializedProperty element)
    {
        Type type = ManagedReferenceUtility.GetType(element.managedReferenceFullTypename);
        if (type == null)
            return new GUIContent(TypeMenuUtility.k_NullDisplayName);
        string name = TypeMenuUtility.GetAttribute(type)?.GetTypeNameWithoutPath();
        return new GUIContent(ObjectNames.NicifyVariableName(string.IsNullOrWhiteSpace(name) ? type.Name : name));
    }

    private void ShowTypeMenu(Rect position, SerializedProperty element)
    {
        string path = element.propertyPath;
        SerializedObject serializedObject = element.serializedObject;
        var popup = new AdvancedTypePopup(TypeSearch.GetTypes(typeof(ImpactEffectConfig)), 13,
            new AdvancedDropdownState());
        popup.OnItemSelected += item => _onTypeSelected?.Invoke(serializedObject, path, item.Type);
        popup.Show(position);
    }
}
