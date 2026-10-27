#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Completes native-looking Copy/Paste for the three supported authoring list families.</summary>
[InitializeOnLoad]
internal static class AuthoringListClipboard
{
    internal enum Kind { Conditions, CancelRules, Effects }
    private const string Prefix = "CombatSample.AuthoringList/v1:";
    private const string SessionKey = "CombatSample.AuthoringListClipboard.Session";

    [Serializable]
    private sealed class Snapshot
    {
        public string session;
        public Kind kind;
        public string data;
        public string[] types;
    }

    // Also invalidates outer list heights when a nested Conditions list changes.
    internal static int Revision { get; private set; }
    static AuthoringListClipboard() => Undo.undoRedoPerformed += InvalidateViews;

    private static string Session
    {
        get
        {
            string value = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(value)) SessionState.SetString(SessionKey, value = Guid.NewGuid().ToString("N"));
            return value;
        }
    }

    private static bool TryKind(Type elementType, out Kind kind)
    {
        if (elementType == typeof(ActionCondition)) { kind = Kind.Conditions; return true; }
        if (elementType == typeof(CancelRule)) { kind = Kind.CancelRules; return true; }
        if (elementType == typeof(ImpactEffectConfig)) { kind = Kind.Effects; return true; }
        kind = default;
        return false;
    }

    /// <summary>Only handles list-header context clicks; normal text/element menus remain untouched.</summary>
    internal static bool HandleHeader(Rect header, SerializedProperty property, Type elementType)
    {
        if (Event.current.type != EventType.ContextClick || !header.Contains(Event.current.mousePosition) ||
            !TryKind(elementType, out Kind kind)) return false;
        Event.current.Use();
        try
        {
            // Settle the Inspector's buffered edits before a deferred menu callback uses a fresh view.
            var source = property.serializedObject;
            Object owner = source.targetObject;
            string path = property.propertyPath;
            bool editable = GUI.enabled && property.editable;
            bool multiple = source.isEditingMultipleObjects;
            source.ApplyModifiedProperties();
            string observed = EditorJsonUtility.ToJson(owner);
            var menu = new GenericMenu();
            bool readable = !multiple && !SerializationUtility.HasManagedReferencesWithMissingTypes(owner);
            if (readable) menu.AddItem(new GUIContent("Copy"), false, () => Execute(false));
            else menu.AddDisabledItem(new GUIContent("Copy"));
            if (readable && editable && CanWrite(owner) && CanPaste(kind))
                menu.AddItem(new GUIContent("Paste"), false, () => Execute(true));
            else menu.AddDisabledItem(new GUIContent("Paste"));
            menu.ShowAsContext();

            void Execute(bool paste)
            {
                // Cancel conservatively after ANY host edit (including Undo, removal or reordering).
                // A property path alone can otherwise identify a different list at the same array index.
                try
                {
                    if (owner == null || !string.Equals(observed, EditorJsonUtility.ToJson(owner), StringComparison.Ordinal))
                    {
                        Debug.LogWarning("The list changed while its menu was open. Open the menu again.");
                        return;
                    }
                    using var fresh = new SerializedObject(owner);
                    SerializedProperty current = fresh.FindProperty(path);
                    if (current == null) return;
                    string error = string.Empty;
                    bool success = paste ? editable && Paste(current, kind, out error) : Copy(current, kind, out error);
                    if (!success && !string.IsNullOrEmpty(error)) Debug.LogWarning(error, owner);
                }
                catch (Exception exception) { Debug.LogWarning($"Cannot use list menu: {exception.Message}", owner); }
            }
        }
        catch (Exception exception) { Debug.LogWarning($"Cannot open list menu: {exception.Message}"); }
        return true;
    }

    internal static bool Copy(SerializedProperty property, Kind kind, out string error)
    {
        error = string.Empty;
        AuthoringListClipboardPayload payload = null;
        try
        {
            if (!Validate(property, out error)) return false;
            Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            property.serializedObject.ApplyModifiedProperties();
            using var fresh = new SerializedObject(owner);
            SerializedProperty list = fresh.FindProperty(path);
            if (!Validate(list, out error)) return false;
            payload = CreatePayload();
            var items = payload.Items(kind);
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                items.Add(kind switch
                {
                    Kind.Conditions => (ActionCondition)element.managedReferenceValue,
                    Kind.CancelRules => (CancelRule)element.boxedValue,
                    Kind.Effects => (ImpactEffectConfig)element.managedReferenceValue,
                    _ => throw new ArgumentOutOfRangeException(nameof(kind)),
                });
            }
            // Serialize the entire list in one host, not each element independently. This preserves
            // nested references and sharing/cycles WITHIN the copied graph, without linking to its source.
            string data = EditorJsonUtility.ToJson(payload);
            using var serializedPayload = new SerializedObject(payload);
            var types = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<long>();
            SerializedProperty iterator = serializedPayload.GetIterator();
            bool enter = true;
            while (iterator.Next(enter))
            {
                enter = true;
                if (iterator.propertyType != SerializedPropertyType.ManagedReference) continue;
                object value = iterator.managedReferenceValue;
                enter = value != null && visited.Add(iterator.managedReferenceId);
                if (value != null) types.Add(value.GetType().AssemblyQualifiedName);
            }
            var snapshot = new Snapshot { session = Session, kind = kind, data = data, types = new List<string>(types).ToArray() };
            // Do not replace the system clipboard until the complete snapshot has succeeded.
            EditorGUIUtility.systemCopyBuffer = Prefix + JsonUtility.ToJson(snapshot);
            return true;
        }
        catch (Exception exception) { error = $"Cannot copy list: {exception.Message}"; return false; }
        finally { if (payload != null) Object.DestroyImmediate(payload); }
    }

    internal static bool CanPaste(Kind kind)
    {
        bool valid = TryRead(kind, out var payload, out _);
        if (payload != null) Object.DestroyImmediate(payload);
        return valid;
    }

    internal static bool Paste(SerializedProperty property, Kind kind, out string error)
    {
        error = string.Empty;
        AuthoringListClipboardPayload payload = null;
        try
        {
            if (!Validate(property, out error) || !property.editable) return false;
            Object owner = property.serializedObject.targetObject;
            if (!CanWrite(owner)) { error = "Finish the current Timeline edit before pasting."; return false; }
            if (!TryRead(kind, out payload, out error)) return false;
            // Flush unrelated pending fields before opening a new staging buffer. They are not part
            // of Paste's Undo group and must not be silently discarded by SerializedObject.Update().
            string path = property.propertyPath;
            property.serializedObject.ApplyModifiedProperties();
            using var staged = new SerializedObject(owner);
            SerializedProperty list = staged.FindProperty(path);
            if (!Validate(list, out error)) return false;
            var items = payload.Items(kind);
            if (list.arraySize == 0 && items.Count == 0) return true;
            // Build the entire replacement before modifying the live host or recording Undo.
            list.ClearArray();
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                if (kind == Kind.CancelRules) element.boxedValue = items[i];
                else element.managedReferenceValue = items[i];
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Paste");
            try
            {
                Undo.RegisterCompleteObjectUndo(owner, "Paste");
                staged.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(owner);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
            Undo.CollapseUndoOperations(group);
            if (owner is ActionAsset action && action == ActionEditorContext.Shared.CurrentAction)
                ActionEditorContext.Shared.QueueBindingChange(action, ActionEditorChangeFlags.Content);
            InvalidateViews();
            return true;
        }
        catch (Exception exception) { error = $"Cannot paste list: {exception.Message}"; return false; }
        finally { if (payload != null) Object.DestroyImmediate(payload); }
    }

    private static bool Validate(SerializedProperty property, out string error)
    {
        error = string.Empty;
        if (property == null || !property.isArray || property.propertyType == SerializedPropertyType.String)
        { error = "The list is no longer available."; return false; }
        if (property.serializedObject.isEditingMultipleObjects)
        { error = "Select one object to copy or paste a list."; return false; }
        if (SerializationUtility.HasManagedReferencesWithMissingTypes(property.serializedObject.targetObject))
        { error = "Resolve missing managed-reference types before copying or pasting this object's lists."; return false; }
        return true;
    }

    private static bool CanWrite(Object owner) => owner != null &&
        !(owner is ActionAsset action && action == ActionEditorContext.Shared.CurrentAction && ActionEditorInteractionGate.IsActive);

    private static AuthoringListClipboardPayload CreatePayload()
    {
        var value = ScriptableObject.CreateInstance<AuthoringListClipboardPayload>();
        value.hideFlags = HideFlags.HideAndDontSave;
        return value;
    }

    private static bool TryRead(Kind kind, out AuthoringListClipboardPayload payload, out string error)
    {
        payload = null;
        error = "The clipboard does not contain a compatible list.";
        try
        {
            string text = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(text) || !text.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            var snapshot = JsonUtility.FromJson<Snapshot>(text.Substring(Prefix.Length));
            if (snapshot == null || snapshot.session != Session || snapshot.kind != kind || string.IsNullOrEmpty(snapshot.data) || snapshot.types == null)
                return false;
            foreach (string name in snapshot.types)
            {
                if (string.IsNullOrEmpty(name) || Type.GetType(name, false) == null)
                { error = "A copied type is no longer available. Copy the list again."; return false; }
            }
            payload = CreatePayload();
            EditorJsonUtility.FromJsonOverwrite(snapshot.data, payload);
            payload.hideFlags = HideFlags.HideAndDontSave;
            if (payload.Items(kind) == null || SerializationUtility.HasManagedReferencesWithMissingTypes(payload))
            { error = "The copied list contains missing types."; return false; }
            error = string.Empty;
            return true;
        }
        catch (Exception exception) { error = $"Cannot read copied list: {exception.Message}"; return false; }
    }

    private static void InvalidateViews()
    {
        Revision++;
        InternalEditorUtility.RepaintAllViews();
    }
}
#endif
