#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MackySoft.SerializeReferenceExtensions.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

/// <summary>Shared list chrome for Action conditions and cancellation rules.</summary>
internal sealed class ActionAuthoringListGUI
{
    private const float HeaderInset = 20f;
    private const float ElementPadding = 5f;
    private const float TitleSpacing = 4f;
    private const float BottomSpacing = 8f;
    private readonly Action<SerializedObject, string, Type> _onTypeSelected;
    private ReorderableList _list;

    internal ActionAuthoringListGUI(Action<SerializedObject, string, Type> onTypeSelected = null)
    {
        _onTypeSelected = onTypeSelected;
    }

    internal void Reset() => _list = null;

    internal void Draw(SerializedProperty property, string title)
    {
        int indent = EditorGUI.indentLevel;
        bool hierarchy = EditorGUIUtility.hierarchyMode;
        try
        {
            // List depth must not shift foldouts into drag handles or shrink field labels.
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.hierarchyMode = false;
            if (_list == null || _list.serializedProperty.serializedObject != property.serializedObject ||
                _list.serializedProperty.propertyPath != property.propertyPath)
                _list = CreateList(property);
            else
                _list.serializedProperty = property;

            int previousSize = property.arraySize;
            Rect header = EditorGUILayout.GetControlRect();
            var label = new GUIContent(title, property.tooltip);
            using (new EditorGUI.PropertyScope(header, label, property))
            {
                Rect size = header;
                size.xMin = size.xMax - EditorGUIUtility.fieldWidth;
                header.xMax = size.xMin - EditorGUIUtility.standardVerticalSpacing;
                property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, label, true);
                EditorGUI.PropertyField(size, property.FindPropertyRelative("Array.size"), GUIContent.none);
            }
            if (property.arraySize != previousSize)
            {
                int selected = _list.index;
                _list = CreateList(property);
                _list.index = Mathf.Min(selected, property.arraySize - 1);
            }
            if (property.isExpanded)
            {
                // Space the whole list, including its border, handles and footer.
                GUILayout.Space(TitleSpacing);
                _list.DoLayoutList();
                GUILayout.Space(BottomSpacing);
            }
        }
        finally
        {
            EditorGUI.indentLevel = indent;
            EditorGUIUtility.hierarchyMode = hierarchy;
        }
    }

    private ReorderableList CreateList(SerializedProperty property)
    {
        var list = new ReorderableList(property.serializedObject, property, true, false, true, true)
        {
            headerHeight = 3f,
        };
        list.elementHeightCallback = index => ElementHeight(list.serializedProperty.GetArrayElementAtIndex(index));
        list.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
            float labelWidth = EditorGUIUtility.labelWidth;
            try
            {
                rect.y += ElementPadding;
                rect.height = ElementHeight(element) - ElementPadding * 2f;
                EditorGUIUtility.labelWidth = Mathf.Min(180f, rect.width * 0.45f);
                if (_onTypeSelected == null)
                    EditorGUI.PropertyField(rect, element, new GUIContent("Cancel Rule"), true);
                else
                    DrawCondition(rect, element);
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
        };
        list.onAddCallback = current =>
        {
            ReorderableList.defaultBehaviours.DoAddButton(current);
            SerializedProperty element = current.serializedProperty.GetArrayElementAtIndex(current.index);
            // Do not duplicate the last condition/rule when adding a new element.
            if (_onTypeSelected != null)
                element.managedReferenceValue = null;
            else
                element.boxedValue = new CancelRule();
            element.isExpanded = _onTypeSelected == null;
        };
        return list;
    }

    private float ElementHeight(SerializedProperty element)
    {
        float height;
        if (_onTypeSelected == null)
            height = EditorGUI.GetPropertyHeight(element, true);
        else
        {
            height = EditorGUIUtility.singleLineHeight;
            if (element.isExpanded && HasType(element))
            {
                if (TryGetConditionDrawer(element, out PropertyDrawer drawer))
                    height += EditorGUIUtility.standardVerticalSpacing + drawer.GetPropertyHeight(element, GUIContent.none);
                else
                    foreach (SerializedProperty child in VisibleChildren(element))
                        height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
            }
        }
        return height + ElementPadding * 2f;
    }

    private void DrawCondition(Rect rect, SerializedProperty element)
    {
        using (new EditorGUI.PropertyScope(rect, GUIContent.none, element))
        {
            Rect header = rect;
            header.height = EditorGUIUtility.singleLineHeight;
            Rect typeRect = header;
            typeRect.xMin += HeaderInset;
            bool hasType = HasType(element);
            if (hasType)
            {
                Rect foldout = header;
                foldout.width = HeaderInset - 4f;
                element.isExpanded = EditorGUI.Foldout(foldout, element.isExpanded, GUIContent.none, false);
            }
            if (EditorGUI.DropdownButton(typeRect, TypeLabel(element), FocusType.Keyboard))
                ShowTypeMenu(typeRect, element);
            if (!hasType || !element.isExpanded)
                return;

            Rect field = typeRect;
            field.y += header.height + EditorGUIUtility.standardVerticalSpacing;
            if (TryGetConditionDrawer(element, out PropertyDrawer drawer))
            {
                field.height = drawer.GetPropertyHeight(element, GUIContent.none);
                drawer.OnGUI(field, element, GUIContent.none);
            }
            else
            {
                foreach (SerializedProperty child in VisibleChildren(element))
                {
                    field.height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(field, child, true);
                    field.y += field.height + EditorGUIUtility.standardVerticalSpacing;
                }
            }
        }
    }

    private static IEnumerable<SerializedProperty> VisibleChildren(SerializedProperty element)
    {
        var condition = element.managedReferenceValue as ActionCondition;
        foreach (SerializedProperty child in element.GetChildProperties())
        {
            if (child.name == nameof(ActionCondition.invertResult) && condition?.SupportsInversion == false)
                continue;
            yield return child;
        }
    }

    private static bool HasType(SerializedProperty element) =>
        !string.IsNullOrEmpty(element.managedReferenceFullTypename);

    private static bool TryGetConditionDrawer(SerializedProperty element, out PropertyDrawer drawer)
    {
        Type type = ManagedReferenceUtility.GetType(element.managedReferenceFullTypename);
        drawer = null;
        return type != null && PropertyDrawerCache.TryGetPropertyDrawer(type, out drawer);
    }

    private static GUIContent TypeLabel(SerializedProperty element)
    {
        Type type = ManagedReferenceUtility.GetType(element.managedReferenceFullTypename);
        if (type == null)
            return new GUIContent(TypeMenuUtility.k_NullDisplayName);
        string name = TypeMenuUtility.GetAttribute(type)?.GetTypeNameWithoutPath();
        return new GUIContent(ObjectNames.NicifyVariableName(string.IsNullOrWhiteSpace(name) ? type.Name : name));
    }

    private void ShowTypeMenu(Rect rect, SerializedProperty element)
    {
        // Resolve again when the popup closes; never retain a potentially stale array-element property.
        SerializedObject source = element.serializedObject;
        string path = element.propertyPath;
        var popup = new AdvancedTypePopup(TypeSearch.GetTypes(typeof(ActionCondition)), 13, new AdvancedDropdownState());
        popup.OnItemSelected += item => _onTypeSelected(source, path, item.Type);
        popup.Show(rect);
    }
}
#endif
