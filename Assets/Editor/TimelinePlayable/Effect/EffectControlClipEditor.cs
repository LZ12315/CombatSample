//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEngine;

//[CustomEditor(typeof(EffectControlClip))]
//public class EffectControlClipEditor : Editor
//{
//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();

//        var clip = target as EffectControlClip;

//        EditorGUILayout.PropertyField(serializedObject.FindProperty("effectPrefab"));

//        if (clip.effectPrefab != null)
//        {
//            EditorGUILayout.Space();
//            EditorGUILayout.LabelField("变换设置", EditorStyles.boldLabel);
//            EditorGUILayout.PropertyField(serializedObject.FindProperty("localPosition"));
//            EditorGUILayout.PropertyField(serializedObject.FindProperty("localRotation"));
//            EditorGUILayout.PropertyField(serializedObject.FindProperty("localScale"));

//            EditorGUILayout.Space();
//            EditorGUILayout.LabelField("播放设置", EditorStyles.boldLabel);
//            EditorGUILayout.PropertyField(serializedObject.FindProperty("randomSeed"));
//            EditorGUILayout.PropertyField(serializedObject.FindProperty("destroyOnFinish"));
//            EditorGUILayout.PropertyField(serializedObject.FindProperty("previewInEditMode"));
//        }
//        else
//        {
//            EditorGUILayout.HelpBox("请指定一个特效预制体", MessageType.Warning);
//        }

//        serializedObject.ApplyModifiedProperties();
//    }
//}
//#endif