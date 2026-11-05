using DeiveEx.TagTree;
using DeiveEx.TagTree.Editor;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEditor;
using UnityEngine;

namespace NodeCanvas.Editor
{
    /// <summary>Connects NodeCanvas's reflected TagReference fields to TagTree's normal picker.</summary>
    public sealed class NodeCanvasTagReferenceDrawer : ObjectDrawer<TagReference>
    {
        public override TagReference OnGUI(GUIContent content, TagReference instance)
        {
            // Drawers are shared and cleared after OnGUI. Dropdown callbacks must capture the
            // original field/owner, not this drawer's context after another node has been drawn.
            var owner = contextUnityObject;
            var boundOwner = GraphEditor.currentGraph == owner ? GraphEditor.targetOwner : null;
            if (boundOwner != null && !boundOwner.graphIsBound)
                boundOwner = null;
            var parent = context;
            var field = fieldInfo;
            Rect rect = EditorGUILayout.GetControlRect();
            TagReferencePropertyDrawer.DrawField(rect, content, instance,
                () =>
                {
                    UndoUtility.RecordObjectComplete(owner, "Set Actor Tag");
                    UndoUtility.RecordObjectComplete(boundOwner, "Set Actor Tag");
                },
                selected =>
                {
                    // Existing references are updated in-place by TagTree (including list items).
                    // Null direct fields need their newly created reference assigned explicitly.
                    if (field != null && parent != null && field.FieldType == typeof(TagReference))
                        field.SetValue(parent, selected);
                    if (owner is Graph graph)
                        graph.SelfSerialize();
                    UndoUtility.SetDirty(owner);
                    UndoUtility.SetDirty(boundOwner);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                });
            return instance;
        }
    }
}
