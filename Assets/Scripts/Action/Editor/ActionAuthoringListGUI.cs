#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MackySoft.SerializeReferenceExtensions.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

/// <summary>Shared list chrome for requirements, route conditions, cancellation rules and entries.</summary>
internal sealed class ActionAuthoringListGUI
{
    private const float HeaderInset = 20f;
    private const float ElementPadding = 4f;
    private const float TitleSpacing = 4f;
    private const float BottomSpacing = 6f;
    private readonly Type _elementType;
    private readonly bool _conditions;
    private ReorderableList _list;
    private UnityEngine.Object _viewOwner;
    private string _viewPath;
    private int _clipboardRevision = -1;

    internal ActionAuthoringListGUI(Type elementType = null)
    {
        _elementType = elementType ?? typeof(CancelRule);
        _conditions = typeof(ActionCondition).IsAssignableFrom(_elementType);
    }

    internal void Reset() => _list = null;

    private void Prepare(SerializedProperty property)
    {
        if (_clipboardRevision != AuthoringListClipboard.Revision)
        {
            _clipboardRevision = AuthoringListClipboard.Revision;
            _list = null;
        }
        UnityEngine.Object owner = property.serializedObject.targetObject;
        if (_viewOwner != owner || _viewPath != property.propertyPath)
        {
            _viewOwner = owner;
            _viewPath = property.propertyPath;
            // This is view state only. Once opened, an empty list stays open for native add/remove.
            if (property.arraySize == 0) property.isExpanded = false;
        }
        if (_list == null || _list.serializedProperty.serializedObject != property.serializedObject ||
            _list.serializedProperty.propertyPath != property.propertyPath)
            _list = CreateList(property);
        else
            _list.serializedProperty = property;
    }

    internal float GetHeight(SerializedProperty property)
    {
        Prepare(property);
        return EditorGUIUtility.singleLineHeight + (property.isExpanded
            ? TitleSpacing + _list.GetHeight() + BottomSpacing : 0f);
    }

    internal void Draw(SerializedProperty property, string title)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, GetHeight(property));
        Draw(rect, property, title);
    }

    /// <summary>Rect-based counterpart so nested lists use exactly the same editor as top-level lists.</summary>
    internal void Draw(Rect rect, SerializedProperty property, string title)
    {
        int indent = EditorGUI.indentLevel;
        bool hierarchy = EditorGUIUtility.hierarchyMode;
        try
        {
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.hierarchyMode = false;
            Prepare(property);
            var header = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            var label = new GUIContent(title, property.tooltip);
            if (AuthoringListClipboard.HandleHeader(header, property, _elementType)) return;
            using (new EditorGUI.PropertyScope(header, label, property))
            {
                // Section headers use native foldout spacing, not the list-item drag/type inset.
                property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, label, true);
            }
            if (property.isExpanded)
            {
                rect.y += EditorGUIUtility.singleLineHeight + TitleSpacing;
                rect.height = _list.GetHeight();
                _list.DoList(rect);
            }
        }
        finally
        {
            EditorGUI.indentLevel = indent;
            EditorGUIUtility.hierarchyMode = hierarchy;
        }
    }

    private void InitializeElement(SerializedProperty element)
    {
        // New elements must not duplicate the previous rule, Action reference or input condition.
        if (_conditions)
            element.managedReferenceValue = null;
        else
            element.boxedValue = Activator.CreateInstance(_elementType);
        element.isExpanded = !_conditions;
    }

    private ReorderableList CreateList(SerializedProperty property)
    {
        var list = new ReorderableList(property.serializedObject, property, true, false, true, true)
        {
            headerHeight = 3f,
        };
        if (_conditions)
            list.drawNoneElementCallback = rect => EditorGUI.LabelField(rect, "No additional conditions");
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
                if (_conditions)
                    DrawCondition(rect, element);
                else
                    EditorGUI.PropertyField(rect, element, new GUIContent(ObjectNames.NicifyVariableName(_elementType.Name)), true);
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
        };
        list.onAddCallback = current =>
        {
            ReorderableList.defaultBehaviours.DoAddButton(current);
            InitializeElement(current.serializedProperty.GetArrayElementAtIndex(current.index));
        };
        return list;
    }

    private float ElementHeight(SerializedProperty element)
    {
        float height;
        if (!_conditions)
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

            // Concrete drawers already manage their own child indentation (e.g. Input Steps).
            // Do not add another type-selector inset around their entire contents.
            Rect field = rect;
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
            return new GUIContent("Select Condition", "Choose a condition type. An unconfigured element rejects the condition set.");
        string name = TypeMenuUtility.GetAttribute(type)?.GetTypeNameWithoutPath();
        return new GUIContent(ObjectNames.NicifyVariableName(string.IsNullOrWhiteSpace(name) ? type.Name : name));
    }

    private void ShowTypeMenu(Rect rect, SerializedProperty element)
    {
        // Resolve again after the popup closes; do not retain a stale array-element property.
        UnityEngine.Object owner = element.serializedObject.targetObject;
        string path = element.propertyPath;
        var popup = new AdvancedTypePopup(TypeSearch.GetTypes(typeof(ActionCondition)), 13, new AdvancedDropdownState());
        popup.OnItemSelected += item =>
        {
            if (owner == null) return;
            ActionAsset action = owner as ActionAsset;
            if (action != null && action == ActionEditorContext.Shared.CurrentAction && ActionEditorInteractionGate.IsActive)
                return;
            // The originating Inspector may have been rebuilt or disposed while the popup was open.
            using (var source = new SerializedObject(owner))
            {
                SerializedProperty current = source.FindProperty(path);
                if (current == null || current.propertyType != SerializedPropertyType.ManagedReference) return;
                current.SetManagedReference(item.Type);
                current.isExpanded = item.Type != null;
                if (source.ApplyModifiedProperties() && action != null && action == ActionEditorContext.Shared.CurrentAction)
                    ActionEditorContext.Shared.QueueBindingChange(action, ActionEditorChangeFlags.Content);
            }
            Reset();
            InternalEditorUtility.RepaintAllViews();
        };
        popup.Show(rect);
    }
}

/// <summary>Per-property state, shared by the CancelRule and ActionListEntry drawers.</summary>
internal sealed class ActionConditionListDrawers
{
    internal const float NestedInset = 16f;

    private readonly Dictionary<string, ActionAuthoringListGUI> _lists = new Dictionary<string, ActionAuthoringListGUI>();

    internal ActionAuthoringListGUI For(SerializedProperty property)
    {
        string key = property.serializedObject.targetObject.GetInstanceID() + ":" + property.propertyPath;
        if (!_lists.TryGetValue(key, out ActionAuthoringListGUI list))
            _lists.Add(key, list = new ActionAuthoringListGUI(typeof(ActionCondition)));
        return list;
    }
}
#endif
