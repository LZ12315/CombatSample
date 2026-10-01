using System;
using MackySoft.SerializeReferenceExtensions.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HitFeedbackProfile))]
public sealed class HitFeedbackProfileInspector : Editor
{
    private EffectListGUI _effectList;

    private void OnEnable()
    {
        _effectList = new EffectListGUI(ApplyEffectTypeSelection);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.UpdateIfRequiredOrScript();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        SerializedProperty effects = serializedObject.FindProperty("effects");
        if (effects == null)
        {
            EditorGUILayout.HelpBox("Serialized field 'Effects' is unavailable.", MessageType.Warning);
            return;
        }

        _effectList.Draw(effects);
        serializedObject.ApplyModifiedProperties();
    }

    private void ApplyEffectTypeSelection(SerializedObject source, string path, Type type)
    {
        if (this == null || target == null || source != serializedObject)
            return;
        source.UpdateIfRequiredOrScript();
        SerializedProperty current = source.FindProperty(path);
        if (current == null)
            return;
        current.SetManagedReference(type);
        current.isExpanded = type != null;
        source.ApplyModifiedProperties();
        Repaint();
    }
}
