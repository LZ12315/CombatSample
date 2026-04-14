using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

public class ActionTimelineTargetPickerWindow : EditorWindow
{
    private ActionAsset _actionAsset;
    private List<PlayableDirector> _directors;
    private Vector2 _scroll;

    public static void Show(ActionAsset actionAsset, List<PlayableDirector> directors)
    {
        if (actionAsset == null || directors == null || directors.Count == 0)
            return;

        var window = CreateInstance<ActionTimelineTargetPickerWindow>();
        window.titleContent = new GUIContent("Choose Timeline Target");
        window._actionAsset = actionAsset;
        window._directors = new List<PlayableDirector>(directors);
        window.minSize = new Vector2(460f, 240f);
        window.ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            $"Select which character should open '{_actionAsset?.name ?? "Action"}' in Timeline.",
            EditorStyles.wordWrappedLabel);

        EditorGUILayout.Space(8f);

        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;

            if (_directors != null)
            {
                foreach (var director in _directors)
                {
                    if (director == null)
                        continue;

                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        EditorGUILayout.LabelField(GetDirectorLabel(director), GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("Use", GUILayout.Width(60f)))
                        {
                            ActionAssetHelper.ApplyActionToDirectorAndOpenTimeline(_actionAsset, director);
                            Close();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
        }
    }

    private static string GetDirectorLabel(PlayableDirector director)
    {
        var go = director.gameObject;
        return $"{GetHierarchyPath(go.transform)} [{go.scene.name}]";
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform.parent == null)
            return transform.name;

        return $"{GetHierarchyPath(transform.parent)}/{transform.name}";
    }
}
